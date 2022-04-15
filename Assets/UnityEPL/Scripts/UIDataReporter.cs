using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("UnityEPL/Reporters/UI Data Reporter")]
public class UIDataReporter : DataReporter
{

    // TODO: JPB: This is a hack and should be removed
    private ElememInterface elememInterface;

    public void LogUIEvent(string name)
    {
        eventQueue.Enqueue(new DataPoint(name, RealWorldFrameDisplayTime(), new Dictionary<string, object>()));

        #if !UNITY_WEBGL
        if (Config.elememOn)
        {
            if (elememInterface == null)
                elememInterface = GameObject.Find("ElememInterface").GetComponent<ElememInterface>();

            string type = name;
            string elemem_type = type.ToUpper();
            elemem_type = elemem_type.Replace(' ', '_');

            elememInterface.SendStateMessage(elemem_type);
        }
        #endif
    }
}