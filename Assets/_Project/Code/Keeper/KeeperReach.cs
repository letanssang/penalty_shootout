using Unity.Mathematics;

namespace Eleven.Keeper
{
    /// <summary>
    /// Hình học TAY–BÓNG: bàn tay thủ môn ở đâu vào đúng lúc bóng qua vạch vôi, và cách đó
    /// bao xa (T21).
    ///
    /// TẠI SAO PHẢI CÓ LỚP NÀY (không nằm trong đặc tả gốc của T21):
    /// Hợp đồng T21 nhận <c>handDistanceToBall</c> làm THAM SỐ VÀO, nhưng trong toàn bộ repo
    /// không có gì sinh ra được giá trị đó — <see cref="DiveDecision"/> chỉ có
    /// <c>targetCell</c>, <c>commitTime</c>, <c>isFullDive</c>; không có vị trí tay, không có
    /// quỹ đạo bay người. Nếu chỉ làm đúng chữ trong đặc tả thì SaveResolver sẽ là một hàm
    /// phân loại không ai gọi được. Lớp này lấp đúng khoảng trống đó và không đụng vào
    /// chữ ký T21.
    ///
    /// HÀM THUẦN: không MonoBehaviour, không Time.deltaTime, không cấp phát, không ngẫu nhiên.
    ///
    /// ĐIỂM QUAN TRỌNG — ĐÂY LÀ THỨ THAY THẾ LUẬT "TRÚNG Ô THÌ CẢN ĐƯỢC":
    /// Trước T21, mô phỏng ở T25 phán đoán cản phá bằng so sánh nguyên ô (<c>targetCell ==
    /// trueCell</c>). Luật đó lượng tử hoá quá thô: sút vào sát mép ô mà thủ môn bay đúng ô
    /// vẫn tính là cản được, còn sút cách tay 5 cm nhưng lệch ô thì tính là thủng lưới.
    /// Ở đây khoảng cách là liên tục, nên bay lệch ô vẫn có thể với tới nếu bóng đi gần biên,
    /// và bay đúng ô vẫn có thể hụt nếu bóng nằm ở góc xa của ô.
    /// </summary>
    public static class KeeperReach
    {
        /// <summary>
        /// Độ cao tay thủ môn ở tư thế chuẩn bị (m). Đứng trên vạch vôi, tay ngang thắt lưng
        /// / ngực thấp. Không dùng tâm ô 4 (y = 1.22) vì tư thế set position thấp hơn thế.
        /// </summary>
        public const float RestHandHeight = 1.05f;

        /// <summary>
        /// Bán kính chạm bóng khi ĐANG BAY NGƯỜI hết tầm (m).
        /// Suy ra từ kích thước thật: găng tay người lớn dài ~0.24 m, tầm với từ cổ tay tới
        /// đầu ngón ~0.30 m, cộng bán kính bóng 0.11 m → ~0.41 m cho MỘT bàn tay. Cộng thêm
        /// dung sai vì cánh tay là một đoạn thẳng chứ không phải một điểm → 0.55 m.
        /// </summary>
        public const float DiveCatchRadius = 0.55f;

        /// <summary>
        /// Bán kính chạm bóng khi KHÔNG bay người (ô 1, 4, 7 — đứng hoặc hạ người tại chỗ).
        /// Lớn hơn lúc bay người vì thủ môn cản được bằng cả hai tay, thân và chân, chứ không
        /// chỉ đầu ngón tay của một tay duỗi hết cỡ.
        /// </summary>
        public const float StandingCatchRadius = 0.75f;

        /// <summary>Vị trí tay ở tư thế chuẩn bị, trên mặt phẳng khung thành.</summary>
        public static float3 RestHandPosition
            => new float3(0f, RestHandHeight, GoalFrame.PenaltyDistance);

