using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class VideoSelector : MonoBehaviour
{
    public UnityEngine.Video.VideoPlayer videoPlayer;
    public UnityEngine.Video.VideoClip englishIntro;
    public UnityEngine.Video.VideoClip germanIntro;
    public UnityEngine.Video.VideoClip englishEfrIntro;
    public UnityEngine.Video.VideoClip germanEfrIntro;
    public UnityEngine.Video.VideoClip englishNewEfrIntro;
    public UnityEngine.Video.VideoClip germanNewEfrIntro;
    public UnityEngine.Video.VideoClip niclsEnglishIntro;
    public UnityEngine.Video.VideoClip[] niclsMovie;
    public UnityEngine.Video.VideoClip[] musicVideos;
    public UnityEngine.Video.VideoClip valueCourier;

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
        EfrIntro,
        NewEfrIntro,
        NiclsMainIntro,
        NiclsMovie,
        MusicVideos,
        valueIntro
    }

    public void SetVideo(VideoType videoType, int videoIndex = 0)
    {
        videoPlayer.targetTexture.Release();
        videoPlayer.targetTexture.Create();

        #if !UNITY_WEBGL // WebGL VideoPlayer
            switch (videoType)
            {
                // TODO: JPB: Refactor this to make movies an array of language options
                case VideoType.MainIntro:
                    if (LanguageSource.current_language == LanguageSource.LANGUAGE.GERMAN)
                        videoPlayer.clip = germanIntro;
                    else
                        videoPlayer.clip = englishIntro;
                    break;
                case VideoType.EfrIntro:
                    if (LanguageSource.current_language == LanguageSource.LANGUAGE.GERMAN)
                        videoPlayer.clip = germanEfrIntro;
                    else
                        videoPlayer.clip = englishEfrIntro;
                    break;
                case VideoType.NewEfrIntro:
                    if (LanguageSource.current_language == LanguageSource.LANGUAGE.GERMAN)
                        videoPlayer.clip = germanNewEfrIntro;
                    else
                        videoPlayer.clip = englishNewEfrIntro;
                    break;
                case VideoType.NiclsMainIntro:
                    videoPlayer.clip = niclsEnglishIntro;
                    break;
                case VideoType.NiclsMovie:
                    videoPlayer.clip = niclsMovie[videoIndex];
                    break;
                case VideoType.MusicVideos:
                    videoPlayer.clip = musicVideos[videoIndex];
                    break;
                default: break;
            }
        #else
            string path = Application.streamingAssetsPath;
            switch (videoType)
            {
                case VideoType.MainIntro:
                    videoPlayer.url = System.IO.Path.Combine(path,"instruction_video.mp4");                    
                    break;
                 case VideoType.valueIntro:
                    videoPlayer.url = System.IO.Path.Combine(path,"instruction_video_updated.mp4");
                    break;
                    
                // LC: could later add onto webGL but not yet
                // case VideoType.EfrIntro:
                //     videoPlayer.url = System.IO.Path.Combine(path,
                //                                              "englishCourierEfrIntro.mp4");
                //     break;
                // case VideoType.NewEfrIntro:
                //     videoPlayer.url = System.IO.Path.Combine(path,
                //                                              "englishCourierEfrIntro.mp4");
                //     break;
                // case VideoType.NiclsMainIntro:
                //     videoPlayer.url = System.IO.Path.Combine(path,
                //                                              "englishCourierIntroShort_NoPoint_NoRecap.mov");
                //     break;
                // case VideoType.NiclsMovie:
                //     videoPlayer.url = System.IO.Path.Combine(path,
                //                                              "Sherlock_" + videoIndex+1 + ".mov");
                //     break;
                // case VideoType.MusicVideos:
                //     videoPlayer.url = System.IO.Path.Combine(path,
                //                                              "music_video_" + videoIndex + ".mov");
                //     break;
                default: break;

            }
#endif // !UNITY_WEBGL

        videoPlayer.Prepare();
    }
}
