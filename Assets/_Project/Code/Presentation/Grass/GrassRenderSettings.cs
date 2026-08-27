using System;

namespace Eleven.Presentation.Grass
{
    /// <summary>
    /// Ba công tắc mà ô nghiệm thu T29 bắt phải đo riêng: alpha clip, đổ bóng, gió.
    /// Ba công tắc nhị phân cho đúng tám tổ hợp — đúng bằng số dòng của bảng so sánh.
    ///
    /// Chỉ số biến thể được mã hoá bằng bit để bảng đo và mã dựng hình dùng CHUNG một cách
    /// đánh số, không ai phải nhớ thứ tự dòng trong bảng:
    ///   bit 0 = alphaClip · bit 1 = castShadows · bit 2 = wind
    /// </summary>
    public struct GrassRenderSettings : IEquatable<GrassRenderSettings>
    {
        /// <summary>Cắt alpha trong fragment. Tắt đi thì cỏ thành tấm bảng đặc — xấu nhưng nhanh.</summary>
        public bool alphaClip;

        /// <summary>Có ghi vào shadow map không.</summary>
        public bool castShadows;

        /// <summary>Có dao động theo gió trong vertex shader không.</summary>
        public bool wind;

        public const int VariantCount = 8;

        public const int BitAlphaClip = 1;
        public const int BitCastShadows = 2;
        public const int BitWind = 4;

        public int VariantIndex =>
            (alphaClip ? BitAlphaClip : 0) |
            (castShadows ? BitCastShadows : 0) |
            (wind ? BitWind : 0);

        public static GrassRenderSettings FromVariantIndex(int index)
        {
            return new GrassRenderSettings
            {
                alphaClip = (index & BitAlphaClip) != 0,
                castShadows = (index & BitCastShadows) != 0,
                wind = (index & BitWind) != 0
            };
        }

        /// <summary>Cấu hình mặc định của bậc A: đủ cả ba, tức là dòng đắt nhất của bảng.</summary>
        public static GrassRenderSettings Full => FromVariantIndex(BitAlphaClip | BitCastShadows | BitWind);

        /// <summary>Nhãn ngắn cho dòng bảng đo. Chỉ dùng khi ghi báo cáo, không dùng mỗi khung hình.</summary>
        public string Label =>
            (alphaClip ? "clip+" : "clip-") + " " +
            (castShadows ? "bóng+" : "bóng-") + " " +
            (wind ? "gió+" : "gió-");

        /// <summary>
        /// Tám tổ hợp, không trùng nhau, theo đúng thứ tự chỉ số biến thể.
        /// Cấp phát một mảng — chỉ gọi khi dựng bảng đo, tuyệt đối không gọi mỗi khung hình.
        /// </summary>
        public static GrassRenderSettings[] AllVariants()
        {
            var result = new GrassRenderSettings[VariantCount];
            for (int i = 0; i < VariantCount; i++)
            {
                result[i] = FromVariantIndex(i);
            }
            return result;
        }

        public bool Equals(GrassRenderSettings other) => VariantIndex == other.VariantIndex;
        public override bool Equals(object obj) => obj is GrassRenderSettings other && Equals(other);
        public override int GetHashCode() => VariantIndex;
    }

    /// <summary>Mô tả một lần vẽ cỏ. Trả về theo giá trị, không cấp phát.</summary>
    public struct GrassRenderBatch
    {
        public int instanceCount;
        public int drawCallCount;
        public int variantIndex;

        /// <summary>Bậc C: không vẽ túm nào, mặt sân dùng texture thay thế.</summary>
        public bool usesGroundTexture;

        public int GpuBufferBytes => instanceCount * GrassInstanceGpu.SizeInBytes;
    }
}
