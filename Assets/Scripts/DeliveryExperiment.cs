using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Luminosity.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine.Networking;
using UnityEngine.UI;

using Accord.Math;
// using Accord.Statistics.Distributions.Multivariate;
using Accord.Statistics.Distributions.Univariate;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Distributions;

using static MessageImageDisplayer;
using static WorldDataReporter;
using UnityEngine.AI;

[System.Serializable]
public struct Environment
{
    public GameObject parent;
    public StoreComponent[] stores;
}

public enum StorePointType 
{
    SpatialPosition,
    SerialPosition,
    Random,
}

public class DeliveryExperiment : CoroutineExperiment
{
#if UNITY_WEBGL
        [DllImport("__Internal")]
        private static extern void EndTask();

        // [DllImport("__Internal")]
        // private static extern void NoRefresh();
#endif

    public delegate void StateChange(string stateName, bool on);
    public static StateChange OnStateChange;

    private static int sessionNumber = -1;
    private static int continuousSessionNumber = -1;
    private static string expName;

    // TODO: JPB: Make these configuration variables

    // Experiment type
    private const bool HOSPITAL_COURIER = false;
    private const bool NICLS_COURIER = false;
    private const bool VALUE_COURIER = true;
#if !UNITY_WEBGL
    private const bool COURIER_ONLINE = false;
#else
        private const bool COURIER_ONLINE = true;
#endif // !UNITY_WEBGL

    // debug
    private const bool skipFPS = true;

    private const string COURIER_VERSION = "v6.0.0";
    private const bool DEBUG = true;

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
    // Free recall length (seconds). Use 75s for the structured recall (value then free)
    private const float FREE_RECALL_LENGTH = 75f;
    private const float VALUE_RECALL_LENGTH = 10f;
    private const float PRACTICE_FREE_RECALL_LENGTH = 25f;
    private const float STORE_FINAL_RECALL_LENGTH = 90f;
    private const float OBJECT_FINAL_RECALL_LENGTH = NICLS_COURIER ? 120f : COURIER_ONLINE ? 240f : 180f;
    private const float TIME_BETWEEN_DIFFERENT_RECALL_PHASES = 2f;
    // private const float CUED_RECALL_TIME_PER_STORE = 10f;
    private const float MIN_CUED_RECALL_TIME_PER_STORE = 2f;
    private const float MAX_CUED_RECALL_TIME_PER_STORE = NICLS_COURIER ? 6f : 7.5f;
    private const float POINTING_CORRECT_THRESHOLD = Mathf.PI / 6; // 30°
    private const float ARROW_CORRECTION_TIME = 3f;
    private const float ARROW_ROTATION_SPEED = 1f;
    private const float PAUSE_BEFORE_RETRIEVAL = 10f;
    private const float DISPLAY_ITEM_PAUSE = 5f;
    private const float AUDIO_TEXT_DISPLAY = 1.6f;
    private const float WORD_PRESENTATION_TOTAL_TIME = 3f;
    private const float WORD_PRESENTATION_DELAY = 1f; //NICLS_COURIER ? 1f : 1.25f; // TODO: JPB: Fix this is NICLS
    private const float WORD_PRESENTATION_JITTER = 0.25f;
    private const float EFR_KEYPRESS_PRACTICE_DELAY = 2.25f;
    private const float EFR_KEYPRESS_PRACTICE_JITTER = 0.25f;

    // Keep as hardcoded values
    private const bool STAR_SYSTEM_ACTIVE = false;
    private const bool CHOOSE_NONVISIBLE_STORES = false;
    private const bool DO_REPEATS = false;
    private const bool RANDOM_STORE_ORDER = DO_REPEATS;

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
    private static bool useElemem = false;
#if !UNITY_WEBGL // Syncbox, Ramulator, and NICLS
    private Syncbox syncs;
    public RamulatorInterface ramulatorInterface;
    public NiclsInterface niclsInterface;
    public ElememInterface elememInterface;
#endif // !UNITY_WEBGL

    public PlayerMovement playerMovement;
    public GameObject pointer;
    public GameObject truePointer;
    public ParticleSystem pointerParticleSystem;
    public GameObject pointerMessage;
    public UnityEngine.UI.Text pointerText;
    public GameObject navigationMessage;
    public UnityEngine.UI.Text navigationText;
    public StarSystem starSystem;
    public DeliveryItems deliveryItems;
    public GameObject deliveryZones;
    private List<Transform> allZones;
    private List<Transform> distanceZonePair;
    private List<string> distanceItemPair;
    public Transform startLocation;
    public GameObject items;
    public Pauser pauser;

    public GameObject cityEnvironment;
    public GameObject terrain;

    public float pointerRotationSpeed = 10f;

    public GameObject memoryWordCanvas;

    public Environment[] environments;
    private Environment environment;

    private System.Random rng = new System.Random();

    private ActionButton efrCorrectButtonSide = ActionButton.RightButton;
    private string efrLeftLogMsg = "incorrect";
    private string efrRightLogMsg = "correct";

    private List<Transform> thisTrialPresentedStores = new List<Transform>();
    private StoreComponent previousTrialStore = null;
    private List<string> allPresentedObjects = new List<string>();

    List<NiclsClassifierType> niclsClassifierTypes = null;


    List<List<StoreComponent>> storeLists;  // For Value Courier

    // Typed Response fields
    public GameObject freeInputField;
    public UnityEngine.UI.InputField freeResponse;
    public UnityEngine.UI.Text placeHolder;
    public GameObject cuedInputField;
    public UnityEngine.UI.InputField cuedResponse;

    public GameObject freeRecallWrongType;
    public GameObject valueGuessWrongType;

    // Frame testing variables
    private static int FPScutoff = 30;
    private static bool isFrameTesting = false;
    private static int fpsValue = 0;
    private static float updateRateSeconds = 4.0f;
    private static int frameCount = 0;
    private float dt = 0.0f;
    private float fps = 0.0f;
    private List<int> fpsList = new List<int>();

    // Store Generation variables
    public bool[] freeTaskFirst;
    int freeIndex = 0;
    int valueIndex = 0;

    // Stim / No Stim Stores Lists
    public List<StoreComponent> StimStores = new List<StoreComponent>();
    public List<StoreComponent> noStimStores = new List<StoreComponent>();

    // Stim variables
    public List<string> stimTags = new List<string> { "3Hz", "8Hz" };
    public const float STIM_DURATION = 3f;
    private const int ELEMEM_REP_STIM_INTERVAL = 6000; // ms, 2*STIM_DURATION
    private const int ELEMEM_REP_STIM_DELAY = 1500; // ms
    private const int ELEMEM_REP_SWITCH_DELAY = 3000; // ms
    private double[] actualAvgStorePoints = new double[Config.trialsPerSession];
    private double[] guessAvgStorePoints = new double[Config.trialsPerSession];

    // Stim Tags
    List<string> GenerateStimTags(int numTrials)
    {
        List<string> result = new List<string>();
        stimTags.Shuffle();

        for (int i = 0; i < numTrials; i++)
            result.Add(stimTags[i % 2]);
        // Debug.Log("stim tags: " + string.Join(", ", stimTags));

        return result;
    }

    // store points generating algorithms
    double[] RandomStorePoints(int numStores)
    {
        // Same as temporal algorithm but shuffled
        System.Random rng = new System.Random();
        bool coinFlip = rng.Next(2) == 0;
        double[] storePoints = TemporalStorePoints(numStores, coinFlip);
        storePoints.Shuffle(rng);

        return storePoints;
    }

    double[] StandardizeStorePoints(double[] storePoints)
    {
        // std = sqrt(mean(x)), where x = abs(a - a.mean())**2
        double[] storePoints2 = Vector.Zeros(storePoints.Length);
        for (int i = 0; i < storePoints.Length; i++)
            storePoints2[i] = Math.Pow(Math.Abs(storePoints[i] - storePoints.Average()), 2);
        double std = Math.Sqrt(storePoints2.Average());

        // points_standardized = (points - mean(points)) / std(points)
        storePoints = Elementwise.Subtract(storePoints, storePoints.Average());
        storePoints = Elementwise.Divide(storePoints, std);

        return storePoints;
    }

    // double[] TemporalStorePoints(int numStores)
    // {
    //     // Setup covariance matrix variables
    //     double[] serialPositions = Vector.Range(new Accord.DoubleRange(0, numStores), 1);
    //     int N = serialPositions.Length;
    //     // double[] mu = Vector.Zeros(N);
    //     Vector<double> mu = Vector<double>.Build.Dense(N);
    //     // double[,] K = Matrix.Zeros(N, N);
    //     Matrix<double> K = Matrix<double>.Build.Dense(N, N);
    //     int rhoSq = numStores;

    //     // Create covariance matrix
    //     for (int i = 0; i < N; i++)
    //     {
    //         // ZR: add small number to get numerical stability
    //         K[i, i] = 1 + 1e-5;
    //         for (int j = i + 1; j < N; j++)
    //         {
    //             K[i, j] = Math.Exp(-(1d / (2d * rhoSq)) * Math.Pow(serialPositions[i] - serialPositions[j], 2));
    //             K[j, i] = K[i, j];
    //         }
    //     }

    //     K[(N - 1), (N - 1)] = 1;

    //     for (int i=0; i<N; i++)
    //     {
    //         for (int j=0; j<N; j++)
    //         {
    //             Debug.Log(i.ToString() + " " + j.ToString());
    //             Debug.Log(K[i, j]);
    //         }
    //     }

    //     //// ZR: Add Small value to ensure numerical stability
    //     //for (int i = 0; i < N; i++)
    //     //{
    //     //    K[i, i] += 1e-5;
    //     //}

    //     // double[] storePoints = new MultivariateNormalDistribution(mu, K).Generate();
    //     double[] storePoints = MatrixNormal.Sample(new System.Random(), mu.ToColumnMatrix(), K, Matrix<double>.Build.DenseIdentity(1)).Column(0).ToArray();
    //     Debug.Log(string.Join(",", storePoints));

    //     // standardize point values
    //     storePoints = StandardizeStorePoints(storePoints);
    //     // Debug.Log(string.Join(",", storePoints));

    //     // sample points from gaussian process
    //     double pointMean = new UniformContinuousDistribution(30, 70).Generate();
    //     double pointVar = new UniformContinuousDistribution(13, 17).Generate();
    //     storePoints = Elementwise.Multiply(storePoints, pointVar);
    //     storePoints = Elementwise.Add(storePoints, pointMean);

    //     return storePoints;
    // }

    public static double[] TemporalStorePoints(int numStores, bool highFirst = true)
    {
        // Debug.Log("TemporalStorePoints called with numStores: " + numStores + ", highFirst: " + highFirst);
        // --- Hardcoded parameters ---
        int valMin = 1;
        int valMax = 50;
        int primacyBuf = 2;
        int recencyBuf = 3;
        int numInGroupChosen = 4;

        if (numStores <= primacyBuf + recencyBuf)
        {
            primacyBuf = 0;
            recencyBuf = 0;
            Debug.LogWarning("List length too short for buffers. Removed Buffers");
        }
        int middleLen = numStores - (primacyBuf + recencyBuf);
        int firstHalfSize = middleLen / 2;
        int secondHalfSize = middleLen - firstHalfSize;

        int midpoint = (valMin + valMax) / 2;
        System.Random rng = new System.Random();

        // Define ranges
        List<int> firstRange, secondRange;
        if (highFirst)
        {
            firstRange = Enumerable.Range(midpoint, valMax - midpoint + 1).ToList();
            secondRange = Enumerable.Range(valMin, midpoint - valMin).ToList();
        }
        else
        {
            firstRange = Enumerable.Range(valMin, midpoint - valMin).ToList();
            secondRange = Enumerable.Range(midpoint, valMax - midpoint + 1).ToList();
        }

        // --- First half of middle ---
        var firstHalf = SampleFromList(firstRange, numInGroupChosen, rng);
        RemoveRange(firstRange, firstHalf);

        var firstNotInGroup = SampleFromList(secondRange, firstHalfSize - numInGroupChosen, rng);
        RemoveRange(secondRange, firstNotInGroup);

        firstHalf.AddRange(firstNotInGroup);
        Shuffle(firstHalf, rng);

        // --- Second half of middle ---
        var secondHalf = SampleFromList(secondRange, numInGroupChosen, rng);
        RemoveRange(secondRange, secondHalf);

        var secondNotInGroup = SampleFromList(firstRange, secondHalfSize - numInGroupChosen, rng);
        RemoveRange(firstRange, secondNotInGroup);

        secondHalf.AddRange(secondNotInGroup);
        Shuffle(secondHalf, rng);

        // Combine middle
        var middleVals = firstHalf.Concat(secondHalf).ToArray();
        int[] valsInt = new int[numStores];

        // --- Remaining pool for buffers ---
        var remainderRange = Enumerable.Range(valMin, valMax - valMin + 1)
                                       .Except(middleVals)
                                       .ToList();

        // Primacy buffer
        if (primacyBuf > 0)
        {
            var primacyChoice = SampleFromList(remainderRange, primacyBuf, rng);
            RemoveRange(remainderRange, primacyChoice);
            Array.Copy(primacyChoice.ToArray(), 0, valsInt, 0, primacyBuf);
        }

        // Middle
        Array.Copy(middleVals, 0, valsInt, primacyBuf, middleLen);

        // Recency buffer
        if (recencyBuf > 0)
        {
            var recencyChoice = SampleFromList(remainderRange, recencyBuf, rng);
            Array.Copy(recencyChoice.ToArray(), 0, valsInt, numStores - recencyBuf, recencyBuf);
        }

        double[] vals = Array.ConvertAll(valsInt, x => (double)x);

        Debug.Log("TemporalStorePoints: " + string.Join(", ", vals));
        Debug.Log("TemporalStorePoints: " + (highFirst ? "highFirst" : "lowFirst"));
        Debug.Log($"Primacy: {primacyBuf}, Recency: {recencyBuf}, numInGroupChosen: {numInGroupChosen}");

        return vals;
    }


