#if !UNITY_WEBGL
using System;
using System.Linq;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;
using System.Net.Sockets;
using Newtonsoft.Json.Linq;

using UnityEngine;

// TODO: class containing constructors for elemem messages

public abstract class IHostPC : EventLoop {
    public abstract JObject WaitForMessage(string type, int timeout);
    public abstract JObject WaitForMessages(string[] types, int timeout);
    public abstract void Connect();
    public abstract void HandleMessage(string message, DateTime time);
    public abstract void SendMessage(string type, Dictionary<string, object> data);
}

public class NiclsListener {
    NiclsInterfaceHelper niclsInterfaceHelper;
    Byte[] buffer; 
    const Int32 bufferSize = 2048;

    private volatile ManualResetEventSlim callbackWaitHandle;
    private ConcurrentQueue<string> queue = null;

    string messageBuffer = "";
    public NiclsListener(NiclsInterfaceHelper _niclsInterfaceHelper) {
        niclsInterfaceHelper = _niclsInterfaceHelper;
        buffer = new Byte[bufferSize];
        callbackWaitHandle = new ManualResetEventSlim(true);
    }

    public bool IsListening() {
        return !callbackWaitHandle.IsSet;
    }

    public ManualResetEventSlim GetListenHandle() {
        return callbackWaitHandle;
    }

    public void RegisterMessageQueue(ConcurrentQueue<string> messages) {
        queue = messages;
    }

    public void RemoveMessageQueue() {
        queue = null;
    }

    public void Listen() {
        if(IsListening()) {
            throw new AccessViolationException("Already Listening");
        }

        NetworkStream stream = niclsInterfaceHelper.GetReadStream();
        callbackWaitHandle.Reset();
        stream.BeginRead(buffer, 0, bufferSize, Callback, 
                        new Tuple<NetworkStream, ManualResetEventSlim, ConcurrentQueue<string>>
                            (stream, callbackWaitHandle, queue));
    } 

    private void Callback(IAsyncResult ar) {
        NetworkStream stream;
        ConcurrentQueue<string> queue;
        ManualResetEventSlim handle;
        int bytesRead;

        Tuple<NetworkStream, ManualResetEventSlim, ConcurrentQueue<string>> state = (Tuple<NetworkStream, ManualResetEventSlim, ConcurrentQueue<string>>)ar.AsyncState;
        stream = state.Item1;
        handle = state.Item2;
        queue = state.Item3;

        bytesRead = stream.EndRead(ar);

        foreach(string msg in ParseBuffer(bytesRead)) {
            queue?.Enqueue(msg); // queue may be deleted by this point, if wait has ended
        }

        handle.Set();
    }
    
    private List<string> ParseBuffer(int bytesRead) {
        messageBuffer += System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
        List<string> received = new List<string>();

        UnityEngine.Debug.Log("ParseBuffer\n" + messageBuffer.ToString());

        while(messageBuffer.IndexOf("\n") != -1) {
            string message = messageBuffer.Substring(0, messageBuffer.IndexOf("\n") + 1);
            received.Add(message);
            messageBuffer = messageBuffer.Substring(messageBuffer.IndexOf("\n") + 1);
            
            ReportMessage(message);
        }

        return received;
    }

    private void ReportMessage(string message) {
        niclsInterfaceHelper.Do(new EventBase<string, DateTime>(niclsInterfaceHelper.HandleMessage, message, System.DateTime.UtcNow));
    }
}

// NOTE: the gotcha here is avoiding deadlocks when there's an error
// message in the queue and some blocking wait in the EventLoop thread
public class NiclsInterfaceHelper : IHostPC 
{
    //public InterfaceManager im;

    int messageTimeout = 3000;
    int heartbeatTimeout = 8000; // TODO: configuration

    private TcpClient elemem;
    private NiclsListener listener;
    private int heartbeatCount = 0;

    private ScriptedEventReporter scriptedEventReporter;

    public readonly object classifierResultLock = new object();
    public volatile int classifierResult = 0;

    public NiclsInterfaceHelper(ScriptedEventReporter _scriptedEventReporter) { //InterfaceManager _im) {
        //im = _im;
        scriptedEventReporter = _scriptedEventReporter;
        listener = new NiclsListener(this);
        Start();
        StartLoop();
        Connect();
        //Do(new EventBase(Connect));
    }

