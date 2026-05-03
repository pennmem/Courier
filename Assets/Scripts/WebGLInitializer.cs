using UnityEngine;

/// <summary>
/// Initializes WebGL-specific settings and disables incompatible systems.
/// Uses reflection-style component scanning so WebGL builds do not require
/// the old PostProcessing namespace/package to exist.
/// </summary>
public class WebGLInitializer : MonoBehaviour
{
    void OnEnable()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        DisablePostProcessing();
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    void DisablePostProcessing()
    {
        MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>();
        int disabledCount = 0;

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null)
            {
                continue;
            }

            string typeName = behaviour.GetType().FullName;

            if (
                typeName.Contains("PostProcessingBehaviour") ||
                typeName.Contains("PostProcessVolume") ||
                typeName.Contains("PostProcessLayer")
            )
            {
                behaviour.enabled = false;
                disabledCount++;
            }
        }

        if (disabledCount > 0)
        {
            Debug.LogWarning("[WebGL] Disabled " + disabledCount + " post-processing component(s).");
        }
    }
#endif
}