using Unity.Collections;
using Unity.Mathematics;

namespace Eleven.Keeper
{
    /// <summary>
    /// Cài đặt Bayesian của IKeeperBrain (T18).
    ///
    /// Thuật toán:
    /// 1. Tính likelihood từ tín hiệu thị giác (KeeperCues) → softmax trên 9 ô
    /// 2. Lấy prior từ ShotHistory
    /// 3. Nhân prior × likelihood → posterior
    /// 4. Thêm nhiễu Dirichlet-like từ seed (tỉ lệ nghịch với readAccuracy)
    /// 5. Trộn với phân phối đều theo observability
    /// 6. Chuẩn hoá, tính bestCell và confidence
    ///
    /// TẤT ĐỊNH: cùng input cho cùng output từng bit.
    /// 0 GC allocation.
    /// </summary>
    public sealed class BayesianKeeperBrain : IKeeperBrain
    {
        // ── Bảng tâm tín hiệu cho từng ô ────────────────────────
        // Mỗi ô có "chữ ký tín hiệu" mong đợi:
        //   [lateralOffset, hipYaw, approachAngle]
        //
        // Quy ước (từ góc nhìn thủ môn nhìn ra):
        //   lateralOffset > 0 → chân trụ đặt bên phải bóng → sút sang trái thủ môn (ô 0,3,6)
        //   lateralOffset < 0 → chân trụ đặt bên trái bóng → sút sang phải thủ môn (ô 2,5,8)
        //   hipYaw > 0   → hông xoay trái → sút sang trái
        //   hipYaw < 0   → hông xoay phải → sút sang phải
        //   approachAngle > 0 → chạy đà từ phải → sút sang trái
        //   approachAngle < 0 → chạy đà từ trái → sút sang phải
        //
        // Hàng trên (0,1,2): runUpLength dài hơn, approach mạnh hơn
        // Hàng giữa (3,4,5): trung bình
        // Hàng dưới (6,7,8): runUpLength ngắn, approach nhẹ

        // Tâm lateral offset mong đợi cho mỗi ô (mét)
        static readonly float[] CenterLateral = {
             0.20f,  0.00f, -0.20f,  // top: left, center, right
             0.15f,  0.00f, -0.15f,  // mid
             0.12f,  0.00f, -0.12f   // bot
        };

        // Tâm hip yaw mong đợi cho mỗi ô (độ)
        static readonly float[] CenterHipYaw = {
             15f,  0f, -15f,
             12f,  0f, -12f,
             10f,  0f, -10f
        };

        // Tâm approach angle mong đợi cho mỗi ô (độ)
        static readonly float[] CenterApproach = {
             20f,  0f, -20f,
             15f,  0f, -15f,
             10f,  0f, -10f
        };

        // Tâm runUpLength mong đợi cho mỗi ô (mét)
        // Top row = chạy đà dài, bot row = ngắn
        static readonly float[] CenterRunUp = {
            4.5f, 4.5f, 4.5f,
            3.5f, 3.5f, 3.5f,
            2.5f, 2.5f, 2.5f
        };

        // Độ rộng Gaussian (sigma) cho mỗi chiều tín hiệu
        const float SigmaLateral  = 0.15f;
        const float SigmaHipYaw   = 12f;
        const float SigmaApproach = 15f;
        const float SigmaRunUp    = 1.5f;

        // ── Trọng số tín hiệu ──────────────────────────────────
        // Cột (trái/giữa/phải) dễ đọc hơn hàng (trên/giữa/dưới)
        const float WeightLateral  = 1.0f;
        const float WeightHipYaw   = 0.8f;
        const float WeightApproach = 0.5f;
        const float WeightRunUp    = 0.3f;