    ~NiclsInterfaceHelper() {
        elemem.Close();
    }


    public NetworkStream GetWriteStream() {
        // TODO implement locking here
        if(elemem == null) {
            throw new InvalidOperationException("Socket not initialized.");
        }

        return elemem.GetStream();
    }

    public NetworkStream GetReadStream() {
        // TODO implement locking here
        if(elemem == null) {
            throw new InvalidOperationException("Socket not initialized.");
        }

        return elemem.GetStream();
    }

    public override void Connect() {
        elemem = new TcpClient();

        //try {
        IAsyncResult result = elemem.BeginConnect(Config.niclServerIP, Config.niclServerPort, null, null);
        result.AsyncWaitHandle.WaitOne(messageTimeout);
        elemem.EndConnect(result);
        //}
        //catch(SocketException) {    // TODO: set hostpc state on task side
        //    //im.Do(new EventBase<string>(im.SetHostPCStatus, "ERROR")); 
        //    throw new OperationCanceledException("Failed to Connect");
        //}

        //im.Do(new EventBase<string>(im.SetHostPCStatus, "INITIALIZING")); 

        UnityEngine.Debug.Log("CONNECTING");
        SendMessage("CONNECTED"); // Awake
        WaitForMessage("CONNECTED_OK", messageTimeout);

        Dictionary<string, object> configDict = new Dictionary<string, object>();
        //configDict.Add("stim_mode", (string)im.GetSetting("stimMode"));
        //configDict.Add("experiment", (string)im.GetSetting("experimentName"));
        //configDict.Add("subject", (string)im.GetSetting("participantCode"));
        //configDict.Add("session", (int)im.GetSetting("session"));
        SendMessage("CONFIGURE", configDict);
        WaitForMessage("CONFIGURE_OK", messageTimeout);

        // excepts if there's an issue with latency, else returns
        //DoLatencyCheck();

        // JPB: TODO: Put classifier stuff here
        Do(new EventBase(RepeatedlyUpdateClassifierResult));
        //DoRepeating(new RepeatingEvent(ClassifierResult, -1, 0, 1000));

        // start heartbeats
        //int interval = (int)im.GetSetting("heartbeatInterval");
        //DoRepeating(new EventBase(Heartbeat), -1, 0, interval);

        //SendMessage("READY", new Dictionary<string, object>());
        //im.Do(new EventBase<string>(im.SetHostPCStatus, "READY")); 
    }

    private void DoLatencyCheck() {
        // except if latency is unacceptable
        Stopwatch sw = new Stopwatch();
        float[] delay = new float[20];

        for(int i=0; i < 20; i++) {
            sw.Restart();
            Heartbeat();
            sw.Stop();

            delay[i] = sw.ElapsedTicks * (1000f / Stopwatch.Frequency);
            if(delay[i] > 20) {
                break;
            }

            Thread.Sleep(50 - (int)delay[i]);
        }
        
        float max = delay.Max();
        float mean = delay.Sum() / delay.Length;
        float acc = (1000L*1000L*1000L) / Stopwatch.Frequency;

        Dictionary<string, object> dict = new Dictionary<string, object>();
        dict.Add("max_latency", max);
        dict.Add("mean_latency", mean);
        dict.Add("accuracy", acc);

        //im.Do(new EventBase<string, Dictionary<string, object>>(im.ReportEvent, "latency check", dict));
    }

    public override JObject WaitForMessage(string type, int timeout)
    {
        return WaitForMessages(new[] { type }, timeout);
    }

    public override JObject WaitForMessages(string[] types, int timeout) {
        Stopwatch sw = new Stopwatch();
        sw.Start();

        ManualResetEventSlim wait;
        int waitDuration;
        ConcurrentQueue<string> queue = new ConcurrentQueue<string>();
        JObject json;

        listener.RegisterMessageQueue(queue);
        while(sw.ElapsedMilliseconds < timeout) {
            listener.Listen();
            wait = listener.GetListenHandle();
            waitDuration =  timeout - (int)sw.ElapsedMilliseconds;
            waitDuration =  waitDuration > 0 ? waitDuration : 0;

            wait.Wait(waitDuration);

            string message;
            while (queue.TryDequeue(out message))
            {
                json = JObject.Parse(message);
                if (types.Contains(json["type"]?.Value<string>()))
                {
                    listener.RemoveMessageQueue();
                    return json;
                }
            }
        }
        
        listener.RemoveMessageQueue();
        UnityEngine.Debug.Log("Wait for message timed out");
        throw new TimeoutException("Timed out waiting for response");
    }

