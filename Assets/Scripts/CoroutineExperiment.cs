using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Luminosity.IO;

using System;
using System.IO;
using UnityEngine.Networking;

public abstract class CoroutineExperiment : MonoBehaviour
{
    private const int MICROPHONE_TEST_LENGTH = 5;
    #if !UNITY_WEBGL
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

    public ScriptedEventReporter scriptedEventReporter;

    protected abstract void SetRamulatorState(string stateName, bool state, Dictionary<string, object> extraData);

    protected IEnumerator DoSubjectSessionQuitPrompt(int sessionNumber, string message)
    {
        yield return null;
        SetRamulatorState("WAITING", true, new Dictionary<string, object>());
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

    #if !UNITY_WEBGL
    protected IEnumerator DoMicrophoneTest(string title, string press_any_key, string recording, string playing, string confirmation)
    {
        DisplayTitle(title);
        bool repeat = false;
        string wavFilePath;

        do
        {
            yield return PressAnyKey(press_any_key);
            lowBeep.Play();
            textDisplayer.DisplayText("microphone test recording", recording);
            textDisplayer.ChangeColor(Color.red);
            yield return new WaitForSeconds(lowBeep.clip.length);
            wavFilePath = System.IO.Path.Combine(UnityEPL.GetDataPath(), "microphone_test_" + DataReporter.RealWorldTime().ToString("yyyy-MM-dd_HH_mm_ss") + ".wav");

            soundRecorder.StartRecording(wavFilePath);
            float startTime = Time.time;
            while (Time.time < startTime + MICROPHONE_TEST_LENGTH)
            {
                yield return null;
                if (InputManager.GetButtonDown("Secret") && Time.time - startTime > 0.1f)
                    break;
            }

            audioPlayback.clip = soundRecorder.StopRecording();

            textDisplayer.DisplayText("microphone test playing", playing);
            textDisplayer.ChangeColor(Color.green);

            audioPlayback.Play();
            yield return new WaitForSeconds(audioPlayback.clip.length);
            textDisplayer.ClearText();
            textDisplayer.OriginalColor();

            SetRamulatorState("WAITING", true, new Dictionary<string, object>());
            textDisplayer.DisplayText("microphone test confirmation", confirmation);
            while (!InputManager.GetKeyDown(KeyCode.Y) && !InputManager.GetKeyDown(KeyCode.N) && !InputManager.GetKeyDown(KeyCode.C))
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

    protected IEnumerator DoVideo(string playPrompt, string repeatPrompt, VideoSelector.VideoType videoType, int videoIndex = -1)
    {
        yield return PressAnyKey(playPrompt);

        bool replay = false;
        do
        {
            //start video player and wait for it to stop playing
            SetRamulatorState("INSTRUCT", true, new Dictionary<string, object>());
            videoSelector.SetVideo(videoType, videoIndex);
            scriptedEventReporter.ReportScriptedEvent("start video", new Dictionary<string, object> { { "video number", videoIndex } });
            videoPlayer.StartVideo();
            while (videoPlayer.IsPlaying())
                yield return null;
            scriptedEventReporter.ReportScriptedEvent("stop video", new Dictionary<string, object> { { "video number", videoIndex } });
            SetRamulatorState("INSTRUCT", false, new Dictionary<string, object>());

            SetRamulatorState("WAITING", true, new Dictionary<string, object>());
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
        yield return null;

        textDisplayer.DisplayText("press any key prompt", displayText);
        while (!InputManager.anyKeyDown)
            yield return null;

        textDisplayer.ClearText();
        SetRamulatorState("WAITING", false, new Dictionary<string, object>());
    }

    class ReadOnlineFile
    {
        ReadOnlineFile(string filePath)
        {

        }
    }


    // TODO: LC: This should later be refactored into FlexibleConfig.cs
    protected IEnumerator GetOnlineConfig()
    {
        Debug.Log("setting web request");
        string systemConfigPath = System.IO.Path.Combine(Application.streamingAssetsPath, "config.json");
        UnityWebRequest systemWWW = new UnityWebRequest(systemConfigPath);
        systemWWW.downloadHandler = new DownloadHandlerBuffer();
        yield return systemWWW.SendWebRequest();

        if (systemWWW.result != UnityWebRequest.Result.Success)
        {
            Debug.Log("Network error " + systemWWW.error);
        }
        else
        {
            Config.onlineSystemConfigText = systemWWW.downloadHandler.text;
            Debug.Log("Online System Config fetched!!");
            Debug.Log(Config.onlineSystemConfigText);
        }

        string experimentConfigPath = System.IO.Path.Combine(Application.streamingAssetsPath, Config.experimentConfigName + ".json");
        UnityWebRequest experimentWWW = new UnityWebRequest(experimentConfigPath);
        experimentWWW.downloadHandler = new DownloadHandlerBuffer();
        yield return experimentWWW.SendWebRequest();

        if (systemWWW.result != UnityWebRequest.Result.Success)
        {
            Debug.Log("Network error " + experimentWWW.error);
        }
        else
        {
            Config.onlineExperimentConfigText = experimentWWW.downloadHandler.text;
            Debug.Log("Online Experiment Config fetched!!");
            Debug.Log(Config.onlineExperimentConfigText);
        }
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
