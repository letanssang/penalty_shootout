using System;
using System.Text;
using Unity.Mathematics;

namespace Eleven.Shooter {
    /// <summary>
    /// Xếp hạng thời điểm bấm. Chỉ có ba mức: người chơi không phân biệt nổi nhiều hơn ba
    /// mức trong một thao tác kéo dài 0.2 giây, và mỗi mức thêm là một dòng nữa phải cân bằng.
    /// Sớm hay muộn KHÔNG nằm trong enum này — dấu đã có sẵn trong
    /// <see cref="TimingResult.errorSeconds"/>, nhân đôi nó ra enum là mở đường cho hai
    /// nguồn sự thật lệch nhau.
    /// </summary>
    public enum TimingGrade { Perfect, Good, Poor }

    /// <summary>
    /// Kích thước cửa sổ thời điểm. Đây là "cửa sổ bấm chuẩn" mà
    /// <see cref="ShotMappingConfig.maxTimingErrorSeconds"/> cố ý KHÔNG giữ: bên đó là mức
    /// TỆ NHẤT (sai tới đâu thì hết cứu), bên này là mức TỐT (sai tới đâu thì vẫn coi là chuẩn).
    /// Tách ra vì hai con số được chỉnh vì hai lý do khác nhau: cửa sổ chuẩn chỉnh theo độ khó
    /// mong muốn, còn mức tệ nhất chỉnh theo mức tản mát tối đa chấp nhận được.
    /// </summary>
    [Serializable]
    public struct TimingWindowConfig {
        /// <summary>
        /// Giây. Sai trong khoảng ±mức này coi như bấm hoàn hảo — sai số ĐƯA VÀO GAMEPLAY bị
        /// đưa về đúng 0 (xem <see cref="TimingResult.mappedErrorSeconds"/>).
        ///
        /// Vì sao phải có vùng chết: không có nó thì trạng thái "quality = 1" là bất khả thi
        /// (không ai bấm trúng đúng 0.000 s), nên nhánh scatter = 0 trong
        /// <see cref="ShotMappingConfig.qualityToScatter"/> vĩnh viễn không chạy tới, và cú sút
        /// hoàn hảo mà người chơi cảm thấy mình vừa thực hiện vẫn lệch vài chục centimet.
        /// 50 ms lấy theo ngưỡng cảm nhận thông thường của thao tác chạm: dưới mức đó người
        /// chơi không quy được kết quả về tay mình nữa, nên phạt họ là phạt oan.
        /// </summary>
        public float perfectHalfWidthSeconds;

        /// <summary>
        /// Giây. Ngoài vùng hoàn hảo nhưng trong ±mức này thì còn là <see cref="TimingGrade.Good"/>.
        /// Sai số gameplay tính từ MÉP vùng hoàn hảo trở ra, không tính từ 0 — nếu tính từ 0 thì
        /// ngay sát mép vùng hoàn hảo sẽ có một bậc nhảy 50 ms, và người chơi cảm nhận được
        /// bậc nhảy đó dưới dạng "vừa nãy y hệt mà lần này lệch hẳn".
        /// </summary>
        public float goodHalfWidthSeconds;

        /// <summary>
        /// Giây. Trần cứng của sai số gameplay. Nên đặt bằng
        /// <see cref="ShotMappingConfig.maxTimingErrorSeconds"/> — bấm tệ hơn mức này cũng
        /// không tệ thêm được nữa, vì đằng nào quality cũng đã chạm 0.
        /// </summary>
        public float maxErrorSeconds;

        /// <summary>
        /// ±50 / ±120 / trần 200 ms. Trần khớp mặc định của
        /// <see cref="ShotMappingConfig.maxTimingErrorSeconds"/>.
        /// </summary>
        public static TimingWindowConfig Default => new TimingWindowConfig {
            perfectHalfWidthSeconds = 0.05f,
            goodHalfWidthSeconds    = 0.12f,
            maxErrorSeconds         = 0.2f,
        };
    }

    /// <summary>
    /// Kết quả chấm một lần bấm. Hai sai số CỐ Ý tách đôi:
    /// <see cref="errorSeconds"/> là sự thật để hiện lên màn hình debug, còn
    /// <see cref="mappedErrorSeconds"/> là con số đã tha thứ để đưa vào gameplay.
    /// Gộp làm một thì hoặc HUD nói dối, hoặc vùng chết biến mất.
    /// </summary>
    public struct TimingResult {
        /// <summary>Giây, CÓ DẤU. Âm = bấm sớm, dương = bấm muộn. Chưa tha thứ, chưa kẹp.</summary>
        public float errorSeconds;

