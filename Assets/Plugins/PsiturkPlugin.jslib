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
    // Participant identifiers are resolved by $courierGetIdentity below. They used to be read
    // straight off globals set by the hosting page, which silently produced "UNKNOWN_PID" for
    // every row when the deployed page turned out to be a stock Unity template with no
    // identity block. Resolving here means the logic ships inside the WebGL bundle and cannot
    // be lost to template drift. This path does not depend on the psiTurk `psiturk` object.

    // Resolve participant identity once per page load and cache it on window.courierIdentity.
    // Order: globals set by the hosting page -> URL query params -> sessionStorage -> generated.
    // A value starting with "{{" is an unsubstituted Prolific placeholder, i.e. a misconfigured
    // study URL; treat it as absent rather than writing it to the database.
    $courierGetIdentity: function() {
        if (window.courierIdentity) { return window.courierIdentity; }

        var params;
        try { params = new URLSearchParams(window.location.search); }
        catch (e) { params = null; }

        var ok = function(v) { return !!v && v.indexOf("{{") !== 0; };
        var pick = function() {
            for (var i = 0; i < arguments.length; i++) {
                if (!params) { break; }
                var v = params.get(arguments[i]);
                if (ok(v)) { return v; }
            }
            return "";
        };

        var pid = ok(window.prolific_pid) ? window.prolific_pid
                                          : pick("PROLIFIC_PID", "prolific_pid", "workerId");
        if (!pid) {
            // sessionStorage lets a mid-task reload keep the same identity.
            try { pid = sessionStorage.getItem("courier_pid") || ""; } catch (e) { pid = ""; }
        }
        if (!pid) {
            pid = "anon_" + Date.now();
            window.courierIdentityFallback = true;
            console.error("PsiturkPlugin: no PROLIFIC_PID available; using generated id " + pid);
        }
        try { sessionStorage.setItem("courier_pid", pid); } catch (e) { /* private mode */ }

        var identity = {
            prolific_pid: pid,
            study_id:   ok(window.study_id)   ? window.study_id   : pick("STUDY_ID", "study_id"),
            session_id: ok(window.session_id) ? window.session_id : pick("SESSION_ID", "session_id"),
            // Distinguishes batches from separate page loads. courierSeq restarts at 0 on every
            // load, so without this fetch_courier.py cannot tell two sessions apart and its
            // dedupe-by-seq silently overwrites the earlier one.
            load_token: pid + "_" + Date.now()
        };

        window.courierIdentity = identity;
        window.prolific_pid = identity.prolific_pid;
        window.study_id     = identity.study_id;
        window.session_id   = identity.session_id;
        console.log("PsiturkPlugin identity:", identity.prolific_pid, identity.study_id,
                    identity.session_id, identity.load_token);
        return identity;
    },

    SaveData__deps: ['$courierGetIdentity'],
    SaveData: function() {
        try {
            if (typeof window.courierPending === "undefined") { window.courierPending = []; }
            if (window.courierPending.length === 0) { return; }
            if (window.courierSaveInFlight) { return; }

            var batch = window.courierPending;
            window.courierPending = [];
            window.courierSaveInFlight = true;

            var identity = courierGetIdentity();
            var payload = {
                prolific_pid: identity.prolific_pid,
                study_id: identity.study_id,
                session_id: identity.session_id,
                load_token: identity.load_token,
                experiment: "VCBehOnly",
                table_name: "vconline",
                data: batch
            };

            // Escalate warn -> error once failures stack up, so a wholly broken save path is
            // impossible to miss in the console instead of looking like a normal session.
            var noteFailure = function(reason) {
                window.courierPending = batch.concat(window.courierPending);
                window.courierSaveFailures = (window.courierSaveFailures || 0) + 1;
                window.courierSaveLastError = reason;
                var msg = "PsiturkPlugin.SaveData: " + reason + " (consecutive failures: " +
                          window.courierSaveFailures + ", buffered events: " +
                          window.courierPending.length + ")";
                if (window.courierSaveFailures >= 3) { console.error(msg); }
                else { console.warn(msg + "; will retry."); }
            };

            fetch("/save", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload)
            }).then(function(response) {
                window.courierSaveInFlight = false;
                if (response.ok) {
                    window.courierSaveFailures = 0;
                } else {
                    // Re-queue this batch ahead of anything newer so it retries next flush.
                    noteFailure("/save returned HTTP " + response.status);
                }
            }).catch(function(error) {
                window.courierSaveInFlight = false;
                noteFailure("/save request failed: " + error);
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
    EndTask__deps: ['$courierGetIdentity'],
    EndTask: function() {
        var finish = null;
        try {
            if (typeof window.courierPending === "undefined") { window.courierPending = []; }
            var batch = window.courierPending;
            window.courierPending = [];

            finish = function(success) {
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

            // Only claim success once nothing is left buffered. Batches re-queued by earlier
            // failed flushes are already back in courierPending, so checking this final POST
            // alone would tell the participant their data was saved when it wasn't.
            var settle = function(ok) {
                finish(ok && window.courierPending.length === 0);
            };

            if (batch.length === 0) { settle(true); return; }

            var identity = courierGetIdentity();
            var payload = {
                prolific_pid: identity.prolific_pid,
                study_id: identity.study_id,
                session_id: identity.session_id,
                load_token: identity.load_token,
                experiment: "VCBehOnly",
                table_name: "vconline",
                data: batch
            };

            // The participant leaves after this screen, so retry a few times before giving up
            // rather than discarding the tail of the session on one transient failure.
            var attempt = function(triesLeft) {
                fetch("/save", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify(payload)
                }).then(function(response) {
                    if (response.ok) { settle(true); return; }
                    if (triesLeft > 0) {
                        console.warn("PsiturkPlugin.EndTask: final /save returned HTTP " +
                                     response.status + "; retrying.");
                        setTimeout(function() { attempt(triesLeft - 1); }, 1000);
                    } else {
                        console.error("PsiturkPlugin.EndTask: final /save returned HTTP " +
                                      response.status + "; giving up.");
                        window.courierPending = batch.concat(window.courierPending);
                        settle(false);
                    }
                }).catch(function(error) {
                    if (triesLeft > 0) {
                        console.warn("PsiturkPlugin.EndTask: final /save failed; retrying.", error);
                        setTimeout(function() { attempt(triesLeft - 1); }, 1000);
                    } else {
                        console.error("PsiturkPlugin.EndTask: final /save failed; giving up.", error);
                        window.courierPending = batch.concat(window.courierPending);
                        settle(false);
                    }
                });
            };
            attempt(2);
        } catch (error) {
            console.error("PsiturkPlugin.EndTask failed.", error);
            // Without this the participant is left staring at a frozen canvas.
            if (finish) { try { finish(false); } catch (e) { /* nothing else we can do */ } }
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
