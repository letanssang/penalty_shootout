using Unity.Mathematics;
using Eleven.Ball;
using Eleven.Keeper;

namespace Eleven.Match
{
    public enum ShotOutcome
    {
        Goal, PostIn, PostOut, Crossbar, WideLeft,
        WideRight, Over, Short, Saved
    }

    /// <summary>
    /// Xác định vào/trượt bằng hình học giải tích thuần, không dùng collider của Unity —
    /// kết quả phải tất định và tính trước được cho AI thủ môn (T16-T21) trước khi bóng
    /// thực sự bay tới.
    ///
    /// GHI CHÚ VỀ DIỄN GIẢI HỢP ĐỒNG (cần người xác nhận):
    /// Backlog T10 chỉ cho chữ ký hàm, không nói rõ ba điểm sau — đây là diễn giải của tôi,
    /// suy ra từ quy ước đã có ở T06/T08 (chấm phạt đền ở gốc toạ độ, bóng bay theo +Z):
    ///
    /// 1. MẶT PHẲNG KHUNG THÀNH nằm ở z = PenaltyDistance (11m), KHÔNG PHẢI z = 0. Lý do:
    ///    test T06 (BallSolverTests) luôn khởi tạo bóng ở position = 0 rồi bay theo +Z tới
    ///    khi position.z >= 11 — tức chấm phạt đền ở gốc toạ độ, khung thành ở z = 11.
    ///    Comment gốc trong backlog T08 ghi "(z = 0)" nhưng đó chỉ là ví dụ minh hoạ cho
    ///    FirstCrossing (hàm nhận planeZ làm tham số, không hardcode) — không mâu thuẫn.
    /// 2. CỘT/XÀ NGANG được mô hình là đường tâm nằm NGOÀI mép trong đúng bằng PostRadius,
    ///    vì luật IFAB đo Width/Height tới MÉP TRONG của khung — khớp với ô nghiệm thu
    ///    "sút đúng vào x = 3.66 (mép trong cột)".
    /// 3. Khi chạm khung (cột hoặc xà), tôi không mô phỏng bật lại vật lý (không có dữ liệu
    ///    hệ số nảy). PostIn/PostOut phân biệt bằng: nếu bỏ khung đi, quỹ đạo phân tích có
    ///    lọt vào trong khung không. Xà ngang không phân biệt trong/ngoài vì enum chỉ có
    ///    một giá trị Crossbar.
    ///
    /// Saved KHÔNG được hàm này trả về — đó là kết quả của T21 (thủ môn), nằm ngoài phạm vi
    /// hình học thuần túy.
    /// </summary>
    public static class GoalGeometry
    {
        // Số liệu và lưới ô nay do Eleven.Keeper.GoalFrame giữ, vì T21 (SaveResolver) nằm
        // trong Eleven.Keeper mà asmdef đó KHÔNG tham chiếu ngược lên Eleven.Match được.
        // Ở đây chỉ chuyển tiếp để mã gọi cũ và bộ test T10 không phải đổi một dòng nào.
        public const float Width = GoalFrame.Width;
        public const float Height = GoalFrame.Height;
        public const float PostRadius = GoalFrame.PostRadius;
        public const float PenaltyDistance = GoalFrame.PenaltyDistance;

        // Đường tâm của cột/xà nằm ngoài mép trong đúng PostRadius (xem ghi chú diễn giải #2).
        const float PostCenterX = GoalFrame.PostCenterX;
        const float CrossbarCenterY = GoalFrame.CrossbarCenterY;

        const float SimDt = 1f / 240f; // mịn hơn SimDt của BallDriver (1/120) để phân loại biên chính xác
        const float SafetyMaxTime = 30f; // trần an toàn nếu bóng không bao giờ tới đất lẫn khung thành

        /// <summary>Ô lưới 3x3 chứa điểm rơi. 0 = trên-trái .. 8 = dưới-phải. Luôn trả giá trị hợp lệ (kẹp biên).</summary>
        public static int CellOf(float3 crossingPoint) => GoalFrame.CellOf(crossingPoint);

        public static float3 CellCenter(int cell) => GoalFrame.CellCenter(cell);

