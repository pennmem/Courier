using UnityEngine;
using UnityEngine.SceneManagement;
using Luminosity.IO;
using System.Runtime.InteropServices;

public class BeginExperiment : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void ResumeAudioContext();
    [DllImport("__Internal")]
    private static extern void LogWebAudioState(string tag);
#else
    private static void ResumeAudioContext() { }
    private static void LogWebAudioState(string tag) { }
#endif

    public UnityEngine.GameObject greyedOutButton;
    public UnityEngine.GameObject beginExperimentButton;
    public UnityEngine.GameObject loadingButton;
    public UnityEngine.GameObject finishedButton;
    public UnityEngine.GameObject languageMismatchButton;
    public UnityEngine.UI.InputField participantCodeInput;
    public UnityEngine.UI.Toggle useRamulatorToggle;
    public UnityEngine.UI.Toggle useNiclsToggle;
    public UnityEngine.UI.Text beginButtonText;
    public UnityEngine.UI.InputField sessionInput;

    // LC: Add UseElemem toggle
    public UnityEngine.UI.Toggle useElememToggle;

    // Label GameObjects for the lab-rig toggles (Ramulator/Nicls/Elemem). These have no
    // shared parent with their toggles in the scene, so assign the three label objects here
    // to hide them alongside the toggles in the online build. Assign in the MainMenu scene.
    public UnityEngine.GameObject[] nonEssentialControls;

    // Query-string keys passed on the WebGL launch URL by Prolific.
    // Prolific injects PROLIFIC_PID automatically; SESSION is a custom param the
    // researcher sets per session study link (defaults to 0 when absent).
    private const string PROLIFIC_PID_KEY = "PROLIFIC_PID";
    private const string SESSION_KEY = "SESSION";

    // TODO: JPB: Make these configuration variables
    private const bool HOSPITAL_COURIER = false;
    private const bool NICLS_COURIER = false;
    private const bool VALUE_COURIER = true;
    private const bool EFR_COURIER = false;
    // string experiment_name = HOSPITAL_COURIER ? "StandardCourier" :
                            //  NICLS_COURIER ? "NiclsCourier" :
                            //  "StandardCourier";

    private const string scene_name = VALUE_COURIER ? "NewTown" : "MainGame";

    public const string EXP_NAME_COURIER = "Courier";
    public const string EXP_NAME_EFR = "EFRCourier";
    public const string EXP_NAME_NICLS = "NiclsCourier";
    public const string EXP_NAME_VALUE = "VCBehOnly";
    public const string EXP_NAME_VC_ONLINE = "VCOnline";

    private const int WEBGL_SESSION_NUMBER = 0;

    private void OnEnable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Start()
    {
        HideNonEssentialControls();
        ApplyProlificLaunchParams();
    }

    // The Ramulator/Nicls/Elemem toggles drive lab hardware that does nothing in the online
    // build, so hide them (and their labels) on every launch. DoBeginExperiment still reads
    // useXToggle.isOn, which returns the serialized 'false' even while the object is inactive.
    private void HideNonEssentialControls()
    {
        if (useRamulatorToggle != null) useRamulatorToggle.gameObject.SetActive(false);
        if (useNiclsToggle != null) useNiclsToggle.gameObject.SetActive(false);
        if (useElememToggle != null) useElememToggle.gameObject.SetActive(false);

        if (nonEssentialControls != null)
            foreach (UnityEngine.GameObject control in nonEssentialControls)
                if (control != null) control.SetActive(false);
    }

    // Reads PROLIFIC_PID / SESSION from the launch URL (e.g. ...?PROLIFIC_PID=<id>&SESSION=<n>).
    // When BOTH are present and valid, pre-fill and lock the fields so the participant only
    // clicks Begin. Otherwise stay in editable manual mode, pre-filling whatever was provided.
    private void ApplyProlificLaunchParams()
    {
        string prolificId = UrlParams.Get(PROLIFIC_PID_KEY);
        string sessionParam = UrlParams.Get(SESSION_KEY);

        bool hasValidId = !string.IsNullOrEmpty(prolificId) && IsValidParticipantName(prolificId);

        int session = 0;
        bool hasValidSession = !string.IsNullOrEmpty(sessionParam)
                               && System.Int32.TryParse(sessionParam, out session);

        // Pre-fill the participant code (registers participant; on WebGL this also resets session to 0).
        if (hasValidId)
        {
            participantCodeInput.text = prolificId;
            UpdateParticipant();
        }

        // Pre-fill the session AFTER UpdateParticipant, which would otherwise overwrite the field.
        if (hasValidSession)
        {
            sessionInput.text = session.ToString();
            UpdateSession();
        }

        // Lock the screen down only when both came from the URL; partial/none stays editable.
        if (hasValidId && hasValidSession)
        {
            participantCodeInput.interactable = false;
            sessionInput.interactable = false;
        }
    }

    private void Update()
    {
        if (DeliveryItems.ItemsExhausted())
        {
            beginExperimentButton.SetActive(false);
            finishedButton.SetActive(true);
        }
        else
        {
            finishedButton.SetActive(false);
        }
        if (LanguageMismatch())
        {
            beginExperimentButton.SetActive(false);
            languageMismatchButton.SetActive(true);
        }
        else
        {
            languageMismatchButton.SetActive(false);
        }
    }

    public void UpdateParticipant() {
        if (IsValidParticipantName(participantCodeInput.text))
        {
            UnityEPL.ClearParticipants();
            UnityEPL.AddParticipant(participantCodeInput.text);
            UnityEPL.SetExperimentName(SelectedExperimentName());
            beginExperimentButton.SetActive(true);
            greyedOutButton.SetActive(false);
            int nextSessionNumber = NextSessionNumber();
            UnityEPL.SetSessionNumber(nextSessionNumber);
            sessionInput.text = nextSessionNumber.ToString();
            beginButtonText.text = LanguageSource.GetLanguageString("begin session") + " " + nextSessionNumber.ToString();
        }
        else
        {
            UnityEPL.ClearParticipants();
            greyedOutButton.SetActive(true);
            beginExperimentButton.SetActive(false);
        }
    }

    public void UpdateSession() {
        int session;
         
        if(System.Int32.TryParse(sessionInput.text, out session)) {
            beginButtonText.text = LanguageSource.GetLanguageString("begin session") + " " + session.ToString();
            UnityEPL.SetSessionNumber(session);
            beginExperimentButton.SetActive(true);
            greyedOutButton.SetActive(false);
        }
        else {
            greyedOutButton.SetActive(true);
            beginExperimentButton.SetActive(false);
        }
    }

