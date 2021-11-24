using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Luminosity.IO;
using System.Linq;
using System.Runtime.InteropServices;

using Accord.Math;
using Accord.Statistics.Distributions.Multivariate;
using Accord.Statistics.Distributions.Univariate;

using static MessageImageDisplayer;

[System.Serializable]
public struct Environment
{
    public GameObject parent;
    public StoreComponent[] stores;
}

public enum StorePointType 
{
    SpacialPosition,
    SerialPosition,
    Random,
}

public class DeliveryExperiment : CoroutineExperiment
{   
    #if UNITY_WEBGL
        [DllImport("__Internal")]
        private static extern void EndTask();
    #endif

    public delegate void StateChange(string stateName, bool on);
    public static StateChange OnStateChange;

    private static int sessionNumber = -1;
    private static int continuousSessionNumber = -1;
    private static string expName;

    // TODO: JPB: Make these configuration variables

    // Experiment type
    private const bool HOSPITAL_COURIER = true;
    private const bool NICLS_COURIER = false;
    private const bool GRANT_VERSION = false;
    #if !UNITY_WEBGL
        private const bool COURIER_ONLINE = false;
    #else
        private const bool COURIER_ONLINE = true;
    #endif // !UNITY_WEBGL
    
    private const string COURIER_VERSION = COURIER_ONLINE ? "v5.0.0online" : "v5.1.3";

    private const string RECALL_TEXT = "*******"; // TODO: JPB: Remove this and use display system
    // Constants moved to the Config File
    //private const int DELIVERIES_PER_TRIAL = LESS_DELIVERIES ? 3 : (NICLS_COURIER ? 16 : 13);
    //private const int PRACTICE_DELIVERIES_PER_TRIAL = 4;
    //private const int TRIALS_PER_SESSION = LESS_TRIALS ? 2 : (NICLS_COURIER ? 5 : 8);
    //private const int TRIALS_PER_SESSION_SINGLE_TOWN_LEARNING = LESS_TRIALS ? 2 : 5;
    //private const int TRIALS_PER_SESSION_DOUBLE_TOWN_LEARNING = LESS_TRIALS ? 1 : 3;
    private const int EFR_PRACTICE_TRIAL_NUM = 1;
    private const int NUM_CLASSIFIER_NORMALIZATION_TRIALS = 1;
    private const int HOSPTIAL_TOWN_LEARNING_NUM_STORES = 8;
    private const int SINGLE_TOWN_LEARNING_SESSIONS = 1;
    private const int DOUBLE_TOWN_LEARNING_SESSIONS = 0;
    private const int POINTING_INDICATOR_DELAY = NICLS_COURIER ? 12 : 48;
    private const int EFR_KEYPRESS_PRACTICES = 10;
    private const float FRAME_TEST_LENGTH = 20f;
    private const float MIN_FAMILIARIZATION_ISI = 0.4f;
    private const float MAX_FAMILIARIZATION_ISI = 0.6f;
    private const float FAMILIARIZATION_PRESENTATION_LENGTH = 1.5f;
    private const float RECALL_MESSAGE_DISPLAY_LENGTH = 6f;
    private const float RECALL_TEXT_DISPLAY_LENGTH = 1f;
    private const float FREE_RECALL_LENGTH = 90f;
    private const float PRACTICE_FREE_RECALL_LENGTH = 25f;
    private const float STORE_FINAL_RECALL_LENGTH = 90f;
    private const float OBJECT_FINAL_RECALL_LENGTH = NICLS_COURIER ? 120f : COURIER_ONLINE ? 240f : 180f;
    private const float TIME_BETWEEN_DIFFERENT_RECALL_PHASES = 2f;
    private const float CUED_RECALL_TIME_PER_STORE = 10f;
    private const float MIN_CUED_RECALL_TIME_PER_STORE = 2f;
    private const float MAX_CUED_RECALL_TIME_PER_STORE = NICLS_COURIER ? 6f : 7.5f;
    private const float POINTING_CORRECT_THRESHOLD = Mathf.PI / 12; // 15°
    private const float ARROW_CORRECTION_TIME = 3f;
    private const float ARROW_ROTATION_SPEED = 1f;
    private const float PAUSE_BEFORE_RETRIEVAL = 10f;
    private const float DISPLAY_ITEM_PAUSE = 5f;
    private const float AUDIO_TEXT_DISPLAY = 1.6f;
    private const float WORD_PRESENTATION_DELAY = NICLS_COURIER ? 1f : 1.25f; // TODO: JPB: Fix this is NICLS
    private const float WORD_PRESENTATION_JITTER = 0.25f;
    private const float EFR_KEYPRESS_PRACTICE_DELAY = 2.25f;
    private const float EFR_KEYPRESS_PRACTICE_JITTER = 0.25f;

    // Keep as hardcoded values
    private const bool STAR_SYSTEM_ACTIVE = true;

    private const int NICLS_READ_ONLY_SESSIONS = 8;
    private const int NICLS_CLOSED_LOOP_SESSIONS = 4;

    private const int NUM_MUSIC_VIDEOS = 6;
    private const int NUM_MUSIC_VIDEOS_PER_SESSION = 2;
    private readonly int[] MUSIC_VIDEO_RECALL_SESSIONS = { 9, 10, 11 }; // Can't make const arrays in c#
    private const int MUSIC_VIDEO_PROMPT_TIME = 5;
    private const int MUSIC_VIDEO_RECALL_TIME = 175;

    public Camera regularCamera;
    public Camera blackScreenCamera;
    public Familiarizer familiarizer;
    public MessageImageDisplayer messageImageDisplayer;

    private static bool useRamulator = false;
    private static bool useNiclServer = false;
    #if !UNITY_WEBGL // Syncbox, Ramulator, and NICLS
        private Syncbox syncs;
        public RamulatorInterface ramulatorInterface;
        public NiclsInterface niclsInterface;
    #endif // !UNITY_WEBGL

    public PlayerMovement playerMovement;
    public GameObject pointer;
    public ParticleSystem pointerParticleSystem;
    public GameObject pointerMessage;
    public UnityEngine.UI.Text pointerText;
    public StarSystem starSystem;
    public DeliveryItems deliveryItems;
    public Pauser pauser;

    public float pointerRotationSpeed = 10f;

    public GameObject memoryWordCanvas;

    public Environment[] environments;
    private Environment environment;

    private System.Random rng = new System.Random();

    private ActionButton efrCorrectButtonSide = ActionButton.RightButton;
    private string efrLeftLogMsg = "incorrect";
    private string efrRightLogMsg = "correct";

    private List<StoreComponent> thisTrialPresentedStores = new List<StoreComponent>();
    private List<string> allPresentedObjects = new List<string>();

    List<NiclsClassifierType> niclsClassifierTypes = null;


    // Typed Response fields
    public GameObject freeInputField;
    public UnityEngine.UI.InputField freeResponse;
    public UnityEngine.UI.Text placeHolder;
    public GameObject cuedInputField;
    public UnityEngine.UI.InputField cuedResponse;


    // Frame testing variables
    private static bool isFrameTesting = false;
    private static int fpsValue = 0;
    private static float updateRateSeconds = 4.0f;
    private static int frameCount = 0;
    private float dt = 0.0f;
    private float fps = 0.0f;
    private List<int> fpsList = new List<int>();

    // Temporal/Distance store points
    // private StorePointType storePointType = StorePointType.Random;
    // private int free_index = 0;
    // private int value_index = 0;

    // These names are used in for what is sent to the log
    // If you change them, then you have to change the event processing (or the logging code)
    private enum NiclsClassifierType
    {
        Pos,
        Neg,
        Sham
    }
    
    public static void ConfigureExperiment(bool newUseRamulator, bool newUseNiclServer, int newSessionNumber, string newExpName)
    {
        #if !UNITY_WEBGL // Ramulator and NICLS
            useRamulator = newUseRamulator;
            useNiclServer = newUseNiclServer;
        #endif // !UNITY_WEBGL
        sessionNumber = newSessionNumber;
        continuousSessionNumber = useNiclServer ? NICLS_READ_ONLY_SESSIONS + sessionNumber :
                                  sessionNumber;
        expName = newExpName;
        Config.experimentConfigName = expName;
    }

    void UncaughtExceptionHandler(object sender, UnhandledExceptionEventArgs args)
    {
        Exception e = (Exception)args.ExceptionObject;
        Debug.Log("UncaughtException: " + e.Message);
        Debug.Log("UncaughtException: " + e);

        Dictionary<string, object> exceptionData = new Dictionary<string, object>()
            { { "name", e.Message },
              { "traceback", e.ToString() } };
        scriptedEventReporter.ReportScriptedEvent("unhandled program exception", exceptionData);
    }