        public KeeperRead Infer(in KeeperCues cues, in ShotHistory history,
                                KeeperProfile profile, uint seed)
        {
            var rng = new Random(seed != 0 ? seed : 1u);

            float readAcc = profile != null ? math.saturate(profile.readAccuracy) : 0.5f;
            float memWeight = profile != null ? math.saturate(profile.memoryWeight) : 0.5f;

            // ── Bước 1: Tính log-likelihood cho mỗi ô ──────────
            var logLikelihood = new FixedList64Bytes<float>();
            for (int c = 0; c < 9; c++)
            {
                float ll = 0f;

                // Gaussian log-likelihood: -0.5 * ((x - mu) / sigma)^2
                float dLat = (cues.plantFootLateralOffset - CenterLateral[c]) / SigmaLateral;
                float dHip = (cues.hipYawDegrees - CenterHipYaw[c]) / SigmaHipYaw;
                float dApp = (cues.approachAngleDegrees - CenterApproach[c]) / SigmaApproach;
                float dRun = (cues.runUpLength - CenterRunUp[c]) / SigmaRunUp;

                ll -= 0.5f * WeightLateral  * dLat * dLat;
                ll -= 0.5f * WeightHipYaw   * dHip * dHip;
                ll -= 0.5f * WeightApproach * dApp * dApp;
                ll -= 0.5f * WeightRunUp    * dRun * dRun;

                logLikelihood.Add(ll);
            }

            // ── Bước 2: Softmax với sharpness theo readAccuracy ─
            // readAccuracy cao → phân phối nhọn hơn (keeper đọc chính xác hơn)
            // Sharpness: readAcc=0.3 → 2.0, readAcc=0.52 → 3.6, readAcc=0.72 → 5.3
            float sharpness = 1.0f + readAcc * readAcc * 10.0f;

            var likelihood = new FixedList64Bytes<float>();
            float maxLL = float.MinValue;
            for (int c = 0; c < 9; c++)
                maxLL = math.max(maxLL, logLikelihood[c]);

            float sumExp = 0f;
            for (int c = 0; c < 9; c++)
            {
                float val = math.exp(sharpness * (logLikelihood[c] - maxLL));
                likelihood.Add(val);
                sumExp += val;
            }

            // Chuẩn hoá likelihood
            if (sumExp > 0f)
            {
                float inv = 1f / sumExp;
                for (int c = 0; c < 9; c++)
                    likelihood[c] *= inv;
            }

            // ── Bước 3: Lấy prior từ lịch sử ───────────────────
            var prior = history.Prior(memWeight, 0.75f);

            // ── Bước 4: Bayesian update: posterior ∝ prior × likelihood
            var posterior = new FixedList64Bytes<float>();
            float sumPost = 0f;
            for (int c = 0; c < 9; c++)
            {
                float p = prior[c] * likelihood[c];
                // Bảo vệ: không cho âm
                p = math.max(p, 1e-10f);
                posterior.Add(p);
                sumPost += p;
            }

            // Chuẩn hoá posterior
            if (sumPost > 0f)
            {
                float inv = 1f / sumPost;
                for (int c = 0; c < 9; c++)
                    posterior[c] *= inv;
            }

            // ── Bước 5: Thêm nhiễu tất định từ seed ────────────
            // Nhiễu Dirichlet-like: thêm noise ∝ (1 - readAccuracy)
            float noiseScale = (1f - readAcc) * 0.15f;

            if (noiseScale > 1e-6f)
            {
                float sumNoise = 0f;
                var noisy = new FixedList64Bytes<float>();
                for (int c = 0; c < 9; c++)
                {
                    // Tạo nhiễu exponential từ uniform (Dirichlet(1) = Exponential)
                    float u = math.max(rng.NextFloat(), 1e-10f);
                    float noise = -math.log(u);
                    float val = posterior[c] + noiseScale * noise;
                    noisy.Add(val);
                    sumNoise += val;
                }

                // Chuẩn hoá
                if (sumNoise > 0f)
                {
                    float inv = 1f / sumNoise;
                    for (int c = 0; c < 9; c++)
                        posterior[c] = noisy[c] * inv;
                }
            }

            // ── Bước 6: Trộn với phân phối đều theo observability ─
            // observability=0 → đều hoàn toàn, observability=1 → posterior nguyên bản
            float obs = math.saturate(cues.observability);
            const float uniformVal = 1f / 9f;

            float sumFinal = 0f;
            for (int c = 0; c < 9; c++)
            {
                posterior[c] = math.lerp(uniformVal, posterior[c], obs);
                sumFinal += posterior[c];
            }

            // Chuẩn hoá lần cuối (đảm bảo tổng = 1 chính xác)
            if (sumFinal > 0f)
            {
                float inv = 1f / sumFinal;
                for (int c = 0; c < 9; c++)
                    posterior[c] *= inv;
            }

            // ── Bước 7: Tìm bestCell và tính confidence ─────────
            int best = 0;
            float maxProb = posterior[0];
            for (int c = 1; c < 9; c++)
            {
                if (posterior[c] > maxProb)
                {
                    maxProb = posterior[c];
                    best = c;
                }
            }

            // Confidence = xác suất của ô tốt nhất, quy về thang [0, 1] với 0 = phân phối đều.
            //
            // BẢN ĐẦU DÙNG ENTROPY và đó là một lỗi GHÉP TẦNG, không phải lỗi của T18:
            // 1 - entropy/log(9) trên 9 ô luôn ra số rất nhỏ với mọi phân phối thực tế
            // (đo được 0.03–0.10 ở bậc Thường), rồi còn nhân thêm observability. Trong khi
            // T19 (SimpleKeeperController) đặt ngưỡng theo thang XÁC SUẤT: 0.45 là "đủ chắc",
            // dưới 0.20 là "mù". Hai thang không gặp nhau, nên nhánh "confidence < 0.20 và
            // hết giờ → đứng giữa" ăn 100% số lượt: thủ môn không bao giờ bay người.
            // Cả hai lớp đều xanh test khi đứng riêng — chỉ khi đo bằng
            // KeeperReadsShotTests (dựng lại đúng tín hiệu mà trận đấu bơm vào) mới lộ ra.
            //
            // Thang mới khớp đúng ngữ nghĩa T19 mong đợi và vẫn giữ mọi bất biến cũ:
            // observability = 0 → posterior đều tuyệt đối → maxProb = 1/9 → confidence = 0.
            // Không cần nhân obs lần nữa: obs đã nằm trong posterior ở bước 6.
            float confidence = math.saturate((maxProb - uniformVal) / (1f - uniformVal));

            return new KeeperRead
            {
                cellProbabilities = posterior,
                bestCell = best,
                confidence = confidence
            };
        }
    }
}