#if !(UNITY_WEBGL && !UNITY_EDITOR)
    private string GetLanguageFilePath()
    {
        string dataPath = UnityEPL.GetParticipantFolder();
        System.IO.Directory.CreateDirectory(dataPath);
        string languageFilePath = System.IO.Path.Combine(dataPath, "language");
        if (!System.IO.File.Exists(languageFilePath))
            System.IO.File.Create(languageFilePath).Close();
        return languageFilePath;
    }
#endif // !(UNITY_WEBGL && !UNITY_EDITOR)

    private bool LanguageMismatch()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return false;
#else
        if (UnityEPL.GetParticipants()[0].Equals("unspecified_participant"))
            return false;
        if (System.IO.File.ReadAllText(GetLanguageFilePath()).Equals(""))
            return false;
        return !LanguageSource.current_language.ToString().Equals(System.IO.File.ReadAllText(GetLanguageFilePath()));
#endif
    }

    private void LockLanguage()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return;
#else
        System.IO.File.WriteAllText(GetLanguageFilePath(), LanguageSource.current_language.ToString());
#endif
    }

    public void DoBeginExperiment()
    {
        if (!IsValidParticipantName(participantCodeInput.text)) {
            loadingButton.SetActive(false);
            greyedOutButton.SetActive(true);
            beginExperimentButton.SetActive(false);

            throw new UnityException("You are trying to start the experiment with an invalid participant name!");
        }

        int session;
        if (!System.Int32.TryParse(sessionInput.text, out session)) {
            loadingButton.SetActive(false);
            greyedOutButton.SetActive(true);
            beginExperimentButton.SetActive(false);

            throw new UnityException("You are trying to start the experiment with an invalid session number!");
        }

        UnityEPL.SetSessionNumber(session);
        UnityEPL.ClearParticipants();
        UnityEPL.AddParticipant(participantCodeInput.text);
        string experiment_name = SelectedExperimentName();
        
        UnityEPL.SetExperimentName(experiment_name);

        LockLanguage();
        // TODO: JPB: Use NextSessionNumber()
        // LC: added in Elemem 
        DeliveryExperiment.ConfigureExperiment(useRamulatorToggle.isOn, useNiclsToggle.isOn, useElememToggle.isOn,
                                               UnityEPL.GetSessionNumber(), experiment_name);
        Debug.Log("Ram On: " + useRamulatorToggle.isOn);
        Debug.Log("Nicls On: " + useNiclsToggle.isOn);
        Debug.Log("Elemem On: " + useElememToggle.isOn);
#if UNITY_WEBGL && !UNITY_EDITOR
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        Screen.fullScreen = true;   // enter fullscreen on the Begin click (valid user gesture)
        LogWebAudioState("beginBefore");
        ResumeAudioContext();       // unlock WebAudio within the same gesture so all audio (incl. video) can play
        LogWebAudioState("beginAfter");
#endif // UNITY_WEBGL
        SceneManager.LoadScene(scene_name);
    }

    private int NextSessionNumber()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return WEBGL_SESSION_NUMBER;
