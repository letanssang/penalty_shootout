using Unity.Mathematics;

namespace Eleven.Keeper
{
    /// <summary>
    /// Kích thước khung thành và lưới ô 3x3 — NGUỒN SỰ THẬT DUY NHẤT cho cả
    /// <c>Eleven.Keeper</c> lẫn <c>Eleven.Match</c>.
    ///
    /// TẠI SAO NẰM Ở ĐÂY CHỨ KHÔNG PHẢI TRONG GoalGeometry:
    /// asmdef <c>Eleven.Match</c> tham chiếu <c>Eleven.Keeper</c>, KHÔNG có chiều ngược lại
    /// (tham chiếu vòng thì Unity từ chối biên dịch). Nhưng T21 cần biết cột dọc nằm ở đâu để
    /// trả về <see cref="SaveResult.OntoPost"/>, và T16 (ReachEnvelope) từ đầu đã dùng quy ước
    /// lưới 3x3 của khung thành mà chỉ ghi bằng comment "đồng bộ với GoalGeometry.CellOf" —
    /// tức là đã phụ thuộc ngầm rồi, chỉ chưa được ràng buộc bằng code.
    /// Đưa hằng số xuống đây rồi để GoalGeometry chuyển tiếp lên là cách duy nhất giữ MỘT
    /// nguồn số liệu mà không tạo tham chiếu vòng, và biến phụ thuộc ngầm thành phụ thuộc thật.
    ///
    /// Quy ước trục (khớp BallState): +Z từ chấm phạt đền hướng tới khung thành, +Y lên trên,
    /// +X sang phải. Mặt phẳng khung thành ở z = PenaltyDistance.
    ///
    /// Lưới 3x3, chỉ số 0..8 theo thứ tự đọc (hàng trên trước, trái sang phải):
    ///   0: Trên-trái   1: Trên-giữa   2: Trên-phải
    ///   3: Giữa-trái   4: Giữa-giữa   5: Giữa-phải
    ///   6: Dưới-trái   7: Dưới-giữa   8: Dưới-phải
    /// "Trái" ở đây là -X (nhìn từ phía người sút), khớp với GoalGeometry.CellCenter cũ.
    /// </summary>
    public static class GoalFrame
    {
        /// <summary>Chiều rộng giữa hai mép TRONG của cột dọc (m) — luật IFAB.</summary>
        public const float Width = 7.32f;

        /// <summary>Chiều cao từ mặt đất tới mép TRONG của xà ngang (m) — luật IFAB.</summary>
        public const float Height = 2.44f;

        /// <summary>Bán kính tiết diện cột dọc và xà ngang (m).</summary>
        public const float PostRadius = 0.06f;

        /// <summary>Khoảng cách từ chấm phạt đền tới mặt phẳng khung thành (m).</summary>
        public const float PenaltyDistance = 11f;

        /// <summary>Đường tâm cột dọc nằm NGOÀI mép trong đúng PostRadius (xem ghi chú #2 của GoalGeometry).</summary>
        public const float PostCenterX = Width * 0.5f + PostRadius;

        /// <summary>Đường tâm xà ngang nằm TRÊN mép trong đúng PostRadius.</summary>
        public const float CrossbarCenterY = Height + PostRadius;

        /// <summary>Bề rộng một ô lưới (m).</summary>
        public const float CellWidth = Width / 3f;

        /// <summary>Chiều cao một ô lưới (m).</summary>
        public const float CellHeight = Height / 3f;

        /// <summary>Ô lưới 3x3 chứa điểm rơi. 0 = trên-trái .. 8 = dưới-phải. Luôn trả giá trị hợp lệ (kẹp biên).</summary>
        public static int CellOf(float3 crossingPoint)
        {
            float colF = (crossingPoint.x + Width * 0.5f) / CellWidth;
            float rowF = (Height - crossingPoint.y) / CellHeight;

            int col = (int)math.clamp(math.floor(colF), 0f, 2f);
            int row = (int)math.clamp(math.floor(rowF), 0f, 2f);

            return row * 3 + col;
        }

        /// <summary>Tâm hình học của ô lưới, nằm trên mặt phẳng khung thành (z = PenaltyDistance).</summary>
        public static float3 CellCenter(int cell)
        {
            cell = math.clamp(cell, 0, 8);
            int row = cell / 3;
            int col = cell % 3;

            float x = -Width * 0.5f + (col + 0.5f) * CellWidth;
            float y = Height - (row + 0.5f) * CellHeight;

            return new float3(x, y, PenaltyDistance);
        }

        /// <summary>
        /// Khoảng cách từ một điểm tới đoạn thẳng trong mặt phẳng XY.
        /// Dùng chung cho phân loại chạm khung (GoalGeometry) và phán đoán OntoPost (T21).
        /// </summary>
        public static float DistancePointToSegment(float2 point, float2 a, float2 b)
        {
            float2 ab = b - a;
            float lenSq = math.dot(ab, ab);
            float t = lenSq > 0f ? math.saturate(math.dot(point - a, ab) / lenSq) : 0f;
            float2 closest = a + t * ab;
            return math.distance(point, closest);
        }

        /// <summary>
        /// Khoảng cách từ một điểm trên mặt phẳng khung thành tới đường tâm gần nhất của
        /// khung (hai cột dọc và xà ngang). Không tính bán kính bóng — người gọi tự cộng.
        /// </summary>
        public static float DistanceToFrame(float2 point)
        {
            float left = DistancePointToSegment(point, new float2(-PostCenterX, 0f), new float2(-PostCenterX, CrossbarCenterY));
            float right = DistancePointToSegment(point, new float2(PostCenterX, 0f), new float2(PostCenterX, CrossbarCenterY));
            float bar = DistancePointToSegment(point, new float2(-PostCenterX, CrossbarCenterY), new float2(PostCenterX, CrossbarCenterY));
            return math.min(left, math.min(right, bar));
        }
    }
}
