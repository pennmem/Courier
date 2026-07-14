using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Luminosity.IO;
using UnityEngine.Video;
using System.Runtime.InteropServices;

public class VideoControl : MonoBehaviour
{
    public UnityEngine.Video.VideoPlayer videoPlayer;
    public bool deactivateWhenFinished = true;

    private const float PLAYBACK_START_TIMEOUT = 15f;

    private bool isPlayingVideo;
    private bool playbackError;
    private float playRequestTime;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void LogWebAudioState(string tag);
#else
    private static void LogWebAudioState(string tag) { }
#endif

    void OnEnable()
    {
        videoPlayer.loopPointReached += OnLoopPointReached;
        videoPlayer.errorReceived += OnVideoErrorReceived;
    }

    void OnDisable()
    {
        videoPlayer.loopPointReached -= OnLoopPointReached;
        videoPlayer.errorReceived -= OnVideoErrorReceived;
    }

    void Update()
    {
        // TODO: JPB: (Hokua) Fix the video pause
        // Pause
        //if (Input.GetKeyDown(KeyCode.Space)) 
        //{
        //    if (videoPlayer.isPlaying)
        //        videoPlayer.Pause();
        //    else
        //        videoPlayer.Play();
        //}

        if (isPlayingVideo && !playbackError && !videoPlayer.isPlaying && Time.unscaledTime - playRequestTime > PLAYBACK_START_TIMEOUT)
        {
            Debug.LogWarning("VideoControl video did not start playing; continuing without blocking.");
            FinishVideo();
        }

        #if !(UNITY_WEBGL && !UNITY_EDITOR) // WebGL No Secret Key
            // Stop
            if (InputManager.GetButtonDown("Secret"))
            {
                videoPlayer.Stop();
                FinishVideo();
            }

            // Video finished
            if (videoPlayer.source == VideoSource.VideoClip &&
                videoPlayer.clip != null &&
                videoPlayer.time >= videoPlayer.clip.length)
            {
                Debug.Log("VideoControl end video");
                FinishVideo();
            }
        #endif
    }


    public IEnumerator StartVideoAndWait()
    {
        Debug.Log("[FLOW] VideoControl.StartVideoAndWait. source=" + videoPlayer.source + " clip=" + (videoPlayer.clip != null ? videoPlayer.clip.name : "<null>") + " url='" + videoPlayer.url + "'");
        playbackError = false;
        isPlayingVideo = true;
        videoPlayer.playOnAwake = false;   // we drive Play() explicitly; avoid an auto-play race on enable
        gameObject.SetActive(true);
        playRequestTime = Time.unscaledTime;   // start the Update() watchdog clock; covers Prepare + Play

        if ((videoPlayer.source == VideoSource.VideoClip && videoPlayer.clip == null) ||
            (videoPlayer.source == VideoSource.Url && string.IsNullOrEmpty(videoPlayer.url)))
        {
            Debug.LogWarning("[FLOW] VideoControl StartVideoAndWait called without a configured clip or URL; skipping video.");
            FinishVideo();
            yield break;
        }

        // Configure audio routing here, where the GameObject is active (SetVideo runs while this
        // object is disabled, so settings applied there don't take effect). These MUST be set before
        // Prepare() so the audio track is included in preparation. WebGL supports only None/Direct
        // output modes and does not report audioTrackCount, so we force track 0 unconditionally.
        videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        videoPlayer.controlledAudioTrackCount = 1;
        videoPlayer.EnableAudioTrack(0, true);
        videoPlayer.SetDirectAudioMute(0, false);
        videoPlayer.SetDirectAudioVolume(0, 1f);

        // Prepare while ENABLED and wait until the media (including its audio track) is loaded before
        // Play(). Playing an unprepared URL video wires up no audio on WebGL (audioTrackCount=0 at play
        // time), which is why the first, uncached play was silent while cached replays had sound.
        videoPlayer.Prepare();
        float prepStart = Time.unscaledTime;
        while (!videoPlayer.isPrepared && !playbackError && Time.unscaledTime - prepStart < PLAYBACK_START_TIMEOUT)
            yield return null;

        Debug.Log("[FLOW] VideoControl prepared=" + videoPlayer.isPrepared
                  + " outputMode=" + videoPlayer.audioOutputMode
                  + " controlled=" + videoPlayer.controlledAudioTrackCount
                  + " audioTrackCount=" + videoPlayer.audioTrackCount);
        LogWebAudioState("videoStart");

        if (playbackError || !isPlayingVideo)
            yield break;   // errored or finished during preparation; do not play

        playRequestTime = Time.unscaledTime;
        videoPlayer.Play();
    }

    public bool IsPlaying()
    {
        return isPlayingVideo;
    }

    private void OnLoopPointReached(VideoPlayer vp)
    {
        Debug.Log("VideoControl end video");
        FinishVideo();
    }

    private void OnVideoErrorReceived(VideoPlayer vp, string message)
    {
        playbackError = true;
        Debug.LogWarning("VideoControl video error: " + message);
        FinishVideo();
    }

    private void FinishVideo()
    {
        isPlayingVideo = false;
        videoPlayer.Stop();
        if (deactivateWhenFinished)
            gameObject.SetActive(false);
    }
}