        public static ShotOutcome Classify(in BallState start, in BallParams p,
                                           out float3 crossing, out int cell)
        {
            int maxSteps = (int)(SafetyMaxTime / SimDt) + 1;

            BallState prev = start;
            float zPrev = prev.position.z;
            float yPrev = prev.position.y;

            for (int i = 0; i < maxSteps; i++)
            {
                BallState cur = BallSolver.Step(prev, p, SimDt);
                float zCur = cur.position.z;
                float yCur = cur.position.y;

                bool crossedGoalPlane = (zPrev < PenaltyDistance) != (zCur < PenaltyDistance);
                // Một chiều (đang ở trên không rồi rơi xuống <=0) — không phải đổi dấu hai
                // chiều, vì bóng luôn XUẤT PHÁT ở y=0 (mặt đất); đổi dấu hai chiều sẽ hiểu
                // nhầm bước đầu tiên rời mặt đất là "chạm đất".
                bool crossedGround = yPrev > 0f && yCur <= 0f;

                if (crossedGoalPlane || crossedGround)
                {
                    float fracGoal = crossedGoalPlane ? SafeFrac(zPrev, zCur, PenaltyDistance) : float.PositiveInfinity;
                    float fracGround = crossedGround ? SafeFrac(yPrev, yCur, 0f) : float.PositiveInfinity;

                    if (crossedGround && fracGround <= fracGoal)
                    {
                        // Chạm đất trước khi tới mặt phẳng khung thành → hụt tầm.
                        float3 groundPoint = math.lerp(prev.position, cur.position, fracGround);
                        crossing = groundPoint;
                        cell = CellOf(new float3(math.clamp(groundPoint.x, -Width * 0.5f, Width * 0.5f), 0f, PenaltyDistance));
                        return ShotOutcome.Short;
                    }

                    float3 point = math.lerp(prev.position, cur.position, fracGoal);
                    crossing = point;
                    return ClassifyPoint(point, p, out cell);
                }

                prev = cur;
                zPrev = zCur;
                yPrev = yCur;
            }

            // Không bao giờ tới đất lẫn mặt phẳng khung thành trong 30s mô phỏng — trường hợp
            // suy biến cực hiếm (ví dụ vận tốc và trọng lực đều 0). Coi như hụt tầm.
            crossing = prev.position;
            cell = CellOf(new float3(math.clamp(prev.position.x, -Width * 0.5f, Width * 0.5f), 0f, PenaltyDistance));
            return ShotOutcome.Short;
        }

        static ShotOutcome ClassifyPoint(float3 point, in BallParams p, out int cell)
        {
            cell = CellOf(point);
            float effRadius = PostRadius + p.radius;

            float2 xy = point.xy;
            float distLeftPost = DistancePointToSegment(xy, new float2(-PostCenterX, 0f), new float2(-PostCenterX, CrossbarCenterY));
            float distRightPost = DistancePointToSegment(xy, new float2(PostCenterX, 0f), new float2(PostCenterX, CrossbarCenterY));
            float distCrossbar = DistancePointToSegment(xy, new float2(-PostCenterX, CrossbarCenterY), new float2(PostCenterX, CrossbarCenterY));

            // Biên bao gồm cả mép (>=/<=): mép trong cột/xà tính là "trong khung", cùng quy
            // ước với vạch biên ở hầu hết luật thể thao (chạm vạch = còn trong sân).
            bool insideFrame = point.x >= -Width * 0.5f && point.x <= Width * 0.5f
                             && point.y >= 0f && point.y <= Height;

            if (distCrossbar <= effRadius && distCrossbar <= distLeftPost && distCrossbar <= distRightPost)
                return ShotOutcome.Crossbar;

            if (distLeftPost <= effRadius || distRightPost <= effRadius)
                return insideFrame ? ShotOutcome.PostIn : ShotOutcome.PostOut;

            if (insideFrame)
                return ShotOutcome.Goal;

            if (point.y >= Height)
                return ShotOutcome.Over;

            return point.x <= 0f ? ShotOutcome.WideLeft : ShotOutcome.WideRight;
        }

        /// <summary>Tỉ lệ nội suy tuyến tính tại đó giá trị bằng đúng threshold, kẹp trong [0,1].</summary>
        static float SafeFrac(float prevValue, float curValue, float threshold)
        {
            float denom = curValue - prevValue;
            if (denom == 0f)
                return 0f;
            return math.saturate((threshold - prevValue) / denom);
        }

        /// <summary>Khoảng cách từ điểm tới đoạn thẳng trong mặt phẳng XY.</summary>
        static float DistancePointToSegment(float2 point, float2 a, float2 b)
            => GoalFrame.DistancePointToSegment(point, a, b);
    }
}
