using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

//these datapoints represent behavioral events
//data about the event is currently stored in a dictionary
[JsonObject(MemberSerialization.Fields)]
public class DataPointNicls
{
    private string type;
    private Dictionary<string, object> data;
    private DateTime time;

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
    /// <param name="newData">New data.</param>
    public DataPointNicls(string newType, DateTime newTime, Dictionary<string, object> newData)
    {
        if (newData == null)
            newData = new Dictionary<string, object>();

        type = newType;
        data = newData;
        time = newTime;
    }

    public static DataPointNicls FromJsonString(String jsonString)
    {
        DataPointNicls dp = JsonConvert.DeserializeObject<DataPointNicls>(
            jsonString,
            new JsonSerializerSettings {
            Converters = { new UnixDateTimeConverter() }
        });
        //Console.WriteLine("Classfier Data Converted: {} {}", dp.type, dp.time.ToString());
        return dp;
    }

    public Dictionary<string, object> getData()
    {
        return data;
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
        foreach (string key in data.Keys)
        {
            dynamic value = data[key];

            string valueJSONString = ValueToString(value);
            JSONString = JSONString + "\"" + key + "\":" + valueJSONString + ",";
        }
        if (data.Count > 0) JSONString = JSONString.Substring(0, JSONString.Length - 1);
        JSONString = JSONString + "},\"time\":" + unixTimestamp.ToString() + "}";
        return JSONString;
    }

    public string ValueToString(dynamic value) {
        if(value.GetType().IsArray || value is IList)
        { 
            string json = "[";
            foreach (object val in (IEnumerable)value) { 
                json = json + ValueToString(val);
            }
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
        else {
            throw new Exception("Data logging type not supported");
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

public class UnixDateTimeConverter : DateTimeConverterBase
{
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        //DateTime dt = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
        //return dt.AddMilliseconds(float.Parse(reader.Value.ToString())).ToLocalTime();
        float unixTime = float.Parse(reader.Value.ToString());
        return DateTimeOffset.FromUnixTimeMilliseconds((long)unixTime).DateTime;
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        writer.WriteValue(((DateTimeOffset)value).ToUnixTimeMilliseconds());
    }
}