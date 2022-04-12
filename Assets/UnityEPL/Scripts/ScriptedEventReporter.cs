using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("UnityEPL/Reporters/Scripted Event Reporter")]
public class ScriptedEventReporter : DataReporter
{
    public ElememInterface elememInterface;

    public void ReportScriptedEvent(string type, Dictionary<string, object> dataDict = null)
    {
        if (dataDict == null)
            dataDict = new Dictionary<string, object>();
        
        // TODO: change type to all caps
        #if !UNITY_WEBGL
        if (Config.elememOn)
        {
            string elemem_type = type.ToUpper();
            elemem_type = elemem_type.Replace(' ', '_');

            if (dataDict == null)
                dataDict = new Dictionary<string, object>();

            elememInterface.SendStateMessage(elemem_type, extraData: dataDict);
        }
        #endif

        eventQueue.Enqueue(new DataPoint(type, ThreadsafeTime(), dataDict));
    }
}