mergeInto(LibraryManager.library, {

    SaveData: function() {
        psiturk.saveData();
    },

    AddData: function(data) {
        psiturk.recordTrialData([Pointer_stringify(data)]);
    },
    
    EndTask: function() {
        Questionnaire(psiturk);
    },

    NoRefresh: function() {
        psiturk.finishInstructions();
    },
});