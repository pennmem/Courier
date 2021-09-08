using UnityEngine;
using System;
using System.IO;
using System.IO.Compression;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine.Networking;

public class WriteToPsiturkHandler : WriteToDiskHandler {

    [DllImport("__Internal")]
    private static extern void SaveData();

    [DllImport("__Internal")]
    private static extern void AddData(string data);

    public override void DoWrite() {
        while (waitingPoints.Count > 0)
        {
            DataPoint dataPoint = waitingPoints.Dequeue();

            string json = dataPoint.ToJSON();
            AddData(json);
        }
        SaveData();
    }
   public static byte[] Compress(byte[] data)
    {
        MemoryStream output = new MemoryStream();
        using (GZipStream dstream = new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal))
        {
            dstream.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    public void SaveAudio(string filename, float[] audio) {
        Byte[] data = Compress(SavWav.ToWav(audio));
        UnityWebRequest www = UnityWebRequest.Put("/audio/" + filename + ".gz", data);

        StartCoroutine(_SaveAudioCoroutine(www));
    }

    private IEnumerator _SaveAudioCoroutine(UnityWebRequest www) {
        yield return www.SendWebRequest();

        if(www.isNetworkError || www.isHttpError) {
            throw new Exception("Connection to the server lost");
        }
    }
}