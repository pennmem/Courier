# Handoff — Courier Online data pipeline

**Date:** 2026-07-30
**Branch:** `courier-prolific-ver-2021.3.45f1`
**State:** all changes are **uncommitted and undeployed**. Nothing on cmlpsiturk has been
touched. The server still serves the broken page.

---

## TL;DR

The `/save` → MySQL pipeline was never broken. `vconline` has 261 rows of real behavioral
data. The actual defect was that **every row is attributed to `UNKNOWN_PID`**, because the
page served at `/exp/VCOnline/` is Unity's stock WebGL template and never reads the URL query
string.

One file copy to the server fixes it, with no Unity rebuild:
`Psiturk_Wrapper/deploy/patch_served_index.py` → run against
`<docroot>/exp/VCOnline/index.html`. **Do this first; everything else is hardening.**

---

## What was actually wrong

Investigated in order; the first three were ruled out:

| Suspect | Verdict |
|---|---|
| `vconline` table missing | ❌ exists, correct schema |
| `pymysql` missing / blueprint unregistered | ❌ rows are being written, so `/save` works |
| Prolific not substituting URL placeholders | ❌ preview confirmed real values |
| **Served page never reads the query string** | ✅ **root cause** |

`curl` of `http://cmlpsiturk.compmemlab.org:9000/exp/VCOnline/` returns 200 / 1779 bytes of
Unity's stock template:

```html
<canvas id="unity-canvas" ...></canvas>
<script src="Build/VCOnline.loader.js"></script>
<script>createUnityInstance(..., {dataUrl: "Build/VCOnline.data.gz", ...});</script>
```

No identity code, so `window.prolific_pid` is unset and
[PsiturkPlugin.jslib](Assets/Plugins/PsiturkPlugin.jslib) falls back to the literal
`"UNKNOWN_PID"`. It is neither `templates/exp.html` (7885 B) nor `templates/index.html`
(937 B) — the live deployment is a static build directory that bypasses psiTurk's templates
entirely. `exp.html` is separately stale: it loads `build.loader.js` while the live bundle is
`VCOnline.*`.

Why nobody noticed for two full runs: the client only `console.warn`ed, and `/save` logged at
`logger.info` while `config.txt` sets `loglevel = 2`. Every layer failed silently.

### Existing data

| Rows | When | pid |
|---|---|---|
| 1–99 | 2026-07-24 20:14–20:24 | `UNKNOWN_PID` |
| 100–261 | 2026-07-28 16:56–17:18 | `UNKNOWN_PID` |

`seq` restarts at 0 each page load and the old `fetch_courier.py` deduped with
`by_pid[pid][seq] = event` (last write wins), so **the Jul 28 run was overwriting the Jul 24
run during reconstruction**. Nothing was lost in MySQL. The rewritten script now separates
them automatically.

---

## Changes made (all local)

### The fix
| File | Purpose |
|---|---|
| `Psiturk_Wrapper/deploy/VCOnline_index.html` | The served page + identity block. Version-controls what was previously an untracked server file. |
| `Psiturk_Wrapper/deploy/patch_served_index.py` | Patches the *existing* server file in place. Additive, idempotent, writes `.bak`. Tested against the real fetched page. |

Identity resolution order: URL params → `sessionStorage` → generated `anon_<ts>`. Rejects
unsubstituted `{{%PROLIFIC_PID%}}` placeholders rather than storing them. Also defines
`showCourierEndScreen`, which the stock template lacks.

### Hardening
| File | Change |
|---|---|
| `Assets/Plugins/PsiturkPlugin.jslib` | `$courierGetIdentity` resolves identity inside the bundle (immune to template drift); adds per-page-load `load_token`; escalates repeated save failures to `console.error`; retries the final POST; gates the "data saved" screen on `courierPending` being empty; `finish(false)` on throw. **Needs WebGL rebuild.** |
| `Assets/Scripts/BeginExperiment.cs` | Blocks session ≥ 2 with "Cannot select session 2 or above" on the start screen. **Needs WebGL rebuild.** |
| `Psiturk_Wrapper/custom.py` | Lazy `pymysql` import (missing driver → real 500, not a 404); `logger.exception` + error text in response; 400 vs 500; logs ERROR on missing/`UNKNOWN_PID` identity; stores `load_token` with graceful fallback if the column is absent. |
| `Psiturk_Wrapper/fetch_sql_data.py` | **Replaces `fetch_courier.py`**, generalized to every experiment in the database — see below. Splits sessions by `(pid, load_token)`, falling back to seq-restart + content/timestamp inference for legacy rows; writes `session_N/`; adds `--min-id/--max-id/--pid/--out`; reports missing `seq`. |
| `Psiturk_Wrapper/tests/test_collect_sessions.py` | 12 tests pinning the session-splitting behaviour. |
| `Psiturk_Wrapper/schema/vconline.sql` | Canonical schema + re-runnable `load_token` migration. |
| `Psiturk_Wrapper/requirements.txt` | Added `pymysql`. |
| `Psiturk_Wrapper/DEPLOY.md` | Deploy steps, smoke test, failure table. |

### Cleanup
Removed 5 committed `.pyc` files (`custom.pyc` was boilerplate with no `/save` — it obscured
which `custom.py` was live), dead `templates/index.html`, duplicate `static/js/Unity/build 2`
symlink. `config.txt` now holds the real config (was demo values: `test_db`,
`table_name = courier`, `host = localhost`); `config1.txt` deleted. `.gitignore` now covers
`__pycache__/` and `*.pyc`. `exp.html` carries a header explaining it is not the deployed page.

> `Assets/Scenes/NewTown.unity` was already modified before this work — not part of these changes.

