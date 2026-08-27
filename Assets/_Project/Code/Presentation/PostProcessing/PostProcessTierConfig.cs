using System;
using Eleven.Core;

namespace Eleven.Presentation
{
    /// <summary>
    /// Chế độ chuyển đổi màu sắc (Tonemapping) được hỗ trợ.
    /// </summary>
    public enum TonemapMode
    {
        None,
        Neutral,
        Lut3D // Bắt buộc dùng 3D LUT cho mobile để tiết kiệm GPU thay vì ACES runtime
    }

    /// <summary>
    /// Dữ liệu cấu hình hậu kỳ chi tiết cho một bậc thiết bị.
    /// </summary>
    [Serializable]
    public struct PostProcessSettings
    {
        public QualityTier tier;
        public TonemapMode tonemap;
        public bool useVignette;
        public bool useBloom;
        public bool allowImpactChromaticAberration;
        public bool useSSAO; // Bắt buộc false trên mọi bậc
        public float maxGpuBudgetMs;

        public static PostProcessSettings ForTier(QualityTier tier)
        {
            switch (tier)
            {
                case QualityTier.A:
                    return new PostProcessSettings
                    {
                        tier = QualityTier.A,
                        tonemap = TonemapMode.Lut3D,
                        useVignette = true,
                        useBloom = true,
                        allowImpactChromaticAberration = true,
                        useSSAO = false, // Không dùng SSAO
                        maxGpuBudgetMs = 1.5f
                    };

                case QualityTier.B:
                    return new PostProcessSettings
                    {
                        tier = QualityTier.B,
                        tonemap = TonemapMode.Lut3D,
                        useVignette = true,
                        useBloom = false,
                        allowImpactChromaticAberration = true,
                        useSSAO = false, // Không dùng SSAO
                        maxGpuBudgetMs = 1.0f
                    };

                case QualityTier.C:
                default:
                    return new PostProcessSettings
                    {
                        tier = QualityTier.C,
                        tonemap = TonemapMode.Lut3D,
                        useVignette = true,
                        useBloom = false,
                        allowImpactChromaticAberration = false, // Bậc C chỉ có tonemap + vignette
                        useSSAO = false, // Không dùng SSAO
                        maxGpuBudgetMs = 0.5f
                    };
            }
        }
    }

    /// <summary>
    /// Trình quản lý và cung cấp cấu hình hậu kỳ theo từng bậc chất lượng.
    /// </summary>
    public static class PostProcessTierConfig
    {
        public const float MaxTierAGpuBudgetMs = 1.5f;

        public static PostProcessSettings GetSettings(QualityTier tier)
        {
            return PostProcessSettings.ForTier(tier);
        }

        public static bool ValidateSettings(in PostProcessSettings settings, out string error)
        {
            error = null;

            if (settings.useSSAO)
            {
                error = "SSAO toàn màn hình bị cấm tuyệt đối trên mọi bậc do nút thắt băng thông GPU di động.";
                return false;
            }

            if (settings.tonemap != TonemapMode.Lut3D)
            {
                error = "Tonemapping bắt buộc phải sử dụng LUT 3D thay vì thuật toán ACES tốn kém.";
                return false;
            }

            if (settings.tier == QualityTier.C)
            {
                if (settings.useBloom || settings.allowImpactChromaticAberration)
                {
                    error = "Bậc C chỉ được phép giữ lại Tonemap và Vignette.";
                    return false;
                }
            }

            if (settings.maxGpuBudgetMs > MaxTierAGpuBudgetMs)
            {
                error = $"Ngân sách GPU {settings.maxGpuBudgetMs}ms vượt quá trần quy định {MaxTierAGpuBudgetMs}ms.";
                return false;
            }

            return true;
        }
    }
}
