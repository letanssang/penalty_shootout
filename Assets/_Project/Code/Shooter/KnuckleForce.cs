using System;
using Unity.Burst;
using Unity.Mathematics;
using Eleven.Ball;

namespace Eleven.Shooter
{
    /// <summary>
    /// Cấu hình hiệu ứng bất ổn định knuckle. Mọi số đáng chỉnh nằm ở đây,
    /// không có hằng số ma thuật rải rác trong <see cref="KnuckleForce"/>.
    /// </summary>
    [Serializable]
    public struct KnuckleConfig
    {
        /// <summary>
        /// Ràng buộc cứng: độ lệch ngang tích luỹ không bao giờ vượt giá trị này (mét).
        /// Đây là BẢO ĐẢM TOÁN HỌC chứ không phải clamp — xem chứng minh trong
        /// <see cref="KnuckleForce.Evaluate"/>.
        /// </summary>
        public float maxLateralDeviation;

        /// <summary>
        /// Tần số cơ bản của dao động knuckle (Hz).
        ///
        /// PHẢI đọc cùng thời gian bay thật: pha bay chỉ ~0.42 s (T12 đo trên video: 0.38 s).
        /// Đặt tần số cao là hỏng theo hai đường cùng lúc — bóng rung thấy rõ thay vì "đi một
        /// đường rồi bẻ", và gia tốc ngang bốc lên theo bình phương tần số (ở 6 Hz với biên độ
        /// 25 cm là 750 m/s², tức 77 g: bóng nhảy cóc chứ không bay). Mặc định 1.1 Hz cho đúng
        /// chưa tới nửa chu kỳ trong pha bay, tức MỘT cú bẻ — thứ người chơi đọc được.
        /// </summary>
        public float frequencyHz;

        /// <summary>
        /// Giây. Thời gian bao hình mở tới ~90%: hiệu ứng lên dần từ 0 chứ không bật cái rụp.
        ///
        /// Tách khỏi <see cref="frequencyHz"/> chứ không suy ra từ nó, vì hai thứ này điều
        /// khiển hai chuyện khác nhau: tần số là bóng bẻ NHANH cỡ nào, bao hình là hiệu ứng
        /// KHỞI ĐỘNG trong bao lâu. Cột chúng vào nhau thì hạ tần số cho đỡ rung sẽ kéo theo
        /// bao hình chậm tới mức cả pha bay không kịp mở, và hiệu ứng biến mất trong im lặng.
        /// Đặt quá ngắn cũng hỏng: bao hình mở càng gấp thì đạo hàm bậc hai của nó càng lớn,
        /// và cú giật đó đi thẳng vào gia tốc. 0.25 s cùng cỡ với nửa chu kỳ dao động.
        /// </summary>
        public float envelopeRiseSeconds;

        /// <summary>
        /// Biên độ dao động (mét). Bị kẹp vào <see cref="maxLateralDeviation"/> trong
        /// <see cref="KnuckleForce.Evaluate"/> để đảm bảo ràng buộc cứng luôn đúng.
        /// </summary>
        public float amplitude;

        /// <summary>
        /// Ngưỡng tốc độ kích hoạt (m/s). Dưới ngưỡng này hiệu ứng knuckle tắt hoàn toàn:
        /// bóng chậm thì dòng khí tách đối xứng, không có hiện tượng bất ổn.
        /// </summary>
        public float onsetSpeed;

        /// <summary>
        /// Giá trị mặc định dùng chung cho test và gameplay. Biên độ mặc định đã nằm trong
        /// <see cref="maxLateralDeviation"/> nên ràng buộc cứng tự thoả.
        ///
        /// Bộ số này chỉnh theo pha bay THẬT ~0.42 s chứ không lấy từ sách: 1.1 Hz cho chưa
        /// tới nửa chu kỳ trong pha bay (một cú bẻ), bao hình 0.25 s mở gần hết trước khi bóng
        /// tới khung thành, biên độ 22 cm cho độ lệch điển hình 10–20 cm — đủ để thủ môn bắt
        /// hụt, chưa tới mức người chơi thấy bóng "nhảy". Gia tốc ngang đỉnh trong trường hợp
        /// pha xấu nhất khoảng 22 m/s² (2.3 g), cùng cỡ với lực cản không khí ở tốc độ này.
        ///
        /// <c>onsetSpeed</c> 18 m/s nằm dưới hẳn tốc độ rời chân nhỏ nhất mà
        /// <see cref="ShotMapper"/> có thể xếp loại Knuckle (~26.4 m/s ở config mặc định),
        /// nên cửa không thể đóng giữa pha bay — xem ghi chú "cửa tốc độ" ở
        /// <see cref="KnuckleForce"/> để biết vì sao điều đó quan trọng.
        /// </summary>
        public static KnuckleConfig Default => new KnuckleConfig
        {
            maxLateralDeviation = 0.35f,
            frequencyHz         = 1.1f,
            envelopeRiseSeconds = 0.25f,
            amplitude           = 0.22f,
            onsetSpeed          = 18f,
        };
    }

