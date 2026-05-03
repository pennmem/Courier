#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

public class WebGLVideoReferenceStripper : IProcessSceneWithReport
{
    public int callbackOrder { get { return 0; } }

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report == null || report.summary.platform != BuildTarget.WebGL)
            return;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (VideoSelector selector in root.GetComponentsInChildren<VideoSelector>(true))
            {
                ClearVideoSelectorClips(selector);
            }

            foreach (VideoPlayer videoPlayer in root.GetComponentsInChildren<VideoPlayer>(true))
            {
                videoPlayer.clip = null;
            }
        }
    }

    private static void ClearVideoSelectorClips(VideoSelector selector)
    {
        selector.englishIntro = null;
        selector.germanIntro = null;
        selector.englishEfrIntro = null;
        selector.germanEfrIntro = null;
        selector.englishNewEfrIntro = null;
        selector.germanNewEfrIntro = null;
        selector.niclsEnglishIntro = null;
        selector.niclsMovie = new VideoClip[0];
        selector.musicVideos = new VideoClip[0];
        selector.townlearingVideo = null;
        selector.practiceVideo = null;
        selector.ecrVideo = null;
        selector.efrRecapVideo = null;
        selector.vcInstructionsVideo = null;
    }
}
#endif // UNITY_EDITOR
