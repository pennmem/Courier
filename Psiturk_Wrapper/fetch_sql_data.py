#!/usr/bin/env python3
"""
Rebuild experiment data files from the psiTurk MySQL database on cmlpsiturk.

Several experiments share the `psiturk` database, one table each, all with the same columns
(id, prolific_pid, study_id, session_id, experiment, data, timestamp). What differs is the
shape of the JSON in the `data` column, and there are two:

  seq_events  (vconline / Courier Online)
      A batch of {"seq": <int>, "event": "<DataPoint JSON string>"} written by
      PsiturkPlugin.jslib. `seq` restarts at 0 on every page load, so rows are grouped into
      sessions and deduped by seq. Output is one directory per session.

  trial_rows  (dirfru / dirfr)
      A plain JSON array of trial dicts. No seq, no dedupe. Output is a flat trials CSV,
      one row per trial, matching what fetch_table.py / fetch_dirfr.py produced.

The shape is detected from the data itself, so a table whose format differs from what the
registry claims is still handled correctly.

Usage:
    python3 fetch_sql_data.py VCOnline
    python3 fetch_sql_data.py dirFRU
    python3 fetch_sql_data.py --list
    python3 fetch_sql_data.py VCOnline --min-id 1 --max-id 99 --out ~/courier_data/run_jul24
    python3 fetch_sql_data.py vconline --dry-run          # unknown names are read as tables
"""

import argparse
import csv
import json
import os
import sys

import pymysql

DEFAULT_OUT_ROOT = os.path.expanduser("~/courier_data/data")

# Credentials come from the environment when set, so deployments can stop relying on the
# literals committed here. Same variable names work for custom.py if it is ever updated.
DB_CONFIG = dict(
    host=os.environ.get("PSITURK_DB_HOST", "127.0.0.1"),
    user=os.environ.get("PSITURK_DB_USER", "maint"),
    password=os.environ.get("PSITURK_DB_PASSWORD",
                            "strangle.explode.sprout.underfeed.yo-yo"),
    database=os.environ.get("PSITURK_DB_NAME", "psiturk"),
    charset="utf8mb4",
)

SHAPE_SEQ_EVENTS = "seq_events"
SHAPE_TRIAL_ROWS = "trial_rows"

# Legacy seq_events rows only: a seq-0 batch arriving after this much silence is a new page
# load, not a re-delivered first batch. Flushes happen every ~5 s, far inside this.
SESSION_GAP_SECONDS = 300

# Column names that have been used for the participant identifier.
PID_COLUMNS = ("prolific_pid", "worker_id", "workerid", "subject", "participant")
OPTIONAL_COLUMNS = ("id", "study_id", "session_id", "experiment", "load_token", "timestamp")


# --------------------------------------------------------------------------- registry

def courier_extras(session_dir, events):
    """Courier-only outputs: remaining_items/<store> and <trial>.lst.

    Both come from event types emitted by DeliveryExperiment.cs; no other experiment has them.
    """
    # remaining_items/<store>: from the final "remaining items" event
    remaining = None
    for ev in events:
        if ev.get("type") == "remaining items":
            remaining = ev.get("data", {})
    if remaining:
        ri_dir = os.path.join(session_dir, "remaining_items")
        os.makedirs(ri_dir, exist_ok=True)
        for store, items in remaining.items():
            with open(os.path.join(ri_dir, store), "w") as f:
                for item in (items or []):
                    f.write(str(item) + "\n")

    # <trial>.lst: presented item names per trial, in serial-position order
    trials = {}
    for ev in events:
        if ev.get("type") == "object presentation begins":
            d = ev.get("data", {})
            trial = d.get("trial number")
            if trial is None:
                continue
            trials.setdefault(trial, []).append((d.get("serial position", 0), d.get("item name")))
    for trial, entries in trials.items():
        entries.sort(key=lambda x: x[0])
        with open(os.path.join(session_dir, "{}.lst".format(trial)), "w") as f:
            for _, name in entries:
                if name is not None:
                    f.write(str(name) + "\n")


# To add an experiment: one entry here. `experiment` filters the experiment column and is
# usually None -- dirFRU for instance stores dirFRU/dirFRU1..4 in that column to distinguish
# sub-studies, so filtering on the registry key would silently drop sessions 1-4.
EXPERIMENTS = {
    "VCOnline": {
        "table": "vconline",
        "experiment": None,
        "shape": SHAPE_SEQ_EVENTS,
        "extras": courier_extras,
        "note": "Courier Online. Rows hold {seq, event} batches; experiment column is VCBehOnly.",
    },
    "dirFRU": {
        "table": "dirfru",
        "experiment": None,
        "shape": SHAPE_TRIAL_ROWS,
        "note": "Directed forgetting (unified). experiment column holds dirFRU / dirFRU1..4.",
    },
    "dirFR": {
        "table": "dirfr",
        "experiment": None,
        "shape": SHAPE_TRIAL_ROWS,
        "note": "Directed forgetting (original).",
    },
}


