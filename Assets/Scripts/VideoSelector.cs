using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Video;

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

    public UnityEngine.Video.VideoClip townlearingVideo;
    public UnityEngine.Video.VideoClip practiceVideo;
    public UnityEngine.Video.VideoClip ecrVideo;
    public UnityEngine.Video.VideoClip efrRecapVideo;
    public UnityEngine.Video.VideoClip vcInstructionsVideo;

    public string webGLVideoBaseUrl = "";

    void OnEnable()
    {
        bool hasClip = videoPlayer.source == VideoSource.VideoClip && videoPlayer.clip != null;
        bool hasUrl = videoPlayer.source == VideoSource.Url && !string.IsNullOrEmpty(videoPlayer.url);
        if (!hasClip && !hasUrl)
            Debug.LogWarning("VideoSelector::OnEnable - SetVideo was not called before OnEnable.");
    }

    public enum VideoType
    {
        MainIntro,
        EfrIntro,
        NewEfrIntro,
        NiclsMainIntro,
        NiclsMovie,
        MusicVideos,
        valueIntro,
        townlearningVideo,
        practiceVideo,
        ecrVideo,
        efrRecapVideo,
        vcInstructionsVideo
    }

    public void SetVideo(VideoType videoType, int videoIndex = 0)
    {
        videoPlayer.Stop();
        if (videoPlayer.targetTexture != null)
        {
            videoPlayer.targetTexture.Release();
            videoPlayer.targetTexture.Create();
        }

        #if !UNITY_WEBGL // Non-WebGL targets use the bundled VideoClip directly.
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.url = "";
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
                case VideoType.townlearningVideo:
                    videoPlayer.clip = townlearingVideo;
                    break;
                case VideoType.practiceVideo:
                    videoPlayer.clip = practiceVideo;
                    break;
                case VideoType.ecrVideo:
                    videoPlayer.clip = ecrVideo;
                    break;
                case VideoType.efrRecapVideo:
                    videoPlayer.clip = efrRecapVideo;
                break;
                case VideoType.vcInstructionsVideo:
                    videoPlayer.clip = vcInstructionsVideo;
                    break;
                default: break;
            }
        #else
            videoPlayer.source = VideoSource.Url;
            videoPlayer.clip = null;
            string videoUrl = GetWebGLVideoUrl(videoType);
            if (string.IsNullOrEmpty(videoUrl))
            {
                videoPlayer.url = "";
                Debug.LogWarning("No WebGL video URL is configured for " + videoType + " index " + videoIndex + ". Host the instruction videos externally and set webGLVideoBaseUrl or the videoBaseUrl query parameter.");
            }
            else
            {
                videoPlayer.url = videoUrl;
            }
        #endif // !UNITY_WEBGL

        if ((videoPlayer.source == VideoSource.VideoClip && videoPlayer.clip != null) ||
            (videoPlayer.source == VideoSource.Url && !string.IsNullOrEmpty(videoPlayer.url)))
        {
            // WebGL only supports None/Direct output modes (AudioSource mode is ignored). The full
            // audio routing (controlled track enabled + unmuted) is set in VideoControl.StartVideo,
            // where the GameObject is active; setting it here would be lost because SetVideo runs
            // while the VideoPlayer is disabled ("Cannot Prepare a disabled VideoPlayer").
            videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;

            videoPlayer.Prepare();
        }
    }

#if UNITY_WEBGL
    private const string WebGLVideoBaseUrlQueryKey = "videoBaseUrl";

    private string GetWebGLVideoUrl(VideoType videoType)
    {
        string videoFile = GetWebGLVideoFileName(videoType);
        string baseUrl = GetWebGLVideoBaseUrl();

        if (string.IsNullOrEmpty(videoFile) || string.IsNullOrEmpty(baseUrl))
            return null;

        string combined = baseUrl.TrimEnd('/', '\\') + "/" + videoFile.Replace("\\", "/");
        return EnsureUrlScheme(combined);
    }

    private string GetWebGLVideoBaseUrl()
    {
        if (!string.IsNullOrEmpty(webGLVideoBaseUrl))
            return webGLVideoBaseUrl;

        string queryValue = UrlParams.Get(WebGLVideoBaseUrlQueryKey);
        if (!string.IsNullOrEmpty(queryValue))
            return queryValue;

        // Fallback: serve videos from StreamingAssets/Videos alongside the build.
        return Application.streamingAssetsPath.TrimEnd('/', '\\') + "/Videos";
    }

    // In the Editor (any platform) Application.streamingAssetsPath is a local filesystem path,
    // which VideoPlayer's URL source can't consume without a file:// scheme. In an actual WebGL
    // build the path is already a relative URL served alongside the build, so leave it alone
    // so anyone hosting the build sees the video.
    private static string EnsureUrlScheme(string path)
    {
#if UNITY_EDITOR
        if (string.IsNullOrEmpty(path))
            return path;

        if (path.Contains("://"))
            return path;

        string normalized = path.Replace("\\", "/");
        if (normalized.StartsWith("/"))
            return "file://" + normalized;
        return "file:///" + normalized;
#else
        return path;
#endif
    }

    private string GetWebGLVideoFileName(VideoType videoType)
    {
        // Filenames must match files placed under Assets/StreamingAssets/Videos/.
        switch (videoType)
        {
            case VideoType.MainIntro:
            case VideoType.valueIntro:
            case VideoType.vcInstructionsVideo:
                return "VC_Online_Intro_Vid.mp4";
            case VideoType.townlearningVideo:
                return "town_learning_instuctions.mp4";
            case VideoType.practiceVideo:
                return "standard_FR_instructions.mp4";
            case VideoType.efrRecapVideo:
                return "EFR_instructions.mp4";
            case VideoType.ecrVideo:
                return "cued_recall_video.mp4";
            default:
                return null;
        }
    }
#endif // UNITY_WEBGL
}
