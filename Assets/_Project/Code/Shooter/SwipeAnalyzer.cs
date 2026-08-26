using Unity.Collections;
using Unity.Mathematics;

namespace Eleven.Shooter {
    public struct SwipeSample { public float2 position; public float time; }

    public struct SwipeFeatures {
        public float2 start, end;
        public float  length, duration, peakSpeed, endSpeed;
        public float  curvature;
        public float  straightness;
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
            var smooth = new NativeArray<float2>(n, Allocator.Temp);
            for (int i = 0; i < n; i++) {
                if (i == 0 || i == n - 1) smooth[i] = samples[i].position;
                else smooth[i] = (samples[i - 1].position + samples[i].position + samples[i + 1].position) / 3f;
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
            if (chordLen > 1e-8f) {
                float2 d = chord / chordLen;
                float ePrev = Cross(smooth[0] - f.start, d);
                for (int i = 1; i < n; i++) {
                    float eCur = Cross(smooth[i] - f.start, d);
                    float ds = math.length(smooth[i] - smooth[i - 1]);
                    signedArea += 0.5f * (ePrev + eCur) * ds;
                    ePrev = eCur;
                }
            }
            f.curvature = signedArea / len; // dương = cong sang phải

            smooth.Dispose();
            return f;
        }

        private static float Cross(float2 a, float2 b) => a.x * b.y - a.y * b.x;
    }
}