    public override void HandleMessage(string message, DateTime time) {
        JObject json = JObject.Parse(message);
        json.Add("task pc time", time);

        string type = json["type"]?.Value<string>();
        ReportMessage(json.ToString(), false);

        if (type == null)
        {
            throw new Exception("Message is missing \"type\" field: " + json.ToString());
        }

        if (type.Contains("ERROR") == true) {
            throw new Exception("Error received from Host PC.");
        }
        if(type == "EXIT") {
            return;
        }

        // // start listener if not running
        // if(!listener.IsListening()) {
        //     listener.Listen();
        // }
    }

    public override void SendMessage(string type, Dictionary<string, object> data = null) {
        if (data == null)
            data = new Dictionary<string, object>();
        DataPoint point = new DataPoint(type, System.DateTime.UtcNow, data);
        string message = point.ToJSON();

        UnityEngine.Debug.Log("Sent Message");
        UnityEngine.Debug.Log(message);

        Byte[] bytes = System.Text.Encoding.UTF8.GetBytes(message+"\n");

        NetworkStream stream = GetWriteStream();
        stream.Write(bytes, 0, bytes.Length);
        ReportMessage(message, true);
    }

    private void Heartbeat()
    {
        var data = new Dictionary<string, object>();
        data.Add("count", heartbeatCount);
        heartbeatCount++;
        SendMessage("HEARTBEAT", data);
        WaitForMessages(new[] { "HEARTBEAT_OK" }, heartbeatTimeout);
    }

    public void RepeatedlyUpdateClassifierResult()
    {
        while (true)
        {
            var classifierInfo = WaitForMessages(new[] { "CLASSIFIER_RESULT", "EEG_EPOCH_END" }, 20000);
            switch(classifierInfo["type"].Value<string>())
            {
                case "CLASSIFIER_RESULT":
                    lock (classifierResultLock)
                        classifierResult = classifierInfo["data"]["result"].ToObject<int>();
                    break;
                case "EEG_EPOCH_END":
                    // Do nothing, just log the info
                    break;
            }
            
        }
    }

    private void ReportMessage(string message, bool sent)
    {
        Dictionary<string, object> messageDataDict = new Dictionary<string, object>();
        messageDataDict.Add("message", message);
        messageDataDict.Add("sent", sent.ToString());

        scriptedEventReporter.ReportScriptedEvent("network", messageDataDict);
        //im.Do(new EventBase<string, Dictionary<string, object>, DateTime>(im.ReportEvent, "network", 
        //                        messageDataDict, System.DateTime.UtcNow));

    }
}

public class NiclsInterface3 : MonoBehaviour
{
    //This will be updated with warnings about the status of nicls connectivity
    public UnityEngine.UI.Text niclsWarningText;
    //This will be activated when a warning needs to be displayed
    public GameObject niclsWarning;
    //This will be used to log messages
    public ScriptedEventReporter scriptedEventReporter;

    private NiclsInterfaceHelper niclsInterfaceHelper = null;

    private bool interfaceDisabled = false;

    public IEnumerator BeginNewSession(int sessionNum, bool disableInterface = false)
    {
        interfaceDisabled = disableInterface;
        if (interfaceDisabled)
            yield break;

        niclsInterfaceHelper = new NiclsInterfaceHelper(scriptedEventReporter);
        UnityEngine.Debug.Log("Started Nicls Interface");
    }

    public void SendEncodingToNicls(int enable)
    {
        if (interfaceDisabled) return;
        var enableDict = new Dictionary<string, object> { { "enable", enable } };
        niclsInterfaceHelper.SendMessage("ENCODING", enableDict);
    }

    public void SendReadOnlyStateToNicls(int enable)
    {
        if (interfaceDisabled) return;
        var enableDict = new Dictionary<string, object> { { "enable", enable } };
        niclsInterfaceHelper.SendMessage("READ_ONLY_STATE", enableDict);
    }

    public bool classifierInPosState()
    {
        return niclsInterfaceHelper.classifierResult == 1;
    }

    public bool classifierInNegState()
    {
        return niclsInterfaceHelper.classifierResult == 0;
    }
}
#endif // UNITY_STANDALONE