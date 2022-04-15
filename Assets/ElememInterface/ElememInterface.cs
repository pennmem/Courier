#if !UNITY_WEBGL // Elemem
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

public abstract class IHostPC : EventLoop {
    public abstract JObject WaitForMessage(string type, int timeout);
    public abstract JObject WaitForMessages(string[] types, int timeout);
    public abstract void Connect();
    public abstract void HandleMessage(string message, DateTime time);
    public abstract void SendMessage(string type, Dictionary<string, object> data);
    protected abstract void SendMessageInternal(string type, Dictionary<string, object> data);
}

public class ElememListener {
    ElememInterfaceHelper ElememInterfaceHelper;
    Byte[] buffer; 
    const Int32 bufferSize = 2048;

    private volatile ManualResetEventSlim callbackWaitHandle;
    private ConcurrentQueue<string> queue = null;

    string messageBuffer = "";
    public ElememListener(ElememInterfaceHelper _ElememInterfaceHelper) {
        ElememInterfaceHelper = _ElememInterfaceHelper;
        buffer = new Byte[bufferSize];
        callbackWaitHandle = new ManualResetEventSlim(true);
    }

    public bool IsListening() {
        return !callbackWaitHandle.IsSet;
    }

    public ManualResetEventSlim GetListenHandle() {
        return callbackWaitHandle;
    }

    public void StopListening() {
        if (IsListening())
            callbackWaitHandle.Set();
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

        NetworkStream stream = ElememInterfaceHelper.GetReadStream();
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
        while (messageBuffer.IndexOf("\n") != -1) {
            string message = messageBuffer.Substring(0, messageBuffer.IndexOf("\n") + 1);
            received.Add(message);
            messageBuffer = messageBuffer.Substring(messageBuffer.IndexOf("\n") + 1);
            
            ElememInterfaceHelper.HandleMessage(message, System.DateTime.UtcNow);
        }

        return received;
    }
}

// NOTE: the gotcha here is avoiding deadlocks when there's an error
// message in the queue and some blocking wait in the EventLoop thread
public class ElememInterfaceHelper : IHostPC 
{
    //public InterfaceManager im;

    int messageTimeout = 3000;
    int heartbeatTimeout = 8000; // TODO: configuration

    private TcpClient elememServer;
    private ElememListener listener;
    private int heartbeatCount = 0;

    private ScriptedEventReporter scriptedEventReporter;

    private bool interfaceDisabled = true;

    public ElememInterfaceHelper(ScriptedEventReporter _scriptedEventReporter, bool _interfaceDisabled) { //InterfaceManager _im) {
        //im = _im;

        interfaceDisabled = _interfaceDisabled;
        if (interfaceDisabled) return;

        scriptedEventReporter = _scriptedEventReporter;
        listener = new ElememListener(this);
        Start();
        StartLoop();
        Connect();
        //Do(new EventBase(Connect));
    }

    ~ElememInterfaceHelper() {
        elememServer.Close();
    }


    private NetworkStream GetWriteStream() {
        // TODO implement locking here
        if(elememServer == null) {
            throw new InvalidOperationException("Socket not initialized.");
        }

        return elememServer.GetStream();
    }

    // Should only be used by ElememListener
    public NetworkStream GetReadStream() {
        // TODO implement locking here
        if(elememServer == null) {
            throw new InvalidOperationException("Socket not initialized.");
        }

        return elememServer.GetStream();
    }

