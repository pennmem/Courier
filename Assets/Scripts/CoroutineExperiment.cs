using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using Luminosity.IO;

using System;
using System.IO;
using UnityEngine.Networking;
using System.Runtime.InteropServices;

public abstract class CoroutineExperiment : MonoBehaviour
{
    private const int MICROPHONE_TEST_LENGTH = 5;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void ResumeAudioContext();
    [DllImport("__Internal")] private static extern int IsWebAudioRunning();
    [DllImport("__Internal")] private static extern void LogWebAudioState(string tag);
    [DllImport("__Internal")] private static extern void UnmuteVideoElements();
#else
    private static void ResumeAudioContext() { }
    private static int IsWebAudioRunning() { return 1; }
    private static void LogWebAudioState(string tag) { }
    private static void UnmuteVideoElements() { }
#endif
#if !(UNITY_WEBGL && !UNITY_EDITOR)
        public SoundRecorder soundRecorder;
#endif
    public TextDisplayer textDisplayer;
    public VideoControl videoPlayer;
    public VideoSelector videoSelector;

    public GameObject titleMessage;
    public UnityEngine.UI.Text titleText;

    public AudioSource audioPlayback;
    public AudioSource highBeep;
    public AudioSource lowBeep;
    public AudioSource lowerBeep;
    public AudioMixer volume;

    public ScriptedEventReporter scriptedEventReporter;

    protected abstract void SetRamulatorState(string stateName, bool state, Dictionary<string, object> extraData);

    protected abstract void SetElememState(string stateName, Dictionary<string, object> extraData = null);

    protected IEnumerator DoSubjectSessionQuitPrompt(int sessionNumber, string message)
    {
        yield return null;
        SetRamulatorState("WAITING", true, new Dictionary<string, object>());
        SetElememState("WAITING");
        textDisplayer.DisplayText("subject/session confirmation", message);
        while (!InputManager.GetKeyDown(KeyCode.Y) && !InputManager.GetKeyDown(KeyCode.N))
        {
            yield return null;
        }
        textDisplayer.ClearText();
        SetRamulatorState("WAITING", false, new Dictionary<string, object>());
        if (InputManager.GetKey(KeyCode.N))
            Quit();
    }

#if !(UNITY_WEBGL && !UNITY_EDITOR)
    protected IEnumerator DoMicrophoneTest(string title, string press_any_key, string recording, string playing, string confirmation)
    {
        DisplayTitle(title);
        bool repeat = false;
        string wavFilePath;
        Debug.Log("Starting microphone test");

        do
        {
            Debug.Log("Press Key");
            yield return PressAnyKey(press_any_key);
            Debug.Log("After Press Key");
            lowBeep.Play();
            // Debug.Log("After Beep");
            textDisplayer.DisplayText("microphone test recording", recording);
            textDisplayer.ChangeColor(Color.red);
            yield return new WaitForSeconds(lowBeep.clip.length);
            wavFilePath = System.IO.Path.Combine(UnityEPL.GetDataPath(), "microphone_test_" + DataReporter.RealWorldTime().ToString("yyyy-MM-dd_HH_mm_ss") + ".wav");
            // Debug.Log("BEEEP");
            soundRecorder.StartRecording(wavFilePath);
            // Debug.Log("Start Recording");
            float startTime = Time.time;
            while (Time.time < startTime + MICROPHONE_TEST_LENGTH)
            {
                // Debug.Log("In While Loop");
                yield return null;
                if (InputManager.GetButtonDown("Secret") && Time.time - startTime > 0.1f)
                    break;
            }

            audioPlayback.clip = soundRecorder.StopRecording();
            // Debug.Log("Got CLip");
            textDisplayer.DisplayText("microphone test playing", playing);
            textDisplayer.ChangeColor(Color.green);

            audioPlayback.Play();
            // Debug.Log("Playing clip");
            yield return new WaitForSeconds(audioPlayback.clip.length);
            textDisplayer.ClearText();
            textDisplayer.OriginalColor();

            SetRamulatorState("WAITING", true, new Dictionary<string, object>());
            SetElememState("WAITING");
            textDisplayer.DisplayText("microphone test confirmation", confirmation);
            while (!InputManager.GetKeyDown(KeyCode.Y) && !InputManager.GetKeyDown(KeyCode.N) && !InputManager.GetKeyDown(KeyCode.C) &&
                   !InputManager.GetButtonDown("Continue"))
            {
                yield return null;
            }
            textDisplayer.ClearText();
            SetRamulatorState("WAITING", false, new Dictionary<string, object>());
            if (InputManager.GetKey(KeyCode.C))
                Quit();
            repeat = InputManager.GetKey(KeyCode.N);
        }
        while (repeat);
        

        if (!System.IO.File.Exists(wavFilePath))
            yield return PressAnyKey("WARNING: Wav output file not detected.  Sounds may not be successfully recorded to disk.");

        ClearTitle();
    }
#endif

