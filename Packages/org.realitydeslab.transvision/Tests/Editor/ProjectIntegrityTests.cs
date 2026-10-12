using System.Collections.Generic;
using System.Linq;
using HoloKit;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TransVision.Tests
{
    /// <summary>
    /// The app content lives in this package and runs on URP with the HoloKit SDK: the scene,
    /// its references and the render pipeline settings must survive package moves and upgrades.
    /// </summary>
    public class ProjectIntegrityTests
    {
        const string PackageRoot = "Packages/org.realitydeslab.transvision";
        const string ScenePath = PackageRoot + "/Scenes/transvision.unity";

        Scene m_Scene;

        [OneTimeSetUp]
        public void OpenScene()
        {
            m_Scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        [OneTimeTearDown]
        public void CloseScene()
        {
            if (m_Scene.IsValid())
                EditorSceneManager.CloseScene(m_Scene, true);
        }

        [Test]
        public void Scene_IsTheFirstEnabledBuildScene()
        {
            EditorBuildSettingsScene first = EditorBuildSettings.scenes.FirstOrDefault(s => s.enabled);
            Assert.That(first, Is.Not.Null);
            Assert.That(first.path, Is.EqualTo(ScenePath));
        }

        [Test]
        public void Scene_HasNoMissingScripts()
        {
            var missing = AllGameObjects()
                .Where(go => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) > 0)
                .Select(go => go.name)
                .ToList();
            Assert.That(missing, Is.Empty);
        }

        [Test]
        public void Scene_HasOneHoloKitCameraManager()
        {
            Assert.That(Components<HoloKitCameraManager>().Count, Is.EqualTo(1));
        }

        /// As in EchoVision: no SDK stereo bar in Stereo by default.
        [Test]
        public void Stereo_ShowsNoSdkStereoBar()
        {
            Assert.That((int)Components<HoloKitCameraManager>().Single().StereoBarItems, Is.EqualTo(0));
        }

        /// Stereo's plane shows the AR camera's picture, so it must follow the AR camera.
        [Test]
        public void PlanePlacer_FollowsTheArCamera()
        {
            PlanePlacer placer = Components<PlanePlacer>().Single();
            Assert.That(placer.m_HoloKitCameraManager, Is.Not.Null);
            Assert.That(placer.monoCamera, Is.Not.Null);
            Assert.That(placer.monoCamera.GetComponent<UnityEngine.XR.ARFoundation.ARCameraManager>(), Is.Not.Null);
        }

        [Test]
        public void CameraImageProcessor_HasEveryMaterialAndRenderTexture()
        {
            var processor = new SerializedObject(Components<CameraImageProcessor>().Single());
            string[] fields =
            {
                "m_RenderTexture", "m_CalculateMaskMaterial", "m_MaskTexture", "m_BlurMaterial", "m_BlurredMaskTexture",
                "m_GenerateOffsetMaterial", "m_OffsetTexture", "m_DistortionMaterial", "m_DistortionTexture",
            };
            foreach (string field in fields)
                Assert.That(processor.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        }

        [Test]
        public void EffectShaders_CompileWithoutErrors()
        {
            var processor = new SerializedObject(Components<CameraImageProcessor>().Single());
            foreach (string field in new[] { "m_CalculateMaskMaterial", "m_BlurMaterial", "m_GenerateOffsetMaterial", "m_DistortionMaterial" })
            {
                var material = (Material)processor.FindProperty(field).objectReferenceValue;
                Assert.That(material.shader, Is.Not.Null, field);
                Assert.That(material.shader.name, Is.Not.EqualTo("Hidden/InternalErrorShader"), field);
                Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False, $"{field}: {material.shader.name}");
            }
        }

        /// Recording as in EchoVision: one shared microphone input with Voice processing, HD60 by
        /// default, and the SDK's Mono buttons (Record, HD60/4K30, Stereo) wired to the UI canvas.
        [Test]
        public void Recorder_AndMonoButtons_MatchEchoVision()
        {
            var recorder = new SerializedObject(Components<HoloKit.iOS.HoloKitVideoRecorder>().Single());
            Assert.That(recorder.FindProperty("_microphoneInput").objectReferenceValue, Is.InstanceOf<HoloKit.iOS.HoloKitMicrophoneCapture>());
            Assert.That(recorder.FindProperty("_includeMicrophone").boolValue, Is.True);
            Assert.That(recorder.FindProperty("_environmentProcessing").enumValueIndex, Is.EqualTo((int)HoloKit.iOS.MicrophoneProcessing.Voice));
            Assert.That(recorder.FindProperty("_captureCameraIn4K").boolValue, Is.False, "HD60 by default");

            var ui = Components<HoloKit.UI.HoloKitDefaultUICanvas>().Single();
            Assert.That(ui.RecordIn4KButton, Is.Not.Null);
            Assert.That(ui.RecordButtonText, Is.Not.Null);
            Assert.That(ui.RenderModeButtonText, Is.Not.Null);
            var clicks = new Dictionary<string, string>
            {
                ["Record Button"] = "ToggleRecording",
                ["Recording 4K Button"] = "ToggleRecording4K",
                ["Switch Render Mode Button"] = "SwitchRenderMode",
            };
            foreach (var (name, method) in clicks)
            {
                var button = ui.transform.Find(name).GetComponent<UnityEngine.UI.Button>();
                Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(1), name);
                Assert.That(button.onClick.GetPersistentTarget(0), Is.SameAs(ui), name);
                Assert.That(button.onClick.GetPersistentMethodName(0), Is.EqualTo(method), name);
            }
        }

        /// iOS 27 or later, so the HoloKit SDK's iOS 27 camera correction always applies.
        [Test]
        public void Ios_MinimumVersionIs27()
        {
            Assert.That(PlayerSettings.iOS.targetOSVersionString, Is.EqualTo("27.0"));
        }

        [Test]
        public void Urp_IsTheActivePipelineOnEveryQualityLevel()
        {
            Assert.That(GraphicsSettings.defaultRenderPipeline, Is.InstanceOf<UniversalRenderPipelineAsset>());
            for (int i = 0; i < QualitySettings.count; i++)
            {
                RenderPipelineAsset asset = QualitySettings.GetRenderPipelineAssetAt(i);
                Assert.That(asset == null || asset is UniversalRenderPipelineAsset, QualitySettings.names[i]);
            }
        }

        [Test]
        public void UrpRenderers_HaveNoMissingRendererFeatures()
        {
            string[] guids = AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets" });
            Assert.That(guids, Is.Not.Empty);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                Assert.That(renderer.rendererFeatures, Has.None.Null, path);
            }
        }

        /// SSAO darkens nothing in this scene (one unlit plane over the camera image) but costs a
        /// depth-normals prepass and full-screen passes per eye; it heated the phone for no effect.
        [Test]
        public void UrpRenderers_DoNotRunSsao()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                var active = renderer.rendererFeatures.Where(f => f != null && f.isActive && f.GetType().Name == "ScreenSpaceAmbientOcclusion");
                Assert.That(active, Is.Empty, path);
            }
        }

        [Test]
        public void XRSimulationEnvironment_LoadsFromThePackage()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackageRoot + "/Debug/Red Simulation Environment.prefab");
            Assert.That(prefab, Is.Not.Null);
        }

        List<T> Components<T>() where T : Component =>
            AllGameObjects().SelectMany(go => go.GetComponents<T>()).ToList();

        IEnumerable<GameObject> AllGameObjects() =>
            m_Scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject);
    }
}
