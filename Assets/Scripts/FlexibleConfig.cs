
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
    public static string experimentConfigName = "EXPERIMENT_CONFIG_NAME_NOT_SET";
    public static string onlineSystemConfigText = null;
    public static string onlineExperimentConfigText = null;
    // LC: TODO: COME UP WITH A BETTER WAY
    public static bool elememStimMode = false;

    // System Settings
    public static string niclServerIP { get { return (string)Config.GetSetting("niclServerIP"); } }
    public static int niclServerPort { get { return (int)Config.GetSetting("niclServerPort"); } }
    public static string elememServerIP { get { return (string)Config.GetSetting("elememServerIP"); } }
    public static int elememServerPort { get { return (int)Config.GetSetting("elememServerPort"); } }
    public static bool elememOn { get { return (bool)Config.GetSetting("elememOn"); } }

    // Hardware
    public static bool noSyncbox { get { return (bool)Config.GetSetting("noSyncbox"); } }
    public static bool ps4Controller { get { return (bool)Config.GetSetting("ps4Contoller"); } }

    // Programmer Conveniences
    public static bool lessTrials { get { return (bool)Config.GetSetting("lessTrials"); } }
    public static bool lessDeliveries { get { return (bool)Config.GetSetting("lessDeliveries"); } }
    public static bool showFps { get { return (bool)Config.GetSetting("showFps"); } }

    // Game Section Skips
    public static bool skipIntros { get { return (bool)Config.GetSetting("skipIntros"); } }
    public static bool skipTownLearning { get { return (bool)Config.GetSetting("skipTownLearning"); } }
    public static bool skipNewEfrKeypressCheck { get { return (bool)Config.GetSetting("skipNewEfrKeypressCheck"); } }
    public static bool skipNewEfrKeypressPractice { get { return (bool)Config.GetSetting("skipNewEfrKeypressPractice"); } }

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

    // Constants
    public static int trialsPerSession { get {
            if (lessTrials) return 2;
            else return (int)Config.GetSetting("trialsPerSession"); } }
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
        var experimentConfig = GetExperimentConfig();
        if (experimentConfig.TryGetValue(setting, out value))
            return value;

        var systemConfig = GetSystemConfig();
        if (systemConfig.TryGetValue(setting, out value))
            return value;

        throw new MissingFieldException("Missing Config Setting " + setting + ".");
    }

    private static IDictionary<string, object> GetSystemConfig()
    {
        if (systemConfig == null)
        {
            // Setup config file
            #if !UNITY_WEBGL // System.IO
                string configPath = System.IO.Path.Combine(
                    Directory.GetParent(Directory.GetParent(UnityEPL.GetParticipantFolder()).FullName).FullName,
                    "configs");
                string text = File.ReadAllText(Path.Combine(configPath, SYSTEM_CONFIG_NAME));
                systemConfig = FlexibleConfig.LoadFromText(text);
            #else
                if (onlineSystemConfigText == null)
                    Debug.Log("Missing config from web");
                else
                    systemConfig = FlexibleConfig.LoadFromText(onlineSystemConfigText);
            #endif
        }
        return (IDictionary<string, object>)systemConfig;
    }

    private static IDictionary<string, object> GetExperimentConfig()
    {
        if (experimentConfig == null)
        {
            // Setup config file
            #if !UNITY_WEBGL // System.IO
                string configPath = System.IO.Path.Combine(
                    Directory.GetParent(Directory.GetParent(UnityEPL.GetParticipantFolder()).FullName).FullName,
                    "configs");
                string text = File.ReadAllText(Path.Combine(configPath, experimentConfigName + ".json"));
                experimentConfig = FlexibleConfig.LoadFromText(text);
            #else
                if (onlineExperimentConfigText == null)
                    Debug.Log("Missing config from web");
                else
                    experimentConfig = FlexibleConfig.LoadFromText(onlineExperimentConfigText);
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