        /// <summary>
        /// Giây, CÓ DẤU. Đã trừ vùng chết và đã kẹp về <c>maxErrorSeconds</c>.
        /// Đây là thứ truyền vào <see cref="ShotMapper.Map"/>, không phải cái trên.
        /// </summary>
        public float mappedErrorSeconds;

        public TimingGrade grade;

        /// <summary>Mili-giây có dấu để hiện lên HUD debug. Chỉ đọc, suy từ <see cref="errorSeconds"/>.</summary>
        public float ErrorMilliseconds => errorSeconds * 1000f;

        /// <summary>Bấm sớm hay muộn. Đúng 0 tính là không sớm.</summary>
        public bool IsEarly => errorSeconds < 0f;
    }

    /// <summary>
    /// Cửa sổ thời điểm của cú sút. HÀM THUẦN: không đọc <c>Time</c>, không giữ trạng thái
    /// tĩnh, không cấp phát. Thời điểm bấm và thời điểm chuẩn đều do người gọi truyền vào.
    ///
    /// Vì sao không tự đọc đồng hồ: cùng lý do với <c>BallSolver</c> — replay (T27) phải dựng
    /// lại được đúng pha bóng cũ từ dữ liệu đã ghi, và test phải chấm được cả nghìn thời điểm
    /// mà không cần chạy game. Một lần gọi <c>Time.time</c> ở đây là mất cả hai thứ đó.
    ///
    /// CHIA VIỆC VỚI <see cref="ShotMapper"/>: file này biến (thời điểm bấm, thời điểm chuẩn)
    /// thành SAI SỐ. ShotMapper biến sai số thành <c>quality</c> và tản mát. Không file nào
    /// làm phần của file kia — nếu không sẽ có hai định nghĩa "quality" trôi dạt khỏi nhau.
    /// </summary>
    public static class TimingWindow {
        /// <summary>
        /// Chấm một lần bấm.
        /// </summary>
        /// <param name="releaseTime">Giây, thời điểm ngón tay nhấc lên (hoặc thời điểm chốt cú sút).</param>
        /// <param name="idealTime">Giây, thời điểm chuẩn. Cùng gốc đồng hồ với <paramref name="releaseTime"/>.</param>
        /// <param name="cfg">Kích thước cửa sổ. Giá trị lộn xộn được <see cref="Sanitize"/> sắp lại
        /// tại chỗ thay vì ném lỗi: config này sẽ được chỉnh trên máy thật giữa lúc chơi.</param>
        public static TimingResult Evaluate(float releaseTime, float idealTime, in TimingWindowConfig cfg) {
            TimingWindowConfig c = Sanitize(cfg);

            float error = releaseTime - idealTime;

            // NaN vào thì NaN ra là cái bẫy tệ nhất: nó chui qua ShotMapper, thành aimPoint NaN,
            // thành quỹ đạo NaN, và người ta đi tìm bug ở solver. Chặn ngay tại cửa.
            if (!math.isfinite(error)) error = 0f;

            float abs = math.abs(error);

            // Sai số gameplay đo từ MÉP vùng hoàn hảo trở ra. Trong vùng hoàn hảo thì bằng 0
            // chính xác — không phải "gần 0", vì nhánh scatter = 0 phải thật sự chạy tới.
            float beyond = math.max(0f, abs - c.perfectHalfWidthSeconds);
            float mappedAbs = math.min(beyond, c.maxErrorSeconds);

            // math.sign(0) = 0 nên cú bấm đúng khoảnh khắc cho mappedError đúng bằng 0,
            // không phải -0f (âm không: so sánh thì bằng 0 nhưng in ra HUD lại ra "-0 ms").
            float mapped = mappedAbs > 0f ? math.sign(error) * mappedAbs : 0f;

            TimingGrade grade = abs <= c.perfectHalfWidthSeconds ? TimingGrade.Perfect
                              : abs <= c.goodHalfWidthSeconds    ? TimingGrade.Good
                                                                 : TimingGrade.Poor;

            return new TimingResult {
                errorSeconds       = error,
                mappedErrorSeconds = mapped,
                grade              = grade,
            };
        }

