#if !UNITY_WEBGL
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using MonoLibUsb;

public class FreiburgSyncbox : EventLoop {
    public ScriptedEventReporter scriptedEventReporter;

	private const short FREIBURG_SYNCBOX_VENDOR_ID  = 0x0403;
	private const short FREIBURG_SYNCBOX_PRODUCT_ID = 0x6001;
	private const int FREIBURG_SYNCBOX_TIMEOUT_MS = 500;
	private const int FREIBURG_SYNCBOX_PIN_COUNT = 8;
    private const int FREIBURG_SYNCBOX_ENDPOINT = 2;
    private const int FREIBURG_SYNCBOX_INTERFACE_NUMBER = 0;

    private const int TIME_BETWEEN_PULSES_MIN = 800;
    private const int TIME_BETWEEN_PULSES_MAX = 1200;

    private MonoLibUsb.MonoUsbSessionHandle sessionHandle = new MonoUsbSessionHandle();
    private MonoLibUsb.Profile.MonoUsbProfileList profileList = null;
    private MonoLibUsb.Profile.MonoUsbProfile freiburgSyncboxProfile = null;
    private MonoLibUsb.MonoUsbDeviceHandle freiburgSyncboxDeviceHandle = null;

    private bool stopped = false;

    private System.Random rnd;

    public FreiburgSyncbox(ScriptedEventReporter reporter = null)
    {
        scriptedEventReporter = reporter;
    }

	// Use this for initialization
	public bool Init()
	{
        if(BeginFreiburgSyncSession())
        {
            rnd = new System.Random();
            StopPulse();
            StartLoop();

            Debug.Log("Successful FreiburgSyncbox Init");

            return true;
        }

        Debug.Log("Failed FreiburgSyncbox Init");
        return false;
	}

    public void TestPulse() {
        if(!IsRunning())
        {
            Do(new EventBase(StartPulse));
            DoIn(new EventBase(StopPulse), 5000);
        }
    }

    private bool BeginFreiburgSyncSession()
    {
        if (sessionHandle.IsInvalid)
        {
            // throw new ExternalException("Failed to initialize context.");
            Debug.Log("Failed to initialize context.");
            return false;
        }

        MonoUsbApi.SetDebug(sessionHandle, 0);

        profileList = new MonoLibUsb.Profile.MonoUsbProfileList();

        // The list is initially empty.
        // Each time refresh is called the list contents are updated. 
        int profileListRefreshResult;
        profileListRefreshResult = profileList.Refresh(sessionHandle);


        if (profileListRefreshResult < 0)
        {
            // throw new ExternalException("Failed to retrieve device list.");
            Debug.Log("Failed to retrieve device list.");
            return false;
        }

        Debug.Log(profileListRefreshResult.ToString() + " device(s) found.");

        // Iterate through the profile list.
        // If we find the device, write 00000000 to its endpoint 2.
        foreach (MonoLibUsb.Profile.MonoUsbProfile profile in profileList)
        {
            if (profile.DeviceDescriptor.ProductID == FREIBURG_SYNCBOX_PRODUCT_ID && profile.DeviceDescriptor.VendorID == FREIBURG_SYNCBOX_VENDOR_ID)
            {
                freiburgSyncboxProfile = profile;
            }
        }

        if (freiburgSyncboxProfile == null)
        {
            // throw new ExternalException("None of the connected USB devices were identified as a Freiburg syncbox.");
            Debug.Log("None of the connected USB devices were identified as a Freiburg syncbox.");
            return false;
        }

        freiburgSyncboxDeviceHandle = new MonoUsbDeviceHandle(freiburgSyncboxProfile.ProfileHandle);
        freiburgSyncboxDeviceHandle = freiburgSyncboxProfile.OpenDeviceHandle();
       
        if (freiburgSyncboxDeviceHandle == null)
        {
            // throw new ExternalException("The ftd USB device was found but couldn't be opened");
            Debug.Log("The ftd USB device was found but couldn't be opened");
            return false;
        }

        // StartCoroutine(FreiburgPulse());
        return true;
    }

    private void EndFreiburgSyncSession()
    {
        //These seem not to be required, and in fact cause crashes.  I'm not sure why. (Henry)
        //freiburgSyncboxDeviceHandle.Close();
        //freiburgSyncboxProfile.Close ();
        //profileList.Close();
        //sessionHandle.Close();
    }

    public void StartPulse() {
        StopPulse();
        stopped = false;
        DoIn(new EventBase(Pulse), TIME_BETWEEN_PULSES_MIN);
    }

    public void StopPulse() {
        StopTimers();
        stopped = true;
    }

    public bool IsRunning() {
        return !stopped;
    }

    private void Pulse() {
        if(!stopped)
        {
            Debug.Log("Pew!");

            int claimInterfaceResult = MonoUsbApi.ClaimInterface(freiburgSyncboxDeviceHandle, FREIBURG_SYNCBOX_INTERFACE_NUMBER);
            Debug.Log("Claimed Interface");
            int actual_length;
            int bulkTransferResult = MonoUsbApi.BulkTransfer(freiburgSyncboxDeviceHandle, FREIBURG_SYNCBOX_ENDPOINT, byte.MinValue, FREIBURG_SYNCBOX_PIN_COUNT / 8, out actual_length, FREIBURG_SYNCBOX_TIMEOUT_MS);

            if (bulkTransferResult == 0) {
                LogPulse();
            }

            Debug.Log("Sync pulse. " + actual_length.ToString() + " byte(s) written.");

            MonoUsbApi.ReleaseInterface(freiburgSyncboxDeviceHandle, FREIBURG_SYNCBOX_INTERFACE_NUMBER);

            if (claimInterfaceResult != 0 || bulkTransferResult != 0) {
                Debug.Log("Restarting sync session.");
                EndFreiburgSyncSession();
                BeginFreiburgSyncSession();
            }

            // Wait a random interval between min and max
            int timeBetweenPulses = (int)(TIME_BETWEEN_PULSES_MIN + (int)(rnd.NextDouble() * (TIME_BETWEEN_PULSES_MAX - TIME_BETWEEN_PULSES_MIN)));
            Debug.Log("Queued next Pulse");
            DoIn(new EventBase(Pulse), timeBetweenPulses);
		}
    }

    private void LogPulse()
    {
        scriptedEventReporter?.ReportScriptedEvent("syncPulse");
    }


    public void OnDisable() {
        EndFreiburgSyncSession();
        StopPulse();
        StopLoop();
    }
}
#endif