    public override void Connect() {
        if (interfaceDisabled) return;

        elememServer = new TcpClient();

        //try {
        IAsyncResult result = elememServer.BeginConnect(Config.elememServerIP, Config.elememServerPort, null, null);
        result.AsyncWaitHandle.WaitOne(messageTimeout);
        elememServer.EndConnect(result);
        //}
        //catch(SocketException) {    // TODO: set hostpc state on task side
        //    //im.Do(new EventBase<string>(im.SetHostPCStatus, "ERROR")); 
        //    throw new OperationCanceledException("Failed to Connect");
        //}

        //im.Do(new EventBase<string>(im.SetHostPCStatus, "INITIALIZING")); 

        UnityEngine.Debug.Log("CONNECTING");
        SendMessageInternal("CONNECTED"); // Awake
        WaitForMessage("CONNECTED_OK", messageTimeout);

        Dictionary<string, object> configDict = new Dictionary<string, object>();
        configDict.Add("stim_mode", Config.elememStimMode ? "open" : "none");
        configDict.Add("experiment", UnityEPL.GetExperimentName());
        configDict.Add("subject", UnityEPL.GetParticipants()[0]);
        configDict.Add("session", UnityEPL.GetSessionNumber().ToString());
        SendMessageInternal("CONFIGURE", configDict);
        var ElememConfig = WaitForMessage("CONFIGURE_OK", messageTimeout);
        var ElememerverConfigPath = System.IO.Path.Combine(UnityEPL.GetDataPath(), "elememServer_config.json");
        System.IO.File.AppendAllText(ElememerverConfigPath, ElememConfig.ToString());

        // excepts if there's an issue with latency, else returns
        DoLatencyCheck();

        //Do(new EventBase(RepeatedlyUpdateClassifierResult));
        //DoRepeating(new RepeatingEvent(ClassifierResult, -1, 0, 1000));

        // start heartbeats
        //int interval = (int)im.GetSetting("heartbeatInterval");
        //DoRepeating(new EventBase(Heartbeat), -1, 0, interval);

        SendMessageInternal("READY");
        WaitForMessage("START", messageTimeout);
        //im.Do(new EventBase<string>(im.SetHostPCStatus, "READY"));

        // This is after WaitForMessage until the TODO for making the WaitForMessage use the event loop
        DoRepeating(new RepeatingEvent(new EventBase(Heartbeat), -1, 0, 1000));
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
        scriptedEventReporter.ReportScriptedEvent("latency check", dict, true);
    }

    public override JObject WaitForMessage(string type, int timeout)
    {
        if (interfaceDisabled) return new JObject();

        return WaitForMessages(new[] { type }, timeout);
    }

