mergeInto(LibraryManager.library, {

    // --- Data pipeline: batched POST to the psiTurk /save route (MySQL-backed) ---
    //
    // Each DataPoint arrives via AddData and is buffered in window.courierPending with a
    // monotonic per-session sequence number. SaveData() (called by the Unity data handler
    // every ~300 frames) POSTs the buffered batch to /save in the same shape the dirFRU
    // experiment uses. A failed POST puts the batch back at the front of the buffer so the
    // next flush retries it; seq numbers + the server row id/timestamp let fetch_courier.py
    // recover a total order even if a retried batch lands late.
    //
    // Participant identifiers come from globals set by exp.html (parsed from URL query
    // params). This path no longer depends on the psiTurk `psiturk` object.

    SaveData: function() {
        try {
            if (typeof window.courierPending === "undefined") { window.courierPending = []; }
            if (window.courierPending.length === 0) { return; }
            if (window.courierSaveInFlight) { return; }

            var batch = window.courierPending;
            window.courierPending = [];
            window.courierSaveInFlight = true;

            var payload = {
                prolific_pid: window.prolific_pid || "UNKNOWN_PID",
                study_id: window.study_id || "",
                session_id: window.session_id || "",
                experiment: "VCBehOnly",
                table_name: "vconline",
                data: batch
            };

            fetch("/save", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload)
            }).then(function(response) {
                window.courierSaveInFlight = false;
                if (!response.ok) {
                    // Re-queue this batch ahead of anything newer so it retries next flush.
                    window.courierPending = batch.concat(window.courierPending);
                    console.warn("PsiturkPlugin.SaveData: /save returned HTTP " + response.status + "; will retry.");
                }
            }).catch(function(error) {
                window.courierSaveInFlight = false;
                window.courierPending = batch.concat(window.courierPending);
                console.warn("PsiturkPlugin.SaveData: /save request failed; will retry.", error);
            });
        } catch (error) {
            window.courierSaveInFlight = false;
            console.warn("PsiturkPlugin.SaveData failed; continuing without blocking.", error);
        }
    },

    AddData: function(data) {
        var json = UTF8ToString(data);
        try {
            if (typeof window.courierPending === "undefined") { window.courierPending = []; }
            if (typeof window.courierSeq === "undefined") { window.courierSeq = 0; }
            window.courierPending.push({ seq: window.courierSeq++, event: json });
        } catch (error) {
            console.warn("PsiturkPlugin.AddData failed; logging locally instead.", error);
            console.log("PsiturkPlugin.AddData:", json);
        }
    },

    // Final flush at end of session, then show the end screen. We fire one last POST of any
    // buffered events (bypassing the in-flight guard so it always sends), then tell the page
    // to display the "you're done, log off" screen. Success/failure toggles the message.
    EndTask: function() {
        try {
            if (typeof window.courierPending === "undefined") { window.courierPending = []; }
            var batch = window.courierPending;
            window.courierPending = [];

            var finish = function(success) {
                if (typeof window.showCourierEndScreen === "function") {
                    try { window.showCourierEndScreen(success); }
                    catch (e) { console.warn("PsiturkPlugin.EndTask: showCourierEndScreen failed.", e); }
                } else {
                    // Inline fallback if exp.html didn't define the end screen.
                    try {
                        document.body.innerHTML =
                            "<div style='color:#fff;background:#000;font-family:sans-serif;" +
                            "font-size:24px;text-align:center;padding-top:20%;height:100%;'>" +
                            (success
                                ? "Your data has been saved.<br><br>You are all done — you may now close this window and log off."
                                : "There was a problem saving your data.<br><br>Please email kahanalab@gmail.com.") +
                            "</div>";
                    } catch (e) { /* nothing else we can do */ }
                }
            };

            if (batch.length === 0) { finish(true); return; }

            var payload = {
                prolific_pid: window.prolific_pid || "UNKNOWN_PID",
                study_id: window.study_id || "",
                session_id: window.session_id || "",
                experiment: "VCBehOnly",
                table_name: "vconline",
                data: batch
            };

            fetch("/save", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload)
            }).then(function(response) {
                finish(response.ok);
            }).catch(function(error) {
                console.warn("PsiturkPlugin.EndTask: final /save failed.", error);
                finish(false);
            });
        } catch (error) {
            console.warn("PsiturkPlugin.EndTask failed; continuing without blocking.", error);
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
