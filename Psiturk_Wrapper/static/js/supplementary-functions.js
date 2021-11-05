function randomInt(min, max) {
  min = Math.ceil(min);
  max = Math.floor(max);
  return Math.floor(Math.random() * (max - min)) + min;
}

async function loadSession(condition, counter) {
  // loads counterbalanced resource and starts experiment script
  await $.getScript("static/js/pregenerated_sessions/condition_" + condition + "/" + counter + ".js");
}

function addStimHTMLTags(list) {
    for(var i = 0; i < list.length; i++) {
        list[i] = {stimulus: "<p id='stim'>".concat(list[i].toUpperCase(), "</p>")};
    }
    return list
}

function saveData(filedata) {
  // This call passes session data to the Python function "save" in custom.py
  $.ajax({
    "url": "/save",
    "method": "POST",
    "headers": {
      "datatype": "application/json",
      "content-type": "application/json"
    },
    "processData": false,
    "data": filedata
  });
}