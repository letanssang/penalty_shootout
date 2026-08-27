using System;

namespace Eleven.Presentation.Skin
{
    /// <summary>
    /// Danh sách keyword và biến thể mà shader da BẮT BUỘC phải có lúc chạy trong build.
    ///
    /// Ô nghiệm thu "không có shader variant nào bị strip nhầm" là ô dễ tick khống nhất trong
    /// cả phase: trong Editor mọi biến thể đều biên dịch theo yêu cầu, nên không bao giờ thiếu.
    /// Chỉ khi build thật, bộ lọc biến thể mới cắt — và thứ bị cắt sẽ hiện ra là một nhân vật
    /// màu hồng (shader lỗi) hoặc, tệ hơn, một nhân vật trông bình thường nhưng chạy nhánh sai.
    ///
    /// Hai cách hỏng và cách chặn:
    ///   1. <c>shader_feature</c> bị lược khi không vật liệu nào trong build bật keyword đó.
    ///      → T31 khai keyword riêng bằng <c>multi_compile</c>, kiểm bằng test đọc thẳng file .shader.
    ///   2. Bộ lọc biến thể của URP cắt <c>_CLUSTER_LIGHT_LOOP</c> nếu URP Asset trong build
    ///      không bật Forward+. → phải kiểm trên build thật, EditMode không thấy được.
    /// </summary>
    public static class SkinShaderKeywords
    {
        public const string ShaderName = "Eleven/Skin";

        /// <summary>Keyword riêng của T31: bật đường tán xạ dưới bề mặt.</summary>
        public const string Sss = "_SKIN_SSS_ON";

        /// <summary>Keyword riêng của T31: ánh sáng xuyên qua vành tai / cánh mũi.</summary>
        public const string Transmission = "_SKIN_TRANSMISSION_ON";

        /// <summary>Keyword của URP 17 cho vòng lặp đèn phân cụm (Forward+). Đổi tên từ <c>_FORWARD_PLUS</c> ở 6.1.</summary>
        public const string ClusterLightLoop = "_CLUSTER_LIGHT_LOOP";

        /// <summary>
        /// Những keyword phải khai bằng <c>multi_compile</c> trong Skin.shader.
        /// Test đọc thẳng file để kiểm — đây là phần duy nhất của ô "không strip nhầm" mà
        /// EditMode chứng minh được.
        /// </summary>
        public static readonly string[] MustUseMultiCompile =
        {
            Sss,
            Transmission,
            ClusterLightLoop
        };
    }

    /// <summary>Một biến thể cụ thể, đủ để mô tả một dòng của báo cáo kiểm build.</summary>
    public struct SkinShaderVariant : IEquatable<SkinShaderVariant>
    {
        public bool sss;
        public bool transmission;
        public bool clusterLightLoop;

        public bool Equals(SkinShaderVariant other) =>
            sss == other.sss && transmission == other.transmission && clusterLightLoop == other.clusterLightLoop;

        public override bool Equals(object obj) => obj is SkinShaderVariant other && Equals(other);

        public override int GetHashCode() =>
            (sss ? 1 : 0) | (transmission ? 2 : 0) | (clusterLightLoop ? 4 : 0);

        public string Label =>
            (sss ? "sss+" : "sss-") + " " +
            (transmission ? "xuyên+" : "xuyên-") + " " +
            (clusterLightLoop ? "cluster+" : "cluster-");
    }

    /// <summary>
    /// Bộ biến thể tối thiểu phải sống sót qua build. Dự án chạy Forward+ ở cả ba bậc
    /// (docs/plan.md), nên <c>_CLUSTER_LIGHT_LOOP</c> luôn bật; ba biến thể còn lại là ba bậc
    /// thiết bị.
    /// </summary>
    public static class SkinVariantManifest
    {
        /// <summary>Biến thể mà một bậc thiết bị sẽ chạy.</summary>
        public static SkinShaderVariant ForTier(Eleven.Core.QualityTier tier)
        {
            SkinSssSettings settings = SkinSssSettings.ForTier(tier);
            SkinKeywordSet keywords = SkinKeywordSet.For(in settings);

            return new SkinShaderVariant
            {
                sss = keywords.sss,
                transmission = keywords.transmission,
                clusterLightLoop = true      // dự án dùng Forward+ ở mọi bậc
            };
        }

        /// <summary>
        /// Ba biến thể bắt buộc, một cho mỗi bậc. Cấp phát một mảng — chỉ dùng khi dựng báo cáo
        /// hoặc trong test, không bao giờ mỗi khung hình.
        /// </summary>
        public static SkinShaderVariant[] Required()
        {
            return new[]
            {
                ForTier(Eleven.Core.QualityTier.A),
                ForTier(Eleven.Core.QualityTier.B),
                ForTier(Eleven.Core.QualityTier.C)
            };
        }
    }
}
