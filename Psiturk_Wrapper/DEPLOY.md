# Deploying Courier Online to cmlpsiturk

The live study is served as a **static Unity WebGL build** at
`http://cmlpsiturk.compmemlab.org:9000/exp/VCOnline/`. It does not go through psiTurk's
`/exp` route or the Jinja templates in `templates/`. The Flask app at the same origin answers
`POST /save`, which writes to the `vconline` MySQL table.

```
browser (static page)  ->  PsiturkPlugin.jslib  ->  POST /save  ->  custom.py  ->  vconline
                                                                                      |
                                                             fetch_sql_data.py  <-------
```

## Layout on the server

| What | Where |
|---|---|
| Participant page | `<docroot>/exp/VCOnline/index.html` — deploy `deploy/VCOnline_index.html` |
| WebGL bundle | `<docroot>/exp/VCOnline/Build/VCOnline.{loader.js,data.gz,framework.js.gz,wasm.gz}` |
| Flask route | `custom.py` in the psiTurk app directory |
| Table | `psiturk.vconline` |

## Steps

1. **Schema** (idempotent, safe to re-run):
   ```bash
   mysql -u maint -p psiturk < schema/vconline.sql
   ```
   This creates `vconline` if absent and adds the `load_token` column if absent.

2. **Dependencies:**
   ```bash
   pip install -r requirements.txt      # PsiTurk, psycopg2-binary, pymysql
   python3 -c "import pymysql; print(pymysql.__version__)"
   ```

3. **Server code** — copy `custom.py` and `config.txt`, then **clear stale bytecode**:
   ```bash
   rm -rf __pycache__
   ```
   Committed `.pyc` files previously made it impossible to tell which `custom.py` was live.

4. **Participant page** — copy `deploy/VCOnline_index.html` to
   `<docroot>/exp/VCOnline/index.html`. Keep the two in sync; the deployed copy drifting from
   the repo is what caused the `UNKNOWN_PID` incident (see below).

5. **WebGL bundle** — after any change to `Assets/Plugins/PsiturkPlugin.jslib` you must
   rebuild in Unity and redeploy `Build/`. The jslib is compiled into
   `VCOnline.framework.js.gz`; editing the HTML alone will not pick it up.

6. **Restart psiTurk**, then run the smoke test.

## Smoke test

```bash
curl -i -X POST http://cmlpsiturk.compmemlab.org:9000/save \
  -H 'Content-Type: application/json' \
  -d '{"prolific_pid":"smoketest","study_id":"s","session_id":"x","load_token":"tok1",
       "experiment":"VCBehOnly","table_name":"vconline",
       "data":[{"seq":0,"event":"{\"type\":\"smoke\",\"data\":{},\"time\":0}"}]}'
```

Expect `200 {"success": true, "events": 1}`. Failures now name their cause in both the
response body and `server.log`:

| Response | Cause |
|---|---|
| 404 | blueprint not registered — `custom.py` not deployed, or it failed to import |
| 500 `pymysql not installed` | step 2 not done |
| 500 `table vconline does not exist` | step 1 not done |
| 400 `bad table` | `table_name` not in `ALLOWED_TABLES` |

Then confirm the row and clean up:
```bash
mysql -u maint -p psiturk -e "SELECT id, prolific_pid, load_token, LENGTH(data) FROM vconline WHERE prolific_pid='smoketest';"
mysql -u maint -p psiturk -e "DELETE FROM vconline WHERE prolific_pid='smoketest';"
```

## Verifying participant identity

This is the check that was missing. Load the study URL with real values:

```
http://cmlpsiturk.compmemlab.org:9000/exp/VCOnline/?PROLIFIC_PID=testpid123&STUDY_ID=teststudy&SESSION_ID=testsess
```

The console must log `Courier identity: testpid123 teststudy testsess`. After one flush
(~5 s of play):

```sql
SELECT prolific_pid, study_id, session_id, load_token, COUNT(*)
FROM vconline GROUP BY 1,2,3,4 ORDER BY MAX(id) DESC LIMIT 5;
```

**No row should ever say `UNKNOWN_PID`.** `custom.py` logs an ERROR for each one, and
`fetch_sql_data.py` prints a warning.

## Pulling the data down

`fetch_sql_data.py` handles every experiment in the `psiturk` database, not just Courier:

```bash
python3 ~/fetch_sql_data.py --list                          # known experiments
python3 ~/fetch_sql_data.py VCOnline                        # all rows
python3 ~/fetch_sql_data.py VCOnline --min-id 1 --max-id 99 --out ~/courier_data/run_jul24
python3 ~/fetch_sql_data.py VCOnline --pid 5ece82a1ba09f524e15d973b
python3 ~/fetch_sql_data.py dirFRU                          # directed forgetting
python3 ~/fetch_sql_data.py some_table --dry-run            # unknown names read as tables
```

Two output shapes, chosen by inspecting the `data` column rather than trusting the registry:

| Shape | Tables | Output |
|---|---|---|
| `seq_events` | `vconline` | `<out>/<experiment>/<pid>/session_<N>/` with `session.jsonl`, `remaining_items/<store>`, `<trial>.lst` |
| `trial_rows` | `dirfru`, `dirfr` | `<out>/<table>_trials.csv`, one row per trial |

`seq_events` rows are deduped by `seq` and split into sessions; the script reports any missing
`seq` numbers, which means a batch never reached the server. `trial_rows` have no `seq`, so a
re-delivered batch would duplicate its trials — `row_id` is kept in the CSV so duplicates can
be spotted afterwards.

To add an experiment, add one entry to `EXPERIMENTS` in `fetch_sql_data.py`. Leave
`experiment` as `None` unless you really want to filter that column: `dirfru` stores
`dirFRU`/`dirFRU1..4` there to distinguish sub-studies, so filtering on it would drop rows.

`fetch_sql_data.sh` (repo root) wraps this over ssh and rsyncs the result down. It needs an
`ssh cmlpsiturk` alias in `~/.ssh/config`.

Tests for the session-splitting logic: `python3 -m unittest discover Psiturk_Wrapper/tests`.

## Background: the UNKNOWN_PID incident

Two full runs (2026-07-24, rows 1–99; 2026-07-28, rows 100–261) were collected with
`prolific_pid = 'UNKNOWN_PID'` and empty `study_id`/`session_id`. Prolific was substituting
the URL correctly; the deployed page was Unity's stock template with no query-string parsing,
so `window.prolific_pid` was never set and the jslib fell back to the literal `"UNKNOWN_PID"`.
Nothing surfaced the failure — the client only warned to the console and `/save` logged below
the configured loglevel.

Guards added since: identity resolution lives in the jslib (ships with the build), unset
identifiers are logged as ERROR server-side, `/save` returns real status codes with reasons,
and unsubstituted `{{%PROLIFIC_PID%}}` placeholders are rejected rather than stored.

Note also that `seq` restarts at 0 on every page load, so sessions are separated by
`load_token`. Rows written before that column existed are split by seq restart plus a
content/timestamp check — see `collect_sessions` in `fetch_sql_data.py`.
