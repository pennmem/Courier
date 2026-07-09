using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Luminosity.IO;
using UnityEngine.Video;

public class VideoControl : MonoBehaviour
{
    public UnityEngine.Video.VideoPlayer videoPlayer;
    public bool deactivateWhenFinished = true;

    private const float PLAYBACK_START_TIMEOUT = 15f;

    private bool isPlayingVideo;
    private bool playbackError;
    private float playRequestTime;

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


    public void StartVideo()
    {
        Debug.Log("[FLOW] VideoControl.StartVideo. source=" + videoPlayer.source + " clip=" + (videoPlayer.clip != null ? videoPlayer.clip.name : "<null>") + " url='" + videoPlayer.url + "'");
        playbackError = false;
        isPlayingVideo = true;
        gameObject.SetActive(true);

        if ((videoPlayer.source == VideoSource.VideoClip && videoPlayer.clip == null) ||
            (videoPlayer.source == VideoSource.Url && string.IsNullOrEmpty(videoPlayer.url)))
        {
            Debug.LogWarning("[FLOW] VideoControl StartVideo called without a configured clip or URL; skipping video.");
            FinishVideo();
            return;
        }

        // Configure audio routing here, where the GameObject is guaranteed active (SetVideo runs
        // while this object is disabled, so audio settings applied there don't take effect on WebGL).
        // WebGL supports only None/Direct output modes and needs an explicit controlled track that is
        // enabled and unmuted - it does NOT report audioTrackCount, so we force track 0 unconditionally.
        videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        videoPlayer.controlledAudioTrackCount = 1;
        videoPlayer.EnableAudioTrack(0, true);
        videoPlayer.SetDirectAudioMute(0, false);
        videoPlayer.SetDirectAudioVolume(0, 1f);
        Debug.Log("[FLOW] VideoControl audio: outputMode=" + videoPlayer.audioOutputMode
                  + " controlled=" + videoPlayer.controlledAudioTrackCount
                  + " audioTrackCount=" + videoPlayer.audioTrackCount);

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
