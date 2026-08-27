using Unity.Mathematics;

namespace Eleven.Presentation.Grass
{
    /// <summary>
    /// Mật độ cỏ theo bán kính tính từ chấm phạt đền (gốc toạ độ).
    ///
    /// Vì sao giảm dần theo bán kính chứ không rải đều: camera luân lưu luôn nhìn về khung
    /// thành từ quanh chấm phạt đền (xem T26). Cỏ ở xa chiếm rất ít điểm ảnh nhưng vẫn tốn
    /// nguyên một lần dựng hình đỉnh và một lần ghi alpha — đó là chi phí thuần lãng phí.
    /// Đổi lại, cỏ gần camera là thứ người chơi thật sự nhìn thấy, nên giữ dày.
    ///
    /// ĐƠN VỊ: hàm này trả về số TÚM cỏ trên mỗi mét vuông, không phải số lá. Một túm là một
    /// cặp tấm bảng cắt chéo nhau, trên đó texture đã vẽ sẵn vài lá. Rải theo lá là cách chắc
    /// chắn vỡ ngân sách trên GPU di động.
    /// </summary>
    public static class GrassDensityField
    {
        /// <summary>Trong bán kính này mật độ giữ nguyên mức tối đa (mét).</summary>
        public const float FullDensityRadius = 12.0f;

        /// <summary>Ra tới bán kính này thì hết cỏ, nhường chỗ cho texture mặt sân (mét).</summary>
        public const float FadeEndRadius = 34.0f;

        /// <summary>Số túm cỏ trên mét vuông ở mật độ tối đa (tierDensity = 1).</summary>
        public const float BaseTuftsPerSquareMetre = 8.0f;

        /// <summary>
        /// Hệ số suy giảm theo bán kính, trong [0, 1]. Dùng smoothstep chứ không tuyến tính:
        /// chuyển tiếp tuyến tính để lại một đường tròn thấy rõ trên sân ở chỗ đạo hàm gãy.
        /// </summary>
        public static float RadialFalloff(float radius)
        {
            if (radius <= FullDensityRadius) return 1.0f;
            if (radius >= FadeEndRadius) return 0.0f;

            float t = (radius - FullDensityRadius) / (FadeEndRadius - FullDensityRadius);
            return 1.0f - math.smoothstep(0.0f, 1.0f, t);
        }

        /// <summary>
        /// Mật độ (túm/m²) tại bán kính đã cho, với hệ số bậc thiết bị lấy từ
        /// <c>TierProfile.grassDensity</c> (1.0 / 0.4 / 0.0).
        /// </summary>
        public static float DensityAt(float radius, float tierDensity)
        {
            if (tierDensity <= 0.0f) return 0.0f;
            return BaseTuftsPerSquareMetre * math.saturate(tierDensity) * RadialFalloff(math.max(0.0f, radius));
        }

        /// <summary>Xác suất nhận một ô lưới lấy mẫu, trong [0, 1]. Dùng khi rải cỏ.</summary>
        public static float AcceptProbability(float radius, float tierDensity, float cellArea)
        {
            return math.saturate(DensityAt(radius, tierDensity) * cellArea);
        }

        /// <summary>
        /// Ước lượng tổng số túm trong toàn vùng, tích phân giải tích hoá thành tổng Riemann
        /// theo vành khuyên. Dùng để chọn dung lượng bộ đệm trước khi rải, không dùng lúc chạy.
        /// </summary>
        public static int EstimateTotalTufts(float tierDensity, int rings = 512)
        {
            if (tierDensity <= 0.0f) return 0;

            double total = 0.0;
            double dr = FadeEndRadius / rings;

            for (int i = 0; i < rings; i++)
            {
                double r = (i + 0.5) * dr;
                double ringArea = 2.0 * math.PI_DBL * r * dr;
                total += ringArea * DensityAt((float)r, tierDensity);
            }

            return (int)math.round(total);
        }
    }
}