    /// <summary>
    /// Bất ổn định của cú sút không xoáy (hiệu ứng knuckle). Trả GIA TỐC (m/s²) theo
    /// trục thế giới — không phải Newton — vì <see cref="BallState"/> không mang khối lượng
    /// và <c>BallSolver.Acceleration</c> cộng dồn gia tốc. Khâu thực thi chỉ việc cộng
    /// thẳng kết quả này vào gia tốc mỗi bước.
    ///
    /// HÀM THUẦN: không đọc Time, không biến static, không System.Random,
    /// không UnityEngine.Random. Cùng <c>(s, c, elapsed, seed)</c> luôn cho cùng
    /// <c>float3</c> từng bit.
    ///
    /// CHỨNG MINH RÀNG BUỘC maxLateralDeviation:
    ///
    ///   Định nghĩa hàm độ lệch ngang:
    ///     d(t) = A · E(t) · O(t)
    ///   trong đó:
    ///     A = min(amplitude, maxLateralDeviation)
    ///     E(t) = (1 − exp(−β·t²))²,  β > 0        (bao hình lên dần, bậc hai để d''(0)=0)
    ///     O(t) = w₁·sin(ω₁t+φ₁) + w₂·sin(ω₂t+φ₂),  w₁,w₂ ≥ 0, w₁+w₂ = 1
    ///
    ///   Ta có:
    ///     |E(t)| = u(t)² ≤ 1   ∀t  (vì 0 ≤ u(t) = 1−exp(−β·t²) ≤ 1)
    ///     |O(t)| ≤ w₁ + w₂ = 1   (bất đẳng thức tam giác, |sin| ≤ 1)
    ///   ⇒  |d(t)| ≤ A · 1 · 1 = A ≤ maxLateralDeviation.
    ///
    ///   Mặt khác: đặt u = 1−exp(−β·t²), ta có u(0) = 0, u'(0) = 0.
    ///   E = u² nên E(0) = 0, E'(0) = 2u(0)·u'(0) = 0, E''(0) = 2u'(0)² + 2u(0)·u''(0) = 0.
    ///   Suy ra d(0) = 0, d'(0) = 0, d''(0) = 0.
    ///   Evaluate trả d''(t): khi tích phân hai lần từ trạng thái nghỉ cho lại đúng d(t),
    ///   nên ràng buộc |d(t)| ≤ maxLateralDeviation là hệ quả của phép toán, không phải
    ///   của một câu clamp hậu kỳ.
    ///
    ///   ĐIỀU KIỆN CỦA CHỨNG MINH — đọc kỹ trước khi chỉnh config:
    ///   ràng buộc trên chỉ đúng chừng nào hiệu ứng còn CHẠY LIÊN TỤC từ t = 0. Cửa tốc độ
    ///   (<c>onsetSpeed</c>) cắt phăng gia tốc về 0, nên nếu nó đóng giữa pha bay tại một
    ///   thời điểm mà d'(t) ≠ 0, phần vận tốc lệch còn lại KHÔNG bị triệt tiêu: bóng tiếp tục
    ///   trôi ngang đều và độ lệch cuối cùng có thể vượt maxLateralDeviation.
    ///   Đây không phải lỗ hổng bỏ ngỏ mà là ràng buộc lên việc chỉnh số: <c>onsetSpeed</c>
    ///   phải nằm dưới tốc độ CUỐI pha bay của cú sút chậm nhất còn được xếp loại Knuckle.
    ///   Ở config mặc định của cả hai bên, cú Knuckle chậm nhất rời chân 26.4 m/s và còn
    ///   23.4 m/s khi tới khung thành (0.42 s), 22.5 m/s ở mốc 0.6 s mà test dùng cho dư —
    ///   so với onsetSpeed 18 m/s thì cửa không thể đóng giữa chừng. Có test khoá điều kiện này lại
    ///   (<c>Knuckle_CuaTocDo_KhongTheDongGiuaPhaBay_VoiCauHinhMacDinh</c>) để lần sau ai hạ
    ///   maxSpeed hoặc nâng onsetSpeed thì test đỏ ngay, chứ không phải bóng bay lệch trong im lặng.
    ///
    /// [BurstCompile] chỉ đặt ở cấp class, TUYỆT ĐỐI KHÔNG đặt lên method — lý do đã ghi
    /// trong BallSolver.cs: gắn lên method bật Direct Call, ABI cấm trả struct/vector theo
    /// giá trị (BC1064/BC1067) → hỏng AOT lúc build player.
    /// </summary>
    [BurstCompile]
    public static class KnuckleForce
    {
        /// <summary>
        /// Tính gia tốc knuckle tại thời điểm <paramref name="elapsed"/> giây kể từ khi
        /// bóng rời chân. Hướng lệch vuông góc với vận tốc, cường độ suy từ đạo hàm bậc
        /// hai của hàm độ lệch đã chứng minh bị chặn.
        /// </summary>
        /// <param name="s">Trạng thái bóng hiện tại.</param>
        /// <param name="c">Cấu hình knuckle.</param>
        /// <param name="elapsed">Thời gian kể từ khi bóng rời chân (giây).</param>
        /// <param name="seed">Hạt ngẫu nhiên — cùng seed cho cùng kết quả từng bit.</param>
        /// <returns>Gia tốc (m/s²) theo trục thế giới.</returns>
        public static float3 Evaluate(in BallState s, in KnuckleConfig c, float elapsed, uint seed)
        {
            // ---- Tắt sớm: bóng có xoáy thì không có hiệu ứng knuckle ----
            // Knuckle xảy ra vì bóng KHÔNG xoáy nên dòng khí tách ra bất đối xứng và điểm
            // tách nhảy quanh. Bóng có xoáy thì dòng khí tách ổn định (Magnus), hai hiệu ứng
            // loại trừ nhau.
            if (math.lengthsq(s.spin) > 0f)
                return float3.zero;

            // ---- Tắt sớm: dưới ngưỡng tốc độ ----
            // Bóng chậm thì dòng khí tách đối xứng, không có bất ổn.
            float speed = math.length(s.velocity);
            if (speed < c.onsetSpeed)
                return float3.zero;

            // ---- Tắt sớm: thời điểm không hợp lệ ----
            // Một câu chặn ba trường hợp: elapsed = 0 (lực vốn đã bằng 0 theo bao hình),
            // elapsed âm (bao hình chứa t² nên sẽ soi gương và cho lực ở thời điểm chưa tồn
            // tại), và elapsed = NaN (mọi so sánh đều false nên nó lọt qua mọi if khác rồi
            // biến cả gia tốc thành NaN — kiểu lỗi đi rất xa mới lộ ra).
            if (!(elapsed > 0f))
                return float3.zero;

            // ---- Phòng thủ: config bất thường ----
            // frequencyHz hoặc maxLateralDeviation bằng 0 thì không có gì dao động.
            if (!(c.frequencyHz > 0f) || !(c.maxLateralDeviation > 0f))
                return float3.zero;

            // ---- Dựng trục lệch ngang: vuông góc với vận tốc ----
            // right = normalize(cross(up, v)). Nếu v gần song song trục Y thì cross gần 0,
            // suy biến → chuyển sang dùng trục Z làm "up" thay thế để tránh NaN.
            float3 up = new float3(0f, 1f, 0f);
            float3 raw = math.cross(up, s.velocity);
            float rawLenSq = math.lengthsq(raw);

            // Ngưỡng suy biến: |cross|² < ε² thì v gần song song up.
            const float degenerateEpsSq = 1e-8f;
            if (rawLenSq < degenerateEpsSq)
            {
                // Dùng trục Z làm "up" phụ. cross((0,0,1), v) không thể đồng thời bằng 0
                // khi v gần song song Y.
                raw = math.cross(new float3(0f, 0f, 1f), s.velocity);
                rawLenSq = math.lengthsq(raw);

                // Nếu vẫn bằng 0 thì velocity = 0 — đã bị chặn bởi onsetSpeed ở trên,
                // nhưng phòng thủ thêm cho chắc.
                if (rawLenSq < degenerateEpsSq)
                    return float3.zero;
            }

            float3 right = raw * math.rsqrt(rawLenSq);

            // ---- Tham số dao động ----
            // Kẹp amplitude vào maxLateralDeviation: đảm bảo A ≤ maxLateralDeviation.
            float A = math.min(c.amplitude, c.maxLateralDeviation);
            if (!(A > 0f))
                return float3.zero;

            // Random.CreateFromIndex băm seed nên seed = 0 vẫn dùng được, và các seed
            // liên tiếp không cho chuỗi giống nhau — xem ghi chú trong ShotMapper.cs.
            var rng = Unity.Mathematics.Random.CreateFromIndex(seed);

            // Hai pha ngẫu nhiên và tỉ lệ trọng số — suy ra TẤT ĐỊNH từ seed.
            float phi1 = rng.NextFloat(0f, 2f * math.PI);
            float phi2 = rng.NextFloat(0f, 2f * math.PI);
            // w1 trong [0.3, 0.7] để cả hai thành phần đều đáng kể.
            float w1 = rng.NextFloat(0.3f, 0.7f);
            float w2 = 1f - w1;

            // Hai tần số: f₁ = frequencyHz, f₂ = frequencyHz * tỉ lệ vô tỉ (~golden ratio)
            // để tránh chu kỳ lặp lại quá đều.
            float omega1 = 2f * math.PI * c.frequencyHz;
            float omega2 = omega1 * 1.6180339887f;

            // β suy từ thời gian mở bao hình: E(rise) = 0.9 ⇔ u = √0.9 ⇔ exp(−β·rise²) = 1−√0.9
            // ⇔ β = −ln(1−√0.9)/rise² = 2.9757/rise². Viết hằng đã tính sẵn thay vì gọi log()
            // mỗi lần: nó là hệ quả của định nghĩa "mở tới 90%", không phải số chỉnh tay.
            const float riseTo90 = 2.9757f;
            float rise = c.envelopeRiseSeconds;
            // rise ≤ 0 nghĩa là "bật ngay lập tức", mà bật ngay thì d'(0) ≠ 0 và ràng buộc
            // độ lệch không còn chứng minh được. Coi như config hỏng và tắt hẳn, hơn là chạy
            // một hiệu ứng không còn bảo đảm gì.
            if (!(rise > 0f))
                return float3.zero;
            float beta = riseTo90 / (rise * rise);

            float t = elapsed;
            float t2 = t * t;
            float expTerm = math.exp(-beta * t2);

            // ---- Envelope E(t) = u(t)² với u(t) = 1 − exp(−β·t²) ----
            // Dùng u² thay vì u: E(0) = E'(0) = E''(0) = 0, nên d''(0) = 0 (lực bằng 0 tại t=0).
            // Đạo hàm u: u' = 2β·t·exp(−β·t²), u'' = 2β·exp(−β·t²)·(1 − 2β·t²)
            // Tên có hậu tố D1/D2 chứ không phải u'/u'': trục "lên trên" ở trên đã tên là up,
            // và một biến float tên up nằm cạnh một float3 tên up là cách chắc chắn nhất để
            // người đọc sau đọc nhầm công thức.
            float u   = 1f - expTerm;
            float uD1 = 2f * beta * t * expTerm;
            float uD2 = 2f * beta * expTerm * (1f - 2f * beta * t2);

            // E = u², E' = 2u·u', E'' = 2(u')² + 2u·u''
            float E   = u * u;
            float Ep  = 2f * u * uD1;
            float Epp = 2f * (uD1 * uD1 + u * uD2);

            // ---- Oscillation O(t) = w₁·sin(ω₁t+φ₁) + w₂·sin(ω₂t+φ₂) ----
            float arg1 = omega1 * t + phi1;
            float arg2 = omega2 * t + phi2;

            float sin1 = math.sin(arg1);
            float sin2 = math.sin(arg2);
            float cos1 = math.cos(arg1);
            float cos2 = math.cos(arg2);

            float O   = w1 * sin1 + w2 * sin2;
            float Op  = w1 * omega1 * cos1 + w2 * omega2 * cos2;
            float Opp = -(w1 * omega1 * omega1 * sin1 + w2 * omega2 * omega2 * sin2);

            // ---- d''(t) = A · [E''·O + 2·E'·O' + E·O''] ----
            float dpp = A * (Epp * O + 2f * Ep * Op + E * Opp);

            return dpp * right;
        }
    }
}
