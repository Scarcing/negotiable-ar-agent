using UnityEngine;
using UnityEngine.UI;

namespace NegotiableAR
{
    // Built at runtime so the prototype does not depend on imported font/sample assets.
    public sealed class AgentPanel : MonoBehaviour
    {
        public Transform head;
        public AgentController agent;
        public MetaPerception perception;
        Text subtitle, diagnostics, observationText;
        GameObject accept, decline;
        Font font;
        string pendingText;

        void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            gameObject.AddComponent<CanvasScaler>();
            var rect = GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(700, 420);
            transform.localScale = Vector3.one * 0.0011f;
            var background = gameObject.AddComponent<Image>(); background.color = new Color(0.035f, 0.055f, 0.08f, 0.94f);
            var handle = new GameObject("Grab title bar to move", typeof(RectTransform));
            handle.transform.SetParent(transform, false);
            var handleRect = handle.GetComponent<RectTransform>();
            handleRect.anchorMin = handleRect.anchorMax = new Vector2(0, 1);
            handleRect.pivot = new Vector2(0, 1);
            handleRect.anchoredPosition = new Vector2(8, -6); handleRect.sizeDelta = new Vector2(684, 42);
            handle.AddComponent<Image>().color = new Color(0.08f, 0.18f, 0.22f);
            var grabCollider = handle.AddComponent<BoxCollider>();
            grabCollider.center = new Vector3(342, -21, 0); grabCollider.size = new Vector3(684, 42, 8);
            var drag = handle.AddComponent<PanelDragHandle>(); drag.panel = transform; drag.head = head;
            Label("NEGOTIABLE / AR       ···  GRAB TO MOVE", new Vector2(20, -16), new Vector2(650, 30), 19, new Color(0.35f, 0.9f, 0.8f));
            subtitle = Label("Explore at your own pace.", new Vector2(20, -54), new Vector2(660, 88), 25, Color.white);
            observationText = Label("Waiting for visual evidence…", new Vector2(20, -148), new Vector2(660, 50), 16, Color.gray);
            diagnostics = Label("", new Vector2(20, -202), new Vector2(660, 54), 14, Color.gray);
            Button("Help", 20, 270, () => agent.Signal(UserSignal.RequestHelp));
            accept = Button("Yes, hint", 185, 270, () => agent.Signal(UserSignal.Accept));
            decline = Button("Not now", 350, 270, () => agent.Signal(UserSignal.Decline));
            Button("Quiet", 515, 270, () => agent.Signal(UserSignal.Quiet));
            Button("More often", 20, 325, () => agent.Signal(UserSignal.MoreHelp));
            Button("Less often", 185, 325, () => agent.Signal(UserSignal.LessHelp));
            Button("Resume", 350, 325, () => agent.Signal(UserSignal.Resume));
            Label("Hold the title bar to move", new Vector2(515, -328), new Vector2(150, 50), 14, Color.gray);
            Label("Title bar: hold pinch / trigger + move · Buttons: point + pinch / trigger", new Vector2(20, -385), new Vector2(660, 24), 13, Color.gray);
            if (pendingText != null) subtitle.text = pendingText;
        }

        void Start() { PlaceInitially(); }

        Text Label(string content, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            var go = new GameObject(content, typeof(RectTransform)); go.transform.SetParent(transform, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = go.AddComponent<Text>(); text.font = font; text.text = content; text.fontSize = fontSize; text.color = color;
            text.raycastTarget = false;
            return text;
        }

        GameObject Button(string title, float x, float y, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(title, typeof(RectTransform)); go.transform.SetParent(transform, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(150, 44);
            go.AddComponent<Image>().color = new Color(0.12f, 0.24f, 0.29f);
            var button = go.AddComponent<UnityEngine.UI.Button>(); button.onClick.AddListener(action);
            var box = go.AddComponent<BoxCollider>(); box.center = new Vector3(75, -22, 0); box.size = new Vector3(150, 44, 8);
            var textGo = new GameObject("Label", typeof(RectTransform)); textGo.transform.SetParent(go.transform, false);
            var tr = textGo.GetComponent<RectTransform>(); tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
            var label = textGo.AddComponent<Text>(); label.font = font; label.text = title; label.fontSize = 19; label.color = Color.white; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
            return go;
        }

        void PlaceInitially()
        {
            if (head == null) return;
            Vector3 horizontal = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (horizontal.sqrMagnitude < 0.01f) horizontal = Vector3.forward;
            transform.position = head.position + horizontal * 1.1f + Vector3.up * -0.12f;
            transform.rotation = Quaternion.LookRotation(horizontal, Vector3.up);
            if (perception != null && perception.Workspace != null)
                perception.Workspace.position = head.position + horizontal * 0.7f - Vector3.up * 0.55f;
        }

        public void Show(string text) { pendingText = text; if (subtitle != null) subtitle.text = text; }
        public void Refresh(AgentController controller, string sensors, string model)
        {
            if (diagnostics == null) return;
            diagnostics.text = sensors + "\n" + model + $" | {controller.Policy.Mode} · spacing {controller.Policy.CooldownSeconds:0}s";
            accept.SetActive(controller.Policy.OfferPending); decline.SetActive(controller.Policy.OfferPending);
            if (controller.Latest != null)
            {
                string labels = "";
                foreach (var item in controller.Latest.items ?? System.Array.Empty<ItemObservation>())
                    labels += (labels.Length == 0 ? "" : ", ") + item.label;
                double age = Time.realtimeSinceStartupAsDouble - controller.LatestCaptureTime;
                observationText.text = (age > controller.settings.policy.staleSeconds ? "[STALE] " : "") + controller.Latest.summary + "\n" + labels;
            }
        }
    }
}
