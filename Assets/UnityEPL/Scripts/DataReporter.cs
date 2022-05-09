using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;

//this superclass implements an interface for retrieving behavioral events from a queue
public abstract class DataReporter : MonoBehaviour
{
    public string reportingID = "Object ID not set.";
    private static System.DateTime realWorldStartTime;
    private static System.Diagnostics.Stopwatch stopwatch;

    protected volatile static bool nativePluginRunning = false;
    private static bool startTimeInitialized = false;

    protected System.Collections.Concurrent.ConcurrentQueue<DataPoint> eventQueue = new ConcurrentQueue<DataPoint>();

    protected static double OSStartTime;
    private static float unityTimeStartTime;

    public DataHandler reportTo;
    protected Transform xform;

    // TODO: JPB: This is a hack and should be removed
    private ElememInterface elememInterface;

    protected bool IsMacOS()
    {
        return Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer;
    }

    void Awake()
    {
        if(reportingID == "Object ID not set.") {
            GenerateDefaultName();
        }

        if (!startTimeInitialized)
        {
            realWorldStartTime = System.DateTime.UtcNow;
            unityTimeStartTime = Time.realtimeSinceStartup;
            stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            startTimeInitialized = true;
        }

        if (QualitySettings.vSyncCount == 0)
            Debug.LogWarning("vSync is off!  This will cause tearing, which will prevent meaningful reporting of frame-based time data.");
    }

    protected virtual void OnEnable() {
        xform = gameObject.transform;
        if(!reportTo)  {
            GameObject data = GameObject.Find("DataCollection");
            if(data != null) {
                reportTo = (DataHandler)data.GetComponent("DataHandler");
            }
        }

        if(reportTo) {
            reportTo.AddReporter(this);
        }
    }

    protected virtual void OnDisable() 
    {
        if(reportTo) { 
            eventQueue.Enqueue(new DataPoint(reportingID + "Disabled", RealWorldFrameDisplayTime(), new Dictionary<string, object>()));
            reportTo.RemoveReporter(this);
        }
    }

    /// <summary>
    /// The number of data points currently queued in this object.
    /// 
    /// Datapoints are dequeued when read. (Usually when  handled by a DataHandler.)
    /// </summary>
    /// <returns>The data point count.</returns>
    public int UnreadDataPointCount()
    {
        return eventQueue.Count;
    }

    /// <summary>
    /// If you want to be responsible yourself for handling data points, instead of letting DataHandlers handle them, you can call this.
    /// 
    /// Read datapoints will be dequeueud and not read by other users of this object.
    /// </summary>
    /// <returns>The data points.</returns>
    /// <param name="count">How many data points to read.</param>
    public DataPoint[] ReadDataPoints(int count)
    {
        if (eventQueue.Count < count)
        {
            throw new UnityException("Not enough data points!  Check UnreadDataPointCount first.");
        }

        DataPoint[] dataPoints = new DataPoint[count];
        for (int i = 0; i < count; i++)
        {
            bool dequeued = false;
            while (!dequeued)
            {
                DataPoint readPoint;
                dequeued = eventQueue.TryDequeue(out readPoint);
                dataPoints[i] = readPoint;
            }
        }

        return dataPoints;
    }

    public void DoReport(System.Collections.Generic.Dictionary<string, object> extraData = null)
    {
        if (extraData == null)
            extraData = new System.Collections.Generic.Dictionary<string, object>();
        System.Collections.Generic.Dictionary<string, object> transformDict = new System.Collections.Generic.Dictionary<string, object>(extraData);
        transformDict.Add("positionX", xform.position.x);
        transformDict.Add("positionY", xform.position.y);
        transformDict.Add("positionZ", xform.position.z);
        transformDict.Add("rotationX", xform.rotation.eulerAngles.x);
        transformDict.Add("rotationY", xform.rotation.eulerAngles.y);
        transformDict.Add("rotationZ", xform.rotation.eulerAngles.z);
        transformDict.Add("object reporting id", reportingID);
        eventQueue.Enqueue(new DataPoint(gameObject.name + " transform", RealWorldFrameDisplayTime(), transformDict));

        #if !UNITY_WEBGL
        if (Config.elememOn)
        {
            if (elememInterface == null)
                elememInterface = GameObject.Find("ElememInterface").GetComponent<ElememInterface>();

            string type = gameObject.name + " transform";
            string elemem_type = type.ToUpper();
            elemem_type = elemem_type.Replace(' ', '_');

            if (transformDict == null)
                transformDict = new Dictionary<string, object>();

            elememInterface.SendStateMessage(elemem_type, transformDict);
        }
        #endif
    }

    protected System.DateTime RealWorldFrameDisplayTime()
    {
        double secondsSinceUnityStart = Time.unscaledTime - unityTimeStartTime;
        return GetStartTime().AddSeconds(secondsSinceUnityStart);
    }

    protected System.DateTime OSXTimestampToTimestamp(double OSXTimestamp)
    {
        double secondsSinceOSStart = OSXTimestamp - OSStartTime;
        return GetStartTime().AddSeconds(secondsSinceOSStart);
    }

    /// <summary>
    /// Returns the System.DateTime time at startup plus the (higher precision) unity time elapsed since startup, resulting in a value representing the current real world time.
    /// </summary>
    /// <returns>The real world time.</returns>
    public static System.DateTime RealWorldTime()
    {
        double secondsSinceUnityStart = Time.realtimeSinceStartup - unityTimeStartTime;
        return GetStartTime().AddSeconds(secondsSinceUnityStart);
    }

    public static System.DateTime ThreadsafeTime()
    {
        return GetStartTime().Add(stopwatch.Elapsed);
    }

    public static System.DateTime GetStartTime()
    {
        return realWorldStartTime;
    }

    private void GenerateDefaultName()
    {
        reportingID = this.name + System.Guid.NewGuid().ToString();
    }
}
