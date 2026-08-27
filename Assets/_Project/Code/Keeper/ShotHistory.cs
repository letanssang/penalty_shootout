using Unity.Collections;
using Unity.Mathematics;

namespace Eleven.Keeper
{
    /// <summary>
    /// Lịch sử các cú sút gần đây của người sút trong một loạt luân lưu (T20).
    /// Thủ môn dùng lịch sử này để "bắt bài" thói quen.
    ///
    /// HÀM THUẦN: không MonoBehaviour, không cấp phát GC, dùng FixedList.
    /// Tất định theo input — cùng chuỗi Record cho cùng Prior.
    ///
    /// Quy ước ô 0–8 đồng bộ với GoalGeometry / ReachEnvelope:
    ///   0: Trên-trái   1: Trên-giữa   2: Trên-phải
    ///   3: Giữa-trái   4: Giữa-giữa   5: Giữa-phải
    ///   6: Dưới-trái   7: Dưới-giữa   8: Dưới-phải
    /// </summary>
    public struct ShotHistory
    {
        /// <summary>
        /// Danh sách các ô mà người sút đã sút, theo thứ tự thời gian.
        /// Phần tử cuối là cú sút gần nhất. Tối đa 20 cú.
        /// </summary>
        public FixedList128Bytes<byte> cells;

        /// <summary>
        /// Ghi nhận một cú sút mới vào ô <paramref name="cell"/>.
        /// Nếu danh sách đầy (20 cú), xoá cú cũ nhất (đầu danh sách).
        /// Cell được kẹp vào [0, 8].
        /// </summary>
        public void Record(int cell)
        {
            byte clampedCell = (byte)math.clamp(cell, 0, 8);

            // FixedList128Bytes<byte> chứa tối đa ~126 phần tử,
            // nhưng hợp đồng yêu cầu chỉ giữ 20 cú gần nhất.
            if (cells.Length >= 20)
            {
                // Dịch tất cả lên 1 vị trí, xoá phần tử đầu
                for (int i = 0; i < cells.Length - 1; i++)
                    cells[i] = cells[i + 1];
                cells.Length -= 1;
            }

            cells.Add(clampedCell);
        }

        /// <summary>
        /// Trả về phân phối prior trên 9 ô dựa trên lịch sử sút.
        ///
        /// Thuật toán:
        /// 1. Bắt đầu với phân phối đều (1/9 cho mỗi ô)
        /// 2. Duyệt qua từng cú sút trong lịch sử, cú gần nhất có trọng số cao nhất
        /// 3. Trọng số cú thứ k (từ cuối) = weight * decay^k
        /// 4. Cộng trọng số vào ô tương ứng, rồi chuẩn hoá tổng = 1
        ///
        /// Nếu <paramref name="weight"/> = 0, trả về phân phối đều tuyệt đối.
        /// Nếu lịch sử rỗng, trả về phân phối đều tuyệt đối.
        /// </summary>
        /// <param name="weight">Trọng số trí nhớ (0 = không nhớ, 1 = nhớ mạnh)</param>
        /// <param name="decay">Hệ số suy giảm theo thời gian (0..1). 0.8 = cú cách 5 lượt chỉ còn 0.33 trọng số</param>
        public FixedList64Bytes<float> Prior(float weight, float decay)
        {
            var prior = new FixedList64Bytes<float>();
            const float uniform = 1f / 9f;

            // Khởi tạo phân phối đều
            for (int i = 0; i < 9; i++)
                prior.Add(uniform);

            // Nếu không có trí nhớ hoặc lịch sử rỗng → đều tuyệt đối
            if (weight <= 0f || cells.Length == 0)
                return prior;

            float clampedWeight = math.saturate(weight);
            float clampedDecay = math.saturate(decay);

            // Tính phân phối lịch sử trực tiếp (không lưu trung gian)
            // Cú gần nhất (cuối danh sách) có trọng số cao nhất (decay^0 = 1)
            // Cú xa nhất có trọng số thấp nhất (decay^(n-1))
            // Dùng mảng stack 9 phần tử thay vì FixedList để tránh overflow
            float hist0 = 0f, hist1 = 0f, hist2 = 0f, hist3 = 0f, hist4 = 0f;
            float hist5 = 0f, hist6 = 0f, hist7 = 0f, hist8 = 0f;
            float totalHistoryWeight = 0f;

            for (int i = 0; i < cells.Length; i++)
            {
                int distanceFromEnd = cells.Length - 1 - i;
                float w = math.pow(clampedDecay, distanceFromEnd);
                totalHistoryWeight += w;

                int cellIdx = cells[i];
                switch (cellIdx)
                {
                    case 0: hist0 += w; break;
                    case 1: hist1 += w; break;
                    case 2: hist2 += w; break;
                    case 3: hist3 += w; break;
                    case 4: hist4 += w; break;
                    case 5: hist5 += w; break;
                    case 6: hist6 += w; break;
                    case 7: hist7 += w; break;
                    case 8: hist8 += w; break;
                }
            }

            // Tránh chia cho 0
            if (totalHistoryWeight <= 0f)
                return prior;

            float invTotal = 1f / totalHistoryWeight;

            // Trộn: prior_final = (1 - weight) * uniform + weight * (histN / totalWeight)
            prior[0] = (1f - clampedWeight) * uniform + clampedWeight * hist0 * invTotal;
            prior[1] = (1f - clampedWeight) * uniform + clampedWeight * hist1 * invTotal;
            prior[2] = (1f - clampedWeight) * uniform + clampedWeight * hist2 * invTotal;
            prior[3] = (1f - clampedWeight) * uniform + clampedWeight * hist3 * invTotal;
            prior[4] = (1f - clampedWeight) * uniform + clampedWeight * hist4 * invTotal;
            prior[5] = (1f - clampedWeight) * uniform + clampedWeight * hist5 * invTotal;
            prior[6] = (1f - clampedWeight) * uniform + clampedWeight * hist6 * invTotal;
            prior[7] = (1f - clampedWeight) * uniform + clampedWeight * hist7 * invTotal;
            prior[8] = (1f - clampedWeight) * uniform + clampedWeight * hist8 * invTotal;

            // Chuẩn hoá để tổng = 1 chính xác
            float sum = 0f;
            for (int i = 0; i < 9; i++)
                sum += prior[i];

            if (sum > 0f)
            {
                float invSum = 1f / sum;
                for (int i = 0; i < 9; i++)
                    prior[i] *= invSum;
            }

            return prior;
        }

        /// <summary>
        /// Xoá toàn bộ lịch sử (dùng khi sang trận mới).
        /// </summary>
        public void Clear()
        {
            cells.Clear();
        }
    }
}
