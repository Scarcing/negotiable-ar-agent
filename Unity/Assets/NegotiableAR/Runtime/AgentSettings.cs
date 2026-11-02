using UnityEngine;

namespace NegotiableAR
{
    [CreateAssetMenu(menuName = "Negotiable AR/Settings")]
    public sealed class AgentSettings : ScriptableObject
    {
        [Tooltip("For USB testing use adb reverse tcp:8765 tcp:8765. For Wi-Fi use the PC's LAN IP.")]
        public string serviceUrl = "http://127.0.0.1:8765";
        public string accessToken = "";
        public TaskProfile task = new TaskProfile();
        public AgentConfig policy = new AgentConfig();
        public float sampleInterval = 1.5f;
        public float requestTimeoutSeconds = 6;
        public int imageWidth = 512;
        [Range(25, 90)] public int jpegQuality = 65;
        [Header("Task foreground")]
        public bool focusForeground = true;
        [Tooltip("Normalized RGB image region, with origin at the bottom-left. Excludes upper room background.")]
        public Rect taskImageRegion = new Rect(0.12f, 0.04f, 0.76f, 0.72f);
        public bool useDepthBackgroundMask = true;
        [Range(0.4f, 3f)] public float foregroundDistanceMetres = 1.4f;
        [Range(0.02f, 0.3f)] public float depthEdgeToleranceMetres = 0.12f;
        public bool showJointMarkers = true;
        public bool speak = true;
        public bool logObservations = false;
    }
}
