using Unity.Collections;
using Unity.Mathematics;

namespace Eleven.Shooter {
    public struct SwipeSample { public float2 position; public float time; }

    public struct SwipeFeatures {
        public float2 start, end;
        public float  length, duration, peakSpeed, endSpeed;
        public float  curvature;
        public float  straightness;
        /// <summary>
        /// Độ thẳng đo trên đường ĐÃ LÀM MƯỢT. Khác straightness (đo trên mẫu thô) ở chỗ nó
        /// phân biệt được "vuốt thẳng nhưng tay run" với "cố tình vuốt ngoằn ngoèo" — hai thứ
        /// mà straightness thô trộn lẫn hoàn toàn. Đo thực tế trên cú vuốt 7.5 cm, 25 mẫu:
        ///   tay run 13 px:  thô 0.839  |  mượt 0.979
        ///   chữ S 1.0 cm :  thô 0.865  |  mượt 0.870
        /// Tức là trên số liệu thô, cú vuốt thẳng của người tay run trông CÒN KÉM THẲNG HƠN cú
        /// ngoằn ngoèo cố ý. Dùng để phân loại HÌNH DÁNG cử chỉ thì phải dùng bản làm mượt.
        /// </summary>
        public float  straightnessSmooth;
        public float  verticalRatio;
    }

