using UnityEngine;

#if !(UNITY_WEBGL && !UNITY_EDITOR)
using System;
using System.Runtime.InteropServices;
using System.Threading;
#endif

public class Syncbox : MonoBehaviour
{
    public ScriptedEventReporter scriptedInput = null;

#if !(UNITY_WEBGL && !UNITY_EDITOR)

    private UPennSyncbox upennSync;
    private FreiburgSyncbox freiburgSync;

    private bool isInit = false;

    public void Init()
    {
        upennSync = new UPennSyncbox(scriptedInput);
        // freiburgSync = new FreiburgSyncbox(scriptedInput);

        Debug.Log("Begin Init");

        try
        {
            if (!upennSync.Init())
            {
                Debug.Log("Invalid UPenn Handle");
                upennSync = null;
            }
            else
            {
                isInit = true;
            }
        }
        catch
        {
            Debug.Log("Failed opening UPenn Sync");
        }

        if (freiburgSync != null)
        {
            try
            {
                if (!freiburgSync.Init())
                {
                    Debug.Log("Invalid Freiburg Handle");
                    freiburgSync = null;
                }
                else
                {
                    isInit = true;
                }
            }
            catch
            {
                Debug.Log("Failed opening Freiburg sync");
            }
        }
    }

    public void StartPulse()
    {
        Debug.Log("Starting Pulses");
        upennSync?.StartPulse();
        freiburgSync?.StartPulse();
    }

    public void StopPulse()
    {
        upennSync?.StopPulse();
        freiburgSync?.StopPulse();
    }

    public void TestPulse()
    {
        Debug.Log("Testing");

        if (!isInit)
        {
            Init();
            isInit = true;
        }

        upennSync?.TestPulse();
        freiburgSync?.TestPulse();
    }

    public void OnDisable()
    {
        upennSync?.OnDisable();
        freiburgSync?.OnDisable();
    }

#else

    public void Init()
    {
        Debug.LogWarning("Syncbox.Init(): native syncbox unavailable on WebGL.");
    }

    public void StartPulse()
    {
    }

    public void StopPulse()
    {
    }

    public void TestPulse()
    {
    }

    public void OnDisable()
    {
    }

#endif
}