using System;
using System.Collections.Generic;
using Meta.XR;
using Meta.XR.EnvironmentDepth;
using UnityEngine;
using UnityEngine.Rendering;

namespace NegotiableAR
{
    [Serializable] public class JointSample { public string id; public Vector3 position; }
    [Serializable] public class FocusSample
    {
        public string mode = "full";
        public bool depthMasked;
        public float maxDistanceMetres;
        // Crop in the ORIGINAL RGB image, origin bottom-left. Model boxes remain
        // normalized to the processed image it actually receives, origin top-left.
        public float cropX, cropY, cropWidth = 1, cropHeight = 1;
    }
    [Serializable] public class HandSample
    {
        public string side;
        public bool tracked, reliable, pinching;
        public JointSample[] joints = Array.Empty<JointSample>();
    }

    public sealed class MetaPerception : MonoBehaviour
    {
        public PassthroughCameraAccess cameraAccess;
        public EnvironmentDepthManager depth;
        public OVRCameraRig rig;
        public OVRHand leftHand, rightHand;
        public OVRSkeleton leftSkeleton, rightSkeleton;
        public AgentSettings settings;
        public HandSample[] Hands { get; private set; } = { new HandSample(), new HandSample() };
        public ActivityEvidence Activity { get; private set; }
        public bool DepthReady => depth != null && depth.IsDepthAvailable;
        public bool CameraReady => cameraAccess != null && cameraAccess.IsPlaying;
        public string Status { get; private set; } = "Waiting for Meta camera permission";
        public string FocusStatus { get; private set; } = "Task crop";
        public Transform Workspace;
        readonly Dictionary<string, Vector3> previous = new Dictionary<string, Vector3>();
        readonly Dictionary<string, Transform> markers = new Dictionary<string, Transform>();
        double lastMotion;
        bool reliableLastFrame;
        bool captureBusy;
        long lastCameraTicks;
        RenderTexture resized;
        Texture2D cpu;
        Material foreground;

        void Start() { lastMotion = Time.realtimeSinceStartupAsDouble; }

        void Update()
        {
            bool moving = false;
            Hands = new[] { Sample("left", leftHand, leftSkeleton, ref moving), Sample("right", rightHand, rightSkeleton, ref moving) };
            bool reliable = Hands[0].reliable || Hands[1].reliable;
            // Tracking loss/reacquisition must never manufacture evidence of a thinking pause.
            if (moving || !reliable || !reliableLastFrame) lastMotion = Time.realtimeSinceStartupAsDouble;
            reliableLastFrame = reliable;
            Transform head = rig != null ? rig.centerEyeAnchor : transform;
            bool looking = Workspace != null && Vector3.Angle(head.forward, Workspace.position - head.position) < 55;
            Activity = new ActivityEvidence { HandsReliable = reliable, HandsMoving = moving, LookingAtWorkspace = looking, LastMovementTime = lastMotion };
            Status = $"RGB {(CameraReady ? "ready" : "waiting / permission")} | Depth {(DepthReady ? "ready" : "unavailable")} | Hands {(reliable ? (moving ? "moving" : "still") : "not reliable")} | {FocusStatus}";
        }

        HandSample Sample(string side, OVRHand hand, OVRSkeleton skeleton, ref bool moving)
        {
            var sample = new HandSample { side = side, tracked = hand != null && hand.IsTracked };
            sample.reliable = sample.tracked && hand.IsDataHighConfidence && skeleton != null && skeleton.IsDataValid;
            sample.pinching = sample.reliable && hand.GetFingerIsPinching(OVRHand.HandFinger.Index);
            var joints = new List<JointSample>();
            if (sample.reliable)
            {
                foreach (var bone in skeleton.Bones)
                {
                    if (bone.Transform == null) continue;
                    string key = side + "/" + bone.Id;
                    Vector3 p = bone.Transform.position;
                    // Speed, rather than per-frame displacement, works across headset frame rates.
                    if (previous.TryGetValue(key, out var before) && Vector3.Distance(before, p) / Mathf.Max(Time.unscaledDeltaTime, 0.001f) > 0.12f) moving = true;
                    previous[key] = p;
                    joints.Add(new JointSample { id = bone.Id.ToString(), position = p });
                    if (settings.showJointMarkers)
                    {
                        if (!markers.TryGetValue(key, out var marker))
                        {
                            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                            sphere.name = key; Destroy(sphere.GetComponent<Collider>());
                            marker = sphere.transform; marker.SetParent(transform); marker.localScale = Vector3.one * 0.006f;
                            markers[key] = marker;
                        }
                        marker.gameObject.SetActive(true); marker.position = p;
                    }
                }
            }
            foreach (var pair in markers)
                if (pair.Key.StartsWith(side + "/") && (!sample.reliable || !settings.showJointMarkers)) pair.Value.gameObject.SetActive(false);
            if (!sample.reliable)
                foreach (var key in new List<string>(previous.Keys)) if (key.StartsWith(side + "/")) previous.Remove(key);
            sample.joints = joints.ToArray();
            return sample;
        }