        /// <summary>
        /// Sắp lại config cho hợp lệ: mọi mức không âm, và ba mức phải tăng dần
        /// (hoàn hảo ≤ tốt ≤ trần). Sai thứ tự thì việc phân hạng sẽ phụ thuộc thứ tự viết
        /// if trong <see cref="Evaluate"/> chứ không phụ thuộc con số — tức là im lặng sai.
        /// Trả bản sao, không sửa tham số: config gốc có thể là asset dùng chung.
        /// </summary>
        public static TimingWindowConfig Sanitize(in TimingWindowConfig cfg) {
            float perfect = math.max(0f, Finite(cfg.perfectHalfWidthSeconds));
            float good    = math.max(perfect, Finite(cfg.goodHalfWidthSeconds));
            float max     = math.max(0f, Finite(cfg.maxErrorSeconds));
            return new TimingWindowConfig {
                perfectHalfWidthSeconds = perfect,
                goodHalfWidthSeconds    = good,
                maxErrorSeconds         = max,
            };
        }

        static float Finite(float v) => math.isfinite(v) ? v : 0f;

        // ---------------------------------------------------------------------------------
        // Hiển thị debug
        // ---------------------------------------------------------------------------------

        /// <summary>
        /// Viết "+42 ms Good" vào bộ đệm sẵn có. KHÔNG cấp phát một byte nào — dùng được ở
        /// chế độ debug mỗi khung hình mà không đẻ rác cho GC, đúng quy ước của
        /// <c>PerfHud</c> (đường đo không cấp phát, chỉ chỗ làm mới chữ mới được phép).
        ///
        /// Tự ghép chữ số thay vì gọi <c>sb.Append(int)</c>/<c>ToString()</c>: cách kia phụ
        /// thuộc cài đặt runtime (một số bản Mono/IL2CPP vẫn dựng chuỗi trung gian) và phụ
        /// thuộc locale. Ở đây số nhỏ, tự làm vừa chắc vừa ngắn.
        /// </summary>
        public static void AppendDebug(StringBuilder sb, in TimingResult r) {
            if (sb == null) return;

            // Làm tròn tới ms: HUD hiện phần lẻ của mili-giây là hiện nhiễu, không phải thông tin.
            float ms = r.ErrorMilliseconds;
            int rounded = math.isfinite(ms) ? (int)math.round(ms) : 0;

            // Luôn có dấu, kể cả khi bằng 0: "+0 ms" đọc ra ngay là "đúng khoảnh khắc", còn
            // "0 ms" thì phải nghĩ một nhịp xem có phải HUD chưa cập nhật không.
            sb.Append(rounded < 0 ? '-' : '+');
            AppendDigits(sb, rounded < 0 ? -rounded : rounded);
            sb.Append(" ms ");

            switch (r.grade) {
                case TimingGrade.Perfect: sb.Append("Perfect"); break;
                case TimingGrade.Good:    sb.Append("Good");    break;
                default:                  sb.Append("Poor");    break;
            }
        }

        static void AppendDigits(StringBuilder sb, int value) {
            if (value >= 1000000) value = 999999; // sai số một nghìn giây thì con số cụ thể hết nghĩa
            if (value == 0) { sb.Append('0'); return; }

            // Ghi từ chữ số cao xuống thấp bằng một ước số chạy. Cách hiển nhiên hơn là bốc
            // chữ số thấp rồi Insert ngược lại đầu — nhưng Insert dịch cả bộ đệm và có thể
            // xin thêm chunk mới, tức là cấp phát, đúng thứ hàm này hứa sẽ không làm.
            int div = 1;
            while (value / div >= 10) div *= 10;
            while (div > 0) {
                sb.Append((char)('0' + (value / div) % 10));
                div /= 10;
            }
        }

        /// <summary>
        /// Bản trả chuỗi cho test và cho Inspector. CÓ cấp phát — đừng gọi mỗi khung hình,
        /// dùng <see cref="AppendDebug"/> cho đường debug chạy liên tục.
        /// </summary>
        public static string Describe(in TimingResult r) {
            var sb = new StringBuilder(16);
            AppendDebug(sb, r);
            return sb.ToString();
        }
    }
}
