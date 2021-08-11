using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using NetMQ;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using System.Net.Sockets;

public class NiclsInterface2 : MonoBehaviour
{
    //This will be updated with warnings about the status of nicls connectivity
    public UnityEngine.UI.Text niclsWarningText;
    //This will be activated when a warning needs to be displayed
    public GameObject niclsWarning;
    //This will be used to log messages
    public ScriptedEventReporter scriptedEventReporter;

    //how long to wait for NICLS to connect
    const int timeoutDelay = 150;
    const int unreceivedHeartbeatsToQuit = 8;

    private int unreceivedHeartbeats = 0;

    private NetMQ.Sockets.PairSocket zmqSocket;
    private string address = "tcp://localhost:8889";

    //private NiclsEventLoop niclsEventLoop;
    private volatile int classifierResult = 0;

    private bool disabled = false;



    TcpClient tcpClient;

    const int MESSAGE_TIMEOUT = 1000;


    private void Awake()
    {
        address = "tcp://" + Config.niclServerIP + ":" + Config.niclServerPort;
        Debug.Log("NiclServer address: " + address);
    }


    void OnApplicationQuit()
    {
        if (zmqSocket != null)
        {
            zmqSocket.Close();
            NetMQConfig.Cleanup();
        }
    }

    public bool classifierReady()
    {
        return classifierResult == 1;
    }

    // -------------------------------------

    public NetworkStream GetWriteStream()
    {
        // TODO implement locking here
        if (tcpClient == null)
        {
            throw new InvalidOperationException("Socket not initialized.");
        }

        return tcpClient.GetStream();
    }

    public NetworkStream GetReadStream()
    {
        // TODO implement locking here
        if (tcpClient == null)
        {
            throw new InvalidOperationException("Socket not initialized.");
        }

        return tcpClient.GetStream();
    }

    public IEnumerator Connect()
    {
        tcpClient = new TcpClient();

        try
        {
            IAsyncResult result = tcpClient.BeginConnect(Config.niclServerIP, Config.niclServerPort, null, null);
            result.AsyncWaitHandle.WaitOne(MESSAGE_TIMEOUT);
            tcpClient.EndConnect(result);
        }
        catch (SocketException)
        {    // TODO: set hostpc state on task side
            throw new OperationCanceledException("Failed to Connect");
        }

        SendMessageToNicls("CONNECTED"); // Awake
        yield return WaitForMessage("CONNECTED_OK", "NICLS not connected.", MESSAGE_TIMEOUT);

        Dictionary<string, object> configDict = new Dictionary<string, object>();
        SendMessageToNicls("CONFIGURE");
        yield return WaitForMessage("CONFIGURE_OK", "NICLS not configured.", MESSAGE_TIMEOUT);

        // excepts if there's an issue with latency, else returns
        //DoLatencyCheck();

        InvokeRepeating("ReceiveClassifierInfo", 0, 1);
        yield return null;
    }

    // -------------------------------------

    private IEnumerator WaitForJson(string containingString, string errorMessage, int timeout = timeoutDelay)
    {
        niclsWarning.SetActive(true);
        niclsWarningText.text = "Waiting on NICLS";

        string receivedMessage = "";
        float startTime = Time.time;

        while (receivedMessage == null || !receivedMessage.Contains(containingString))
        {
            zmqSocket.TryReceiveFrameString(out receivedMessage);
            if (receivedMessage != "" && receivedMessage != null)
            {
                string messageString = receivedMessage.ToString();
                Debug.Log("received: " + messageString);
                DataPointNicls dataPoint = DataPointNicls.FromJsonString(messageString);
                ReportMessage(messageString, false);
            }

            //if we have exceeded the timeout time, show warning and stop trying to connect
            if (Time.time >= startTime + timeout)
            {
                niclsWarningText.text = errorMessage;
                Debug.LogWarning("Timed out waiting for NICLS");
                yield break;
            }
            yield return null;
        }
        niclsWarning.SetActive(false);
    }

