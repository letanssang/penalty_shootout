using System;
using Unity.Mathematics;

namespace Eleven.Ball
{
    /// <summary>
    /// Tham số khí động của bóng size 5. Giá trị mặc định lấy từ docs/plan.md mục 05.
    ///
    /// Đây là ĐIỂM KHỞI ĐẦU, không phải điểm kết thúc: T12 sẽ fit lại cdLow/cdHigh/cdVLow/
    /// cdVHigh/liftCoefficient/spinDecayPerSecond từ video penalty thật. Vì vậy mọi con số
    /// đều nằm trong struct này chứ không hằng số hoá trong solver.
    /// </summary>
    [Serializable]
    public struct BallParams
    {
        public float mass;            // kg
        public float radius;          // m
        public float airDensity;      // kg/m^3
        public float gravity;         // m/s^2, độ lớn — solver áp theo -Y

        public float cdLow;           // hệ số cản dòng chảy tầng, dưới cdVLow
        public float cdHigh;          // hệ số cản sau khủng hoảng cản, trên cdVHigh
        public float cdVLow;          // m/s, đầu dưới của dải nội suy
        public float cdVHigh;         // m/s, đầu trên của dải nội suy

        public float liftCoefficient;      // Cl của lực Magnus
        public float spinDecayPerSecond;   // hằng số phân rã mũ của xoáy: omega(t) = omega0 * exp(-k t)

        /// <summary>Diện tích mặt cắt (m^2). Suy ra từ bán kính, không phải hằng số rời.</summary>
        public float CrossSectionArea => math.PI * radius * radius;

        /// <summary>
        /// Bóng thi đấu size 5: m 0.43 · r 0.11 · rho 1.225 · g 9.81 ·
        /// Cd 0.45 → 0.22 giữa 12 và 20 m/s · Cl 0.25.
        /// </summary>
        public static BallParams Default => new BallParams
        {
            mass = 0.43f,
            radius = 0.11f,
            airDensity = 1.225f,
            gravity = 9.81f,

            cdLow = 0.45f,
            cdHigh = 0.22f,
            cdVLow = 12f,
            cdVHigh = 20f,

            liftCoefficient = 0.25f,
            // Xoáy tắt chậm trong pha bay 0.45s: mất khoảng 4.4% mỗi giây.
            // Con số này chưa được đo — T12 fit lại từ video thật.
            spinDecayPerSecond = 0.045f,
        };
    }
}
