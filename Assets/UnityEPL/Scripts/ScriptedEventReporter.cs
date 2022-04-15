using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Threading;

[AddComponentMenu("UnityEPL/Reporters/Scripted Event Reporter")]
public class ScriptedEventReporter : DataReporter
{
    // TODO: JPB: This is a hack and should be removed
    private ElememInterface elememInterface;

    public void ReportScriptedEvent(string type, Dictionary<string, object> dataDict = null, bool noNetwork = false)
    {
        if (dataDict == null)
            dataDict = new Dictionary<string, object>();

        #if !UNITY_WEBGL
        if (Config.elememOn)
        {
            if (elememInterface == null)
                elememInterface = GameObject.Find("ElememInterface").GetComponent<ElememInterface>();

            string elemem_type = type.ToUpper();
            elemem_type = elemem_type.Replace(' ', '_');

            if (dataDict == null)
                dataDict = new Dictionary<string, object>();

            if (!noNetwork)
                elememInterface.SendStateMessage(elemem_type, dataDict);
        }
        #endif

        eventQueue.Enqueue(new DataPoint(type, ThreadsafeTime(), dataDict));
    }
}