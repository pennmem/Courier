
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
    const string SYSTEM_CONFIG_NAME = "config.json";

    static object configLock = new object();
    static dynamic systemConfig = null;
    static dynamic experimentConfig = null;

    public static string experimentConfigName = null;

    public static dynamic GetSystemConfig()
    {
        if (systemConfig == null)
        {
            // Setup config file
            string configPath = System.IO.Path.Combine(
                Directory.GetParent(Directory.GetParent(UnityEPL.GetParticipantFolder()).FullName).FullName,
                "configs");
            string text = File.ReadAllText(Path.Combine(configPath, SYSTEM_CONFIG_NAME));
            systemConfig = FlexibleConfig.LoadFromText(text);
        }

        return systemConfig;
    }

    public static dynamic GetExperimentConfig()
    {
        if(experimentConfig == null)
        {
            // Setup config file
            string configPath = System.IO.Path.Combine(
                Directory.GetParent(Directory.GetParent(UnityEPL.GetParticipantFolder()).FullName).FullName,
                "configs");
            string text = File.ReadAllText(Path.Combine(configPath, experimentConfigName + ".json"));
            experimentConfig = FlexibleConfig.LoadFromText(text);
        }

        return experimentConfig;
    }

    //public static dynamic GetSetting(string setting)
    //{
    //    dynamic value = null;
    //    if (value == null)
    //    value = ((IDictionary<string, dynamic>)GetSystemConfig())[setting];
        

    //    throw new MissingFieldException("Missing Setting " + setting + ".");
    //}

    //public static dynamic GetSetting(string setting)
    //{
    //    lock (configLock)
    //    {
    //        JToken value = null;

    //        if (experimentConfig != null)
    //            if (experimentConfig.TryGetValue(setting, out value))
    //                if (value != null)
    //                    return value;

    //        if (systemConfig != null)
    //            if (systemConfig.TryGetValue(setting, out value))
    //                if (value != null)
    //                    return value;

    //        throw new MissingFieldException("Missing Setting " + setting + ".");
    //    }
    //}
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
