mergeInto(LibraryManager.library, {

    SaveData: function() {
        if (typeof psiturk !== "undefined" && psiturk && psiturk.saveData) {
            psiturk.saveData();
        } else {
            console.warn("PsiturkPlugin.SaveData skipped: psiturk is not available.");
        }
    },

    AddData: function(data) {
        var json = UTF8ToString(data);
        if (typeof psiturk !== "undefined" && psiturk && psiturk.recordTrialData) {
            psiturk.recordTrialData([json]);
        } else {
            console.log("PsiturkPlugin.AddData:", json);
        }
    },
    
    EndTask: function() {
        if (typeof Questionnaire !== "undefined" && typeof psiturk !== "undefined" && psiturk) {
            Questionnaire(psiturk);
        } else {
            console.warn("PsiturkPlugin.EndTask skipped: Questionnaire or psiturk is not available.");
        }
    },

    NoRefresh: function() {
        if (typeof psiturk !== "undefined" && psiturk && psiturk.finishInstructions) {
            psiturk.finishInstructions();
        } else {
            console.warn("PsiturkPlugin.NoRefresh skipped: psiturk is not available.");
        }
    },
});
