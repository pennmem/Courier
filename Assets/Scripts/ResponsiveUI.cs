using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Makes all UI scale with the viewport so text fits any screen size. Several scenes
/// ship with CanvasScaler set to ConstantPixelSize, which keeps a fixed pixel size
/// regardless of window dimensions; combined with the Text components' Best Fit this
/// left text too small/large depending on the browser size. This runs automatically
/// after every scene load and switches each CanvasScaler to ScaleWithScreenSize, so no
/// per-scene wiring is needed (mirrors the runtime-fixup approach in WebGLInitializer).
/// </summary>
public static class ResponsiveUI
{
    private static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyToLoadedScenes();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyToLoadedScenes();
    }

    private static void ApplyToLoadedScenes()
    {
        foreach (CanvasScaler scaler in Object.FindObjectsOfType<CanvasScaler>())
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }
    }
}
