mergeInto(LibraryManager.library, {

    SaveData: function() {
        if (typeof psiturk !== "undefined" && psiturk && typeof psiturk.saveData === "function") {
            try {
                psiturk.saveData();
            } catch (error) {
                console.warn("PsiturkPlugin.SaveData failed; continuing without blocking.", error);
            }
        } else {
            console.warn("PsiturkPlugin.SaveData skipped: psiturk is not available.");
        }
    },

    AddData: function(data) {
        var json = UTF8ToString(data);
        if (typeof psiturk !== "undefined" && psiturk && typeof psiturk.recordTrialData === "function") {
            try {
                psiturk.recordTrialData([json]);
            } catch (error) {
                console.warn("PsiturkPlugin.AddData failed; logging locally instead.", error);
                console.log("PsiturkPlugin.AddData:", json);
            }
        } else {
            console.log("PsiturkPlugin.AddData:", json);
        }
    },
    
    EndTask: function() {
        if (typeof Questionnaire === "function" && typeof psiturk !== "undefined" && psiturk) {
            try {
                Questionnaire(psiturk);
            } catch (error) {
                console.warn("PsiturkPlugin.EndTask failed; continuing without blocking.", error);
            }
        } else {
            console.warn("PsiturkPlugin.EndTask skipped: Questionnaire or psiturk is not available.");
        }
    },

    NoRefresh: function() {
        if (typeof psiturk !== "undefined" && psiturk && typeof psiturk.finishInstructions === "function") {
            try {
                psiturk.finishInstructions();
            } catch (error) {
                console.warn("PsiturkPlugin.NoRefresh failed; continuing without blocking.", error);
            }
        } else {
            console.warn("PsiturkPlugin.NoRefresh skipped: psiturk is not available.");
        }
    },
});
