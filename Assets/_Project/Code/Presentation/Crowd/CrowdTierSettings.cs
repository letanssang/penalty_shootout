using Eleven.Core;

namespace Eleven.Presentation.Crowd
{
    /// <summary>
    /// Ngân sách khán đài theo bậc thiết bị.
    ///
    /// Bậc C KHÔNG tắt khán giả — ô nghiệm thu ghi rõ "vẫn còn hình, không biến mất". Khán đài
    /// trống là thứ người chơi nhìn thấy ngay, còn khán giả đứng yên thì hầu như không ai để ý
    /// ở khoảng cách 15m. Nên bậc C giữ nguyên số ghế và chỉ đóng băng animation: cùng một
    /// draw call, cùng một atlas, chỉ khác là chỉ số khung hình luôn bằng 0.
    /// </summary>
    public struct CrowdTierSettings
    {
        public QualityTier tier;

        /// <summary>Có chạy animation theo thời gian không. Bậc C: false.</summary>
        public bool animated;

        /// <summary>Số khung hình đổi mỗi giây. 0 nghĩa là đứng yên.</summary>
        public float animationFps;

        /// <summary>Trần GPU cho riêng khán đài, mili-giây.</summary>
        public float maxGpuBudgetMs;

        /// <summary>Có vẽ khán giả không. PHẢI true ở mọi bậc.</summary>
        public bool visible;

        public static CrowdTierSettings ForTier(QualityTier tier)
        {
            switch (tier)
            {
                case QualityTier.A:
                    return new CrowdTierSettings
                    {
                        tier = QualityTier.A,
                        animated = true,
                        animationFps = 12f,
                        maxGpuBudgetMs = CrowdBudget.MaxGpuBudgetMs,
                        visible = true
                    };

                case QualityTier.B:
                    return new CrowdTierSettings
                    {
                        tier = QualityTier.B,
                        animated = true,
                        animationFps = 8f,
                        maxGpuBudgetMs = 0.6f,
                        visible = true
                    };

                case QualityTier.C:
                default:
                    return new CrowdTierSettings
                    {
                        tier = QualityTier.C,
                        animated = false,      // khán giả tĩnh
                        animationFps = 0f,
                        maxGpuBudgetMs = 0.3f,
                        visible = true         // vẫn còn hình
                    };
            }
        }
    }

    /// <summary>Hằng số ngân sách của T30, để test và code đo đạc dùng chung một nguồn.</summary>
    public static class CrowdBudget
    {
        /// <summary>Trần GPU cho toàn bộ khán đài ở bậc A (ms) — ô nghiệm thu T30.</summary>
        public const float MaxGpuBudgetMs = 0.8f;

        /// <summary>Số draw call cho phép cho TOÀN BỘ khán đài. Một, không thương lượng.</summary>
        public const int MaxDrawCalls = 1;

        /// <summary>Số atlas cho phép. Một.</summary>
        public const int MaxAtlasCount = 1;
    }
}
