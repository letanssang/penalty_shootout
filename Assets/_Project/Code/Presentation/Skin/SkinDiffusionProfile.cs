using Unity.Mathematics;

namespace Eleven.Presentation.Skin
{
    /// <summary>
    /// Hồ sơ khuếch tán dưới da người, xấp xỉ bằng tổng SÁU Gauss (d'Eon &amp; Luebke).
    ///
    /// Ý nghĩa vật lý: ánh sáng đi vào da không dội ngay ra tại điểm chạm mà lang thang trong
    /// lớp hạ bì rồi ló ra ở chỗ khác, cách đó vài milimét. Quãng đường đi được phụ thuộc bước
    /// sóng — ánh sáng ĐỎ đi xa nhất (hemoglobin ít hấp thụ đỏ), nên rìa bóng trên mặt người
    /// luôn ửng đỏ. Đó là toàn bộ hiệu ứng mà T31 phải tái tạo.
    ///
    /// ĐƠN VỊ: bán kính tính bằng MILIMÉT. Các phương sai dưới đây là số đã công bố cho da
    /// người, đo bằng mm² — đổi đơn vị là hỏng hồ sơ.
    ///
    /// Vì sao tính trước trên CPU thay vì blur nhiều pass lúc chạy: blur màn hình cần một
    /// render target riêng cho vùng da cộng vài pass ngang/dọc. Trên GPU di động đó là vài lần
    /// đọc-ghi toàn màn hình, tức là vài mili-giây — vượt xa ngân sách 0.5ms của cả hai nhân vật.
    /// Tích phân trước thành một LUT thì lúc chạy chỉ còn ĐÚNG MỘT lần lấy mẫu texture.
    /// </summary>
    public static class SkinDiffusionProfile
    {
        /// <summary>Số Gauss trong hồ sơ.</summary>
        public const int LobeCount = 6;

        /// <summary>Phương sai của từng Gauss (mm²).</summary>
        public static readonly float[] Variances =
        {
            0.0064f, 0.0484f, 0.1870f, 0.5670f, 1.9900f, 7.4100f
        };

        /// <summary>Trọng số RGB của từng Gauss. Tổng theo từng kênh xấp xỉ 1.</summary>
        public static readonly float3[] Weights =
        {
            new float3(0.233f, 0.455f, 0.649f),
            new float3(0.100f, 0.336f, 0.344f),
            new float3(0.118f, 0.198f, 0.000f),
            new float3(0.113f, 0.007f, 0.007f),
            new float3(0.358f, 0.004f, 0.000f),
            new float3(0.078f, 0.000f, 0.000f)
        };

        /// <summary>
        /// Một Gauss tại bán kính <paramref name="radiusMm"/>, chuẩn hoá MỘT CHIỀU.
        ///
        /// Vì sao một chiều chứ không phải hai: hồ sơ khuếch tán là hàm trên MẶT PHẲNG hai chiều,
        /// nhưng <see cref="SkinSssLut.Integrate"/> chỉ đi dọc MỘT cung tròn. Rút gọn đúng từ hai
        /// chiều xuống một chiều là lấy phân phối biên — tích phân Gauss 2D theo phương vuông góc
        /// với cung — và phân phối biên của Gauss 2D chính là Gauss 1D với cùng phương sai.
        ///
        /// Chuyện này KHÔNG phải chi tiết vụn: hệ số 1/(2πv) của bản hai chiều đè nặng lên các
        /// thuỳ hẹp theo tỷ lệ 1/σ, khiến thuỳ rộng — đúng những thuỳ mang màu đỏ đi xa — bị dìm
        /// khoảng ba mươi lần. Kết quả là một LUT gần như Lambert trơn, tức là làm cả T31 để
        /// không nhìn thấy gì. Số đo: ở bán kính 6mm, NdotL = -0.1, bản hai chiều cho kênh đỏ
        /// 0.005; bản một chiều cho 0.031.
        /// </summary>
        public static float Gaussian(float variance, float radiusMm)
        {
            return 1.0f / math.sqrt(2.0f * math.PI * variance)
                   * math.exp(-(radiusMm * radiusMm) / (2.0f * variance));
        }

        /// <summary>
        /// Trọng số khuếch tán RGB tại bán kính <paramref name="radiusMm"/> (mm).
        /// Không chuẩn hoá — hàm tích phân sẽ chia cho tổng trọng số.
        /// </summary>
        public static float3 Scatter(float radiusMm)
        {
            float r = math.abs(radiusMm);
            float3 sum = float3.zero;

            for (int i = 0; i < LobeCount; i++)
            {
                sum += Gaussian(Variances[i], r) * Weights[i];
            }

            return sum;
        }
    }
}