    void Update()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        
        // Courier Online Frame testing
        if (COURIER_ONLINE && isFrameTesting) {
            frameCount++;
            dt += Time.unscaledDeltaTime;
            if (dt > 1.0 / updateRateSeconds)
            {
                fps = frameCount / dt;
                frameCount = 0;
                dt -= 1.0f / updateRateSeconds;
            }
            fpsValue = (int)Math.Round(fps);
            // TODO: JPB: (Hokua) Make this use SetFPSDisplayText is MessageImageDisplayer
            //            It is likely that all this Update() frame testing code should be moved into MessageImageDisplayer
            messageImageDisplayer.fpsDisplayText.text = fpsValue.ToString();
            fpsList.Add(fpsValue);

            Dictionary<string, object> fpsValueDict = new Dictionary<string, object>();
            fpsValueDict.Add("fps value", fpsValue);
            scriptedEventReporter.ReportScriptedEvent("fps value", fpsValueDict);
        }

    }

    void Start()
    {
        if (UnityEPL.viewCheck)
            return;
        
        if (COURIER_ONLINE)
            ConfigureExperiment(false, false, 0, "CourierOnline");

        if (sessionNumber == -1)
            throw new UnityException("Please call ConfigureExperiment before beginning the experiment.");

        AppDomain currentDomain = AppDomain.CurrentDomain;
        currentDomain.UnhandledException += new UnhandledExceptionEventHandler(UncaughtExceptionHandler);

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.SetCursor(new Texture2D(0, 0), new Vector2(0, 0), CursorMode.ForceSoftware);

        // Turn player particles and falling leaves off for Nicls Courier
        if (NICLS_COURIER)
        {
            GameObject.Find("Player/player perspective/Particle System").SetActive(false);
            var trees = new List<int> { 26, 27, 28, 29, 30, 31, 32, 33, 34, 44 };
            string treeStr = "henry tc3 environment/edge/Tree ({0})/Particle System";
            foreach (int treeIndex in trees)
                GameObject.Find(string.Format(treeStr, treeIndex)).SetActive(false);
        }
        
        #if !UNITY_WEBGL // Syncbox
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = 300;
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
                System.Random reliableRandom = deliveryItems.ReliableRandom();
                efrCorrectButtonSide = (ActionButton)reliableRandom.Next(0, 2);
            }
        #else // UNITY_WEBGL
            UnityEPL.AddParticipant(System.Guid.NewGuid().ToString());
            UnityEPL.SetExperimentName("COURIER_ONLINE");
            UnityEPL.SetSessionNumber(0);
        #endif

        Dictionary<string, object> sceneData = new Dictionary<string, object>();
        sceneData.Add("sceneName", "MainGame");
        scriptedEventReporter.ReportScriptedEvent("loadScene", sceneData);

        StartCoroutine(ExperimentCoroutine());
    }

    private IEnumerator ExperimentCoroutine()
    {
        Debug.Log(UnityEPL.GetDataPath());

        foreach (string name in UnityEPL.GetParticipants())
            Debug.Log(name);

        // Write versions to logfile
        LogVersions(expName);

        if (COURIER_ONLINE)
            yield return GetOnlineConfig();

        // Setup Environment
        yield return EnableEnvironment();

        // Frame Rate Test
        if (COURIER_ONLINE)
            yield return DoFrameTest();

        #if !UNITY_WEBGL // NICLS
            // Setup Ramulator
            if (useRamulator)
                yield return ramulatorInterface.BeginNewSession(sessionNumber);

            // Setup NiclServer
            if (useNiclServer)
            {
                yield return niclsInterface.BeginNewSession(sessionNumber);
                SetupNiclsClassifier();
                niclsInterface.SendReadOnlyState(1);
            }
            else
            {
                yield return niclsInterface.BeginNewSession(sessionNumber, true);
            }
        #endif // !UNITY_WEBGL

        // Intros
        yield return DoIntros();

        // Town Learning
        int trialsForFirstSubSession = Config.trialsPerSession;
        if (sessionNumber < SINGLE_TOWN_LEARNING_SESSIONS + DOUBLE_TOWN_LEARNING_SESSIONS)
        {
            if (NICLS_COURIER && !useNiclServer)
            {
                Debug.Log("Town Learning Phase");
                trialsForFirstSubSession = Config.trialsPerSessionSingleTownLearning;
                messageImageDisplayer.SetGeneralMessageText("town learning title", "town learning main 1");
                yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_message_display);
                WorldScreen();
                yield return DoTownLearning(0, environment.stores.Length);

                if (sessionNumber < DOUBLE_TOWN_LEARNING_SESSIONS)
                {
                    trialsForFirstSubSession = Config.trialsPerSessionDoubleTownLearning;
                    messageImageDisplayer.SetGeneralMessageText("town learning title", "town learning main 2");
                    yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_message_display);
                    WorldScreen();
                    yield return DoTownLearning(1, environment.stores.Length);
                }
            }
            else if (HOSPITAL_COURIER)
            {
                Debug.Log("Town Learning Phase");
                trialsForFirstSubSession = Config.trialsPerSessionSingleTownLearning;
                messageImageDisplayer.SetGeneralMessageText("town learning title", "town learning main 1");
                yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_message_display);
                WorldScreen();
                yield return DoTownLearning(0, environment.stores.Length / 2);
            }
        }


        // Task Recap Instructions and Practice Trials
        // Using useNiclsServer to skip practices on closed loop sessions
        if (sessionNumber == 0 && !useNiclServer)
            yield return DoPracticeTrials(2);

        // Delay note
        if (useNiclServer)
        {
            messageImageDisplayer.SetGeneralBigMessageText(titleText: "classifier delay note title",
                                                           mainText: "classifier delay note main");
            yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);
        }

        // Player Reminders/Tips/Notes
        messageImageDisplayer.SetGeneralBigMessageText(titleText: "navigation note title",
                                                       mainText: "navigation note main");
        yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);

        // 1st Real Trials
        int trialsThisSession = 0;
        yield return DoSubSession(0, trialsThisSession, trialsForFirstSubSession);
        trialsThisSession += trialsForFirstSubSession;

        // Break / MV Playing / 2nd Real Trials / MV Recall / MV questions
        if (NICLS_COURIER)
        {
            var videoOrder = GenMusicVideoOrder();
            yield return DoBreak();
            yield return DoMusicVideos(videoOrder);
            yield return DoSubSession(1, trialsThisSession, Config.trialsPerSession);
            trialsThisSession += Config.trialsPerSession;
            if (MUSIC_VIDEO_RECALL_SESSIONS.Contains(continuousSessionNumber))
                yield return DoMusicVideoRecall(videoOrder);

            if (continuousSessionNumber == NICLS_READ_ONLY_SESSIONS + NICLS_CLOSED_LOOP_SESSIONS - 1)
            {
                var ratings = new string[] { "music video question 0 rating 0", "music video question 0 rating 1" };
                messageImageDisplayer.SetSlidingScale2Text(mainText: "music video question 0 title",
                                                           ratings: ratings);
                StartCoroutine(messageImageDisplayer.DisplaySlidingScale2Message(messageImageDisplayer.sliding_scale_2_display));
            }
        }

        // Ending Message
        string endMessage = NICLS_COURIER
            ? LanguageSource.GetLanguageString("end message")
            : LanguageSource.GetLanguageString("end message scored") + starSystem.CumulativeRating().ToString("+#.##;-#.##");
        textDisplayer.DisplayText("end text", endMessage);

        #if !UNITY_WEBGL // WebGL DLL
            // TODO: JPB: (Hokua) Wait for button press to quit
            while (true)
                yield return null;
        #else
            #if !UNITY_EDITOR // LC: remove after upgrade
                WebGLInput.captureAllKeyboardInput = false;
            #endif // !UNITY_EDITOR
            yield return new WaitForSeconds(5.0f);
            EndTask();
        # endif // !UNITY_WEBGL
    }

    private IEnumerator DoSubSession(int subSessionNum, int priorTrialsThisSession, int trialsPerSubSession)
    {
        BlackScreen();
        
        // Real trials
        if (Config.efrEnabled)
            if (Config.twoBtnEfrEnabled)
                messageImageDisplayer.SetGeneralMessageText(mainText: "first day main", descriptiveText: "two btn er first day description");
            else
                messageImageDisplayer.SetGeneralMessageText(mainText: "first day main", descriptiveText: "one btn er first day description");
        else
            messageImageDisplayer.SetGeneralMessageText(mainText: "first day main");
        yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_message_display);
        yield return DoTrials(trialsPerSubSession, trialNumOffset: priorTrialsThisSession);

        // Final Recalls
        BlackScreen();
        if (NICLS_COURIER)
            yield return messageImageDisplayer.DisplayLanguageMessage(messageImageDisplayer.nicls_final_recall_messages);
        else
            yield return messageImageDisplayer.DisplayLanguageMessage(messageImageDisplayer.final_recall_messages);
        yield return DoFinalRecall(subSessionNum);
    }

    private IEnumerator DoFrameTest()
    {
        Debug.Log("Frame Testing");
        if (Config.skipFPS)
            yield break;

        Debug.Log("config works");

        messageImageDisplayer.SetGeneralBigMessageText(titleText: "frame test start title", mainText: "frame test start main");
        yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);

        WorldScreen();
        pointer.SetActive(false);
        isFrameTesting = true;
        messageImageDisplayer.fpsDisplay.SetActive(true);

        yield return new WaitForSeconds(FRAME_TEST_LENGTH);

        isFrameTesting = false;

        BlackScreen();
        int averageFps = (int)Math.Round(fpsList.Average());
        messageImageDisplayer.fpsDisplayText.text = "";

        Dictionary<string, object> fpsData = new Dictionary<string, object>();
        fpsData.Add("average FPS", averageFps);
        fpsData.Add("OS information", SystemInfo.operatingSystem);
        scriptedEventReporter.ReportScriptedEvent("FPS data", fpsData);

        yield return new WaitForSeconds(1.5f);

        messageImageDisplayer.fpsDisplay.SetActive(false);
        pointer.SetActive(true);
        playerMovement.Reset();


        messageImageDisplayer.SetFPSDisplayText(fpsValue: averageFps.ToString(), mainText: "frame test end main");
        yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);

        yield return new WaitForSeconds(1.5f);

        messageImageDisplayer.SetGeneralBigMessageText(mainText: "frame test continue main");
        yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);
    }

    private IEnumerator DoIntros()
    {   
        Debug.Log("DoIntros");
        if (Config.skipIntros)                                                                                          
            yield break;                                                                                                

        BlackScreen();

        if (NICLS_COURIER && sessionNumber == 0 && !useNiclServer)
        {
            yield return DoVideo(LanguageSource.GetLanguageString("play movie"),
                                 LanguageSource.GetLanguageString("standard intro video"),
                                 VideoSelector.VideoType.NiclsMainIntro);
        }
        else if (NICLS_COURIER) // sessionNumber >= 1 || useNiclServer                                              
        {
            yield return DoRecapInstructions();
        }
        else
        {
            yield return DoVideo(LanguageSource.GetLanguageString("play movie"),
                                 LanguageSource.GetLanguageString("standard intro video"),
                                 VideoSelector.VideoType.MainIntro);
        }

        if (HOSPITAL_COURIER && !COURIER_ONLINE)
            yield return DoSubjectSessionQuitPrompt(sessionNumber, LanguageSource.GetLanguageString("running participant"));

        #if !UNITY_WEBGL // Microphone
            yield return DoMicrophoneTest(LanguageSource.GetLanguageString("microphone test"),
                                          LanguageSource.GetLanguageString("after the beep"),
                                          LanguageSource.GetLanguageString("recording"),
                                          LanguageSource.GetLanguageString("playing"),
                                          LanguageSource.GetLanguageString("recording confirmation"));
        #endif // !UNITY_WEBGL
    }

    private IEnumerator DoRecapInstructions(bool forceFR = false)
    {
        GameObject[] messages;
        if (Config.efrEnabled && !forceFR) // TODO: JPB: Hospital handle ECR case
            if (Config.twoBtnEfrEnabled)
                messages = messageImageDisplayer.recap_instruction_messages_efr_2btn_en;
            else
                messages = messageImageDisplayer.recap_instruction_messages_efr_en;
        else
            messages = messageImageDisplayer.recap_instruction_messages_fr_en;

        foreach (var message in messages)
            yield return messageImageDisplayer.DisplayMessage(message);
    }

    private IEnumerator DoFamiliarization()
    {
        yield return messageImageDisplayer.DisplayLanguageMessage(messageImageDisplayer.store_images_presentation_messages);
        yield return familiarizer.DoFamiliarization(MIN_FAMILIARIZATION_ISI, MAX_FAMILIARIZATION_ISI, FAMILIARIZATION_PRESENTATION_LENGTH);
    }

    private IEnumerator DoTownLearning(int trialNumber, int numDeliveries)
    {
        if (Config.skipTownLearning || InputManager.GetButton("Secret"))                                                
            yield break;                                                                                                

        scriptedEventReporter.ReportScriptedEvent("start town learning");
        messageImageDisplayer.please_find_the_blah_reminder.SetActive(true);

        thisTrialPresentedStores = new List<StoreComponent>();
        List<StoreComponent> unvisitedStores = new List<StoreComponent>(environment.stores);

        for (int i = 0; i < numDeliveries; i++)
        {
            StoreComponent nextStore = null;
            int random_store_index = -1;
            int tries = 0;

            do
            {
                tries++;
                random_store_index = UnityEngine.Random.Range(0, unvisitedStores.Count);
                nextStore = unvisitedStores[random_store_index];
            }
            while (nextStore.IsVisible() && tries < environment.stores.Length);

            unvisitedStores.RemoveAt(random_store_index);
            thisTrialPresentedStores.Add(nextStore);

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
                if (InputManager.GetButton("Secret"))
                    goto SkipRemainingDeliveries;
            }
            yield return DisplayPointingIndicator(nextStore, false);

            scriptedEventReporter.ReportScriptedEvent("store visited",
                new Dictionary<string, object>() { {"trial number", trialNumber},
                                                   {"store name", nextStore.GetStoreName()},
                                                   {"serial position", i+1},
                                                   {"player position", playerMovement.transform.position.ToString()},
                                                   {"store position", nextStore.transform.position.ToString()}});
        }

    SkipRemainingDeliveries:
        messageImageDisplayer.please_find_the_blah_reminder.SetActive(false);
        scriptedEventReporter.ReportScriptedEvent("stop town learning");
    }

    private IEnumerator DoDeliveries(int trialNumber, int continuousTrialNum, bool practice = false, bool skipLastDelivStores = false)
    {
        Dictionary<string, object> trialData = new Dictionary<string, object>();
        trialData.Add("trial number", continuousTrialNum);
        if (practice)
            scriptedEventReporter.ReportScriptedEvent("start practice deliveries", trialData);
        else
            scriptedEventReporter.ReportScriptedEvent("start deliveries", trialData);

        WorldScreen();

        SetRamulatorState("ENCODING", true, new Dictionary<string, object>());
        messageImageDisplayer.please_find_the_blah_reminder.SetActive(true);

        List<StoreComponent> unvisitedStores = new List<StoreComponent>(environment.stores);
        if (skipLastDelivStores)
            foreach (var store in thisTrialPresentedStores)
                unvisitedStores.Remove(store);
        thisTrialPresentedStores = new List<StoreComponent>();

        int deliveries = practice ? Config.deliveriesPerPracticeTrial : Config.deliveriesPerTrial;
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
                if (InputManager.GetButton("Secret"))
                    goto SkipRemainingDeliveries;
            }
            yield return DisplayPointingIndicator(nextStore, false);

            ///AUDIO PRESENTATION OF OBJECT///
            if (i != deliveries - 1)
            {
                playerMovement.Freeze();
                AudioClip deliveredItem = (practice && trialNumber == 0 && i == craft_shop_delivery_num)
                    ? nextStore.PopPracticeItem(LanguageSource.GetLanguageString("confetti"))
                    : nextStore.PopItem();

                #if !UNITY_WEBGL // NICLS
                    if (useNiclServer && !practice)
                    {
                        yield return new WaitForSeconds(WORD_PRESENTATION_DELAY);
                        if (trialNumber < NUM_CLASSIFIER_NORMALIZATION_TRIALS)
                            niclsInterface.SendEncoding(1);
                        else
                            yield return WaitForClassifier(niclsClassifierTypes[continuousTrialNum]);
                    }
                    else
                    {
                        float wordDelay = UnityEngine.Random.Range(WORD_PRESENTATION_DELAY - WORD_PRESENTATION_JITTER,
                                                WORD_PRESENTATION_DELAY + WORD_PRESENTATION_JITTER);
                        yield return new WaitForSeconds(wordDelay);
                    }
                #endif

                string deliveredItemName = deliveredItem.name;
                string deliveredItemNameWithSpace = deliveredItemName.Replace('_', ' ');
                audioPlayback.clip = deliveredItem;
                audioPlayback.Play();
                scriptedEventReporter.ReportScriptedEvent("object presentation begins",
                                                          new Dictionary<string, object>() { {"trial number", continuousTrialNum},
                                                                                             {"item name", deliveredItemName},
                                                                                             {"store name", nextStore.GetStoreName()},
                                                                                             {"serial position", i+1},
                                                                                             {"player position", playerMovement.transform.position.ToString()},
                                                                                             {"store position", nextStore.transform.position.ToString()}});
                
                #if !UNITY_WEBGL // System.IO
                    string lstFilepath = practice
                                ? System.IO.Path.Combine(UnityEPL.GetDataPath(), "practice-" + continuousTrialNum.ToString() + ".lst")
                                : System.IO.Path.Combine(UnityEPL.GetDataPath(), continuousTrialNum.ToString() + ".lst");
                    AppendWordToLst(lstFilepath, deliveredItemName);
                #endif

                thisTrialPresentedStores.Add(nextStore);
                allPresentedObjects.Add(deliveredItemName);

                SetRamulatorState("WORD", true, new Dictionary<string, object>() { { "word", deliveredItemName } });
                //add visuals with sound
                messageImageDisplayer.deliver_item_visual_dislay.SetActive(true);
                messageImageDisplayer.SetDeliverItemText(deliveredItemNameWithSpace);
                yield return SkippableWait(AUDIO_TEXT_DISPLAY);
                messageImageDisplayer.deliver_item_visual_dislay.SetActive(false);

                SetRamulatorState("WORD", false, new Dictionary<string, object>() { { "word", deliveredItemName } });

                scriptedEventReporter.ReportScriptedEvent("audio presentation finished",
                                                          new Dictionary<string, object>());
                playerMovement.Unfreeze();
            }
        }

    SkipRemainingDeliveries:
        messageImageDisplayer.please_find_the_blah_reminder.SetActive(false);

        SetRamulatorState("ENCODING", false, new Dictionary<string, object>());

        if (practice)
            scriptedEventReporter.ReportScriptedEvent("stop practice deliveries");
        else
            scriptedEventReporter.ReportScriptedEvent("stop deliveries");
    }

    private IEnumerator DoPracticeTrials(int numTrials)
    {
        Debug.Log("Practice trials");
        scriptedEventReporter.ReportScriptedEvent("start practice trials");

        starSystem.ResetSession();

        yield return DoRecapInstructions(forceFR: true);

        messageImageDisplayer.SetGeneralMessageText(mainText: "practice invitation");
        yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_message_display);

        for (int trialNumber = 0; trialNumber < numTrials; trialNumber++)
        {
            // ER instructions
            if ((Config.efrEnabled || Config.ecrEnabled) && trialNumber == EFR_PRACTICE_TRIAL_NUM)
            {
                if (Config.twoBtnEfrEnabled || Config.twoBtnEcrEnabled)
                {
                    yield return DoVideo(LanguageSource.GetLanguageString("play movie"),
                                         LanguageSource.GetLanguageString("efr intro video"),
                                         VideoSelector.VideoType.EfrIntro);
                    yield return DoTwoBtnErKeypressCheck();
                    yield return DoTwoBtnErKeypressPractice();
                }
                else // One btn ER
                {
                    if (Config.efrEnabled)
                        messageImageDisplayer.SetGeneralBigMessageText(titleText: "one btn efr instructions title",
                                                                       mainText: "one btn efr instructions main");
                    if (Config.ecrEnabled) // TODO: JPB: Hospital add ecr instructions
                        messageImageDisplayer.SetGeneralBigMessageText(titleText: "one btn ecr instructions title",
                                                                       mainText: "one btn ecr instructions main");
                    yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);
                    yield return DoOneBtnErKeypressCheck();
                    yield return DoOneBtnErKeypressPractice();
                }

                if (HOSPITAL_COURIER) // Skip the second ER practice deliv day (have it be a real deliv day)
                    break;

                messageImageDisplayer.SetGeneralMessageText(titleText: "er check understanding title",
                                                            mainText: "er check understanding main");
                yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_message_display);
            }

            // Next day message (and trial skip button)
            SetRamulatorState("WAITING", true, new Dictionary<string, object>());
            if (!DeliveryItems.ItemsExhausted())
            {
                BlackScreen();
                if (trialNumber > 0)
                {
                    messageImageDisplayer.SetGeneralBigMessageText(mainText: "next practice day");
                    yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);
                }

                // Skip to the next trial
                if (InputManager.GetButton("Secret"))
                {
                    SetRamulatorState("WAITING", false, new Dictionary<string, object>());
                    continue;
                }
            }
            else
            {
                yield return PressAnyKey(LanguageSource.GetLanguageString("final recall"));
                break;
            }
            SetRamulatorState("WAITING", false, new Dictionary<string, object>());

            #if !UNITY_WEBGL // Ramulator
                // Set ramulator trial start                       
                if (useRamulator)                                  
                    ramulatorInterface.BeginNewTrial(trialNumber); 
            #endif                                                 

            // Do deliveries and recall
            if (HOSPITAL_COURIER && trialNumber == 0) // Skip town learning stores in first pratice deliv days
                yield return DoDeliveries(trialNumber, trialNumber, practice: true, skipLastDelivStores: true);
            else
                yield return DoDeliveries(trialNumber, trialNumber, practice: true);
            if (!COURIER_ONLINE)
                yield return DoFixation(PAUSE_BEFORE_RETRIEVAL, practice: true);
            yield return DoRecall(trialNumber, trialNumber, practice: true);

            // Delivery Scores
            if (HOSPITAL_COURIER)
            {
                var mtFormatValues = new string[] { starSystem.NumCorrectInSession(c => c < POINTING_CORRECT_THRESHOLD / Mathf.PI).ToString(),
                                                    starSystem.NumInSession().ToString() };
                messageImageDisplayer.SetGeneralBigMessageText(titleText: "deliv day pointing accuracy title",
                                                               mainText: "deliv day pointing accuracy main",
                                                               mtFormatVals: mtFormatValues);
                yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);
            }
        }

        messageImageDisplayer.SetGeneralMessageText(titleText: "er check understanding title",
                                                    mainText: "er check understanding main");
        yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_message_display);

        scriptedEventReporter.ReportScriptedEvent("stop practice trials");
    }

    private IEnumerator DoTrials(int numTrials, int trialNumOffset = 0)
    {
        Debug.Log("Real trials");
        scriptedEventReporter.ReportScriptedEvent("start trials");
        for (int trialNumber = 0; trialNumber < numTrials; trialNumber++)
        {
            int continuousTrialNum = trialNumber + trialNumOffset;

            #if !UNITY_WEBGL // NICLS
            //Turn off ReadOnlyState
            if (NICLS_COURIER && trialNumber == NUM_CLASSIFIER_NORMALIZATION_TRIALS)
            {
                Debug.Log("READ_ONLY_OFF");
                niclsInterface.SendReadOnlyState(0);
            }
            #endif

            // Next day message (and trial skip button)
            SetRamulatorState("WAITING", true, new Dictionary<string, object>());
            if (!DeliveryItems.ItemsExhausted())
            {
                BlackScreen();
                if (trialNumber > 0)
                {
                    messageImageDisplayer.SetGeneralBigMessageText(mainText: "next day");
                    yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);
                }

                // Skip to the next trial
                if (InputManager.GetButton("Secret"))
                {
                    SetRamulatorState("WAITING", false, new Dictionary<string, object>());
                    continue;
                }
            }
            else
            {
                yield return PressAnyKey(LanguageSource.GetLanguageString("final recall"));
                break;
            }
            SetRamulatorState("WAITING", false, new Dictionary<string, object>());

            #if !UNITY_WEBGL // Ramulator
                // Set ramulator trial start
                if (useRamulator)
                    ramulatorInterface.BeginNewTrial(continuousTrialNum);
            #endif

            // Do deliveries and recall
            yield return DoDeliveries(trialNumber, continuousTrialNum, practice: false);
            if (!COURIER_ONLINE)
                yield return DoFixation(PAUSE_BEFORE_RETRIEVAL, practice: false);
            yield return DoRecall(trialNumber, continuousTrialNum, practice: false);

            // Delivery Scores
            if (HOSPITAL_COURIER)
            {
                var mtFormatValues = new string[] { starSystem.NumCorrectInSession(c => c < POINTING_CORRECT_THRESHOLD / Mathf.PI).ToString(),
                                                    starSystem.NumInSession().ToString() };
                messageImageDisplayer.SetGeneralBigMessageText(titleText: "deliv day pointing accuracy title",
                                                               mainText: "deliv day pointing accuracy main",
                                                               mtFormatVals: mtFormatValues);
                yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);
            }
        }
        scriptedEventReporter.ReportScriptedEvent("stop trials");
    }

    private IEnumerator DoBreak()
    {
        scriptedEventReporter.ReportScriptedEvent("start required break");
        BlackScreen();
        textDisplayer.DisplayText("break prompt", LanguageSource.GetLanguageString("break"));
        while (!InputManager.GetKeyDown(KeyCode.Space))
            yield return null;
        textDisplayer.ClearText();
        scriptedEventReporter.ReportScriptedEvent("stop required break");
    }



    private IEnumerator DoFixation(float time, bool practice = false)
    {
        scriptedEventReporter.ReportScriptedEvent("start fixation");
        BlackScreen();

        if (practice)
            messageImageDisplayer.SetGeneralBiggerMessageText(titleText: "fixation practice message",
                                                           mainText: "fixation item",
                                                           continueText: "");
        else
            messageImageDisplayer.SetGeneralBiggerMessageText(mainText: "fixation item",
                                                           continueText: "");

        yield return messageImageDisplayer.DisplayMessageTimed(messageImageDisplayer.general_bigger_message_display, time);
        scriptedEventReporter.ReportScriptedEvent("stop fixation");
    }

    // LC: no warning field implemented yet
    private IEnumerator DoTypedResponses(int trialNumber, string taskType, float taskLength, GameObject inputObject,
                                         UnityEngine.UI.InputField inputField, string storeName = "")
    {
        float taskStart = Time.time;

        Dictionary<string, object> taskTypeData = new Dictionary<string, object>();
        taskTypeData.Add("trial number", trialNumber);
        // if (!String.IsNullOrEmpty(store_name)) {
        //     taskTypeData.Add("store displayed", store_name);
        // }
        scriptedEventReporter.ReportScriptedEvent("start " + taskType + " typing", taskTypeData);

        // during the duration of the task...
        while (Time.time < taskStart + taskLength)
        {
            yield return null;

            // activate the input text UI
            inputObject.SetActive(true);
            inputField.ActivateInputField();

            if ((Input.anyKeyDown) && (taskType == "cued recall"))
            {
                Debug.Log("Deleting typed response");
                taskStart = Time.time;
            }

            // 3 main cases: free recall, cued recall, value recall
            // free recall will last for the entirety
            // cued recall and value guess will end whenever correct input has been typed
            if (Input.GetKeyDown(KeyCode.Return))
            {
                Debug.Log(inputField.text);
                // if typed response is numeric value...
                int inputFieldAsNum;
                if (int.TryParse(inputField.text, out inputFieldAsNum))
                {
                    if (taskType == "value recall")
                    {
                        // valueGuessWrongType.SetActive(false);

                        // save & report the response
                        Dictionary<string, object> typedData = new Dictionary<string, object>();
                        typedData.Add("trial number", trialNumber);
                        typedData.Add("typed response", inputField.text);
                        scriptedEventReporter.ReportScriptedEvent(taskType, typedData);

                        // clear the field and exit the coroutine
                        inputField.Select();
                        inputField.text = "";
                        inputObject.SetActive(false);
                        yield break;
                    }
                    // tasks other than value guess should have word response
                    else
                    {
                        // freeRecallWrongType.SetActive(true);
                    }
                }
                // if typed response is not numeric value...
                else
                {
                    // value guess should have numeric response
                    if (taskType == "value recall")
                    {
                        // valueGuessWrongType.SetActive(true);
                    }
                    else
                    {
                        // freeRecallWrongType.SetActive(false);

                        // save & report the response
                        Dictionary<string, object> typedData = new Dictionary<string, object>();
                        typedData.Add("trial number", trialNumber);

                        // cued recall should also report the store displayed during the task
                        if (!String.IsNullOrEmpty(storeName))
                        {
                            typedData.Add("store displayed", storeName);
                        }
                        typedData.Add("typed response", inputField.text);
                        scriptedEventReporter.ReportScriptedEvent(taskType, typedData);

                        // reset input text UI
                        inputField.Select();
                        inputField.text = "";

                        // cued recall task should exit the coroutine when the response is reported
                        if (taskType == "cued recall")
                        {
                            inputObject.SetActive(false);
                            yield break;
                        }
                    }
                }
            }
        }

        scriptedEventReporter.ReportScriptedEvent("end" + taskType + " typing", taskTypeData);

        // reset input text UI at the end
        inputField.Select();
        inputField.text = "";
        inputObject.SetActive(false);
        // freeRecallWrongType.SetActive(false);
        // valueGuessWrongType.SetActive(false);
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
        if (COURIER_ONLINE)
        {
            messageImageDisplayer.SetGeneralBigMessageText("free recall title", "free recall main");
            yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);
        }
        
        scriptedEventReporter.ReportScriptedEvent("start free recall");
        BlackScreen();
        textDisplayer.ClearText();

        highBeep.Play();
        scriptedEventReporter.ReportScriptedEvent("sound played", new Dictionary<string, object>() { { "sound name", "high beep" }, { "sound duration", highBeep.clip.length.ToString() } });
        textDisplayer.DisplayText("display recall text", RECALL_TEXT);
        yield return SkippableWait(RECALL_TEXT_DISPLAY_LENGTH);
        textDisplayer.ClearText();

        #if !UNITY_WEBGL // System.IO
            Dictionary<string, object> recordingData = new Dictionary<string, object>();
            recordingData.Add("trial number", continuousTrialNum);
            scriptedEventReporter.ReportScriptedEvent("object recall recording start", recordingData);
            string output_directory = UnityEPL.GetDataPath();
            string wavFilePath = practice
                        ? System.IO.Path.Combine(output_directory, "practice-" + continuousTrialNum.ToString()) + ".wav"
                        : System.IO.Path.Combine(output_directory, continuousTrialNum.ToString()) + ".wav";
            soundRecorder.StartRecording(wavFilePath);

            if (practice && trialNumber == 0)
                yield return DoFreeRecallDisplay("", PRACTICE_FREE_RECALL_LENGTH, practice: true, efrDisabled: true);
            else if (practice)
                yield return DoFreeRecallDisplay("", PRACTICE_FREE_RECALL_LENGTH, practice: true);
            else
                yield return DoFreeRecallDisplay("", FREE_RECALL_LENGTH);

            scriptedEventReporter.ReportScriptedEvent("object recall recording stop", recordingData);
            soundRecorder.StopRecording();
        #else
            // recordingData.Add("trial number", trialNumber);
            // scriptedEventReporter.ReportScriptedEvent("object recall typing start", recordingData);
            yield return DoTypedResponses(trialNumber, "free recall", FREE_RECALL_LENGTH, freeInputField, freeResponse);
            // scriptedEventReporter.ReportScriptedEvent("object recall typing end", recordingData);
        #endif // !UNITY_WEBGL

        textDisplayer.ClearText();
        lowBeep.Play();
        scriptedEventReporter.ReportScriptedEvent("sound played", new Dictionary<string, object>() { { "sound name", "low beep" }, { "sound duration", lowBeep.clip.length.ToString() } });
        BlackScreen();
        scriptedEventReporter.ReportScriptedEvent("stop free recall");
    }

    private IEnumerator DoCuedRecall(int trialNumber, int continuousTrialNum, bool practice = false)
    {
        scriptedEventReporter.ReportScriptedEvent("start cued recall");
        BlackScreen();
        thisTrialPresentedStores.Shuffle(rng);
        Debug.Log(thisTrialPresentedStores);
        
        if (COURIER_ONLINE)
        {
            messageImageDisplayer.SetGeneralBigMessageText(titleText: "cued recall title", mainText: "online cued recall main");
            yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);
        }
        else if (HOSPITAL_COURIER) // TODO: JPB: Merge this with the else statement (need descriptions)
        {
            messageImageDisplayer.SetGeneralBigMessageText(mainText: "store cue recall");
            yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);
        }
        else
        {
            textDisplayer.DisplayText("display day cued recall prompt", LanguageSource.GetLanguageString("store cue recall"));
            yield return SkippableWait(RECALL_MESSAGE_DISPLAY_LENGTH);
            textDisplayer.ClearText();
        }

        highBeep.Play();
        scriptedEventReporter.ReportScriptedEvent("sound played", new Dictionary<string, object>() { { "sound name", "high beep" }, { "sound duration", highBeep.clip.length.ToString() } });
        textDisplayer.DisplayText("display recall text", RECALL_TEXT);
        yield return SkippableWait(RECALL_TEXT_DISPLAY_LENGTH);
        textDisplayer.ClearText();
        foreach (StoreComponent cueStore in thisTrialPresentedStores)
        {   
            #if !UNITY_WEBGL // NICLS
                if (useNiclServer && (trialNumber >= NUM_CLASSIFIER_NORMALIZATION_TRIALS))
                {
                    yield return new WaitForSeconds(WORD_PRESENTATION_DELAY);
                    yield return WaitForClassifier(niclsClassifierTypes[continuousTrialNum]);
                }
                else
                {
                    float wordDelay = UnityEngine.Random.Range(WORD_PRESENTATION_DELAY - WORD_PRESENTATION_JITTER,
                                                               WORD_PRESENTATION_DELAY + WORD_PRESENTATION_JITTER);
                    yield return new WaitForSeconds(wordDelay);
                }
            #endif

            #if !UNITY_WEBGL //  System.IO
                string output_file_name = practice
                            ? "practice-" + continuousTrialNum.ToString() + "-" + cueStore.GetStoreName()
                            : continuousTrialNum.ToString() + "-" + cueStore.GetStoreName();
                string output_directory = UnityEPL.GetDataPath();
                string wavFilePath = System.IO.Path.Combine(output_directory, output_file_name) + ".wav";
                string lstFilepath = System.IO.Path.Combine(output_directory, output_file_name) + ".lst";
                AppendWordToLst(lstFilepath, cueStore.GetLastPoppedItemName());
            #endif

            Dictionary<string, object> cuedRecordingData = new Dictionary<string, object>();
            cuedRecordingData.Add("trial number", COURIER_ONLINE ? trialNumber : continuousTrialNum);
            cuedRecordingData.Add("store", cueStore.GetStoreName());
            cuedRecordingData.Add("item", cueStore.GetLastPoppedItemName());
            cuedRecordingData.Add("store position", cueStore.transform.position.ToString());
            
            #if !UNITY_WEBGL // Microphone
                scriptedEventReporter.ReportScriptedEvent("cued recall recording start", cuedRecordingData);
                soundRecorder.StartRecording(wavFilePath);

                if (practice && trialNumber == 0)
                    yield return DoCuedRecallDisplay(cueStore, "", MAX_CUED_RECALL_TIME_PER_STORE, practice: true, ecrDisabled: true, minWaitTime: MIN_CUED_RECALL_TIME_PER_STORE);
                else if (practice)
                    yield return DoCuedRecallDisplay(cueStore, "", MAX_CUED_RECALL_TIME_PER_STORE, practice: true, minWaitTime: MIN_CUED_RECALL_TIME_PER_STORE);
                else
                    yield return DoCuedRecallDisplay(cueStore, "", MAX_CUED_RECALL_TIME_PER_STORE, minWaitTime: MIN_CUED_RECALL_TIME_PER_STORE);

                scriptedEventReporter.ReportScriptedEvent("cued recall recording stop", cuedRecordingData);
                soundRecorder.StopRecording();
            #else
                scriptedEventReporter.ReportScriptedEvent("cued recall answer start", cuedRecordingData);
                yield return DoTypedResponses(trialNumber, "cued recall", CUED_RECALL_TIME_PER_STORE, cuedInputField, cuedResponse, cueStore.GetStoreName());
                scriptedEventReporter.ReportScriptedEvent("cued recall answer stop", cuedRecordingData);
            #endif
            
            lowBeep.Play();
            scriptedEventReporter.ReportScriptedEvent("sound played", new Dictionary<string, object>() { { "sound name", "low beep" }, { "sound duration", highBeep.clip.length.ToString() } });
            textDisplayer.DisplayText("display recall text", RECALL_TEXT);
            yield return SkippableWait(RECALL_TEXT_DISPLAY_LENGTH);
            textDisplayer.ClearText();
        }
        scriptedEventReporter.ReportScriptedEvent("stop cued recall");
    }

    private IEnumerator DoFinalRecall(int subSessionNum)
    {
        Debug.Log("Final Recalls");
        scriptedEventReporter.ReportScriptedEvent("start final recall");

        #if !UNITY_WEBGL // Microphone and System.IO
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
                yield return DoFreeRecallDisplay("all stores recall", STORE_FINAL_RECALL_LENGTH);

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
            foreach (string deliveredObject in allPresentedObjects)
                AppendWordToLst(lstFilepath, deliveredObject);

            scriptedEventReporter.ReportScriptedEvent("final object recall recording start");
            soundRecorder.StartRecording(wavFilePath);

            textDisplayer.ClearText();
            ClearTitle();
            yield return DoFreeRecallDisplay("all objects recall", OBJECT_FINAL_RECALL_LENGTH);
            scriptedEventReporter.ReportScriptedEvent("final object recall recording stop");
            soundRecorder.StopRecording();

            textDisplayer.ClearText();
            lowBeep.Play();
            scriptedEventReporter.ReportScriptedEvent("sound played", new Dictionary<string, object>() { { "sound name", "low beep" }, { "sound duration", lowBeep.clip.length.ToString() } });

            SetRamulatorState("RETRIEVAL", false, new Dictionary<string, object>());
            scriptedEventReporter.ReportScriptedEvent("stop final recall");
        #else
            messageImageDisplayer.SetGeneralBigMessageText("final store recall title", "final store recall main");
            yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);

            highBeep.Play();
            scriptedEventReporter.ReportScriptedEvent("Sound played", new Dictionary<string, object>() { { "sound name", "high beep" }, { "sound duration", highBeep.clip.length.ToString() } });

            scriptedEventReporter.ReportScriptedEvent("final store recall start", new Dictionary<string, object>());
            placeHolder.text = LanguageSource.GetLanguageString("final store recall text");
            yield return DoTypedResponses(-999, "final store recall", STORE_FINAL_RECALL_LENGTH, freeInputField, freeResponse);

            lowBeep.Play();
            scriptedEventReporter.ReportScriptedEvent("Sound played", new Dictionary<string, object>() { { "sound name", "low beep" }, 
                                                                                                        { "sound duration", lowBeep.clip.length.ToString() } });
            scriptedEventReporter.ReportScriptedEvent("final store recall stop", new Dictionary<string, object>());

            yield return SkippableWait(TIME_BETWEEN_DIFFERENT_RECALL_PHASES);

            messageImageDisplayer.SetGeneralBigMessageText("final object recall title", "final object recall main");
            yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);

            highBeep.Play();
            scriptedEventReporter.ReportScriptedEvent("Sound played", new Dictionary<string, object>() { { "sound name", "high beep" }, { "sound duration", highBeep.clip.length.ToString() } });

            scriptedEventReporter.ReportScriptedEvent("final object recall start", new Dictionary<string, object>());
            placeHolder.text = LanguageSource.GetLanguageString("final object recall text");
            yield return DoTypedResponses(-999, "final object recall", STORE_FINAL_RECALL_LENGTH, freeInputField, freeResponse);

            lowBeep.Play();
            scriptedEventReporter.ReportScriptedEvent("Sound played", new Dictionary<string, object>() { { "sound name", "low beep" }, 
                                                                                                        { "sound duration", lowBeep.clip.length.ToString() } });
            scriptedEventReporter.ReportScriptedEvent("final object recall stop", new Dictionary<string, object>());
        #endif
    }



    private List<List<int>> GenMusicVideoOrder()
    {
        // Setup random video order list that's consistent across each participant's sessions
        // We create the whole list each time to make sure that the rng is consistent but unique per session group
        var videoOrder = new List<List<int>>();
        var reliableRandom = deliveryItems.ReliableRandom();
        int numSessionsPerVideoList = NUM_MUSIC_VIDEOS / NUM_MUSIC_VIDEOS_PER_SESSION;
        foreach (int i in Enumerable.Range(0, (continuousSessionNumber / numSessionsPerVideoList) + 1))
        {
            var shuffledVideos = Enumerable.Range(0, NUM_MUSIC_VIDEOS).ToList().Shuffle(reliableRandom).ToList();
            foreach (int j in Enumerable.Range(0, numSessionsPerVideoList))
                videoOrder.Add(shuffledVideos.GetRange(j * NUM_MUSIC_VIDEOS_PER_SESSION, NUM_MUSIC_VIDEOS_PER_SESSION).ToList());
        }
        Debug.Log(string.Join("\n", videoOrder.Select(x => string.Join(", ", x))));

        return videoOrder;
    }

    private IEnumerator DoMusicVideos(List<List<int>> videoOrder)
    {
        scriptedEventReporter.ReportScriptedEvent("start music videos", new Dictionary<string, object> { { "video numbers", videoOrder[continuousSessionNumber] } });
        BlackScreen();

        // Show Instructions
        messageImageDisplayer.SetGeneralBigMessageText(titleText: "music video instructions title",
                                                       mainText: "music video instructions main");
        yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);

        // Play the music videos
        foreach (int clipNum in Enumerable.Range(0, NUM_MUSIC_VIDEOS_PER_SESSION))
        {
            yield return null;
            BlackScreen();
            yield return DoVideo(LanguageSource.GetLanguageString("play movie"),
                                 LanguageSource.GetLanguageString("music video ending instructions"),
                                 VideoSelector.VideoType.MusicVideos,
                                 videoOrder[continuousSessionNumber][clipNum]);

            // TODO: JPB: (Hokua) Make this dynamic
            var ratings = new string[] { "music video familiarity rating 0", "music video familiarity rating 1", "music video familiarity rating 2", "music video familiarity rating 3", "music video familiarity rating 4", };
            messageImageDisplayer.SetSlidingScaleText(mainText: "music video familiarity title",
                                                      ratings: ratings);
            yield return messageImageDisplayer.DisplaySlidingScaleMessage(messageImageDisplayer.sliding_scale_display);

            ratings = new string[] { "music video engagement rating 0", "music video engagement rating 1", "music video engagement rating 2", "music video engagement rating 3", "music video engagement rating 4", };
            messageImageDisplayer.SetSlidingScaleText(mainText: "music video engagement title",
                                                      ratings: ratings);
            yield return messageImageDisplayer.DisplaySlidingScaleMessage(messageImageDisplayer.sliding_scale_display);
        }
        scriptedEventReporter.ReportScriptedEvent("stop music videos");
    }

    private IEnumerator DoMusicVideoRecall(List<List<int>> videoOrder)
    {
        scriptedEventReporter.ReportScriptedEvent("start music video recall");
        BlackScreen();

        messageImageDisplayer.SetGeneralBigMessageText(titleText: "music video recall instructions title",
                                                       mainText: "music video recall instructions main");
        yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);

        foreach (int clipNum in Enumerable.Range(0, NUM_MUSIC_VIDEOS_PER_SESSION))
        {
            highBeep.Play();
            scriptedEventReporter.ReportScriptedEvent("sound played", new Dictionary<string, object>() { { "sound name", "high beep" }, { "sound duration", highBeep.clip.length.ToString() } });

            var videoIndex = videoOrder[continuousSessionNumber][clipNum];
            yield return messageImageDisplayer.DisplayMessageTimed(messageImageDisplayer.music_video_prompts[videoIndex], MUSIC_VIDEO_PROMPT_TIME);

            string output_directory = UnityEPL.GetDataPath();
            string wavFilePath = System.IO.Path.Combine(output_directory, "music_video_recall_" + videoIndex) + ".wav";
            Dictionary<string, object> recordingData = new Dictionary<string, object>();
            recordingData.Add("video number", videoIndex);
            scriptedEventReporter.ReportScriptedEvent("music video recall recording start", recordingData);
            soundRecorder.StartRecording(wavFilePath);

            yield return DoFreeRecallDisplay("music video " + videoIndex + " recall", MUSIC_VIDEO_RECALL_TIME, efrDisabled: true);

            scriptedEventReporter.ReportScriptedEvent("music video recall recording stop", recordingData);
            soundRecorder.StopRecording();

            lowBeep.Play();
            scriptedEventReporter.ReportScriptedEvent("sound played", new Dictionary<string, object>() { { "sound name", "low beep" }, { "sound duration", lowBeep.clip.length.ToString() } });
        }
        scriptedEventReporter.ReportScriptedEvent("stop music video recall");
    }



    private IEnumerator DoPointingTask(StoreComponent nextStore)
    {
        pointer.SetActive(true);
        ColorPointer(new Color(0.5f, 0.5f, 1f));
        pointer.transform.eulerAngles = new UnityEngine.Vector3(0, rng.Next(360), 0);
        scriptedEventReporter.ReportScriptedEvent("pointing begins", new Dictionary<string, object> { { "start direction", pointer.transform.eulerAngles.y }, { "store", nextStore.GetStoreName() } });
        pointerMessage.SetActive(true);
        pointerText.text = COURIER_ONLINE ?
                           LanguageSource.GetLanguageString("next package prompt") + "<b>" +
                           LanguageSource.GetLanguageString(nextStore.GetStoreName()) + "</b>" + ". " +
                           LanguageSource.GetLanguageString("please point") +
                           LanguageSource.GetLanguageString(nextStore.GetStoreName()) + "." + "\n\n" +
                           LanguageSource.GetLanguageString("keyboard")
                           :
                           LanguageSource.GetLanguageString("next package prompt") + "<b>" +
                           LanguageSource.GetLanguageString(nextStore.GetStoreName()) + "</b>" + ". " +
                           LanguageSource.GetLanguageString("please point") +
                           LanguageSource.GetLanguageString(nextStore.GetStoreName()) + "." + "\n\n" +
                           LanguageSource.GetLanguageString("joystick");
        yield return null;
        while (!InputManager.GetButtonDown("Continue"))
        {
            yield return null;
            if (!playerMovement.IsDoubleFrozen())
                pointer.transform.eulerAngles = pointer.transform.eulerAngles + new UnityEngine.Vector3(0, InputManager.GetAxis("Horizontal") * Time.deltaTime * pointerRotationSpeed, 0);
        }

        float pointerError = PointerError(nextStore.gameObject);
        if (pointerError < POINTING_CORRECT_THRESHOLD)
        {
            pointerParticleSystem.Play();
            pointerText.text = LanguageSource.GetLanguageString("correct pointing");
        }
        else
        {
            pointerText.text = LanguageSource.GetLanguageString("incorrect pointing");
        }

        float wrongness = pointerError / Mathf.PI;
        ColorPointer(new Color(wrongness, 1 - wrongness, .2f));
        bool improvement = starSystem.ReportScore(1 - wrongness);

        if (STAR_SYSTEM_ACTIVE)
            starSystem.gameObject.SetActive(true);
            yield return starSystem.ShowDifference();

        if (improvement)
            pointerText.text = pointerText.text + "\n" + LanguageSource.GetLanguageString("rating improved");

        pointerText.text = pointerText.text + "\n" + LanguageSource.GetLanguageString("continue");

        while (!InputManager.GetButtonDown("Continue"))
            yield return null;
        scriptedEventReporter.ReportScriptedEvent("pointer message cleared");
        pointerParticleSystem.Stop();
        pointer.SetActive(false);
        pointerMessage.SetActive(false);
        starSystem.gameObject.SetActive(false);
    }

    private bool lastPointingIndicatorState = false;
    private IEnumerator DisplayPointingIndicator(StoreComponent nextStore, bool enable = false)
    {
        if (enable) {
            if (lastPointingIndicatorState != enable)
                scriptedEventReporter.ReportScriptedEvent("continuous pointer");
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
            UnityEngine.Vector3 lookDirection = pointToStore.transform.position - pointer.transform.position;
            pointer.transform.rotation = Quaternion.Slerp(pointer.transform.rotation,
                                                          Quaternion.LookRotation(lookDirection),
                                                          rotationSpeed);
        } while (Time.time < startTime + arrowCorrectionTime) ;
    }

    private float PointerError(GameObject toStore)
    {
        UnityEngine.Vector3 lookDirection = toStore.transform.position - pointer.transform.position;
        float correctYRotation = Quaternion.LookRotation(lookDirection).eulerAngles.y;
        float actualYRotation = pointer.transform.eulerAngles.y;
        float offByRads = Mathf.Abs(correctYRotation - actualYRotation) * Mathf.Deg2Rad;
        if (offByRads > Mathf.PI)
            offByRads = Mathf.PI * 2 - offByRads;

        scriptedEventReporter.ReportScriptedEvent("pointing finished", new Dictionary<string, object>() { {"correct direction (degrees)", correctYRotation},
                                                                                                          {"pointed direction (degrees)", actualYRotation} });

        return offByRads;
    }

    private void ColorPointer(Color color)
    {
        foreach (Renderer eachRenderer in pointer.GetComponentsInChildren<Renderer>())
            eachRenderer.material.SetColor("_Color", color);
    }



    private IEnumerator DoFreeRecallDisplay(string title, float waitTime, bool practice = false, bool efrDisabled = false)
    {
        BlackScreen();
        if (Config.efrEnabled && !efrDisabled)
        {
            if (Config.twoBtnEfrEnabled)
            {
                SetTwoBtnErDisplay();
                messageImageDisplayer.SetEfrText(titleText: title);
                messageImageDisplayer.SetEfrElementsActive(speakNowText: true);
                yield return messageImageDisplayer.DisplayMessageTimedLRKeypressBold(
                    messageImageDisplayer.efr_display, waitTime,
                    efrLeftLogMsg, efrRightLogMsg, practice);
            }
            else // One btn EFR
            {
                if (NICLS_COURIER)
                {
                    messageImageDisplayer.SetGeneralBiggerMessageText(titleText: "one btn er message",
                                                                      continueText: "speak now");
                    yield return messageImageDisplayer.DisplayMessageTimed(
                        messageImageDisplayer.general_bigger_message_display, waitTime);
                }
                else
                {
                    messageImageDisplayer.SetGeneralBiggerMessageText(titleText: "one btn er message",
                                                                      continueText: "speak now");
                    yield return messageImageDisplayer.DisplayMessageTimedKeypressBold(
                        messageImageDisplayer.general_bigger_message_display, waitTime, ActionButton.RejectButton, "title text", "reject button");
                }
            }
        }
        else
        {
            messageImageDisplayer.SetGeneralBiggerMessageText(continueText: "speak now");
            yield return messageImageDisplayer.DisplayMessageTimed(
                messageImageDisplayer.general_bigger_message_display, waitTime);
        }
    }

    private IEnumerator DoCuedRecallDisplay(StoreComponent store, string title, float waitTime, bool practice = false, bool ecrDisabled = false, float minWaitTime = 0f)
    {
        BlackScreen();

        if (Config.ecrEnabled && !ecrDisabled) 
        {
            if (Config.twoBtnEcrEnabled)
            {
                throw new NotImplementedException("Two button ECR not implemented");

                // TODO: JPB: Hospital add two btn ECR
                //SetTwoBtnErDisplay();
                //messageImageDisplayer.SetEfrText(titleText: title);
                //messageImageDisplayer.SetEfrElementsActive(speakNowText: true);
                //yield return messageImageDisplayer.DisplayMessageTimedLRKeypressBold(
                //        messageImageDisplayer.efr_display, waitTime,
                //        efrLeftLogMsg, efrRightLogMsg, practice);
            }
            else // One btn ECR
            {
                messageImageDisplayer.SetCuedRecallMessage("one btn er message");
                Func<IEnumerator> func = () => { return messageImageDisplayer.DisplayMessageTimedKeypressBold(
                    messageImageDisplayer.cued_recall_message, waitTime, ActionButton.RejectButton, "continue text", "reject button"); };
                yield return messageImageDisplayer.DisplayMessageFunction(store.familiarization_object, func);
            }
        }
        else
        {
            if (NICLS_COURIER)
            {
                messageImageDisplayer.SetCuedRecallMessage("cued recall message");
                Func<IEnumerator> func = () => { return messageImageDisplayer.DisplayMessageTimedKeypressBold(
                    messageImageDisplayer.cued_recall_message, waitTime, ActionButton.ContinueButton, "continue text", "continue button", true, minWaitTime); };
                yield return messageImageDisplayer.DisplayMessageFunction(store.familiarization_object, func);
            }
            else
            {
                yield return messageImageDisplayer.DisplayMessageTimed(store.familiarization_object, waitTime);
            }
        }

        yield return null;
    }



    private IEnumerator DoTwoBtnErKeypressCheck()
    {
        if (InputManager.GetButton("Secret"))
            yield break;

        scriptedEventReporter.ReportScriptedEvent("start efr keypress check");
        BlackScreen();

        // Display intro message
        messageImageDisplayer.SetGeneralMessageText(mainText: "er check main");
        yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_message_display);

        // Setup EFR display
        SetTwoBtnErDisplay();

        // Ask for right button press
        messageImageDisplayer.SetEfrText(descriptiveText: "two btn er check description right button");
        messageImageDisplayer.SetEfrElementsActive(descriptiveText: true, controllerRightButtonImage: true);
        yield return messageImageDisplayer.DisplayMessageLRKeypressBold(
           messageImageDisplayer.efr_display, ActionButton.RightButton);
        yield return messageImageDisplayer.DisplayMessageTimedLRKeypressBold(
            messageImageDisplayer.efr_display, 1f, efrLeftLogMsg, efrRightLogMsg);

        // Ask for left button press
        messageImageDisplayer.SetEfrText(descriptiveText: "two btn er check description left button");
        messageImageDisplayer.SetEfrElementsActive(descriptiveText: true, controllerLeftButtonImage: true);
        yield return messageImageDisplayer.DisplayMessageLRKeypressBold(
            messageImageDisplayer.efr_display, ActionButton.LeftButton);
        yield return messageImageDisplayer.DisplayMessageTimedLRKeypressBold(
            messageImageDisplayer.efr_display, 1f, efrLeftLogMsg, efrRightLogMsg);

        scriptedEventReporter.ReportScriptedEvent("stop efr keypress check");
    }

    private IEnumerator DoTwoBtnErKeypressPractice()
    {
        if (InputManager.GetButton("Secret"))
            yield break;

        scriptedEventReporter.ReportScriptedEvent("start efr keypress practice");
        BlackScreen();

        // Display intro message
        messageImageDisplayer.SetGeneralBigMessageText(titleText: "two btn er keypress practice main", 
                                                       mainText: "two btn er keypress practice description");
        yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);

        // Setup EFR display
        messageImageDisplayer.SetEfrElementsActive();

        // Show equal number of left and right keypress practices in random order
        List<ActionButton> lrButtonIndicator = Enumerable.Repeat(ActionButton.LeftButton, EFR_KEYPRESS_PRACTICES)
                                                      .Concat(Enumerable.Repeat(ActionButton.RightButton, EFR_KEYPRESS_PRACTICES))
                                                      .ToList();
        lrButtonIndicator.Shuffle(rng);

        foreach (var buttonIndicator in lrButtonIndicator)
        {
            SetTwoBtnErDisplay();
            float efrKeypressPracticedelay = UnityEngine.Random.Range(EFR_KEYPRESS_PRACTICE_DELAY - EFR_KEYPRESS_PRACTICE_JITTER,
                                                                      EFR_KEYPRESS_PRACTICE_DELAY + EFR_KEYPRESS_PRACTICE_JITTER);
            yield return messageImageDisplayer.DisplayMessageTimed(
                messageImageDisplayer.efr_display, efrKeypressPracticedelay);

            if (buttonIndicator == ActionButton.LeftButton)
            {
                SetTwoBtnErDisplay(ActionButton.LeftButton);
                messageImageDisplayer.SetEfrTextResize(LeftButtonSize: 0.3f);
                yield return messageImageDisplayer.DisplayMessageLRKeypressBold(
                    messageImageDisplayer.efr_display, ActionButton.LeftButton);
                messageImageDisplayer.SetEfrTextResize(LeftButtonSize: -0.3f);
            }
            else if (buttonIndicator == ActionButton.RightButton)
            {
                SetTwoBtnErDisplay(ActionButton.RightButton);
                messageImageDisplayer.SetEfrTextResize(rightButtonSize: 0.3f);
                yield return messageImageDisplayer.DisplayMessageLRKeypressBold(
                    messageImageDisplayer.efr_display, ActionButton.RightButton);
                messageImageDisplayer.SetEfrTextResize(rightButtonSize: -0.3f);
            }
        }

        scriptedEventReporter.ReportScriptedEvent("stop efr keypress practice");
    }

    private IEnumerator DoOneBtnErKeypressCheck()
    {
        if (Config.skipNewEfrKeypressCheck || InputManager.GetButton("Secret"))
            yield break;

        scriptedEventReporter.ReportScriptedEvent("start efr keypress check");
        BlackScreen();

        // Display intro message
        messageImageDisplayer.SetGeneralMessageText(mainText: "er check main");
        yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_message_display);

        // Ask for reject button press
        messageImageDisplayer.SetGeneralBiggerMessageText(titleText: "one btn er message",
                                                          continueText: "");
        yield return messageImageDisplayer.DisplayMessage(
            messageImageDisplayer.general_bigger_message_display, "EfrReject");

        scriptedEventReporter.ReportScriptedEvent("stop efr keypress check");
    }

    private IEnumerator DoOneBtnErKeypressPractice()
    {
        if (Config.skipNewEfrKeypressPractice || InputManager.GetButton("Secret"))
            yield break;

        scriptedEventReporter.ReportScriptedEvent("start efr keypress practice");
        BlackScreen();

        // Display intro message
        messageImageDisplayer.SetGeneralBigMessageText(titleText: "one btn er keypress practice main",
                                                       mainText: "one btn er keypress practice description");
        yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);

        // Ask for reject button press
        messageImageDisplayer.SetGeneralBiggerMessageText(titleText: "one btn er message",
                                                          continueText: "");
        for (int i = 0; i < Config.newEfrKeypressPractices; i++)
            yield return messageImageDisplayer.DisplayMessage(
                messageImageDisplayer.general_bigger_message_display, "EfrReject");

        scriptedEventReporter.ReportScriptedEvent("stop efr keypress practice");
    }

    private void SetTwoBtnErDisplay(ActionButton? keypressPractice = null)
    {
        if (efrCorrectButtonSide == ActionButton.RightButton)
        {
            efrLeftLogMsg = "incorrect";
            efrRightLogMsg = "correct";
            if (keypressPractice == ActionButton.LeftButton)
                messageImageDisplayer.SetEfrText(leftButton: "two btn er keypress practice left button incorrect message",
                                                 rightButton: "two btn er right button correct message");
            else if (keypressPractice == ActionButton.RightButton)
                messageImageDisplayer.SetEfrText(leftButton: "two btn er left button incorrect message",
                                                 rightButton: "two btn efr keypress practice right button correct message");
            else
                messageImageDisplayer.SetEfrText(leftButton: "two btn er left button incorrect message",
                                                 rightButton: "two btn efr right button correct message");
        }
        else if (efrCorrectButtonSide == ActionButton.LeftButton)
        {
            efrLeftLogMsg = "correct";
            efrRightLogMsg = "incorrect";
            if (keypressPractice == ActionButton.LeftButton)
                messageImageDisplayer.SetEfrText(leftButton: "two btn er keypress practice left button correct message",
                                                 rightButton: "two btn er right button incorrect message");
            if (keypressPractice == ActionButton.RightButton)
                messageImageDisplayer.SetEfrText(leftButton: "two btn er left button correct message",
                                                 rightButton: "two btn er keypress practice right button incorrect message");
            else
                messageImageDisplayer.SetEfrText(leftButton: "two btn er left button correct message",
                                                 rightButton: "two btn er right button incorrect message");
        }
    }



    private void SetupNiclsClassifier()
    {
        // Setup which classifiers run
        List<NiclsClassifierType> subList = Enumerable.Repeat(NiclsClassifierType.Pos, 3)
                                                .Concat(Enumerable.Repeat(NiclsClassifierType.Neg, 3))
                                                .Concat(Enumerable.Repeat(NiclsClassifierType.Sham, 2))
                                                .ToList();
        subList.Shuffle(rng);

        // 0th and 5th indeces aren't used (ReadOnly trial)
        niclsClassifierTypes = (new List<NiclsClassifierType> { NiclsClassifierType.Pos })
            .Concat(subList.GetRange(0, 4))
            .Concat(new List<NiclsClassifierType> { NiclsClassifierType.Pos })
            .Concat(subList.GetRange(4, 4))
            .ToList();

        Debug.Log(string.Join(", ",
            niclsClassifierTypes.Select(x => Enum.GetName(typeof(NiclsClassifierType), x))));
    }

    private IEnumerator WaitForClassifier(NiclsClassifierType niclsClassifierType)
    {
        #if !UNITY_WEBGL // NICLS
            scriptedEventReporter.ReportScriptedEvent("start classifier wait");
            Debug.Log(Enum.GetName(typeof(NiclsClassifierType), niclsClassifierType));
            WaitUntilWithTimeout waitForClassifier = null;
            var classifierWaitInfo = new Dictionary<string, object> { { "type", niclsClassifierType.ToString() }, { "timed out", 0 } };
            switch (niclsClassifierType)
            {
                case NiclsClassifierType.Pos:
                    waitForClassifier = new WaitUntilWithTimeout(niclsInterface.classifierInPosState, 5);
                    yield return waitForClassifier;
                    classifierWaitInfo["timed out"] = waitForClassifier.timedOut() ? 1 : 0;
                    break;
                case NiclsClassifierType.Neg:
                    waitForClassifier = new WaitUntilWithTimeout(niclsInterface.classifierInNegState, 5);
                    yield return waitForClassifier;
                    classifierWaitInfo["timed out"] = waitForClassifier.timedOut() ? 1 : 0;
                    break;
                case NiclsClassifierType.Sham:
                    yield return new WaitForSeconds((float)rng.NextDouble() * 5f);
                    classifierWaitInfo["timed out"] = 0;
                    break;
            }
            scriptedEventReporter.ReportScriptedEvent("stop classifier wait", classifierWaitInfo);
            Debug.Log("CLASSIFIER SAID TO GO ---------------------------------------------------------");
        #else
            yield return null;
        #endif // !UNITY_WEBGL
    }
    
    //WAITING, INSTRUCT, COUNTDOWN, ENCODING, WORD, DISTRACT, RETRIEVAL
    protected override void SetRamulatorState(string stateName, bool state, Dictionary<string, object> extraData)
    {
        #if !UNITY_WEBGL // Ramulator
            if (OnStateChange != null)
                OnStateChange(stateName, state);

            if (useRamulator)
                ramulatorInterface.SetState(stateName, state, extraData);
        #endif // !UNITY_WEBG
    }



    private void LogVersions(string expName)
    {
        Dictionary<string, object> versionsData = new Dictionary<string, object>();
        versionsData.Add("UnityEPL version", Application.version);
        versionsData.Add("Experiment version", expName + COURIER_VERSION);
        versionsData.Add("Logfile version", "2.0.0");
        scriptedEventReporter.ReportScriptedEvent("versions", versionsData);
    }

    private IEnumerator EnableEnvironment()
    {
        yield return new WaitUntil(() => deliveryItems.StoresSetup());

        environment = environments[0]; // Remnant of old design
        environment.parent.SetActive(true);

        // Log the store mappings
        Dictionary<string, object> storeMappings = new Dictionary<string, object>();
        foreach (StoreComponent store in environment.stores)
        {
            storeMappings.Add(store.gameObject.name, store.GetStoreName());
            storeMappings.Add(store.GetStoreName() + " position X", store.transform.position.x);
            storeMappings.Add(store.GetStoreName() + " position Y", store.transform.position.y);
            storeMappings.Add(store.GetStoreName() + " position Z", store.transform.position.z);
        }
        scriptedEventReporter.ReportScriptedEvent("store mappings", storeMappings);
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

    private void WorldScreen()
    {
        pauser.AllowPausing();
        regularCamera.enabled = true;
        blackScreenCamera.enabled = false;
        // TODO: JPB: Hospital decide
        //if (HOSPITAL_COURIER && STAR_SYSTEM_ACTIVE)
        //    starSystem.gameObject.SetActive(true);
        memoryWordCanvas.SetActive(false);
        playerMovement.Zero();
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



    private void AppendWordToLst(string lstFilePath, string word)
    {
        #if !UNITY_WEBGL // System.IO
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
        #endif
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
    public static IList<T> Shuffle<T>(this IList<T> list, System.Random rng)
    {
        var count = list.Count;
        for (int i = 0; i < count; ++i)
        {
            int r = rng.Next(i, count);
            T tmp = list[i];
            list[i] = list[r];
            list[r] = tmp;
        }
        return list;
    }
}

