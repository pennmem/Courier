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

        #if !(UNITY_WEBGL && !UNITY_EDITOR) // WebGL VideoPlayer
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
        #endif // !(UNITY_WEBGL && !UNITY_EDITOR)

        if ((videoPlayer.source == VideoSource.VideoClip && videoPlayer.clip != null) ||
            (videoPlayer.source == VideoSource.Url && !string.IsNullOrEmpty(videoPlayer.url)))
        {
            videoPlayer.Prepare();
        }
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private const string WebGLVideoBaseUrlQueryKey = "videoBaseUrl";

    private string GetWebGLVideoUrl(VideoType videoType)
    {
        string videoFile = GetWebGLVideoFileName(videoType);
        string baseUrl = GetWebGLVideoBaseUrl();

        if (string.IsNullOrEmpty(videoFile) || string.IsNullOrEmpty(baseUrl))
            return null;

        return baseUrl.TrimEnd('/', '\\') + "/" + videoFile.Replace("\\", "/");
    }

    private string GetWebGLVideoBaseUrl()
    {
        if (!string.IsNullOrEmpty(webGLVideoBaseUrl))
            return webGLVideoBaseUrl;

        return GetAbsoluteUrlQueryValue(WebGLVideoBaseUrlQueryKey);
    }

    private string GetAbsoluteUrlQueryValue(string key)
    {
        string absoluteUrl = Application.absoluteURL;
        if (string.IsNullOrEmpty(absoluteUrl))
            return null;

        int queryStart = absoluteUrl.IndexOf('?');
        if (queryStart < 0 || queryStart >= absoluteUrl.Length - 1)
            return null;

        int queryEnd = absoluteUrl.IndexOf('#', queryStart + 1);
        string query = queryEnd >= 0
            ? absoluteUrl.Substring(queryStart + 1, queryEnd - queryStart - 1)
            : absoluteUrl.Substring(queryStart + 1);

        string[] pairs = query.Split('&');
        foreach (string pair in pairs)
        {
            string[] parts = pair.Split(new char[] { '=' }, 2);
            if (parts.Length == 0 || parts[0] != key)
                continue;

            string value = parts.Length > 1 ? parts[1] : "";
            return Uri.UnescapeDataString(value.Replace("+", " "));
        }

        return null;
    }

    private string GetWebGLVideoFileName(VideoType videoType)
    {
        switch (videoType)
        {
            case VideoType.MainIntro:
            case VideoType.townlearningVideo:
            case VideoType.practiceVideo:
                return "instruction_video.mp4";
            case VideoType.valueIntro:
            case VideoType.vcInstructionsVideo:
            case VideoType.efrRecapVideo:
                return "instruction_video_updated.mp4";
            default:
                return null;
        }
    }
#endif // UNITY_WEBGL && !UNITY_EDITOR
}
