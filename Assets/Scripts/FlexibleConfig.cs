
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Converters;
using System;
using System.IO;
using System.Dynamic;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class Config
{
    public static string experimentConfigName = "ValueCourierReadOnly";
    public static string onlineSystemConfigText = null;
    public static string onlineExperimentConfigText = "ValueCourierReadOnly";
    // LC: TODO: COME UP WITH A BETTER WAY
    public static bool elememStimMode = false;

    // System Settings
    public static string niclServerIP { get { return (string)Config.GetSetting("niclServerIP"); } }
    public static int niclServerPort { get { return (int)Config.GetSetting("niclServerPort"); } }
    public static string elememServerIP { get { return (string)Config.GetSetting("elememServerIP"); } }
    public static int elememServerPort { get { return (int)Config.GetSetting("elememServerPort"); } }
    public static bool elememOn { get { return (bool)Config.GetSetting("elememOn"); } }
    public static bool freiburgSyncboxOn { get { return (bool)Config.GetSetting("freiburgSyncboxOn"); } }
    public static int freiburgSyncboxPort { get { return (int)Config.GetSetting("freiburgSyncboxPort"); } }

    // Hardware
    public static bool noSyncbox { get { return (bool)Config.GetSetting("noSyncbox"); } }
    public static bool ps4Controller { get { return (bool)Config.GetSetting("ps4Controller"); } }


    // Programmer Conveniences
    public static bool lessTrials { get { return (bool)Config.GetSetting("lessTrials"); } }
    public static bool lessDeliveries { get { return (bool)Config.GetSetting("lessDeliveries"); } }
    public static bool showFps { get { return (bool)Config.GetSetting("showFps"); } }

    // Game Section Skips
    public static bool skipIntros { get { return (bool)Config.GetSetting("skipIntros"); } }
    public static bool skipTownLearning { get { return (bool)Config.GetSetting("skipTownLearning"); } }
    public static bool skipNewEfrKeypressCheck { get { return (bool)Config.GetSetting("skipNewEfrKeypressCheck"); } }
    public static bool skipNewEfrKeypressPractice { get { return (bool)Config.GetSetting("skipNewEfrKeypressPractice"); } }
    // Pointing Indicator Trigger Options
    public static bool distTrigger { get { return (bool)Config.GetSetting("distTrigger"); } }
    public static int distThreshold { get { return (int)Config.GetSetting("distThreshold"); } }
    public static bool timeTrigger { get { return (bool)Config.GetSetting("timeTrigger"); } }
    public static int timeDelay { get { return (int)Config.GetSetting("timeDelay"); } }


    // Game Logic
    public static bool efrEnabled { get { return (bool)Config.GetSetting("efrEnabled"); } }
    public static bool ecrEnabled { get { return (bool)Config.GetSetting("ecrEnabled"); } }
    public static bool twoBtnEfrEnabled { get { return (bool)Config.GetSetting("twoBtnEfrEnabled"); } }
    public static bool twoBtnEcrEnabled { get { return (bool)Config.GetSetting("twoBtnEcrEnabled"); } }
    public static bool counterBalanceCorrectIncorrectButton { get { return (bool)Config.GetSetting("counterBalanceCorrectIncorrectButton"); } }

    public static bool temporallySmoothedTurning { get { return (bool)Config.GetSetting("temporallySmoothedTurning"); } }
    public static bool sinSmoothedTurning { get { return (bool)Config.GetSetting("sinSmoothedTurning"); } }
    public static bool cubicSmoothedTurning { get { return (bool)Config.GetSetting("cubicSmoothedTurning"); } }

    public static bool singleStickController { get { return (bool)Config.GetSetting("singleStickController"); } }

    public static bool valueAlwaysFirst { get { return (bool)Config.GetSetting("valueAlwaysFirst"); } }
    public static bool enableTemporal { get { return (bool)Config.GetSetting("enableTemporal"); } }
    public static bool enableSpatial { get { return (bool)Config.GetSetting("enableSpatial"); } }
    public static bool enableRandom { get { return (bool)Config.GetSetting("enableRandom"); } }
    public static double maxCompensation
    {
        get { return Convert.ToDouble(Config.GetSetting("maxCompensation")); }
    }

    public static int maxForwardSpeed { get { return (int)Config.GetSetting("maxForwardSpeed"); } }
    public static int maxBackwardSpeed { get { return (int)Config.GetSetting("maxBackwardSpeed"); } }
    public static int maxTurnSpeed { get { return (int)Config.GetSetting("maxTurnSpeed"); } }
    public static int primacyBuf { get { return (int)Config.GetSetting("primacyBuf"); } }
    public static int recencyBuf { get { return (int)Config.GetSetting("recencyBuf"); } }
    public static int numInGroupChosen { get { return (int)Config.GetSetting("numInGroupChosen"); } }
    public static int[] targetMean { get { return (int[])Config.GetSetting("targetMean"); } }
    public static int[] targetVar { get { return (int[])Config.GetSetting("targetVar"); } }
    public static float audioTextDisplayLength { get { return (float)Config.GetSetting("audioTextDisplayLength"); } }
    
    // public static bool valueAlwaysFirst { get { return true; } }
    // public static bool enableTemporal { get { return true; } }
    // public static bool enableSpatial { get { return false; } }
    // public static bool enableRandom { get { return true; } }

    // Constants
    public static int trialsPerSession
    {
        get
        {
            if (lessTrials) return 2;
            else return (int)Config.GetSetting("trialsPerSession");
        }
    }
    public static int trialsPerSessionSingleTownLearning { get {
            if (lessTrials) return 2;
            else return (int)Config.GetSetting("trialsPerSessionSingleTownLearning"); } }
    public static int trialsPerSessionDoubleTownLearning { get {
            if (lessTrials) return 1;
            else return (int)Config.GetSetting("trialsPerSessionDoubleTownLearning"); } }
    public static int deliveriesPerTrial { get {
            if (lessDeliveries) return 3;
            else return (int)Config.GetSetting("deliveriesPerTrial"); } }
    public static int deliveriesPerPracticeTrial { get {
            if (lessDeliveries) return 3;
            else return (int)Config.GetSetting("deliveriesPerPracticeTrial"); } }
    public static int newEfrKeypressPractices { get { return (int)Config.GetSetting("newEfrKeypressPractices"); } }

    private const string SYSTEM_CONFIG_NAME = "config.json";

    private static IDictionary<string, object> systemConfig = null;
    private static IDictionary<string, object> experimentConfig = null;


    public static T Get<T>(Func<T> getProp, T defaultValue)
    {
        try
        {
            return getProp.Invoke();
        }
        catch (MissingFieldException)
        {
            return defaultValue;
        }
    }

    // TODO: JPB: (Hokua) Should this function be templated? What are the pros and cons?
    //            Note: It could also be a "dynamic" type, but WebGL doesn't support it (so we can't use dynamic)
    //            Should it be a nullable type and remove the Get<T> function? (hint: Look up the ?? operator)
    private static object GetSetting(string setting)
    {
        object value;
        Debug.Log("[Config] GetSetting requested: '" + setting + "'");

        var expCfg = GetExperimentConfig();
        if (expCfg != null)
        {
            if (expCfg.TryGetValue(setting, out value))
                return value;
            try {
                var d = (IDictionary<string, object>)expCfg;
                Debug.Log("[Config] experimentConfig present, entries=" + d.Count + ", sample keys=" + string.Join(",", new List<string>(d.Keys).GetRange(0, Math.Min(10, d.Keys.Count))));
            } catch { /* ignore */ }
        }

        var sysCfg = GetSystemConfig();
        if (sysCfg != null)
        {
            if (sysCfg.TryGetValue(setting, out value))
                return value;
            try {
                var d = (IDictionary<string, object>)sysCfg;
                Debug.Log("[Config] systemConfig present, entries=" + d.Count + ", sample keys=" + string.Join(",", new List<string>(d.Keys).GetRange(0, Math.Min(10, d.Keys.Count))));
            } catch { /* ignore */ }
        }

        Debug.LogError("[Config] Missing setting '" + setting + "' in both experiment and system configs.");
        throw new MissingFieldException("Missing Config Setting " + setting + ".");
    }

    private static IDictionary<string, object> GetSystemConfig()
    {
        if (systemConfig == null)
        {
            #if !UNITY_WEBGL // System.IO
                try {
                    string participantFolder = UnityEPL.GetParticipantFolder();
                    string configDir = System.IO.Path.Combine(
                        Directory.GetParent(Directory.GetParent(participantFolder).FullName).FullName,
                        "configs");

                    string fullPath = Path.Combine(configDir, SYSTEM_CONFIG_NAME);
                    Debug.Log("[Config] participantFolder=" + participantFolder + ", system config path=" + fullPath + ", exists=" + System.IO.File.Exists(fullPath));

                    string text = File.ReadAllText(fullPath);
                    systemConfig = FlexibleConfig.LoadFromText(text);
                }
                catch (Exception e) {
                    Debug.LogError("[Config] Failed to load system config: " + e.Message);
                }
            #else
                if (onlineSystemConfigText == null)
                    Debug.LogWarning("[Config] Missing online system config text for WebGL");
                else
                {
                    try { systemConfig = FlexibleConfig.LoadFromText(onlineSystemConfigText); }
                    catch (Exception e) { Debug.LogError("[Config] Failed to parse online system config: " + e.Message); }
                }
            #endif
        }

        return (IDictionary<string, object>)systemConfig;
    }

    private static IDictionary<string, object> GetExperimentConfig()
    {
        if (experimentConfig == null)
        {
            #if !UNITY_WEBGL // System.IO
                try {
                    string participantFolder = UnityEPL.GetParticipantFolder();
                    string configDir = System.IO.Path.Combine(
                        Directory.GetParent(Directory.GetParent(participantFolder).FullName).FullName,
                        "configs");

                    string fullPath = Path.Combine(configDir, experimentConfigName + ".json");
                    Debug.Log("[Config] participantFolder=" + participantFolder + ", experiment config path=" + fullPath + ", exists=" + System.IO.File.Exists(fullPath));

                    string text = File.ReadAllText(fullPath);
                    experimentConfig = FlexibleConfig.LoadFromText(text);
                }
                catch (Exception e) {
                    Debug.LogError("[Config] Failed to load experiment config: " + e.Message);
                }
            #else
                if (onlineExperimentConfigText == null)
                    Debug.LogWarning("[Config] Missing online experiment config text for WebGL");
                else
                {
                    try { experimentConfig = FlexibleConfig.LoadFromText(onlineExperimentConfigText); }
                    catch (Exception e) { Debug.LogError("[Config] Failed to parse online experiment config: " + e.Message); }
                }
            #endif
        }

        return (IDictionary<string, object>)experimentConfig;
    }

    public static void SaveConfigs(ScriptedEventReporter scriptedEventReporter, string path)
    {
        if (experimentConfig != null)
        {
            if (scriptedEventReporter != null)
                scriptedEventReporter.ReportScriptedEvent("experimentConfig", new Dictionary<string, object>(experimentConfig));
                // Debug.Log("Saving experiment config to " + Path.Combine(path, experimentConfigName + ".json"));
            #if !UNITY_WEBGL // System.IO
            FlexibleConfig.WriteToText(systemConfig, Path.Combine(path, experimentConfigName + ".json"));
            #endif // !UNITY_WEBGL
        }

        if (systemConfig != null)
        {
            if (scriptedEventReporter != null)
                scriptedEventReporter.ReportScriptedEvent("systemConfig", new Dictionary<string, object>(systemConfig));
            #if !UNITY_WEBGL // System.IO
                FlexibleConfig.WriteToText(systemConfig, Path.Combine(path, SYSTEM_CONFIG_NAME));
            #endif // !UNITY_WEBGL
        }
    }

    // TODO: JPB: Refactor this to be of the singleton form (likely needs to use the new threading system)
    public static IEnumerator GetOnlineConfig()
    {
        Debug.Log("setting web request");
        string systemConfigPath = System.IO.Path.Combine(Application.streamingAssetsPath, "config.json");
        // string systemConfigPath = "http://psiturk.sas.upenn.edu:22371/static/js/Unity/build/StreamingAssets/config.json";
        UnityWebRequest systemWWW = UnityWebRequest.Get(systemConfigPath);
        yield return systemWWW.SendWebRequest();

        // TODO: LC: 
        if (systemWWW.result != UnityWebRequest.Result.Success)
        // if (systemWWW.isNetworkError || systemWWW.isHttpError)
        {
            Debug.Log("Network error " + systemWWW.error);
        }
        else
        {
            onlineSystemConfigText = systemWWW.downloadHandler.text;
            Debug.Log("Online System Config fetched!!");
            Debug.Log(onlineSystemConfigText);
        }

        string experimentConfigPath = System.IO.Path.Combine(Application.streamingAssetsPath, "CourierOnline.json");
        // string experimentConfigPath = "http://psiturk.sas.upenn.edu:22371/static/js/Unity/build/StreamingAssets/CourierOnline.json";
        UnityWebRequest experimentWWW = UnityWebRequest.Get(experimentConfigPath);
        yield return experimentWWW.SendWebRequest();

        // TODO: LC: 
        if (experimentWWW.result != UnityWebRequest.Result.Success)
        // if (experimentWWW.isNetworkError || experimentWWW.isHttpError)
        {
            Debug.Log("Network error " + experimentWWW.error);
        }
        else
        {
            onlineExperimentConfigText = experimentWWW.downloadHandler.text;
            Debug.Log("Online Experiment Config fetched!!");
            Debug.Log(Config.onlineExperimentConfigText);
        }
    }

    // Debug helpers
    public static void ForceReloadSystemConfig()
    {
        Debug.Log("ForceReloadSystemConfig called: clearing cached systemConfig");
        systemConfig = null;
    }

    public static void DumpSystemConfig()
    {
        if (systemConfig == null)
        {
            Debug.Log("DumpSystemConfig: systemConfig is null");
            return;
        }

        var dict = (IDictionary<string, object>)systemConfig;
        Debug.Log("DumpSystemConfig: systemConfig contains " + dict.Count + " entries");
        foreach (var kv in dict)
        {
            Debug.Log("  " + kv.Key + " => " + (kv.Value == null ? "null" : kv.Value.ToString()));
        }
    }

    public static void ForceReloadExperimentConfig()
    {
        Debug.Log("ForceReloadExperimentConfig called: clearing cached experimentConfig");
        experimentConfig = null;
    }

    public static void DumpExperimentConfig()
    {
        if (experimentConfig == null)
        {
            Debug.Log("DumpExperimentConfig: experimentConfig is null");
            return;
        }

        var dict = (IDictionary<string, object>)experimentConfig;
        Debug.Log("DumpExperimentConfig: experimentConfig contains " + dict.Count + " entries");
        foreach (var kv in dict)
        {
            Debug.Log("  " + kv.Key + " => " + (kv.Value == null ? "null" : kv.Value.ToString()));
        }
    }
}

