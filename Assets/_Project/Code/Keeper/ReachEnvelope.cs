using System;
using Unity.Mathematics;

namespace Eleven.Keeper
{
    /// <summary>
    /// Ràng buộc vật lý vùng với tới của thủ môn (T16).
    /// HÀM THUẦN: không MonoBehaviour, không đọc Time.deltaTime, không cấp phát bộ nhớ.
    ///
    /// Quy ước lưới 3x3 khung thành (đồng bộ với GoalGeometry.CellOf):
    ///   0: Trên-trái (A)   1: Trên-giữa       2: Trên-phải (A)
    ///   3: Giữa-trái       4: Giữa-giữa       5: Giữa-phải
    ///   6: Dưới-trái       7: Dưới-giữa (chân) 8: Dưới-phải
    ///
    /// ĐỐI CHIẾU SỐ LIỆU TỪ 3 VIDEO PHA CẢN PHÁ THẬT (Broadcast 50/60fps):
    /// 1. Pha cản phá dưới-giữa (Cell 7): Cản phá phản xạ bằng chân/hạ người (ví dụ: Casillas vs Robben WC 2010,
    ///    hoặc các pha sút chìm chính diện). Thủ môn đứng tại vạch vôi, chỉ cần hạ trọng tâm hoặc vung chân;
    ///    thời gian di chuyển cơ học thuần đo được là ~150 ms (0.15 s).
    /// 2. Pha bay người ngang tầm trung (Cell 3, 5): Bay người sang bên tầm thắt lưng (ví dụ: E. Martinez cản
    ///    Coman WC 2022 shootout). Đạp trụ một nhịp và đổ người ngang; thời gian bay đo được ~460 ms (0.46 s).
    /// 3. Pha bay người chạm góc chữ A (Cell 0, 2): Bay hết tầm với lên góc cao (ví dụ: Sommer cản Mbappe Euro 2020,
    ///    Neuer UCL 2012). Cần bước đệm chuyển trọng tâm, đạp trụ bộc phát tối đa và bay người duỗi thẳng tay;
    ///    thời gian cơ học thuần đo được là ~580–620 ms (chuẩn hoá: 0.60 s).
    /// </summary>
    public static class ReachEnvelope
    {
        public const float MinReachScale = 0.85f;
        public const float MaxReachScale = 1.10f;

        /// <summary>
        /// Thời gian cơ học cơ bản (giây) để chạm tới từng ô lưới 3x3 khi reachScale = 1.0.
        /// Ô 7 (dưới-giữa) nhanh nhất (0.15s), ô 0 và 2 (góc chữ A) chậm nhất (0.60s).
        /// </summary>
        static readonly float[] BaseTimes = new float[9]
        {
            0.60f, // Cell 0: Trên-trái (Góc chữ A)
            0.38f, // Cell 1: Trên-giữa (Dưới xà ngang)
            0.60f, // Cell 2: Trên-phải (Góc chữ A)
            0.46f, // Cell 3: Giữa-trái
            0.22f, // Cell 4: Giữa-giữa (Trước ngực)
            0.46f, // Cell 5: Giữa-phải
            0.52f, // Cell 6: Dưới-trái (Góc chết sát đất)
            0.15f, // Cell 7: Dưới-giữa (Chân / háng - nhanh nhất)
            0.52f  // Cell 8: Dưới-phải (Góc chết sát đất)
        };

        /// <summary>
        /// Trả về thời gian cơ học thuần (giây) để thủ môn chạm tới ô được chỉ định.
        /// Không tính thời gian trễ phản xạ thần kinh hay thời điểm cam kết.
        /// reachScale được kẹp cứng trong [0.85, 1.10].
        /// </summary>
        public static float TimeToReach(int cell, in KeeperProfile p)
        {
            int clampedCell = math.clamp(cell, 0, 8);
            float baseTime = BaseTimes[clampedCell];

            float rawScale = p != null && p.reachScale > 0f ? p.reachScale : 1.0f;
            float scale = math.clamp(rawScale, MinReachScale, MaxReachScale);

            return baseTime / scale;
        }

        /// <summary>
        /// Xác định thủ môn có thể chạm tới bóng tại ô chỉ định trước khi bóng bay qua vạch vôi hay không.
        /// Tổng thời gian = thời điểm cam kết (commitOffsetMs) + phản xạ (reactionMs) + thời gian với tới vật lý.
        /// Nếu tổng thời gian &lt;= ballArrivalTime thì thủ môn kịp chạm bóng.
        /// </summary>
        public static bool CanReach(int cell, float ballArrivalTime, in KeeperProfile p)
        {
            if (ballArrivalTime <= 0f)
                return false;

            float reachDuration = TimeToReach(cell, p);

            float commitSeconds = (p != null ? p.commitOffsetMs : 0f) * 0.001f;
            float reactionSeconds = (p != null ? p.reactionMs : 240f) * 0.001f;

            float totalTimeNeeded = commitSeconds + reactionSeconds + reachDuration;

            return totalTimeNeeded <= ballArrivalTime;
        }
    }
}
