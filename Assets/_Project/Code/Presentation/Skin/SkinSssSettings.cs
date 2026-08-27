using Eleven.Core;

namespace Eleven.Presentation.Skin
{
    /// <summary>
    /// Cấu hình shader da theo bậc thiết bị.
    ///
    /// Bậc C KHÔNG dùng shader da: <c>TierProfile.subsurfaceScattering</c> = false thì tắt keyword
    /// <c>_SKIN_SSS_ON</c>, và nhánh còn lại của shader chính là Lambert + GGX — tức là Lit thường.
    /// Cách này giữ nguyên vật liệu, nguyên texture, nguyên draw call; chỉ đổi một keyword.
    /// Đổi hẳn sang <c>Universal Render Pipeline/Lit</c> thì phải nhân đôi vật liệu và làm hỏng
    /// batching, mà kết quả nhìn ra vẫn thế.
    /// </summary>
    public struct SkinSssSettings
    {
        public QualityTier tier;

        /// <summary>Có bật tán xạ dưới bề mặt không. Bậc C: false.</summary>
        public bool enabled;

        /// <summary>Về đường Lit thường (Lambert + GGX). Luôn ngược với <see cref="enabled"/>.</summary>
        public bool useLitFallback;

        /// <summary>Cường độ pha trộn giữa Lambert và kết quả LUT, trong [0, 1].</summary>
        public float sssStrength;

        /// <summary>Có tính ánh sáng xuyên qua (vành tai, cánh mũi ngược sáng) không. Chỉ bậc A.</summary>
        public bool transmission;

        /// <summary>Trần GPU cho cả HAI nhân vật, mili-giây.</summary>
        public float maxGpuBudgetMs;

        public static SkinSssSettings ForTier(QualityTier tier)
        {
            switch (tier)
            {
                case QualityTier.A:
                    return Make(QualityTier.A, sss: true, strength: 1.0f, transmission: true);

                case QualityTier.B:
                    // Bậc B giữ tán xạ (đó là thứ làm mặt người ra mặt người) nhưng bỏ ánh sáng
                    // xuyên — hiệu ứng ngược sáng chỉ thấy được ở vài góc camera, còn giá thì
                    // phải trả ở mọi điểm ảnh da.
                    return Make(QualityTier.B, sss: true, strength: 0.75f, transmission: false);

                case QualityTier.C:
                default:
                    return Make(QualityTier.C, sss: false, strength: 0.0f, transmission: false);
            }
        }

        /// <summary>
        /// Đọc thẳng từ profile. Đây là đường đi thật lúc chạy — ô nghiệm thu đòi "tắt được ở
        /// bậc C qua <c>TierProfile.subsurfaceScattering</c>", không phải qua hằng số trong file này.
        /// </summary>
        public static SkinSssSettings FromProfile(TierProfile profile)
        {
            if (profile == null)
            {
                return ForTier(QualityTier.A);
            }

            SkinSssSettings settings = ForTier(profile.tier);

            if (!profile.subsurfaceScattering)
            {
                // Cờ trong profile luôn thắng bảng mặc định của bậc: người chơi tắt SSS ở bậc A
                // thì phải tắt thật.
                settings.enabled = false;
                settings.useLitFallback = true;
                settings.sssStrength = 0.0f;
                settings.transmission = false;
            }

            return settings;
        }

        private static SkinSssSettings Make(QualityTier tier, bool sss, float strength, bool transmission)
        {
            return new SkinSssSettings
            {
                tier = tier,
                enabled = sss,
                useLitFallback = !sss,
                sssStrength = sss ? strength : 0.0f,
                transmission = sss && transmission,
                maxGpuBudgetMs = tier == QualityTier.A ? SkinBudget.MaxGpuBudgetMs
                               : tier == QualityTier.B ? SkinBudget.MaxGpuBudgetMs
                               : 0.15f
            };
        }
    }

    /// <summary>
    /// Tập keyword shader ứng với một cấu hình. Thuần dữ liệu, không đụng <c>Material</c> —
    /// nhờ vậy EditMode kiểm được đúng bộ keyword mà lúc chạy sẽ bật.
    /// </summary>
    public struct SkinKeywordSet
    {
        public bool sss;
        public bool transmission;

        public static SkinKeywordSet For(in SkinSssSettings settings)
        {
            return new SkinKeywordSet
            {
                sss = settings.enabled,
                transmission = settings.enabled && settings.transmission
            };
        }

        /// <summary>Số keyword riêng của T31 đang bật.</summary>
        public int EnabledCount => (sss ? 1 : 0) + (transmission ? 1 : 0);
    }

    /// <summary>Hằng số ngân sách của T31, để mã đo đạc và test dùng chung một nguồn.</summary>
    public static class SkinBudget
    {
        /// <summary>Trần GPU cho CẢ HAI nhân vật (ms) — ô nghiệm thu T31.</summary>
        public const float MaxGpuBudgetMs = 0.5f;

        /// <summary>Số nhân vật mà ngân sách trên áp cho: thủ môn và người sút.</summary>
        public const int CharacterCount = 2;

        /// <summary>
        /// Trần thời gian biên dịch shader làm chậm màn hình đầu tiên (ms) — ô nghiệm thu T31.
        /// </summary>
        public const float MaxFirstScreenCompileMs = 1000.0f;

        /// <summary>
        /// Trần số biến thể của pass dựng hình. Đây là thứ QUYẾT ĐỊNH ô "màn hình đầu tiên không
        /// delay quá 1 giây": mỗi biến thể là một lần biên dịch. URP/Lit khai hơn ba mươi
        /// multi_compile, ra hàng chục nghìn biến thể — shader da không được đi theo đường đó.
        /// </summary>
        public const int MaxForwardVariants = 256;
    }
}
