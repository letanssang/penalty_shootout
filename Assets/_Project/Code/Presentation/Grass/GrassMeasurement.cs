using System;
using Unity.Mathematics;

namespace Eleven.Presentation.Grass
{
    /// <summary>
    /// Một dòng của bảng so sánh tám biến thể. Đây là chỗ dán SỐ ĐO THẬT từ máy —
    /// quy tắc 7 của docs/backlog/README.md: ghi lại số đo, đừng ghi cảm nhận.
    ///
    /// Kiểu này cố tình không có giá trị mặc định nào "coi như đạt": một dòng chưa đo thì
    /// <see cref="IsRecorded"/> = false, và bảng chưa đủ tám dòng thì không kết luận được gì.
    /// </summary>
    public struct GrassMeasurement
    {
        /// <summary>Chỉ số biến thể, xem <see cref="GrassRenderSettings.VariantIndex"/>.</summary>
        public int variantIndex;

        /// <summary>Thời gian GPU của riêng cỏ (ms), đo bằng profiler trên máy thật.</summary>
        public float gpuMs;

        /// <summary>Overdraw trung bình trên vùng có cỏ, đọc từ debug view của URP.</summary>
        public float averageOverdraw;

        /// <summary>Số túm cỏ lúc đo.</summary>
        public int instanceCount;

        /// <summary>Frame time khi BẬT cỏ (ms) — dùng cờ tắt riêng của <see cref="GrassField"/>.</summary>
        public float frameTimeWithGrassMs;

        /// <summary>Frame time khi TẮT cỏ (ms), cùng cảnh, cùng góc camera.</summary>
        public float frameTimeWithoutGrassMs;

        /// <summary>Tên máy đo. Bắt buộc — "đo trên Android" không phải là một số đo.</summary>
        public string deviceName;

        /// <summary>Đóng góp thật của cỏ vào frame time (ms).</summary>
        public float FrameTimeDeltaMs => frameTimeWithGrassMs - frameTimeWithoutGrassMs;

        /// <summary>
        /// Dòng này đã có số đo thật chưa. Đòi cả tên máy: một con số không có tên máy đi kèm
        /// thì không so sánh được với bất cứ dòng nào khác.
        /// </summary>
        public bool IsRecorded =>
            gpuMs > 0.0f &&
            instanceCount > 0 &&
            !string.IsNullOrEmpty(deviceName);
    }

    /// <summary>Kết luận của bảng đo. Không có giá trị nào nghĩa là "tự sửa giúp".</summary>
    public enum GrassVerdict
    {
        /// <summary>Chưa đo đủ tám dòng — chưa kết luận được gì.</summary>
        ChuaDoDu = 0,

        /// <summary>Biến thể mặc định của bậc A nằm trong ngân sách 2.0ms.</summary>
        Dat = 1,

        /// <summary>
        /// Vượt ngân sách. Ô nghiệm thu T29 ghi rõ: "báo cáo lại thay vì tự ý giảm chất lượng —
        /// quyết định cắt là của bạn". Nên đây là một kết luận để BÁO CÁO, không phải một tín
        /// hiệu để mã tự hạ mật độ.
        /// </summary>
        VuotNganSach_PhaiBaoCao = 2
    }

    /// <summary>
    /// Bảng tám dòng của ô nghiệm thu "đo cả ba biến thể... bảng so sánh 8 dòng".
    ///
    /// Là class chứ không phải struct vì nó ôm một mảng và bị sửa dần trong lúc đo; nó là kiểu
    /// BÁO CÁO, không bao giờ chạy trong vòng lặp mỗi khung hình nên quy tắc "struct, không cấp
    /// phát" của dự án không áp dụng ở đây.
    ///
    /// KHÔNG có hàm nào trong lớp này đụng vào <see cref="GrassTierSettings"/> hay
    /// <see cref="GrassField"/>. Đọc bảng không được phép làm cỏ thưa đi.
    /// </summary>
    public sealed class GrassMeasurementTable
    {
        private readonly GrassMeasurement[] _rows = new GrassMeasurement[GrassRenderSettings.VariantCount];

        public int RowCount => _rows.Length;

        public GrassMeasurement this[int variantIndex] => _rows[variantIndex];

        public void Record(in GrassMeasurement measurement)
        {
            int index = measurement.variantIndex;
            if (index < 0 || index >= _rows.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(measurement),
                    $"variantIndex {index} nằm ngoài dải 0..{_rows.Length - 1}.");
            }

            _rows[index] = measurement;
        }

        /// <summary>Đã có đủ số đo thật cho cả tám biến thể chưa.</summary>
        public bool IsComplete
        {
            get
            {
                for (int i = 0; i < _rows.Length; i++)
                {
                    if (!_rows[i].IsRecorded) return false;
                }
                return true;
            }
        }

        public int RecordedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _rows.Length; i++)
                {
                    if (_rows[i].IsRecorded) n++;
                }
                return n;
            }
        }

        /// <summary>Biến thể tốn nhất trong các dòng đã đo. Trả về -1 nếu chưa đo dòng nào.</summary>
        public int WorstVariantIndex
        {
            get
            {
                int worst = -1;
                float worstMs = float.NegativeInfinity;

                for (int i = 0; i < _rows.Length; i++)
                {
                    if (!_rows[i].IsRecorded) continue;
                    if (_rows[i].gpuMs > worstMs)
                    {
                        worstMs = _rows[i].gpuMs;
                        worst = i;
                    }
                }

                return worst;
            }
        }

        /// <summary>
        /// Kết luận cho một cấu hình dựng hình cụ thể (thường là cấu hình mặc định của bậc A).
        /// Hàm THUẦN: không sửa gì, không hạ mật độ, chỉ trả về một trong ba kết luận.
        /// </summary>
        public GrassVerdict Evaluate(in GrassRenderSettings render, float budgetMs = GrassBudget.MaxTierAGpuMs)
        {
            if (!IsComplete)
            {
                return GrassVerdict.ChuaDoDu;
            }

            GrassMeasurement row = _rows[render.VariantIndex];
            return row.gpuMs <= budgetMs ? GrassVerdict.Dat : GrassVerdict.VuotNganSach_PhaiBaoCao;
        }

        /// <summary>Chênh lệch giữa dòng đắt nhất và rẻ nhất (ms). Chưa đủ dữ liệu thì trả 0.</summary>
        public float SpreadMs()
        {
            if (!IsComplete) return 0.0f;

            float lo = float.PositiveInfinity;
            float hi = float.NegativeInfinity;

            for (int i = 0; i < _rows.Length; i++)
            {
                lo = math.min(lo, _rows[i].gpuMs);
                hi = math.max(hi, _rows[i].gpuMs);
            }

            return hi - lo;
        }
    }

    /// <summary>
    /// Ô nghiệm thu "bậc C tắt hoàn toàn, thay bằng texture, chênh lệch frame time được ghi lại".
    /// Một cặp số đo trên CÙNG một máy, cùng một cảnh.
    /// </summary>
    public struct GrassTierCComparison
    {
        /// <summary>Frame time khi còn cỏ instanced (ms).</summary>
        public float frameTimeWithGrassMs;

        /// <summary>Frame time khi đã thay bằng texture mặt sân (ms).</summary>
        public float frameTimeWithTextureMs;

        public string deviceName;

        public float DeltaMs => frameTimeWithGrassMs - frameTimeWithTextureMs;

        public bool IsRecorded =>
            frameTimeWithGrassMs > 0.0f &&
            frameTimeWithTextureMs > 0.0f &&
            !string.IsNullOrEmpty(deviceName);
    }
}
