**P0: Compile**
1. Commit: isolate editor inspectors  
Files: `Assets/ModularProceduralBuildings/Scripts/BuildingEditor.cs`, `Assets/ModularProceduralBuildings/Scripts/SideewalkEditor.cs`  
Wrap whole files in `#if UNITY_EDITOR` or move to an `Editor/` folder.

2. Commit: remove editor dependency from runtime controllers  
Files: `Assets/ModularProceduralBuildings/Scripts/BuildingController.cs`, `Assets/ModularProceduralBuildings/Scripts/SidewalkController.cs`  
Guard `using UnityEditor`, `PrefabUtility`, and editor-only generation calls.

3. Commit: make UnityEPL native bridge platform-dependent  
File: `Assets/UnityEPL/Scripts/UnityEPL.cs`  
Keep `UnityEPLNativePlugin` imports for mac/native; add WebGL-player stubs or `__Internal` declarations only where needed.

4. Commit: make browser JS externs editor-safe  
Files: `Assets/Scripts/DeliveryExperiment.cs`, `Assets/UnityEPL/Scripts/WriteToDiskHandler.cs`, `Assets/Plugins/PsiturkPlugin.jslib`  
Guard `EndTask`, `SaveData`, `AddData` with `UNITY_WEBGL && !UNITY_EDITOR`; update `Pointer_stringify` to modern `UTF8ToString`.

5. Commit: fix native interface symbol visibility  
Files: `Assets/ElememInterface/ElememInterface.cs`, `Assets/NiclsInterface/NiclsInterface.cs`, `Assets/RamulatorInterface/RamulatorInterface.cs`  
Change top guards from `#if !UNITY_WEBGL` to `#if !(UNITY_WEBGL && !UNITY_EDITOR)`.

6. Commit: fix syncbox/native thread symbol visibility  
Files: `Assets/Scripts/Syncbox.cs`, `Assets/Scripts/FreiburgSyncbox.cs`, `Assets/Scripts/UPennSyncbox.cs`  
Use the same guard; include the unguarded `SyncListener` in `UPennSyncbox.cs`.

7. Commit: guard remaining experiment native references  
File: `Assets/Scripts/DeliveryExperiment.cs`  
Wrap `elememInterface`, `niclsInterface`, `ramulatorInterface`, `syncs`, and calls at `1856`, `2294`, `2320`, `3256`, `3535`.

8. Commit: exclude desktop device/video DLLs from WebGL  
Files: `Assets/Plugins/Accord.Video.DirectShow.dll.meta`, `Assets/Plugins/Accord.Video.Kinect.dll.meta`, `Assets/Plugins/Accord.Video.VFW.dll.meta`.

9. Commit: exclude more desktop DLLs from WebGL  
Files: `Assets/Plugins/Accord.Video.Ximea.dll.meta`, `Assets/Plugins/LibUsbDotNet.dll.meta`, `Assets/Plugins/NetMQ.dll.meta`.

10. Commit: exclude unused desktop UI/science DLLs if compile complains  
Files: `Assets/Plugins/ZedGraph.dll.meta`, `Assets/Plugins/AsyncIO 0.1.26/AsyncIO.dll.meta`, `Assets/Plugins/Accord.Controls.dll.meta`.

**P0: Config Boot**
11. Commit: stop Config reads during field initialization  
Files: `Assets/Scripts/DeliveryExperiment.cs`, `Assets/Scripts/PlayerMovement.cs`, `Assets/Scripts/FlexibleConfig.cs`  
Move `Config.*` reads from field initializers into post-config init; allocate arrays after config is loaded.

12. Commit: remove early Elemem config reads  
Files: `Assets/UnityEPL/Scripts/InputReporter.cs`, `Assets/UnityEPL/Scripts/WorldDataReporter.cs`  
Do not initialize `elememOn = Config.elememOn` before config exists.