        public bool Capture(Action<byte[], string, double, FocusSample> complete)
        {
            if (captureBusy || !CameraReady) return false;
            long cameraTicks = cameraAccess.Timestamp.Ticks;
            if (cameraTicks == 0 || cameraTicks == lastCameraTicks) return false;
            Texture texture = cameraAccess.GetTexture();
            if (texture == null) return false;
            lastCameraTicks = cameraTicks;
            Rect crop = new Rect(0, 0, 1, 1);
            if (settings.focusForeground)
            {
                float x = Mathf.Clamp(settings.taskImageRegion.x, 0, 0.9f);
                float y = Mathf.Clamp(settings.taskImageRegion.y, 0, 0.9f);
                crop = new Rect(x, y, Mathf.Clamp(settings.taskImageRegion.width, 0.1f, 1 - x), Mathf.Clamp(settings.taskImageRegion.height, 0.1f, 1 - y));
            }
            var focus = new FocusSample { mode = settings.focusForeground ? "crop" : "full", cropX = crop.x, cropY = crop.y, cropWidth = crop.width, cropHeight = crop.height };
            int width = Mathf.Clamp(settings.imageWidth, 128, 1024);
            int height = Mathf.Max(1, Mathf.RoundToInt(texture.height * crop.height / (texture.width * crop.width) * width));
            if (resized == null || resized.width != width || resized.height != height)
            {
                if (resized != null) { resized.Release(); Destroy(resized); }
                if (cpu != null) Destroy(cpu);
                resized = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
                resized.Create(); cpu = new Texture2D(width, height, TextureFormat.RGBA32, false);
            }
            if (settings.focusForeground)
            {
                if (foreground == null)
                {
                    var shader = Resources.Load<Shader>("TaskForeground");
                    if (shader != null && shader.isSupported) foreground = new Material(shader);
                }
                // Always preserve the crop, including on devices without this shader.
                if (foreground == null) Graphics.Blit(texture, resized, crop.size, crop.position);
                else
                {
                    foreground.SetVector("_CropRect", new Vector4(crop.x, crop.y, crop.width, crop.height));
                    bool masked = ConfigureDepthFilter();
                    foreground.SetFloat("_UseTaskDepth", masked ? 1 : 0);
                    focus.depthMasked = masked;
                    focus.maxDistanceMetres = masked ? Mathf.Clamp(settings.foregroundDistanceMetres, 0.4f, 3) : 0;
                    if (masked) focus.mode = "crop_depth";
                    Graphics.Blit(texture, resized, foreground);
                }
            }
            else Graphics.Blit(texture, resized);
            FocusStatus = focus.depthMasked ? $"Task crop + depth <{focus.maxDistanceMetres:0.0}m" : (settings.focusForeground ? "Task crop (depth fallback)" : "Full image");
            string frameId = cameraTicks.ToString();
            double capturedAt = Time.realtimeSinceStartupAsDouble;
            captureBusy = true;
            if (SystemInfo.supportsAsyncGPUReadback)
            {
                AsyncGPUReadback.Request(resized, 0, TextureFormat.RGBA32, result =>
                {
                    captureBusy = false;
                    if (this == null) return;
                    if (result.hasError) { complete(null, frameId, capturedAt, focus); return; }
                    cpu.LoadRawTextureData(result.GetData<byte>()); cpu.Apply(false);
                    complete(cpu.EncodeToJPG(settings.jpegQuality), frameId, capturedAt, focus);
                });
            }
            else
            {
                var prior = RenderTexture.active;
                try { RenderTexture.active = resized; cpu.ReadPixels(new Rect(0, 0, width, height), 0, 0); cpu.Apply(false); }
                finally { RenderTexture.active = prior; captureBusy = false; }
                complete(cpu.EncodeToJPG(settings.jpegQuality), frameId, capturedAt, focus);
            }
            return true;
        }

        bool ConfigureDepthFilter()
        {
            if (!settings.useDepthBackgroundMask || !DepthReady) return false;
            var depthTexture = Shader.GetGlobalTexture("_EnvironmentDepthTexture");
            var matrices = Shader.GetGlobalMatrixArray("_EnvironmentDepthReprojectionMatrices");
            var parameters = Shader.GetGlobalVector("_EnvironmentDepthZBufferParams");
            if (depthTexture == null || depthTexture.dimension != TextureDimension.Tex2DArray || matrices == null || matrices.Length == 0 || Mathf.Abs(parameters.x) < 0.000001f) return false;
            Pose pose = cameraAccess.GetCameraPose();
            Transform tracking = rig != null ? rig.trackingSpace : null;
            Vector3 origin = tracking != null ? tracking.TransformPoint(pose.position) : pose.position;
            Vector3 forward = pose.rotation * Vector3.forward;
            Vector3 Corner(float x, float y)
            {
                Vector3 direction = cameraAccess.ViewportPointToRay(new Vector2(x, y), pose).direction;
                // Recover pinhole (z=1) directions before interpolation. Interpolating
                // normalized corner rays would distort an asymmetric camera frustum.
                direction /= Mathf.Max(0.001f, Vector3.Dot(direction, forward));
                return tracking != null ? tracking.TransformVector(direction) : direction;
            }
            foreground.SetTexture("_TaskDepthTexture", depthTexture);
            foreground.SetMatrix("_TaskDepthProjection", matrices[0]);
            foreground.SetVector("_TaskDepthParameters", parameters);
            foreground.SetVector("_RgbOrigin", origin);
            foreground.SetVector("_RgbRay00", Corner(0, 0));
            foreground.SetVector("_RgbRay10", Corner(1, 0));
            foreground.SetVector("_RgbRay01", Corner(0, 1));
            foreground.SetVector("_RgbRay11", Corner(1, 1));
            foreground.SetFloat("_TaskRange", Mathf.Clamp(settings.foregroundDistanceMetres, 0.4f, 3));
            foreground.SetFloat("_TaskEdgeTolerance", Mathf.Clamp(settings.depthEdgeToleranceMetres, 0.02f, 0.3f));
            return true;
        }

        void OnDestroy()
        {
            // Ensure pending readback has finished before freeing its target.
            if (captureBusy) AsyncGPUReadback.WaitAllRequests();
            if (resized != null) { resized.Release(); Destroy(resized); }
            if (cpu != null) Destroy(cpu);
            if (foreground != null) Destroy(foreground);
        }
    }
}
