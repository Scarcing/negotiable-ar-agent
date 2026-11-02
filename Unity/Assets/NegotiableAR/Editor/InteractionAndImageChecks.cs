using System;
using UnityEditor;
using UnityEngine;

namespace NegotiableAR.Editor
{
    public static class InteractionAndImageChecks
    {
        static int passed;
        static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("FAILED: " + name);
            passed++; Debug.Log("PASS: " + name);
        }

        [MenuItem("Negotiable AR/Run drag and image checks")]
        public static void Run()
        {
            passed = 0;
            CheckDrag(); CheckImage();
            Debug.Log($"NEGOTIABLE_AR_INTERACTION_IMAGE_CHECKS_OK: {passed} checks");
        }

        static void CheckDrag()
        {
            var panel = new GameObject("Drag check panel");
            var head = new GameObject("Drag check head");
            try
            {
                panel.transform.position = new Vector3(0, 1.5f, 1);
                panel.transform.localScale = Vector3.one * 0.0011f;
                head.transform.position = new Vector3(0, 1.6f, 0);
                var handle = panel.AddComponent<PanelDragHandle>(); handle.panel = panel.transform; handle.head = head.transform;
                Vector3 localGrab = new Vector3(0, 150, 0);
                Vector3 hit = panel.transform.TransformPoint(localGrab);
                var initialRay = new Ray(head.transform.position, hit - head.transform.position);
                float distance = Vector3.Distance(initialRay.origin, hit);
                handle.BeginDrag(initialRay, hit);
                Check(handle.IsDragging, "Grabbing title bar starts drag");
                var movedRay = new Ray(initialRay.origin + Vector3.right * 0.25f, initialRay.direction);
                handle.MoveDrag(movedRay);
                Check(panel.transform.position.x > 0.15f, "Held pointer moves world-space panel");
                Check(Vector3.Distance(panel.transform.TransformPoint(localGrab), movedRay.GetPoint(distance)) < 0.0001f, "Non-unit canvas scale and rotation preserve grab anchor");
                handle.MoveDrag(movedRay, 0.2f);
                Check(Vector3.Distance(panel.transform.TransformPoint(localGrab), movedRay.GetPoint(distance + 0.2f)) < 0.0001f, "Thumbstick can adjust panel distance during drag");
                handle.EndDrag();
                Vector3 stopped = panel.transform.position;
                handle.MoveDrag(new Ray(Vector3.zero, Vector3.left));
                Check(!handle.IsDragging && panel.transform.position == stopped, "Released panel remains in its placed position");
            }
            finally { UnityEngine.Object.DestroyImmediate(panel); UnityEngine.Object.DestroyImmediate(head); }
        }

        static void CheckImage()
        {
            var shader = Resources.Load<Shader>("TaskForeground");
            Check(shader != null && shader.isSupported, "Foreground shader is included and supported");
            var material = new Material(shader);
            var source = new Texture2D(4, 4, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point };
            var output = new RenderTexture(8, 8, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var cpu = new Texture2D(8, 8, TextureFormat.RGBA32, false, true);
            var depth = new Texture2DArray(2, 2, 2, TextureFormat.RFloat, false, true) { filterMode = FilterMode.Point };
            output.Create();
            var prior = RenderTexture.active;
            Color RenderPixel(int x = 4)
            {
                Graphics.Blit(source, output, material);
                RenderTexture.active = output; cpu.ReadPixels(new Rect(0, 0, 8, 8), 0, 0); cpu.Apply(false);
                return cpu.GetPixel(x, 4);
            }
            void DepthValue(float value)
            {
                var colors = new[] { new Color(value, 0, 0), new Color(value, 0, 0), new Color(value, 0, 0), new Color(value, 0, 0) };
                depth.SetPixels(colors, 0);
                // Opposite right-eye depth catches accidentally selecting the wrong slice.
                depth.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white }, 1);
                depth.Apply(false);
            }
            bool Blue(Color c) => c.b > 0.9f && c.r < 0.1f;
            try
            {
                var colors = new Color[16];
                for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++) colors[y * 4 + x] = x < 2 ? Color.red : Color.blue;
                source.SetPixels(colors); source.Apply(false);
                material.SetVector("_CropRect", new Vector4(0.5f, 0, 0.5f, 1));
                material.SetFloat("_UseTaskDepth", 0);
                Check(Blue(RenderPixel(1)) && Blue(RenderPixel(6)), "Crop removes excluded background pixels before readback");
                material.SetVector("_CropRect", new Vector4(0, 0, 1, 1));
                Check(RenderPixel(1).r > 0.9f && Blue(RenderPixel(6)), "Disabled crop preserves full-image coordinates");
                material.SetVector("_CropRect", new Vector4(0.5f, 0, 0.5f, 1));
                material.SetFloat("_UseTaskDepth", 1);
                material.SetTexture("_TaskDepthTexture", depth);
                var projection = Matrix4x4.zero; projection.m23 = 0.5f; projection.m33 = 1;
                material.SetMatrix("_TaskDepthProjection", projection);
                material.SetVector("_TaskDepthParameters", new Vector4(1, 0, 0, 0));
                material.SetVector("_RgbOrigin", Vector3.zero);
                material.SetVector("_RgbRay00", new Vector3(-1, -1, 1));
                material.SetVector("_RgbRay10", new Vector3(1, -1, 1));
                material.SetVector("_RgbRay01", new Vector3(-1, 1, 1));
                material.SetVector("_RgbRay11", new Vector3(1, 1, 1));
                material.SetFloat("_TaskRange", 1.4f); material.SetFloat("_TaskEdgeTolerance", 0.12f);
                DepthValue(0.625f); // Linear depth 4, beyond the test boundary at 2.
                Color far = RenderPixel();
                Check(Mathf.Abs(far.r - far.b) < 0.02f && far.b < 0.5f, "Distant left-eye depth is replaced with neutral gray");
                DepthValue(0.99f);
                Check(Blue(RenderPixel()), "Near foreground is preserved");
                DepthValue(0);
                Check(Blue(RenderPixel()), "Missing depth holes preserve cropped RGB");
                DepthValue(0.5f);
                Check(Blue(RenderPixel()), "Invalid depth denominator falls back safely");
                DepthValue(0.625f); projection.m03 = 4;
                material.SetMatrix("_TaskDepthProjection", projection);
                Check(Blue(RenderPixel()), "Pixels outside depth coverage preserve cropped RGB");
                material.SetFloat("_UseTaskDepth", 0);
                Check(Blue(RenderPixel()), "Depth-unavailable fallback never blanks the task crop");
            }
            finally
            {
                RenderTexture.active = prior; output.Release();
                UnityEngine.Object.DestroyImmediate(material); UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(output); UnityEngine.Object.DestroyImmediate(cpu); UnityEngine.Object.DestroyImmediate(depth);
            }
        }
    }
}