    // TODO: JPB: Make this a helper so that it calls a Do() instead (threading issue if you wait on another event)
    public override JObject WaitForMessages(string[] types, int timeout) {
        if (interfaceDisabled) return new JObject();

        Stopwatch sw = new Stopwatch();
        sw.Start();

        ManualResetEventSlim wait;
        int waitDuration;
        ConcurrentQueue<string> queue = new ConcurrentQueue<string>();
        JObject json;

        listener.RegisterMessageQueue(queue);
        while (sw.ElapsedMilliseconds < timeout) {
            listener.Listen();
            wait = listener.GetListenHandle();
            waitDuration = timeout - (int)sw.ElapsedMilliseconds;
            waitDuration = waitDuration > 0 ? waitDuration : 0;

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

        sw.Stop();
        listener.StopListening();
        listener.RemoveMessageQueue();
        UnityEngine.Debug.Log("Wait for message timed out\n" + String.Join(",", types));
        throw new TimeoutException("Timed out waiting for response");
    }

    public override void HandleMessage(string message, DateTime time) {
        if (interfaceDisabled) return;

        JObject json = JObject.Parse(message);
        json.Add("task pc time", time);

        string type = json["type"]?.Value<string>();
        ReportMessage(json.ToString(Newtonsoft.Json.Formatting.None), false);

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


    protected override void SendMessageInternal(string type, Dictionary<string, object> data = null) {
        if (interfaceDisabled) return;

        if (data == null)
            data = new Dictionary<string, object>();

        ElememDataPoint point = new ElememDataPoint(type, System.DateTime.UtcNow, data);
        string message = point.ToJSON();

        UnityEngine.Debug.Log("Sent Message");
        UnityEngine.Debug.Log(message);

        Byte[] bytes = System.Text.Encoding.UTF8.GetBytes(message+"\n");

        NetworkStream stream = GetWriteStream();
        stream.Write(bytes, 0, bytes.Length);
        ReportMessage(message, true);
    }

    public override void SendMessage(string type, Dictionary<string, object> data = null)
    {
        if (interfaceDisabled) return;
        Do(new EventBase<string, Dictionary<string, object>>(SendMessageInternal, type, data));
    }

    private void Heartbeat()
    {
        var data = new Dictionary<string, object>();
        data.Add("count", heartbeatCount);
        heartbeatCount++;
        SendMessageInternal("HEARTBEAT", data);
        WaitForMessage("HEARTBEAT_OK", heartbeatTimeout);
    }

    private void ReportMessage(string message, bool sent)
    {
        Dictionary<string, object> messageDataDict = new Dictionary<string, object>();
        messageDataDict.Add("message", message);
        messageDataDict.Add("sent", sent.ToString());

        //scriptedEventReporter.ReportScriptedEvent("network", messageDataDict);
        //im.Do(new EventBase<string, Dictionary<string, object>, DateTime>(im.ReportEvent, "network", 
        //                        messageDataDict, System.DateTime.UtcNow));

    }
}

public class ElememInterface : MonoBehaviour
{
    //This will be updated with warnings about the status of Elemem connectivity
    public UnityEngine.UI.Text ElememWarningText;
    //This will be activated when a warning needs to be displayed
    public GameObject ElememWarning;
    //This will be used to log messages
    public ScriptedEventReporter scriptedEventReporter;

    private ElememInterfaceHelper elememInterfaceHelper = null;

    // CONNECTED, CONFIGURE, READY, and HEARTBEAT
    public IEnumerator BeginNewSession(int sessionNum, bool disableInterface = false)
    {
        yield return new WaitForSeconds(1);
        elememInterfaceHelper = new ElememInterfaceHelper(scriptedEventReporter, disableInterface);
        UnityEngine.Debug.Log("Started Elemem Interface");
    }

    // MATH
    public void SendMathMessage(string problem, string response, int responseTimeMs, bool correct)
    {
        var data = new Dictionary<string, object>();
        data.Add("problem", problem);
        data.Add("response", response);
        data.Add("response_time_ms", responseTimeMs.ToString());
        data.Add("correct", correct.ToString());
        SendMessage("MATH", data);
    }

    // STIM
    public void SendStimMessage()
    {
        SendMessage("STIM");
    }

    // CLSTIM, CLSHAM, CLNORMALIZE
    public void SendCLMessage(string type, uint classifyMs)
    {
        // TODO: JPB: Turn CLMessage "type" from string to enum
        var data = new Dictionary<string, object>();
        data.Add("classifyms", classifyMs);
        SendMessage(type, data);
    }

    // STIMSELECT
    public void SendStimSelectMessage(string tag)
    {
        var data = new Dictionary<string, object>();
        data.Add("tag", tag);
        SendMessage("STIMSELECT", data);
    }

    // SESSION
    public void SendSessionMessage(int session)
    {
        var data = new Dictionary<string, object>();
        data.Add("session", session);
        SendMessage("SESSION", data);
    }

    // NO INPUT:  REST, ORIENT, COUNTDOWN, TRIALEND, DISTRACT, INSTRUCT, WAITING, SYNC, VOCALIZATION
    // INPUT:     ISI (float duration), RECALL (float duration)
    // RAMULATOR: ENCODING, RETRIEVAL
    public void SendStateMessage(string state, Dictionary<string, object> extraData = null)
    {
        SendMessage(state, extraData);
    }

    // TRIAL
    public void SendTrialMessage(int trial, bool stim)
    {
        var data = new Dictionary<string, object>();
        data.Add("trial", trial);
        data.Add("stim", stim);
        SendMessage("TRIAL", data);
    }

    // WORD
    public void SendWordMessage(string word, int serialPos, bool stim, Dictionary<string, object> extraData = null)
    {
        var data = new Dictionary<string, object>();
        data.Add("word", word);
        data.Add("serialPos", serialPos);
        data.Add("stim", stim);
        if (extraData != null)
            foreach (string key in extraData.Keys)
                if (!data.ContainsKey(key))
                    data.Add(key, extraData[key]);
        SendMessage("WORD", data);
    }

    // LC: TODO: REPEATING STIM
    

    // EXIT
    public void SendExitMessage()
    {
        SendMessage("EXIT");
    }

    private void SendMessage(string type, Dictionary<string, object> data = null)
    {
        if (elememInterfaceHelper != null)
            elememInterfaceHelper.SendMessage(type, data);
    }
}
#endif // !UNITY_WEBGL