### Multi-experiment fetching

`fetch_courier.py` was replaced by `fetch_sql_data.py`, which handles every experiment in the
`psiturk` database. `fetch_courier.sh` became `fetch_sql_data.sh`.

The tables share a column layout but **not** a `data` payload, which is the key fact:

| Shape | Tables | `data` holds | Output |
|---|---|---|---|
| `seq_events` | `vconline` | `{seq, event}` batches from `PsiturkPlugin.jslib` | session directories |
| `trial_rows` | `dirfru`, `dirfr` | plain array of trial dicts | `<table>_trials.csv` |

So the session-splitting logic applies only to `vconline`. The shape is **detected from the
data**, not taken from the registry, so a mis-registered table still reads correctly.

Adding an experiment is one entry in `EXPERIMENTS`. Leave `experiment` as `None` unless you
truly want to filter that column — `dirfru` stores `dirFRU`/`dirFRU1..4` there to distinguish
sub-studies, so filtering on the registry key would silently drop sessions 1–4.

Replaces the standalone `fetch_table.py` / `fetch_dirfr.py` on the server. Two behaviours of
theirs worth noting: `fetch_dirfr.py` crashed on an empty table (`pd.concat([])`), which is now
handled; and neither deduped, so a re-delivered batch duplicates its trials. That is unchanged
— `vconline` is immune via `seq`, but `trial_rows` tables are not, so `row_id` is kept in the
CSV to make duplicates findable. Also, `fetch_sql_data.py` drops the pandas dependency (stdlib
`csv`), and `fetch_sql_data.sh` fixes a latent bug where the old wrapper hardcoded its rsync
source path and would have copied down stale data if `--out` were ever passed.

---

## Next actions, in order

1. **Patch the served page** (fixes the PID, no rebuild):
   ```bash
   # find it
   find / -path '*VCOnline*' -name 'index.html' 2>/dev/null
   # from laptop
   scp Psiturk_Wrapper/deploy/patch_served_index.py maint@cmlpsiturk.compmemlab.org:~/
   # on server
   python3 ~/patch_served_index.py <path> --check
   python3 ~/patch_served_index.py <path>
   ```

2. **Verify** — load
   `.../exp/VCOnline/?PROLIFIC_PID=testpid123&STUDY_ID=teststudy&SESSION_ID=testsess`,
   confirm the console shows `Courier identity: testpid123 teststudy testsess`, play ~10 s, then:
   ```sql
   SELECT id, prolific_pid, study_id, session_id FROM vconline ORDER BY id DESC LIMIT 3;
   DELETE FROM vconline WHERE prolific_pid='testpid123';
   ```

3. **Recover the existing 261 rows** — the new script separates the two runs on its own:
   ```bash
   python3 ~/fetch_sql_data.py VCOnline
   ```
   It prints per-session event counts and flags any missing `seq`. Use `--min-id/--max-id` to
   force a split if the inference looks wrong.

4. **Deploy server code** — ⚠️ see the warning below before copying `custom.py`. Then
   `rm -rf __pycache__`, `pip install -r requirements.txt`,
   `mysql -u maint -p psiturk < schema/vconline.sql`, restart psiTurk, run the smoke test in
   `DEPLOY.md`.

5. **Rebuild WebGL** to pick up the jslib identity logic, `load_token`, and the session-limit
   check. After this, step 1's page script becomes redundant belt-and-braces.

---

## Warnings

- **Do not blindly overwrite the live `custom.py`.** `/exp/VCOnline/` is served from a path
  the repo's `custom.py` has no route for, so the live copy almost certainly contains extra
  routes. Diff first:
  ```bash
  diff <(ssh maint@cmlpsiturk.compmemlab.org 'cat <path>/custom.py') Psiturk_Wrapper/custom.py
  ```
- **The MySQL password is committed** in `config.txt`, `custom.py`, and `fetch_sql_data.py`,
  and has been typed into shell history. Worth rotating.
- No `ssh cmlpsiturk` alias exists in `~/.ssh/config` on this machine (only
  `rhino2b.rhino.psych.upenn.edu` and `node41`), so `fetch_sql_data.sh` will not run as
  written. Key-based ssh as `maint` and `zrentala` was refused from here.
- The session limit assumes 0-indexing (`NextSessionNumber()` returns 0 online), so
  `MAX_ALLOWED_SESSION = 1` permits sessions 0 and 1. If participants should only ever run
  one session, set it to `0`; the on-screen message derives from the constant.

## Verification already done

- Identity logic executed against the real Prolific preview URL → correct pid/study/session,
  no fallback. Also checked: placeholder URL, empty query, `workerId`, lowercase params.
- `patch_served_index.py` run against the actual fetched page — patches, is idempotent,
  produces valid JS.
- `fetch_sql_data.py` session splitting tested on six cases: two runs days apart, duplicate
  re-delivery, re-queue-then-continue, fast reload, `load_token` present, and no `timestamp`
  column. All correct. (A first attempt using a subset test got two of these wrong — the
  content comparison is load-bearing, don't simplify it away.)
- `custom.py` and `fetch_sql_data.py` compile; `BeginExperiment.cs` compiles clean under
  Roslyn against Unity 2021.3.45f1 assemblies; jslib parses.
- **Not** tested: an actual end-to-end WebGL session, since nothing is deployed and no rebuild
  was run.

## Note for whoever rebuilds

`LanguageSwitch` sits on the greyed-out button's Text and reassigns `.text` **every frame** in
`Update()`. The session-limit error works by disabling that component while the message shows.
If you refactor the start screen, don't just set `.text` — it will be silently overwritten
before it renders.
