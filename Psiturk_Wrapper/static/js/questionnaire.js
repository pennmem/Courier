var Questionnaire = async function(psiturk) {

    var pages = [
        "survey.html"
    ];

	var error_message = "<h1>Oops!</h1><p>Something went wrong submitting your HIT. This might happen if you lose your internet connection. Press the button to resubmit.</p><button id='resubmit'>Resubmit</button>";
	record_responses = function() {
		psiturk.recordTrialData({'phase':'survey', 'status':'submit'});
		$('input[type=radio]:checked').each( function(i, val) {
			psiturk.recordUnstructuredData(this.name, this.value);
		});
		$('input[type=number]').each( function(i, val) {
			psiturk.recordUnstructuredData(this.name, this.value);
		});
		$('input[type=text]').each( function(i, val) {
			psiturk.recordUnstructuredData(this.name, this.value);
		});
		$('input[type=range]').each( function(i, val) {
			psiturk.recordUnstructuredData(this.name, this.value);
		});
		$('select').each( function(i, name) {
			psiturk.recordUnstructuredData(this.name, this.value);
		});
		$('textarea').each( function(i, name) {
			psiturk.recordUnstructuredData(this.name, this.value);
		});
		var races = $('input[type=checkbox]:checked').map(function () {
    	return this.value;
		}).get();
		psiturk.recordUnstructuredData('race', races);
	};

	prompt_resubmit = function() {
		document.body.innerHTML = error_message;
		$("#resubmit").click(resubmit);
	};

	// Load the questionnaire snippet
    await psiturk.preloadPages(pages);
	psiturk.showPage('survey.html');
	psiturk.recordTrialData({'phase':'survey', 'status':'begin'});

	$("#postquiz").submit(function() {

        // Turn off pop-up warning for closing browser so it doesn't try to block the experiment from closing
        window.onbeforeunload = null;
	    record_responses();
	    psiturk.saveData({
            success: function(){
                	psiturk.completeHIT(); // when finished saving, mark HIT complete and quit
            },
            error: prompt_resubmit});
        psiturk.completeHIT();
	});
};