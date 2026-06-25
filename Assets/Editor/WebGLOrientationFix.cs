#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Neutralizes Unity 2021.3.x's WebGL screen-orientation handler in the generated framework.js.
//
// The engine wires JS_ScreenOrientation_eventHandler to the window "resize" event and self-fires it
// once via setTimeout at init. That handler calls into the wasm OrientationChangeHandler, which traps
// with "RuntimeError: index out of bounds" on load — a known 2021.3 WebGL bug. A wasm trap kills the
// whole instance, so nothing runs. No C# code in this project reads Screen.orientation, so making the
// handler a no-op is safe for this desktop, fixed-landscape experiment.
//
// This runs after the build (post-process), so it patches the plain *.framework.js produced by
// uncompressed (development) builds. Release builds with Brotli/Gzip compression emit a compressed
// *.framework.js.br/.gz that this text patch cannot touch — in that case it logs a warning so the
// crash can't ship silently (set Compression Format to Disabled, or Gzip + Decompression Fallback).
public class WebGLOrientationFix : IPostprocessBuildWithReport
{
    // After WebGLEmscriptenArgs (callbackOrder 0).
    public int callbackOrder { get { return 10; } }

    private const string Marker = "CourierWebGL: Unity 2021.3 orientation crash workaround";
    private const string Target = "function JS_ScreenOrientation_eventHandler() {";
    private const string Patched = "function JS_ScreenOrientation_eventHandler() { return; /* " + Marker + " */";

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.WebGL)
            return;

        string outputPath = report.summary.outputPath;
        // outputPath is the build folder (containing Build/, TemplateData/, index.html). Be tolerant
        // of it pointing directly at the Build/ folder or a file.
        string root = Directory.Exists(outputPath) ? outputPath : Path.GetDirectoryName(outputPath);
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            Debug.LogWarning("[WebGLOrientationFix] Could not resolve build output folder from: " + outputPath);
            return;
        }

        string[] frameworkFiles = Directory.GetFiles(root, "*.framework.js", SearchOption.AllDirectories);
        int patchedCount = 0;
        foreach (string file in frameworkFiles)
        {
            string contents = File.ReadAllText(file);
            if (contents.Contains(Marker))
            {
                patchedCount++; // already patched (idempotent)
                continue;
            }
            if (!contents.Contains(Target))
            {
                Debug.LogWarning("[WebGLOrientationFix] Orientation handler not found in: " + file +
                    " (Unity version change?). Skipped.");
                continue;
            }
            contents = contents.Replace(Target, Patched);
            File.WriteAllText(file, contents);
            patchedCount++;
            Debug.Log("[WebGLOrientationFix] Patched orientation handler in: " + file);
        }

        if (patchedCount == 0)
        {
            string[] compressed = Directory.GetFiles(root, "*.framework.js.br", SearchOption.AllDirectories);
            string[] gzipped = Directory.GetFiles(root, "*.framework.js.gz", SearchOption.AllDirectories);
            if (compressed.Length > 0 || gzipped.Length > 0)
                Debug.LogWarning("[WebGLOrientationFix] framework.js is compressed (.br/.gz) and cannot be " +
                    "text-patched. The orientation crash WILL be present. Set Player Settings > Publishing " +
                    "Settings > Compression Format to Disabled (or Gzip + Decompression Fallback) and rebuild.");
            else
                Debug.LogWarning("[WebGLOrientationFix] No *.framework.js found under " + root +
                    " — orientation crash workaround NOT applied.");
        }
    }
}
#endif // UNITY_EDITOR
