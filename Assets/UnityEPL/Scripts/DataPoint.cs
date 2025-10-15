using System.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;

//these datapoints represent behavioral events
//data about the event is currently stored in a dictionary
public class DataPoint
{
    private string type;
    private Dictionary<string, object> dataDict;
    private System.DateTime time;

    /// <summary>
    /// Initializes a new instance of the <see cref="T:DataPoint"/> class.  This represents a piece of data that you might want to keep about your project.
    /// 
    /// "Type" is a short description of the data.  Time is the time when the datapoint occured (or was collected, if it's a continuous event).
    /// 
    /// dataDict contains the actual data that you might want to analyze later.  Each element of the data is a key-value pair, the key representing its name.
    /// The value can be any C# object.  If the datapoint is written to disk using a WirteToDiskHandler, the handler will try to deduce an appropriate way of
    /// serializing the object.  This is easy for strings, integers, booleans, etc., but for other objects the object's ToString method might be used as a fallback.
    /// </summary>
    /// <param name="newType">New type.</param>
    /// <param name="newTime">New time.</param>
    /// <param name="newDataDict">New data dict.</param>
    public DataPoint(string newType, System.DateTime newTime, Dictionary<string, object> newDataDict)
    {
        if (newDataDict == null)
            newDataDict = new Dictionary<string, object>();

        type = newType;
        dataDict = newDataDict;
        time = newTime;
    }

    /// <summary>
    /// Returns a JSON string representing this datapoint.
    /// 
    /// Strings conforming to certain formats will be converted to corresponding types.  For example, if a string looks like a number it will be represented as a JSON number type. 
    /// </summary>
    /// <returns>The json.</returns>
    public string ToJSON()
    {
        double unixTimestamp = ConvertToMillisecondsSinceEpoch(time);
        string JSONString = "{\"type\":\"" + type + "\",\"data\":{";
        foreach (string key in dataDict.Keys)
            JSONString = JSONString + "\"" + key + "\":" + ValueToString(dataDict[key]) + ",";
        if (dataDict.Count > 0) // Remove the last comma
            JSONString = JSONString.Substring(0, JSONString.Length - 1);
        JSONString = JSONString + "},\"time\":" + unixTimestamp.ToString() + "}";

        if (type == "mouse position")
            Debug.Log(JSONString);

        return JSONString;

        // string JSONString = "{\"type\":\"" + type + "\",\"data\":";
        // // Debug.Log("iterating over keys");
        // JSONString = JSONString + JsonConvert.SerializeObject(dataDict) + ",\"time\":" + unixTimestamp.ToString() + "}";
        // Debug.Log(JSONString);
        // return JSONString;
    }

    public string ValueToString(object value)
    {
        // Null safety
        if (value == null)
            return "null";

        // Handle nested dictionaries
        if (value is Dictionary<string, object> dict)
        {
            var json = new System.Text.StringBuilder("{");
            bool first = true;

            foreach (var kvp in dict)
            {
                if (!first) json.Append(",");
                json.Append("\"").Append(kvp.Key).Append("\":").Append(ValueToString(kvp.Value));
                first = false;
            }

            json.Append("}");
            return json.ToString();
        }

        // Handle arrays
        if (value.GetType().IsArray)
        {
            IEnumerable enumerable = (IEnumerable)value;
            var json = new System.Text.StringBuilder("[");
            bool first = true;

            foreach (var val in enumerable)
            {
                if (!first) json.Append(",");
                json.Append(ValueToString(val));
                first = false;
            }

            json.Append("]");
            return json.ToString();
        }

        // Handle other IEnumerable (e.g., List<T>)
        if (value is IEnumerable enumerableObj)
        {
            var json = new System.Text.StringBuilder("[");
            bool first = true;

            foreach (var val in enumerableObj)
            {
                if (!first) json.Append(",");
                json.Append(ValueToString(val));
                first = false;
            }

            json.Append("]");
            return json.ToString();
        }

        // Handle numeric types
        if (IsNumeric(value))
            return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);

