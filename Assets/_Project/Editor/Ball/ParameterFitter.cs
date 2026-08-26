using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Unity.Mathematics;

namespace Eleven.Ball.Tools
{
    public struct TrackedPoint
    {
        public float3 position;
        public float time;
    }

    public static class ParameterFitter
    {
        /// <summary>
        /// Đọc điểm bám vết từ file CSV, cột theo thứ tự "time,x,y,z". Bọc quanh
        /// <see cref="ParseCsv"/> — tách riêng để test được phần phân tích cú pháp mà
        /// không cần ghi file thật.
        /// </summary>
        public static TrackedPoint[] LoadCsv(string path)
        {
            return ParseCsv(File.ReadAllText(path));
        }

        /// <summary>
        /// Phân tích nội dung CSV "time,x,y,z" thành mảng <see cref="TrackedPoint"/>.
        /// Dòng trống, dòng tiêu đề (không parse được số ở cột đầu), và dòng thiếu cột
        /// đều bị bỏ qua thay vì ném lỗi — dữ liệu quay tay từ video thường có vài dòng
        /// hỏng. Luôn dùng <see cref="CultureInfo.InvariantCulture"/> để không phụ thuộc
        /// dấu thập phân theo locale máy (vd máy VN dùng dấu phẩy).
        /// </summary>
        public static TrackedPoint[] ParseCsv(string csvContent)
        {
            if (string.IsNullOrEmpty(csvContent)) return Array.Empty<TrackedPoint>();

            var lines = csvContent.Split('\n');
            var points = new List<TrackedPoint>(lines.Length);

            foreach (var raw in lines)
            {
                var line = raw.Trim().TrimEnd('\r');
                if (line.Length == 0) continue;

                var cols = line.Split(',');
                if (cols.Length < 4) continue;

                if (!TryParseFloat(cols[0], out float time)) continue; // vd dòng tiêu đề "time,x,y,z"
                if (!TryParseFloat(cols[1], out float x)) continue;
                if (!TryParseFloat(cols[2], out float y)) continue;
                if (!TryParseFloat(cols[3], out float z)) continue;

                points.Add(new TrackedPoint { time = time, position = new float3(x, y, z) });
            }

            return points.ToArray();
        }