    protected void DisplayTitle(string title)
    {
        titleMessage.SetActive(true);
        titleText.text = title;
    }

    protected void ClearTitle()
    {
        titleMessage.SetActive(false);
    }

    protected IEnumerator DoVideo(string playPrompt, string repeatPrompt, VideoSelector.VideoType videoType, int videoIndex = -1, bool skipPrompt=false)
    {
        Debug.Log("[FLOW] DoVideo entered. videoType=" + videoType + " videoIndex=" + videoIndex + " skipPrompt=" + skipPrompt + " playPrompt='" + playPrompt + "'");
        if (!skipPrompt)
        {
            Debug.Log("[FLOW] DoVideo waiting for PressAnyKey");
            yield return PressAnyKey(playPrompt);
            Debug.Log("[FLOW] DoVideo PressAnyKey returned");
        }

        bool replay = false;
        do
        {
            //start video player and wait for it to stop playing
            SetRamulatorState("INSTRUCT", true, new Dictionary<string, object>());
            SetElememState("INSTRUCT");
            videoSelector.SetVideo(videoType, videoIndex);
            Debug.Log("Starting video " + videoType.ToString() + " " + videoIndex.ToString());
            scriptedEventReporter.ReportScriptedEvent("start video", new Dictionary<string, object> { { "video number", videoIndex } });
#if UNITY_WEBGL && !UNITY_EDITOR
            // The browser resumes the WebAudio context asynchronously; if the video starts before it
            // reaches "running", the first play is silent (only replays have sound, because the resume
            // has finished by then). Kick a resume right after the PressAnyKey gesture and wait (capped)
            // until the context is actually running so the very first play has audio. On replays it is
            // already running, so this returns immediately.
            ResumeAudioContext();
            LogWebAudioState("preVideo");
            float audioResumeStart = Time.unscaledTime;
            while (IsWebAudioRunning() == 0 && Time.unscaledTime - audioResumeStart < 3f)
                yield return null;
            LogWebAudioState("preVideoAfterWait");
#endif
            yield return videoPlayer.StartVideoAndWait();
#if UNITY_WEBGL && !UNITY_EDITOR
            // Belt-and-suspenders: if the browser autoplay policy muted the underlying <video>
            // element, unmute it now that we have user activation. Harmless if audio routes through
            // WEBAudio instead. The element appears asynchronously, so repeat briefly after play starts.
            float unmuteStart = Time.unscaledTime;
            while (videoPlayer.IsPlaying() && Time.unscaledTime - unmuteStart < 1f)
            {
                UnmuteVideoElements();
                yield return null;
            }
#endif
            while (videoPlayer.IsPlaying())
                yield return null;
            scriptedEventReporter.ReportScriptedEvent("stop video", new Dictionary<string, object> { { "video number", videoIndex } });
            SetRamulatorState("INSTRUCT", false, new Dictionary<string, object>());

            SetRamulatorState("WAITING", true, new Dictionary<string, object>());
            SetElememState("WAITING");
            if (repeatPrompt != null)
            {
                textDisplayer.DisplayText("repeat video prompt", repeatPrompt);
                while (!InputManager.GetButtonDown("Continue") && !InputManager.GetKeyDown(KeyCode.N))
                {
                    yield return null;
                }
                replay = InputManager.GetKey(KeyCode.N);
                textDisplayer.ClearText();
            }
            SetRamulatorState("WAITING", false, new Dictionary<string, object>());
        }
        while (replay);
    }

    protected IEnumerator PressAnyKey(string displayText)
    {
        SetRamulatorState("WAITING", true, new Dictionary<string, object>());
        SetElememState("WAITING");
        yield return null;
        Debug.Log("In PressAnyKey");
        textDisplayer.DisplayText("press any key prompt", displayText);

        while (!InputManager.anyKeyDown)
            yield return null;
        Debug.Log("After any key down");
        textDisplayer.ClearText();
        Debug.Log("After clear text");
        SetRamulatorState("WAITING", false, new Dictionary<string, object>());
        Debug.Log("After waiting");
    }

    protected void Quit()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}
