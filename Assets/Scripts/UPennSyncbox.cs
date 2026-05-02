#if !(UNITY_WEBGL && !UNITY_EDITOR) // Syncbox
using UnityEngine;
using System.Collections;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Net.Sockets;
using System.Net.Http;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

public class UPennSyncbox : EventLoop {
//Function from Corey's Syncbox plugin (called "ASimplePlugin")
	//[DllImport ("ASimplePlugin")]
	//private static extern IntPtr OpenUSB();

	//[DllImport ("ASimplePlugin")]
	//private static extern IntPtr CloseUSB();

	//[DllImport ("ASimplePlugin")]
    //private static extern float SyncPulse();
    private TcpClient tcpClient;
    protected SyncListener listener;
    private ConcurrentQueue<string> queue;

    private const int PULSE_START_DELAY = 1000; // ms
    private const int TIME_BETWEEN_PULSES_MIN = 800;
    private const int TIME_BETWEEN_PULSES_MAX = 1200;

    private int messageTimeout = 1000;
    private int heartbeatTimeout = 8000;
    private int heartbeatCount = 0;

    private volatile bool stopped = true;

    private System.Random rnd;
    
    // from editor
    public ScriptedEventReporter scriptedInput = null;

    public UPennSyncbox(ScriptedEventReporter reporter = null) {
        scriptedInput = reporter;
        tcpClient = new TcpClient();
        listener = new SyncListener(tcpClient, scriptedInput);

        Start();
        StartLoop();
        //Connect();
    }

    protected NetworkStream GetWriteStream()
    {
        // only one writer can be active at a time
        if (tcpClient == null)
        {
            throw new InvalidOperationException("Socket not initialized.");
        }

        return tcpClient.GetStream();
    }

    private void Heartbeat(string prefix)
    {
        var data = new Dictionary<string, object>();
        data.Add("count", heartbeatCount);
        heartbeatCount++;
        SendMessage(prefix + "HEARTBEAT", data);
        WaitForMessages(new[] { prefix + "HEARTBEAT_OK" }, heartbeatTimeout);
    }

    private void DoLatencyCheck(string prefix = "")
    {
        // except if latency is unacceptable
        Stopwatch sw = new Stopwatch();
        float[] delay = new float[20];

        // send 20 heartbeats, except if max latency is out of tolerance
        for (int i = 0; i < 20; i++)
        {
            sw.Restart();
            Heartbeat(prefix);
            sw.Stop();

            // calculate manually to have sub millisecond resolution,
            // as ElapsedMilliseconds returns an integer number of
            // milliseconds.
            delay[i] = sw.ElapsedTicks * (1000f / Stopwatch.Frequency);

            if (delay[i] > 20)
            {
                throw new TimeoutException("Network Latency too large.");
            }

            // send new heartbeat every 50 ms
            Thread.Sleep(50 - (int)delay[i]);
        }

        float max = delay.Max();
        float mean = delay.Average();

        // the maximum resolution of the timer in nanoseconds
        long acc = (1000L * 1000L * 1000L) / Stopwatch.Frequency;

        Dictionary<string, object> dict = new Dictionary<string, object>();
        dict.Add("max_latency_ms", max);
        dict.Add("mean_latency_ms", mean);
        dict.Add("resolution_ns", acc);

        UnityEngine.Debug.Log(string.Join("\n", dict));
    }

    public void SendMessage(string type, Dictionary<string, object> data)
    {
        string message = type;

        Byte[] bytes = System.Text.Encoding.UTF8.GetBytes(message + "\n");

        NetworkStream stream = GetWriteStream();
        stream.Write(bytes, 0, bytes.Length);
    }

    public string WaitForMessages(string[] types, int timeout)
    {
        Stopwatch sw = new Stopwatch();
        sw.Start();

        ManualResetEventSlim wait;
        int waitDuration;
        string responseType;

        while (sw.ElapsedMilliseconds < timeout)
        {                
            if (!listener.IsListening())
            {
                listener.Listen();
            }
            wait = listener.GetListenHandle();
            waitDuration = timeout - (int)sw.ElapsedMilliseconds;
            waitDuration = waitDuration > 0 ? waitDuration : 0;
            wait.Wait(waitDuration);
            string message;
            while (queue.TryDequeue(out message))
            {
                UnityEngine.Debug.Log("gets here");
                responseType = message.Trim().Split(",")[0];
                if (types.Contains(responseType))
                {
                    return message;
                }
            }
        }

        sw.Stop();
        UnityEngine.Debug.Log("Wait for message timed out\n" + String.Join(",", types));
        throw new TimeoutException("Timed out waiting for response");
    }

    public void Connect()
    {

        try
        {
            IAsyncResult result = tcpClient.BeginConnect("127.0.0.1", 8903, null, null);
            result.AsyncWaitHandle.WaitOne(1000);
            tcpClient.EndConnect(result);
        }
        catch (SocketException ex)
        {
            UnityEngine.Debug.Log($"SocketException: {ex.Message}");
            UnityEngine.Debug.Log("Failed to connect to networked syncbox");
        }

        queue = new ConcurrentQueue<string>();
        listener.RegisterMessageQueue(queue);
        listener.Listen();

        SendMessage("NSBOPENUSB", new());
        WaitForMessages(new[] { "NSBOPENUSB_OK" }, messageTimeout);

        // excepts if there's an issue with latency, else returns
        DoLatencyCheck("NSB");

    }

