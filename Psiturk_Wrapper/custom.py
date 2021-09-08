from flask import Blueprint, current_app, request
from json import dump, dumps

custom_code = Blueprint('custom_code', __name__, template_folder='templates', static_folder='static')

@custom_code.route('/save', methods=['GET', 'POST'])
def save():
    try:
        filedata = request.get_json(force=True)
        filename = 'data/%s.json' % filedata['workerId']
        with open(filename, 'w') as f:
            dump(filedata, f)
        return dumps({'success': True}), 200, {'ContentType': 'application/json'}
    except Exception as e:
        current_app.logger.info(e)
        return dumps({'success': False}), 400, {'ContentType': 'application/json'}