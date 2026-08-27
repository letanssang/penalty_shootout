using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Eleven.Core;
using Eleven.Presentation;
using Is = NUnit.Framework.Is;

namespace Eleven.Tests.EditMode
{
    [TestFixture]
    public class PostProcessingTierTests
    {
        [Test]
        public void Tonemap_SuDungLut3D_KhongDungACES_TrenMoiBac()
        {
            foreach (QualityTier tier in Enum.GetValues(typeof(QualityTier)))
            {
                var settings = PostProcessTierConfig.GetSettings(tier);
                Assert.AreEqual(TonemapMode.Lut3D, settings.tonemap,
                    $"Bậc {tier} đang không sử dụng Tonemap 3D LUT! " +
                    "Hệ quả: Sử dụng ACES runtime trên mobile gây lãng phí chu kỳ shader không cần thiết.");
            }
        }

        [Test]
        public void SSAO_TatTuyetDoi_TrenMoiBac()
        {
            foreach (QualityTier tier in Enum.GetValues(typeof(QualityTier)))
            {
                var settings = PostProcessTierConfig.GetSettings(tier);
                Assert.IsFalse(settings.useSSAO,
                    $"Bậc {tier} đang bật SSAO! " +
                    "Hệ quả: SSAO toàn màn hình phá vỡ băng thông GPU trên kiến trúc TBDR di động.");
            }
        }

        [Test]
        public void BacC_ChiConTonemapVaVignette()
        {
            var settings = PostProcessTierConfig.GetSettings(QualityTier.C);

            Assert.AreEqual(TonemapMode.Lut3D, settings.tonemap);
            Assert.IsTrue(settings.useVignette);
            Assert.IsFalse(settings.useBloom, "Bậc C không được bật Bloom.");
            Assert.IsFalse(settings.allowImpactChromaticAberration, "Bậc C không được bật Chromatic Aberration.");
            Assert.IsFalse(settings.useSSAO, "Bậc C không được bật SSAO.");

            bool valid = PostProcessTierConfig.ValidateSettings(in settings, out string error);
            Assert.IsTrue(valid, $"Cấu hình Bậc C không hợp lệ: {error}");
        }

        [Test]
        public void NganSachGPU_TierA_Duoi1_5ms()
        {
            var settingsA = PostProcessTierConfig.GetSettings(QualityTier.A);
            Assert.LessOrEqual(settingsA.maxGpuBudgetMs, PostProcessTierConfig.MaxTierAGpuBudgetMs);

            bool valid = PostProcessTierConfig.ValidateSettings(in settingsA, out string error);
            Assert.IsTrue(valid, $"Cấu hình Bậc A không hợp lệ: {error}");
        }

        [Test]
        public void ImpactEffect_ThoiLuongDuoi200ms_TuTat()
        {
            var effect = new ImpactPostProcessEffect();

            // Yêu cầu thời lượng vượt quá 200ms (ví dụ 0.5s)
            effect.TriggerImpact(0.8f, 0.5f);

            Assert.LessOrEqual(effect.TotalDuration, ImpactPostProcessEffect.MaxAllowedDurationSeconds,
                "Thời lượng hiệu ứng va chạm bắt buộc phải bị kẹp trần dưới 200ms (0.2s).");
            Assert.IsTrue(effect.IsActive);
            Assert.Greater(effect.CurrentIntensity, 0f);

            // Tick 0.1s
            effect.Tick(0.1f);
            Assert.IsTrue(effect.IsActive);
            Assert.Less(effect.CurrentIntensity, 0.8f);

            // Tick thêm 0.15s (tổng cộng 0.25s > MaxAllowedDuration)
            effect.Tick(0.15f);
            Assert.IsFalse(effect.IsActive);
            Assert.AreEqual(0.0f, effect.CurrentIntensity);
        }

        [Test]
        public void ImpactEffect_KhongCapPhatGC()
        {
            var effect = new ImpactPostProcessEffect();

            // Warm-up JIT
            effect.TriggerImpact(0.5f, 0.1f);
            effect.Tick(0.016f);

            Assert.That(() =>
            {
                for (int i = 0; i < 50; i++)
                {
                    effect.TriggerImpact(0.6f, 0.15f);
                    effect.Tick(0.016f);
                }
            }, Is.Not.AllocatingGCMemory());
        }
    }
}