        // Handle booleans
        if (value is bool b)
            return b.ToString().ToLower();

        // Handle strings and chars
        if (value is string || value is char)
        {
            string s = value.ToString().Replace("\n", " ").Replace("\"", "\\\"");
            if (s.Length > 2 && s[0] == '{' && s[^1] == '}')
                return s; // treat as embedded JSON
            return $"\"{s}\"";
        }

        // Handle DateTime
        if (value is DateTime dt)
            return ConvertToMillisecondsSinceEpoch(dt).ToString();

        // Handle Unity-specific types
        if (value is UnityEngine.Vector3 v3)
            return $"\"{v3.x:F4},{v3.y:F4},{v3.z:F4}\"";
        if (value is UnityEngine.Transform t)
            return $"\"{t.name}\"";
        if (value is UnityEngine.GameObject go)
            return $"\"{go.name}\"";

        // Catch-all for primitive or unexpected simple types
        if (value.GetType().IsPrimitive)
            return value.ToString();

        // Unsupported complex types
        Debug.LogWarning($"[ValueToString] Unsupported data type: {value.GetType()}. Serializing as string.");
        return $"\"{value}\"";
    }



    // public string ValueToString(object value) {
    //     if (value is Dictionary<string, object>) // TODO: JPB: Remove
    //     {
    //         var dataDict = (Dictionary<string, object>)value;  // cast to dictionary
    //         string jsonString = "{";
    //         foreach (string key in dataDict.Keys)    
    //         {
    //             object dataVal = dataDict[key];

    //             string valueJSONString = ValueToString(dataVal);
    //             jsonString = jsonString + "\"" + key + "\":" + valueJSONString + ",";
    //         }
    //         if (dataDict.Count > 0) // Remove the last comma
    //             jsonString = jsonString.Substring(0, jsonString.Length - 1);
    //         jsonString = jsonString + "}";
    //         return jsonString;
    //     }
    //     else if(value.GetType().IsArray || value is IList)
    //     { 
    //         string jsonString = "[";
    //         var dataList = (IEnumerable<object>)value;
    //         foreach (object val in dataList) {
    //             jsonString = jsonString + ValueToString(val) + ",";
    //         }
    //         if (dataList != null && dataList.Any()) // Remove the last comma
    //             jsonString = jsonString.Substring(0, jsonString.Length - 1); // Remove last comma
    //         return jsonString + "]";
    //     }
    //     else if (IsNumeric(value)) 
    //     {
    //         return value.ToString();
    //     }
    //     else if (value is bool)
    //     {
    //         return value.ToString().ToLower();
    //     }
    //     else if (value is string) 
    //     {
    //         string valueString = (string)value.ToString().Replace("\n", " "); // clean newlines for writing to jsonl
    //         if(valueString.Length > 2 && valueString[0] == '{' && valueString[valueString.Length - 1] == '}') {
    //             return valueString; // treat as embedded JSON
    //         }
    //         else {
    //             return "\"" + valueString + "\"";
    //         }
    //     }
    //     else if (value is DateTime)
    //     {
    //         return ConvertToMillisecondsSinceEpoch((DateTime)value).ToString(); ////// cast value to DateTime
    //     }
    //     else {
    //         throw new Exception("Data logging type not supported: " + value.GetType().ToString());
    //     }
    // }

    public static double ConvertToMillisecondsSinceEpoch(System.DateTime convertMe)
    {
        double milliseconds = (double)(convertMe.ToUniversalTime().Subtract(new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc))).TotalMilliseconds;
        return milliseconds;
    }

    private static bool IsNumeric(object obj)
    {
        return (obj == null) ? false : IsNumeric(obj.GetType()); 
    }

    private static bool IsNumeric(Type type)
    {
        if (type == null)
        return false;

        TypeCode typeCode = Type.GetTypeCode(type);

        switch (typeCode)
        {
            case TypeCode.Byte:
            case TypeCode.Decimal:
            case TypeCode.Double:
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
            case TypeCode.SByte:
            case TypeCode.Single:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
            return true;
        }
        return false;
    }
}