#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
#if NOISETEXTURE_UIELEMENTS
using UnityEngine.UIElements;
#endif

namespace NoiseTexture.Sample.Editor
{
    /// <summary>Install-and-run entry point: generates demo scenes and opens them.</summary>
    public static class NoiseTextureDemoSetup
    {
        public const string UiSceneName = "Demo - UI Toolkit";
        public const string PanelSceneName = "Demo - PanelRenderer (Unity 6)";
        public const string ImguiSceneName = "Demo - IMGUI (no UI Toolkit)";

        [MenuItem("Noise Texture/Open Demo - UI Toolkit")]
        public static void OpenUiDemo()
        {
#if NOISETEXTURE_UIELEMENTS
            EnsureScenes();
            OpenScene(UiSceneName);
#else
            Debug.LogWarning("[NoiseTexture] UI Toolkit is unavailable — opening the IMGUI demo.");
            OpenImguiDemo();
#endif
        }

        [MenuItem("Noise Texture/Open Demo - PanelRenderer (Unity 6)")]
        public static void OpenPanelDemo()
        {
#if NOISETEXTURE_UIELEMENTS
            EnsureScenes();
            OpenScene(PanelSceneName);
#else
            Debug.LogWarning("[NoiseTexture] UI Toolkit is unavailable — opening the IMGUI demo.");
            OpenImguiDemo();
#endif
        }

        [MenuItem("Noise Texture/Open Demo - IMGUI (no UI Toolkit)")]
        public static void OpenImguiDemo()
        {
            EnsureScenes();
            OpenScene(ImguiSceneName);
        }

        [MenuItem("Noise Texture/Regenerate Demo Scenes")]
        public static void RegenerateDemoScenes()
        {
            GenerateDemoScenes();
            Debug.Log("[NoiseTexture] Demo scenes regenerated.");
        }

        /// <summary>Creates missing scenes only; never overwrites existing ones.</summary>
        public static void EnsureScenes()
        {
            var scenes = ScenesDirectory(DemoDirectory());
#if NOISETEXTURE_UIELEMENTS
            if (!File.Exists(scenes + "/" + UiSceneName + ".unity") ||
                !File.Exists(scenes + "/" + PanelSceneName + ".unity") ||
                !File.Exists(scenes + "/" + ImguiSceneName + ".unity"))
                GenerateDemoScenes();
#else
            if (!File.Exists(scenes + "/" + ImguiSceneName + ".unity"))
                GenerateDemoScenes();
#endif
        }

        /// <summary>Rebuilds all scenes from scratch. Safe to call from CI via script eval.</summary>
        public static void GenerateDemoScenes()
        {
            var editorDirectory = DemoDirectory();
            var demoRoot = ParentDirectory(editorDirectory);
            var scenes = ScenesDirectory(editorDirectory);

#if NOISETEXTURE_UIELEMENTS
            // Load assets after NewScene: switching scenes invalidates previous asset handles.
            var uiScene = NewScene();
            var uxml = LoadTree(demoRoot);
            var panel = EnsurePanelSettings(demoRoot);

            var uiHost = new GameObject("Noise Demo", typeof(UIDocument), typeof(CrtNoiseDemo));
            var document = uiHost.GetComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = uxml;
            AssignSerialized(document, "m_PanelSettings", panel);
            AssignSerialized(document, "sourceAsset", uxml);
            SaveScene(uiScene, scenes, UiSceneName, document);

            // Unity 6 API: PanelRenderer replaces UIDocument for runtime UXML rendering.
            var panelScene = NewScene();
            var panelUxml = LoadTree(demoRoot);
            var panelSettings = EnsurePanelSettings(demoRoot); // Reload after the scene switch.
            var panelRenderer = new GameObject("Noise Demo", typeof(PanelRenderer), typeof(CrtNoiseDemo))
                .GetComponent<PanelRenderer>();
            panelRenderer.panelSettings = panelSettings;
            panelRenderer.visualTreeAsset = panelUxml;
            AssignSerialized(panelRenderer, "m_PanelSettings", panelSettings);
            AssignSerialized(panelRenderer, "sourceAsset", panelUxml);
            SaveScene(panelScene, scenes, PanelSceneName, panelRenderer);
#endif

            var imguiScene = NewScene();
            new GameObject("Noise Demo", typeof(CrtNoiseIMGUIDemo));
            SaveScene(imguiScene, scenes, ImguiSceneName, null);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void SaveScene(Scene scene, string scenes, string name, Object dirtyTarget)
        {
            if (dirtyTarget != null) EditorUtility.SetDirty(dirtyTarget);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenes + "/" + name + ".unity");
        }

#if NOISETEXTURE_UIELEMENTS
        private static VisualTreeAsset LoadTree(string demoRoot) =>
            AssetDatabase.LoadAssetAtPath(demoRoot + "/CrtNoiseDemo.uxml", typeof(VisualTreeAsset)) as VisualTreeAsset;

        private static PanelSettings EnsurePanelSettings(string root)
        {
            var path = root + "/DemoPanelSettings.asset";
            var panel = AssetDatabase.LoadAssetAtPath(path, typeof(PanelSettings)) as PanelSettings;

            // Ship the theme with the package: an empty themeUss makes UI Toolkit skip rendering.
            var theme = AssetDatabase.LoadAssetAtPath(root + "/NoiseDemoTheme.tss", typeof(StyleSheet)) as StyleSheet;
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, path);
            }

            // ConstantPhysicalSize (the ScriptableObject default) leaves the panel height at 0
            // in a fresh project: no noise layer and no bottom-anchored controls are visible.
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);

            // themeUss is serialized but has no public setter in Unity 6.6.
            if (theme != null) AssignSerialized(panel, "themeUss", theme);
            EditorUtility.SetDirty(panel);
            return panel;
        }
#endif

        /// <summary>Public setters do not always dirty the serialized field in edit mode.</summary>
        private static void AssignSerialized(Object target, string property, Object value)
        {
            var serialized = new SerializedObject(target);
            var field = serialized.FindProperty(property);
            if (field == null) return;
            field.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Scene NewScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 0f, -10f);
            var cam = camera.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.015f, 0.02f, 0.018f, 1f);
            return scene;
        }

        private static string DemoDirectory()
        {
            foreach (var guid in AssetDatabase.FindAssets("NoiseTextureDemoSetup t:MonoScript"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                    return path.Substring(0, path.LastIndexOf('/'));
            }
            return "Assets/Plugins/NoiseTexture/Samples/Demo/Editor";
        }

        private static string ParentDirectory(string path) =>
            path.Substring(0, path.LastIndexOf('/'));

        private static string ScenesDirectory(string editorDirectory)
        {
            var scenes = ParentDirectory(editorDirectory) + "/Scenes";
            if (!Directory.Exists(scenes))
            {
                Directory.CreateDirectory(scenes);
                AssetDatabase.Refresh();
            }
            return scenes;
        }

        private static void OpenScene(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets(name + " t:Scene"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name)
                {
                    EditorSceneManager.OpenScene(path);
                    return;
                }
            }
            Debug.LogWarning("[NoiseTexture] Demo scene not found: " + name);
        }
    }
}
#endif // UNITY_EDITOR
