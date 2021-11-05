using System.Collections;
using System;
using System.Collections.Generic;
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
        {
            object value = dataDict[key];

            string valueJSONString = ValueToString(value);
            JSONString = JSONString + "\"" + key + "\":" + valueJSONString + ",";
        }
        if (dataDict.Count > 0) // Remove the last comma
            JSONString = JSONString.Substring(0, JSONString.Length - 1);
        JSONString = JSONString + "},\"time\":" + unixTimestamp.ToString() + "}";
        return JSONString;


        // string JSONString = "{\"type\":\"" + type + "\",\"data\":";
        // // Debug.Log("iterating over keys");
        // JSONString = JSONString + JsonConvert.SerializeObject(dataDict) + ",\"time\":" + unixTimestamp.ToString() + "}";
        // Debug.Log(JSONString);
        // return JSONString;
    }

    public string ValueToString(object value) { // dynamic
        if (value is Dictionary<string, object>) // JPB: TODO: Remove
        {
            var dataDict = (Dictionary<string, object>)value;  // cast to dictionary
            string JSONString = "{";
            foreach (string key in dataDict.Keys)    
            {
                object dataVal = dataDict[key]; // dynamic

                string valueJSONString = ValueToString(dataVal);
                JSONString = JSONString + "\"" + key + "\":" + valueJSONString + ",";
            }
            if (dataDict.Count > 0) // Remove the last comma
                JSONString = JSONString.Substring(0, JSONString.Length - 1);
            JSONString = JSONString + "}";
            return JSONString;
        }
        else if(value.GetType().IsArray || value is IList)
        { 
            string json = "[";
            foreach (object val in (IEnumerable)value) { 
                json = json + ValueToString(val) + ",";  // LC: added in comma in between the values
            }
            json = json.Substring(0, json.Length - 1);   // LC: and remove the last comma
            return json + "]";
        }
        else if (IsNumeric(value)) 
        {
            return value.ToString();
        }
        else if (value is bool) //bools
        {
            return value.ToString().ToLower();
        }
        else if (value is string) 
        {
            string valueString = (string)value.ToString().Replace("\n", " "); // clean newlines for writing to jsonl
            if(valueString.Length > 2 && valueString[0] == '{' && valueString[valueString.Length - 1] == '}') {
                return valueString; // treat as embedded JSON
            }
            else {
                return "\"" + valueString + "\"";
            }
        }
        else if (value is DateTime)
        {
            return ConvertToMillisecondsSinceEpoch((DateTime)value).ToString(); ////// cast value to DateTime
        }
        else {
            throw new Exception("Data logging type not supported: " + value.GetType().ToString());
        }
    }

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