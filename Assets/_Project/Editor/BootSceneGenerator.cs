using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Eleven.Core;

namespace Eleven.Editor.Tools
{
    /// <summary>
    /// Sinh scene Boot tối thiểu và đưa vào Build Settings. BuildScript từ chối build khi
    /// Build Settings rỗng, nên đây là điều kiện cần để ./tools/build.sh chạy được.
    /// Chạy lại được: ghi đè scene cũ bằng nội dung chuẩn.
    /// </summary>
    public static class BootSceneGenerator
    {
        const string ScenesDir = "Assets/_Project/Scenes";
        const string BootScenePath = ScenesDir + "/Boot.unity";
        const string SettingsDir = "Assets/_Project/Settings";

        [MenuItem("Eleven/Phase 0/Generate Boot Scene")]
        public static void Generate()
        {
            if (!AssetDatabase.IsValidFolder(ScenesDir))
                AssetDatabase.CreateFolder("Assets/_Project", "Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(0f, 1.6f, -11f);
            var cam = cameraGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.09f, 0.07f);

            var lightGo = new GameObject("Directional Light", typeof(Light));
            var light = lightGo.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var bootstrapGo = new GameObject("Eleven.Bootstrap", typeof(TierBootstrap));
            AssignProfiles(bootstrapGo.GetComponent<TierBootstrap>());

            EditorSceneManager.SaveScene(scene, BootScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"[BootSceneGenerator] Đã sinh {BootScenePath} và đặt làm scene duy nhất trong Build Settings.");
        }

        /// <summary>Gán 3 TierProfile vào field private bằng SerializedObject — không cần mở API ra public.</summary>
        static void AssignProfiles(TierBootstrap bootstrap)
        {
            var so = new SerializedObject(bootstrap);
            var array = so.FindProperty("profiles");
            array.arraySize = 3;
            string[] names = { "A", "B", "C" };
            for (int i = 0; i < 3; i++)
            {
                string path = $"{SettingsDir}/TierProfile-{names[i]}.asset";
                var profile = AssetDatabase.LoadAssetAtPath<TierProfile>(path);
                if (profile == null)
                    Debug.LogWarning($"[BootSceneGenerator] Chưa có {path} — chạy Generate Tier Assets trước.");
                array.GetArrayElementAtIndex(i).objectReferenceValue = profile;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
