using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("UnityEPL/Handlers/Write to Disk Handler")]
public class WriteToDiskHandler : DataHandler
{
    //more output formats may be added in the future
    public enum FORMAT { JSON_LINES };
    public FORMAT outputFormat;
    public int framesPerWrite = 300;

    [HideInInspector]
    [SerializeField]
    protected bool writeAutomatically = true;

    protected System.Collections.Generic.Queue<DataPoint> waitingPoints = new System.Collections.Generic.Queue<DataPoint>();


    public void SetWriteAutomatically(bool newAutomatically)
    {
        writeAutomatically = newAutomatically;
    }
    public bool WriteAutomatically()
    {
        return writeAutomatically;
    }
    public void SetFramesPerWrite(int newFrames)
    {
        if (newFrames > 0)
            framesPerWrite = newFrames;
    }
    public int GetFramesPerWrite()
    {
        return framesPerWrite;
    }

    protected override void Update()
    {
        base.Update();

        if (Time.frameCount % framesPerWrite == 0)
            StartCoroutine(DoWrite());
    }

    protected override void HandleDataPoints(DataPoint[] dataPoints)
    {
        foreach (DataPoint dataPoint in dataPoints)
            waitingPoints.Enqueue(dataPoint);
    }

#if !UNITY_WEBGL // System.IO
    public IEnumerator DoWrite()
    {
        yield return null;
        Debug.Log("writing " + waitingPoints.Count + " json lines to file");
        while (waitingPoints.Count > 0)
        {
            string directory = UnityEPL.GetDataPath();
            System.IO.Directory.CreateDirectory(directory);
            string filePath = System.IO.Path.Combine(directory, "unnamed_file");

            DataPoint dataPoint = waitingPoints.Dequeue();
            string writeMe = "unrecognized type";
            string extensionlessFileName = "session";//DataReporter.GetStartTime ().ToString("yyyy-MM-dd HH mm ss");
            switch (outputFormat)
            {
                case FORMAT.JSON_LINES:
                    writeMe = dataPoint.ToJSON();
                    filePath = System.IO.Path.Combine(directory, extensionlessFileName + ".jsonl");
                    break;
            }
            System.IO.File.AppendAllText(filePath, writeMe + System.Environment.NewLine);
        }
        Debug.Log("wrote " + waitingPoints.Count + " json lines to file");
    }
#else
    [DllImport("__Internal")]
    private static extern void SaveData();

    [DllImport("__Internal")]
    private static extern void AddData(string data);

    public IEnumerator DoWrite()
    {
        while (waitingPoints.Count > 0)
        {
            yield return null;
            DataPoint dataPoint = waitingPoints.Dequeue();

            string json = dataPoint.ToJSON();
            // Debug.Log(json);
            AddData(json);
        }
        SaveData();
    }
#endif // !UNITY_WEBGL

#if UNITY_WEBGL // Microphone
    //public static byte[] Compress(byte[] data)
    // {
    //     MemoryStream output = new MemoryStream();
    //     using (GZipStream dstream = new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal))
    //     {
    //         dstream.Write(data, 0, data.Length);
    //     }
    //     return output.ToArray();
    // }

    // public void SaveAudio(string filename, float[] audio) {
    //     Byte[] data = Compress(SavWav.ToWav(audio));
    //     UnityWebRequest www = UnityWebRequest.Put("/audio/" + filename + ".gz", data);

    //     StartCoroutine(_SaveAudioCoroutine(www));
    // }

    // private IEnumerator _SaveAudioCoroutine(UnityWebRequest www) {
    //     yield return www.SendWebRequest();

    //     if(www.isNetworkError || www.isHttpError) {
    //         throw new Exception("Connection to the server lost");
    //     }
    // }
#endif // UNITY_WEBGL
}