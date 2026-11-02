using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace NegotiableAR
{
    public sealed class AgentController : MonoBehaviour
    {
        public AgentSettings settings;
        public MetaPerception perception;
        public PerceptionClient client;
        public AgentPanel panel;
        public AudioSource voice;
        public NegotiationPolicy Policy { get; private set; }
        public Observation Latest { get; private set; }
        public double LatestCaptureTime { get; private set; }
        SessionLog log;
        Coroutine speaking;
        UnityWebRequest speechRequest;
        AudioClip ownedClip;
        double lastSilentLog;
        [System.Serializable] class SpeechBody { public string text; }
        [System.Serializable] class SessionConfiguration
        {
            public string unityVersion;
            public TaskProfile task;
            public AgentConfig policy;
            public int imageWidth;
            public float sampleInterval;
        }

        void Start()
        {
            Policy = new NegotiationPolicy(settings.policy);
            log = new SessionLog();
            log.Write("session_started", JsonUtility.ToJson(new SessionConfiguration { unityVersion = Application.unityVersion, task = settings.task, policy = settings.policy, imageWidth = settings.imageWidth, sampleInterval = settings.sampleInterval }));
            client.Observed += OnObserved;
            panel.Show("Explore at your own pace. Ask for help whenever you want.");
        }

        void OnObserved(Observation observation, double capturedAt)
        {
            Latest = observation; LatestCaptureTime = capturedAt; Policy.Observe(observation, capturedAt);
            if (settings.logObservations) log.Write("observation", JsonUtility.ToJson(observation));
            else log.Write("perception_timing", $"frame={observation.frameId}; latencyMs={observation.latencyMs}; confidence={observation.confidence}; foreground={observation.foregroundMode}");
        }

        void Update()
        {
            if (Policy == null) return;
            bool wasPending = Policy.OfferPending;
            Handle(Policy.Tick(Time.realtimeSinceStartupAsDouble, perception.Activity));
            if (wasPending && !Policy.OfferPending) panel.Show("Take your time. Ask for help whenever you want.");
            panel.Refresh(this, perception.Status, client.Status);
            if (Input.GetKeyDown(KeyCode.H) || OVRInput.GetDown(OVRInput.Button.One)) Signal(UserSignal.RequestHelp);
            if (Input.GetKeyDown(KeyCode.Y) || OVRInput.GetDown(OVRInput.Button.Three)) Signal(UserSignal.Accept);
            if (Input.GetKeyDown(KeyCode.N) || OVRInput.GetDown(OVRInput.Button.Two)) Signal(UserSignal.Decline);
            if (Input.GetKeyDown(KeyCode.Q) || OVRInput.GetDown(OVRInput.Button.Four)) Signal(UserSignal.Quiet);
        }

        public void Signal(UserSignal signal)
        {
            if (Policy == null) return;
            log.Write("user_signal", signal.ToString());
            if (signal == UserSignal.Quiet || signal == UserSignal.Decline || signal == UserSignal.Later) StopVoice();
            Handle(Policy.Signal(signal, Time.realtimeSinceStartupAsDouble));
        }

        void Handle(Decision decision)
        {
            if (decision.Action == AgentAction.Silent)
            {
                if (Time.realtimeSinceStartupAsDouble - lastSilentLog > 2)
                {
                    log.Write("withheld", decision.Reason); lastSilentLog = Time.realtimeSinceStartupAsDouble;
                }
                return;
            }
            log.Write("decision", $"{decision.Action}|{decision.What}|{decision.When}|{decision.Reason}|{decision.Text}");
            panel.Show(decision.Text);
            if (settings.speak)
            {
                StopVoice(); speaking = StartCoroutine(Speak(decision.Text));
            }
        }

        IEnumerator Speak(string text)
        {
            using (var request = UnityWebRequestMultimedia.GetAudioClip(settings.serviceUrl.TrimEnd('/') + "/speak", AudioType.WAV))
            {
                speechRequest = request;
                request.method = "POST";
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(new SpeechBody { text = text })));
                request.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrEmpty(settings.accessToken)) request.SetRequestHeader("Authorization", "Bearer " + settings.accessToken);
                request.timeout = 10;
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    ownedClip = DownloadHandlerAudioClip.GetContent(request); voice.clip = ownedClip; voice.Play();
                }
                else log.Write("voice_unavailable", request.responseCode.ToString());
                speechRequest = null;
            }
            speaking = null;
        }

        void StopVoice()
        {
            if (speechRequest != null) speechRequest.Abort();
            if (speaking != null) StopCoroutine(speaking);
            speechRequest = null; speaking = null;
            if (voice != null) { voice.Stop(); voice.clip = null; }
            if (ownedClip != null) Destroy(ownedClip);
            ownedClip = null;
        }
        void OnApplicationPause(bool paused) { if (paused && Policy != null) { StopVoice(); log.Write("application_paused", "true"); } }
        void OnDestroy() { StopVoice(); if (client != null) client.Observed -= OnObserved; log?.Dispose(); }
    }
}
