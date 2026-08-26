using Unity.Mathematics;

namespace Eleven.Shooter {
    /// <summary>
    /// Bốn kiểu chạm bóng. Tất cả đều phải với tới được bằng CỬ CHỈ, không nút bấm —
    /// xem <see cref="ShotMapper"/> để biết cử chỉ nào ra kiểu nào.
    /// </summary>
    public enum ShotType {
        /// <summary>Mu bàn chân: vuốt thẳng, mạnh. Đây là mặc định khi không cử chỉ nào khớp.</summary>
        Instep,
        /// <summary>Má trong: vuốt cong rõ rệt, đổi lại xoáy nhiều.</summary>
        InsideFoot,
        /// <summary>Lốp bóng: giật ngắn mà nhanh.</summary>
        Chip,
        /// <summary>Sút không xoáy: vuốt thẳng đét và mạnh. Bay bất ổn định (T15).</summary>
        Knuckle
    }

    /// <summary>
    /// Ý ĐỒ cú sút — sản phẩm của <see cref="ShotMapper"/>, đầu vào của khâu thực thi (T16).
    /// Đây thuần tuý là dữ liệu: không có hàm, không đọc engine.
    ///
    /// <c>aimPoint</c> ĐÃ ở KHÔNG GIAN THẾ GIỚI và ĐÃ cộng tản mát. Người tính nó là
    /// <see cref="AimProjector"/> — <see cref="ShotMapper"/> không bao giờ đụng tới camera.
    /// Lý do: quyết định camera 2026-08-26 (T26) chốt camera đứng yên trong giai đoạn đầu,
    /// nhưng khi sau này camera chạy thì chỉ mình AimProjector phải sửa.
    /// </summary>
    public struct ShotIntent {
        /// <summary>Điểm ngắm trong không gian thế giới, đã cộng tản mát (m).</summary>
        public float3 aimPoint;

        /// <summary>
        /// Vector xoáy (rad/s) theo trục thế giới. Quy ước khớp
        /// <c>BallSolver</c>: lực Magnus ∝ <c>cross(spin, velocity)</c>, bóng bay +Z,
        /// nên <c>spin = +Y</c> đẩy bóng sang +X (sang PHẢI theo mắt người sút).
        /// </summary>
        public float3 spin;

        /// <summary>Tốc độ rời chân (m/s).</summary>
        public float speed;

        public ShotType type;

        /// <summary>0..1, 1 = bấm đúng thời điểm tuyệt đối. Suy từ sai số thời điểm.</summary>
        public float quality;

        /// <summary>
        /// Cờ bay bất ổn định của cú knuckle. CỐ Ý tách khỏi <see cref="spin"/>:
        /// knuckle bất ổn vì bóng KHÔNG xoáy (dòng khí tách không đối xứng), nên giả lập
        /// nó bằng cách gán xoáy ngẫu nhiên là sai bản chất. T15 đọc cờ này.
        /// </summary>
        public bool unstable;

        /// <summary>
        /// Bán kính tản mát đã dùng (m, trên mặt phẳng khung thành). Chỉ để chẩn đoán/HUD —
        /// khâu thực thi không cần đọc, tản mát đã nằm trong <see cref="aimPoint"/> rồi.
        /// </summary>
        public float scatterRadius;
    }
}
