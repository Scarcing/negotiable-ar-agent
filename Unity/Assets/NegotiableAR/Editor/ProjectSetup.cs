using System;
using System.IO;
using System.Linq;
using Meta.XR;
using Meta.XR.EnvironmentDepth;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace NegotiableAR.Editor
{
    public static class ProjectSetup
    {
        const string Root = "Assets/NegotiableAR";
        [MenuItem("Negotiable AR/Create or rebuild Quest scene")]
        public static void CreateScene()
        {
            Directory.CreateDirectory(Root + "/Scenes"); Directory.CreateDirectory(Root + "/Resources");
            var settings = AssetDatabase.LoadAssetAtPath<AgentSettings>(Root + "/Resources/AgentSettings.asset");
            if (settings == null) { settings = ScriptableObject.CreateInstance<AgentSettings>(); AssetDatabase.CreateAsset(settings, Root + "/Resources/AgentSettings.asset"); }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab");
            if (prefab == null) throw new InvalidOperationException("Meta XR Core rig prefab is missing; resolve packages first.");
            var rigGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var rig = rigGo.GetComponent<OVRCameraRig>();
            var manager = rigGo.GetComponent<OVRManager>();
            manager.isInsightPassthroughEnabled = true;
            var managerData = new SerializedObject(manager);
            managerData.FindProperty("requestPassthroughCameraAccessPermissionOnStartup").boolValue = true;
            managerData.FindProperty("requestScenePermissionOnStartup").boolValue = true;
            managerData.ApplyModifiedPropertiesWithoutUndo();
            var camera = rig.centerEyeAnchor.GetComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            var passthrough = rigGo.AddComponent<OVRPassthroughLayer>(); passthrough.overlayType = OVROverlay.OverlayType.Underlay;
            var depthGo = new GameObject("Meta Environment Depth");
            var depth = depthGo.AddComponent<EnvironmentDepthManager>(); depth.OcclusionShadersMode = OcclusionShadersMode.SoftOcclusion; depth.RemoveHands = false;
            var rgbGo = new GameObject("Meta RGB Camera");
            var rgb = rgbGo.AddComponent<PassthroughCameraAccess>(); rgb.enabled = false; rgb.RequestedResolution = new Vector2Int(640, 480); rgb.MaxFramerate = 30; rgb.enabled = true;
            var app = new GameObject("Negotiable AR Agent");
            var perception = app.AddComponent<MetaPerception>(); perception.settings = settings; perception.rig = rig; perception.cameraAccess = rgb; perception.depth = depth;
            CreateHand(rig.leftHandAnchor, 0, out perception.leftHand, out perception.leftSkeleton);
            CreateHand(rig.rightHandAnchor, 1, out perception.rightHand, out perception.rightSkeleton);
            var workspace = new GameObject("Workspace reference (independent of draggable panel)");
            workspace.transform.position = new Vector3(0, 0.75f, 0.7f); perception.Workspace = workspace.transform;
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name = "Depth occlusion test cube"; cube.transform.SetParent(workspace.transform, false);
            cube.transform.localScale = Vector3.one * 0.06f;
            var shader = Shader.Find("Meta/EnvironmentDepth/Built-in Render Pipeline/OcclusionUnlit");
            if (shader == null) throw new InvalidOperationException("Meta depth occlusion shader missing.");
            var material = new Material(shader); material.color = new Color(0.15f, 0.9f, 0.75f);
            const string materialPath = Root + "/Resources/DepthProbe.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(materialPath) != null) AssetDatabase.DeleteAsset(materialPath);
            AssetDatabase.CreateAsset(material, materialPath); cube.GetComponent<Renderer>().sharedMaterial = material;
            var client = app.AddComponent<PerceptionClient>(); client.settings = settings; client.perception = perception;
            var agent = app.AddComponent<AgentController>(); agent.settings = settings; agent.perception = perception; agent.client = client; agent.voice = app.AddComponent<AudioSource>(); agent.voice.spatialBlend = 0;
            var panelGo = new GameObject("Agent subtitles and negotiation", typeof(RectTransform));
            var panel = panelGo.AddComponent<AgentPanel>(); panel.head = rig.centerEyeAnchor; panel.agent = agent; panel.perception = perception; agent.panel = panel;
            var pointer = app.AddComponent<PanelPointer>(); pointer.rig = rig; pointer.left = perception.leftHand; pointer.right = perception.rightHand;
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), Root + "/Scenes/QuestAgent.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Root + "/Scenes/QuestAgent.unity", true) };
            Configure(); AssetDatabase.SaveAssets();
            Debug.Log("NEGOTIABLE_AR_SETUP_OK");
        }

        static void CreateHand(Transform parent, int side, out OVRHand hand, out OVRSkeleton skeleton)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.meta.xr.sdk.core/Prefabs/OVRHandPrefab.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab); go.transform.SetParent(parent, false);
            hand = go.GetComponent<OVRHand>(); skeleton = go.GetComponent<OVRSkeleton>();
            var data = new SerializedObject(hand); data.FindProperty("HandType").intValue = side; data.ApplyModifiedPropertiesWithoutUndo();
            var skel = new SerializedObject(skeleton); skel.FindProperty("_skeletonType").intValue = side; skel.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Negotiable AR/Configure Android and OpenXR")]
        public static void Configure()
        {
            PlayerSettings.companyName = "NegotiableAR"; PlayerSettings.productName = "Negotiable AR Agent";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "org.research.negotiablear");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.GameActivity;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SplashScreen.backgroundColor = new Color(0.15f, 0.17f, 0.2f);
            PlayerSettings.insecureHttpOption = InsecureHttpOption.DevelopmentOnly;
            var config = OVRProjectConfig.CachedProjectConfig;
            config.handTrackingSupport = OVRProjectConfig.HandTrackingSupport.ControllersAndHands;
            config.sceneSupport = OVRProjectConfig.FeatureSupport.Required;
            config.insightPassthroughSupport = OVRProjectConfig.FeatureSupport.Required;
            config.systemLoadingScreenBackground = OVRProjectConfig.SystemLoadingScreenBackground.ContextualPassthrough;
            config.targetDeviceTypes = new System.Collections.Generic.List<OVRProjectConfig.DeviceType> { OVRProjectConfig.DeviceType.Quest3, OVRProjectConfig.DeviceType.Quest3S };
            config.isPassthroughCameraAccessEnabled = true;
            // Permit only the local USB-forwarded bridge; all other HTTP remains blocked.
            config.enableNSCConfig = true;
            config.securityXmlPath = "Assets/NegotiableAR/Network/local_bridge_security.xml";
            OVRProjectConfig.CommitProjectConfig(config);
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget))
            {
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, Root + "/Resources/XRSettings.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            }
            var general = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            if (general == null)
            {
                general = ScriptableObject.CreateInstance<XRGeneralSettings>(); AssetDatabase.AddObjectToAsset(general, perTarget);
                perTarget.SetSettingsForBuildTarget(BuildTargetGroup.Android, general);
            }
            if (general.Manager == null)
            {
                general.Manager = ScriptableObject.CreateInstance<XRManagerSettings>(); AssetDatabase.AddObjectToAsset(general.Manager, perTarget);
            }
            general.InitManagerOnStart = true;
            if (!XRPackageMetadataStore.AssignLoader(general.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Android))
                throw new InvalidOperationException("Failed to assign Android OpenXR loader");
            var openxr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            UnityEditor.XR.OpenXR.Features.FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            openxr.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            foreach (var feature in openxr.GetFeatures())
            {
                string type = feature.GetType().Name;
                if (type == "OculusQuestFeature") feature.enabled = false; // Superseded by MetaQuestFeature in OpenXR 1.15.
                if (type == "MetaQuestFeature" || type == "MetaXRFeature" || type == "ARSessionFeature" || type == "AROcclusionFeature" || type == "OpenXRCompositionLayersFeature" || type == "HandTracking" || type == "MetaHandTrackingAim" || type == "OculusTouchControllerProfile" || type == "MetaQuestTouchPlusControllerProfile") feature.enabled = true;
                EditorUtility.SetDirty(feature);
                Debug.Log("OpenXR feature: " + type + " enabled=" + feature.enabled);
            }
            EditorUtility.SetDirty(openxr); EditorUtility.SetDirty(general); EditorUtility.SetDirty(general.Manager); EditorUtility.SetDirty(perTarget);
            AssetDatabase.SaveAssets();
        }

        public static void BuildAndroid()
        {
            CreateScene();
            // Unity's simulation build hook can leave temporary copies after a failed build.
            if (AssetDatabase.IsValidFolder("Assets/XR/Temp")) AssetDatabase.DeleteAsset("Assets/XR/Temp");
            Directory.CreateDirectory("Builds");
            string localSdk = Path.GetFullPath("../.tools/android-sdk");
            string originalSdk = UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath;
            string originalNdk = UnityEditor.Android.AndroidExternalToolsSettings.ndkRootPath;
            try
            {
                if (Directory.Exists(Path.Combine(localSdk, "platforms")))
                {
                    UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath = localSdk;
                    UnityEditor.Android.AndroidExternalToolsSettings.ndkRootPath = Path.Combine(localSdk, "ndk/27.2.12479018");
                }
                var result = BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, "Builds/NegotiableAR.apk", BuildTarget.Android, BuildOptions.Development);
                if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Android build failed: " + result.summary.result);
            }
            finally
            {
                // Unity rejects restoring an incomplete installation; preserve valid paths only.
                if (File.Exists(Path.Combine(originalSdk, "cmdline-tools/latest/bin/sdkmanager.bat")))
                    UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath = originalSdk;
                if (File.Exists(Path.Combine(originalNdk, "source.properties")))
                    UnityEditor.Android.AndroidExternalToolsSettings.ndkRootPath = originalNdk;
            }
        }
    }
}
