
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Converters;
using System;
using System.IO;
using System.Dynamic;
using System.Collections.Generic;
using UnityEngine;

public class Config
{
    public static string experimentConfigName = null;

    // System Settings
    public static string niclServerIP { get { return Config.GetSetting("niclServerIP"); } }
    public static int niclServerPort { get { return Config.GetSetting("niclServerPort"); } }

    // Programmer Conveniences
    public static bool noSyncbox { get { return Config.GetSetting("noSyncbox"); } }
    public static bool lessTrials { get { return Config.GetSetting("lessTrials"); } }
    public static bool lessDeliveries { get { return Config.GetSetting("lessDeliveries"); } }

    // Game Section Skips
    public static bool skipFPS { get { return Config.GetSetting("skipFPS"); } }
    public static bool skipIntros { get { return Config.GetSetting("skipIntros"); } }
    public static bool skipTownLearning { get { return Config.GetSetting("skipTownLearning"); } }
    public static bool skipNewEfrKeypressCheck { get { return Config.GetSetting("skipNewEfrKeypressCheck"); } }
    public static bool skipNewEfrKeypressPractice { get { return Config.GetSetting("skipNewEfrKeypressPractice"); } }

    // Game Logic
    public static bool efrEnabled { get { return Config.GetSetting("efrEnabled"); } }
    public static bool newEfrEnabled { get { return Config.GetSetting("newEfrEnabled"); } }
    public static bool niclsCourier { get { return Config.GetSetting("niclsCourier"); } }
    public static bool counterBalanceCorrectIncorrectButton { get { return Config.GetSetting("counterBalanceCorrectIncorrectButton"); } }

    // Constants
    public static int trialsPerSession { get {
            if (lessTrials) return 2;
            else return Config.GetSetting("trialsPerSession"); } }
    public static int trialsPerSessionSingleTownLearning { get {
            if (lessTrials) return 2;
            else return Config.GetSetting("trialsPerSessionSingleTownLearning"); } }
    public static int trialsPerSessionDoubleTownLearning { get {
            if (lessTrials) return 1;
            else return Config.GetSetting("trialsPerSessionDoubleTownLearning"); } }
    public static int deliveriesPerTrial { get {
            if (lessDeliveries) return 3;
            else return Config.GetSetting("deliveriesPerTrial"); } }
    public static int practiceDeliveriesPerTrial { get { return Config.GetSetting("practiceDeliveriesPerTrial"); } }

    public static int newEfrKeypressPractices { get { return Config.GetSetting("newEfrKeypressPractices"); } }

    private const string SYSTEM_CONFIG_NAME = "config.json";

    private static dynamic systemConfig = null;
    private static dynamic experimentConfig = null;

    private static dynamic GetSetting(string setting)
    {
        dynamic value;

        var experimentConfig = (IDictionary<string, dynamic>)GetExperimentConfig();
        if (experimentConfig.TryGetValue(setting, out value))
            return value;

        var systemConfig = (IDictionary<string, dynamic>)GetSystemConfig();
        if (systemConfig.TryGetValue(setting, out value))
            return value;

        throw new MissingFieldException("Missing Config Setting " + setting + ".");
    }

    private static dynamic GetSystemConfig()
    {
        if (systemConfig == null)
        {
            // Setup config file
            #if UNITY_STANDALONE
            string configPath = System.IO.Path.Combine(
                Directory.GetParent(Directory.GetParent(UnityEPL.GetParticipantFolder()).FullName).FullName,
                "configs");
            string text = File.ReadAllText(Path.Combine(configPath, SYSTEM_CONFIG_NAME));
            #else
            string text = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, SYSTEM_CONFIG_NAME));
            #endif

            systemConfig = FlexibleConfig.LoadFromText(text);
        }

        return systemConfig;
    }

    private static dynamic GetExperimentConfig()
    {
        if(experimentConfig == null)
        {
            // Setup config file
            #if UNITY_STANDALONE
            string configPath = System.IO.Path.Combine(
                Directory.GetParent(Directory.GetParent(UnityEPL.GetParticipantFolder()).FullName).FullName,
                "configs");
            string text = File.ReadAllText(Path.Combine(configPath, experimentConfigName + ".json"));
            #else
            string text = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, experimentConfigName + ".json"));
            #endif
            
            experimentConfig = FlexibleConfig.LoadFromText(text);
        }

        return experimentConfig;
    }
}

public class FlexibleConfig {

    public static dynamic LoadFromText(string json) {
        JObject cfg = JObject.Parse(json);
        return CastToStatic(cfg);
    }

    public static void WriteToText(dynamic data, string filename) {
    JsonSerializer serializer = new JsonSerializer();

    using (StreamWriter sw = new StreamWriter(filename))
      using (JsonWriter writer = new JsonTextWriter(sw))
      {
        serializer.Serialize(writer, data);
      }
    }

    public static dynamic CastToStatic(JObject cfg) {
        // casts a JObject consisting of simple types (int, bool, string,
        // float, and single dimensional arrays) to a C# expando object, obviating
        // the need for casts to work in C# native types

        dynamic settings = new ExpandoObject();

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
                    ((IDictionary<string, dynamic>)settings).Add(prop.Name, prop.Value.ToObject<string[]>());
                } 
                else if(cType == typeof(int)) {
                    ((IDictionary<string, dynamic>)settings).Add(prop.Name, prop.Value.ToObject<int[]>());
                }
                else if(cType == typeof(float)) {
                    ((IDictionary<string, dynamic>)settings).Add(prop.Name, prop.Value.ToObject<float[]>());
                }
                else if(cType == typeof(bool)) {
                    ((IDictionary<string, dynamic>)settings).Add(prop.Name, prop.Value.ToObject<bool[]>());
                }
            }
            else {
                Type cType = JTypeConversion((int)prop.Value.Type);
                if(cType == typeof(string)) {
                    ((IDictionary<string, dynamic>)settings).Add(prop.Name, prop.Value.ToObject<string>());
                }
                else if(cType == typeof(int)) {
                    ((IDictionary<string, dynamic>)settings).Add(prop.Name, prop.Value.ToObject<int>());
                }
                else if(cType == typeof(float)) {
                    ((IDictionary<string, dynamic>)settings).Add(prop.Name, prop.Value.ToObject<float>());
                }
                else if(cType == typeof(bool)) {
                    ((IDictionary<string, dynamic>)settings).Add(prop.Name, prop.Value.ToObject<bool>());
                }
            }
        }

        return settings;
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
