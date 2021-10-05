using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class VideoControl : MonoBehaviour
{
    public UnityEngine.KeyCode pauseToggleKey = UnityEngine.KeyCode.Space;
    public UnityEngine.KeyCode deactivateKey = UnityEngine.KeyCode.Escape;
    public UnityEngine.Video.VideoPlayer videoPlayer;
    public bool deactivateWhenFinished = true;

    void Update()
    {
        if (Input.GetKeyDown(pauseToggleKey))
        {
            if (videoPlayer.isPlaying)
                videoPlayer.Pause();
            else
                videoPlayer.Play();
        }

        // LC: allowing this for online version is dangerous...
        #if UNITY_STANDALONE
            if (Input.GetKeyDown(deactivateKey))
            {
                videoPlayer.Stop();
                gameObject.SetActive(false);
            }

        if (videoPlayer.time >= videoPlayer.clip.length) // videoPlayer.clip.length
        {
            gameObject.SetActive(false);
        }
        #endif
    }

    public void StartVideo()
    {
        Debug.Log("VideoControl");
        #if UNITY_WEBGL
        videoPlayer.loopPointReached += (VideoPlayer vp) => gameObject.SetActive(false);
        #endif
        gameObject.SetActive(true);
    }

    public bool IsPlaying()
    {
        return gameObject.activeSelf;
    }
}
