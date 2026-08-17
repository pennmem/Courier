"""Behaviour tests for session splitting in fetch_sql_data.

`collect_sessions` separates one participant's rows into distinct sessions. This matters
because `seq` restarts at 0 on every page load, and events are deduped by seq -- so if two
page loads are merged into one session, the later one silently overwrites the earlier one
event for event.

Rows written before the `load_token` column existed have to be split by inference, and the
hard part is telling apart two cases that both look like "seq went back to 0":

  * a re-delivered batch (the INSERT committed but the client never saw the response, so it
    resent the same events)  -> same session
  * a genuine page reload (fresh events reusing the same seq numbers) -> new session

An earlier implementation used a subset test for this and got two of these cases wrong, so
these tests pin the behaviour down.

Run with:  python3 -m unittest discover Psiturk_Wrapper/tests
"""

import datetime
import json
import os
import sys
import types
import unittest

# fetch_sql_data imports pymysql at module scope; stub it so these run without a database.
if "pymysql" not in sys.modules:
    stub = types.ModuleType("pymysql")
    stub.cursors = types.SimpleNamespace(DictCursor=object)

    class _ProgrammingError(Exception):
        pass

    stub.err = types.SimpleNamespace(ProgrammingError=_ProgrammingError)
    stub.connect = lambda **kwargs: None
    sys.modules["pymysql"] = stub

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import fetch_sql_data  # noqa: E402


BASE_TIME = datetime.datetime(2026, 7, 24, 20, 14, 48)


def row(row_id, pid, seqs, load_token=None, offset_seconds=0, epoch=1000, experiment="VCBehOnly"):
    """One `vconline` row holding a batch of {seq, event} pairs.

    `epoch` shifts the events' absolute timestamps, which is what distinguishes a genuine
    reload (fresh events) from a re-delivered batch (identical events).
    """
    return {
        "id": row_id,
        "prolific_pid": pid,
        "session_id": "",
        "experiment": experiment,
        "load_token": load_token,
        "timestamp": BASE_TIME + datetime.timedelta(seconds=offset_seconds),
        "data": json.dumps([
            {"seq": s, "event": json.dumps({"type": "e", "data": {}, "time": epoch + s})}
            for s in seqs
        ]),
    }


def seqs_per_session(rows):
    """{pid: [[seq, ...] per session]} -- the shape assertions below care about."""
    collected = fetch_sql_data.collect_sessions(rows)
    return {pid: [sorted(s["events"]) for s in sessions]
            for pid, sessions in collected.items()}


