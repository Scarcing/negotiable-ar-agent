using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace NegotiableAR
{
    public sealed class PerceptionClient : MonoBehaviour
    {
        [Serializable] class FrameRequest
        {
            public string frameId, imageBase64;
            public TaskProfile task;
            public HandSample[] hands;
            public bool depthAvailable;
            public FocusSample foreground;
        }
        public AgentSettings settings;
        public MetaPerception perception;
        public event Action<Observation, double> Observed;
        public string Status { get; private set; } = "Waiting for camera";
        bool busy;
        double nextCapture;
        UnityWebRequest active;

        void Update()
        {
            if (busy || Time.realtimeSinceStartupAsDouble < nextCapture) return;
            if (!perception.CameraReady) return;
            busy = true;
            nextCapture = Time.realtimeSinceStartupAsDouble + settings.sampleInterval;
            // Freeze hand metadata at capture time, not after network latency.
            var hands = perception.Hands;
            bool depthReady = perception.DepthReady;
            if (!perception.Capture((bytes, frameId, capturedAt, focus) =>
                {
                    if (this == null || !isActiveAndEnabled) { busy = false; return; }
                    if (bytes == null) { busy = false; Status = "Camera readback failed; retrying"; return; }
                    StartCoroutine(Send(bytes, frameId, capturedAt, hands, depthReady, focus));
                })) busy = false;
        }

        IEnumerator Send(byte[] jpeg, string frameId, double capturedAt, HandSample[] hands, bool depthReady, FocusSample focus)
        {
            var body = new FrameRequest { frameId = frameId, imageBase64 = Convert.ToBase64String(jpeg), task = settings.task, hands = hands, depthAvailable = depthReady, foreground = focus };
            using (var request = new UnityWebRequest(settings.serviceUrl.TrimEnd('/') + "/observe", "POST"))
            {
                active = request;
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrEmpty(settings.accessToken)) request.SetRequestHeader("Authorization", "Bearer " + settings.accessToken);
                request.timeout = Mathf.CeilToInt(settings.requestTimeoutSeconds);
                Status = "Observing…";
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success) Status = "VLM unavailable: " + request.responseCode;
                else
                {
                    Observation result = null;
                    try { result = JsonUtility.FromJson<Observation>(request.downloadHandler.text); }
                    catch (Exception e) { Status = "Invalid observation: " + e.Message; }
                    if (result != null && result.frameId == frameId)
                    {
                        double age = Time.realtimeSinceStartupAsDouble - capturedAt;
                        if (age <= settings.policy.staleSeconds && !float.IsNaN(result.confidence))
                        {
                            Status = $"VLM {result.latencyMs:0} ms | {result.items?.Length ?? 0} items | {result.confidence:P0}";
                            Observed?.Invoke(result, capturedAt);
                        }
                        else Status = "Discarded stale observation";
                    }
                }
                active = null;
            }
            busy = false;
            nextCapture = Math.Max(nextCapture, Time.realtimeSinceStartupAsDouble + 0.1);
        }

        void OnDisable() { if (active != null) active.Abort(); StopAllCoroutines(); active = null; busy = false; }
    }
}
