async function runExperiment() {

  /* - - - - PSITURK - - - - */

  var psiturk = new PsiTurk(uniqueId, adServerLoc, mode);

  // Record screen resolution & available screen size
  psiturk.recordUnstructuredData('screen_width', screen.width)
  psiturk.recordUnstructuredData('screen_height', screen.height)
  psiturk.recordUnstructuredData('avail_screen_width', screen.availWidth)
  psiturk.recordUnstructuredData('avail_screen_height', screen.availHeight)
  psiturk.recordUnstructuredData('color_depth', screen.colorDepth)
  psiturk.recordUnstructuredData('pixel_depth', screen.pixelDepth)

  var pages = [
  ];

  psiturk.preloadPages(pages);


  /* - - - - SETTINGS - - - - */

  var rec_dur = 4000; // Duration of the recall per item
  var post_rec_dur = 1000; // Duration of blank screen after recall period
  var warning_duration = 5000;

  var pr = 1000;
  var pr_jitter = 200;

  var isi = 500;
  var isi_jitter = 200;


  /* - - - - INSTRUCTIONS - - - - */

  var instructions = {
   type: 'instructions',
   pages: [
          "<p id='inst'>Describe what we are doing here!</p><p id='inst'>Press space to continue</p>",
          "<p id='inst'>Describe what we are doing here!</p><p id='inst'>Press space to begin</p>"
          ],
   key_forward: ' ',
   allow_backward: false,
 };

 var loop_practice = {
  type: 'html-keyboard-response',
  stimulus: "<p id='inst'>Press c to continue on to the task or r to repeat the practice.</p>",
  response_ends_trial: true,
  choices: ['r', 'c'],
 }

 psiturk.finishInstructions();


  /* - - - - START LIST - - - - */

  function generateListStart(list) {

    return {
      type: 'hold-keys',
      response_ends_trial: true,
      choices: ['z', 'p'], //FIXME: make configurable
      stimulus: "<p id='stim'>Hold down the z and p keys to start the next list and continue holding " +
                      "them through the list presentation period.</p>"
    }

  }


  /* - - - - COUNTDOWN - - - - */

  function generateCountdown(list) {
    jittered_isi = isi + randomInt(0, isi_jitter) 

    return {
      type: 'countdown',
      seconds: 10,
      post_trial_gap: jittered_isi,
      data: {type: 'countdown', isi: jittered_isi }
    }

  }
  

  /* - - - - STIMULI - - - - */

  // Generate word lists based on the list lengths and presentation rates

  function generateEncoding(list) {
    jittered_isi = isi + randomInt(0, isi_jitter) 
    jittered_pr = pr + randomInt(0, pr_jitter)

    return {
      type: 'hold-keys',
      trial_duration: jittered_pr + jittered_isi, 
      stimulus_duration: jittered_pr,
      response_ends_trial: false,
      choices: ['z', 'p'],
      timeline: addStimHTMLTags(list.words),
      data: {type: "encoding", conditions: list.conditions, isi: jittered_isi, pr: jittered_pr}
    }
  }


  /* - - - - DISTRACTOR - - - - */

  function generateDistractor(list) {
    return {
      type: 'math-distractor',
      trial_duration: 12000,
      post_trial_gap: 2000, // Long delay so that people do not acidentally start the next trial by hitting a key after the math ends
      data: {type: "math", conditions: list.conditions}
    }
  }


  /* - - - - FREE RECALL - - - - */

  function generateFreeRecall(list) {

    return {
      type: 'free-recall',
      post_trial_gap: post_rec_dur,
      preamble: '',
      questions: '',
      trial_duration: rec_dur*list.words.length,
      data: {type: "recall", conditions: list.conditions},
      on_finish: function() {
          saveData(JSON.stringify(psiturk.taskdata.toJSON()));
      }
    }

  }


  /* - - - - END LIST - - - - */

  function generateListEnd(list) {

    return {
        type: 'hold-keys-check',
        message_true: "",
        message_false: "",
        trial_duration: 0,
        keys: ["z", "p"],
        data: {type: 'check'}
    }

  }
  
  function generatePracticeEnd(list) {

    return {
        type: 'hold-keys-check',
        message_true: "<p id='inst'>You may release the keys once the study period has ended.</p>",
        message_false: "<p id='inst'>Please remember to hold the keys throughout the study period.</p>",
        trial_duration: warning_duration,
        keys: ["z", "p"],
        data: {type: 'check'}
    }

  }


  /* - - - - BLOCKING - - - - */

  function generatePracticeBlock(list) {
    start = generateListStart(list)
    countdown = generateCountdown(list);
    encoding = generateEncoding(list);
    distractor = generateDistractor(list);
    recall = generateFreeRecall(list);
    
    end = generatePracticeEnd(list);
    
    return [start, countdown, encoding, end, recall]
  }

  function generateTestBlock(list) {
    start = generateListStart(list)
    countdown = generateCountdown(list);
    encoding = generateEncoding(list);
    distractor = generateDistractor(list);
    recall = generateFreeRecall(list);
    
    end = generateListEnd(list);
    
    return [start, countdown, encoding, end, recall]
  }


  /* - - - - PRACTICE - - - - */

  // loads list objects into global pregenerated_practice_lists variable
  await loadSession("practice", "practice")

  function practice_loop_func(data) {
      if('r' == data.values()[0].key_press){
          return true;
      } else {
          return false;
      }
  }

  var practice_timeline = [instructions]
  for(var i = 0; i < pregenerated_practice_lists.length; i++) {
    practice_timeline = practice_timeline.concat( generatePracticeBlock(pregenerated_practice_lists[i]) )
  }

  practice_timeline = practice_timeline.concat(loop_practice)

  var practice_node = {timeline: practice_timeline,
                       loop_function: practice_loop_func}

  /* - - - - TRIALS - - - - */

  // loads list objects into global pregenerated_lists variable
  await loadSession(condition, counterbalance)

  // This loop could also be accomplished using timeline variables and
  // a special plugin, but I haven't found this worthwhile
  var test_timeline = []
  for(var i = 0; i < pregenerated_lists.length; i++) {
    test_timeline = test_timeline.concat( generateTestBlock(pregenerated_lists[i]) )
  }

  var test_node = {timeline: test_timeline}

  timeline_all = [practice_node, test_node] //end experiment

  window.onbeforeunload = function() {
    return "Warning: Refreshing the window will RESTART the experiment from the beginning! Please avoid refreshing your browser while the task is running.";
  }

  /* - - - - EXECUTION - - - - */
  jsPsych.init({
      timeline: timeline_all,
      on_finish: function() {
          // use psiturk savedata
          psiturk.saveData()
          saveData(JSON.stringify(psiturk.taskdata.toJSON()));
          Questionnaire(psiturk);
      },
      on_data_update: function(data) {
          psiturk.recordTrialData(data);

          // FIXME
          // saveData(JSON.stringify(psiturk.taskdata.toJSON()));
          psiturk.saveData()
      },

      exclusions: {
        min_width: 800,
        min_height: 600
      }
  });
}
