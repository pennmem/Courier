grep "ReportScriptedEvent" Assets/Scripts/* | sed 's/.*ReportScriptedEvent("//' | sed 's/".*//' | sort -u
