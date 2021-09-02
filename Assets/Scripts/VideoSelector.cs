using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
        MusicVideos
    }

    public void SetVideo(VideoType videoType, int videoIndex = 0)
    {
        videoPlayer.targetTexture.Release();
        videoPlayer.targetTexture.Create();

        #if !UNITY_WEBGL
            switch (videoType)
            {
                // JPB: TODO: Refactor this to make movies an array of language options
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
            string oldPath = "http://psiturk.sas.upenn.edu:22371/static/js/Unity/build/StreamingAssets";
            switch (videoType)
            {
                case VideoType.MainIntro:
                    if (LanguageSource.current_language == LanguageSource.LANGUAGE.GERMAN)
                        videoPlayer.url = System.IO.Path.Combine(oldPath,
                                                                 "germanCourierIntro.mov");
                    else
                        videoPlayer.url = System.IO.Path.Combine(oldPath,
                                                                 "instruction_video.mp4");
                    break;
                case VideoType.EfrIntro:
                    videoPlayer.url = System.IO.Path.Combine(oldPath,
                                                             "englishCourierEfrIntro.mp4");
                    break;
                case VideoType.NewEfrIntro:
                    videoPlayer.url = System.IO.Path.Combine(oldPath,
                                                             "englishCourierEfrIntro.mp4");
                    break;
                case VideoType.NiclsMainIntro:
                    videoPlayer.url = System.IO.Path.Combine(oldPath,
                                                             "englishCourierIntroShort_NoPoint_NoRecap.mov");
                    break;
                case VideoType.NiclsMovie:
                    videoPlayer.url = System.IO.Path.Combine(oldPath,
                                                             "Sherlock_" + videoIndex+1 + ".mov");
                    break;
                case VideoType.MusicVideos:
                    videoPlayer.url = System.IO.Path.Combine(oldPath,
                                                             "music_video_" + videoIndex + ".mov");
                    break;
                default: break;

            }
#endif // !UNITY_WEBGL

        videoPlayer.Prepare();
    }
}
