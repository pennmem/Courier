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

    public virtual IEnumerator DoWrite()
    {
        Debug.Log(waitingPoints.Count);
        while (waitingPoints.Count > 0)
        {
            yield return null;
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
        Debug.Log("Done");
    }
}