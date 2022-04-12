using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Threading;

[AddComponentMenu("UnityEPL/Reporters/Scripted Event Reporter")]
public class ScriptedEventReporter : DataReporter
{
    // TODO: JPB: This is a hack and should be removed
    public ElememInterface elememInterface;

    public void ReportScriptedEvent(string type, Dictionary<string, object> dataDict = null, bool noNetwork = false)
    {
        if (dataDict == null)
            dataDict = new Dictionary<string, object>();

        // TODO: change type to all caps
        #if !UNITY_WEBGL
        if (Config.elememOn)
        {
            //if (elememInterface == null)
            //{
            //    elememInterface = GameObject.Find("MainCoroutine").GetComponent<ElememInterface>();
            //    Debug.Log(GameObject.Find("MainCoroutine"));
            //}

            string elemem_type = type.ToUpper();
            elemem_type = elemem_type.Replace(' ', '_');

            if (dataDict == null)
                dataDict = new Dictionary<string, object>();

            Debug.Log(elememInterface != null);
            Debug.Log(elemem_type != null);
            Debug.Log(dataDict != null);

            if (!noNetwork)
                elememInterface.SendStateMessage(elemem_type, dataDict);
        }
        #endif

        eventQueue.Enqueue(new DataPoint(type, ThreadsafeTime(), dataDict));
    }
}