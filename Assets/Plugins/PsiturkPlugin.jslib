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

    // Browsers start the WebAudio context suspended and only allow it to resume
    // after a user gesture. Until that happens ALL audio (sound effects AND video
    // sound) is silent. Call this from C# inside a click handler (e.g. the Begin
    // Experiment button) so the resume happens within the user gesture. This works
    // regardless of which HTML page hosts the build (default index.html or exp.html).
    ResumeAudioContext: function() {
        try {
            if (typeof WEBAudio !== "undefined" && WEBAudio.audioContext &&
                WEBAudio.audioContext.state === "suspended") {
                WEBAudio.audioContext.resume();
            }
        } catch (error) {
            console.warn("PsiturkPlugin.ResumeAudioContext failed; continuing without blocking.", error);
        }
    },

    // Diagnostic: log the current WebAudio context state (suspended / running / n/a) with a caller
    // tag. Direct-mode video audio on WebGL flows through this same context, so seeing "running" at
    // "videoStart" confirms the audio path is unlocked when the intro video plays.
    LogWebAudioState: function(tag) {
        try {
            var state = (typeof WEBAudio !== "undefined" && WEBAudio.audioContext)
                ? WEBAudio.audioContext.state : "n/a";
            console.log("[FLOW] WebAudio state @" + UTF8ToString(tag) + " = " + state);
        } catch (error) {
            console.warn("PsiturkPlugin.LogWebAudioState failed; continuing without blocking.", error);
        }
    },

    // Returns 1 only when the WebAudio context has fully transitioned to "running". resume() is
    // async, so C# polls this after ResumeAudioContext() to hold the first video until its audio
    // path is actually unlocked (otherwise the first play is silent and only replays have sound).
    IsWebAudioRunning: function() {
        try {
            return (typeof WEBAudio !== "undefined" && WEBAudio.audioContext &&
                    WEBAudio.audioContext.state === "running") ? 1 : 0;
        } catch (error) {
            return 0;
        }
    },

    // Unity may create the WebGL <video> element muted (or the browser autoplay policy mutes it) so
    // the picture plays with no sound. Once the user has interacted, force every <video> element
    // audible. Safe no-op if the video audio actually routes through WEBAudio instead of the element.
    UnmuteVideoElements: function() {
        try {
            var vids = document.getElementsByTagName("video");
            for (var i = 0; i < vids.length; i++) {
                vids[i].muted = false;
                vids[i].volume = 1.0;
            }
        } catch (error) {
            console.warn("PsiturkPlugin.UnmuteVideoElements failed; continuing without blocking.", error);
        }
    },
});
