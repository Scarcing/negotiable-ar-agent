# Validation record — 29 October 2026 (Hong Kong)

## Completed

- Unity Editor **6000.3.27f1** resolved the pinned packages and compiled the runtime/editor scripts.
- The generated `QuestAgent.unity` scene and Android OpenXR configuration were saved successfully.
- A development **ARM64 / IL2CPP / Vulkan** APK built successfully with Android API 36 and minimum API 32.
  - Output: `Unity/Builds/NegotiableAR.apk` (84,498,999 bytes).
  - SHA-256: `36e36fc785a9d36c91886320ea07ef07b8a9c590a3387f848e08a97c2cc1ec33`.
- Packaged manifest inspection confirmed `org.research.negotiablear`, `UnityPlayerGameActivity`, the headset-camera permission, spatial-data permission, and ARM64-only native libraries.
- The packaged GameActivity uses `BaseUnityGameActivityTheme`. Both runtime permission requests are enabled on the generated OVRManager.
- Packaged network-security XML retains a default HTTP block and allows HTTP only to `127.0.0.1` and `localhost`. This supports the documented USB-forwarding setup.
- **20 pure C# policy checks passed**: ambiguous pauses, no implicit acceptance, offer expiry, acceptance races, stale/uncertain evidence, movement, tracking loss, look-away, refusal, persistent quiet, direct requests, on-request mode, and bounded adaptive frequency.
- **13 Python tests passed**: schema validation, invalid optional boxes, provider request contracts, authentication, overload, timeout/unavailability, observation and voice HTTP handling.
- PowerShell scripts parsed successfully; Python modules compiled; whitespace checks passed.
- Portable **Ollama 0.40.13** and **Qwen3-VL 2B Instruct Q4_K_M** were installed locally. Ollama detected the **RTX 5080, 16 GB VRAM**.
- Actual `/observe` HTTP calls recognized a synthetic red triangle, blue square and yellow parallelogram. The final three warmed round trips took **1,407 ms, 1,187 ms and 1,172 ms**. This repeated-image test benefits from caching and is not a headset benchmark.
- Actual `/speak` HTTP output was a valid **162,008-byte RIFF/WAV** from Windows System.Speech.
- The shipped warmup script loaded the model and vision encoder successfully. Model cold start was approximately 23 seconds during initial testing; perform warmup before participants begin.

The small VLM sometimes returns the wrong coordinate scale despite the prompt. Invalid boxes are explicitly marked `boundsValid=false` and receive no usable location; their coarse labels remain available. The final smoke run exercised this fallback. The headset panel displays labels and summary text, not spatial boxes. A task phase or visible-action description from one image may also be wrong; those descriptions do not drive the movement/consent policy.

The online-compatible provider was tested with mocked HTTP output only. No remote model account was configured or billed.

## Pending Quest hardware validation

ADB reported **no connected device**. The following are required before using this prototype with participants:

1. Connect Quest 3 with developer mode and USB debugging, start the local bridge, run `adb reverse tcp:8765 tcp:8765`, install the APK, and launch it.
2. Grant headset-camera and spatial-data permission. Verify the panel reports RGB ready and Depth ready. Reject each permission in a separate run to check the waiting state and continued explicit Help availability.
3. Inspect a recognizable tabletop image through VLM summaries. Confirm image orientation, colors, crop, and actual camera-to-service latency using the session log. Tune resolution and sample interval based on Quest profiling.
4. Inspect both hand skeletons and confidence behavior. Occlude a hand and remove both hands; neither should create a synthetic pause. Test left/right hand input and right-controller pointing.
5. Start facing the task and drag the panel by its title bar to keep it out of the way. Place a hand or real object between the headset and the small virtual cube; verify depth occlusion. Its fixed offset is a test placement, not a detected table anchor.
6. Pause with reliable visible hands and fresh perception. The agent should offer help after the threshold, wait for explicit acceptance, and back off after an ignored offer. Verify subtitles and audible speech.
7. Accept, decline, request help while moving, select Quiet, request help while quiet, resume, and change offer frequency. Confirm the session records the intended actions.
8. Pause/resume the app; disconnect and reconnect the bridge; stop Ollama; restore it. Verify stale data is marked and does not trigger unsolicited guidance.
9. Run a sustained session with actual candidate pieces, lighting changes and occlusion. Record dropped frames, encoding cost, thermal behavior, recognition failures and inappropriate hints before selecting the task or intervention thresholds.

## Reproduce