    public void Disconnect()
    {
        UnityEngine.Debug.Log("Why are we here");
        if (tcpClient != null && !tcpClient.Connected)
        {
            SendMessage("NSBCLOSEUSB", new());
            WaitForMessages(new[] { "NSBCLOSEUSB_OK" }, messageTimeout);
        }
        listener.StopListening();
        listener.RemoveMessageQueue();
        tcpClient?.Close();
        tcpClient?.Dispose();
    }

    public bool Init() {
        //IntPtr ptr = OpenUSB();

        try
        {
            StopPulse();
            Connect();
            rnd = new System.Random();
            return true;
        }
        catch
        {
            return false;
        }

        //// TODO: update plugin to improve this check
        ////if(Marshal.PtrToStringAuto(ptr) != "didn't open USB...") {
        //if (connected)
        //{
        //    rnd = new System.Random();
        //    StopPulse();
        //    StartLoop();

        //    UnityEngine.Debug.Log("Successful UpennSyncbox Init");

        //    return true;
        //}
        //UnityEngine.Debug.Log("Failed UPennSyncbox Init");
        //return false;
    }

    public bool IsRunning() {
        return !stopped;
    }

    public void TestPulse() {
        if(!IsRunning()) {
            Do(new EventBase(StartPulse));
            DoIn(new EventBase(StopPulse), 8000);
        }
    }

    public void StartPulse() {
        StopPulse();
        stopped = false;
        DoIn(new EventBase(Pulse), PULSE_START_DELAY);
    }

	private void Pulse ()
    {
		if(!stopped)
        {
            UnityEngine.Debug.Log("Pew!");
            // Send a pulse
            if(scriptedInput != null)
                scriptedInput.ReportScriptedEvent("syncPulse");

            SendMessage("NSBSYNCPULSE", new());

            // Wait a random interval between min and max
            int timeBetweenPulses = (int)(TIME_BETWEEN_PULSES_MIN + (int)(rnd.NextDouble() * (TIME_BETWEEN_PULSES_MAX - TIME_BETWEEN_PULSES_MIN)));
            DoIn(new EventBase(Pulse), timeBetweenPulses);
		}
	}

    public void StopPulse() {
        StopTimers();
        stopped = true;
    }

    public void OnDisable() {
        StopPulse();
        Disconnect();
        StopLoop();
    }
}

//using UnityEngine;
//using System.Collections;
//using System;
//using System.Runtime.InteropServices;
//using System.Threading;
//using System.Net.Sockets;
//using System.Net.Http;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Linq;
//using Newtonsoft.Json.Linq;
//using System.Collections.Concurrent;
//using System.Net.NetworkInformation;
//using System.Threading.Tasks;

public class SyncListener
{
    TcpClient tcpClient;
    ScriptedEventReporter scriptedInput;
    Byte[] buffer;
    const Int32 bufferSize = 2048;

    private volatile ManualResetEventSlim callbackWaitHandle;
    private ConcurrentQueue<string> queue = null;

    string messageBuffer = "";
    public SyncListener(TcpClient _tcpClient, ScriptedEventReporter _scriptedInput)
    {
        tcpClient = _tcpClient;
        scriptedInput = _scriptedInput;
        buffer = new Byte[bufferSize];
        callbackWaitHandle = new ManualResetEventSlim(true);
    }

    public bool IsListening()
    {
        return !callbackWaitHandle.IsSet;
    }

    public ManualResetEventSlim GetListenHandle()
    {
        return callbackWaitHandle;
    }

    public void StopListening()
    {
        if (IsListening())
            callbackWaitHandle.Set();
    }

    public void RegisterMessageQueue(ConcurrentQueue<string> messages)
    {
        queue = messages;
    }

    public void RemoveMessageQueue()
    {
        queue = null;
    }

    public void Listen()
    {
        if (IsListening())
        {
            throw new AccessViolationException("Already Listening");
        }
        NetworkStream stream = tcpClient.GetStream();
        callbackWaitHandle.Reset();
        stream.BeginRead(buffer, 0, bufferSize, Callback,
                        new Tuple<NetworkStream, ManualResetEventSlim, ConcurrentQueue<string>>
                            (stream, callbackWaitHandle, queue));
    }

    private void Callback(IAsyncResult ar)
    {
        NetworkStream stream;
        ConcurrentQueue<string> queue;
        ManualResetEventSlim handle;
        int bytesRead;

        Tuple<NetworkStream, ManualResetEventSlim, ConcurrentQueue<string>> state = (Tuple<NetworkStream, ManualResetEventSlim, ConcurrentQueue<string>>)ar.AsyncState;
        stream = state.Item1;
        handle = state.Item2;
        queue = state.Item3;

        bytesRead = stream.EndRead(ar);

        foreach (string msg in ParseBuffer(bytesRead))
        {
            queue?.Enqueue(msg); // queue may be deleted by this point, if wait has ended
        }

        handle.Set();
    }

    private List<string> ParseBuffer(int bytesRead)
    {
        messageBuffer += System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
        List<string> received = new List<string>();

        while (messageBuffer.IndexOf("\n") != -1)
        {
            string message = messageBuffer.Substring(0, messageBuffer.IndexOf("\n"));
            received.Add(message);
            messageBuffer = messageBuffer.Substring(messageBuffer.IndexOf("\n") + 1);
            scriptedInput?.ReportScriptedEvent(messageBuffer.ToString());
        }
        return received;
    }
}
#endif // !(UNITY_WEBGL && !UNITY_EDITOR)