#else
        string dataPath = UnityEPL.GetParticipantFolder();
		System.IO.Directory.CreateDirectory(dataPath);
        string[] sessionFolders = System.IO.Directory.GetDirectories(dataPath);
        int mostRecentSessionNumber = -1;
        foreach (string folder in sessionFolders)
        {
            int thisSessionNumber = -1;
            if (int.TryParse(folder.Substring(folder.LastIndexOf('_')+1), out thisSessionNumber) && thisSessionNumber > mostRecentSessionNumber)
                mostRecentSessionNumber = thisSessionNumber;
        }
        return mostRecentSessionNumber + 1;
#endif
    }

    private string SelectedExperimentName()
    {
#if UNITY_WEBGL
        // WebGL (and Editor with WebGL target) uses the VC-Online flow. Settling this here means
        // DeliveryItems.Awake writes its remaining_items files under data/VC-Online/<participant>/...
        // which is the same path PopItem reads from at runtime.
        if (VALUE_COURIER && !EFR_COURIER && !NICLS_COURIER)
            return EXP_NAME_VC_ONLINE;
#endif
        string experiment_name = EFR_COURIER ? EXP_NAME_EFR :
                                NICLS_COURIER ? EXP_NAME_NICLS :
                                VALUE_COURIER ? EXP_NAME_VALUE :
                                EXP_NAME_COURIER;
        if (experiment_name == EXP_NAME_NICLS)
        {
            if (useNiclsToggle.isOn)
                experiment_name += "ClosedLoop";
            else
                experiment_name += "ReadOnly";
        }

        return experiment_name;
    }

    private bool IsValidParticipantName(string name)
    {

        if (name.Length < 1) {
            return false;
        }
        return true;

        // bool isTest = name.Equals("TEST");
        // if (isTest)
        //     return true;

        // if (name.Length != 6)
        //     return false;

        // bool isValidRAMName = name[0].Equals('R') && name[1].Equals('1') && char.IsDigit(name[2]) && char.IsDigit(name[3]) && char.IsDigit(name[4]) && char.IsUpper(name[5]);
        // bool isValidSCALPName = char.IsUpper(name[0]) && char.IsUpper(name[1]) && char.IsUpper(name[2]) && char.IsDigit(name[3]) && char.IsDigit(name[4]) && char.IsDigit(name[5]);
        // Debug.Log(isValidSCALPName);
        // return isValidRAMName || isValidSCALPName;
    }
}
