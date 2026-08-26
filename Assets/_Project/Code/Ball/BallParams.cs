using System;
using Unity.Mathematics;

namespace Eleven.Ball
{
    /// <summary>
    /// Tham số khí động của bóng size 5. Giá trị mặc định lấy từ docs/plan.md mục 05.
    ///
    /// NGUỒN SỐ LIỆU (T12, 2026-08-26): đã đối chiếu với 5 video penalty eFootball
    /// 1920×1080 capture 60 fps. Kết luận: GIỮ NGUYÊN các giá trị dưới đây.
    /// Video xác nhận chúng hợp lý nhưng KHÔNG đủ chính xác để fit lại Cd/Cl — camera
    /// nhìn gần như dọc trục bay nên sai số chiều sâu nuốt trọn đại lượng cần đo
    /// (fit thô cho ra Cd âm, tức bóng tự tăng tốc). Chi tiết và thanh sai số:
    /// docs/research-t12-ket-qua-do-tu-video.md.
    ///
    /// Cái video ĐÃ chốt được, dùng làm mốc chỉnh cảm giác chơi chứ không phải tham số ở đây:
    /// tốc độ rời chân 28.9 ± 2.7 m/s, góc nâng 2.5–4°, thời gian bay ~0.38 s,
    /// trọng lực trong game 9.79 ± 1.91 m/s² (tức eFootball dùng trọng lực thật, không bịa).
    ///
    /// Mọi con số vẫn nằm trong struct này chứ không hằng số hoá trong solver, để lần sau
    /// có video quay GÓC NGANG thì fit lại được mà không phải đụng vào solver.
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
            // T12 KHÔNG đo được con số này: video eFootball không phân giải nổi vòng xoáy
            // của bóng ở 60 fps. Vẫn là giá trị sách vở, giữ nguyên cho tới khi có dữ liệu thật.
            spinDecayPerSecond = 0.045f,
        };
    }
}