def resolve_experiment(name):
    """Registry entry for `name`, case-insensitively; unknown names are read as raw tables."""
    for key, cfg in EXPERIMENTS.items():
        if key.lower() == name.lower():
            resolved = dict(cfg)
            resolved["name"] = key
            return resolved
    return {"name": name, "table": name, "experiment": None, "shape": None, "extras": None,
            "note": "not in the registry; treated as a table name"}


# --------------------------------------------------------------------------- loading

def table_columns(conn, table):
    with conn.cursor() as cur:
        cur.execute(
            "SELECT COLUMN_NAME FROM information_schema.COLUMNS "
            "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = %s", (table,))
        return {r[0] for r in cur.fetchall()}


def load_rows(conn, table, min_id=None, max_id=None, pid=None, experiment=None):
    """Rows ordered by id, selecting only columns the table actually has."""
    columns = table_columns(conn, table)
    if not columns:
        raise SystemExit("Table `{}` does not exist in database `{}`."
                         .format(table, DB_CONFIG["database"]))

    pid_column = next((c for c in PID_COLUMNS if c in columns), None)
    if "data" not in columns or pid_column is None:
        raise SystemExit(
            "Table `{}` is not in a recognised layout.\n"
            "  needs: a `data` column and one of {}\n"
            "  found: {}".format(table, ", ".join(PID_COLUMNS), ", ".join(sorted(columns))))

    selected = [c for c in OPTIONAL_COLUMNS if c in columns] + [pid_column, "data"]

    where, params = [], []
    if min_id is not None and "id" in columns:
        where.append("id >= %s")
        params.append(min_id)
    if max_id is not None and "id" in columns:
        where.append("id <= %s")
        params.append(max_id)
    if pid is not None:
        where.append("`{}` = %s".format(pid_column))
        params.append(pid)
    if experiment is not None and "experiment" in columns:
        where.append("experiment = %s")
        params.append(experiment)

    sql = "SELECT {} FROM `{}`{} ORDER BY {}".format(
        ", ".join("`{}`".format(c) for c in selected),
        table,
        (" WHERE " + " AND ".join(where)) if where else "",
        "id ASC" if "id" in columns else ("timestamp ASC" if "timestamp" in columns else "1"))

    with conn.cursor(pymysql.cursors.DictCursor) as cur:
        cur.execute(sql, params)
        rows = cur.fetchall()

    # Normalise the identifier column so downstream code only knows one name.
    if pid_column != "prolific_pid":
        for r in rows:
            r["prolific_pid"] = r.get(pid_column)
    return rows


def decode_batch(raw):
    if isinstance(raw, (bytes, bytearray)):
        raw = raw.decode("utf-8")
    if isinstance(raw, str):
        raw = json.loads(raw)
    return raw


def detect_payload_shape(batch):
    """SHAPE_SEQ_EVENTS if the batch is the {seq, event} envelope, else SHAPE_TRIAL_ROWS."""
    if not batch:
        return None
    first = batch[0]
    if isinstance(first, dict) and "seq" in first and "event" in first:
        return SHAPE_SEQ_EVENTS
    return SHAPE_TRIAL_ROWS


# --------------------------------------------------------- seq_events: session splitting