13. Commit: harden WebGL config loading  
File: `Assets/Scripts/FlexibleConfig.cs`  
Make `onlineExperimentConfigText` default `null`, make `GetOnlineConfig()` required/idempotent, and fail clearly if JSON fetch fails.

**P1: Runtime File IO**
14. Commit: convert TSP route loading  
File: `Assets/Scripts/DeliveryExperiment.cs`  
Replace `File.Exists` / `File.ReadAllLines` on `Application.streamingAssetsPath` with `UnityWebRequest`.

15. Commit: WebGL-safe menu/session file IO  
File: `Assets/Scripts/BeginExperiment.cs`  
Guard `GetLanguageFilePath`, `LanguageMismatch`, `LockLanguage`, `NextSessionNumber` for native only; browser should not touch local disk.

16. Commit: WebGL-safe remaining-item behavior  
File: `Assets/Scripts/DeliveryItems.cs`  
Keep mac disk persistence; verify WebGL in-memory pool supports language selection, resets, and exhaustion rules.

17. Commit: guard dev-only disk tools  
Files: `Assets/Scripts/GetItems.cs`, `Assets/Scripts/SimulateRoutes.cs`, `Assets/UnityEPL/Scripts/PostHocViewReport.cs`.

**P1: Browser Behavior**
18. Commit: make PsiTurk optional  
Files: `Assets/Plugins/PsiturkPlugin.jslib`, `Assets/UnityEPL/Scripts/WriteToDiskHandler.cs`, `Assets/Scripts/DeliveryExperiment.cs`  
Detect missing `psiturk` / `Questionnaire`; log locally instead of hard-crashing.

19. Commit: browser input and cursor lock pass  
Files: `Assets/Scripts/BeginExperiment.cs`, `Assets/Scripts/DeliveryExperiment.cs`  
Ensure scene start and `Cursor.lockState` happen after a user gesture when needed.

20. Commit: WebGL video smoke fixes  
Files: `Assets/Scripts/VideoSelector.cs`, `Assets/UnityEPL/Prefabs/VideoPlayer/VideoControl.cs`  
Verify URL paths, `Prepare`, `loopPointReached`, autoplay/user gesture, and fallback when browser blocks playback.

21. Commit: microphone/recall policy  
Files: `Assets/Scripts/CoroutineExperiment.cs`, `Assets/UnityEPL/Prefabs/SoundRecorder/SoundRecorder.cs`, `Assets/Scripts/DeliveryExperiment.cs`  
Keep native microphone path; define WebGL typed-response behavior explicitly.

**P2: Threading, AOT, Performance**
22. Commit: quarantine thread/timer framework  
Files: `Assets/Scripts/EventLoop.cs`, `Assets/Scripts/EventQueue.cs`  
Either native-guard or replace with coroutine scheduler if any WebGL code needs timed events.

23. Commit: AOT/dynamic cleanup  
Files: `Assets/ElememInterface/ElememDataPoint.cs`, `Assets/UnityEPL/Scripts/PostHocViewReport.cs`, `Assets/Scripts/FlexibleConfig.cs`  
Avoid `dynamic` in WebGL player paths; keep reflection-like code native/editor only.

24. Commit: WebGL size triage for guaranteed payload  
Files: `Assets/StreamingAssets/instruction_video.mp4.meta`, `Assets/StreamingAssets/instruction_video_updated.mp4.meta`, `Assets/Scripts/VideoSelector.cs`  
Compress or externally host large videos; StreamingAssets currently adds about `171M`.

25. Commit: asset-reference size pass  
Files: `Assets/Scripts/VideoSelector.cs`, relevant video `.meta` files, relevant scene only if unavoidable  
Remove unused referenced clips from WebGL build path without changing mac behavior.

**Verification Gates**
After commits 1-10: Unity compile with StandaloneOSX and WebGL target.  
After commits 11-17: WebGL build starts and loads config/routes.  
After commits 18-21: browser smoke test: start, move, view instructions, complete one trial.  
After commits 22-25: console has no blocking red errors, build size is known and acceptable.