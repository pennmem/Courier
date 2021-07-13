using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Luminosity.IO;
using System.Linq;

using static MessageImageDisplayer;

[System.Serializable]
public struct Environment
{
    public GameObject parent;
    public StoreComponent[] stores;
}

public class DeliveryExperiment : CoroutineExperiment
{
    public delegate void StateChange(string stateName, bool on);
    public static StateChange OnStateChange;

    private static int sessionNumber = -1;
    private static bool useRamulator;
    private static bool useNicls;
    private static string expName;

    // JPB: TODO: Make these configuration variables
    //private const bool NO_SYNCBOX = true;
    //private const bool LESS_TRIALS = false;
    //private const bool LESS_DELIVERIES = true;
    //private const bool SKIP_INTROS = true;
    //private const bool SKIP_TOWN_LEARNING = true;

    //private const bool EFR_ENABLED = true;
    private const bool NICLS_COURIER = true;
    //private const bool COUNTER_BALANCE_CORRECT_INCORRECT_BUTTONS = false;

    private const string COURIER_VERSION = "v5.0.7";
    private const string RECALL_TEXT = "*******";
    //private const int DELIVERIES_PER_TRIAL = LESS_DELIVERIES ? 3 : (NICLS_COURIER ? 16 : 13);
    //private const int PRACTICE_DELIVERIES_PER_TRIAL = 4;
    //private const int TRIALS_PER_SESSION = LESS_TRIALS ? 2 : (NICLS_COURIER ? 5 : 8);
    //private const int TRIALS_PER_SESSION_SINGLE_TOWN_LEARNING = LESS_TRIALS ? 2 : 5;
    //private const int TRIALS_PER_SESSION_DOUBLE_TOWN_LEARNING = LESS_TRIALS ? 1 : 3;
    private const int EFR_PRACTICE_TRIAL_NUM = 1;
    private const int NUM_READ_ONLY_TRIALS = 1;
    private const int SINGLE_TOWN_LEARNING_SESSIONS = 1000; // All sessions
    private const int DOUBLE_TOWN_LEARNING_SESSIONS = 1;
    private const int POINTING_INDICATOR_DELAY = NICLS_COURIER ? 12 : 40;
    private const int EFR_KEYPRESS_PRACTICES = 8;
    private const float MIN_FAMILIARIZATION_ISI = 0.4f;
    private const float MAX_FAMILIARIZATION_ISI = 0.6f;
    private const float FAMILIARIZATION_PRESENTATION_LENGTH = 1.5f;
    private const float RECALL_MESSAGE_DISPLAY_LENGTH = 6f;
    private const float RECALL_TEXT_DISPLAY_LENGTH = 1f;
    private const float FREE_RECALL_LENGTH = 90f;
    private const float PRACTICE_FREE_RECALL_LENGTH = 25f;
    private const float STORE_FINAL_RECALL_LENGTH = 90f;
    private const float FINAL_RECALL_LENGTH = NICLS_COURIER ? 120f : 180f;
    private const float TIME_BETWEEN_DIFFERENT_RECALL_PHASES = 2f;
    private const float MIN_CUED_RECALL_TIME_PER_STORE = 2f;
    private const float MAX_CUED_RECALL_TIME_PER_STORE = NICLS_COURIER ? 6f : 10f;
    private const float ARROW_CORRECTION_TIME = 3f;
    private const float ARROW_ROTATION_SPEED = 1f;
    private const float PAUSE_BEFORE_RETRIEVAL = 10f;
    private const float DISPLAY_ITEM_PAUSE = 5f;
    private const float AUDIO_TEXT_DISPLAY = 1.6f;
    private const float WORD_PRESENTATION_DELAY = 1f;
    private const float WORD_PRESENTATION_JITTER = 0.25f;
    private const float EFR_KEYPRESS_PRACTICE_DELAY = 2f;
    private const float EFR_KEYPRESS_PRACTICE_JITTER = 0.25f;

    public Camera regularCamera;
    public Camera blackScreenCamera;
    public Familiarizer familiarizer;
    public MessageImageDisplayer messageImageDisplayer;
    public RamulatorInterface ramulatorInterface;
    public NiclsInterface niclsInterface;
    public PlayerMovement playerMovement;
    public GameObject pointer;
    public ParticleSystem pointerParticleSystem;
    public GameObject pointerMessage;
    public UnityEngine.UI.Text pointerText;
    public StarSystem starSystem;
    public DeliveryItems deliveryItems;
    public Pauser pauser;

    public float pointerRotationSpeed = 10f;

    public ScriptedEventReporter scriptedEventReporter;
    public GameObject memoryWordCanvas;

    public Environment[] environments;

    System.Random rng = new System.Random();

    private EfrButton efrCorrectButtonSide = EfrButton.RightButton;
    private string efrLeftLogMsg = "incorrect";
    private string efrRightLogMsg = "correct";

    private List<StoreComponent> this_trial_presented_stores = new List<StoreComponent>();
    private List<string> all_presented_objects = new List<string>();

    private Syncbox syncs;

    public static void ConfigureExperiment(bool newUseRamulator, bool newUseNicls, int newSessionNumber, string newExpName)
    {
        useRamulator = newUseRamulator;
        useNicls = newUseNicls;
        sessionNumber = newSessionNumber;
        expName = newExpName;
        Config.experimentConfigName = expName;
    }

