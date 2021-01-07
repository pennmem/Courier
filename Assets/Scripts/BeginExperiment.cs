using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BeginExperiment : MonoBehaviour
{
    public UnityEngine.GameObject greyedOutButton;
    public UnityEngine.GameObject beginExperimentButton;
    public UnityEngine.GameObject loadingButton;
    public UnityEngine.GameObject finishedButton;
    public UnityEngine.GameObject languageMismatchButton;
    public UnityEngine.UI.InputField participantCodeInput;
    public UnityEngine.UI.Toggle useRamulatorToggle;
    public UnityEngine.UI.Text beginButtonText;
    public UnityEngine.UI.InputField sessionInput;

    private const string scene_name = "MainGame";

    private void OnEnable() {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        UnityEPL.SetExperimentName("DBOY1");
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
            beginExperimentButton.SetActive(true);
            greyedOutButton.SetActive(false);
            int nextSessionNumber = NextSessionNumber();
            sessionInput.text = nextSessionNumber.ToString();
            beginButtonText.text = LanguageSource.GetLanguageString("begin session") + " " + nextSessionNumber.ToString();
        }
        else
        {
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

    private string GetLanguageFilePath()
    {
        string dataPath = UnityEPL.GetParticipantFolder();
        string languageFilePath = System.IO.Path.Combine(dataPath, "language");
        return languageFilePath;
    }

    private bool LanguageMismatch()
    {
        if(!System.IO.Directory.Exists(UnityEPL.GetParticipantFolder()))
            return false;

        if(!System.IO.File.Exists(GetLanguageFilePath()))
            return false;

        if(System.IO.File.ReadAllText(GetLanguageFilePath()).Equals(""))
            return false;

        return !LanguageSource.current_language.ToString().Equals(System.IO.File.ReadAllText(GetLanguageFilePath()));
    }

    private void LockLanguage()
    {
        string languageFilePath = GetLanguageFilePath();

        if (!System.IO.File.Exists(languageFilePath))
            System.IO.File.Create(languageFilePath).Close();

        System.IO.File.WriteAllText(languageFilePath, LanguageSource.current_language.ToString());
    }

    public void DoBeginExperiment()
    {
        if (!IsValidParticipantName(participantCodeInput.text)) {
            loadingButton.SetActive(false);
            greyedOutButton.SetActive(true);
            beginExperimentButton.SetActive(false);

            throw new UnityException("You are trying to start the experiment with an invalid participant name!");
        }

        if (System.IO.Directory.Exists(UnityEPL.GetDataPath())) {
            loadingButton.SetActive(false);
            greyedOutButton.SetActive(true);
            beginExperimentButton.SetActive(false);

            throw new UnityException("You are trying to start an already existing session!");
        }

        System.IO.Directory.CreateDirectory(UnityEPL.GetParticipantFolder());
        System.IO.Directory.CreateDirectory(UnityEPL.GetDataPath());

        LockLanguage();
        DeliveryExperiment.ConfigureExperiment( useRamulatorToggle.isOn, 
                                                UnityEPL.GetSessionNumber(), 
                                                UnityEPL.GetParticipants()[0]); 

        Debug.Log(useRamulatorToggle.isOn);
        SceneManager.LoadScene(scene_name);
    }

    private int NextSessionNumber()
    {
        string dataPath = UnityEPL.GetParticipantFolder();
        int mostRecentSessionNumber = -1;

        if(!System.IO.Directory.Exists(dataPath))
            return mostRecentSessionNumber + 1;

        string[] sessionFolders = System.IO.Directory.GetDirectories(dataPath);
        foreach (string folder in sessionFolders)
        {
            int thisSessionNumber = -1;
            if (int.TryParse(folder.Substring(folder.LastIndexOf('_')+1), out thisSessionNumber) && thisSessionNumber > mostRecentSessionNumber)
                mostRecentSessionNumber = thisSessionNumber;
        }
        return mostRecentSessionNumber + 1;
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