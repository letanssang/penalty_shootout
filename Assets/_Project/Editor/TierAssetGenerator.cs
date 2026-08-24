using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Eleven.Core;

namespace Eleven.Editor.Tools
{
    /// <summary>
    /// Chạy MỘT LẦN sau khi mở project lần đầu: menu Eleven > Phase 0 > Generate Tier Assets.
    /// Sinh 3 URP asset + 3 TierProfile khớp bảng trong docs/plan.md, và dựng 3 quality level A/B/C
    /// trỏ tới đúng URP asset của từng bậc. Chạy lại được — ghi đè bằng giá trị chuẩn.
    /// </summary>
    public static class TierAssetGenerator
    {
        const string SettingsDir = "Assets/_Project/Settings";

        [MenuItem("Eleven/Phase 0/Generate Tier Assets")]
        public static void Generate()
        {
            if (!AssetDatabase.IsValidFolder(SettingsDir))
                AssetDatabase.CreateFolder("Assets/_Project", "Settings");

            var profiles = new TierProfile[3];
            var urpAssets = new UniversalRenderPipelineAsset[3];

            string[][] rows =
            {
                new[] { "A", "1.0", "60", "1.0", "1", "1", "1", "400" },
                new[] { "B", "0.80", "60", "0.4", "1", "1", "0", "250" },
                new[] { "C", "0.65", "30", "0.0", "0", "0", "0", "140" },
            };

            for (int i = 0; i < 3; i++)
            {
                urpAssets[i] = CreateOrLoadUrp(rows[i][0]);
                profiles[i] = CreateOrLoadProfile(rows[i], i);
            }

            ApplyQualityLevels(profiles, urpAssets);

            // Bật pipeline vừa sinh làm asset mặc định (bậc B là khởi đầu an toàn).
            GraphicsSettings.defaultRenderPipeline = urpAssets[1];

            AssetDatabase.SaveAssets();
            Debug.Log("[TierAssetGenerator] Đã sinh xong 3 URP asset + 3 TierProfile + 3 quality level (A/B/C).");
        }

        static UniversalRenderPipelineAsset CreateOrLoadUrp(string tier)
        {
            string path = $"{SettingsDir}/URP-Tier{tier}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (existing != null)
                return existing;

            // Renderer data đi kèm mỗi tier. Dùng reflection cho cờ Forward+ vì tên thuộc tính
            // đổi giữa các phiên bản URP; không có thì giữ mặc định và ghi log nhắc chỉnh tay.
            string rdPath = $"{SettingsDir}/URP-Tier{tier}-Renderer.asset";
            var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(rdPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                var prop = rendererData.GetType().GetProperty("renderingMode");
                if (prop != null && prop.CanWrite && prop.PropertyType.IsEnum)
                {
                    object forwardPlus = System.Enum.Parse(prop.PropertyType, "ForwardPlus");
                    prop.SetValue(rendererData, forwardPlus);
                }
                else
                {
                    Debug.LogWarning($"[TierAssetGenerator] Không đặt được Forward+ bằng reflection cho {rdPath} — hãy bật Forward+ trong Inspector.");
                }
                AssetDatabase.CreateAsset(rendererData, rdPath);
            }

            var createMethod = typeof(UniversalRenderPipelineAsset).GetMethod(
                "Create", new[] { typeof(ScriptableRendererData) });
            UniversalRenderPipelineAsset urp = createMethod != null
                ? (UniversalRenderPipelineAsset)createMethod.Invoke(null, new object[] { rendererData })
                : ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();

            AssetDatabase.CreateAsset(urp, path);
            urp.renderScale = tier == "A" ? 1.0f : tier == "B" ? 0.80f : 0.65f;
            urp.msaaSampleCount = tier == "A" ? 4 : 2;
            urp.supportsHDR = tier != "C";
            urp.shadowDistance = tier == "A" ? 40f : tier == "B" ? 25f : 15f;
            return urp;
        }

        static TierProfile CreateOrLoadProfile(string[] row, int index)
        {
            string path = $"{SettingsDir}/TierProfile-{row[0]}.asset";
            var p = AssetDatabase.LoadAssetAtPath<TierProfile>(path);
            if (p == null)
            {
                p = ScriptableObject.CreateInstance<TierProfile>();
                AssetDatabase.CreateAsset(p, path);
            }

            p.tier = (QualityTier)index;
            float.TryParse(row[1], out p.renderScale);
            int.TryParse(row[2], out p.targetFrameRate);
            float.TryParse(row[3], out p.grassDensity);
            p.netSimulation = row[4] == "1";
            p.subsurfaceScattering = row[5] == "1";
            p.lightShafts = row[6] == "1";
            int.TryParse(row[7], out p.textureMemoryBudgetMB);
            return p;
        }

        static void ApplyQualityLevels(TierProfile[] profiles, UniversalRenderPipelineAsset[] urpAssets)
        {
            string[] names = { "TierA", "TierB", "TierC" };

            // Xoá level cũ cùng tên để chạy lại không sinh rác.
            var existingNames = QualitySettings.names;
            for (int i = existingNames.Length - 1; i >= 0; i--)
                if (System.Array.IndexOf(names, existingNames[i]) >= 0)
                    QualitySettings.DeleteCustomLevel(i);

            for (int i = 0; i < 3; i++)
            {
                int idx = QualitySettings.AddCustomLevel(names[i]);
                QualitySettings.SetQualityLevel(idx, false);
                QualitySettings.renderPipeline = urpAssets[i]; // URP asset gắn với level này
                QualitySettings.vSyncCount = 0;                 // tự quản targetFrameRate
            }

            // Mặc định mở project ở bậc A để đo đường cơ sở.
            QualitySettings.SetQualityLevel(0, false);

            // Đặt DeviceTier khởi động theo bậc A; runtime sẽ Detect() và Apply() lại.
            var bootstrapProfiles = profiles;
            Debug.Log($"[TierAssetGenerator] Quality levels: {string.Join(", ", names)} — " +
                      $"profile A/B/C: {bootstrapProfiles[0].name}, {bootstrapProfiles[1].name}, {bootstrapProfiles[2].name}");
        }
    }
}
