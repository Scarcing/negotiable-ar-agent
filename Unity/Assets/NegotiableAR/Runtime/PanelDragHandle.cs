using UnityEngine;
using UnityEngine.UI;

namespace NegotiableAR
{
    public sealed class PanelDragHandle : MonoBehaviour
    {
        public Transform panel;
        public Transform head;
        public bool IsDragging { get; private set; }
        public Vector3 GrabWorldPoint => panel != null ? panel.TransformPoint(localGrabPoint) : transform.position;
        Vector3 localGrabPoint;
        float grabDistance;
        Image surface;
        readonly Color resting = new Color(0.08f, 0.18f, 0.22f);

        public void BeginDrag(Ray ray, Vector3 hitPoint)
        {
            if (panel == null) return;
            localGrabPoint = panel.InverseTransformPoint(hitPoint);
            grabDistance = Mathf.Clamp(Vector3.Distance(ray.origin, hitPoint), 0.25f, 3);
            IsDragging = true;
            surface = GetComponent<Image>();
            if (surface != null) surface.color = new Color(0.1f, 0.42f, 0.43f);
        }

        public void MoveDrag(Ray ray, float distanceChange = 0)
        {
            if (!IsDragging || panel == null) return;
            grabDistance = Mathf.Clamp(grabDistance + distanceChange, 0.25f, 3);
            Vector3 target = ray.GetPoint(grabDistance);
            // Keep the grabbed title-bar point under the pointer as the panel turns.
            if (head != null && (target - head.position).sqrMagnitude > 0.01f)
                panel.rotation = Quaternion.LookRotation(target - head.position, Vector3.up);
            panel.position = target - panel.TransformVector(localGrabPoint);
        }

        public void EndDrag()
        {
            IsDragging = false;
            if (surface != null) surface.color = resting;
        }

        void OnDisable() { EndDrag(); }
    }
}
