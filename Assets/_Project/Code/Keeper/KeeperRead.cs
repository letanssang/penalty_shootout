using Unity.Collections;

namespace Eleven.Keeper
{
    /// <summary>
    /// Kết quả suy luận đọc vị của thủ môn (T18).
    /// Chứa phân phối xác suất trên 9 ô khung thành,
    /// ô được dự đoán cao nhất, và độ tự tin.
    ///
    /// Quy ước ô 0–8 đồng bộ với GoalGeometry / ReachEnvelope:
    ///   0: Trên-trái   1: Trên-giữa   2: Trên-phải
    ///   3: Giữa-trái   4: Giữa-giữa   5: Giữa-phải
    ///   6: Dưới-trái   7: Dưới-giữa   8: Dưới-phải
    /// </summary>
    public struct KeeperRead
    {
        /// <summary>
        /// Phân phối xác suất trên 9 ô. Tổng = 1, mỗi phần tử >= 0.
        /// </summary>
        public FixedList64Bytes<float> cellProbabilities;

        /// <summary>
        /// Chỉ số ô có xác suất cao nhất (0–8).
        /// </summary>
        public int bestCell;

        /// <summary>
        /// Độ tự tin tổng thể (0..1).
        /// 0 = không biết gì (phân phối đều), 1 = chắc chắn hoàn toàn.
        /// Tương quan thuận với độ chính xác thực tế.
        /// </summary>
        public float confidence;
    }
}