    void Update()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        // Need to do this because MacOS thinks it knows better than you do
    }

    void Start()
    {
        if (UnityEPL.viewCheck)
            return;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        Application.targetFrameRate = 300;
        Cursor.SetCursor(new Texture2D(0, 0), new Vector2(0, 0), CursorMode.ForceSoftware);
        QualitySettings.vSyncCount = 1;

        // Start syncpulses
        if (!Config.noSyncbox)
        {
            syncs = GameObject.Find("SyncBox").GetComponent<Syncbox>();
            syncs.StartPulse();
        }

        // Randomize efr correct/incorrect button sides
        if (Config.efrEnabled && Config.counterBalanceCorrectIncorrectButton)
        {
            // We want randomness for different people, but consistency between sessions
            System.Random reliableRandom = new System.Random(UnityEPL.GetParticipants()[0].GetHashCode());
            efrCorrectButtonSide = (EfrButton)reliableRandom.Next(0, 2);
        }

        Dictionary<string, object> sceneData = new Dictionary<string, object>();
        sceneData.Add("sceneName", "MainGame");
        scriptedEventReporter.ReportScriptedEvent("loadScene", sceneData);

        StartCoroutine(ExperimentCoroutine());
    }

    private IEnumerator ExperimentCoroutine()
    {
        if (sessionNumber == -1)
            throw new UnityException("Please call ConfigureExperiment before beginning the experiment.");

        Debug.Log(UnityEPL.GetDataPath());

        //write versions to logfile
        LogVersions(expName);

        if (useRamulator)
            yield return ramulatorInterface.BeginNewSession(sessionNumber);

        if (useNicls)
            yield return niclsInterface.BeginNewSession(sessionNumber);
        else
            yield return niclsInterface.BeginNewSession(sessionNumber, true);

        yield return DoSubSession(0);

        if (NICLS_COURIER)
        {
            yield return DoBreak();
            System.Random reliableRandom = new System.Random(UnityEPL.GetParticipants()[0].GetHashCode());
            int[][] clipsIndices = new int[4][] { new int[8] { 0, 1, 2, 3, 2, 4, 0, 5 },
                                                  new int[8] { 0, 1, 2, 3, 5, 0, 4, 2 },
                                                  new int[8] { 0, 1, 2, 3, 3, 4, 1, 5 },
                                                  new int[8] { 0, 1, 2, 3, 5, 1, 4, 3 }, };
            int[] clipIndices = clipsIndices[reliableRandom.Next(4)];
            yield return DoMovie(clipIndices);
            yield return DoSubSession(1);
        }

        string endMessage = NICLS_COURIER
            ? LanguageSource.GetLanguageString("end message")
            : LanguageSource.GetLanguageString("end message scored") + starSystem.CumulativeRating().ToString("+#.##;-#.##");
        textDisplayer.DisplayText("end text", endMessage);

        while (true)
            yield return null;
    }

    private IEnumerator DoSubSession(int subSessionNum)
    {
        BlackScreen();
        if (!Config.skipIntros && subSessionNum == 0) // JPB: TODO: Nick fix
        {
            if ((NICLS_COURIER && sessionNumber == 0)) // JPB: TODO: Nick fix
                yield return DoVideo(LanguageSource.GetLanguageString("play movie"),
                                     LanguageSource.GetLanguageString("standard intro video"),
                                     VideoSelector.VideoType.NiclsMainIntro);
            else if(!NICLS_COURIER)
                yield return DoVideo(LanguageSource.GetLanguageString("play movie"),
                                     LanguageSource.GetLanguageString("standard intro video"),
                                     VideoSelector.VideoType.MainIntro);
            yield return DoSubjectSessionQuitPrompt(sessionNumber,
                                                    LanguageSource.GetLanguageString("running participant"));
            yield return DoMicrophoneTest(LanguageSource.GetLanguageString("microphone test"),
                                          LanguageSource.GetLanguageString("after the beep"),
                                          LanguageSource.GetLanguageString("recording"),
                                          LanguageSource.GetLanguageString("playing"),
                                          LanguageSource.GetLanguageString("recording confirmation"));
            if (!NICLS_COURIER)
                yield return DoFamiliarization();
        }

        Environment environment = EnableEnvironment();
        Dictionary<string, object> storeMappings = new Dictionary<string, object>();
        foreach (StoreComponent store in environment.stores)
        {
            storeMappings.Add(store.gameObject.name, store.GetStoreName());
            storeMappings.Add(store.GetStoreName() + " position X", store.transform.position.x);
            storeMappings.Add(store.GetStoreName() + " position Y", store.transform.position.y);
            storeMappings.Add(store.GetStoreName() + " position Z", store.transform.position.z);
        }
        scriptedEventReporter.ReportScriptedEvent("store mappings", storeMappings);

        int trialsPerSession = Config.trialsPerSession;
        if (NICLS_COURIER)
        {
            Debug.Log("Town Learning Phase");
            niclsInterface.SendReadOnlyStateToNicls(1);

            if (sessionNumber < SINGLE_TOWN_LEARNING_SESSIONS + DOUBLE_TOWN_LEARNING_SESSIONS)
            {
                trialsPerSession = Config.trialsPerSessionSingleTownLearning;
                messageImageDisplayer.SetGeneralMessageText("town learning title", "town learning main 1");
                yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_message_display);
                WorldScreen();
                yield return DoTownLearning(environment, 0, environment.stores.Length);

                if (sessionNumber < DOUBLE_TOWN_LEARNING_SESSIONS && subSessionNum == 0) // JPB: TODO: Nick fix
                {
                    trialsPerSession = Config.trialsPerSessionDoubleTownLearning;
                    messageImageDisplayer.SetGeneralMessageText("town learning title", "town learning main 2");
                    yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_message_display);
                    WorldScreen();
                    yield return DoTownLearning(environment, 1, environment.stores.Length);
                }
            }
        }

        BlackScreen();
        yield return messageImageDisplayer.DisplayLanguageMessage(messageImageDisplayer.delivery_restart_messages);

        if (sessionNumber == 0 && subSessionNum == 0) // JPB: TODO: Nick fix
        {
            Debug.Log("Practice trials");
            messageImageDisplayer.SetGeneralMessageText(mainText: "practice invitation");
            yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_message_display);
            yield return DoTrials(environment, 2, subSessionNum, true);
        }

        Debug.Log("Real trials");
        if (Config.efrEnabled)
            messageImageDisplayer.SetGeneralMessageText(mainText: "first day main", descriptiveText: "efr first day description");
        else
            messageImageDisplayer.SetGeneralMessageText(mainText: "first day main");

        yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_message_display);
        int priorTrialsPerSession = 0;
        if (NICLS_COURIER && subSessionNum > 0) // JPB: TODO: Nick fix
        {
            if (sessionNumber < DOUBLE_TOWN_LEARNING_SESSIONS)
                priorTrialsPerSession = Config.trialsPerSessionDoubleTownLearning;
            else if (sessionNumber < SINGLE_TOWN_LEARNING_SESSIONS)
                priorTrialsPerSession = Config.trialsPerSessionSingleTownLearning;
        }
        yield return DoTrials(environment, trialsPerSession, subSessionNum,
                              trialNumOffset: subSessionNum * priorTrialsPerSession); // JPB: TODO: Fix this to work for more than two sub-sessions

        Debug.Log("Final Recalls");
        BlackScreen();
        yield return messageImageDisplayer.DisplayLanguageMessage(messageImageDisplayer.final_recall_messages);
        yield return DoFinalRecall(environment, subSessionNum);
    }

    private void LogVersions(string expName)
    {
        Dictionary<string, object> versionsData = new Dictionary<string, object>();
        versionsData.Add("UnityEPL version", Application.version);
        versionsData.Add("Experiment version", expName + COURIER_VERSION);
        versionsData.Add("Logfile version", "2.0.0");
        scriptedEventReporter.ReportScriptedEvent("versions", versionsData);
    }

    private void BlackScreen()
    {
        pauser.ForbidPausing();
        memoryWordCanvas.SetActive(true);
        regularCamera.enabled = false;
        blackScreenCamera.enabled = true;
        starSystem.gameObject.SetActive(false);
        playerMovement.Freeze();
    }

    private IEnumerator DoFixation(float time, bool practice = false)
    {
        scriptedEventReporter.ReportScriptedEvent("start fixation", new Dictionary<string, object>());
        BlackScreen();

        if (practice)
            messageImageDisplayer.SetGeneralBigMessageText(titleText: "fixation practice message",
                                                           mainText: "fixation item",
                                                           continueText: "");
        else
            messageImageDisplayer.SetGeneralBigMessageText(mainText: "fixation item",
                                                           continueText: "");

        yield return messageImageDisplayer.DisplayMessageTimed(messageImageDisplayer.general_big_message_display, time);
        scriptedEventReporter.ReportScriptedEvent("stop fixation", new Dictionary<string, object>());
    }

    private void WorldScreen()
    {
        pauser.AllowPausing();
        regularCamera.enabled = true;
        blackScreenCamera.enabled = false;
        if (!NICLS_COURIER)
            starSystem.gameObject.SetActive(true);
        memoryWordCanvas.SetActive(false);
        playerMovement.Zero();
    }

    protected IEnumerator DisplayMessageAndWait(string description, string message)
    {
        SetRamulatorState("WAITING", true, new Dictionary<string, object>());

        BlackScreen();
        textDisplayer.DisplayText(description, message + "\r\nPress (x) to continue");
        while (!InputManager.GetButtonDown("Secret") && !InputManager.GetButtonDown("Continue"))
            yield return null;
        textDisplayer.ClearText();

        SetRamulatorState("WAITING", false, new Dictionary<string, object>());
    }

    private IEnumerator DoRecall(int trialNumber, int continuousTrialNum, bool practice = false)
    {
        SetRamulatorState("RETRIEVAL", true, new Dictionary<string, object>());

        yield return DoFreeRecall(trialNumber, continuousTrialNum, practice);

        yield return DoCuedRecall(trialNumber, continuousTrialNum, practice);

        SetRamulatorState("RETRIEVAL", false, new Dictionary<string, object>());
    }


    private IEnumerator DoFreeRecall(int trialNumber, int continuousTrialNum, bool practice = false)
    {
        scriptedEventReporter.ReportScriptedEvent("start free recall", new Dictionary<string, object>());
        BlackScreen();
        textDisplayer.ClearText();

        highBeep.Play();
        scriptedEventReporter.ReportScriptedEvent("sound played", new Dictionary<string, object>() { { "sound name", "high beep" }, { "sound duration", highBeep.clip.length.ToString() } });
        textDisplayer.DisplayText("display recall text", RECALL_TEXT);
        yield return SkippableWait(RECALL_TEXT_DISPLAY_LENGTH);
        textDisplayer.ClearText();


        string output_directory = UnityEPL.GetDataPath();
        string wavFilePath = practice
                    ? System.IO.Path.Combine(output_directory, "practice-" + continuousTrialNum.ToString()) + ".wav"
                    : System.IO.Path.Combine(output_directory, continuousTrialNum.ToString()) + ".wav";
        Dictionary<string, object> recordingData = new Dictionary<string, object>();
        recordingData.Add("trial number", continuousTrialNum);
        scriptedEventReporter.ReportScriptedEvent("object recall recording start", recordingData);
        soundRecorder.StartRecording(wavFilePath);

        if ((practice && trialNumber == 0) || !Config.efrEnabled)
            yield return DoFreeRecallDisplay(PRACTICE_FREE_RECALL_LENGTH);
        else if (practice)
            yield return DoEfrDisplay("", PRACTICE_FREE_RECALL_LENGTH, practice);
        else
            yield return DoEfrDisplay("", FREE_RECALL_LENGTH);

        scriptedEventReporter.ReportScriptedEvent("object recall recording stop", recordingData);
        soundRecorder.StopRecording();
        textDisplayer.ClearText();
        lowBeep.Play();
        scriptedEventReporter.ReportScriptedEvent("sound played", new Dictionary<string, object>() { { "sound name", "low beep" }, { "sound duration", lowBeep.clip.length.ToString() } });
        BlackScreen();
        scriptedEventReporter.ReportScriptedEvent("stop free recall", new Dictionary<string, object>());
    }

    private IEnumerator DoCuedRecall(int trialNumber, int continuousTrialNum, bool practice = false)
    {
        scriptedEventReporter.ReportScriptedEvent("start cued recall", new Dictionary<string, object>());
        BlackScreen();
        this_trial_presented_stores.Shuffle(rng);
        Debug.Log(this_trial_presented_stores);

        textDisplayer.DisplayText("display day cued recall prompt", LanguageSource.GetLanguageString("store cue recall"));
        yield return SkippableWait(RECALL_MESSAGE_DISPLAY_LENGTH);
        textDisplayer.ClearText();
        highBeep.Play();
        scriptedEventReporter.ReportScriptedEvent("sound played", new Dictionary<string, object>() { { "sound name", "high beep" }, { "sound duration", highBeep.clip.length.ToString() } });
        textDisplayer.DisplayText("display recall text", RECALL_TEXT);
        yield return SkippableWait(RECALL_TEXT_DISPLAY_LENGTH);
        textDisplayer.ClearText();
        foreach (StoreComponent cueStore in this_trial_presented_stores)
        {
            if (useNicls && (trialNumber >= NUM_READ_ONLY_TRIALS))
            {
                yield return new WaitForSeconds(WORD_PRESENTATION_DELAY);
                yield return WaitForClassifier();
            }
            else
            {
                float wordDelay = Random.Range(WORD_PRESENTATION_DELAY - WORD_PRESENTATION_JITTER,
                                               WORD_PRESENTATION_DELAY + WORD_PRESENTATION_JITTER);
                yield return new WaitForSeconds(wordDelay);
            }

            cueStore.familiarization_object.SetActive(true);
            messageImageDisplayer.SetCuedRecallMessage(true);

            string output_file_name = practice
                        ? "practice-" + continuousTrialNum.ToString() + "-" + cueStore.GetStoreName()
                        : continuousTrialNum.ToString() + "-" + cueStore.GetStoreName();
            string output_directory = UnityEPL.GetDataPath();
            string wavFilePath = System.IO.Path.Combine(output_directory, output_file_name) + ".wav";
            string lstFilepath = System.IO.Path.Combine(output_directory, output_file_name) + ".lst";
            AppendWordToLst(lstFilepath, cueStore.GetLastPoppedItemName());
            Dictionary<string, object> cuedRecordingData = new Dictionary<string, object>();
            cuedRecordingData.Add("trial number", continuousTrialNum);
            cuedRecordingData.Add("store", cueStore.GetStoreName());
            cuedRecordingData.Add("item", cueStore.GetLastPoppedItemName());
            cuedRecordingData.Add("store position", cueStore.transform.position.ToString());

            scriptedEventReporter.ReportScriptedEvent("cued recall recording start", cuedRecordingData);
            soundRecorder.StartRecording(wavFilePath);

            float startTime = Time.time;
            while ((!InputManager.GetButtonDown("Continue") || Time.time < startTime + MIN_CUED_RECALL_TIME_PER_STORE)
                   && Time.time < startTime + MAX_CUED_RECALL_TIME_PER_STORE)
                yield return null;

            scriptedEventReporter.ReportScriptedEvent("cued recall recording stop", cuedRecordingData);
            soundRecorder.StopRecording();

            cueStore.familiarization_object.SetActive(false);
            lowBeep.Play();
            scriptedEventReporter.ReportScriptedEvent("sound played", new Dictionary<string, object>() { { "sound name", "low beep" }, { "sound duration", highBeep.clip.length.ToString() } });
            textDisplayer.DisplayText("display recall text", RECALL_TEXT);
            yield return SkippableWait(RECALL_TEXT_DISPLAY_LENGTH);
            textDisplayer.ClearText();
        }
        messageImageDisplayer.SetCuedRecallMessage(false);
        scriptedEventReporter.ReportScriptedEvent("stop cued recall", new Dictionary<string, object>());
    }

    private IEnumerator DoFinalRecall(Environment environment, int subSessionNum)
    {
        scriptedEventReporter.ReportScriptedEvent("start final recall", new Dictionary<string, object>());
        SetRamulatorState("RETRIEVAL", true, new Dictionary<string, object>());

        string output_directory = UnityEPL.GetDataPath();
        string output_file_name;
        string wavFilePath;
        string lstFilepath;

        if (!NICLS_COURIER)
        {
            highBeep.Play();
            scriptedEventReporter.ReportScriptedEvent("sound played", new Dictionary<string, object>() { { "sound name", "high beep" }, { "sound duration", highBeep.clip.length.ToString() } });
            textDisplayer.DisplayText("display recall text", RECALL_TEXT);
            yield return SkippableWait(RECALL_TEXT_DISPLAY_LENGTH);
            textDisplayer.ClearText();

            output_file_name = "final store-" + subSessionNum;
            wavFilePath = System.IO.Path.Combine(output_directory, output_file_name) + ".wav";
            lstFilepath = System.IO.Path.Combine(output_directory, output_file_name) + ".lst";
            foreach (StoreComponent store in environment.stores)
                AppendWordToLst(lstFilepath, store.GetStoreName());

            scriptedEventReporter.ReportScriptedEvent("final store recall recording start", new Dictionary<string, object>());
            soundRecorder.StartRecording(wavFilePath);

            textDisplayer.ClearText();
            ClearTitle();
            yield return DoEfrDisplay("all stores recall", STORE_FINAL_RECALL_LENGTH);

            scriptedEventReporter.ReportScriptedEvent("final store recall recording stop", new Dictionary<string, object>());
            soundRecorder.StopRecording();
            textDisplayer.ClearText();
            lowBeep.Play();
            scriptedEventReporter.ReportScriptedEvent("sound played", new Dictionary<string, object>() { { "sound name", "low beep" }, { "sound duration", lowBeep.clip.length.ToString() } });

            yield return SkippableWait(TIME_BETWEEN_DIFFERENT_RECALL_PHASES);
        }

        highBeep.Play();
        scriptedEventReporter.ReportScriptedEvent("sound played", new Dictionary<string, object>() { { "sound name", "high beep" }, { "sound duration", highBeep.clip.length.ToString() } });
        textDisplayer.DisplayText("display recall text", RECALL_TEXT);
        yield return SkippableWait(RECALL_TEXT_DISPLAY_LENGTH);
        textDisplayer.ClearText();

        output_file_name = "final free-" + subSessionNum;
        wavFilePath = System.IO.Path.Combine(output_directory, output_file_name) + ".wav";
        lstFilepath = System.IO.Path.Combine(output_directory, output_file_name) + ".lst";
        foreach (string deliveredObject in all_presented_objects)
            AppendWordToLst(lstFilepath, deliveredObject);

        scriptedEventReporter.ReportScriptedEvent("final object recall recording start", new Dictionary<string, object>());
        soundRecorder.StartRecording(wavFilePath);

        textDisplayer.ClearText();
        ClearTitle();
        yield return DoEfrDisplay("all objects recall", FINAL_RECALL_LENGTH);
        scriptedEventReporter.ReportScriptedEvent("final object recall recording stop", new Dictionary<string, object>());
        soundRecorder.StopRecording();

        textDisplayer.ClearText();
        lowBeep.Play();
        scriptedEventReporter.ReportScriptedEvent("sound played", new Dictionary<string, object>() { { "sound name", "low beep" }, { "sound duration", lowBeep.clip.length.ToString() } });

        SetRamulatorState("RETRIEVAL", false, new Dictionary<string, object>());
        scriptedEventReporter.ReportScriptedEvent("stop final recall", new Dictionary<string, object>());
    }

    private IEnumerator DoFamiliarization()
    {
        yield return messageImageDisplayer.DisplayLanguageMessage(messageImageDisplayer.store_images_presentation_messages);
        yield return familiarizer.DoFamiliarization(MIN_FAMILIARIZATION_ISI, MAX_FAMILIARIZATION_ISI, FAMILIARIZATION_PRESENTATION_LENGTH);
    }

    private IEnumerator DoTownLearning(Environment environment, int trialNumber, int numDeliveries)
    {
        if (Config.skipTownLearning)
            yield break;

        scriptedEventReporter.ReportScriptedEvent("start town learning", new Dictionary<string, object>());
        messageImageDisplayer.please_find_the_blah_reminder.SetActive(true);

        this_trial_presented_stores = new List<StoreComponent>();
        List<StoreComponent> unvisitedStores = new List<StoreComponent>(environment.stores);

        for (int i = 0; i < numDeliveries; i++)
        {
            StoreComponent nextStore = null;
            int random_store_index = -1;
            int tries = 0;

            do
            {
                tries++;
                random_store_index = Random.Range(0, unvisitedStores.Count);
                nextStore = unvisitedStores[random_store_index];
            }
            while (nextStore.IsVisible() && tries < environment.stores.Length);

            unvisitedStores.RemoveAt(random_store_index);

            playerMovement.Freeze();
            messageImageDisplayer.SetReminderText(nextStore.GetStoreName());
            yield return new WaitForSeconds(0.5f);
            playerMovement.Unfreeze();

            float startTime = Time.time;
            while (!nextStore.PlayerInDeliveryPosition())
            {
                yield return null;
                if (Time.time > startTime + POINTING_INDICATOR_DELAY)
                    yield return DisplayPointingIndicator(nextStore, true);
            }
            yield return DisplayPointingIndicator(nextStore, false);

            scriptedEventReporter.ReportScriptedEvent("store visited",
                new Dictionary<string, object>() { {"trial number", trialNumber},
                                                   {"store name", nextStore.GetStoreName()},
                                                   {"serial position", i+1},
                                                   {"player position", playerMovement.transform.position.ToString()},
                                                   {"store position", nextStore.transform.position.ToString()}});
        }

        messageImageDisplayer.please_find_the_blah_reminder.SetActive(false);
        scriptedEventReporter.ReportScriptedEvent("stop town learning", new Dictionary<string, object>());
    }

    private IEnumerator DoDeliveries(Environment environment, int trialNumber, int continuousTrialNum, bool practice = false)
    {
        Dictionary<string, object> trialData = new Dictionary<string, object>();
        trialData.Add("trial number", trialNumber);
        if (practice)
            scriptedEventReporter.ReportScriptedEvent("start practice deliveries", trialData);
        else
            scriptedEventReporter.ReportScriptedEvent("start deliveries", trialData);

        WorldScreen();

        SetRamulatorState("ENCODING", true, new Dictionary<string, object>());
        messageImageDisplayer.please_find_the_blah_reminder.SetActive(true);

        this_trial_presented_stores = new List<StoreComponent>();
        List<StoreComponent> unvisitedStores = new List<StoreComponent>(environment.stores);

        int deliveries = practice ? Config.practiceDeliveriesPerTrial : Config.deliveriesPerTrial;
        int craft_shop_delivery_num = rng.Next(deliveries - 1);

        for (int i = 0; i < deliveries; i++)
        {
            StoreComponent nextStore = null;
            int random_store_index = -1;
            int tries = 0;

            int craft_shop_index = unvisitedStores.FindIndex(store => store.GetStoreName() == "craft shop");
            if (practice && trialNumber == 0)
            {
                if (i == craft_shop_delivery_num)
                {
                    random_store_index = craft_shop_index;
                    nextStore = unvisitedStores[random_store_index];
                }
                else
                {
                    do
                    {
                        tries++;
                        random_store_index = rng.Next(unvisitedStores.Count);
                        nextStore = unvisitedStores[random_store_index];
                    }
                    while ((nextStore.IsVisible() && tries < environment.stores.Length)
                            || random_store_index == craft_shop_index);
                }
            }
            else
            {
                do
                {
                    tries++;
                    random_store_index = rng.Next(unvisitedStores.Count);
                    nextStore = unvisitedStores[random_store_index];
                }
                while (nextStore.IsVisible() && tries < environment.stores.Length);
            }

            unvisitedStores.RemoveAt(random_store_index);

            playerMovement.Freeze();
            messageImageDisplayer.please_find_the_blah_reminder.SetActive(false);
            messageImageDisplayer.SetReminderText(nextStore.GetStoreName());
            if (!NICLS_COURIER)
                yield return DoPointingTask(nextStore);
            messageImageDisplayer.please_find_the_blah_reminder.SetActive(true);
            playerMovement.Unfreeze();

            float startTime = Time.time;
            while (!nextStore.PlayerInDeliveryPosition())
            {
                yield return null;
                if (Time.time > startTime + POINTING_INDICATOR_DELAY)
                    yield return DisplayPointingIndicator(nextStore, true);
            }
            yield return DisplayPointingIndicator(nextStore, false);

            ///AUDIO PRESENTATION OF OBJECT///
            if (i != deliveries - 1)
            {
                playerMovement.Freeze();
                AudioClip deliveredItem = (practice && trialNumber == 0 && i == craft_shop_delivery_num)
                    ? nextStore.PopPracticeItem(LanguageSource.GetLanguageString("confetti"))
                    : nextStore.PopItem();

                if (useNicls && (trialNumber >= NUM_READ_ONLY_TRIALS))
                {
                    yield return new WaitForSeconds(WORD_PRESENTATION_DELAY);
                    if (practice)
                        niclsInterface.SendEncodingToNicls(1);
                    else
                        yield return WaitForClassifier();
                }
                else
                {
                    float wordDelay = Random.Range(WORD_PRESENTATION_DELAY - WORD_PRESENTATION_JITTER,
                                               WORD_PRESENTATION_DELAY + WORD_PRESENTATION_JITTER);
                    yield return new WaitForSeconds(wordDelay);
                }

                string deliveredItemName = deliveredItem.name;
                audioPlayback.clip = deliveredItem;
                audioPlayback.Play();
                scriptedEventReporter.ReportScriptedEvent("object presentation begins",
                                                          new Dictionary<string, object>() { {"trial number", continuousTrialNum},
                                                                                             {"item name", deliveredItemName},
                                                                                             {"store name", nextStore.GetStoreName()},
                                                                                             {"serial position", i+1},
                                                                                             {"player position", playerMovement.transform.position.ToString()},
                                                                                             {"store position", nextStore.transform.position.ToString()}});
                string lstFilepath = practice
                            ? System.IO.Path.Combine(UnityEPL.GetDataPath(), "practice-" + continuousTrialNum.ToString() + ".lst")
                            : System.IO.Path.Combine(UnityEPL.GetDataPath(), continuousTrialNum.ToString() + ".lst");
                AppendWordToLst(lstFilepath, deliveredItemName);
                this_trial_presented_stores.Add(nextStore);
                all_presented_objects.Add(deliveredItemName);

                SetRamulatorState("WORD", true, new Dictionary<string, object>() { { "word", deliveredItemName } });
                //add visuals with sound
                messageImageDisplayer.deliver_item_visual_dislay.SetActive(true);
                messageImageDisplayer.SetDeliverItemText(deliveredItemName);
                yield return SkippableWait(AUDIO_TEXT_DISPLAY);
                messageImageDisplayer.deliver_item_visual_dislay.SetActive(false);

                SetRamulatorState("WORD", false, new Dictionary<string, object>() { { "word", deliveredItemName } });

                scriptedEventReporter.ReportScriptedEvent("audio presentation finished",
                                                          new Dictionary<string, object>());
                playerMovement.Unfreeze();
            }
        }

        messageImageDisplayer.please_find_the_blah_reminder.SetActive(false);

        SetRamulatorState("ENCODING", false, new Dictionary<string, object>());

        if (practice)
            scriptedEventReporter.ReportScriptedEvent("stop practice deliveries", new Dictionary<string, object>());
        else
            scriptedEventReporter.ReportScriptedEvent("stop deliveries", new Dictionary<string, object>());
    }

    private IEnumerator DoTrials(Environment environment, int numTrials, int subSessionNum, bool practice = false, int trialNumOffset = 0)
    {
        scriptedEventReporter.ReportScriptedEvent("start trials", new Dictionary<string, object>());
        for (int trialNumber = 0; trialNumber < numTrials; trialNumber++)
        {
            int continuousTrialNum = trialNumber + trialNumOffset;
            // Required break
            //if (NICLS_COURIER && !practice)
            //{
            //    if ((sessionNumber < DOUBLE_TOWN_LEARNING_DAYS) && (trialNumber == 1 || trialNumber == 3))
            //        yield return DoBreak();
            //    else if ((sessionNumber >= DOUBLE_TOWN_LEARNING_DAYS) && (trialNumber == 3 || trialNumber == 6))
            //        yield return DoBreak();
            //}

            // Turn off ReadOnlyState
            if (NICLS_COURIER && !practice && trialNumber == NUM_READ_ONLY_TRIALS)
                niclsInterface.SendReadOnlyStateToNicls(0);

            // EFR instructions
            if (Config.efrEnabled && practice
                && trialNumber == EFR_PRACTICE_TRIAL_NUM && subSessionNum == 0) // JPB: TODO: Nick fix
            {
                yield return DoVideo(LanguageSource.GetLanguageString("play movie"),
                             LanguageSource.GetLanguageString("efr intro video"),
                             VideoSelector.VideoType.EfrIntro);
                yield return DoEfrKeypressCheck();
                yield return DoEfrKeypressPractice();
            }

            // Next day message
            SetRamulatorState("WAITING", true, new Dictionary<string, object>());
            if (!DeliveryItems.ItemsExhausted())
            {
                BlackScreen();
                if (practice && trialNumber > 0)
                {
                    messageImageDisplayer.SetGeneralBigMessageText(mainText: "next practice day");
                    yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);
                }
                else if (trialNumber > 0)
                {
                    messageImageDisplayer.SetGeneralBigMessageText(mainText: "next day");
                    yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);
                }

                // Skip the rest of the trials
                if (InputManager.GetButton("Secret"))
                {
                    SetRamulatorState("WAITING", false, new Dictionary<string, object>());
                    break;
                }
            }
            else
            {
                yield return PressAnyKey(LanguageSource.GetLanguageString("final recall"));
                break;
            }
            SetRamulatorState("WAITING", false, new Dictionary<string, object>());

            // Set ramulator trial start
            if (useRamulator)
                ramulatorInterface.BeginNewTrial(continuousTrialNum);

            // Do deliveries and recall
            yield return DoDeliveries(environment, trialNumber, continuousTrialNum, practice);
            if (!(practice && trialNumber < EFR_PRACTICE_TRIAL_NUM))
                yield return DoFixation(PAUSE_BEFORE_RETRIEVAL, practice);
            yield return DoRecall(trialNumber, continuousTrialNum, practice);
        }
        scriptedEventReporter.ReportScriptedEvent("stop trials", new Dictionary<string, object>());
    }

    private void ColorPointer(Color color)
    {
        foreach (Renderer eachRenderer in pointer.GetComponentsInChildren<Renderer>())
            eachRenderer.material.SetColor("_Color", color);
    }

    private IEnumerator DoPointingTask(StoreComponent nextStore)
    {
        pointer.SetActive(true);
        ColorPointer(new Color(0.5f, 0.5f, 1f));
        pointer.transform.eulerAngles = new Vector3(0, rng.Next(360), 0);
        scriptedEventReporter.ReportScriptedEvent("pointing begins", new Dictionary<string, object> { { "start direction", pointer.transform.eulerAngles.y }, { "store", nextStore.GetStoreName() } });
        pointerMessage.SetActive(true);
        pointerText.text = LanguageSource.GetLanguageString("next package prompt") + "<b>" +
                           LanguageSource.GetLanguageString(nextStore.GetStoreName()) + "</b>" + ". " +
                           LanguageSource.GetLanguageString("please point") +
                           LanguageSource.GetLanguageString(nextStore.GetStoreName()) + "." + "\n\n" +
                           LanguageSource.GetLanguageString("joystick");
        yield return null;
        while (!InputManager.GetButtonDown("Continue"))
        {
            yield return null;
            if (!playerMovement.IsDoubleFrozen())
                pointer.transform.eulerAngles = pointer.transform.eulerAngles + new Vector3(0, InputManager.GetAxis("Horizontal") * Time.deltaTime * pointerRotationSpeed, 0);
        }

        float pointerError = PointerError(nextStore.gameObject);
        if (pointerError < Mathf.PI / 12)
        {
            pointerParticleSystem.Play();
            pointerText.text = LanguageSource.GetLanguageString("correct to within") + Mathf.RoundToInt(pointerError * Mathf.Rad2Deg).ToString() + ". ";
        }
        else
        {
            pointerText.text = LanguageSource.GetLanguageString("wrong by") + Mathf.RoundToInt(pointerError * Mathf.Rad2Deg).ToString() + ". ";
        }

        float wrongness = pointerError / Mathf.PI;
        ColorPointer(new Color(wrongness, 1 - wrongness, .2f));
        bool improvement = starSystem.ReportScore(1 - wrongness);

        if (improvement)
            pointerText.text = pointerText.text + LanguageSource.GetLanguageString("rating improved");

        pointerText.text = pointerText.text + "\n" + LanguageSource.GetLanguageString("continue");

        yield return PointArrowToStore(nextStore.gameObject, ARROW_ROTATION_SPEED, ARROW_CORRECTION_TIME);
        while (!InputManager.GetButtonDown("Continue"))
            yield return null;
        scriptedEventReporter.ReportScriptedEvent("pointer message cleared", new Dictionary<string, object>());
        pointerParticleSystem.Stop();
        pointer.SetActive(false);
        pointerMessage.SetActive(false);
    }

    private IEnumerator DoBreak()
    {
        scriptedEventReporter.ReportScriptedEvent("start required break", new Dictionary<string, object>());
        BlackScreen();
        textDisplayer.DisplayText("break prompt", LanguageSource.GetLanguageString("break"));
        while (!Input.GetKeyDown(KeyCode.Space))
            yield return null;
        WorldScreen(); // JPB: TODO: Erase this
        textDisplayer.ClearText();
        scriptedEventReporter.ReportScriptedEvent("stop required break", new Dictionary<string, object>());
    }

    private IEnumerator DoMovie(int[] movieIndices)
    {
        int clipNum = 0;
        scriptedEventReporter.ReportScriptedEvent("start movie", new Dictionary<string, object>{ {"clip num", clipNum} });
        BlackScreen();
        yield return DoVideo(LanguageSource.GetLanguageString("play movie"),
                             LanguageSource.GetLanguageString("nicls movie"),
                             VideoSelector.VideoType.NiclsMovie,
                             movieIndices[sessionNumber]);
        WorldScreen(); // JPB: TODO: Erase this
        scriptedEventReporter.ReportScriptedEvent("stop movie", new Dictionary<string, object>());
    }

    private bool lastPointingIndicatorState = false;
    private IEnumerator DisplayPointingIndicator(StoreComponent nextStore, bool enable = false)
    {
        if (enable) {
            if (lastPointingIndicatorState != enable)
                scriptedEventReporter.ReportScriptedEvent("continuous pointer", new Dictionary<string, object>());
            pointer.SetActive(true);
            ColorPointer(new Color(0.5f, 0.5f, 1f));
            yield return PointArrowToStore(nextStore.gameObject);
        } else {
            pointer.SetActive(false);
            yield return null;
        }
        lastPointingIndicatorState = enable;
    }

    private IEnumerator PointArrowToStore(GameObject pointToStore, float arrowRotationSpeed = 0f, float arrowCorrectionTime = 0f)
    {
        float rotationSpeed = arrowRotationSpeed == 0 ? 1f : arrowRotationSpeed * Time.deltaTime;
        float startTime = Time.time;
        
        do {
            yield return null;
            Vector3 lookDirection = pointToStore.transform.position - pointer.transform.position;
            pointer.transform.rotation = Quaternion.Slerp(pointer.transform.rotation,
                                                          Quaternion.LookRotation(lookDirection),
                                                          rotationSpeed);
        } while (Time.time < startTime + arrowCorrectionTime) ;
    }

    private float PointerError(GameObject toStore)
    {
        Vector3 lookDirection = toStore.transform.position - pointer.transform.position;
        float correctYRotation = Quaternion.LookRotation(lookDirection).eulerAngles.y;
        float actualYRotation = pointer.transform.eulerAngles.y;
        float offByRads = Mathf.Abs(correctYRotation - actualYRotation) * Mathf.Deg2Rad;
        if (offByRads > Mathf.PI)
            offByRads = Mathf.PI * 2 - offByRads;

        scriptedEventReporter.ReportScriptedEvent("pointing finished", new Dictionary<string, object>() { {"correct direction (degrees)", correctYRotation},
                                                                                                          {"pointed direction (degrees)", actualYRotation} });

        return offByRads;
    }

    private void AppendWordToLst(string lstFilePath, string word)
    {
        System.IO.FileInfo lstFile = new System.IO.FileInfo(lstFilePath);
        bool firstLine = !lstFile.Exists;
        if (firstLine)
            lstFile.Directory.Create();
        lstFile.Directory.Create();
        using (System.IO.StreamWriter w = System.IO.File.AppendText(lstFilePath))
        {
            if (!firstLine)
                w.Write(System.Environment.NewLine);
            w.Write(word);
        }
    }

    private Environment EnableEnvironment()
    {
        // We want randomness for different people, but consistency between sessions
        foreach (string name in UnityEPL.GetParticipants())
            Debug.Log(name);
        System.Random reliableRandom = new System.Random(UnityEPL.GetParticipants()[0].GetHashCode());
        Environment environment = environments[reliableRandom.Next(environments.Length)];
        environment.parent.SetActive(true);
        return environment;
    }

    private IEnumerator DoFreeRecallDisplay(float waitTime)
    {
        BlackScreen();
        yield return messageImageDisplayer.DisplayMessageTimed(
            messageImageDisplayer.free_recall_display,
            waitTime);
    }

    private IEnumerator DoEfrDisplay(string title, float waitTime, bool practice = false)
    {
        BlackScreen();
        SetEfrDisplay();

        messageImageDisplayer.SetEfrText(titleText: title);
        messageImageDisplayer.SetEfrElementsActive(speakNowText: true);
        yield return messageImageDisplayer.DisplayMessageTimedLRKeypressBold(
                messageImageDisplayer.efr_display, waitTime,
                efrLeftLogMsg, efrRightLogMsg, practice);
    }

    private void SetEfrDisplay(EfrButton? keypressPractice = null)
    {
        if (efrCorrectButtonSide == EfrButton.RightButton)
        {
            efrLeftLogMsg = "incorrect";
            efrRightLogMsg = "correct";
            if (keypressPractice == EfrButton.LeftButton)
                messageImageDisplayer.SetEfrText(leftButton: "efr keypress practice left button incorrect message",
                                                 rightButton: "efr right button correct message");
            else if (keypressPractice == EfrButton.RightButton)
                messageImageDisplayer.SetEfrText(leftButton: "efr left button incorrect message",
                                                 rightButton: "efr keypress practice right button correct message");
            else
                messageImageDisplayer.SetEfrText(leftButton: "efr left button incorrect message",
                                                 rightButton: "efr right button correct message");
        }
        else if (efrCorrectButtonSide == EfrButton.LeftButton)
        {
            efrLeftLogMsg = "correct";
            efrRightLogMsg = "incorrect";
            if (keypressPractice == EfrButton.LeftButton)
                messageImageDisplayer.SetEfrText(leftButton: "efr keypress practice left button correct message",
                                                 rightButton: "efr right button incorrect message");
            if (keypressPractice == EfrButton.RightButton)
                messageImageDisplayer.SetEfrText(leftButton: "efr left button correct message",
                                                 rightButton: "efr keypress practice right button incorrect message");
            else
                messageImageDisplayer.SetEfrText(leftButton: "efr left button correct message",
                                                 rightButton: "efr right button incorrect message");
        }
    }

    private IEnumerator DoEfrKeypressCheck()
    {
        scriptedEventReporter.ReportScriptedEvent("start efr keypress check", new Dictionary<string, object>());
        BlackScreen();

        // Display intro message
        messageImageDisplayer.SetGeneralMessageText(mainText: "efr check main");
        yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_message_display);

        // Setup EFR display
        SetEfrDisplay();

        // Ask for right button press
        messageImageDisplayer.SetEfrText(descriptiveText: "efr check description right button");
        messageImageDisplayer.SetEfrElementsActive(descriptiveText: true, controllerRightButtonImage: true);
        yield return messageImageDisplayer.DisplayMessageKeypressBold(
           messageImageDisplayer.efr_display, EfrButton.RightButton);
        yield return messageImageDisplayer.DisplayMessageTimedLRKeypressBold(
            messageImageDisplayer.efr_display, 1f, efrLeftLogMsg, efrRightLogMsg);

        // Ask for left button press
        messageImageDisplayer.SetEfrText(descriptiveText: "efr check description left button");
        messageImageDisplayer.SetEfrElementsActive(descriptiveText: true, controllerLeftButtonImage: true);
        yield return messageImageDisplayer.DisplayMessageKeypressBold(
            messageImageDisplayer.efr_display, EfrButton.LeftButton);
        yield return messageImageDisplayer.DisplayMessageTimedLRKeypressBold(
            messageImageDisplayer.efr_display, 1f, efrLeftLogMsg, efrRightLogMsg);

        scriptedEventReporter.ReportScriptedEvent("stop efr keypress check", new Dictionary<string, object>());
    }

    private IEnumerator DoEfrKeypressPractice()
    {
        scriptedEventReporter.ReportScriptedEvent("start efr keypress practice", new Dictionary<string, object>());
        BlackScreen();

        // Display intro message
        messageImageDisplayer.SetGeneralBigMessageText(titleText: "efr keypress practice main", 
                                                       mainText: "efr keypress practice description");
        yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);

        // Setup EFR display
        messageImageDisplayer.SetEfrElementsActive();

        // Show equal number of left and right keypress practices in random order
        List<EfrButton> lrButtonIndicator = Enumerable.Repeat(EfrButton.LeftButton, EFR_KEYPRESS_PRACTICES)
                                                      .Concat(Enumerable.Repeat(EfrButton.RightButton, EFR_KEYPRESS_PRACTICES))
                                                      .ToList();
        lrButtonIndicator.Shuffle(rng);

        foreach (var buttonIndicator in lrButtonIndicator)
        {
            SetEfrDisplay();
            float efrKeypressPracticedelay = Random.Range(EFR_KEYPRESS_PRACTICE_DELAY - EFR_KEYPRESS_PRACTICE_JITTER,
                                                          EFR_KEYPRESS_PRACTICE_DELAY + EFR_KEYPRESS_PRACTICE_JITTER);
            yield return messageImageDisplayer.DisplayMessageTimed(
                messageImageDisplayer.efr_display, efrKeypressPracticedelay);

            if (buttonIndicator == EfrButton.LeftButton)
            {
                SetEfrDisplay(EfrButton.LeftButton);
                messageImageDisplayer.SetEfrTextResize(LeftButtonSize: 0.3f);
                yield return messageImageDisplayer.DisplayMessageKeypressBold(
                    messageImageDisplayer.efr_display, EfrButton.LeftButton);
                messageImageDisplayer.SetEfrTextResize(LeftButtonSize: -0.3f);
            }
            else if (buttonIndicator == EfrButton.RightButton)
            {
                SetEfrDisplay(EfrButton.RightButton);
                messageImageDisplayer.SetEfrTextResize(rightButtonSize: 0.3f);
                yield return messageImageDisplayer.DisplayMessageKeypressBold(
                    messageImageDisplayer.efr_display, EfrButton.RightButton);
                messageImageDisplayer.SetEfrTextResize(rightButtonSize: -0.3f);
            }
        }

        scriptedEventReporter.ReportScriptedEvent("stop efr keypress practice", new Dictionary<string, object>());
    }

    //WAITING, INSTRUCT, COUNTDOWN, ENCODING, WORD, DISTRACT, RETRIEVAL
    protected override void SetRamulatorState(string stateName, bool state, Dictionary<string, object> extraData)
    {
        if (OnStateChange != null)
            OnStateChange(stateName, state);
        if (useRamulator)
            ramulatorInterface.SetState(stateName, state, extraData);
    }

    private IEnumerator SkippableWait(float waitTime)
    {
        float startTime = Time.time;
        while (Time.time < startTime + waitTime)
        {
            if (InputManager.GetButtonDown("Secret"))
                break;
            yield return null;
        }
    }

    private IEnumerator WaitForClassifier()
    {
        scriptedEventReporter.ReportScriptedEvent("start classifier wait", new Dictionary<string, object>());
        WaitUntilWithTimeout waitForClassifier = new WaitUntilWithTimeout(niclsInterface.classifierReady, 5);
        yield return waitForClassifier;
        scriptedEventReporter.ReportScriptedEvent("stop classifier wait",
                                                  new Dictionary<string, object> { {"timed out", waitForClassifier.timedOut()} });
        Debug.Log("CLASSIFIER SAID TO GO ---------------------------------------------------------");
    }

    public string GetStoreNameFromGameObjectName(string gameObjectName)
    {
        foreach (StoreComponent store in environments[0].stores)
            if (store.gameObject.name.Equals(gameObjectName))
                return store.GetStoreName();
        throw new UnityException("That store game object doesn't exist in the stores list.");
    }
}

public static class IListExtensions
{
    /// <summary>
    /// Knuth (Fisher-Yates) Shuffle
    /// Shuffles the element order of the specified list.
    /// </summary>
    public static void Shuffle<T>(this IList<T> list, System.Random rng)
    {
        var count = list.Count;
        for (int i = 0; i < count; ++i)
        {
            int r = rng.Next(i, count);
            T tmp = list[i];
            list[i] = list[r];
            list[r] = tmp;
        }
    }
}