    //this coroutine connects to NICLS and communicates how NICLS expects it to
    //in order to start the experiment session.  follow it up with BeginNewTrial and
    //SetState calls
    public IEnumerator BeginNewSession(int sessionNumber, bool disabled = false)
    {
        if (disabled) {
            this.disabled = disabled;
            yield break;
        }

        //Connect to nicls///////////////////////////////////////////////////////////////////
        zmqSocket = new NetMQ.Sockets.PairSocket();
        Debug.Log("TEST: " + address);
        zmqSocket.Connect(address);
        //Debug.Log ("socket bound");

        SendMessageToNicls("CONNECTED");
        //SendMessageToNicls(new DataPoint("CONNECTED", DataReporter.RealWorldTime(), new Dictionary<string, object>()).ToJSON());
        yield return WaitForMessage("CONNECTED", "NICLS not connected.");

        //yield return WaitForJson("CONNECTED", "NICLS not connected");

        //yield break;

        //niclsEventLoop = new NiclsEventLoop();
        //niclsEventLoop.Init();

        //SendSessionEvent//////////////////////////////////////////////////////////////////////
        //Dictionary<string, object> sessionData = new Dictionary<string, object>();
        //sessionData.Add("name", UnityEPL.GetExperimentName());
        //sessionData.Add("version", Application.version);
        //sessionData.Add("subject", UnityEPL.GetParticipants()[0]);
        //sessionData.Add("session_number", sessionNumber.ToString());
        //DataPoint sessionDataPoint = new DataPoint("SESSION", DataReporter.RealWorldTime(), sessionData);
        //SendMessageToNicls(sessionDataPoint.ToJSON());
        //Debug.Log(sessionDataPoint.ToJSON());
        //yield return null;

        SendMessageToNicls("CONFIGURE");
        //SendMessageToNicls(new DataPoint("READ_ONLY_STATE", DataReporter.RealWorldTime(), new Dictionary<string, object>()).ToJSON());
        yield return WaitForMessage("CONFIGURE", "NICLS not configured.");

        // JPB: TODO: NextDeliverable: Change this to use EventLoop system
        InvokeRepeating("ReceiveClassifierInfo", 0, 1);
        yield return null;

        //EventBase eventBase = new EventBase(WaitForMessage);
        //RepeatingEvent repeatingEvent = new RepeatingEvent(eventBase, 3, 0, 1000);
        //niclsEventLoop.DoRepeating(repeatingEvent);

        yield break;

        //Begin Heartbeats///////////////////////////////////////////////////////////////////////
        InvokeRepeating("SendHeartbeat", 0, 1);


        //SendReadyEvent////////////////////////////////////////////////////////////////////
        DataPoint ready = new DataPoint("READY", DataReporter.RealWorldTime(), new Dictionary<string, object>());
        SendMessageToNicls(ready.ToJSON());
        yield return null;


        yield return WaitForMessage("START", "Start signal not received");


        InvokeRepeating("ReceiveHeartbeat", 0, 1);

    }

    private IEnumerator WaitForMessage(string containingString, string errorMessage, int timeout = timeoutDelay)
    {
        niclsWarning.SetActive(true);
        niclsWarningText.text = "Waiting on NICLS";

        string receivedMessage = "";
        float startTime = Time.time;

        while (receivedMessage == null || !receivedMessage.Contains(containingString))
        {
            zmqSocket.TryReceiveFrameString(out receivedMessage);
            if (receivedMessage != "" && receivedMessage != null)
            {
                string messageString = receivedMessage.ToString();
                Debug.Log("received: " + messageString);
                ReportMessage(messageString, false);
            }

            //if we have exceeded the timeout time, show warning and stop trying to connect
            if (Time.time >= startTime + timeout)
            {
                niclsWarningText.text = errorMessage;
                Debug.LogWarning("Timed out waiting for NICLS");
                yield break;
            }
            yield return null;
        }
        niclsWarning.SetActive(false);
    }

    //NICLS expects this before the beginning of a new list
    public void BeginNewTrial(int trialNumber)
    {
        if (zmqSocket == null)
            throw new Exception("Please begin a session before beginning trials");
        System.Collections.Generic.Dictionary<string, object> sessionData = new Dictionary<string, object>();
        sessionData.Add("trial", trialNumber.ToString());
        DataPoint sessionDataPoint = new DataPoint("TRIAL", DataReporter.RealWorldTime(), sessionData);
        SendMessageToNicls(sessionDataPoint.ToJSON());
    }