    // --- Helpers ---
    private static List<int> SampleFromList(List<int> source, int count, System.Random rng)
    {
        if (count <= 0) return new List<int>();
        return source.OrderBy(_ => rng.Next()).Take(count).ToList();
    }

    private static void RemoveRange(List<int> source, IEnumerable<int> items)
    {
        foreach (var item in items.ToList())
            source.Remove(item);
    }

    private static void Shuffle<T>(List<T> list, System.Random rng)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            (list[k], list[n]) = (list[n], list[k]);
        }
    }


    double[] SpatialStorePoints(List<StoreComponent> stores)
    {
        // Setup covariance matrix variables
        int N = stores.Count;
        // double[] mu = Vector.Zeros(N);
        Vector<double> mu = Vector<double>.Build.Dense(N);
        // double[,] K = Matrix.Zeros(N, N);
        Matrix<double> K = Matrix<double>.Build.Dense(N, N);
        double rhoSq = N;

        // Create covariance matrix
        for (int i = 0; i < N - 1; i++)
        {
            K[i, i] = 1;
            for (int j = i + 1; j < N; j++)
            {
                var a = new double[2] { stores[i].transform.position.x, stores[i].transform.position.z };
                var b = new double[2] { stores[j].transform.position.x, stores[j].transform.position.z };
                K[i, j] = Math.Exp(-(1d / (2d * rhoSq)) * Accord.Math.Distance.Euclidean(a, b));
                Debug.Log(K[i, j]);
                K[j, i] = K[i, j];
            }
        }
        K[(N - 1), (N - 1)] = 1;

        // Generate point values
        double[] storePoints = MatrixNormal.Sample(new System.Random(), mu.ToColumnMatrix(), K, Matrix<double>.Build.DenseIdentity(1)).Column(0).ToArray();
        Debug.Log(string.Join(",", storePoints));

        // standardize point values
        storePoints = StandardizeStorePoints(storePoints);
        // Debug.Log(string.Join(",", storePoints));

        // sample points from gaussian process
        double pointMean = new UniformContinuousDistribution(30, 70).Generate();
        double pointVar = new UniformContinuousDistribution(13, 17).Generate();
        storePoints = Elementwise.Multiply(storePoints, pointVar);
        storePoints = Elementwise.Add(storePoints, pointMean);

        // Set store object point values
        for (int i = 0; i < N; i++)
        {
            stores[i].points = storePoints[i];
        }

        return storePoints;
    }

    // Convenience overload: accept an array of Transforms (delivery zones) and convert
    // them to their StoreComponent counterparts before computing spatial store points.
    double[] SpatialStorePoints(Transform[] storeTransforms)
    {
        var storeComponents = new List<StoreComponent>(storeTransforms.Length);
        foreach (var t in storeTransforms)
        {
            var sc = t.GetComponentInChildren<StoreComponent>();
            if (sc == null)
            {
                // Try direct GetComponent as fallback
                sc = t.GetComponent<StoreComponent>();
            }
            if (sc != null)
                storeComponents.Add(sc);
            else
                Debug.LogWarning("SpatialStorePoints: Transform does not contain a StoreComponent: " + t.name);
        }
        return SpatialStorePoints(storeComponents);
    }

    // LC: this is maually selected store lists that are visible from given store location
    //     need to update this dictionary manually when the town layout changes
    private Dictionary<string, List<string>> storeDict = new Dictionary<string, List<string>>()
    {
        { "gym", new List<string>{ "bakery", "hardware_store", "craft_shop", "dentist"} },
        { "craft_shop", new List<string>{ "gym", "hardware_store", "dentist", "cafe" } },
        { "hardware_store", new List<string>{ "toy_store", "barber_shop", "clothing_store", "gym", "craft_shop" } },
        { "clothing_store", new List<string>{ "barber_shop", "toy_store", "pharmacy", "hardware_store", "jewelry_store", "craft_shop" } },
        { "jewelry_store", new List<string>{ "pharmacy", "toy_store", "clothing_store" } },
        { "pharmacy", new List<string>{ "bakery", "jewelry_store", "clothing_store", "toy_store" } },
        { "bakery", new List<string>{ "pharmacy", "gym", "pet_store" } },
        { "pet_store", new List<string>{ "gym", "bakery" } },
        { "music_store", new List<string>{ "pizzeria", "florist", "dentist", "pet_store" } },
        { "florist", new List<string>{ "dentist", "pizzeria", "music_store" } },
        { "pizzeria", new List<string>{ "music_store", "florist", "dentist" } },
        { "dentist", new List<string>{ "cafe", "grocery_store", "craft_shop", "florist", "music_store", "pizzeria", "gym" } },
        { "grocery_store", new List<string>{ "cafe", "dentist", "craft_shop", "dentist" } },
        { "cafe", new List<string>{ "bike_shop", "grocery_store", "craft_shop", "dentist" } },
        { "bike_shop", new List<string>{ "grocery_store", "cafe", "clothing_store", "barber_shop", "jewelry_store" } },
        { "barber_shop", new List<string>{ "toy_store", "jewelry_store", "hardware_store", "grocery_store", "bike_shop" } },
        { "toy_store", new List<string>{ "pharmacy", "jewelry_store", "clothing_store", "barber_shop", "grocery_store", "bike_shop", "craft_shop" } }
    };

    // LC: this function pre-generates the list of stores to visit in a trial
    private Tuple<bool, List<StoreComponent>> getTrialStores(Environment environment, int num_deliveries)
    {
        List<StoreComponent> this_trial_presented_stores = new List<StoreComponent>();
        List<StoreComponent> unvisitedStores = new List<StoreComponent>(environment.stores);
        StoreComponent nextStore = null;
        bool success = true;

        Dictionary<string, List<string>> storeDict = new Dictionary<string, List<string>>()
    {
        { "gym", new List<string>{ "bakery", "hardware_store", "craft_shop", "dentist"} },
        { "craft_shop", new List<string>{ "gym", "hardware_store", "dentist", "cafe" } },
        { "hardware_store", new List<string>{ "toy_store", "barber_shop", "clothing_store", "gym", "craft_shop" } },
        { "clothing_store", new List<string>{ "barber_shop", "toy_store", "pharmacy", "hardware_store", "jewelry_store", "craft_shop" } },
        { "jewelry_store", new List<string>{ "pharmacy", "toy_store", "clothing_store" } },
        { "pharmacy", new List<string>{ "bakery", "jewelry_store", "clothing_store", "toy_store" } },
        { "bakery", new List<string>{ "pharmacy", "gym", "pet_store" } },
        { "pet_store", new List<string>{ "gym", "bakery" } },
        { "music_store", new List<string>{ "pizzeria", "florist", "dentist", "pet_store" } },
        { "florist", new List<string>{ "dentist", "pizzeria", "music_store" } },
        { "pizzeria", new List<string>{ "music_store", "florist", "dentist" } },
        { "dentist", new List<string>{ "cafe", "grocery_store", "craft_shop", "florist", "music_store", "pizzeria", "gym" } },
        { "grocery_store", new List<string>{ "cafe", "dentist", "craft_shop", "dentist" } },
        { "cafe", new List<string>{ "bike_shop", "grocery_store", "craft_shop", "dentist" } },
        { "bike_shop", new List<string>{ "grocery_store", "cafe", "clothing_store", "barber_shop", "jewelry_store" } },
        { "barber_shop", new List<string>{ "toy_store", "jewelry_store", "hardware_store", "grocery_store", "bike_shop" } },
        { "toy_store", new List<string>{ "pharmacy", "jewelry_store", "clothing_store", "barber_shop", "grocery_store", "bike_shop", "craft_shop" } }
    };

        // pick random store first
        int random_store_index = -1;
        int tries = 0;
        do
        {
            tries++;
            random_store_index = UnityEngine.Random.Range(0, unvisitedStores.Count);
            nextStore = unvisitedStores[random_store_index];
        }
        while (nextStore.IsVisible() && tries < 17);
        unvisitedStores.RemoveAt(random_store_index);
        this_trial_presented_stores.Add(nextStore);

        // then for the remaining stores, pick as such

        for (int i = 0; i < num_deliveries; i++)
        {
            // find the lastly visited store
            StoreComponent prevStore = this_trial_presented_stores.Last();
            string prevStoreName = prevStore.gameObject.name;

            // get the list of stores that are visible from this store
            //string prevStoreName = prevStore.gameObject.name;
            //if (!storeDict.ContainsKey(prevStoreName))
            //{
            //    Debug.LogError("Missing key in storeDict for store: " + prevStoreName);
            //    success = false;
            //    break; // or return Tuple.Create(false, null);
            //}
            List<string> visibleStores = storeDict[prevStoreName];
            List<StoreComponent> candidateStores = new List<StoreComponent>();

            // now loop through the remaining unvisited stores and find candidates
            foreach (StoreComponent store in unvisitedStores)
            {
                if (!visibleStores.Contains(store.gameObject.name))
                {
                    candidateStores.Add(store);
                }
            }

            // if there is/are candidate store(s)...
            if (candidateStores.Count > 0)
            {
                // pick randomly from candidate store list
                random_store_index = UnityEngine.Random.Range(0, candidateStores.Count);
                nextStore = candidateStores[random_store_index];
            }
            else
            {
                // pick randomly from uunvisited store list
                random_store_index = UnityEngine.Random.Range(0, unvisitedStores.Count);
                nextStore = unvisitedStores[random_store_index];
                success = false;
            }

            // remove / add appropriately
            unvisitedStores.Remove(nextStore);
            this_trial_presented_stores.Add(nextStore);
        }

        Tuple<bool, List<StoreComponent>> result = new Tuple<bool, List<StoreComponent>>
                                                            (success, this_trial_presented_stores);
        return result;
    }

    private List<List<StoreComponent>> listGenerator(Environment environment, int num_deliveries)
    {
        List<List<StoreComponent>> total = new List<List<StoreComponent>>();
        int num_total = 20;
        while (total.Count < num_total)
        {
            Tuple<bool, List<StoreComponent>> list = getTrialStores(environment, num_deliveries);
            if (list.Item1)
                total.Add(list.Item2);
        }
        return total;
    }

    private bool listCheck(List<StoreComponent> list1, List<StoreComponent> list2) //, Dictionary<string, List<string>> dict)
    {
        bool result = true;

        // initialize a dictionary with all the transitions in the sequence
        Dictionary<StoreComponent, StoreComponent> listDict = new Dictionary<StoreComponent, StoreComponent>();
        for (int i = 0; i < list1.Count - 1; i++)
        {
            listDict.Add(list1[i], list1[i + 1]);
        }

        // now check and see if there is repetitive transition
        for (int i = 0; i < list2.Count - 1; i++)
        {
            if (listDict.ContainsKey(list2[i]))
            {
                StoreComponent prevNext = listDict[list2[i]];
                StoreComponent currNext = list2[i + 1];

                if (prevNext == currNext)
                {
                    result = false;
                    break;
                }
            }
        }

        return result;
    }

    private List<List<StoreComponent>> getTotalList(Environment environment, int num_trials, int num_deliveries)
    {
        List<List<StoreComponent>> new_total = new List<List<StoreComponent>>();
        bool success = false;

        while (!success)
        {
            List<List<StoreComponent>> total = listGenerator(environment, num_deliveries);
            new_total = new List<List<StoreComponent>>();
            new_total.Add(total[0]);
            total.Remove(total[0]);

            Dictionary<StoreComponent, List<StoreComponent>> transDict = new Dictionary<StoreComponent, List<StoreComponent>>();
            List<StoreComponent> firstList = new_total[0];
            for (int i = 0; i < firstList.Count - 1; i++)
            {
                transDict.Add(firstList[i], new List<StoreComponent> { firstList[i + 1] });
            }

            for (int i = 0; i < total.Count; i++)
            {
                List<StoreComponent> prevList = new_total.Last();
                List<StoreComponent> currList = total[i];
                bool isGood = listCheck(prevList, currList);

                if (isGood)
                {
                    int cumCount = 0;
                    for (int j = 0; j < currList.Count - 1; j++)
                    {
                        StoreComponent currStore = currList[j];
                        StoreComponent nextStore = currList[j + 1];
                        if (transDict.ContainsKey(currStore))
                        {
                            List<StoreComponent> values = transDict[currStore];
                            if (values.Contains(nextStore))
                                cumCount += 1;
                        }
                    }

                    if (cumCount < 1)
                    {
                        new_total.Add(currList);
                        for (int k = 0; k < currList.Count - 1; k++)
                            transDict.AddOrUpdateKey(currList[k], currList[k + 1]);
                    }
                }

                if (new_total.Count == num_trials)
                {
                    success = true;
                    break;
                }
            }
        }

        return new_total;
    }

    // These names are used in for what is sent to the log
    // If you change them, then you have to change the event processing (or the logging code)
    private enum NiclsClassifierType
    {
        Pos,
        Neg,
        Sham
    }

    public static void ConfigureExperiment(bool newUseRamulator, bool newUseNiclServer, bool newUseElemem, int newSessionNumber, string newExpName)
    {
#if !UNITY_WEBGL // Ramulator and NICLS
        useRamulator = newUseRamulator;
        useNiclServer = newUseNiclServer;
        useElemem = newUseElemem;
        Config.elememStimMode = useElemem;
#endif // !UNITY_WEBGL
        sessionNumber = newSessionNumber;
        continuousSessionNumber = useNiclServer ? NICLS_READ_ONLY_SESSIONS + sessionNumber :
                                  sessionNumber;
        expName = newExpName;
        // Config.experimentConfigName = expName;

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
        // Cursor.visible = false;
        // Cursor.lockState = CursorLockMode.Locked;

        // Courier Online Frame testing
        if (COURIER_ONLINE && isFrameTesting)
        {
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
        // ZR: Get rid of delivery zones for now
        // allZones = new List<Transform>();
        // foreach (Transform area in deliveryZones.transform)
        // {
        //     foreach (Transform zone in area)
        //     {
        //         allZones.Add(zone);
        //         zone.GetComponent<DeliveryZone>().Hide();
        //     }
        // }

        if (UnityEPL.viewCheck)
            return;

        // Configure Experiment
        if (COURIER_ONLINE)
        {
            UnityEPL.AddParticipant(System.Guid.NewGuid().ToString());
            UnityEPL.SetExperimentName("COURIER_ONLINE");
            UnityEPL.SetSessionNumber(0);
            ConfigureExperiment(false, false, false, 0, "StandardCourier");
        }

        // if (DEBUG)
        // ConfigureExperiment(false, false, true, 1, "StandardCourier");

        // Session check
        if (sessionNumber == -1)
            throw new UnityException("Please call ConfigureExperiment before beginning the experiment.");

        // Exception handling
        AppDomain currentDomain = AppDomain.CurrentDomain;
        currentDomain.UnhandledException += new UnhandledExceptionEventHandler(UncaughtExceptionHandler);

        // Cursor removal
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.SetCursor(new Texture2D(0, 0), new Vector2(0, 0), CursorMode.ForceSoftware);

        // Player controls
        string controlName = "DrivingControls.xml";
        if (Config.Get(() => Config.singleStickController, false))
            controlName = "SingleStick" + controlName;
        else
            controlName = "Split" + controlName;

        if (Config.Get(() => Config.ps4Controller, false))
            controlName = "Ps4" + controlName;

#if !UNITY_WEBGL // System.IO
        string configPath = System.IO.Path.Combine(
            Directory.GetParent(Directory.GetParent(UnityEPL.GetParticipantFolder()).FullName).FullName,
            "configs");
        InputManager.Load(Path.Combine(configPath, controlName));
#endif // !UNITY_WEBGL

        // Turn player particles and falling leaves off for Nicls Courier
        if (NICLS_COURIER)
        {
            GameObject.Find("Player/player perspective/Particle System").SetActive(false);
            var trees = new List<int> { 26, 27, 28, 29, 30, 31, 32, 33, 34, 44 };
            string treeStr = "henry tc3 environment/edge/Tree ({0})/Particle System";
            foreach (int treeIndex in trees)
                GameObject.Find(string.Format(treeStr, treeIndex)).SetActive(false);
        }

        // Syncbox setup
#if !UNITY_WEBGL // Syncbox
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = 300;
        // Start syncpulses
        if (!Config.noSyncbox)
        {
            syncs = GameObject.Find("SyncBox").GetComponent<Syncbox>();
            // syncs.scriptedInput = scriptedEventReporter;
            // syncs.Init();
            syncs.StartPulse();
        }
#endif

        Dictionary<string, object> sceneData = new Dictionary<string, object>();
        sceneData.Add("sceneName", "MainGame");
        // scriptedEventReporter.ReportScriptedEvent("loadScene");

        // ZR: proeprly instantiate rng object
        this.rng = new System.Random();
        this.allPresentedObjects = new List<string>();

        StartCoroutine(ExperimentCoroutine());
    }

    private IEnumerator ExperimentCoroutine()
    {
        Debug.Log(UnityEPL.GetDataPath());

        foreach (string name in UnityEPL.GetParticipants())
            Debug.Log(name);

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

        // Setup Elemem
        yield return elememInterface.BeginNewSession(sessionNumber,
            disableInterface: !Config.elememOn,
            uniqueStimTags: useElemem ? stimTags.ToArray() : null);
#endif // !UNITY_WEBGL

        // Write versions to logfile
        LogVersions(expName);

        // Set Config for Courier Online
        if (COURIER_ONLINE)
            yield return Config.GetOnlineConfig();

        // Save Config
        Config.SaveConfigs(scriptedEventReporter, UnityEPL.GetDataPath());

        // Setup Environment
        yield return EnableEnvironment();  // TODO: JPB: there is a race condition between saving config and generating session folder probably (remove enable environment to test)

        // set stim/no_stim stores unique for each subject
        var reliableRandom = deliveryItems.ReliableRandom();
        List<StoreComponent> allStores = new List<StoreComponent>(environment.stores);
        allStores.Shuffle(reliableRandom);
        for (int i = 0; i < allStores.Count; i++)
        {
            if (i % 2 == 1)
                StimStores.Add(allStores[i]);
            else
                noStimStores.Add(allStores[i]);
        }

        // Frame Rate Test
        if (COURIER_ONLINE)
            yield return DoFrameTest();

        BlackScreen();

        // Intros
        yield return DoIntros();

        // Town Learning
        int trialsForFirstSubSession = Config.trialsPerSession;
        if (!Config.skipTownLearning && sessionNumber < SINGLE_TOWN_LEARNING_SESSIONS + DOUBLE_TOWN_LEARNING_SESSIONS)
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
            else if (VALUE_COURIER && isFirstSession) // ZR: added back VALUE COURIER
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
        if (sessionNumber == 0 && !useNiclServer && !COURIER_ONLINE)
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
        string endMessage = LanguageSource.GetLanguageString("end message");

        // NICLS_COURIER
        //     ? LanguageSource.GetLanguageString("end message")
        //     : LanguageSource.GetLanguageString("end message scored") + "\n\n" + starSystem.CumulativeRating().ToString("+#.##;-#.##");
        textDisplayer.DisplayText("end text", endMessage);

#if !UNITY_WEBGL // WebGL DLL
        // LC: ELEMEM
        if (HOSPITAL_COURIER)
            elememInterface.SendExitMessage();

        // TODO: JPB: (Hokua) Wait for button press to quit
        while (true)
            yield return null;
#else
#if !UNITY_EDITOR // LC: remove after upgrade
                WebGLInput.captureAllKeyboardInput = false;
#endif // !UNITY_EDITOR
            yield return new WaitForSeconds(5.0f);
            EndTask();
#endif // !UNITY_WEBGL
    }

    private IEnumerator DoSubSession(int subSessionNum, int priorTrialsThisSession, int trialsPerSubSession)
    {
        BlackScreen();

        if (Config.elememOn)
            elememInterface.SendSessionMessage(UnityEPL.GetSessionNumber());

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
        double compensation = DoCompensation();
        string[] formatValues = new string[] { compensation.ToString("C") };
        // Call SetGeneralMessageText, passing the format array for mainText
        messageImageDisplayer.SetGeneralMessageText(
            mainText: "earned_tips",
            mtFormatVals: formatValues
        );
        yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_message_display);
    }

    private IEnumerator DoFrameTest()
    {
        if (skipFPS)
            yield break;

        Debug.Log("Frame Testing");

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


        // TODO: LC: add hard cutoff at 30 FPS : DONE
        if (averageFps < FPScutoff)
        {
            Debug.Log("FPS CHECK FAILED");
            scriptedEventReporter.ReportScriptedEvent("fps check failed");
            messageImageDisplayer.SetFPSDisplayText(fpsValue: averageFps.ToString(), mainText: "frame test end fail", continueText: "no continue");
            yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display, "No Continue");
        }
        else
        {
            Debug.Log("FPS CHECK PASSED");
            scriptedEventReporter.ReportScriptedEvent("fps check passed");
            messageImageDisplayer.SetFPSDisplayText(fpsValue: averageFps.ToString(), mainText: "frame test end pass");
            yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);

            yield return new WaitForSeconds(1.5f);

            messageImageDisplayer.SetGeneralBigMessageText(mainText: "frame test continue main");
            yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);
        }
    }

    private IEnumerator DoIntros()
    {
        if (Config.skipIntros)
            yield break;

        Debug.Log("DoIntros");

        BlackScreen();

        // firts session of NICLS_COURIER
        if (NICLS_COURIER && sessionNumber == 0 && !useNiclServer)
        {
            yield return DoVideo(LanguageSource.GetLanguageString("play movie"),
                                 LanguageSource.GetLanguageString("standard intro video"),
                                 VideoSelector.VideoType.NiclsMainIntro);
        }
        // not the first session, no need for intro video
        else if (NICLS_COURIER) // sessionNumber >= 1 || useNiclServer                                              
        {
            yield return DoRecapInstructions();
        }
        else if (HOSPITAL_COURIER)
        {
            if (sessionNumber == 0)
                yield return DoVideo(LanguageSource.GetLanguageString("play movie"),
                                    LanguageSource.GetLanguageString("standard intro video"),
                                    VideoSelector.VideoType.townlearningVideo);
            else
                yield return DoRecapInstructions(recap: true);

            // yield return DoSubjectSessionQuitPrompt(sessionNumber, LanguageSource.GetLanguageString("running participant"));
        }
        else if (VALUE_COURIER)
        {
            if (sessionNumber == 0)
                yield return DoVideo(LanguageSource.GetLanguageString("play movie"),
                                    LanguageSource.GetLanguageString("standard intro video"),
                                    VideoSelector.VideoType.townlearningVideo);
            else
                yield return DoRecapInstructions(recap: true);

            // yield return DoSubjectSessionQuitPrompt(sessionNumber, LanguageSource.GetLanguageString("running participant"));
        }
        else
        {
            Debug.Log("THIS IS THE VIDEO YOU ARE LOOKING FOR");
            // yield return DoVideo(LanguageSource.GetLanguageString("play movie"),
            //                     LanguageSource.GetLanguageString("standard intro video"),
            //                     VideoSelector.VideoType.valueIntro);
        }

#if !UNITY_WEBGL // Microphone
        yield return DoMicrophoneTest(LanguageSource.GetLanguageString("microphone test"),
                                      LanguageSource.GetLanguageString("after the beep"),
                                      LanguageSource.GetLanguageString("recording"),
                                      LanguageSource.GetLanguageString("playing"),
                                      LanguageSource.GetLanguageString("recording confirmation"));
#endif // !UNITY_WEBGL
    }

    private IEnumerator DoRecapInstructions(bool forceFR = false, bool recap = false)
    {
        GameObject[] messages;

        if (Config.efrEnabled && !forceFR) // TODO: JPB: Hospital handle ECR case  
            if (Config.twoBtnEfrEnabled)
                messages = messageImageDisplayer.recap_instruction_messages_efr_2btn_en;
            else
                messages = HOSPITAL_COURIER ? messageImageDisplayer.hospital_recap_instruction_messages_en
                                            : messageImageDisplayer.recap_instruction_messages_efr_en;
        else
            messages = messageImageDisplayer.recap_instruction_messages_fr_en;

        // LC: if you want them to go back and forth...?
        if (recap)
        {
            // LC: prevent left and right arrow key from actually moving the player in the background
            playerMovement.Freeze();
            int lastpage = messages.Length - 1;
            int currpage = 0;
            int prevpage = 0;

            while ((currpage != lastpage) || !InputManager.GetButtonDown("Continue"))
            {
                if (InputManager.GetButtonDown("Secret"))
                    break;

                if (InputManager.GetButtonDown("UI_Left") || InputManager.GetButtonDown("EfrLeft"))
                {
                    prevpage = currpage;
                    currpage = Math.Max(currpage - 1, 0);
                }
                if (InputManager.GetButtonDown("UI_Right") || InputManager.GetButtonDown("EfrRight"))
                {
                    prevpage = currpage;
                    currpage = Math.Min(currpage + 1, lastpage);
                }

                if ((currpage == lastpage) && InputManager.GetButton("EfrReject"))
                {
                    messages[currpage].SetActive(false);
                    yield return DoVideo(LanguageSource.GetLanguageString("play movie"),
                                        LanguageSource.GetLanguageString("standard intro video"),
                                        VideoSelector.VideoType.efrRecapVideo);
                    messages[currpage].SetActive(true);
                }
                messages[prevpage].SetActive(false);
                messages[currpage].SetActive(true);

                yield return null;
            }
            messages[currpage].SetActive(false);

            playerMovement.Unfreeze();
        }
        else
        {
            foreach (var message in messages)
                yield return messageImageDisplayer.DisplayMessage(message);
        }
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

        thisTrialPresentedStores = new List<StoreComponent>();
        List<StoreComponent> unvisitedStores = new List<StoreComponent>(environment.stores);

        for (int i = 0; i < numDeliveries; i++)
        {
            messageImageDisplayer.please_find_the_blah_reminder.SetActive(false);

            StoreComponent nextStore = PickNextStore(unvisitedStores);
            unvisitedStores.Remove(nextStore);
            thisTrialPresentedStores.Add(nextStore);

            playerMovement.Freeze();
            EnablePlayerTransfromReporting(false);
            pointerParticleSystem.Play();
            yield return new WaitForSeconds(.2f);
            pointerParticleSystem.Stop();

            navigationMessage.SetActive(true);
            if (i != 0)
                navigationText.text = LanguageSource.GetLanguageString("correct pointing");
            else
                navigationText.text = "";
            navigationText.text += LanguageSource.GetLanguageString("town learning prompt 1") +
                                    LanguageSource.GetLanguageString(nextStore.GetStoreName()) + ".\n" +
                                    LanguageSource.GetLanguageString("town learning prompt 2") +
                                    LanguageSource.GetLanguageString(nextStore.GetStoreName()) + ".\n\n" +
                                    LanguageSource.GetLanguageString("continue");

            while (!InputManager.GetButtonDown("Continue"))
                yield return null;
            navigationMessage.SetActive(false);


            playerMovement.Unfreeze();

            messageImageDisplayer.please_find_the_blah_reminder.SetActive(true);
            messageImageDisplayer.SetReminderText(nextStore.GetStoreName());

            float startTime = Time.time;
            float cumDist = 0f;
            float dist = CalculateDistance(nextStore.transform.Find("DeliveryZone"));
            bool distTriggerActivated = false;
            bool timeTriggerActivated = false;
            while (!nextStore.PlayerInDeliveryPosition())
            {
                yield return null;

                float newDist = CalculateDistance(nextStore.transform.Find("DeliveryZone"));
                if (newDist > dist)cumDist += newDist - dist;
                dist = newDist;

                if (Time.time > startTime + POINTING_INDICATOR_DELAY && Config.timeTrigger) timeTriggerActivated = true;
                if (cumDist > DISTANCE_THRESHOLD && Config.distTrigger) distTriggerActivated = true;

                if (timeTriggerActivated || distTriggerActivated)
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
                                                   {"store position", nextStore.transform.position.ToString()},
                                                   {"distance trigger activated", distTriggerActivated.ToString()},
                                                   {"time trigger activated", timeTriggerActivated.ToString()}});
        }

    SkipRemainingDeliveries:
        messageImageDisplayer.please_find_the_blah_reminder.SetActive(false);
        scriptedEventReporter.ReportScriptedEvent("stop town learning");
    }

    private IEnumerator DoDeliveries(int trialNumber, int continuousTrialNum, bool practice = false, bool skipLastDelivStores = false,
                                     StorePointType storePointType = StorePointType.Random, bool freeFirst = true, string stimTag = null, bool highFirst = true)
    {
        Dictionary<string, object> trialData = new Dictionary<string, object>();
        trialData.Add("trial number", continuousTrialNum);
        if (practice)
            scriptedEventReporter.ReportScriptedEvent("start practice deliveries", trialData);
        else
            scriptedEventReporter.ReportScriptedEvent("start deliveries", trialData);

        WorldScreen();

        // LC: add slight delay before calculating PickNextStore
        //     for some reason, before the camera is initialized, PickNextStore calculates the nonvisible stores, 
        //     in this case, every store in the town
        //     so, we need to add slight delay for the camera to "see" the stores in town
        yield return new WaitForSeconds(0.1f);
        int deliveries = practice ? Config.deliveriesPerPracticeTrial : Config.deliveriesPerTrial;





        // Set store points for the delivery day
        List<StoreComponent> curStoreList = null;
        double[] allStoresPoints = null;


        if (VALUE_COURIER)
        {
            curStoreList = storeLists[trialNumber];
            switch (storePointType)
            {
                case StorePointType.Random:
                    allStoresPoints = RandomStorePoints(unvisitedStores.Count);
                    break;
                case StorePointType.SerialPosition:
                    allStoresPoints = TemporalStorePoints(unvisitedStores.Count, highFirst);
                    break;
                case StorePointType.SpatialPosition:
                    SpatialStorePoints(unvisitedStores.ToArray());
                    break;
            }
        }

        SetRamulatorState("ENCODING", true, new Dictionary<string, object>());
        SetElememState("ENCODING");
        volume.SetFloat("MasterVolume", 20);

        messageImageDisplayer.please_find_the_blah_reminder.SetActive(true);




        //messageImageDisplayer.please_find_the_blah_reminder.SetActive(true);
        // genreate store order
        // if (RANDOM_STORE_ORDER)
        // {
        //     int enabledArea = rng.Next(3);
        //     foreach (Transform area in deliveryZones.transform)
        //     {
        //         area.gameObject.SetActive(false);
        //     }
        //     deliveryZones.transform.GetChild(enabledArea).gameObject.SetActive(true);
        // }


        // allZones = new List<Transform>();
        // foreach (Transform area in deliveryZones.transform)
        // {
        //     if (area.gameObject.activeSelf)
        //         foreach (Transform zone in area)
        //         {
        //             allZones.Add(zone);
        //             zone.GetComponent<DeliveryZone>().Hide();
        //         }
        // }

        int deliveries = practice ? Config.deliveriesPerPracticeTrial : Config.deliveriesPerTrial;

        int craft_shop_delivery_num = rng.Next(deliveries - 1);
        int numLocationRepeats = deliveries / 4;
        List<Transform> unvisitedStores = null;
        List<Transform> visitedStores = new List<Transform>();
        List<Transform> repeatStores = new List<Transform>();
        List<Transform> stimStoresToVisit = null;
        List<Transform> nostimStoresToVisit = null;

        List<string> deliveryItems = GetItemsList(deliveries);

        string outputText = "";
        foreach (string item in deliveryItems) outputText += item + "\n";
        Debug.Log(outputText);

        // ZR: NEW
        unvisitedStores = new List<StoreComponent>(environment.stores);
        if (skipLastDelivStores)
            foreach (var store in thisTrialPresentedStores)
                unvisitedStores.Remove(store);

        thisTrialPresentedStores = new List<StoreComponent>();

        // LC: Set the Stim freq
        if (useElemem && (stimTag != null))
        {
            elememInterface.SendStimSelectMessage(stimTag);
            Debug.Log("This Trial is using " + stimTag + " as stim frequency");
        }

        StoreComponent lastStoreToVisit = null;
        StoreComponent nextStore = null;


        // ZR store average for compensation
        if (!practice)
        {
            actualAvgStorePoints[trialNumber] = allStoresPoints.Average();
            Debug.Log("Average store points for trial " + trialNumber + ": " + actualAvgStorePoints[trialNumber]);
        }

        if (skipLastDelivStores)
            foreach (var store in thisTrialPresentedStores)
                unvisitedStores.Remove(store);

        thisTrialPresentedStores = new List<StoreComponent>();

        // LC: Set the Stim freq
        if (useElemem && (stimTag != null))
        {
            elememInterface.SendStimSelectMessage(stimTag);
            Debug.Log("This Trial is using " + stimTag + " as stim frequency");
        }

        StoreComponent lastStoreToVisit = null;
        StoreComponent nextStore = null;

        for (int i = 0; i <= deliveries; i++)
        {
            if (VALUE_COURIER)
            {
                nextStore = curStoreList[i];
            }

            playerMovement.Freeze();
            EnablePlayerTransfromReporting(false);

            messageImageDisplayer.please_find_the_blah_reminder.SetActive(false);
            messageImageDisplayer.SetReminderText(nextStore.GetStoreName());
            // ZR: GOT RID OF POINTING TASK
            // if (!NICLS_COURIER) // && !VALUE_COURIER)
            //     yield return DoPointingTask(nextStore);
            messageImageDisplayer.please_find_the_blah_reminder.SetActive(true);
            playerMovement.Unfreeze();
            EnablePlayerTransfromReporting(true);

            if (i == deliveries) messageImageDisplayer.cue_return.SetActive(true);

            float startTime = Time.time;
            float cumDist = 0f;
            float dist = CalculateDistance(nextStore.transform.Find("DeliveryZone"));
            bool distTriggerActivated = false;
            bool timeTriggerActivated = false;
            while (!nextStore.PlayerInDeliveryPosition())
            {
                yield return null;

                float newDist = CalculateDistance(nextStore.transform.Find("DeliveryZone"));
                if (newDist > dist) cumDist += newDist - dist;
                dist = newDist;

                if (Time.time > startTime + POINTING_INDICATOR_DELAY && Config.timeTrigger) timeTriggerActivated = true;
                if (cumDist > DISTANCE_THRESHOLD && Config.distTrigger) distTriggerActivated = true;

                if (timeTriggerActivated || distTriggerActivated)
                    yield return DisplayPointingIndicator(nextStore, true);
                if (InputManager.GetButton("Secret"))
                    goto SkipRemainingDeliveries;
            }
            yield return DisplayPointingIndicator(nextStore, false);

            // Get points for this store, default value being -1
            double storePoints = 0.0;
            if (VALUE_COURIER && i != deliveries - 1)
            {
                switch (storePointType)
                {
                    case StorePointType.Random:
                    case StorePointType.SerialPosition:
                        storePoints = allStoresPoints[i];
                        break;
                    case StorePointType.SpatialPosition:
                        storePoints = nextStore.points;
                        break;
                }
            }
            else
                storePoints = -1.0;

             ///AUDIO PRESENTATION OF OBJECT///
            if (i != deliveries - 1)
            {
                playerMovement.Freeze();
                EnablePlayerTransfromReporting(false);

                Debug.Log(trialNumber);
                AudioClip deliveredItem = nextStore.PopItem();
                float wordDelay = 0f;

                bool isStimStore = StimStores.Contains(nextStore);
                // Debug.Log("is this stim store? " + isStimStore.ToString());

                #if !UNITY_WEBGL 
                    // NICLS
                    if (useNiclServer && !practice)
                    {
                        yield return new WaitForSeconds(WORD_PRESENTATION_DELAY);
                        if (trialNumber < NUM_CLASSIFIER_NORMALIZATION_TRIALS)
                            niclsInterface.SendEncoding(1);
                        else
                            yield return WaitForClassifier(niclsClassifierTypes[continuousTrialNum]);
                    }
                    // Hospital
                    else
                    {
                        // LC: ELEMEM
                        // ZAP it when they deliver items to stim store
                        if (useElemem && !practice && isStimStore)
                        {
                            elememInterface.SendStimMessage();
                            Debug.Log("ZZZAPPP THIS STORE");
                        }

                        wordDelay = UnityEngine.Random.Range(WORD_PRESENTATION_DELAY - WORD_PRESENTATION_JITTER,
                                                             WORD_PRESENTATION_DELAY + WORD_PRESENTATION_JITTER);
                        yield return new WaitForSeconds(wordDelay);
                    }
                #endif

                string deliveredItemName = deliveredItem.name;
                int roundedPoints = (int)Math.Round(storePoints);
                string deliveredItemNameWithSpace = VALUE_COURIER ? deliveredItemName.Replace('_', ' ') + ", " + roundedPoints.ToString() 
                                                                  : deliveredItemName.Replace('_', ' ');
                var itemPresentationInfo = new Dictionary<string, object>() { {"trial number", continuousTrialNum},
                                                                            {"item name", deliveredItemName},
                                                                            {"store name", nextStore.GetStoreName()},
                                                                            {"serial position", i+1},
                                                                            {"player position", playerMovement.transform.position.ToString()},
                                                                            {"store position", nextStore.transform.position.ToString()},
                                                                            {"distance trigger activated", distTriggerActivated.ToString()},
                                                                            {"time trigger activated", timeTriggerActivated.ToString()},
                                                                            {"store value", roundedPoints},
                                                                            {"point condition", (int)storePointType},
                                                                            {"task condition", freeFirst ? "FreeFirst" : "ValueFirst"},
                                                                            {"stim condition", useElemem ? isStimStore : false},
                                                                            {"stim tag", stimTag} };

                #if !UNITY_WEBGL // System.IO
                    string lstFilepath = practice
                                ? System.IO.Path.Combine(UnityEPL.GetDataPath(), "practice-" + continuousTrialNum.ToString() + ".lst")
                                : System.IO.Path.Combine(UnityEPL.GetDataPath(), continuousTrialNum.ToString() + ".lst");
                    AppendWordToLst(lstFilepath, deliveredItemName);
                #endif
                allPresentedObjects.Add(deliveredItemName);

                audioPlayback.clip = deliveredItem;
                audioPlayback.Play();
                

                scriptedEventReporter.ReportScriptedEvent("object presentation begins", itemPresentationInfo);
                SetRamulatorState("WORD", true, new Dictionary<string, object>() { { "word", deliveredItemName } });

                //add visuals with sound
                messageImageDisplayer.deliver_item_visual_dislay.SetActive(true);
                Debug.Log(deliveredItemNameWithSpace);
                messageImageDisplayer.SetDeliverItemText(deliveredItemNameWithSpace);
                yield return SkippableWait(AUDIO_TEXT_DISPLAY);
                messageImageDisplayer.deliver_item_visual_dislay.SetActive(false);

                SetRamulatorState("WORD", false, new Dictionary<string, object>() { { "word", deliveredItemName } });

                scriptedEventReporter.ReportScriptedEvent("audio presentation finished",
                                                          new Dictionary<string, object>());

                // LC: complete full 3 second interval
                if (EFR_COURIER)
                {
                    float restDelay = WORD_PRESENTATION_TOTAL_TIME - wordDelay - AUDIO_TEXT_DISPLAY;
                    yield return new WaitForSeconds(restDelay);
                }
                playerMovement.Unfreeze();
                EnablePlayerTransfromReporting(true);
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






//             if (i == deliveries) messageImageDisplayer.cue_return.SetActive(false);

//             nextStore.GetComponent<DeliveryZone>().Hide();




//             ///AUDIO PRESENTATION OF OBJECT///
//             if (i != deliveries)
//             {
//                 string nextItem = deliveryItems[i];
//                 playerMovement.Freeze();
//                 Debug.Log(trialNumber);
//                 //AudioClip deliveredItem = nextStore.PopItem();


//                 AudioClip deliveredItem = items.transform.Find("Audio").GetComponent<AudioFiles>().audioFiles.Find(clip => clip.name.Equals("item_" + nextItem));


//                 float wordDelay = 0f;

//                 bool isStimStore = StimStores.Contains(nextStore);
//                 Debug.Log("is this stim store? " + isStimStore.ToString());

// #if !UNITY_WEBGL
//                 // NICLS
//                 if (useNiclServer && !practice)
//                 {
//                     yield return new WaitForSeconds(WORD_PRESENTATION_DELAY);
//                     if (trialNumber < NUM_CLASSIFIER_NORMALIZATION_TRIALS)
//                         niclsInterface.SendEncoding(1);
//                     else
//                         yield return WaitForClassifier(niclsClassifierTypes[continuousTrialNum]);
//                 }
//                 // Hospital
//                 else
//                 {
//                     // LC: ELEMEM
//                     //ZAP it when they deliver items to stim store
//                     if (useElemem && !practice && isStimStore)
//                     {
//                         elememInterface.SendStimMessage();
//                         Debug.Log("ZZZAPPP THIS STORE");
//                     }

//                     wordDelay = UnityEngine.Random.Range(WORD_PRESENTATION_DELAY - WORD_PRESENTATION_JITTER,
//                                                              WORD_PRESENTATION_DELAY + WORD_PRESENTATION_JITTER);
//                     yield return new WaitForSeconds(wordDelay);
//                 }
// #endif

//                 string deliveredItemName = deliveredItem.name;
//                 int roundedPoints = (int)Math.Round(storePoints);
//                 Debug.Log(roundedPoints);
//                 string deliveredItemNameWithSpace = VALUE_COURIER ? nextItem.Replace('_', ' ') + ", " + roundedPoints.ToString()
//                                                                   : nextItem.Replace('_', ' ');
//                 var itemPresentationInfo = new Dictionary<string, object>() { {"trial number", continuousTrialNum},
//                                                                             {"item name", nextItem},
//                                                                             {"store name", nextStore.name},
//                                                                             {"serial position", i+1},
//                                                                             {"player position", playerMovement.transform.position.ToString()},
//                                                                             {"store position", nextStore.position.ToString()},
//                                                                             {"store value", roundedPoints},
//                                                                             {"point condition", (int)storePointType},
//                                                                             {"task condition", freeFirst ? "FreeFirst" : "ValueFirst"},
//                                                                             {"stim condition", useElemem ? isStimStore : false},
//                                                                             {"stim tag", stimTag} };

// #if !UNITY_WEBGL // System.IO
//                 string lstFilepath = practice
//                             ? System.IO.Path.Combine(UnityEPL.GetDataPath(), "practice-" + continuousTrialNum.ToString() + ".lst")
//                             : System.IO.Path.Combine(UnityEPL.GetDataPath(), continuousTrialNum.ToString() + ".lst");
//                 AppendWordToLst(lstFilepath, deliveredItemName);
// #endif
//                 allPresentedObjects.Add(nextItem);


//                 audioPlayback.clip = deliveredItem;
//                 audioPlayback.Play();


//                 scriptedEventReporter.ReportScriptedEvent("object presentation begins", itemPresentationInfo);
//                 SetRamulatorState("WORD", true, new Dictionary<string, object>() { { "word", nextItem } });

//                 //add visuals with sound
//                 messageImageDisplayer.deliver_item_visual_dislay.SetActive(true);
//                 Debug.Log(deliveredItemNameWithSpace);
//                 messageImageDisplayer.SetDeliverItemText(deliveredItemNameWithSpace);
//                 yield return SkippableWait(AUDIO_TEXT_DISPLAY);
//                 messageImageDisplayer.deliver_item_visual_dislay.SetActive(false);

//                 SetRamulatorState("WORD", false, new Dictionary<string, object>() { { "word", nextItem } });

//                 scriptedEventReporter.ReportScriptedEvent("audio presentation finished",
//                                                           new Dictionary<string, object>());

//                 if (HOSPITAL_COURIER)
//                 {
//                     // LC: complete full 3 second interval
//                     float restDelay = WORD_PRESENTATION_TOTAL_TIME - wordDelay - AUDIO_TEXT_DISPLAY;
//                     yield return new WaitForSeconds(restDelay);
//                     // wait a bit longer for hospital flow, then unfreeze below
//                 }

//                 // Always unfreeze after the audio presentation so the player can move again
//                 playerMovement.Unfreeze();
//             }

//             if (repeatStores.Contains(nextStore))
//             {
//                 yield return revisitStore(visitedStores, nextStore, unvisitedStores[0]);
//             }

//             visitedStores.Add(nextStore);

//         }
//         volume.SetFloat("MasterVolume", 0);
//         BlackScreen();

//     SkipRemainingDeliveries:
//         messageImageDisplayer.please_find_the_blah_reminder.SetActive(false);

//         SetRamulatorState("ENCODING", false, new Dictionary<string, object>());

//         if (practice)
//             scriptedEventReporter.ReportScriptedEvent("stop practice deliveries");
//         else
//             scriptedEventReporter.ReportScriptedEvent("stop deliveries");
//     }

    private List<bool> GenerateBalancedHighFirstList(int numTrials)
    {
        // Half true, half false
        int half = numTrials / 2;
        List<bool> highFirstList = new List<bool>();

        for (int i = 0; i < half; i++) highFirstList.Add(true);
        for (int i = 0; i < numTrials - half; i++) highFirstList.Add(false);

        // Shuffle
        System.Random rng = new System.Random();
        for (int i = highFirstList.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            bool temp = highFirstList[i];
            highFirstList[i] = highFirstList[j];
            highFirstList[j] = temp;
        }

        return highFirstList;
    }


    private double DoCompensation()
    {
        if (!VALUE_COURIER)
            return 0.0;

        double actualAvgSum = actualAvgStorePoints.Sum();
        double guessErrorSum = 0.0;

        for (int i = 0; i < guessAvgStorePoints.Length; i++)
        {
            double error = Math.Abs(actualAvgStorePoints[i] - guessAvgStorePoints[i]);
            guessErrorSum += error;
        }

        double compensationMultiplier = 1 - (guessErrorSum / actualAvgSum);
        double compensation = compensationMultiplier * Config.maxCompensation;

        Debug.Log("Total compensation: " + compensation.ToString("C2") +
                " (Multiplier: " + compensationMultiplier.ToString("P1") + ")");

        return compensation;
    }

    private IEnumerator DoPracticeTrials(int numTrials)
    {
        Debug.Log("Practice trials");
        scriptedEventReporter.ReportScriptedEvent("start practice trials");

        starSystem.ResetSession();
        BlackScreen();

        List<bool> highFirstFlags = GenerateBalancedHighFirstList(numTrials);


        if (!HOSPITAL_COURIER)
        {
            yield return DoRecapInstructions(forceFR: true);
            messageImageDisplayer.SetGeneralMessageText(mainText: "practice invitation");
            yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_message_display);
        }
        // LC: add standard FR instruction video
        else
        {
            yield return DoVideo(LanguageSource.GetLanguageString("play movie"),
                                 LanguageSource.GetLanguageString("standard intro video"),
                                 VideoSelector.VideoType.practiceVideo);
            messageImageDisplayer.SetGeneralMessageText(mainText: "practice hospital");
            yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_message_display);
        }
        WorldScreen();

        for (int trialNumber = 0; trialNumber < numTrials; trialNumber++)
        {
            // ER instructions
            // TODO: JPB: Cleanup all the if statements in this section!
            if ((Config.efrEnabled || Config.ecrEnabled) && trialNumber == EFR_PRACTICE_TRIAL_NUM)
            {
                if (Config.twoBtnEfrEnabled || Config.twoBtnEcrEnabled)
                {
                    yield return DoVideo(LanguageSource.GetLanguageString("play movie"),
                                         LanguageSource.GetLanguageString("two btn efr intro video"),
                                         VideoSelector.VideoType.EfrIntro);
                    if (!HOSPITAL_COURIER)
                    {
                        yield return DoTwoBtnErKeypressCheck();
                        yield return DoTwoBtnErKeypressPractice();
                    }
                }
                else // One btn ER
                {
                    if (Config.efrEnabled)
                        yield return DoVideo(LanguageSource.GetLanguageString("play movie"),
                                             LanguageSource.GetLanguageString("one btn efr intro video"),
                                             VideoSelector.VideoType.efrRecapVideo);
                    if (!HOSPITAL_COURIER)
                    {
                        yield return DoOneBtnErKeypressCheck();
                        yield return DoOneBtnErKeypressPractice();
                    }

                }

                if (HOSPITAL_COURIER) // Skip the second ER practice deliv day (have it be a real deliv day)
                    break;

                messageImageDisplayer.SetGeneralMessageText(titleText: "er check understanding title",
                                                            mainText: "er check understanding main");
                yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_message_display);
            }

            // Next day message (and trial skip button)
            SetRamulatorState("WAITING", true, new Dictionary<string, object>());
            // LC: ELEMEM
            SetElememState("WAITING");
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
            if (Config.elememOn)
                elememInterface.SendTrialMessage(trialNumber, false);
#endif

            cityEnvironment.SetActive(true);
            terrain.SetActive(true);

            bool highFirst = highFirstFlags[trialNumber];
            // Do deliveries
            if (HOSPITAL_COURIER && trialNumber == 0) // Skip town learning stores in first pratice deliv days
                yield return DoDeliveries(trialNumber, trialNumber, practice: true, skipLastDelivStores: true);
            else
                yield return DoDeliveries(trialNumber, trialNumber, practice: true, highFirst: highFirst);
            // Delivery Scores : LC : now pointing feedback comes right after the delivery day
            //if (HOSPITAL_COURIER)
            //{
            //    var mtFormatValues = new string[] { starSystem.NumCorrectInSession(c => c < POINTING_CORRECT_THRESHOLD).ToString(),
            //                                        starSystem.NumInSession().ToString() };
            //    messageImageDisplayer.SetGeneralBigMessageText(titleText: "deliv day pointing accuracy title",
            //                                                   mainText: "deliv day pointing accuracy main",
            //                                                   mtFormatVals: mtFormatValues);
            //    yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);
            //}
            //// Do recalls
            if (!COURIER_ONLINE)
                yield return DoFixation(PAUSE_BEFORE_RETRIEVAL, practice: true);
            yield return DoRecall(trialNumber, trialNumber, practice: true);


        }

        if (HOSPITAL_COURIER)
            messageImageDisplayer.SetGeneralMessageText(mainText: "er check understanding main hospital",
                                                        continueText: "");
        else
            messageImageDisplayer.SetGeneralMessageText(titleText: "er check understanding title",
                                                        mainText: "er check understanding main");
        yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_message_display);

        scriptedEventReporter.ReportScriptedEvent("stop practice trials");
    }

    private IEnumerator DoTrials(int numTrials, int trialNumOffset = 0)
    {
        Debug.Log("Real trials");
        scriptedEventReporter.ReportScriptedEvent("start trials");

        List<bool> highFirstFlags = GenerateBalancedHighFirstList(numTrials);

        // randomize the order of free recall & value guess task
        if (Config.valueAlwaysFirst)
        {
            freeTaskFirst = new bool[numTrials];
            for (int i = 0; i < numTrials; i++)
            {
                freeTaskFirst[i] = false;
            }
        }
        else
        {
            freeTaskFirst = new bool[numTrials];
            for (int i = 0; i < numTrials / 2; i++)
            {
                freeTaskFirst[i] = true;
            }
            freeTaskFirst.Shuffle(new System.Random());
        }
        // ZR: enabe conditions based on config
        List<StorePointType> enabledConditions = new List<StorePointType>();
        if (Config.enableTemporal) enabledConditions.Add(StorePointType.SerialPosition);
        if (Config.enableSpatial) enabledConditions.Add(StorePointType.SpatialPosition);
        if (Config.enableRandom) enabledConditions.Add(StorePointType.Random);

        List<StorePointType> freeList = new List<StorePointType>(enabledConditions);
        List<StorePointType> valueList = new List<StorePointType>(enabledConditions);
        for (int i = 0; i < numTrials; i++)
        {
            freeList.Add(enabledConditions[i % enabledConditions.Count]);
            valueList.Add(enabledConditions[i % enabledConditions.Count]);
        }

        // LC: ELEMEM stim tag lists
        List<string> stimTagLists = GenerateStimTags(numTrials);

        // create a condition list for each task
        freeList.Shuffle(new System.Random());
        valueList.Shuffle(new System.Random());

        // int freeIndex = 0;
        // int valueIndex = 0;

        for (int trialNumber = 0; trialNumber < numTrials; trialNumber++)
        {
            starSystem.ResetSession();

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
            // LC: ELEMEM
            SetElememState("WAITING");
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
            if (Config.elememOn)
            {
                elememInterface.SendTrialMessage(continuousTrialNum, useElemem ? true : false);
                // elememInterface.SendStimSelectMessage(stimTagLists[trialNumber]);
            }
#endif

            cityEnvironment.SetActive(true);
            terrain.SetActive(true);
            bool highFirst = highFirstFlags[trialNumber];
            // LC: order of which the task appears is either forced by config or randomized
            if (Config.valueAlwaysFirst)
            {
                // value always first -> mark freeTaskFirst false for this trial
                freeTaskFirst[trialNumber] = false;
            }

            if (freeTaskFirst[trialNumber])
            {
                // LC: for each case, all 3 conditions should appear (serial, spatial, random)
                yield return DoDeliveries(trialNumber, continuousTrialNum, practice: false,
                                          storePointType: freeList[freeIndex], freeFirst: true,
                                          stimTag: stimTagLists[trialNumber], highFirst: highFirst);
                freeIndex += 1;
            }
            else
            {
                yield return DoDeliveries(trialNumber, continuousTrialNum, practice: false,
                                          storePointType: valueList[valueIndex], freeFirst: false,
                                          stimTag: stimTagLists[trialNumber], highFirst: highFirst);
                valueIndex += 1;
            }
            // Delivery Scores
            //if (HOSPITAL_COURIER)
            //{
            //    var mtFormatValues = new string[] { starSystem.NumCorrectInSession(c => c < POINTING_CORRECT_THRESHOLD).ToString(),
            //                                        starSystem.NumInSession().ToString() };
            //    messageImageDisplayer.SetGeneralBigMessageText(titleText: "deliv day pointing accuracy title",
            //                                                   mainText: "deliv day pointing accuracy main",
            //                                                   mtFormatVals: mtFormatValues);
            //    yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);
            //}
            // Do recall
            if (!COURIER_ONLINE)
                yield return DoFixation(PAUSE_BEFORE_RETRIEVAL, practice: false);
            // If valueAlwaysFirst is true, DoRecall will run Value then Free. Otherwise it will use the
            // randomized freeFirst flag we prepared above.
            yield return DoRecall(trialNumber, continuousTrialNum,
                                  practice: false, freeFirst: freeTaskFirst[trialNumber]);

            // Delivery Progress
            if (VALUE_COURIER)
            {
                int currTrial = trialNumber + 1;
                var mtFormatValues = new string[] { currTrial.ToString(), numTrials.ToString() };
                messageImageDisplayer.SetGeneralBigMessageText(titleText: "deliv day progress title",
                                                            mainText: "deliv day progress main",
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
        SetElememState("ORIENT");
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

    private IEnumerator DoTypedResponses(int trialNumber, string taskType, float taskLength, GameObject inputObject,
                                         UnityEngine.UI.InputField inputField, Action<string> onResponse, string storeName = "")
    {
        float taskStart = Time.time;
        Debug.Log("In DoTypedResponses, taskType is " + taskType + ", taskLength is " + taskLength.ToString() + ", storeName is " + storeName);
        Dictionary<string, object> taskTypeData = new Dictionary<string, object>();
        taskTypeData.Add("trial number", trialNumber);
        // if (!String.IsNullOrEmpty(store_name)) {
        //     taskTypeData.Add("store displayed", store_name);
        // }
        scriptedEventReporter.ReportScriptedEvent("start " + taskType + " typing", taskTypeData);

        // during the duration of the task...
        while (Time.time < taskStart + taskLength || taskType == "value recall")
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
                        valueGuessWrongType.SetActive(false);

                        // save & report the response
                        Dictionary<string, object> typedData = new Dictionary<string, object>();
                        typedData.Add("trial number", trialNumber);
                        typedData.Add("typed response", inputField.text);
                        scriptedEventReporter.ReportScriptedEvent(taskType, typedData);

                        onResponse?.Invoke(inputField.text);

                        // clear the field and exit the coroutine
                        inputField.Select();
                        inputField.text = "";
                        inputObject.SetActive(false);
                        yield break;
                    }
                    // tasks other than value guess should have word response
                    else
                    {
                        freeRecallWrongType.SetActive(true);
                    }
                }
                // if typed response is not numeric value...
                else
                {
                    // value guess should have numeric response
                    if (taskType == "value recall")
                    {
                        valueGuessWrongType.SetActive(true);
                    }
                    else
                    {
                        freeRecallWrongType.SetActive(false);

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

                        onResponse?.Invoke(inputField.text);

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
        freeRecallWrongType.SetActive(false);
        valueGuessWrongType.SetActive(false);
    }

    private IEnumerator DoRecall(int trialNumber, int continuousTrialNum, bool practice = false, bool freeFirst = true)
    {
        SetRamulatorState("RETRIEVAL", true, new Dictionary<string, object>());
        // LC: ELEMEM
        SetElememState("RETRIEVAL");
        // Debug.Log("In DoRecall, freeFirst is " + freeFirst.ToString());
        if (VALUE_COURIER)
        {
            if (Config.valueAlwaysFirst)
            {
                // Value always first: run value recall (indefinite) then free recall (FREE_RECALL_LENGTH)
                yield return DoValueRecall(trialNumber, practice);
                yield return DoFreeRecall(trialNumber, continuousTrialNum, practice);
            }
            else
            {
                // Use the randomized order passed in freeFirst
                if (freeFirst)
                {
                    yield return DoFreeRecall(trialNumber, continuousTrialNum, practice);
                    yield return DoValueRecall(trialNumber, practice);
                }
                else
                {
                    yield return DoValueRecall(trialNumber, practice);
                    yield return DoFreeRecall(trialNumber, continuousTrialNum, practice);
                }
            }
        }
        else
        {
            yield return DoFreeRecall(trialNumber, continuousTrialNum, practice);
            //yield return DoCuedRecall(trialNumber, continuousTrialNum, practice);
        }

        // yield return DoDistanceJudgment(trialNumber, continuousTrialNum, practice);

        SetRamulatorState("RETRIEVAL", false, new Dictionary<string, object>());
    }

    private IEnumerator DoFreeRecall(int trialNumber, int continuousTrialNum, bool practice = false)
    {
        messageImageDisplayer.SetGeneralBigMessageText("free recall title", "free recall main");
        yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);
        // ZR: MAKe sure title is shown
        //messageImageDisplayer.SetGeneralBigMessageText("free recall title", "free recall main");
        //yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);

        //if (EFR_COURIER && !practice)
        //{
        //    // LC: add reminder instructions at the beginning of every recall task
        //    if (Config.efrEnabled)
        //    {
        //        if (Config.doReject) messageImageDisplayer.SetGeneralBigMessageText(titleText: "one btn efr instructions title",
        //                                                        mainText: "one btn efr instructions main");
        //        else
        //            messageImageDisplayer.SetGeneralBigMessageText(titleText: "one btn efr instructions title",
        //                                                        mainText: "one btn efr instructions main no reject");
        //    }

        //    yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);
        //}

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

        SetElememState("RECALL", new Dictionary<string, object> { { "duration", practice ? PRACTICE_FREE_RECALL_LENGTH : FREE_RECALL_LENGTH } });

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
        {
            if (useElemem)
            {
                int iterations = (int)Math.Round(FREE_RECALL_LENGTH / (STIM_DURATION * 2));
                elememInterface.DoRepeatingStim(iterations, ELEMEM_REP_SWITCH_DELAY, ELEMEM_REP_STIM_INTERVAL);
            }
            yield return DoFreeRecallDisplay("", FREE_RECALL_LENGTH);
        }
        scriptedEventReporter.ReportScriptedEvent("object recall recording stop", recordingData);
        soundRecorder.StopRecording();
#else
            yield return DoTypedResponses(trialNumber, "free recall", FREE_RECALL_LENGTH, freeInputField, freeResponse);
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
        thisTrialPresentedStores.RemoveAt(thisTrialPresentedStores.Count - 1); // LC: remove lastly visited stores where we don't delivery item
        thisTrialPresentedStores.Shuffle(rng);
        Debug.Log(thisTrialPresentedStores);

        // TODO: JPB: Handle case for multiple practice days
        if (practice && HOSPITAL_COURIER)
        {
            scriptedEventReporter.ReportScriptedEvent("ecr cued recall video start");
            yield return DoVideo(LanguageSource.GetLanguageString("play movie"),
                                 LanguageSource.GetLanguageString("standard intro video"),
                                 VideoSelector.VideoType.ecrVideo);
            yield return DoOneBtnErKeypressCheck();
            scriptedEventReporter.ReportScriptedEvent("ecr cued recall video stop");
        }

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

        // LC: make standard cued recall ECR here
        // This is done to match the instruction video shown before.
        if (HOSPITAL_COURIER)
            practice = false;

        highBeep.Play();
        scriptedEventReporter.ReportScriptedEvent("sound played", new Dictionary<string, object>() { { "sound name", "high beep" }, { "sound duration", highBeep.clip.length.ToString() } });
        textDisplayer.DisplayText("display recall text", RECALL_TEXT);
        yield return SkippableWait(RECALL_TEXT_DISPLAY_LENGTH);
        textDisplayer.ClearText();
        foreach (Transform cueStore in thisTrialPresentedStores)
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

            string output_file_name = practice
                        ? "practice-" + continuousTrialNum.ToString() + "-" + cueStore.name
                        : continuousTrialNum.ToString() + "-" + cueStore.name;
            string output_directory = UnityEPL.GetDataPath();
            string wavFilePath = System.IO.Path.Combine(output_directory, output_file_name) + ".wav";
            string lstFilepath = System.IO.Path.Combine(output_directory, output_file_name) + ".lst";
            //AppendWordToLst(lstFilepath, cueStore.GetLastPoppedItemName()); TODO: CRB: integrate new item system
#endif

            //cueStore.familiarization_object.SetActive(true);

            Dictionary<string, object> cuedRecordingData = new Dictionary<string, object>();
            cuedRecordingData.Add("trial number", COURIER_ONLINE ? trialNumber : continuousTrialNum);
            cuedRecordingData.Add("store", cueStore.name);
            //cuedRecordingData.Add("item", cueStore.GetLastPoppedItemName());
            cuedRecordingData.Add("store position", cueStore.position.ToString());

#if !UNITY_WEBGL // Microphone
            scriptedEventReporter.ReportScriptedEvent("cued recall recording start", cuedRecordingData);
            SetElememState("RECALL", new Dictionary<string, object> { { "duration", MAX_CUED_RECALL_TIME_PER_STORE } });
            soundRecorder.StartRecording(wavFilePath);

            // Pointless - not in new courier
            //if (practice && trialNumber == 0)
            //    yield return DoCuedRecallDisplay(cueStore, "", MAX_CUED_RECALL_TIME_PER_STORE, practice: true, ecrDisabled: true, minWaitTime: MIN_CUED_RECALL_TIME_PER_STORE);
            //else if (practice)
            //    yield return DoCuedRecallDisplay(cueStore, "", MAX_CUED_RECALL_TIME_PER_STORE, practice: true, minWaitTime: MIN_CUED_RECALL_TIME_PER_STORE);
            //else
            //    yield return DoCuedRecallDisplay(cueStore, "", MAX_CUED_RECALL_TIME_PER_STORE, minWaitTime: MIN_CUED_RECALL_TIME_PER_STORE);

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

    private IEnumerator DoDistanceJudgment(int trialNumber, int continuousTrialNum, bool practice = false)
    {
        scriptedEventReporter.ReportScriptedEvent("start relative distance judgment task");
        BlackScreen();
        textDisplayer.ClearText();

        highBeep.Play();
        scriptedEventReporter.ReportScriptedEvent("sound played", new Dictionary<string, object>() { { "sound name", "high beep" }, { "sound duration", highBeep.clip.length.ToString() } });
        textDisplayer.DisplayText("display recall text", RECALL_TEXT);
        yield return SkippableWait(RECALL_TEXT_DISPLAY_LENGTH);
        textDisplayer.ClearText();



        yield return DoDistanceJudgmentDisplay();
    }

    // LC: not implemented for double session, only for single session
    private IEnumerator DoValueRecall(int trialNumber, bool practice = false)
    {
        scriptedEventReporter.ReportScriptedEvent("start value guess");
        BlackScreen();

        if (COURIER_ONLINE)
        {
            messageImageDisplayer.SetGeneralBigMessageText("value guess title", "value guess main");
            yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);
        }
        // ZR: MAKe sure value guess is shown
        messageImageDisplayer.SetGeneralBigMessageText("value guess title", "value guess main");
        yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);

#if !UNITY_WEBGL
        // TODO: implement for UNITY standalone version
        string response = null;
        yield return StartCoroutine(DoTypedResponses(trialNumber, "value recall", VALUE_RECALL_LENGTH, freeInputField, freeResponse,
            result => response = result));
#else
            string response = null;
            yield return StartCoroutine(DoTypedResponses(trialNumber, "value recall", VALUE_RECALL_LENGTH, freeInputField, freeResponse,
                result => response = result)); 
#endif

        if (double.TryParse(response, out double guessValue) && !practice)
        {
            guessAvgStorePoints[trialNumber] = guessValue;
            // Debug.Log($"Stored guess {guessValue} for trial {trialNumber}");
        }
        else
        {
            Debug.LogWarning($"Invalid typed response for trial {trialNumber}: {response} or is practice trial");
        }

        scriptedEventReporter.ReportScriptedEvent("stop value guess");
    }

    private IEnumerator DoFinalRecall(int subSessionNum)
    {
        Debug.Log("Final Recalls");
        scriptedEventReporter.ReportScriptedEvent("start final recall");

#if !UNITY_WEBGL // Microphone and System.IO
        SetRamulatorState("RETRIEVAL", true, new Dictionary<string, object>());
        // LC: ELEMEM
        SetElememState("RETRIEVAL");

        string output_directory = UnityEPL.GetDataPath();
        string output_file_name;
        string wavFilePath;
        string lstFilepath;

        if (!NICLS_COURIER)
        {
            yield return messageImageDisplayer.DisplayLanguageMessage(messageImageDisplayer.final_recall_messages);
            // LC: final store recall reminder slide
            messageImageDisplayer.SetGeneralBigMessageText("final store recall title", "final store recall main", "start");
            yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);

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
            SetElememState("RECALL", new Dictionary<string, object> { { "duration", STORE_FINAL_RECALL_LENGTH } });
            soundRecorder.StartRecording(wavFilePath);

            textDisplayer.ClearText();
            ClearTitle();

            if (useElemem)
            {
                // Elemem testing code
                // if (elememInterface == null)
                //     elememInterface = GameObject.Find("ElememInterface").GetComponent<ElememInterface>();
                //     elememInterface.elememInterfaceHelper.Start();
                //     elememInterface.elememInterfaceHelper.StartLoop();

                int iterations = (int)Math.Round(STORE_FINAL_RECALL_LENGTH / (STIM_DURATION * 2));
                // LC: we need to alternate the stim frequency. also we need to give some time buffer
                elememInterface.stimTags = GenerateStimTags(iterations);
                elememInterface.DoRepeatingSwitch(iterations, ELEMEM_REP_STIM_DELAY, ELEMEM_REP_STIM_INTERVAL);
                elememInterface.DoRepeatingStim(iterations, ELEMEM_REP_SWITCH_DELAY, ELEMEM_REP_STIM_INTERVAL);
            }
            yield return DoFreeRecallDisplay("final store recall", STORE_FINAL_RECALL_LENGTH);

            scriptedEventReporter.ReportScriptedEvent("final store recall recording stop", new Dictionary<string, object>());
            soundRecorder.StopRecording();
            textDisplayer.ClearText();
            lowBeep.Play();
            scriptedEventReporter.ReportScriptedEvent("sound played", new Dictionary<string, object>() { { "sound name", "low beep" }, { "sound duration", lowBeep.clip.length.ToString() } });

            yield return SkippableWait(TIME_BETWEEN_DIFFERENT_RECALL_PHASES);
        }

        // LC: moved this message from DoSubsession to here
        if (NICLS_COURIER)
            yield return messageImageDisplayer.DisplayLanguageMessage(messageImageDisplayer.nicls_final_recall_messages);
        else
            // LC: final object recall reminder slide
            messageImageDisplayer.SetGeneralBigMessageText("final object recall title", "final object recall main", "start");
        yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_big_message_display);

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
        SetElememState("RECALL", new Dictionary<string, object> { { "duration", OBJECT_FINAL_RECALL_LENGTH } });
        soundRecorder.StartRecording(wavFilePath);

        textDisplayer.ClearText();
        ClearTitle();

        if (useElemem)
        {
            // Elemem testing code
            // if (elememInterface == null)
            //     elememInterface = GameObject.Find("ElememInterface").GetComponent<ElememInterface>();
            //     elememInterface.elememInterfaceHelper.Start();
            //     elememInterface.elememInterfaceHelper.StartLoop();

            int iterations = (int)Math.Round(OBJECT_FINAL_RECALL_LENGTH / (STIM_DURATION * 2));
            elememInterface.stimTags = GenerateStimTags(iterations);
            elememInterface.DoRepeatingSwitch(iterations, ELEMEM_REP_STIM_DELAY, ELEMEM_REP_STIM_INTERVAL);
            elememInterface.DoRepeatingStim(iterations, ELEMEM_REP_SWITCH_DELAY, ELEMEM_REP_STIM_INTERVAL);
        }
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
#if !UNITY_WEBGL
            soundRecorder.StartRecording(wavFilePath);
#endif

            yield return DoFreeRecallDisplay("music video " + videoIndex + " recall", MUSIC_VIDEO_RECALL_TIME, efrDisabled: true);

            scriptedEventReporter.ReportScriptedEvent("music video recall recording stop", recordingData);
#if !UNITY_WEBGL
            soundRecorder.StopRecording();
#endif

            lowBeep.Play();
            scriptedEventReporter.ReportScriptedEvent("sound played", new Dictionary<string, object>() { { "sound name", "low beep" }, { "sound duration", lowBeep.clip.length.ToString() } });
        }
        scriptedEventReporter.ReportScriptedEvent("stop music video recall");
    }



    private IEnumerator DoPointingTask(StoreComponent nextStore, bool townlearning = false)
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
                           townlearning ?
                           LanguageSource.GetLanguageString("next store prompt") + "<b>" +
                           LanguageSource.GetLanguageString(nextStore.GetStoreName()) + "</b>" + ". " +
                           LanguageSource.GetLanguageString("please point") +
                           LanguageSource.GetLanguageString(nextStore.GetStoreName()) + "." + "\n\n" +
                           LanguageSource.GetLanguageString("joystick")
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
        bool improvement = starSystem.ReportScore(pointerError, 1 - wrongness);

        if (STAR_SYSTEM_ACTIVE)
            starSystem.gameObject.SetActive(true);
        yield return starSystem.ShowDifference();

        if (STAR_SYSTEM_ACTIVE)
        {
            starSystem.gameObject.SetActive(true);
            yield return starSystem.ShowDifference();
            if (improvement)
                pointerText.text = pointerText.text + "\n" + LanguageSource.GetLanguageString("rating improved");
        }

        // pointerText.text = pointerText.text + "\n" + LanguageSource.GetLanguageString("continue");
        pointerText.text = LanguageSource.GetLanguageString("continue");

        while (!InputManager.GetButtonDown("Continue"))
            yield return null;
        scriptedEventReporter.ReportScriptedEvent("pointer message cleared");
        pointerParticleSystem.Stop();
        pointer.SetActive(false);
        pointerMessage.SetActive(false);
        starSystem.gameObject.SetActive(false);
    }

    private bool lastPointingIndicatorState = false;
    private IEnumerator DisplayPointingIndicator(Transform nextStore, bool enable = false)
    {
        if (enable)
        {
            if (lastPointingIndicatorState != enable)
                scriptedEventReporter.ReportScriptedEvent("continuous pointer");
            pointer.SetActive(true);
            ColorPointer(new Color(0.5f, 0.5f, 1f));
            yield return PointArrowToStore(nextStore);
        }
        else
        {
            pointer.SetActive(false);
            yield return null;
        }
        lastPointingIndicatorState = enable;
    }

    private IEnumerator PointArrowToStore(Transform pointToZone, float arrowRotationSpeed = 0f, float arrowCorrectionTime = 0f)
    {
        //float rotationSpeed = arrowRotationSpeed == 0 ? 1f : arrowRotationSpeed * Time.deltaTime;
        //float startTime = Time.time;

        //do {
        yield return truePointer.GetComponent<Navigation>().target = pointToZone;
        //} while (Time.time < startTime + arrowCorrectionTime) ;
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


    // LC: TODO: Add Elemem stim here (alternating 3 sec, always starting off with NO_STIM)
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
                    if (title == "final store recall")
                        messageImageDisplayer.SetGeneralBiggerMessageText(titleText: "one btn er message store",
                                                                        continueText: "speak now");
                    else
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
                messageImageDisplayer.SetCuedRecallMessage("one btn ecr message", HOSPITAL_COURIER, Config.ecrEnabled);
                Func<IEnumerator> func = () =>
                {
                    return messageImageDisplayer.DisplayMessageTimedKeypressBold(
                    messageImageDisplayer.cued_recall_title, waitTime, ActionButton.RejectButton, HOSPITAL_COURIER ? "title text" : "continue text", "reject button");
                };
                messageImageDisplayer.cued_recall_message.SetActive(true);
                yield return messageImageDisplayer.DisplayMessageFunction(store.familiarization_object, func);
                messageImageDisplayer.cued_recall_message.SetActive(false);
            }
        }
        else
        {
            if (NICLS_COURIER)
            {
                messageImageDisplayer.SetCuedRecallMessage("cued recall message");
                Func<IEnumerator> func = () =>
                {
                    return messageImageDisplayer.DisplayMessageTimedKeypressBold(
                    messageImageDisplayer.cued_recall_message, waitTime, ActionButton.ContinueButton, "continue text", "continue button", true, minWaitTime);
                };
                yield return messageImageDisplayer.DisplayMessageFunction(store.familiarization_object, func);
            }
            else
            {
                messageImageDisplayer.cued_recall_title.SetActive(false);
                messageImageDisplayer.SetCuedRecallMessage("speak now", HOSPITAL_COURIER);
                Func<IEnumerator> func = () => { return messageImageDisplayer.DisplayMessageTimed(messageImageDisplayer.cued_recall_message, waitTime); };
                yield return messageImageDisplayer.DisplayMessageFunction(store.familiarization_object, func);
            }
        }

        yield return null;
    }

    private IEnumerator DoDistanceJudgmentDisplay(bool efrDisabled = false)
    {
        BlackScreen();
        //if (Config.efrEnabled && !efrDisabled)
        //{
        //    if (Config.twoBtnEfrEnabled)
        //    {
        //        SetTwoBtnErDisplay();
        //        messageImageDisplayer.SetEfrText(titleText: title);
        //        messageImageDisplayer.SetEfrElementsActive(speakNowText: true);
        //        yield return messageImageDisplayer.DisplayMessageTimedLRKeypressBold(
        //            messageImageDisplayer.efr_display, waitTime,
        //            efrLeftLogMsg, efrRightLogMsg, practice);
        //    }
        //    else // One btn EFR
        //    {
        //        if (NICLS_COURIER)
        //        {
        //            messageImageDisplayer.SetGeneralBiggerMessageText(titleText: "one btn er message",
        //                                                              continueText: "speak now");
        //            yield return messageImageDisplayer.DisplayMessageTimed(
        //                messageImageDisplayer.general_bigger_message_display, waitTime);
        //        }
        //        else
        //        {
        //            if (title == "final store recall")
        //                messageImageDisplayer.SetGeneralBiggerMessageText(titleText: "one btn er message store",
        //                                                                continueText: "speak now");
        //            else
        //                messageImageDisplayer.SetGeneralBiggerMessageText(titleText: "one btn er message",
        //                                                                continueText: "speak now");
        //            yield return messageImageDisplayer.DisplayMessageTimedKeypressBold(
        //                messageImageDisplayer.general_bigger_message_display, waitTime, ActionButton.RejectButton, "title text", "reject button");
        //        }
        //    }
        //}
        //else
        //{
        //    messageImageDisplayer.SetGeneralBiggerMessageText(continueText: "speak now");
        //    yield return messageImageDisplayer.DisplayMessageTimed(
        //        messageImageDisplayer.general_bigger_message_display, waitTime);
        //}


        yield return messageImageDisplayer.DisplayDistanceJudgmentMessage(messageImageDisplayer.distance_judgment_display, distanceItemPair, distanceZonePair);
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
        // TODO: LC: missing text here
        messageImageDisplayer.SetGeneralMessageText(mainText: "er check main");
        yield return messageImageDisplayer.DisplayMessage(messageImageDisplayer.general_message_display);

        // Ask for reject button press
        messageImageDisplayer.SetGeneralBiggerMessageText(titleText: "one btn er message", continueText: "");
        messageImageDisplayer.general_bigger_message_display.SetActive(true);

        Text toggleText = messageImageDisplayer.general_bigger_message_display.transform.Find("title text").GetComponent<Text>();

        while (!InputManager.GetButton("EfrReject"))
            yield return null;
        yield return messageImageDisplayer.DoTextBoldTimedOrButton("EfrReject", toggleText, 0.5f);
        yield return new WaitForSeconds(1.5f);
        messageImageDisplayer.general_bigger_message_display.SetActive(false);

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

    protected override void SetElememState(string stateName, Dictionary<string, object> extraData = null)
    {
#if !UNITY_WEBGL // Elemem
        if (extraData == null)
            extraData = new Dictionary<string, object>();

        if (Config.elememOn)
            elememInterface.SendStateMessage(stateName, extraData);
#endif
    }

    private void LogVersions(string expName)
    {
        string stimModeLabel = Config.elememStimMode ? "openloop" : "readonly";
        Dictionary<string, object> versionsData = new Dictionary<string, object>();
        versionsData.Add("UnityEPL version", Application.version);
        versionsData.Add("Experiment version", expName + "_" + COURIER_VERSION + "_" + stimModeLabel);
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
        cityEnvironment.SetActive(false);
        terrain.SetActive(false);
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



    private List<StoreComponent> NonVisibleStores(List<StoreComponent> stores)
    {
        return stores.Where(store => !store.IsVisible()).ToList();
    }

    private List<StoreComponent> StoresNotBehindPlayer(List<StoreComponent> stores)
    {
        return stores.Where(store =>
        {
            var player = playerMovement.gameObject.transform;
            float angle = UnityEngine.Vector3.Angle(player.forward, store.transform.position - player.position);
            return angle < 90f;
        }).ToList();
    }

    private StoreComponent PickNextStore(List<StoreComponent> stores)
    {
        if (CHOOSE_NONVISIBLE_STORES)
        {
            int NUM_CLOSE_STORES = 3;

            if (stores == null || stores.Count == 0)
                throw new ArgumentException("There are no stores in provided list");
            // Debug.Log("Unvisited Stores: " + string.Join(", ", stores));

            var tempStores = NonVisibleStores(stores);
            if (tempStores.Count == 0)
                goto PickStore;
            else
                stores = tempStores;
            // Debug.Log("NonVisible Stores: " + string.Join(", ", stores));

            tempStores = StoresNotBehindPlayer(stores);
            if (tempStores.Count == 0)
                goto PickStore;
            else
                stores = tempStores;
            // Debug.Log("Not Behind Player Stores: " + string.Join(", ", stores));

            PickStore:
            stores.Sort((store1, store2) =>
            {
                float dist1 = UnityEngine.Vector3.Distance(playerMovement.gameObject.transform.position, store1.transform.position);
                float dist2 = UnityEngine.Vector3.Distance(playerMovement.gameObject.transform.position, store2.transform.position);
                if (dist1 == dist2) return 0;
                else if (dist1 < dist2) return -1;
                else return 1;
            });

            // Debug.Log("Sorted Stores: " + string.Join(", ", stores));

            int numStoresToChooseFrom = Math.Min(NUM_CLOSE_STORES, stores.Count() - 1);
            return stores[rng.Next(numStoresToChooseFrom)];
        }
        else
        {
            //return stores[rng.Next(stores.Count() - 1)];
            return stores[0];
        }
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
        SetElememState("WAITING");

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

    private List<Transform> GetDeliveryRoute(int deliveries)
    {
        allZones.Shuffle(new System.Random());
        List<Transform> currentZones = allZones.GetRange(0, deliveries);

        if (!RANDOM_STORE_ORDER)
        {
            currentZones.Insert(0, startLocation);
            return deliveryZones.GetComponent<FindDeliveryRoute>().FindRoute(currentZones, 1);
        }
        else
        {
            deliveryZones.GetComponent<FindDeliveryRoute>().FindRoute(currentZones, 1);
            currentZones.Add(startLocation);
            return currentZones;
        }

    }

    private List<string> GetItemsList(int deliveries)
    {
        List<string> deliveryItems = new List<string>();
        List<Transform> categories = new List<Transform>();
        List<string> repItems = new List<string>();
        int numReps = deliveries / 4;  // 1/4 of locations are repeated

        foreach (Transform category in items.transform.Find("Categories"))
        {
            if (category.gameObject.activeSelf) categories.Add(category);  // Adds categories to list if activated - deactivate to prevent use
        }

        categories.Shuffle(new System.Random());

        for (int i = 0; i < deliveries - (DO_REPEATS ? numReps : 0); i++)  // Builds up item list either to number of deliveries or leaving room for repeats
        {
            List<string> list = categories[i].GetComponent<ItemsList>().itemsList;
            list.Shuffle(new System.Random());
            deliveryItems.Add(list.First());
            list.RemoveAt(0);
        }

        if (DO_REPEATS)
        {
            int n = numReps;
            while (n > 0)  // Selects random items to repeat
            {
                String repItem = deliveryItems[rng.Next(deliveries - numReps)];
                if (!repItems.Contains(repItem))
                {
                    repItems.Add(repItem);
                    deliveryItems.Add(repItem);
                    n -= 1;
                }

            }

            deliveryItems.Shuffle(new System.Random());

            var repeatItemInfo = new Dictionary<string, object>() { { "repeated items list", repItems } };

            bool sorted = false;
            while (!sorted)  // Sorts item list so repeated items are not located next to one another
            {
                sorted = true;
                for (int i = 0; i < deliveryItems.Count - 1; i++)
                {
                    if (deliveryItems[i] == deliveryItems[i + 1] || (repItems.Contains(deliveryItems[i]) && repItems.Contains(deliveryItems[i + 1])))
                    {
                        sorted = false;
                        deliveryItems.Shuffle(new System.Random());
                        break;
                    }
                }
            }
        }

        string outputText = "";
        foreach (string item in deliveryItems) outputText += item + "\n";
        Debug.Log(outputText);

        return deliveryItems;
    }

    private IEnumerator revisitStore(List<Transform> visited, Transform last, Transform next)
    {
        float proximity = 5f;  // Distance required for the revisit sequence to end 

        Transform middle = deliveryZones.GetComponent<FindDeliveryRoute>().FindMiddle(visited, last, next);
        while (UnityEngine.Vector3.Distance(middle.position, pointer.transform.position) > proximity)
        {
            yield return DisplayPointingIndicator(middle, true);
        }

        var repeatLocationInfo = new Dictionary<string, object>() { { "name", middle.name }, { "position", middle.position } };

        scriptedEventReporter.ReportScriptedEvent("Revisited prior delivery location", repeatLocationInfo);
    }

    private void EnablePlayerTransfromReporting(bool boolean)
    {
        player.GetComponent<WorldDataReporter>().isStatic = !boolean;
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

