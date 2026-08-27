using Unity.Mathematics;
using Eleven.Core;

namespace Eleven.Presentation.Grass
{
    /// <summary>
    /// Ngân sách cỏ theo bậc thiết bị. Hệ số mật độ KHÔNG được viết cứng ở đây mà lấy từ
    /// <see cref="TierProfile.grassDensity"/> — bảng "Ba bậc thiết bị" trong docs/plan.md là
    /// nguồn duy nhất, ở đây chỉ là giá trị dự phòng khi chưa gán profile.
    ///
    /// Bậc C tắt hoàn toàn và thay bằng texture mặt sân: khác hẳn khán đài (T30, vẫn giữ hình
    /// tĩnh), vì cỏ ở bậc C là thứ đầu tiên phải cắt — nó vừa tốn nhất vừa dễ thay nhất.
    /// </summary>
    public struct GrassTierSettings
    {
        public QualityTier tier;

        /// <summary>Hệ số mật độ, lấy từ <c>TierProfile.grassDensity</c>. 1.0 / 0.4 / 0.0.</summary>
        public float densityScale;

        /// <summary>Có rải cỏ không. Bậc C: false.</summary>
        public bool enabled;

        /// <summary>Thay cỏ bằng texture mặt sân. Bậc C: true.</summary>
        public bool useGroundTexture;

        /// <summary>Trần GPU cho riêng cỏ, mili-giây.</summary>
        public float maxGpuBudgetMs;

        /// <summary>Cấu hình dựng hình mặc định của bậc này.</summary>
        public GrassRenderSettings render;

        /// <summary>Trần số túm được rải ở bậc này.</summary>
        public int maxInstances;

        /// <summary>Giá trị dự phòng khi chưa có <see cref="TierProfile"/>.</summary>
        public static GrassTierSettings ForTier(QualityTier tier)
        {
            switch (tier)
            {
                case QualityTier.A:
                    return Make(QualityTier.A, 1.0f);
                case QualityTier.B:
                    return Make(QualityTier.B, 0.4f);
                default:
                    return Make(QualityTier.C, 0.0f);
            }
        }

        /// <summary>
        /// Đọc thẳng từ profile. Đây là đường đi thật lúc chạy — ô nghiệm thu đòi mật độ
        /// "đọc từ TierProfile.grassDensity", không phải đọc từ hằng số trong file này.
        /// </summary>
        public static GrassTierSettings FromProfile(TierProfile profile)
        {
            if (profile == null)
            {
                return ForTier(QualityTier.A);
            }

            return Make(profile.tier, profile.grassDensity);
        }

        private static GrassTierSettings Make(QualityTier tier, float densityScale)
        {
            float density = math.saturate(densityScale);
            bool enabled = density > 0.0f;

            return new GrassTierSettings
            {
                tier = tier,
                densityScale = density,
                enabled = enabled,
                useGroundTexture = !enabled,
                maxGpuBudgetMs = tier == QualityTier.A ? GrassBudget.MaxTierAGpuMs
                               : tier == QualityTier.B ? 1.2f
                               : 0.0f,
                maxInstances = enabled ? GrassBudget.MaxInstances : 0,
                render = new GrassRenderSettings
                {
                    alphaClip = enabled,
                    // Cỏ không đổ bóng ở bất kỳ bậc nào theo mặc định: mỗi túm ghi thêm một lần
                    // vào shadow map, mà bóng của cỏ cao 8cm thì không ai nhìn thấy. Cờ vẫn tồn
                    // tại để đo được dòng "bóng+" của bảng tám dòng.
                    castShadows = false,
                    wind = tier == QualityTier.A
                }
            };
        }
    }

    /// <summary>Hằng số ngân sách của T29, để mã đo đạc và test dùng chung một nguồn.</summary>
    public static class GrassBudget
    {
        /// <summary>Trần GPU cho riêng cỏ ở bậc A (ms) — ô nghiệm thu T29.</summary>
        public const float MaxTierAGpuMs = 2.0f;

        /// <summary>Trần số túm cỏ. 24.000 × 32 B = 768 KB bộ đệm instance.</summary>
        public const int MaxInstances = 24000;

        /// <summary>
        /// Trần overdraw trung bình đọc từ debug view của URP trên vùng có cỏ.
        /// Trên GPU di động kiến trúc TBDR, mỗi lớp alpha là một lần đọc-ghi tile bộ nhớ.
        /// </summary>
        public const float MaxAverageOverdraw = 2.5f;

        /// <summary>Số draw call cho toàn bộ cỏ. Một.</summary>
        public const int MaxDrawCalls = 1;
    }
}