def collect_sessions(rows):
    """{pid: [session, ...]} where session = {events: {seq: event}, experiment, load_token}.

    Sessions are keyed by (pid, load_token) when the client sent one. Legacy rows have none,
    so a session boundary is inferred from seq restarting at 0 -- distinguishing a genuine
    page reload from a re-delivered batch by comparing event content, with a long time gap as
    a second trigger. See tests/test_collect_sessions.py.
    """
    sessions = {}       # pid -> list of session dicts
    token_index = {}    # (pid, load_token) -> index into sessions[pid]
    last_ts = {}        # pid -> timestamp of the most recent row seen

    for r in rows:
        pid = r.get("prolific_pid") or "UNKNOWN_PID"
        batch = decode_batch(r["data"])
        if not batch:
            continue

        parsed = []
        for item in batch:
            if not isinstance(item, dict):
                continue
            try:
                parsed.append((item.get("seq"), json.loads(item.get("event"))))
            except (TypeError, ValueError):
                # A DataPoint whose value contained an unescaped control character; skip it
                # rather than losing the whole batch.
                continue
        if not parsed:
            continue

        sessions.setdefault(pid, [])
        last_ts.setdefault(pid, None)
        token = r.get("load_token")
        ts = r.get("timestamp")

        def new_session():
            sessions[pid].append({"events": {},
                                  "experiment": r.get("experiment"),
                                  "load_token": token})

        if token:
            key = (pid, token)
            if key not in token_index:
                token_index[key] = len(sessions[pid])
                new_session()
            idx = token_index[key]
        elif not sessions[pid]:
            new_session()
            idx = 0
        else:
            idx = len(sessions[pid]) - 1
            current = sessions[pid][idx]["events"]

            # Legacy rows restart seq at 0 for two reasons: the page reloaded, or a batch that
            # was committed but whose response the client never saw got re-delivered. A
            # re-delivery repeats the same events verbatim; a new page load emits fresh events
            # (different absolute timestamps) at the same seq numbers.
            if min(s for s, _ in parsed) == 0 and current:
                overlap = [(s, ev) for s, ev in parsed if s in current]
                is_redelivery = bool(overlap) and all(current[s] == ev for s, ev in overlap)
                # Belt and braces: a long silence before a seq-0 batch is a new page load
                # regardless of content (this separates runs days apart).
                stale = (ts is not None and last_ts[pid] is not None
                         and (ts - last_ts[pid]).total_seconds() > SESSION_GAP_SECONDS)
                if stale or not is_redelivery:
                    new_session()
                    idx += 1

        session = sessions[pid][idx]
        for seq, event in parsed:
            session["events"][seq] = event      # last write wins on retry
        if session.get("experiment") is None:
            session["experiment"] = r.get("experiment")
        if ts is not None:
            last_ts[pid] = ts

    return sessions


def write_session(out_root, experiment_label, pid, index, events, extras):
    session_dir = os.path.join(out_root, experiment_label, pid, "session_{}".format(index))
    os.makedirs(session_dir, exist_ok=True)

    # session.jsonl: raw event stream, one JSON object per line -- what the standalone
    # desktop build writes, so downstream analysis is identical.
    with open(os.path.join(session_dir, "session.jsonl"), "w") as f:
        for ev in events:
            f.write(json.dumps(ev) + "\n")

    if extras:
        extras(session_dir, events)
    return session_dir


def run_seq_events(rows, cfg, out_root, dry_run):
    by_pid = collect_sessions(rows)
    total_events = total_sessions = total_missing = 0

    for pid, sessions in sorted(by_pid.items()):
        for i, session in enumerate(sessions):
            seqmap = session["events"]
            events = [ev for _, ev in sorted(seqmap.items())]
            label = session.get("experiment") or cfg.get("experiment") or cfg["name"]

            if not dry_run:
                write_session(out_root, label, pid, i, events, cfg.get("extras"))

            total_sessions += 1
            total_events += len(events)

            # A hole in seq means a batch never reached the server; those events are gone.
            missing = set(range(max(seqmap) + 1)) - set(seqmap)
            total_missing += len(missing)
            line = "  {}/{} session_{}: {} events".format(label, pid, i, len(events))
            if missing:
                sample = sorted(missing)[:10]
                line += "  MISSING {} seq(s): {}{}".format(
                    len(missing), sample, "..." if len(missing) > len(sample) else "")
            print(line)

        if pid == "UNKNOWN_PID":
            print("  WARNING: {} session(s) have no participant id -- the hosting page is not "
                  "setting window.prolific_pid (see DEPLOY.md)".format(len(sessions)))

    print("{} {} participants, {} sessions, {} events{}".format(
        "Would write" if dry_run else "Wrote",
        len(by_pid), total_sessions, total_events,
        "" if dry_run else " to " + out_root))
    if total_missing:
        print("WARNING: {} event(s) missing across all sessions -- some batches never reached "
              "the server".format(total_missing))


# ------------------------------------------------------------------ trial_rows: flat CSV