class TestCollectSessions(unittest.TestCase):

    def test_runs_days_apart_are_split(self):
        """The real Jul 24 / Jul 28 case: both landed under UNKNOWN_PID."""
        result = seqs_per_session([
            row(1, "UNKNOWN_PID", [0, 1, 2], offset_seconds=0, epoch=1000),
            row(2, "UNKNOWN_PID", [3, 4], offset_seconds=5, epoch=1000),
            row(3, "UNKNOWN_PID", [0, 1], offset_seconds=345600, epoch=9000),
            row(4, "UNKNOWN_PID", [2, 3], offset_seconds=345605, epoch=9000),
        ])
        self.assertEqual(result, {"UNKNOWN_PID": [[0, 1, 2, 3, 4], [0, 1, 2, 3]]})

    def test_redelivered_batch_is_not_a_new_session(self):
        """Committed but the response was lost, so the client resent identical events."""
        result = seqs_per_session([
            row(1, "P", [0, 1, 2], offset_seconds=0),
            row(2, "P", [0, 1, 2], offset_seconds=5),
            row(3, "P", [3, 4], offset_seconds=10),
        ])
        self.assertEqual(result, {"P": [[0, 1, 2, 3, 4]]})

    def test_requeued_batch_resent_with_newer_events(self):
        """A failed flush is re-queued ahead of newer events, so the batch overlaps and grows."""
        result = seqs_per_session([
            row(1, "P", [0, 1], offset_seconds=0),
            row(2, "P", [0, 1, 2, 3], offset_seconds=5),
            row(3, "P", [4], offset_seconds=10),
        ])
        self.assertEqual(result, {"P": [[0, 1, 2, 3, 4]]})

    def test_quick_reload_is_split_on_content(self):
        """Reload 30s later -- too soon for the time-gap rule, so content has to catch it."""
        result = seqs_per_session([
            row(1, "P", [0, 1, 2], offset_seconds=0, epoch=1000),
            row(2, "P", [0, 1], offset_seconds=30, epoch=7777),
        ])
        self.assertEqual(result, {"P": [[0, 1, 2], [0, 1]]})

    def test_load_token_is_authoritative_even_interleaved(self):
        """Once the client sends load_token, no inference is needed."""
        result = seqs_per_session([
            row(1, "P", [0, 1], load_token="t1", offset_seconds=0),
            row(2, "P", [0, 1], load_token="t2", offset_seconds=5),
            row(3, "P", [2], load_token="t1", offset_seconds=10),
            row(4, "P", [2], load_token="t2", offset_seconds=15),
        ])
        self.assertEqual(result, {"P": [[0, 1, 2], [0, 1, 2]]})

    def test_works_without_a_timestamp_column(self):
        """Older tables may not expose `timestamp`; content alone must still split."""
        result = seqs_per_session([
            {"id": 1, "prolific_pid": "P", "session_id": "",
             "data": json.dumps([{"seq": 0, "event": '{"type":"e","data":{},"time":1}'}])},
            {"id": 2, "prolific_pid": "P", "session_id": "",
             "data": json.dumps([{"seq": 0, "event": '{"type":"e","data":{},"time":99}'}])},
        ])
        self.assertEqual(result, {"P": [[0], [0]]})

    def test_missing_seqs_are_preserved_for_gap_reporting(self):
        """A hole means a batch never reached the server; it must not be silently closed up."""
        sessions = fetch_sql_data.collect_sessions([
            row(1, "P", [0, 1]),
            row(2, "P", [4, 5], offset_seconds=5),
        ])
        events = sessions["P"][0]["events"]
        self.assertEqual(sorted(set(range(max(events) + 1)) - set(events)), [2, 3])

    def test_experiment_label_is_carried_through(self):
        """Output directories come from the experiment column, not a hardcoded name."""
        sessions = fetch_sql_data.collect_sessions([row(1, "P", [0], experiment="VCBehOnly")])
        self.assertEqual(sessions["P"][0]["experiment"], "VCBehOnly")

    def test_unparseable_event_is_skipped_not_fatal(self):
        """DataPoint.cs escapes quotes and newlines but not other control characters."""
        rows = [{
            "id": 1, "prolific_pid": "P", "session_id": "", "timestamp": BASE_TIME,
            "data": json.dumps([
                {"seq": 0, "event": '{"type":"ok","data":{},"time":1}'},
                {"seq": 1, "event": '{"type":"broken", BAD}'},
            ]),
        }]
        sessions = fetch_sql_data.collect_sessions(rows)
        self.assertEqual(sorted(sessions["P"][0]["events"]), [0])


class TestDetectPayloadShape(unittest.TestCase):
    """vconline stores {seq, event} batches; dirfru/dirfr store plain trial dicts."""

    def test_seq_envelope_detected(self):
        batch = [{"seq": 0, "event": '{"type":"e","data":{},"time":1}'}]
        self.assertEqual(fetch_sql_data.detect_payload_shape(batch), fetch_sql_data.SHAPE_SEQ_EVENTS)

    def test_trial_rows_detected(self):
        batch = [{"trial": 1, "word": "apple", "rt": 812}]
        self.assertEqual(fetch_sql_data.detect_payload_shape(batch), fetch_sql_data.SHAPE_TRIAL_ROWS)

    def test_empty_batch_is_unknown(self):
        self.assertIsNone(fetch_sql_data.detect_payload_shape([]))


if __name__ == "__main__":
    unittest.main()
