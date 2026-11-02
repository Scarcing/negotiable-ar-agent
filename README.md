# Negotiable AR Agent — Quest 3

A Unity **6000.3.27f1** research prototype with Meta passthrough camera access, environment-depth occlusion, hand/finger joints, local visual-language perception, and a negotiable assistance policy. The task remains configurable for tangrams, construction pieces, or other compact creative activities.

Open the **`Unity/` directory** as the Unity project, then open `Assets/NegotiableAR/Scenes/QuestAgent.unity`. The project uses the Built-in Render Pipeline with Meta's matching depth shader. No URP assets are required.

## Run on this machine

Portable Ollama and Qwen3-VL 2B have been installed under ignored `.tools/`. The Python service has no third-party dependencies. Python 3.12+ is required; the start script also detects this Codex installation's bundled Python.

```powershell
.\scripts\Start-Local.ps1
```

For a new machine, install Python 3.12+, run `scripts/Install-Local.ps1`, then `scripts/Start-Local.ps1`. The runtime and model require several GB of disk space. A CUDA-compatible GPU is recommended for interactive response times.

With Quest in developer mode and USB debugging authorized:

```powershell
adb reverse tcp:8765 tcp:8765
adb install -r Unity\Builds\NegotiableAR.apk
adb shell am start -n org.research.negotiablear/com.unity3d.player.UnityPlayerGameActivity
```

This workspace also has ADB at `.tools/android-sdk/platform-tools/adb.exe`. The default Unity service URL is `http://127.0.0.1:8765`; `adb reverse` maps that address from Quest to the PC. Grant headset-camera and spatial-data access in the headset. Keep the PC service running. RGB access should be tested with a standalone APK; ordinary editor Play mode does not provide a Quest camera feed.

For Wi-Fi, run `scripts/Start-Local.ps1 -Lan -Token '<chosen token>'`; set `AgentSettings.serviceUrl` to the PC's LAN address and `accessToken` to the same token. Add that exact PC address as a `<domain>` in `Assets/NegotiableAR/Network/local_bridge_security.xml` and rebuild, or use HTTPS. The shipped network configuration permits HTTP only to localhost for USB forwarding. Allow the service on the Windows **private** network if prompted. Do not commit tokens in settings assets.

## Configure and build

Select `Assets/NegotiableAR/Resources/AgentSettings.asset` to edit the task goal, distinctive pieces, constraints, timing, image size, speech, and service endpoint. The default task has no single correct arrangement. A task-specific success detector has not been implemented.

The generated scene and Android OpenXR settings are included. **Negotiable AR → Create or rebuild Quest scene** regenerates the scene and wiring while preserving the settings asset. It replaces the generated scene; save custom scene changes separately. **Window → Meta → Tools → Project Setup Tool** shows Meta's platform checks.

```powershell
.\scripts\Build-Quest.ps1
```

The development APK is written to `Unity/Builds/NegotiableAR.apk`. Unity Hub must include Android Build Support, OpenJDK, Android SDK/NDK, and CMake. On this machine, the incomplete Android installation was supplemented under `.tools/android-sdk` (SDK 36, build tools 36.0.0, NDK r27c, CMake 3.22.1); the build script detects it. The Unity Android external-tool paths may point there after building when their previous paths were invalid.

Packages are pinned and locked: Meta XR Core/MRUK 85.0.0, Unity OpenXR 1.15.1, Unity OpenXR: Meta 2.3.0, and XR Management 4.5.4. The actual installed SDK sources were used to verify component names and settings.

## Headset controls

Point at a panel button and pinch, or point a controller and press its index trigger. To move the panel, aim at its **GRAB TO MOVE title bar**, hold the pinch or trigger, move it, and release. While dragging with a controller, the thumbstick adjusts its distance. Losing tracking releases the panel; a held gesture cannot activate another button when tracking returns. The panel stays where you place it.

The panel supports **Help**, **Yes, hint**, **Not now**, **Quiet**, **More often**, **Less often**, and **Resume**. Yes/Not now appear only while an offer is pending. There is no recenter button.

Controller shortcuts: **A** Help, **X** accept, **B** decline, **Y** Quiet. Editor keyboard equivalents are H/Y/N/Q. Start the app while facing the task; the panel and workspace reference are initially placed ahead of you. Dragging the panel does not move the workspace reference. The small mint cube is a depth-occlusion probe, not an inferred object location or detected table surface.

## Foreground task focus

Before encoding an image for the VLM, the GPU crops it to the lower central task area (76% of RGB width, 72% of height). When Meta environment depth is available, a range mask replaces surfaces beyond **1.4 m** with neutral gray. The mask reprojects the RGB camera's rays into left-eye environment depth using the timestamped RGB pose and Meta depth matrices; it does not assume RGB and depth pixels line up. A 12 cm transition tolerance softens the cutoff. It is a coarse background suppressor, not precise object segmentation.

The panel reports **Task crop + depth <1.4m** when depth masking is applied, or **Task crop (depth fallback)** when depth is unavailable. Unknown depth and areas outside depth coverage retain cropped RGB so missing measurements do not erase the task. Masking affects only the VLM input; the user's passthrough view stays intact.

In `AgentSettings.asset`, adjust `taskImageRegion` (normalized image coordinates, origin bottom-left), `foregroundDistanceMetres`, and `depthEdgeToleranceMetres` for your table. Keep pieces inside that lower central region, or widen it. Disable `useDepthBackgroundMask` for crop-only processing, or `focusForeground` to restore full-frame input. Distant task objects can be masked if the range is too short. The VLM also receives the processing mode and is instructed to omit room fixtures and support surfaces unless explicitly part of the task. Stable foreground pieces remain valid detections.

Active movement suppresses automatic offers. A pause can elicit an offer; silence does not accept it. Declined and ignored offers lengthen the interval. Help remains available even in quiet mode. Subtitles work if voice is unavailable.

## Verification

```powershell
dotnet run --project tests/PolicyChecks
python -m unittest discover -s server -v
python scripts/smoke_local.py
```

The first command needs .NET 10. The final optional smoke test needs a running Ollama instance; it uses the included synthetic fixture, not headset images. Core policy and HTTP tests cover stale data, tracking loss, quiet mode, consent, deadlines, overload, and provider contracts. See `docs/validation.md` for the actual validation results and remaining hardware checks.

## Boundaries

The VLM returns sampled item labels and scene descriptions, not a calibrated object detector or persistent tracker. Hand bones come directly from Meta tracking, independently of VLM response time. Depth drives both the test cube's occlusion and the coarse foreground mask; precise object-to-depth fusion and anchored object hints are future work. Model boxes are relative to the processed crop, not the full camera image. The current timing policy is deterministic and adaptive, while the VLM supplies candidate content. Voice is output-only; user responses use buttons or controller inputs.

See [architecture](docs/architecture.md) and [Quest validation](docs/validation.md).

## Official references

- [Meta Passthrough Camera API](https://developers.meta.com/vr/documentation/unity/unity-pca-documentation/) and [official samples](https://github.com/oculus-samples/Unity-PassthroughCameraApiSamples).
- [Meta Depth API samples](https://github.com/oculus-samples/Unity-DepthAPI).
- [Qwen3-VL 2B Instruct](https://ollama.com/library/qwen3-vl:2b-instruct) and [Ollama chat API](https://docs.ollama.com/api/chat).
- [Unity Android dependency versions](https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/android/getting-started/sdksetup/supported-dependency-versions).

Meta SDK packages and local model/runtime downloads retain their upstream licenses. They are not vendored into the repository.