        static bool TryParseFloat(string s, out float value)
        {
            return float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private const float Dt = 1f / 240f;
        private const int MaxSteps = 4096;

        // Số chiều tối ưu: pos(3) + vel(3) + cdLow + cdHigh + cdVLow + cdVHigh + lift + spinDecay
        private const int Dim = 12;

        // Quy mô bước khởi tạo cho simplex (theo thứ tự chiều như trên)
        private static readonly float[] InitScale =
        {
            0.02f, 0.02f, 0.02f,          // position (m)
            0.5f, 0.5f, 0.5f,             // velocity (m/s)
            0.05f, 0.05f,                 // cd low/high
            2.0f, 2.0f,                   // cdV thresholds (m/s)
            0.05f,                        // lift coefficient
            0.05f                         // spin decay per second
        };

        /// <summary>
        /// Fit tham số khí động + trạng thái ban đầu để khớp quỹ đạo quan sát được,
        /// bằng Nelder-Mead simplex trên tổng bình phương khoảng cách vị trí.
        /// mass/radius/airDensity/gravity lấy từ initialGuess (hằng số vật lý đã biết).
        /// </summary>
        public static BallParams Fit(TrackedPoint[] observed, BallState initialGuess,
                                     out float rmsError, out BallState fittedInitial)
        {
            if (observed == null || observed.Length == 0)
            {
                // Không có dữ liệu -> không thể fit. Trả nguyên đầu vào, rmsError = NaN
                // (khác 0 để caller phân biệt "fit tốt" với "không có gì để fit").
                rmsError = float.NaN;
                fittedInitial = initialGuess;
                return BallParams.Default;
            }

            // BUG ĐÃ SỬA (do model sinh code để lại): bản Pack(BallState) 1 tham số luôn trả
            // 0 cho cdLow/cdHigh/cdVLow/cdVHigh/lift/spinDecay — khởi điểm simplex ở 0 cho các
            // tham số khí động khiến DragCoefficient rơi vào nhánh suy biến (span=0) ngay từ
            // đầu, hội tụ sai hoặc rất chậm. Phải seed từ BallParams.Default.
            var p0 = Pack(initialGuess, BallParams.Default);

            // THÊM (phát hiện khi chạy test thật): Nelder-Mead 12 chiều từ một điểm khởi
            // đầu duy nhất có thể hội tụ sớm (điều kiện dừng costs gần bằng nhau) vào một
            // điểm chỉ "đủ tốt" về RMS tổng nhưng lệch tham số riêng lẻ do các chiều tương
            // quan (vd cdHigh với cdVHigh ở vùng chuyển tiếp). Chạy nhiều lần khởi động lại
            // từ các điểm nhiễu quanh p0 (seed cố định để tất định), giữ kết quả cost thấp
            // nhất — kỹ thuật chuẩn để thoát cực tiểu cục bộ của thuật toán không đạo hàm.
            const int restarts = 6;
            var rng = new Unity.Mathematics.Random(1234567u);

            float[] overallBest = null;
            float overallBestCost = float.MaxValue;

            for (int r = 0; r < restarts; r++)
            {
                var start = (float[])p0.Clone();
                if (r > 0)
                {
                    // Nhiễu điểm khởi đầu theo đúng quy mô của từng chiều (InitScale),
                    // trừ lần đầu (r=0) giữ nguyên p0 để không mất khởi điểm "sạch".
                    for (int j = 0; j < Dim; j++)
                        start[j] += (rng.NextFloat() * 2f - 1f) * InitScale[j] * 2f;
                }

                var (candidate, cost) = RunSimplex(start, observed, initialGuess);
                if (cost < overallBestCost)
                {
                    overallBestCost = cost;
                    overallBest = candidate;
                }
            }

            rmsError = math.sqrt(overallBestCost / observed.Length);
            fittedInitial = UnpackState(overallBest, initialGuess);
            return UnpackParams(overallBest, initialGuess);
        }

        private static (float[] best, float cost) RunSimplex(float[] p0, TrackedPoint[] observed, BallState seed)
        {
            int n = Dim;
            int m = n + 1;
            var simplex = new float[m][];
            simplex[0] = (float[])p0.Clone();
            for (int i = 1; i < m; i++)
            {
                var v = (float[])p0.Clone();
                v[i - 1] += InitScale[i - 1];
                simplex[i] = v;
            }

            var costs = new float[m];
            for (int i = 0; i < m; i++) costs[i] = Cost(simplex[i], observed, seed);

            const int maxIter = 4000;
            for (int iter = 0; iter < maxIter; iter++)
            {
                Array.Sort(costs, simplex);
                if (math.abs(costs[m - 1] - costs[0]) < 1e-9f) break;

                // Centroid của tất cả trừ điểm tệ nhất
                var centroid = new float[n];
                for (int i = 0; i < m - 1; i++)
                    for (int j = 0; j < n; j++) centroid[j] += simplex[i][j] / (m - 1);

                var worst = simplex[m - 1];

                // Reflection
                var refl = new float[n];
                for (int j = 0; j < n; j++) refl[j] = centroid[j] + (centroid[j] - worst[j]);
                float cr = Cost(refl, observed, seed);

                if (cr < costs[0])
                {
                    // Expansion
                    var exp = new float[n];
                    for (int j = 0; j < n; j++) exp[j] = centroid[j] + 2f * (centroid[j] - worst[j]);
                    float ce = Cost(exp, observed, seed);
                    if (ce < cr) { simplex[m - 1] = exp; costs[m - 1] = ce; }
                    else { simplex[m - 1] = refl; costs[m - 1] = cr; }
                }
                else if (cr < costs[m - 2])
                {
                    simplex[m - 1] = refl; costs[m - 1] = cr;
                }
                else
                {
                    bool outside = cr < costs[m - 1];
                    var con = new float[n];
                    for (int j = 0; j < n; j++)
                        con[j] = centroid[j] + (outside ? 0.5f : -0.5f) * (worst[j] - centroid[j]);
                    float cc = Cost(con, observed, seed);
                    if (cc < math.min(cr, costs[m - 1]))
                    {
                        simplex[m - 1] = con; costs[m - 1] = cc;
                    }
                    else
                    {
                        // Shrink về best
                        for (int i = 1; i < m; i++)
                        {
                            for (int j = 0; j < n; j++) simplex[i][j] = simplex[0][j] + 0.5f * (simplex[i][j] - simplex[0][j]);
                            costs[i] = Cost(simplex[i], observed, seed);
                        }
                    }
                }
            }

            Array.Sort(costs, simplex);
            return (simplex[0], costs[0]);
        }

        // ---- Packing: [pos.xyz, vel.xyz, cdLow, cdHigh, cdVLow, cdVHigh, lift, spinDecay] ----

        private static float[] Pack(BallState s, BallParams p)
        {
            return new[]
            {
                s.position.x, s.position.y, s.position.z,
                s.velocity.x, s.velocity.y, s.velocity.z,
                p.cdLow, p.cdHigh, p.cdVLow, p.cdVHigh, p.liftCoefficient, p.spinDecayPerSecond
            };
        }

        private static BallState UnpackState(float[] x, BallState guess)
        {
            return new BallState(
                new float3(x[0], x[1], x[2]),
                new float3(x[3], x[4], x[5]),
                guess.spin); // spin ban đầu coi như đo được trực tiếp, không fit
        }

        private static BallParams UnpackParams(float[] x, BallState guess)
        {
            // Lấy khung vật lý từ Default rồi ghi đè các trường được fit.
            var p = BallParams.Default;
            // Giữ hằng số vật lý theo initialGuess không khả thi vì initialGuess là BallState;
            // dùng giá trị Default cho mass/radius/airDensity/gravity — đây là hằng số vật lý chuẩn game.
            p.cdLow = x[6];
            p.cdHigh = x[7];
            p.cdVLow = x[8];
            p.cdVHigh = x[9];
            p.liftCoefficient = x[10];
            p.spinDecayPerSecond = x[11];
            return p;
        }

        private static float Cost(float[] x, TrackedPoint[] observed, BallState seed)
        {
            var state = UnpackState(x, seed);
            var prms = UnpackParams(x, seed);
            float sumSq = 0f;

            // Tích phân một lần, ghi lại vị trí tại đúng các mốc time quan sát được.
            int nextIdx = 0;
            float t = 0f;
            // Xử lý các điểm ở t=0 ngay lập tức
            while (nextIdx < observed.Length && observed[nextIdx].time <= 0f)
            {
                sumSq += math.lengthsq(observed[nextIdx].position - state.position);
                nextIdx++;
            }

            for (int step = 0; step < MaxSteps && nextIdx < observed.Length; step++)
            {
                state = BallSolver.Step(state, prms, Dt);
                t += Dt;
                while (nextIdx < observed.Length && observed[nextIdx].time <= t + Dt * 0.5f)
                {
                    sumSq += math.lengthsq(observed[nextIdx].position - state.position);
                    nextIdx++;
                }
            }

            // Nếu còn điểm ngoài tầm tích phân (time quá lớn), phạt nhẹ để tránh kẹt vùng vô nghĩa.
            if (!math.isfinite(sumSq)) return 1e12f;
            return sumSq;
        }
    }
}