public class FlexibleConfig {

    public static IDictionary<string, object> LoadFromText(string json) {
        JObject cfg = JObject.Parse(json);
        return CastToStatic(cfg);
    }

    public static void WriteToText(object data, string filename) {
        JsonSerializer serializer = new JsonSerializer();

        using (StreamWriter sw = new StreamWriter(filename))
        using (JsonWriter writer = new JsonTextWriter(sw))
        {
            serializer.Serialize(writer, data);
        }
    }

    public static IDictionary<string, object> CastToStatic(JObject cfg) {
        // casts a JObject consisting of simple types (int, bool, string,
        // float, and single dimensional arrays) to a C# expando object, obviating
        // the need for casts to work in C# native types

        object settings = new ExpandoObject();  // dynamic

        foreach(JProperty prop in cfg.Properties()) {
            // convert from JObject types to .NET internal types
            // and add to dynamic settings object
            // if JSON contains arrays, we need to peek at the
            // type of the contents to get the right cast, as
            // C# doesn't implicitly cast the contents of an
            // array when casting the array

            if(prop.Value is Newtonsoft.Json.Linq.JArray) {
                JTokenType jType = JTokenType.None;

                foreach(JToken child in prop.Value.Children()) {
                    if(jType == JTokenType.None) {
                        jType = child.Type;
                    }
                    else if (jType != child.Type) {
                        throw new Exception("Mixed type arrays not supported");     
                    }
                }

                Type cType = JTypeConversion((int)jType);
                if(cType  == typeof(string)) {
                    ((IDictionary<string, object>)settings).Add(prop.Name, prop.Value.ToObject<string[]>());
                } 
                else if(cType == typeof(int)) {
                    ((IDictionary<string, object>)settings).Add(prop.Name, prop.Value.ToObject<int[]>());
                }
                else if(cType == typeof(float)) {
                    ((IDictionary<string, object>)settings).Add(prop.Name, prop.Value.ToObject<float[]>());
                }
                else if(cType == typeof(bool)) {
                    ((IDictionary<string, object>)settings).Add(prop.Name, prop.Value.ToObject<bool[]>());
                }
            }
            else {
                Type cType = JTypeConversion((int)prop.Value.Type);
                if(cType == typeof(string)) {
                    ((IDictionary<string, object>)settings).Add(prop.Name, prop.Value.ToObject<string>());
                }
                else if(cType == typeof(int)) {
                    ((IDictionary<string, object>)settings).Add(prop.Name, prop.Value.ToObject<int>());
                }
                else if(cType == typeof(float)) {
                    ((IDictionary<string, object>)settings).Add(prop.Name, prop.Value.ToObject<float>());
                }
                else if(cType == typeof(bool)) {
                    ((IDictionary<string, object>)settings).Add(prop.Name, prop.Value.ToObject<bool>());
                }
            }
        }
        return (IDictionary<string, object>)settings;
    }

    public static Type JTypeConversion(int t) {
        switch(t) {
            case 6:
                return typeof(int);
            case 7:
                return typeof(float);
            case 8:
                return typeof(string);
            case 9: 
                return typeof(bool);
            default:
                throw new Exception("Unsupported Type");
        }
    }
}   