    //NICLS expects this when you display words to the subject.
    //for words, stateName is "WORD"
    public void SetState(string stateName, bool stateToggle, System.Collections.Generic.Dictionary<string, object> sessionData)
    {
        sessionData.Add("name", stateName);
        sessionData.Add("value", stateToggle.ToString());
        DataPoint sessionDataPoint = new DataPoint("STATE", DataReporter.RealWorldTime(), sessionData);
        SendMessageToNicls(sessionDataPoint.ToJSON());
    }

    public void SendMathMessage(string problem, string response, int responseTimeMs, bool correct)
    {
        Dictionary<string, object> mathData = new Dictionary<string, object>();
        mathData.Add("problem", problem);
        mathData.Add("response", response);
        mathData.Add("response_time_ms", responseTimeMs.ToString());
        mathData.Add("correct", correct.ToString());
        DataPoint mathDataPoint = new DataPoint("MATH", DataReporter.RealWorldTime(), mathData);
        SendMessageToNicls(mathDataPoint.ToJSON());
    }


    private void SendHeartbeat()
    {
        DataPoint sessionDataPoint = new DataPoint("HEARTBEAT", DataReporter.RealWorldTime(), null);
        SendMessageToNicls(sessionDataPoint.ToJSON());
    }

    private void ReceiveHeartbeat()
    {
        unreceivedHeartbeats = unreceivedHeartbeats + 1;
        Debug.Log("Unreceived heartbeats: " + unreceivedHeartbeats.ToString());
        if (unreceivedHeartbeats > unreceivedHeartbeatsToQuit)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        string receivedMessage = "";
        float startTime = Time.time;
        zmqSocket.TryReceiveFrameString(out receivedMessage);
        if (receivedMessage != "" && receivedMessage != null)
        {
            string messageString = receivedMessage.ToString();
            Debug.Log("heartbeat received: " + messageString);
            ReportMessage(messageString, false);
            unreceivedHeartbeats = 0;
        }
    }

    private void ReceiveClassifierInfo()
    {
        string receivedMessage = "";
        float startTime = Time.time;
        zmqSocket.TryReceiveFrameString(out receivedMessage);
        if (receivedMessage != "" && receivedMessage != null)
        {
            string messageString = receivedMessage.ToString();
            //Debug.Log("classifierInfo received: " + messageString);
            classifierResult = Int32.Parse(messageString);
            //Debug.Log(classifierResult);
            // JPB: TODO: NextDeliverable: Use DataPoint for classifier info
            //DataPoint dataPoint = DataPoint.FromJsonString(messageString);
            //Dictionary<string, object> dictionary = dataPoint.getData();
            //Debug.Log("classifierInfo data: " + dataPoint.getData()["label"]);
            //foreach (KeyValuePair<string, object> kvp in dictionary)
            //{
            //    Console.WriteLine("Key = {0}, Value = {1}", kvp.Key, kvp.Value);
            //}

            ReportMessage(messageString, false);
        }
    }

    public void SendEncodingToNicls(int enable)
    {
        var enableDict = new Dictionary<string, object> { { "enable", enable } };
        var dataPointNicls = new DataPointNicls("ENCODING", DataReporter.RealWorldTime(), enableDict);
        SendMessageToNicls(dataPointNicls.ToJSON());
    }

    public void SendReadOnlyStateToNicls(int enable)
    {
        var enableDict = new Dictionary<string, object> { { "enable", enable } };
        var dataPointNicls = new DataPointNicls("READ_ONLY_STATE", DataReporter.RealWorldTime(), enableDict);
        SendMessageToNicls(dataPointNicls.ToJSON());
    }

    private void SendMessageToNicls(string message)
    {
        if (!disabled)
        {
            bool wouldNotHaveBlocked = zmqSocket.TrySendFrame(message, more: false);
            Debug.Log("Tried to send a message: " + message + " \nWouldNotHaveBlocked: " + wouldNotHaveBlocked.ToString());
            ReportMessage(message, true);
        }
    }

    private void ReportMessage(string message, bool sent)
    {
        Dictionary<string, object> messageDataDict = new Dictionary<string, object>();
        messageDataDict.Add("message", message);
        messageDataDict.Add("sent", sent.ToString());
        scriptedEventReporter.ReportScriptedEvent("network", messageDataDict);
    }
}

