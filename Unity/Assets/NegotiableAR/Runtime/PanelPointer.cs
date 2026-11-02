using UnityEngine;
using UnityEngine.UI;

namespace NegotiableAR
{
    public sealed class PanelPointer : MonoBehaviour
    {
        public OVRHand left, right;
        public OVRCameraRig rig;
        LineRenderer line;
        readonly PointerGesture gesture = new PointerGesture();
        PanelDragHandle dragging;
        int selectedSource;
        float nextClick;
        void Start()
        {
            line = gameObject.AddComponent<LineRenderer>(); line.positionCount = 2;
            line.startWidth = 0.002f; line.endWidth = 0.001f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = line.endColor = new Color(0.3f, 0.9f, 0.8f);
        }
        void Update()
        {
            var active = OVRInput.GetActiveController();
            Transform source = null;
            bool down = false;
            float distanceChange = 0;
            int sourceId = 0;
            bool rightHandReady = right != null && right.IsDataHighConfidence && right.IsPointerPoseValid;
            bool leftHandReady = left != null && left.IsDataHighConfidence && left.IsPointerPoseValid;
            // Keep the active grab bound to its original hand/controller until release.
            if (dragging != null) sourceId = selectedSource;
            else if ((active & OVRInput.Controller.Touch) != 0)
            {
                // Meta can report both Touch controllers as the active controller.
                // Let the actually pressed trigger select the ray, including the left.
                bool rightReady = OVRInput.GetControllerPositionTracked(OVRInput.Controller.RTouch);
                bool leftReady = OVRInput.GetControllerPositionTracked(OVRInput.Controller.LTouch);
                if (leftReady && OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch) &&
                    !(rightReady && OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))) sourceId = 2;
                else if (rightReady) sourceId = 1;
                else if (leftReady) sourceId = 2;
            }
            else if (leftHandReady && left.GetFingerIsPinching(OVRHand.HandFinger.Index) &&
                     !(rightHandReady && right.GetFingerIsPinching(OVRHand.HandFinger.Index))) sourceId = 4;
            else if (rightHandReady) sourceId = 3;
            else if (leftHandReady) sourceId = 4;
            if (sourceId == 1 || sourceId == 2)
            {
                var controller = sourceId == 1 ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch;
                if ((OVRInput.GetConnectedControllers() & controller) != 0 && OVRInput.GetControllerPositionTracked(controller))
                {
                    source = sourceId == 1 ? rig.rightControllerAnchor : rig.leftControllerAnchor;
                    down = OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, controller);
                    distanceChange = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, controller).y * Time.unscaledDeltaTime * 0.6f;
                }
            }
            else if (sourceId == 3 || sourceId == 4)
            {
                var hand = sourceId == 3 ? right : left;
                if (hand != null && hand.IsDataHighConfidence && hand.IsPointerPoseValid)
                {
                    source = hand.PointerPose;
                    down = hand.GetFingerIsPinching(OVRHand.HandFinger.Index);
                }
            }
            if (source == null) sourceId = 0;
            var phase = gesture.Update(sourceId, down);
            if (dragging != null && (phase == PointerPhase.Released || source == null))
            {
                dragging.EndDrag(); dragging = null;
            }
            line.enabled = source != null;
            if (source == null) return;
            var ray = new Ray(source.position, source.forward);
            line.SetPosition(0, ray.origin);
            if (dragging != null)
            {
                dragging.MoveDrag(ray, distanceChange);
                line.SetPosition(1, dragging.GrabWorldPoint);
                return;
            }
            if (Physics.Raycast(ray, out var hit, 3))
            {
                line.SetPosition(1, hit.point);
                var button = hit.collider.GetComponent<Button>();
                var handle = hit.collider.GetComponent<PanelDragHandle>();
                if (phase == PointerPhase.Pressed && handle != null)
                {
                    dragging = handle; selectedSource = sourceId;
                    dragging.BeginDrag(ray, hit.point);
                }
                else if (button != null && phase == PointerPhase.Pressed && Time.unscaledTime > nextClick)
                {
                    button.onClick.Invoke(); nextClick = Time.unscaledTime + 0.4f;
                }
            }
            else line.SetPosition(1, ray.GetPoint(1.5f));
        }
        void OnDisable() { if (dragging != null) dragging.EndDrag(); dragging = null; gesture.Update(0, false); }
        void OnDestroy() { if (line != null) Destroy(line.material); }
    }
}