        /// <summary>
        /// Bán kính chạm bóng theo kiểu cản phá, đã nhân hệ số tầm với của hồ sơ.
        /// Dùng <c>isFullDive</c> của quyết định chứ không tự suy lại từ chỉ số ô: bảng ô nào
        /// phải bay người là của <see cref="SimpleKeeperController"/>, chép lại ở đây sẽ thành
        /// nguồn sự thật thứ hai.
        /// </summary>
        public static float CatchRadius(bool isFullDive, in KeeperProfile p)
        {
            float baseRadius = isFullDive ? DiveCatchRadius : StandingCatchRadius;

            float rawScale = p != null && p.reachScale > 0f ? p.reachScale : 1.0f;
            float scale = math.clamp(rawScale, ReachEnvelope.MinReachScale, ReachEnvelope.MaxReachScale);

            return baseRadius * scale;
        }

        /// <summary>
        /// Phần đường bay người đã hoàn thành khi bóng tới vạch vôi, trong [0, 1].
        /// 1 = tay đã tới đúng tâm ô mục tiêu; 0 = chưa kịp rời tư thế chuẩn bị.
        ///
        /// BẤT BIẾN (có test ràng): <c>ReachProgress(...) >= 1</c> đúng khi và chỉ khi
        /// <see cref="ReachEnvelope.CanReach"/> trả về true, vì cả hai dùng đúng một ngân sách
        /// thời gian <c>commitOffsetMs + reactionMs + TimeToReach</c>. T21 không định nghĩa
        /// lại "kịp hay không kịp" — chỉ biến câu trả lời nhị phân của T16 thành liên tục.
        /// </summary>
        public static float ReachProgress(int cell, float ballArrivalTime, in KeeperProfile p)
        {
            if (ballArrivalTime <= 0f)
                return 0f;

            float reachDuration = ReachEnvelope.TimeToReach(cell, p);
            if (reachDuration <= 0f)
                return 1f;

            float commitSeconds = (p != null ? p.commitOffsetMs : 0f) * 0.001f;
            float reactionSeconds = (p != null ? p.reactionMs : 240f) * 0.001f;

            // commitOffsetMs âm nghĩa là cam kết TRƯỚC lúc chạm bóng, nên nó CỘNG thêm thời
            // gian cho thủ môn — dấu trừ ở đây là cố ý, khớp ReachEnvelope.CanReach.
            float budget = ballArrivalTime - commitSeconds - reactionSeconds;

            return math.saturate(budget / reachDuration);
        }

        /// <summary>
        /// Vị trí bàn tay tại đúng thời điểm bóng qua mặt phẳng khung thành.
        /// Tay đi thẳng từ tư thế chuẩn bị tới tâm ô mục tiêu, đi được bao xa thì tuỳ
        /// <see cref="ReachProgress"/>.
        /// </summary>
        public static float3 HandPositionAt(int cell, float ballArrivalTime, in KeeperProfile p)
        {
            float t = ReachProgress(cell, ballArrivalTime, p);
            return math.lerp(RestHandPosition, GoalFrame.CellCenter(cell), t);
        }

        /// <summary>
        /// Khoảng cách từ tay thủ môn tới bóng tại thời điểm bóng qua vạch vôi (m) — chính là
        /// tham số <c>handDistanceToBall</c> mà <see cref="SaveResolver.Resolve"/> cần.
        ///
        /// Đo TRONG MẶT PHẲNG khung thành (chỉ x, y): tại thời điểm này cả bóng lẫn tay đều
        /// nằm ở z = PenaltyDistance, nên thành phần z chỉ là nhiễu số học của phép nội suy
        /// điểm cắt, không mang thông tin.
        /// </summary>
        public static float HandDistanceToBall(in DiveDecision dive, float3 crossingPoint,
                                               float ballArrivalTime, in KeeperProfile p)
        {
            float3 hand = HandPositionAt(dive.targetCell, ballArrivalTime, p);
            return math.distance(hand.xy, crossingPoint.xy);
        }
    }
}
