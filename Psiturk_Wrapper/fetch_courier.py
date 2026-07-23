#!/usr/bin/env python3
"""
Rebuild regular-Courier data files from the `vconline` MySQL table on cmlpsiturk.

The Unity WebGL build streams each behavioral event to the /save route (see
PsiturkPlugin.jslib). Each POST inserts one row into `vconline` whose `data` column is
a JSON array of {"seq": <int>, "event": "<DataPoint JSON string>"} objects. Batches may
be retried, so events are de-duplicated by (prolific_pid, seq) and ordered by seq.

For every participant this writes the same layout a standalone Courier session produces:

    <OUT_ROOT>/VCBehOnly/<prolific_pid>/session_0/
        session.jsonl                 one event JSON per line, in order
        remaining_items/<store>       unused items per store (from the "remaining items" event)
        <trial>.lst                   presented item names per trial (from presentation events)

Usage:  python3 fetch_courier.py            # table defaults to "vconline"
        python3 fetch_courier.py <table>
"""

import os
import sys
import json
import pymysql

TABLE = sys.argv[1] if len(sys.argv) > 1 else "vconline"
OUT_ROOT = os.path.expanduser("~/courier_data/data")

conn = pymysql.connect(
    host="127.0.0.1",
    user="maint",
    password="strangle.explode.sprout.underfeed.yo-yo",
    database="psiturk",
    charset="utf8mb4",
)


def load_rows():
    with conn.cursor(pymysql.cursors.DictCursor) as cur:
        cur.execute(
            "SELECT id, prolific_pid, session_id, data FROM {} ORDER BY id ASC".format(TABLE)
        )
        return cur.fetchall()


def collect_events(rows):
    """Return {prolific_pid: [event_dict, ...]} ordered by seq, de-duplicated by seq."""
    by_pid = {}
    for r in rows:
        pid = r["prolific_pid"] or "UNKNOWN_PID"
        batch = r["data"]
        if isinstance(batch, (bytes, bytearray)):
            batch = batch.decode("utf-8")
        if isinstance(batch, str):
            batch = json.loads(batch)
        for item in batch:
            seq = item.get("seq")
            event_str = item.get("event")
            try:
                event = json.loads(event_str)
            except (TypeError, ValueError):
                continue
            by_pid.setdefault(pid, {})[seq] = event  # last write wins on retry
    # sort each participant's events by seq
    return {pid: [ev for _, ev in sorted(seqmap.items())] for pid, seqmap in by_pid.items()}


def write_participant(pid, events):
    session_dir = os.path.join(OUT_ROOT, "VCBehOnly", pid, "session_0")
    os.makedirs(session_dir, exist_ok=True)

    # session.jsonl: raw event stream, one JSON object per line (what standalone writes)
    with open(os.path.join(session_dir, "session.jsonl"), "w") as f:
        for ev in events:
            f.write(json.dumps(ev) + "\n")

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

    return len(events)


def main():
    rows = load_rows()
    by_pid = collect_events(rows)
    total_events = 0
    for pid, events in by_pid.items():
        total_events += write_participant(pid, events)
    print("Wrote {} participants, {} events total to {}".format(
        len(by_pid), total_events, OUT_ROOT))


if __name__ == "__main__":
    main()
