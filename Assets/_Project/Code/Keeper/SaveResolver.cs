using Unity.Mathematics;
using Eleven.Ball;

namespace Eleven.Keeper
{
    /// <summary>
    /// Bộ xử lý kết quả cản phá (Save Resolution) tất định cho thủ môn.
    /// Tính toán phản xạ bóng và phân loại kết quả bắt/đẩy bóng không cấp phát GC.
    /// </summary>
    public static class SaveResolver
    {
        public const float NominalSpeed = 25f;
        public const float SpeedParryGain = 0.020f;
        public const float FingertipQuality = 0.15f;
        public const float QualityParrySplit = 0.50f;
        public const float ParryRestitution = 0.55f;
        public const float DeflectRestitution = 0.30f;
        public const float BallRadius = 0.11f;
        public const float ProbeTime = 0.02f;
        public const float DefaultParryChance = 0.45f;

        /// <summary>
        /// Xác định kết quả pha cứu thua của thủ môn dựa trên trạng thái bóng, quyết định bay người và vị trí tiếp xúc.
        /// </summary>
        /// <param name="atCrossing">Trạng thái bóng tại mặt phẳng khung thành.</param>
        /// <param name="dive">Quyết định bay người của thủ môn.</param>
        /// <param name="handDistanceToBall">Khoảng cách từ tâm tay thủ môn đến bóng.</param>
        /// <param name="p">Hồ sơ năng lực thủ môn (có thể null).</param>
        /// <param name="seed">Hạt giống ngẫu nhiên đảm bảo tính tất định.</param>
        /// <param name="deflectVelocity">Vận tốc phản xạ của bóng sau khi chạm tay (bằng 0 nếu bắt dính hoặc hụt).</param>
        /// <returns>Loại kết quả cứu thua.</returns>
        public static SaveResult Resolve(in BallState atCrossing, in DiveDecision dive,
                                         float handDistanceToBall, KeeperProfile p,
                                         uint seed, out float3 deflectVelocity)
        {
            deflectVelocity = float3.zero;

            float maxReach = KeeperReach.CatchRadius(dive.isFullDive, p);

            // Dùng !(handDistanceToBall >= 0f) để chặn đồng thời cả giá trị âm và NaN mà không cần rẽ nhánh phụ.
            // Nhánh hụt tầm tuyệt đối không dùng ngẫu nhiên để tránh lệch trạng thái hạt giống khi mô phỏng.
            if (!(handDistanceToBall >= 0f) || handDistanceToBall > maxReach)
            {
                return SaveResult.Missed;
            }

            float quality = 1f - math.saturate(handDistanceToBall / maxReach);
            float speed = math.length(atCrossing.velocity);

            // Neo xác suất đẩy bóng ở mốc NominalSpeed (25 m/s) để phân bố ở tốc độ chuẩn luôn khớp với parryChance thực tế.
            float baseParry = p != null ? p.parryChance : DefaultParryChance;
            float parryProb = math.saturate(baseParry + (speed - NominalSpeed) * SpeedParryGain);

            // Unity.Mathematics.Random khởi tạo với state 0 sẽ ném ngoại lệ, cần fallback sang 1u.
            var rng = new Unity.Mathematics.Random(seed != 0u ? seed : 1u);
            float roll = rng.NextFloat();

            bool caught = roll >= parryProb && quality >= FingertipQuality;
            if (caught)
            {
                return SaveResult.Caught;
            }

            float2 handXY = GoalFrame.CellCenter(dive.targetCell).xy;
            float2 awayXY = atCrossing.position.xy - handXY;
            float awayLen = math.length(awayXY);
            float2 inPlane = awayLen > 1e-5f ? awayXY / awayLen : float2.zero;

            // Pháp tuyến mặt "bàn tay": hướng ra xa tâm tay trên mặt phẳng XY, và ngược về phía sân (-Z).
            //
            // TRỌNG SỐ NGANG PHẢI THEO ĐỘ LỆCH, KHÔNG ĐƯỢC CỐ ĐỊNH:
            // nếu để nguyên (inPlane.x, inPlane.y, -1) thì pháp tuyến luôn nghiêng đúng 45 độ
            // bất kể bóng chạm giữa lòng bàn tay hay chạm hờ mép ngoài — hệ quả là một pha
            // đấm bóng chính diện lệch 5 cm cũng bắn bóng đi NGANG 90 độ dọc vạch vôi thay vì
            // bật ngược ra sân. (1 - quality) chính bằng handDistanceToBall / maxReach, nên:
            // chạm đúng lòng bàn tay -> pháp tuyến thuần (0,0,-1), bóng bật thẳng ngược ra;
            // chạm mép ngoài tầm với -> nghiêng dần tới 45 độ, bóng chỉ bị quẹt lệch hướng.
            float lateral = 1f - quality;
            float3 n = math.normalize(new float3(inPlane.x * lateral, inPlane.y * lateral, -1f));

            float3 v = atCrossing.velocity;
            float3 r = v - 2f * math.dot(v, n) * n;

            float rLen = math.length(r);
            float3 dir;
            if (rLen > 1e-5f)
            {
                dir = r / rLen;
            }
            else if (speed > 1e-5f)
            {
                // Khi phản xạ suy biến (tia tới song song hoặc vuông góc bất thường), trả ngược về hướng bóng tới.
                dir = -v / speed;
            }
            else
            {
                dir = new float3(0f, 0f, -1f);
            }

            bool solid = quality >= QualityParrySplit;
            float restitution = solid ? ParryRestitution : DeflectRestitution;

            // Chuẩn hóa vector hướng rồi mới nhân vận tốc để đảm bảo độ lớn vận tốc sau va chạm
            // luôn bằng restitution * speed, triệt tiêu hoàn toàn sai số trôi số thực.
            deflectVelocity = dir * (restitution * speed);

            // Dò trước quỹ đạo sau khi chạm tay trong khoảng ProbeTime để xác định bóng có đập trúng cột/xà hay không.
            float3 probe = atCrossing.position + deflectVelocity * ProbeTime;
            if (GoalFrame.DistanceToFrame(probe.xy) <= GoalFrame.PostRadius + BallRadius)
            {
                return SaveResult.OntoPost;
            }

            return solid ? SaveResult.Parried : SaveResult.Deflected;
        }
    }
}