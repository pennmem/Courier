using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VideoSelector : MonoBehaviour
{
    public UnityEngine.Video.VideoPlayer videoPlayer;
    public UnityEngine.Video.VideoClip englishIntro;
    public UnityEngine.Video.VideoClip germanIntro;
    public UnityEngine.Video.VideoClip englishPostpracticeIntro;
    public UnityEngine.Video.VideoClip germanPostpracticeIntro;

    void OnEnable()
    {
        if (videoPlayer.clip == null)
            Debug.Log("VideoSelector::OnEnable - SetIntroductionVideo was " +
                      "not called before OnEnable");

        videoPlayer.Play();
    }

    public enum VideoType
    {
        MainIntro,
        PostpracticeIntro
    }

    public void SetIntroductionVideo(VideoType videoType)
    {
        switch (videoType)
        {
            case VideoType.MainIntro:
                if (LanguageSource.current_language == LanguageSource.LANGUAGE.GERMAN)
                    videoPlayer.clip = germanIntro;
                else
                    videoPlayer.clip = englishIntro;
                break;
            case VideoType.PostpracticeIntro:
                if (LanguageSource.current_language == LanguageSource.LANGUAGE.GERMAN)
                    videoPlayer.clip = germanPostpracticeIntro;
                else
                    videoPlayer.clip = englishPostpracticeIntro;
                break;
            default: break;
        }
    }
}
