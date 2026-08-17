from flask import Blueprint, current_app, request
from json import dumps
import re

custom_code = Blueprint('custom_code', __name__, template_folder='templates', static_folder='static')

# MySQL-backed /save route matching the dirFRU pipeline. The Unity WebGL build POSTs
# {prolific_pid, study_id, session_id, load_token, experiment, table_name, data} and each POST
# becomes one row in the table named by `table_name`. Courier Online uses table "vconline".
#
# See schema/vconline.sql for the table definition and DEPLOY.md for the deploy steps.

DB_CONFIG = dict(
    host='127.0.0.1',
    user='maint',
    password='strangle.explode.sprout.underfeed.yo-yo',
    database='psiturk',
    charset='utf8mb4',
)

# Only allow tables we expect, so `table_name` from the client can't be used for injection.
ALLOWED_TABLES = {'vconline', 'dirfru', 'dirfr'}

# A participant whose identity never resolved. Rows are still stored -- partial data beats no
# data -- but every one of them is unusable for analysis, so say so loudly. Two full sessions
# were collected under this id before anyone noticed.
MISSING_PID = 'UNKNOWN_PID'

MYSQL_UNKNOWN_COLUMN = 1054
MYSQL_NO_SUCH_TABLE = 1146


def _json(payload, status):
    return dumps(payload), status, {'ContentType': 'application/json'}


@custom_code.route('/save', methods=['POST'])
def save():
    # Imported here rather than at module scope on purpose: a missing driver at import time
    # would make psiTurk's import of this blueprint raise, so /save would never register and
    # every POST would 404 -- indistinguishable from "the route doesn't exist". Failing inside
    # the handler yields a real 500 that names the problem.
    try:
        import pymysql
    except ImportError:
        current_app.logger.exception('/save: pymysql is not installed (pip install -r requirements.txt)')
        return _json({'success': False, 'error': 'pymysql not installed on the server'}, 500)

    try:
        filedata = request.get_json(force=True)
    except Exception as e:
        current_app.logger.warning('/save: unparseable request body: %s', e)
        return _json({'success': False, 'error': 'malformed JSON body'}, 400)

    if not isinstance(filedata, dict):
        return _json({'success': False, 'error': 'body must be a JSON object'}, 400)

    table = filedata.get('table_name', 'vconline')
    if table not in ALLOWED_TABLES or not re.match(r'^[A-Za-z0-9_]+$', table):
        current_app.logger.warning('/save: rejected table_name %r', table)
        return _json({'success': False, 'error': 'bad table'}, 400)

    prolific_pid = filedata.get('prolific_pid', '')
    study_id = filedata.get('study_id', '')
    session_id = filedata.get('session_id', '')
    load_token = filedata.get('load_token', '')
    experiment = filedata.get('experiment', '')
    events = filedata.get('data', [])

    if not prolific_pid or prolific_pid == MISSING_PID:
        current_app.logger.error(
            '/save: participant identity missing (prolific_pid=%r, study_id=%r, session_id=%r). '
            'The hosting page is not setting window.prolific_pid -- see DEPLOY.md. Storing the '
            'row anyway, but it cannot be attributed to a participant.',
            prolific_pid, study_id, session_id)

    try:
        conn = pymysql.connect(**DB_CONFIG)
        try:
            with conn.cursor() as cur:
                params = (prolific_pid, study_id, session_id, load_token, experiment, dumps(events))
                try:
                    cur.execute(
                        "INSERT INTO {} (prolific_pid, study_id, session_id, load_token, experiment, data) "
                        "VALUES (%s, %s, %s, %s, %s, %s)".format(table),
                        params,
                    )
                except pymysql.err.ProgrammingError as e:
                    # Tolerate a server that hasn't had the load_token migration applied yet, so
                    # deploying the code before the ALTER doesn't drop a session on the floor.
                    if e.args and e.args[0] == MYSQL_UNKNOWN_COLUMN and 'load_token' in str(e):
                        current_app.logger.warning(
                            '/save: %s has no load_token column; storing without it. Run '
                            'schema/vconline.sql to migrate.', table)
                        cur.execute(
                            "INSERT INTO {} (prolific_pid, study_id, session_id, experiment, data) "
                            "VALUES (%s, %s, %s, %s, %s)".format(table),
                            (prolific_pid, study_id, session_id, experiment, dumps(events)),
                        )
                    else:
                        raise
            conn.commit()
        finally:
            conn.close()
    except Exception as e:
        # logger.exception, not logger.info: the previous version logged below the configured
        # loglevel and returned no reason, so failures left no trace anywhere.
        current_app.logger.exception('/save: insert into %s failed for pid=%r', table, prolific_pid)
        code = e.args[0] if getattr(e, 'args', None) else None
        if code == MYSQL_NO_SUCH_TABLE:
            return _json({'success': False,
                          'error': 'table {} does not exist; run schema/vconline.sql'.format(table)}, 500)
        return _json({'success': False, 'error': '{}: {}'.format(type(e).__name__, e)}, 500)

    current_app.logger.info('/save: stored %d events for pid=%s token=%s into %s',
                            len(events) if hasattr(events, '__len__') else -1,
                            prolific_pid, load_token, table)
    return _json({'success': True, 'events': len(events) if hasattr(events, '__len__') else None}, 200)