```powershell
dotnet run --project tests/PolicyChecks
python -m unittest discover -s server -v
python server/warmup.py
python scripts/smoke_local.py
.\scripts\Build-Quest.ps1
```

The last three commands require the installed local runtime/toolchain. `scripts/Start-Local.ps1` performs model download checks and warmup before starting the actual bridge. The smoke test starts and stops its own temporary localhost server; it leaves no bridge process running. The Ollama server may remain running for the next session.

## Connected-device smoke test — 30 October 2026

Quest 3 was connected and authorized through ADB. The development APK was installed successfully, USB forwarding for port 8765 was configured, and `UnityPlayerGameActivity` launched successfully. The local VLM was warmed and the bridge was started on localhost. Headset-camera and spatial-data permissions were granted through the headset prompts.

Live headset frames reached the VLM: the app's `perception_timing` records showed model response times of roughly **1.5–1.8 seconds**, with successful HTTP responses from Ollama. The app remained running without a fatal exception. This verifies the physical-device RGB-to-local-model path, not recognition accuracy or total user-perceived latency.

One environment-depth `XR_ERROR_CALL_ORDER_INVALID` error appeared during startup around permission initialization. After one app restart with permissions already granted, no Unity/Android runtime errors appeared in the checked startup logs, and live inference resumed at roughly 1.6–1.8 seconds. Visual depth occlusion, hand/finger tracking, pointing/pinch interactions, and audible headset speech still require the in-headset checks above. The agent initially withheld unsolicited help because engagement evidence was insufficient, which is expected when reliable hands/workspace engagement have not been established.

## Draggable panel and foreground filtering — 2 November 2026

The recenter button was replaced by a draggable title bar. Hold a pinch or controller trigger on **GRAB TO MOVE**, move the pointer, and release. Controller thumbsticks adjust distance during a drag. The grabbed canvas point remains anchored to the pointer despite canvas scale and rotation. Tracking loss ends the drag and a still-held gesture cannot activate buttons after reacquisition or switching hands/controllers. Panel placement does not alter the task workspace reference or consent state.

VLM images are now GPU-cropped before encoding to the lower central 76% × 72% of the camera image. When depth is valid, the same pass suppresses surfaces beyond the default 1.4 m range, with a 12 cm transition tolerance. It projects a range boundary along calibrated RGB camera rays into left-eye environment depth; it does not use unaligned RGB UVs to sample depth. Missing/invalid depth and uncovered pixels preserve the crop. The full passthrough view is unaffected. Focus metadata is passed to the service, and the VLM is instructed to ignore room fixtures/support surfaces unless the task includes them. Inferred boxes remain relative to the processed image.

Validation completed:

- **28 policy/pointer checks passed**, including held-input behavior, drag release, tracking loss, and switching input sources without synthetic clicks.
- **17 Python service tests passed**, including crop/depth metadata, older-client compatibility, and rejection of invalid crop or inconsistent depth-mask claims.
- **14 Unity checks passed on the GPU**, including scaled/rotated canvas dragging, preserved grab anchors, distance adjustment, release behavior, actual crop pixel exclusion, far-depth masking, near-depth preservation, selection of the left depth slice, depth holes, invalid denominators, out-of-coverage fallback, and disabled-depth fallback.
- The Android build compiled the new foreground shader successfully. `Negotiable AR → Run drag and image checks` reproduces the Unity checks.

The Quest was initially disconnected, then reconnected during the update. The final APK was installed successfully, USB forwarding restored, the updated local bridge started, and the app launched. Live session records confirmed **`foreground=crop_depth`**, proving that valid depth was available and the GPU depth-mask path was selected for frames that reached the VLM. Sample model times were **890 ms and 1,000 ms**; they are scene-dependent samples, not an accuracy or latency benchmark. The initial depth call-order warning appeared again around startup, but depth became available and live observations followed. No fatal app exception was observed in the checked startup logs.

Final APK: `Unity/Builds/NegotiableAR.apk`, 84,513,730 bytes. SHA-256: `9050b98ea1c10c5027d678b60f7326e5877e5ee4720c8d1d7a308374b8088e29`.

In-headset drag testing, visual RGB/depth alignment, range tuning, retained task pieces, and room-background suppression on real tasks still require user observation. Depth is a coarse range gate rather than semantic foreground segmentation: nearby background surfaces can remain, distant task pieces can be removed, and crop edges can clip pieces if the configured region is too small. Adjust the task region/range in `AgentSettings.asset` for the activity.
