using Eleven.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.Rendering.Universal;

namespace Eleven.Tests.EditMode
{
    /// <summary>
    /// Nghiệm thu T03 mục 1: 3 URP asset + 3 TierProfile tồn tại và giá trị khớp bảng trong docs/plan.md.
    /// Test này đọc asset thật trên đĩa — nó hỏng nếu ai đó chỉnh tay lệch bảng.
    /// </summary>
    public class TierAssetTests
    {
        const string SettingsDir = "Assets/_Project/Settings";

        [TestCase("A", QualityTier.A, 1.00f, 60, 1.0f, true, true, true, 400)]
        [TestCase("B", QualityTier.B, 0.80f, 60, 0.4f, true, true, false, 250)]
        [TestCase("C", QualityTier.C, 0.65f, 30, 0.0f, false, false, false, 140)]
        public void TierProfile_MatchesPlanTable(string name, QualityTier tier, float renderScale,
            int targetFrameRate, float grassDensity, bool net, bool sss, bool shafts, int textureBudgetMB)
        {
            var p = AssetDatabase.LoadAssetAtPath<TierProfile>($"{SettingsDir}/TierProfile-{name}.asset");
            Assert.IsNotNull(p, $"Thiếu TierProfile-{name}.asset — chạy Eleven > Phase 0 > Generate Tier Assets.");

            Assert.AreEqual(tier, p.tier, "tier");
            Assert.AreEqual(renderScale, p.renderScale, 0.001f, "renderScale");
            Assert.AreEqual(targetFrameRate, p.targetFrameRate, "targetFrameRate");
            Assert.AreEqual(grassDensity, p.grassDensity, 0.001f, "grassDensity");
            Assert.AreEqual(net, p.netSimulation, "netSimulation");
            Assert.AreEqual(sss, p.subsurfaceScattering, "subsurfaceScattering");
            Assert.AreEqual(shafts, p.lightShafts, "lightShafts");
            Assert.AreEqual(textureBudgetMB, p.textureMemoryBudgetMB, "textureMemoryBudgetMB");
        }

        [TestCase("A", 1.00f)]
        [TestCase("B", 0.80f)]
        [TestCase("C", 0.65f)]
        public void UrpAsset_ExistsAndRenderScaleMatchesProfile(string name, float renderScale)
        {
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>($"{SettingsDir}/URP-Tier{name}.asset");
            Assert.IsNotNull(urp, $"Thiếu URP-Tier{name}.asset — chạy Eleven > Phase 0 > Generate Tier Assets.");
            Assert.AreEqual(renderScale, urp.renderScale, 0.001f, "renderScale của URP asset phải khớp TierProfile");
        }

        /// <summary>
        /// DeviceTier.Apply dùng (int)tier làm chỉ số quality level, nên TierA/B/C PHẢI nằm ở 0/1/2.
        /// Nếu còn sót level mặc định của Unity ở đầu danh sách thì đổi bậc sẽ chọn nhầm pipeline.
        /// </summary>
        [Test]
        public void QualityLevels_AreExactlyTierABC_InOrder()
        {
            Assert.AreEqual(new[] { "TierA", "TierB", "TierC" }, UnityEngine.QualitySettings.names);
        }
    }
}
