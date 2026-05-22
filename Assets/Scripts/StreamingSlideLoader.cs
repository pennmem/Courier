using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

// Loads sprite PNGs from StreamingAssets at runtime and assigns them to slide
// Image components. Lets the deployed WebGL build serve slide artwork from
// StreamingAssets instead of relying on baked Sprite references that come up
// blank in WebGL.
public class StreamingSlideLoader : MonoBehaviour
{
    public MessageImageDisplayer messageImageDisplayer;
    public string streamingSubfolder = "Images/Recap Instructions VC";

    private void Start()
    {
        if (messageImageDisplayer == null)
        {
            Debug.LogWarning("StreamingSlideLoader: messageImageDisplayer not assigned.");
            return;
        }

        LoadGroup(messageImageDisplayer.value_instruction_messages_en);
    }

    private void LoadGroup(GameObject[] slides)
    {
        if (slides == null) return;
        foreach (GameObject slide in slides)
        {
            if (slide == null) continue;
            StartCoroutine(LoadSlideSprite(slide));
        }
    }

    private IEnumerator LoadSlideSprite(GameObject slide)
    {
        Image image = slide.GetComponent<Image>();
        if (image == null) yield break;

        string fileName = DeriveFileName(slide.name);
        if (string.IsNullOrEmpty(fileName)) yield break;

        string url = BuildStreamingUrl(fileName);

        using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(url))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("StreamingSlideLoader: failed to load " + url + ": " + www.error);
                yield break;
            }

            Texture2D tex = DownloadHandlerTexture.GetContent(www);
            if (tex == null) yield break;

            Sprite sprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f));
            image.sprite = sprite;
        }
    }

    private string BuildStreamingUrl(string fileName)
    {
        string baseUrl = Application.streamingAssetsPath.TrimEnd('/', '\\')
                         + "/" + streamingSubfolder.Replace("\\", "/")
                         + "/" + fileName;

#if UNITY_EDITOR
        // Editor's streamingAssetsPath is a local filesystem path; UnityWebRequest needs file://.
        if (!baseUrl.Contains("://"))
        {
            string normalized = baseUrl.Replace("\\", "/");
            baseUrl = normalized.StartsWith("/") ? "file://" + normalized : "file:///" + normalized;
        }
#endif
        return baseUrl;
    }

    // Maps a slide GameObject name to its PNG filename in StreamingAssets/Images/Recap Instructions VC/.
    // Convention: "Recap Instructions VC 1" -> "Recap Instructions VC-1.png".
    // Special case: the post-office recap slide is named "Recap Instructions VC post"
    // but the artwork file is "Recap Instructions post office.png".
    private static string DeriveFileName(string slideName)
    {
        if (string.IsNullOrEmpty(slideName)) return null;

        if (slideName == "Recap Instructions VC post")
            return "Recap Instructions post office.png";

        int lastSpace = slideName.LastIndexOf(' ');
        if (lastSpace < 0) return null;

        string suffix = slideName.Substring(lastSpace + 1);
        int parsed;
        if (!int.TryParse(suffix, out parsed)) return null;

        return slideName.Substring(0, lastSpace) + "-" + suffix + ".png";
    }
}
