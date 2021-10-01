using System;
using System.Collections;
using System.Collections.Generic;
#if UNITY_STANDALONE
    using System.IO;
#endif
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

    [DllImport("__Internal")]
    #if UNITY_WEBGL
        private static extern void EndTask();
    #endif

    public delegate void StateChange(string stateName, bool on);
    public static StateChange OnStateChange;

    private static int sessionNumber = -1;
    private static string expName;

    // JPB: TODO: Make these configuration variables
    private const bool NICLS_COURIER = false;

    ///////////////////////////////////////////////////////////////////////////
    // LC: Courier Online variables
    private const bool COURIER_ONLINE = true;
    private const bool GRANT_VERSION = false;
    private const float TEST_LENGTH = 20f;
    // private StorePointType storePointType = StorePointType.Random;
    // private int free_index = 0;
    // private int value_index = 0;
    private int number_input;
    ///////////////////////////////////////////////////////////////////////////
    

    private const string COURIER_VERSION = COURIER_ONLINE ? "v5.0.0online" : "v5.0.13";
    private const string RECALL_TEXT = "*******"; // JPB: TODO: Remove this and use display system
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
    private const int EFR_KEYPRESS_PRACTICES = 10;
    private const float MIN_FAMILIARIZATION_ISI = 0.4f;
    private const float MAX_FAMILIARIZATION_ISI = 0.6f;
    private const float FAMILIARIZATION_PRESENTATION_LENGTH = 1.5f;
    private const float RECALL_MESSAGE_DISPLAY_LENGTH = 6f;
    private const float RECALL_TEXT_DISPLAY_LENGTH = 1f;
    private const float FREE_RECALL_LENGTH = 90f;
    private const float PRACTICE_FREE_RECALL_LENGTH = 25f;
    private const float STORE_FINAL_RECALL_LENGTH = 90f;
    private const float FINAL_RECALL_LENGTH = NICLS_COURIER ? 120f : COURIER_ONLINE ? 240f : 180f;
    private const float TIME_BETWEEN_DIFFERENT_RECALL_PHASES = 2f;
    private const float CUED_RECALL_TIME_PER_STORE = 10f;
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

    private static bool useNiclServer;
    #if UNITY_STANDALONE
        private static bool useRamulator;
        public RamulatorInterface ramulatorInterface;
        private Syncbox syncs;
        //public NiclsInterface niclsInterface;
        public NiclsInterface3 niclsInterface;
    #endif
    

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
    public Environment[] testEnvironments;

    System.Random rng = new System.Random();

    private EfrButton efrCorrectButtonSide = EfrButton.RightButton;
    private string efrLeftLogMsg = "incorrect";
    private string efrRightLogMsg = "correct";

    private List<StoreComponent> this_trial_presented_stores = new List<StoreComponent>();
    private List<string> all_presented_objects = new List<string>();

    List<NiclsClassifierType> niclsClassifierTypes = null;


    public GameObject freeInputField;
    public UnityEngine.UI.InputField freeResponse;
    public UnityEngine.UI.Text placeHolder;
    public GameObject cuedInputField;
    public UnityEngine.UI.InputField cuedResponse;


    // LC: Frame testing variables
    // public GameObject fpsDisplay;
    // public UnityEngine.UI.Text fpsDisplayText;
    private static bool IS_FRAME_TESTING = false;
    private static int fpsValue = 0;
    private static float updateRateSeconds = 4.0f;
    private static int frameCount = 0;
    private float dt = 0.0f;
    private float fps = 0.0f;
    private List<int> fpsList = new List<int>();

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
        #if UNITY_STANDALONE
            useRamulator = newUseRamulator;
            useNiclServer = newUseNiclServer;
        #endif
        Config.experimentConfigName = expName;
        sessionNumber = newSessionNumber;
        expName = newExpName;
    }

    void Update()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        

        // LC: Courier Online Frame testing
        if (COURIER_ONLINE && IS_FRAME_TESTING) {
            frameCount++;
            dt += Time.unscaledDeltaTime;
            if (dt > 1.0 / updateRateSeconds)
            {
                fps = frameCount / dt;
                frameCount = 0;
                dt -= 1.0f / updateRateSeconds;
            }
            fpsValue = (int)Math.Round(fps);
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
        
        ConfigureExperiment(false, false, 0, "CourierOnline");

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        Application.targetFrameRate = 300;
        Cursor.SetCursor(new Texture2D(0, 0), new Vector2(0, 0), CursorMode.ForceSoftware);
        QualitySettings.vSyncCount = 1;

        //////////////////////////////////////////////////////////////////////////////////////////////////////////
        #if UNITY_STANDALONE                                                                                    // Syncbox 
            // Start syncpulses                                                                                 //
            if (!Config.noSyncbox)                                                                              //
            {                                                                                                   //
                syncs = GameObject.Find("SyncBox").GetComponent<Syncbox>();                                     //
                syncs.StartPulse();                                                                             //
            }                                                                                                   //
                                                                                                                //
            // Randomize efr correct/incorrect button sides                                                     //
            if (Config.efrEnabled && Config.counterBalanceCorrectIncorrectButton)                               //
            {                                                                                                   //
                // We want randomness for different people, but consistency between sessions                    //
                System.Random reliableRandom = new System.Random(UnityEPL.GetParticipants()[0].GetHashCode());  //
                efrCorrectButtonSide = (EfrButton)reliableRandom.Next(0, 2);                                    //
            }                                                                                                   //
        //////////////////////////////////////////////////////////////////////////////////////////////////////////
        #else
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
        if (sessionNumber == -1)
            throw new UnityException("Please call ConfigureExperiment before beginning the experiment.");

        Debug.Log(UnityEPL.GetDataPath());

        //write versions to logfile
        LogVersions(expName);
        
        //////////////////////////////////////////////////////////////////////////////////////////////////////////
        #if UNITY_STANDALONE                                                                                    // NICLS
            if (useRamulator)                                                                                   //
                yield return ramulatorInterface.BeginNewSession(sessionNumber);                                 //
                                                                                                                //
            if (useNiclServer)                                                                                  //
            {                                                                                                   //
                yield return niclsInterface.BeginNewSession(sessionNumber);                                     //
                SetupNiclsClassifier();                                                                         // 
            }                                                                                                   //
            else                                                                                                //
            {                                                                                                   //
                yield return niclsInterface.BeginNewSession(sessionNumber, true);                               //
            }                                                                                                   //
        #endif                                                                                                  //
        //////////////////////////////////////////////////////////////////////////////////////////////////////////

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


        if (COURIER_ONLINE) 
        {
            EndTask();
        }
        else {
            while (true)
                yield return null;
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

        foreach (var classType in niclsClassifierTypes)
            Debug.Log(Enum.GetName(typeof(NiclsClassifierType), classType));
    }

    private void TestScreen()
    {
        pauser.AllowPausing();
        regularCamera.enabled = true;
        blackScreenCamera.enabled = false;
        starSystem.gameObject.SetActive(false);
        memoryWordCanvas.SetActive(false);
        playerMovement.Zero();
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


    private IEnumerator DoFrameTest()
    {   
        if (Config.skipFPS)
            yield break;

        messageImageDisplayer.SetGeneralBigMessageText(titleText: "frame test start title", mainText: "frame test start main");
        yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);

        Environment testEnvironment = EnableEnvironment();
        TestScreen();
        pointer.SetActive(false);
        IS_FRAME_TESTING = true;
        messageImageDisplayer.fpsDisplay.SetActive(true);
        
        yield return new WaitForSeconds(TEST_LENGTH);
        
        IS_FRAME_TESTING = false;

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

    // LC: no warning field implemented yet
    private IEnumerator ReportTypedResponses(int trial_number, string task_type, float task_length, 
                                             GameObject input_object, UnityEngine.UI.InputField input_field, string store_name="")
    {
        float taskStart = Time.time;

        Dictionary<string, object> taskTypeData = new Dictionary<string, object>();
        taskTypeData.Add("trial number", trial_number);
        // if (!String.IsNullOrEmpty(store_name)) {
        //     taskTypeData.Add("store displayed", store_name);
        // }
        scriptedEventReporter.ReportScriptedEvent("start " + task_type + " typing", taskTypeData);

        // during the duration of the task...
        while (Time.time < taskStart + task_length) {

            // activate the input text UI
            input_object.SetActive(true);
            input_field.ActivateInputField();

            if ((Input.anyKeyDown) && (task_type == "cued recall")) {
                Debug.Log("Deleting typed response");
                taskStart = Time.time;
            }

            // 3 main cases: free recall, cued recall, value recall
            // free recall will last for the entirety
            // cued recall and value guess will end whenever correct input has been typed

            if (Input.GetKeyDown(KeyCode.Return)) {
                Debug.Log(input_field.text);
                // if typed response is numeric value...
                if (int.TryParse(input_field.text, out number_input)) {
                    
                    if (task_type == "value recall") {
                        // valueGuessWrongType.SetActive(false);

                        // save & report the response
                        Dictionary<string, object> typedData = new Dictionary<string, object>();
                        typedData.Add("trial number", trial_number);
                        typedData.Add("typed response", input_field.text);
                        scriptedEventReporter.ReportScriptedEvent(task_type, typedData);

                        // clear the field and exit the coroutine
                        input_field.Select();
                        input_field.text = "";
                        input_object.SetActive(false);
                        yield break;
                    }
                    // tasks other than value guess should have word response
                    else {
                        // freeRecallWrongType.SetActive(true);
                    }
                }
                // if typed response is not numeric value...
                else {
                    // value guess should have numeric response
                    if (task_type == "value recall") {
                        // valueGuessWrongType.SetActive(true);
                    }
                    else {
                        // freeRecallWrongType.SetActive(false);

                        // save & report the response
                        Dictionary<string, object> typedData = new Dictionary<string, object>();
                        typedData.Add("trial number", trial_number);

                        // cued recall should also report the store displayed during the task
                        if (!String.IsNullOrEmpty(store_name)) {
                                typedData.Add("store displayed", store_name);
                        }
                        typedData.Add("typed response", input_field.text);
                        scriptedEventReporter.ReportScriptedEvent(task_type, typedData);

                        // reset input text UI
                        input_field.Select();
                        input_field.text = "";

                        // cued recall task should exit the coroutine when the response is reported
                        if (task_type == "cued recall") {
                            input_object.SetActive(false);
                            yield break;
                        }
                    }
                }
            }
            // otherwise, keep running
            yield return null;
        }

        scriptedEventReporter.ReportScriptedEvent("end" + task_type + " typing", taskTypeData);

        // reset input text UI at the end
        input_field.Select();
        input_field.text = "";
        input_object.SetActive(false);
        // freeRecallWrongType.SetActive(false);
        // valueGuessWrongType.SetActive(false);
    }

    private IEnumerator DoSubSession(int subSessionNum)
    {
        BlackScreen();

        if (subSessionNum == 0)
            if (COURIER_ONLINE)
            {
                yield return DoFrameTest();
            }
            yield return DoIntros();

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

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #if UNITY_STANDALONE                                                                                           // NICLS 
        if (NICLS_COURIER)                                                                                             //
        {                                                                                                              //
            Debug.Log("Town Learning Phase");                                                                          //
            niclsInterface.SendReadOnlyStateToNicls(1);                                                                //
                                                                                                                       //
            if (subSessionNum == 0                                                                                     //
                && sessionNumber < SINGLE_TOWN_LEARNING_SESSIONS + DOUBLE_TOWN_LEARNING_SESSIONS)                      //
            {                                                                                                          //
                trialsPerSession = Config.trialsPerSessionSingleTownLearning;                                          //
                messageImageDisplayer.SetGeneralMessageText("town learning title", "town learning main 1");            //
                yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_message_display);      //
                WorldScreen();                                                                                         //
                yield return DoTownLearning(environment, 0, environment.stores.Length);                                //
                                                                                                                       //
                if (sessionNumber < DOUBLE_TOWN_LEARNING_SESSIONS && !useNiclServer)                                   //
                {                                                                                                      //
                    trialsPerSession = Config.trialsPerSessionDoubleTownLearning;                                      //
                    messageImageDisplayer.SetGeneralMessageText("town learning title", "town learning main 2");        //
                    yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_message_display);  //
                    WorldScreen();                                                                                     //
                    yield return DoTownLearning(environment, 1, environment.stores.Length);                            //
                }                                                                                                      //
            }                                                                                                          //
        }                                                                                                              //
        #endif                                                                                                         //
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        BlackScreen();
        yield return messageImageDisplayer.DisplayLanguageMessage(messageImageDisplayer.delivery_restart_messages);


        if (sessionNumber == 0 && subSessionNum == 0 && !useNiclServer && !COURIER_ONLINE) // JPB: TODO: Nick fix      
        {                                                                                                              
            Debug.Log("Practice trials");                                                                              
            messageImageDisplayer.SetGeneralMessageText(mainText: "practice invitation");                              
            yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_message_display);          
            yield return DoTrials(environment, 2, subSessionNum, true);                                                
                                                                                                                       
            messageImageDisplayer.SetGeneralMessageText(titleText: "new efr check understanding title",                
                                                        mainText: "new efr check understanding main");                 
            yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_message_display);          
        }                                                                                                              
        

        if (sessionNumber == 0)
        {
            messageImageDisplayer.SetGeneralMessageText(titleText: "navigation note title",
                                                        mainText: "navigation note main");
            yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_message_display);
        }

        Debug.Log("Real trials");
        if (Config.efrEnabled)                                                                                          
            if (Config.newEfrEnabled)                                                                                   
                messageImageDisplayer.SetGeneralMessageText(mainText: "first day main",                                 
                                                            descriptiveText: "new efr first day description");          
            else                                                                                                        
                messageImageDisplayer.SetGeneralMessageText(mainText: "first day main",                                 
                                                            descriptiveText: "efr first day description");              
        else                                                                                                            
            messageImageDisplayer.SetGeneralMessageText(mainText: "first day main");                                    
                                                                                                                        
        yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_message_display);               
                                                                                                                        
        int priorTrialsPerSession = 0;                                                                                  
                                                                                                                        
        if (NICLS_COURIER && subSessionNum > 0) // JPB: TODO: Nick fix                                                  
        {                                                                                                               
            if (sessionNumber < DOUBLE_TOWN_LEARNING_SESSIONS && !useNiclServer)                                        
                priorTrialsPerSession = Config.trialsPerSessionDoubleTownLearning;                                      
            else if (sessionNumber < SINGLE_TOWN_LEARNING_SESSIONS)                                                     
                priorTrialsPerSession = Config.trialsPerSessionSingleTownLearning;                                      
        }                                                                                                               
        

        yield return DoTrials(environment, trialsPerSession, subSessionNum,
                              trialNumOffset: subSessionNum * priorTrialsPerSession); // JPB: TODO: Fix this to work for more than two sub-sessions

        Debug.Log("Final Recalls");
        BlackScreen();
        if (NICLS_COURIER)
            yield return messageImageDisplayer.DisplayLanguageMessage(messageImageDisplayer.nicls_final_recall_messages);
        else
            yield return messageImageDisplayer.DisplayLanguageMessage(messageImageDisplayer.final_recall_messages);
        yield return DoFinalRecall(environment, subSessionNum);
    }

    private IEnumerator DoIntros()
    {   
        if (Config.skipIntros)                                                                                          
            yield break;                                                                                                

        BlackScreen();

        if (COURIER_ONLINE)
        {
            yield return DoVideo(LanguageSource.GetLanguageString("play movie"),
                                        LanguageSource.GetLanguageString("standard intro video"),
                                        VideoSelector.VideoType.NiclsMainIntro);
        }
        else 
        {
            if (NICLS_COURIER && sessionNumber == 0 && !useNiclServer)
            {
                yield return DoVideo(LanguageSource.GetLanguageString("play movie"),
                                        LanguageSource.GetLanguageString("standard intro video"),
                                        VideoSelector.VideoType.NiclsMainIntro);
            }
            
            else if (NICLS_COURIER) // sessionNumber >= 1 || useNiclServer                                              
            {                                                                                                           
                var messages = Config.newEfrEnabled                                                                     
                    ? messageImageDisplayer.recap_instruction_messages_new_en                                           
                    : messageImageDisplayer.recap_instruction_messages_en;                                              
                                                                                                                        
                foreach (var message in messages)                                                                       
                    yield return messageImageDisplayer.DisplayMessage(message);                                         
            }                                                                                                           
            else
            {
                yield return DoVideo(LanguageSource.GetLanguageString("play movie"),
                                        LanguageSource.GetLanguageString("standard intro video"),
                                        VideoSelector.VideoType.MainIntro);
            }

            if (!NICLS_COURIER)
                yield return DoSubjectSessionQuitPrompt(sessionNumber,
                                                        LanguageSource.GetLanguageString("running participant"));
            
            /////////////////////////////////////////////////////////////////////////////////////////////////////////////
            #if UNITY_STANDALONE                                                                                       // Microphone
            yield return DoMicrophoneTest(LanguageSource.GetLanguageString("microphone test"),                         //
                                            LanguageSource.GetLanguageString("after the beep"),                        //
                                            LanguageSource.GetLanguageString("recording"),                             //
                                            LanguageSource.GetLanguageString("playing"),                               //
                                            LanguageSource.GetLanguageString("recording confirmation"));               //
            #endif                                                                                                     //
            /////////////////////////////////////////////////////////////////////////////////////////////////////////////

            if (!NICLS_COURIER)
                yield return DoFamiliarization();
        }
    }

    private IEnumerator DoFixation(float time, bool practice = false)
    {
        scriptedEventReporter.ReportScriptedEvent("start fixation", new Dictionary<string, object>());
        BlackScreen();

        if (practice)
            messageImageDisplayer.SetGeneralBiggerMessageText(titleText: "fixation practice message",
                                                           mainText: "fixation item",
                                                           continueText: "");
        else
            messageImageDisplayer.SetGeneralBiggerMessageText(mainText: "fixation item",
                                                           continueText: "");

        yield return messageImageDisplayer.DisplayMessageTimed(messageImageDisplayer.general_bigger_message_display, time);
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

        Dictionary<string, object> recordingData = new Dictionary<string, object>();

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #if UNITY_STANDALONE                                                                                              // System.IO
            recordingData.Add("trial number", continuousTrialNum);                                                        //
            scriptedEventReporter.ReportScriptedEvent("object recall recording start", recordingData);                    //
            string output_directory = UnityEPL.GetDataPath();                                                             //
            string wavFilePath = practice                                                                                 //
                        ? System.IO.Path.Combine(output_directory, "practice-" + continuousTrialNum.ToString()) + ".wav"  //
                        : System.IO.Path.Combine(output_directory, continuousTrialNum.ToString()) + ".wav";               //
            soundRecorder.StartRecording(wavFilePath);                                                                    //
                                                                                                                          //
            if (practice && trialNumber == 0)                                                                             //
                yield return DoFreeRecallDisplay("", PRACTICE_FREE_RECALL_LENGTH, practice: true, efrDisabled: true);     //
            else if (practice)                                                                                            //
                yield return DoFreeRecallDisplay("", PRACTICE_FREE_RECALL_LENGTH, practice: true);                        //
            else                                                                                                          //
                yield return DoFreeRecallDisplay("", FREE_RECALL_LENGTH);                                                 //
                                                                                                                          //
            scriptedEventReporter.ReportScriptedEvent("object recall recording stop", recordingData);                     //
            soundRecorder.StopRecording();                                                                                //
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #else
            // recordingData.Add("trial number", trialNumber);
            // scriptedEventReporter.ReportScriptedEvent("object recall typing start", recordingData);
            yield return ReportTypedResponses(trialNumber, "free recall", FREE_RECALL_LENGTH, freeInputField, freeResponse);
            // scriptedEventReporter.ReportScriptedEvent("object recall typing end", recordingData);
        #endif

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
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            #if UNITY_STANDALONE                                                                                            // NICLS
            if (useNiclServer && (trialNumber >= NUM_READ_ONLY_TRIALS))                                                     //
            {                                                                                                               //
                yield return new WaitForSeconds(WORD_PRESENTATION_DELAY);                                                   //
                yield return WaitForClassifier(niclsClassifierTypes[continuousTrialNum]);                                   //
            }                                                                                                               //
            else                                                                                                            //
            {                                                                                                               //
                float wordDelay = UnityEngine.Random.Range(WORD_PRESENTATION_DELAY - WORD_PRESENTATION_JITTER,              //
                                               WORD_PRESENTATION_DELAY + WORD_PRESENTATION_JITTER);                         //
                yield return new WaitForSeconds(wordDelay);                                                                 //
            }                                                                                                               //
            #endif                                                                                                          //
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////


            cueStore.familiarization_object.SetActive(true);
            if (!COURIER_ONLINE)
                messageImageDisplayer.SetCuedRecallMessage(true);

            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            #if UNITY_STANDALONE                                                                                            //  System.IO
                string output_file_name = practice                                                                          //
                            ? "practice-" + continuousTrialNum.ToString() + "-" + cueStore.GetStoreName()                   //
                            : continuousTrialNum.ToString() + "-" + cueStore.GetStoreName();                                //
                string output_directory = UnityEPL.GetDataPath();                                                           //
                string wavFilePath = System.IO.Path.Combine(output_directory, output_file_name) + ".wav";                   //
                string lstFilepath = System.IO.Path.Combine(output_directory, output_file_name) + ".lst";                   //
                AppendWordToLst(lstFilepath, cueStore.GetLastPoppedItemName());                                             //
            #endif                                                                                                          //
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            Dictionary<string, object> cuedRecordingData = new Dictionary<string, object>();
            cuedRecordingData.Add("trial number", COURIER_ONLINE ? trialNumber : continuousTrialNum);
            cuedRecordingData.Add("store", cueStore.GetStoreName());
            cuedRecordingData.Add("item", cueStore.GetLastPoppedItemName());
            cuedRecordingData.Add("store position", cueStore.transform.position.ToString());

            if (COURIER_ONLINE)
                scriptedEventReporter.ReportScriptedEvent("cued recall answer", cuedRecordingData);
            else
                scriptedEventReporter.ReportScriptedEvent("cued recall recording start", cuedRecordingData);
            
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            #if UNITY_STANDALONE                                                                                            // Microphone
                soundRecorder.StartRecording(wavFilePath);                                                                  //
                float startTime = Time.time;                                                                                //
                while ((!InputManager.GetButtonDown("Continue") || Time.time < startTime + MIN_CUED_RECALL_TIME_PER_STORE)  //
                    && Time.time < startTime + MAX_CUED_RECALL_TIME_PER_STORE)                                              //
                    yield return null;                                                                                      //
                                                                                                                            //
                scriptedEventReporter.ReportScriptedEvent("cued recall recording stop", cuedRecordingData);                 //
                soundRecorder.StopRecording();                                                                              //
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            #else   
                yield return ReportTypedResponses(trialNumber, "cued recall", CUED_RECALL_TIME_PER_STORE, cuedInputField, cuedResponse, cueStore.GetStoreName());
            #endif
            
            
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

        // LC : Whole chunk applied 
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #if UNITY_STANDALONE
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
            foreach (string deliveredObject in all_presented_objects)
                AppendWordToLst(lstFilepath, deliveredObject);

            scriptedEventReporter.ReportScriptedEvent("final object recall recording start", new Dictionary<string, object>());
            soundRecorder.StartRecording(wavFilePath);

            textDisplayer.ClearText();
            ClearTitle();
            yield return DoFreeRecallDisplay("all objects recall", FINAL_RECALL_LENGTH);
            scriptedEventReporter.ReportScriptedEvent("final object recall recording stop", new Dictionary<string, object>());
            soundRecorder.StopRecording();

            textDisplayer.ClearText();
            lowBeep.Play();
            scriptedEventReporter.ReportScriptedEvent("sound played", new Dictionary<string, object>() { { "sound name", "low beep" }, { "sound duration", lowBeep.clip.length.ToString() } });

            SetRamulatorState("RETRIEVAL", false, new Dictionary<string, object>());
            scriptedEventReporter.ReportScriptedEvent("stop final recall", new Dictionary<string, object>());
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #else
            highBeep.Play();
            scriptedEventReporter.ReportScriptedEvent("Sound played", new Dictionary<string, object>() { { "sound name", "high beep" }, { "sound duration", highBeep.clip.length.ToString() } });

            scriptedEventReporter.ReportScriptedEvent("final store recall start", new Dictionary<string, object>());
            placeHolder.text = LanguageSource.GetLanguageString("final store recall text");
            yield return ReportTypedResponses(-999, "final store recall", STORE_FINAL_RECALL_LENGTH, freeInputField, freeResponse);

            lowBeep.Play();
            scriptedEventReporter.ReportScriptedEvent("Sound played", new Dictionary<string, object>() { { "sound name", "low beep" }, 
                                                                                                        { "sound duration", lowBeep.clip.length.ToString() } });
            scriptedEventReporter.ReportScriptedEvent("final store recall stop", new Dictionary<string, object>());

            yield return SkippableWait(TIME_BETWEEN_DIFFERENT_RECALL_PHASES);

            highBeep.Play();
            scriptedEventReporter.ReportScriptedEvent("Sound played", new Dictionary<string, object>() { { "sound name", "high beep" }, { "sound duration", highBeep.clip.length.ToString() } });

            scriptedEventReporter.ReportScriptedEvent("final object recall start", new Dictionary<string, object>());
            placeHolder.text = LanguageSource.GetLanguageString("final object recall text");
            yield return ReportTypedResponses(-999, "final object recall", STORE_FINAL_RECALL_LENGTH, freeInputField, freeResponse);

            lowBeep.Play();
            scriptedEventReporter.ReportScriptedEvent("Sound played", new Dictionary<string, object>() { { "sound name", "low beep" }, 
                                                                                                        { "sound duration", lowBeep.clip.length.ToString() } });
            scriptedEventReporter.ReportScriptedEvent("final object recall stop", new Dictionary<string, object>());

        #endif
    }

    private IEnumerator DoFamiliarization()
    {
        yield return messageImageDisplayer.DisplayLanguageMessage(messageImageDisplayer.store_images_presentation_messages);
        yield return familiarizer.DoFamiliarization(MIN_FAMILIARIZATION_ISI, MAX_FAMILIARIZATION_ISI, FAMILIARIZATION_PRESENTATION_LENGTH);
    }

    private IEnumerator DoTownLearning(Environment environment, int trialNumber, int numDeliveries)
    {
        if (Config.skipTownLearning || InputManager.GetButton("Secret"))                                                
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
                random_store_index = UnityEngine.Random.Range(0, unvisitedStores.Count);
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
        trialData.Add("trial number", continuousTrialNum);
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

                //////////////////////////////////////////////////////////////////////////////////////////////////////////
                #if UNITY_STANDALONE                                                                                    // NICLS
                    if (useNiclServer && !practice)                                                                     //
                    {                                                                                                   //
                        yield return new WaitForSeconds(WORD_PRESENTATION_DELAY);                                       //
                        if (trialNumber < NUM_READ_ONLY_TRIALS)                                                         //
                            niclsInterface.SendEncodingToNicls(1);                                                      //
                        else                                                                                            //
                            yield return WaitForClassifier(niclsClassifierTypes[continuousTrialNum]);                   //
                    }                                                                                                   //
                    else                                                                                                //
                    {                                                                                                   //
                        float wordDelay = UnityEngine.Random.Range(WORD_PRESENTATION_DELAY - WORD_PRESENTATION_JITTER,  //
                                                WORD_PRESENTATION_DELAY + WORD_PRESENTATION_JITTER);                    //
                        yield return new WaitForSeconds(wordDelay);                                                     //
                    }                                                                                                   //
                #endif                                                                                                  //
                //////////////////////////////////////////////////////////////////////////////////////////////////////////

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
                
                //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                #if UNITY_STANDALONE                                                                                                    // System.IO
                string lstFilepath = practice                                                                                           //
                            ? System.IO.Path.Combine(UnityEPL.GetDataPath(), "practice-" + continuousTrialNum.ToString() + ".lst")      //
                            : System.IO.Path.Combine(UnityEPL.GetDataPath(), continuousTrialNum.ToString() + ".lst");                   //
                AppendWordToLst(lstFilepath, deliveredItemName);                                                                        //
                #endif                                                                                                                  //
                //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

                this_trial_presented_stores.Add(nextStore);
                all_presented_objects.Add(deliveredItemName);

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

            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            #if UNITY_STANDALONE                                                                                            // NICLS
            //Turn off ReadOnlyState                                                                                        //
            if (NICLS_COURIER && !practice && trialNumber == NUM_READ_ONLY_TRIALS)                                          //
            {                                                                                                               //
                Debug.Log("READ_ONLY_OFF");                                                                                 //
                niclsInterface.SendReadOnlyStateToNicls(0);                                                                 //
                niclsInterface.SendReadOnlyStateToNicls(0);                                                                 //
                niclsInterface.SendReadOnlyStateToNicls(0);                                                                 //
                niclsInterface.SendReadOnlyStateToNicls(0);                                                                 //
                niclsInterface.SendReadOnlyStateToNicls(0);                                                                 //
            }                                                                                                               //
            #endif                                                                                                          //
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            // EFR instructions                                                                                             
            if (Config.efrEnabled && practice                                                                               
                && trialNumber == EFR_PRACTICE_TRIAL_NUM && subSessionNum == 0)                                             
            {                                                                                                               
                if (Config.newEfrEnabled)                                                                                   
                {                                                                                                           
                    messageImageDisplayer.SetGeneralBigMessageText(titleText: "new efr instructions title",                 
                                                                    mainText: "new efr instructions main");                 
                    yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);   
                    yield return DoNewEfrKeypressCheck();                                                                   
                    yield return DoNewEfrKeypressPractice();                                                                
                }                                                                                                           
                else                                                                                                        
                {                                                                                                           
                    yield return DoVideo(LanguageSource.GetLanguageString("play movie"),                                    
                             LanguageSource.GetLanguageString("efr intro video"),                                           
                             VideoSelector.VideoType.EfrIntro);                                                             
                    yield return DoEfrKeypressCheck();                                                                      
                    yield return DoEfrKeypressPractice();                                                                   
                }                                                                                                           
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

            //////////////////////////////////////////////////////////////////////////////////
            #if UNITY_STANDALONE                                                            // Ramulator
                // Set ramulator trial start                                                //
                if (useRamulator)                                                           //
                    ramulatorInterface.BeginNewTrial(continuousTrialNum);                   //
            #endif                                                                          //
            //////////////////////////////////////////////////////////////////////////////////

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
        pointer.transform.eulerAngles = new UnityEngine.Vector3(0, rng.Next(360), 0);
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
                pointer.transform.eulerAngles = pointer.transform.eulerAngles + new UnityEngine.Vector3(0, InputManager.GetAxis("Horizontal") * Time.deltaTime * pointerRotationSpeed, 0);
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

    //////////////////////////////////////////////////////////////////////////////////////
    #if UNITY_STANDALONE                                                                // System.IO
    private void AppendWordToLst(string lstFilePath, string word)                       //
    {                                                                                   //
        System.IO.FileInfo lstFile = new System.IO.FileInfo(lstFilePath);               //
        bool firstLine = !lstFile.Exists;                                               //
        if (firstLine)                                                                  //
            lstFile.Directory.Create();                                                 //
        lstFile.Directory.Create();                                                     //
        using (System.IO.StreamWriter w = System.IO.File.AppendText(lstFilePath))       //
        {                                                                               //
            if (!firstLine)                                                             //
                w.Write(System.Environment.NewLine);                                    //
            w.Write(word);                                                              //
        }                                                                               //
    }                                                                                   //
    #endif                                                                              //
    //////////////////////////////////////////////////////////////////////////////////////

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

    private IEnumerator DoFreeRecallDisplay(string title, float waitTime, bool practice = false, bool efrDisabled = false)
    {
        BlackScreen();
        if (Config.efrEnabled && !efrDisabled)
        {
            yield return DoEfrDisplay(title, waitTime, practice);
        }
        else
        {
            messageImageDisplayer.SetGeneralBiggerMessageText(continueText: "speak now");
            yield return messageImageDisplayer.DisplayMessageTimed(
                messageImageDisplayer.general_bigger_message_display, waitTime);
        }
    }

    private IEnumerator DoEfrDisplay(string title, float waitTime, bool practice = false)
    {
        BlackScreen();
        if (Config.newEfrEnabled)
        {
            messageImageDisplayer.SetGeneralBiggerMessageText(titleText: "new efr message",
                                                              continueText: "speak now");
            yield return messageImageDisplayer.DisplayMessageTimed(
                messageImageDisplayer.general_bigger_message_display, waitTime);
        }
        else
        {
            SetEfrDisplay();
            messageImageDisplayer.SetEfrText(titleText: title);
            messageImageDisplayer.SetEfrElementsActive(speakNowText: true);
            yield return messageImageDisplayer.DisplayMessageTimedLRKeypressBold(
                    messageImageDisplayer.efr_display, waitTime,
                    efrLeftLogMsg, efrRightLogMsg, practice);
        }
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

    private IEnumerator DoNewEfrKeypressCheck()
    {
        if (Config.skipNewEfrKeypressCheck)
            yield break;

        scriptedEventReporter.ReportScriptedEvent("start efr keypress check", new Dictionary<string, object>());
        BlackScreen();

        // Display intro message
        messageImageDisplayer.SetGeneralMessageText(mainText: "efr check main");
        yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_message_display);

        // Ask for reject button press
        messageImageDisplayer.SetGeneralBiggerMessageText(titleText: "new efr message",
                                                          continueText: "");
        yield return messageImageDisplayer.DisplayMessage(
            messageImageDisplayer.general_bigger_message_display, "EfrReject");

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
            float efrKeypressPracticedelay = UnityEngine.Random.Range(EFR_KEYPRESS_PRACTICE_DELAY - EFR_KEYPRESS_PRACTICE_JITTER,
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

    private IEnumerator DoNewEfrKeypressPractice()
    {
        if (Config.skipNewEfrKeypressPractice)                                                                          
            yield break;                                                                                                
        
        scriptedEventReporter.ReportScriptedEvent("start efr keypress practice", new Dictionary<string, object>());
        BlackScreen();

        // Display intro message
        messageImageDisplayer.SetGeneralBigMessageText(titleText: "new efr keypress practice main",
                                                       mainText: "new efr keypress practice description");
        yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);

        // Ask for reject button press
        messageImageDisplayer.SetGeneralBiggerMessageText(titleText: "new efr message",
                                                          continueText: "");
        for (int i = 0; i < Config.newEfrKeypressPractices; i++)
            yield return messageImageDisplayer.DisplayMessage(
                messageImageDisplayer.general_bigger_message_display, "EfrReject");

        scriptedEventReporter.ReportScriptedEvent("stop efr keypress practice", new Dictionary<string, object>());
    }

    //WAITING, INSTRUCT, COUNTDOWN, ENCODING, WORD, DISTRACT, RETRIEVAL
    protected override void SetRamulatorState(string stateName, bool state, Dictionary<string, object> extraData)
    {
        //////////////////////////////////////////////////////////////////////
        #if UNITY_STANDALONE                                                // Ramulator
            if (OnStateChange != null)                                      //
                OnStateChange(stateName, state);                            //
                                                                            //
            if (useRamulator)                                               //
                ramulatorInterface.SetState(stateName, state, extraData);   //
        #endif                                                              //
        //////////////////////////////////////////////////////////////////////
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

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #if UNITY_STANDALONE                                                                                                            // NICLS
    private IEnumerator WaitForClassifier(NiclsClassifierType niclsClassifierType)                                                  //
    {                                                                                                                               //
        scriptedEventReporter.ReportScriptedEvent("start classifier wait", new Dictionary<string, object>());                       //
        Debug.Log(Enum.GetName(typeof(NiclsClassifierType), niclsClassifierType));                                                  //
        WaitUntilWithTimeout waitForClassifier = null;                                                                              //
        switch (niclsClassifierType)                                                                                                
        {                                                                                                                           // 
            case NiclsClassifierType.Pos:                                                                                           //
                waitForClassifier = new WaitUntilWithTimeout(niclsInterface.classifierReady, 5);                                    //
                yield return waitForClassifier;                                                                                     //
                scriptedEventReporter.ReportScriptedEvent("stop classifier wait",                                                   //
                    new Dictionary<string, object> { { "type", "Pos" }, { "timed out", waitForClassifier.timedOut() ? 1 : 0 } });   //
                break;                                                                                                              //
            case NiclsClassifierType.Neg:                                                                                           //
                waitForClassifier = new WaitUntilWithTimeout(niclsInterface.classifierNotReady, 5);                                 //
                yield return waitForClassifier;                                                                                     //
                scriptedEventReporter.ReportScriptedEvent("stop classifier wait",                                                   //
                    new Dictionary<string, object> { { "type", "Neg" }, { "timed out", waitForClassifier.timedOut() ? 1 : 0 } });   //
                break;                                                                                                              //
            case NiclsClassifierType.Sham:                                                                                          //
                yield return new WaitForSeconds((float)rng.NextDouble()*5f);                                                        //
                scriptedEventReporter.ReportScriptedEvent("stop classifier wait",                                                   //
                    new Dictionary<string, object> { { "type", "Sham" } });                                                         //
                break;                                                                                                              //
        }                                                                                                                           //
        Debug.Log("CLASSIFIER SAID TO GO ---------------------------------------------------------");                               //
    }                                                                                                                               //
    #endif                                                                                                                          //
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

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