def run_trial_rows(rows, cfg, out_root, dry_run):
    """Flatten each row's `data` array into one CSV row per trial.

    Matches fetch_table.py / fetch_dirfr.py, minus their pandas dependency. Note these rows
    carry no seq, so a re-delivered batch would duplicate its trials; `row_id` is kept so
    duplicates can be identified after the fact.
    """
    trials, columns = [], []
    seen_columns = set()

    for r in rows:
        batch = decode_batch(r["data"])
        if not batch:
            continue
        for trial in batch:
            if not isinstance(trial, dict):
                trial = {"value": trial}
            record = dict(trial)
            record.update({
                "prolific_pid": r.get("prolific_pid"),
                "study_id": r.get("study_id"),
                "session_id": r.get("session_id"),
                "experiment": r.get("experiment"),
                "row_id": r.get("id"),
            })
            for k in record:
                if k not in seen_columns:
                    seen_columns.add(k)
                    columns.append(k)
            trials.append(record)

    participants = len({r.get("prolific_pid") for r in rows})
    by_experiment = {}
    for t in trials:
        by_experiment[t.get("experiment")] = by_experiment.get(t.get("experiment"), 0) + 1

    if dry_run:
        print("Would write {} trial rows from {} participants".format(len(trials), participants))
        for exp, n in sorted(by_experiment.items(), key=lambda kv: str(kv[0])):
            print("  {}: {} trials".format(exp, n))
        return

    os.makedirs(out_root, exist_ok=True)
    out_path = os.path.join(out_root, "{}_trials.csv".format(cfg["table"]))
    with open(out_path, "w", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=columns, extrasaction="ignore")
        writer.writeheader()
        for t in trials:
            writer.writerow(t)

    print("Wrote {} trial rows from {} participants to {}".format(
        len(trials), participants, out_path))
    for exp, n in sorted(by_experiment.items(), key=lambda kv: str(kv[0])):
        print("  {}: {} trials".format(exp, n))


# --------------------------------------------------------------------------- entry point

def parse_args(argv):
    p = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("experiment", nargs="?",
                   help="registry name ({}) or a raw table name".format(
                       ", ".join(sorted(EXPERIMENTS))))
    p.add_argument("--table", default=None, help="override the registry's table")
    p.add_argument("--out", default=DEFAULT_OUT_ROOT,
                   help="output root (default: %(default)s)")
    p.add_argument("--min-id", type=int, default=None, help="only rows with id >= this")
    p.add_argument("--max-id", type=int, default=None, help="only rows with id <= this")
    p.add_argument("--pid", default=None, help="only this participant id")
    p.add_argument("--shape", choices=[SHAPE_SEQ_EVENTS, SHAPE_TRIAL_ROWS], default=None,
                   help="force the payload shape instead of detecting it")
    p.add_argument("--list", action="store_true", help="list known experiments and exit")
    p.add_argument("--dry-run", action="store_true", help="report only, write nothing")
    return p.parse_args(argv)


def main(argv=None):
    args = parse_args(sys.argv[1:] if argv is None else argv)

    if args.list:
        print("Known experiments:\n")
        for name, cfg in sorted(EXPERIMENTS.items()):
            print("  {:<10} table={:<10} shape={:<11} {}".format(
                name, cfg["table"], cfg.get("shape") or "auto", cfg.get("note", "")))
        print("\nAny other name is treated as a table name.")
        return 0

    if not args.experiment:
        raise SystemExit("Give an experiment or table name, or --list. See --help.")

    cfg = resolve_experiment(args.experiment)
    if args.table:
        cfg["table"] = args.table

    conn = pymysql.connect(**DB_CONFIG)
    try:
        rows = load_rows(conn, cfg["table"], args.min_id, args.max_id, args.pid,
                         cfg.get("experiment"))
    finally:
        conn.close()

    if not rows:
        print("No rows matched in `{}`.".format(cfg["table"]))
        return 0

    # Trust the data over the registry: a table whose format differs from what the registry
    # claims is still handled correctly.
    detected = next((s for s in (detect_payload_shape(decode_batch(r["data"])) for r in rows)
                     if s is not None), None)
    shape = args.shape or detected or cfg.get("shape") or SHAPE_TRIAL_ROWS
    if cfg.get("shape") and detected and detected != cfg["shape"] and not args.shape:
        print("note: `{}` rows look like {}, not the expected {}; using {}".format(
            cfg["table"], detected, cfg["shape"], detected))

    out_root = os.path.expanduser(args.out)
    print("{} ({} rows from `{}`, shape={})".format(
        cfg["name"], len(rows), cfg["table"], shape))

    if shape == SHAPE_SEQ_EVENTS:
        run_seq_events(rows, cfg, out_root, args.dry_run)
    else:
        run_trial_rows(rows, cfg, out_root, args.dry_run)
    return 0


if __name__ == "__main__":
    sys.exit(main())
