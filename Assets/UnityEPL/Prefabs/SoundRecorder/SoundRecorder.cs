using UnityEngine;

#if !(UNITY_WEBGL && !UNITY_EDITOR)
// Microphone implementation for non-WebGL builds
public class SoundRecorder : MonoBehaviour
{
    public GameObject pleaseSpeakNow;

    private AudioClip recording;
    private int startSample;
    private float startTime;
    private bool isRecording = false;
    private string nextOutputPath;

    private const int SECONDS_IN_MEMORY = 600;

    void OnEnable()
    {
        recording = Microphone.Start("", true, SECONDS_IN_MEMORY, 44100);
    }

    void OnDisable()
    {
        Microphone.End("");
    }

    public void StartRecording(string outputFilePath)
    {
        if (isRecording)
        {
            throw new UnityException("Already recording. Please StopRecording first.");
        }

        nextOutputPath = outputFilePath;
        pleaseSpeakNow.SetActive(true);
        startSample = Microphone.GetPosition("");
        startTime = Time.unscaledTime;
        isRecording = true;
    }

    public AudioClip StopRecording()
    {
        if (!isRecording)
        {
            throw new UnityException("Not recording. Please StartRecording first.");
        }

        isRecording = false;
        pleaseSpeakNow.SetActive(false);

        float recordingLength = Time.unscaledTime - startTime;
        int outputLength = Mathf.RoundToInt(44100 * recordingLength);

        AudioClip croppedClip = AudioClip.Create(
            "cropped recording",
            outputLength,
            1,
            44100,
            false
        );

        float[] saveData = new float[outputLength];

        if (startSample < recording.samples - outputLength)
        {
            recording.GetData(saveData, startSample);
        }
        else
        {
            float[] tailData = new float[recording.samples - startSample];
            recording.GetData(tailData, startSample);

            float[] headData = new float[outputLength - tailData.Length];
            recording.GetData(headData, 0);

            for (int i = 0; i < tailData.Length; i++)
            {
                saveData[i] = tailData[i];
            }

            for (int i = 0; i < headData.Length; i++)
            {
                saveData[tailData.Length + i] = headData[i];
            }
        }

        croppedClip.SetData(saveData, 0);
        SavWav.Save(nextOutputPath, croppedClip);

        return croppedClip;
    }

    public bool IsRecording()
    {
        return isRecording;
    }

    void OnApplicationQuit()
    {
        if (isRecording)
        {
            StopRecording();
        }
    }
}

#else

// WebGL stub. Unity WebGL cannot use Microphone the same way as desktop builds.
public class SoundRecorder : MonoBehaviour
{
    public GameObject pleaseSpeakNow;

    public void StartRecording(string outputFilePath)
    {
        if (pleaseSpeakNow != null)
        {
            pleaseSpeakNow.SetActive(false);
        }

        Debug.LogWarning("SoundRecorder: microphone recording is disabled in WebGL.");
    }

    public AudioClip StopRecording()
    {
        if (pleaseSpeakNow != null)
        {
            pleaseSpeakNow.SetActive(false);
        }

        Debug.LogWarning("SoundRecorder: StopRecording called in WebGL. Returning null.");
        return null;
    }

    public bool IsRecording()
    {
        return false;
    }
}

#endif