    public static class SwipeAnalyzer {
        // Hàm thuần: không MonoBehaviour, không đọc Time, chỉ dựa vào samples[i].time thật
        // (độc lập tốc độ khung hình / mật độ lấy mẫu).
        public static SwipeFeatures Analyze(NativeSlice<SwipeSample> samples) {
            var f = new SwipeFeatures();

            int n = samples.Length;

            // ---- Bảo vệ: dưới 3 mẫu -> không crash, trả giá trị mặc định ----
            if (n == 0) {
                f.start = float2.zero;
                f.end   = float2.zero;
                return f; // mọi float khác = 0
            }
            if (n < 3) {
                f.start = samples[0].position;
                f.end   = samples[n - 1].position;
                return f; // không đủ điểm để định nghĩa tốc độ/độ cong đáng tin cậy
            }

            // ---- start / end ----
            f.start = samples[0].position;
            f.end   = samples[n - 1].position;

            // ---- duration (dùng time thật) ----
            f.duration = math.max(0f, samples[n - 1].time - samples[0].time);

            // ---- length, peakSpeed, endSpeed ----
            float len = 0f;
            float peak = 0f;
            float endSpeed = 0f;
            bool endSpeedFound = false;
            for (int i = 0; i < n - 1; i++) {
                float2 a = samples[i].position;
                float2 b = samples[i + 1].position;
                float ds = math.length(b - a);
                float dt = samples[i + 1].time - samples[i].time;
                len += ds;
                if (dt > 0f) {
                    float v = ds / dt;
                    if (v > peak) peak = v;
                    endSpeed = v;          // ghi đè: đoạn cuối cùng hợp lệ sẽ thắng
                    endSpeedFound = true;
                }
            }
            f.length     = len;
            f.peakSpeed  = peak;
            f.endSpeed   = endSpeedFound ? endSpeed : 0f;

            if (len <= 1e-8f) {
                // đường đi suy biến (tất cả điểm trùng nhau)
                f.straightness  = 1f;      // thẳng tuyệt đối theo định nghĩa ratio
                f.curvature     = 0f;
                f.verticalRatio = 0f;
                return f;
            }

            f.straightness  = math.saturate(math.length(f.end - f.start) / len);
            f.verticalRatio = math.abs(f.end.y - f.start.y) / len;

            // ---- Làm mượt trung bình trượt 3 điểm (chỉ dùng cho curvature) ----
            // HAI ĐẦU MÚT KHÔNG ĐƯỢC BỎ QUA. Trước đây chúng được giữ nguyên giá trị thô, và
            // đó là một lỗi thật: curvature đo độ lệch so với dây cung start->end, nên nhiễu
            // vuông góc của MẪU ĐẦU TIÊN chui thẳng vào kết quả theo tỉ lệ 1:1. Đo thực nghiệm
            // với nhiễu răng cưa (digitizer nhảy qua lại giữa hai vị trí — có thật trên vài
            // dòng máy) biên độ 26 px: độ cong bị báo thiếu 27.9%. Trên đường vuốt THẲNG,
            // cùng mức nhiễu sinh ra độ cong GIẢ 0.19 cm — vượt xa ngưỡng
            // ShotMappingConfig.knuckleMaxCurvatureCm, tức người chơi vuốt thẳng bằng tay run
            // sẽ mất luôn cú knuckle vì máy tưởng họ vuốt cong.
            //
            // Cách chữa: đầu mút lấy từ HỒI QUY TUYẾN TÍNH 3 ĐIỂM đầu (hoặc 3 điểm cuối) rồi
            // NGOẠI SUY ngược về đúng vị trí đầu mút: v = (5*p0 + 2*p1 - p2)/6.
            // Cố tình KHÔNG dùng trung bình 2 điểm (p0+p1)/2 dù nó khử nhiễu răng cưa tốt hơn:
            // cách đó dịch đầu mút vào trong nửa đoạn, làm độ cong đo được phụ thuộc mật độ lấy
            // mẫu (lệch 10.6% giữa 12 và 25 mẫu) — máy 60 Hz và máy 120 Hz sẽ cho cảm giác sút
            // khác nhau. Ngoại suy giữ nguyên chiều dài cung nên thang đo không đổi
            // (0.6192 so với 0.6196 trên đường sạch) và độ lệch thưa/dày chỉ 3.0%.
            // Đổi lại, nhiễu răng cưa còn 1/3 biên độ: sai số 27.9% -> 7.9%.
            //
            // Lưu ý: f.start/f.end vẫn là MẪU THÔ. Chúng dùng để ngắm, phải đúng chỗ ngón tay
            // thật sự nhấc lên; chỉ riêng phép tính độ cong mới dùng bản đã làm mượt.
            var smooth = new NativeArray<float2>(n, Allocator.Temp);
            for (int i = 0; i < n; i++) {
                if (i == 0) {
                    smooth[i] = (5f * samples[0].position + 2f * samples[1].position - samples[2].position) / 6f;
                } else if (i == n - 1) {
                    smooth[i] = (5f * samples[n - 1].position + 2f * samples[n - 2].position - samples[n - 3].position) / 6f;
                } else {
                    smooth[i] = (samples[i - 1].position + samples[i].position + samples[i + 1].position) / 3f;
                }
            }

            // ---- curvature: diện tích có dấu giữa đường đi và dây start->end ----
            // Trục tham chiếu: hướng đơn vị d = (end - start)/|end - start|.
            // Độ lệch có dấu của điểm p: e = cross(p - start, d) = rx*dy - ry*dx
            //   (dương khi p nằm bên PHẢI hướng start->end trong hệ y-lên).
            // Diện tích xấp xỉ bằng hình thang dọc theo cung:
            //   S = Σ 0.5 * (e[i] + e[i+1]) * ds(i, i+1)
            // curvature = S / length  (chuẩn hoá theo độ dài cung).
            float2 chord = smooth[n - 1] - smooth[0];
            float chordLen = math.length(chord);
            float signedArea = 0f;
            float smoothLen = 0f;   // chiều dài cung của ĐƯỜNG ĐÃ LÀM MƯỢT
            // smoothLen tính TÁCH RIÊNG, ngoài nhánh bảo vệ chordLen. Nếu gộp vào trong thì cú
            // vuốt đi rồi vòng về đúng chỗ cũ (chordLen ~ 0 nhưng cung rất dài) sẽ để smoothLen
            // bằng 0, và straightnessSmooth trả về 1 — tức là "thẳng tuyệt đối" cho một cú vuốt
            // vòng tròn. ShotMapper sẽ đọc nhầm nó thành cú knuckle.
            for (int i = 1; i < n; i++) {
                smoothLen += math.length(smooth[i] - smooth[i - 1]);
            }

            if (chordLen > 1e-8f) {
                float2 d = chord / chordLen;
                float ePrev = 0f;   // theo định nghĩa: smooth[0] là gốc toạ độ của e
                for (int i = 1; i < n; i++) {
                    float eCur = Cross(smooth[i] - smooth[0], d);
                    signedArea += 0.5f * (ePrev + eCur) * math.length(smooth[i] - smooth[i - 1]);
                    ePrev = eCur;
                }
            }

            // Chia cho smoothLen chứ KHÔNG PHẢI len. Hai số này lệch nhau không đáng kể trên
            // cú vuốt sạch, nhưng khi ngón tay run thì len (đo trên mẫu THÔ) phình lên rất
            // nhanh vì nó cộng cả những đoạn zíc-zắc, trong khi signedArea lại tích phân trên
            // đường ĐÃ LÀM MƯỢT. Trộn hai thước đo khác nhau như vậy làm độ cong bị BÁO
            // THIẾU tỉ lệ thuận với độ nhiễu — tức là cú vuốt cong của người có tay run bị
            // ăn mất xoáy, đúng cái mà việc làm mượt sinh ra để tránh.
            // Chia cho smoothLen thì curvature đúng nghĩa "độ lệch trung bình theo chiều dài
            // cung" của cùng một đường cong, và bất biến với nhiễu răng cưa.
            f.curvature = smoothLen > 1e-8f ? signedArea / smoothLen : 0f; // dương = cong sang phải

            // Cung dài bằng 0 nghĩa là mọi mẫu trùng nhau -> coi như thẳng, khớp với cách
            // straightness thô xử lý trường hợp suy biến.
            f.straightnessSmooth = smoothLen > 1e-8f ? math.saturate(chordLen / smoothLen) : 1f;

            smooth.Dispose();
            return f;
        }

        private static float Cross(float2 a, float2 b) => a.x * b.y - a.y * b.x;
    }
}
