using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("UnityEPL/Reporters/Scripted Event Reporter")]
public class ScriptedEventReporter : DataReporter
{
    public void ReportScriptedEvent(string type, Dictionary<string, object> dataDict = null)
    {
        if (dataDict == null)
            dataDict = new Dictionary<string, object>();
        eventQueue.Enqueue(new DataPoint(type, ThreadsafeTime(), dataDict));
    }
}