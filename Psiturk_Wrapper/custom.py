from flask import Blueprint, current_app, request
from json import dumps
import re
import pymysql

custom_code = Blueprint('custom_code', __name__, template_folder='templates', static_folder='static')

# MySQL-backed /save route matching the dirFRU pipeline. The Unity WebGL build POSTs
# {prolific_pid, study_id, session_id, experiment, table_name, data} and each POST becomes
# one row in the table named by `table_name`. Courier Online uses table "vconline".
#
# NOTE: reconcile this with the live custom.py on cmlpsiturk before deploying — the live
# server already inserts dirFRU rows this way, so it may only need the `vconline` table
# created (see the CREATE TABLE in the deploy notes). Connection settings mirror
# fetch_table.py / config.txt (maint@127.0.0.1/psiturk).

DB_CONFIG = dict(
    host='127.0.0.1',
    user='maint',
    password='strangle.explode.sprout.underfeed.yo-yo',
    database='psiturk',
    charset='utf8mb4',
)

# Only allow tables we expect, so `table_name` from the client can't be used for injection.
ALLOWED_TABLES = {'vconline', 'dirfru', 'dirfr'}


@custom_code.route('/save', methods=['POST'])
def save():
    try:
        filedata = request.get_json(force=True)
        table = filedata.get('table_name', 'vconline')
        if table not in ALLOWED_TABLES or not re.match(r'^[A-Za-z0-9_]+$', table):
            return dumps({'success': False, 'error': 'bad table'}), 400, {'ContentType': 'application/json'}

        conn = pymysql.connect(**DB_CONFIG)
        try:
            with conn.cursor() as cur:
                cur.execute(
                    "INSERT INTO {} (prolific_pid, study_id, session_id, experiment, data) "
                    "VALUES (%s, %s, %s, %s, %s)".format(table),
                    (
                        filedata.get('prolific_pid', ''),
                        filedata.get('study_id', ''),
                        filedata.get('session_id', ''),
                        filedata.get('experiment', ''),
                        dumps(filedata.get('data', [])),
                    ),
                )
            conn.commit()
        finally:
            conn.close()

        return dumps({'success': True}), 200, {'ContentType': 'application/json'}
    except Exception as e:
        current_app.logger.info(e)
        return dumps({'success': False}), 400, {'ContentType': 'application/json'}
