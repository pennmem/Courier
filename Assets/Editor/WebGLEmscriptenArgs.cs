#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

// Applies Emscripten linker args for WebGL builds.
//
// Unity 2021.3.4f1's IL2CPP does not define il2cpp::os::Thread::HasCurrentThread, yet generated code
// references it from otherwise-unreachable (lab-hardware/threading) paths, so the WebGL link aborts
// with "undefined symbol: _ZN6il2cpp2os6Thread16HasCurrentThreadEv". ERROR_ON_UNDEFINED_SYMBOLS=0
// lets the link complete by stubbing the dead symbol; LLD_REPORT_UNDEFINED=1 logs the exact
// undefined symbol(s) and referencing object so the culprit can be guarded at the source later.
public class WebGLEmscriptenArgs : IPreprocessBuildWithReport
{
    public int callbackOrder { get { return 0; } }

    private const string RequiredArgs = "-s ERROR_ON_UNDEFINED_SYMBOLS=0 -s LLD_REPORT_UNDEFINED=1";

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.WebGL)
            return;

        string existing = PlayerSettings.WebGL.emscriptenArgs;
        if (string.IsNullOrEmpty(existing))
            PlayerSettings.WebGL.emscriptenArgs = RequiredArgs;
        else if (!existing.Contains("ERROR_ON_UNDEFINED_SYMBOLS"))
            PlayerSettings.WebGL.emscriptenArgs = existing.Trim() + " " + RequiredArgs;
    }
}
#endif // UNITY_EDITOR
