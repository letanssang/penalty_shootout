// ProceduralClips.cs — Tổng hợp toàn bộ âm thanh bằng code; không dùng file audio.
// Mọi hàm đều thuần túy (pure): cùng seed → cùng kết quả, đảm bảo tính tất định.

using System;
using Unity.Mathematics;
using UnityEngine;

namespace Eleven.Presentation.Audio
{
    /// <summary>
    /// Factory tĩnh: mỗi phương thức tạo AudioClip hoàn chỉnh trong bộ nhớ.
    /// Không có trạng thái; AudioDirector gọi một lần trong Awake rồi giữ reference.
    /// </summary>
    public static class ProceduralClips
    {
        // ─────────────────────────────────────────────────────────────────
        // CÒI TRỌNG TÀI (~0.5s)
        // Sóng vuông 2.6kHz + vibrato nhẹ để giống còi plastic thật.
        // Envelope: attack nhanh 5ms, sustain, decay cuối.
        // ─────────────────────────────────────────────────────────────────
        public static AudioClip Whistle(int sampleRate = 44100)
        {
            const float duration    = 0.50f;
            const float freqBase    = 2600f;  // tần số cơ bản còi
            const float vibratoRate = 18f;    // Hz rung
            const float vibratoAmt  = 0.012f; // biên độ rung tần số
            const float attackTime  = 0.005f;
            const float decayStart  = 0.38f;  // bắt đầu fade-out tính từ đầu

            int   n    = (int)(duration * sampleRate);
            var   data = new float[n];

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sampleRate;

                // Envelope hình thang: attack nhanh, phẳng, decay cuối
                float env = EnvTrapezoid(t, duration, attackTime, decayStart);

                // Sóng vuông cho âm sắc "plastic" của còi
                float phase  = freqBase * t + (vibratoAmt / vibratoRate) * math.sin(math.PI2 * vibratoRate * t);
                float square = math.sign(math.sin(math.PI2 * phase));

                // Harmonics bậc 3 thêm vào để tránh sóng vuông quá "digital"
                float h3 = math.sin(math.PI2 * freqBase * 3f * t) * 0.15f;

                data[i] = (square * 0.7f + h3) * env;
            }

            Normalize(data, 0.85f);
            return MakeClip("Whistle", data, sampleRate);
        }

        // ─────────────────────────────────────────────────────────────────
        // TIẾNG CHÂN SÚT BÓNG
        // power01 [0..1] → nâng tần số sine trầm và biên độ nhiễu.
        // Kết hợp: xung nhiễu ngắn (transient) + sine 90–160Hz (thân bóng).
        // ─────────────────────────────────────────────────────────────────
        public static AudioClip Kick(int sampleRate = 44100)
        {
            // Baked ở power01=0.5 làm base; AudioDirector điều chỉnh pitch khi play
            return KickAtPower(0.5f, sampleRate);
        }

        /// <summary>Tạo clip kick với mức power cụ thể — dùng nội bộ bởi AudioDirector.</summary>
        internal static AudioClip KickAtPower(float power01, int sampleRate = 44100)
        {
            // Kẹp để tránh NaN khi power01 ngoài biên
            power01 = math.saturate(power01);

            const float durationMax = 0.22f;
            const float noiseLen    = 0.040f; // 40ms xung nhiễu transient

            // power01 nâng tần số: 90Hz (nhẹ) → 160Hz (mạnh)
            float freq = math.lerp(90f, 160f, power01);
            float amp  = math.lerp(0.55f, 1.0f, power01);

            int   n    = (int)(durationMax * sampleRate);
            var   data = new float[n];

            // seed cố định → tất định
            var rng = new Unity.Mathematics.Random(0xBEEF_CAFEu + (uint)(power01 * 1000));

            int noiseN = (int)(noiseLen * sampleRate);

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sampleRate;

                // Thành phần sine trầm: decay mũ nhanh
                float sinePart = math.sin(math.PI2 * freq * t) * math.exp(-t * 28f);

                // Xung nhiễu transient (chỉ tồn tại trong noiseLen đầu)
                float noisePart = 0f;
                if (i < noiseN)
                {
                    float nt = (float)i / noiseN;
                    // Nhiễu trắng * envelope mũ xuống
                    noisePart = (rng.NextFloat() * 2f - 1f) * math.exp(-nt * 9f) * 0.6f;
                }

                data[i] = (sinePart + noisePart) * amp;
            }

            Normalize(data, 0.85f);
            return MakeClip($"Kick_{power01:F2}", data, sampleRate);
        }

        // ─────────────────────────────────────────────────────────────────
        // BÓNG GĂNG LƯỚI (~350ms)
        // Hai lớp: "phập" tần thấp (transient) + đuôi xào xạc tần cao suy giảm.
        // ─────────────────────────────────────────────────────────────────
        public static AudioClip Net(int sampleRate = 44100)
        {
            const float duration = 0.35f;

            int   n    = (int)(duration * sampleRate);
            var   data = new float[n];
            var   rng  = new Unity.Mathematics.Random(0xAB12_3456u);

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sampleRate;

                // Lớp trầm: nhiễu lọc thông thấp giả bằng running-average ngầm định
                // Dùng sine đa tần thấp để mô phỏng va chạm khối lượng
                float low = (math.sin(math.PI2 * 60f * t) * 0.5f
                           + math.sin(math.PI2 * 95f * t) * 0.3f)
                           * math.exp(-t * 18f);

                // Lớp cao: nhiễu trắng decay để nghe "xào xạc lưới"
                float noise = (rng.NextFloat() * 2f - 1f) * math.exp(-t * 14f) * 0.5f;

                data[i] = low + noise;
            }

            Normalize(data, 0.85f);
            return MakeClip("Net", data, sampleRate);
        }

        // ─────────────────────────────────────────────────────────────────
        // BÓNG ĐẬP CỘT/XÀ (~0.9s)
        // Hai sine không hài (620Hz + 940Hz) → âm sắc kim loại.
        // Méo nhẹ bằng hàm tanh để không nghe "digital" quá.
        // ─────────────────────────────────────────────────────────────────
        public static AudioClip Post(int sampleRate = 44100)
        {
            const float duration = 0.90f;
            const float f1       = 620f;
            const float f2       = 940f;

            int   n    = (int)(duration * sampleRate);
            var   data = new float[n];
            var   rng  = new Unity.Mathematics.Random(0xFEED_F00Du);

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sampleRate;

                // Decay chậm cho âm vang kim loại
                float env = math.exp(-t * 4.5f);

                float s = math.sin(math.PI2 * f1 * t) * 0.6f
                        + math.sin(math.PI2 * f2 * t) * 0.4f;

                // Transient ngắn nhiễu trắng ở đầu để nghe "coong"
                float hit = i < (int)(0.008f * sampleRate)
                    ? (rng.NextFloat() * 2f - 1f) * 0.5f
                    : 0f;

                // Méo mềm bằng tanh → âm sắc ấm hơn, bớt digital
                float raw = (s + hit) * env;
                data[i] = math.tanh(raw * 1.4f) / 1.4f;
            }

            Normalize(data, 0.85f);
            return MakeClip("Post", data, sampleRate);
        }

        // ─────────────────────────────────────────────────────────────────
        // TIẾNG GĂNG TAY (~80ms)
        // Nhiễu ngắn lọc thông thấp: "đấm" khô, không có âm vang.
        // Giả lọc LP bằng cộng dồn running average (IIR 1 cực đơn giản).
        // ─────────────────────────────────────────────────────────────────
        public static AudioClip Glove(int sampleRate = 44100)
        {
            const float duration = 0.080f;
            const float lpAlpha  = 0.15f; // càng nhỏ → càng trầm (LP mạnh hơn)

            int   n    = (int)(duration * sampleRate);
            var   data = new float[n];
            var   rng  = new Unity.Mathematics.Random(0xC0DE_BABEu);

            float prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float t    = (float)i / sampleRate;
                float env  = math.exp(-t * 55f); // decay rất nhanh → khô

                float white = rng.NextFloat() * 2f - 1f;
                // IIR 1-pole low-pass: y[n] = α*x[n] + (1-α)*y[n-1]
                prev    = lpAlpha * white + (1f - lpAlpha) * prev;
                data[i] = prev * env;
            }

            Normalize(data, 0.85f);
            return MakeClip("Glove", data, sampleRate);
        }

        // ─────────────────────────────────────────────────────────────────
        // KHÁN ĐÀI BÙNG NỔ (~2.5s)
        // Pink noise qua envelope bell hình chuông lên nhanh.
        // Thêm nhiều harmonics tần thấp để nghe "ấm" hơn white noise.
        // ─────────────────────────────────────────────────────────────────
        public static AudioClip CrowdRoar(int sampleRate = 44100)
        {
            const float duration  = 2.50f;
            const float peakAt    = 0.35f; // đỉnh volume ở 35% thời lượng → tăng nhanh

            int   n    = (int)(duration * sampleRate);
            var   data = new float[n];
            var   rng  = new Unity.Mathematics.Random(0x1234_5678u);

            // Bộ lọc pink noise đơn giản (Paul Kellet 3-pole approximation)
            float b0 = 0f, b1 = 0f, b2 = 0f;

            for (int i = 0; i < n; i++)
            {
                float t   = (float)i / sampleRate;
                float env = EnvBell(t, duration, peakAt);

                float white = rng.NextFloat() * 2f - 1f;
                // Pink noise filter (Kellet approximate)
                b0 = 0.99886f * b0 + white * 0.0555179f;
                b1 = 0.99332f * b1 + white * 0.0750759f;
                b2 = 0.96900f * b2 + white * 0.1538520f;
                float pink = (b0 + b1 + b2 + white * 0.5362f) * 0.11f;

                data[i] = pink * env;
            }

            Normalize(data, 0.85f);
            return MakeClip("CrowdRoar", data, sampleRate);
        }

        // ─────────────────────────────────────────────────────────────────
        // KHÁN ĐÀI THỞ DÀI (~2.0s)
        // Giống roar nhưng: envelope trôi xuống dần, tần thấp hơn.
        // Pitch shift mô phỏng bằng nhiễu băng hẹp tần trầm.
        // ─────────────────────────────────────────────────────────────────
        public static AudioClip CrowdGroan(int sampleRate = 44100)
        {
            const float duration = 2.00f;
            const float peakAt   = 0.15f; // lên nhanh rồi trôi xuống → "ờ…"

            int   n    = (int)(duration * sampleRate);
            var   data = new float[n];
            var   rng  = new Unity.Mathematics.Random(0x8765_4321u);

            // Pink noise filter riêng biệt cho clip này
            float b0 = 0f, b1 = 0f, b2 = 0f;

            for (int i = 0; i < n; i++)
            {
                float t   = (float)i / sampleRate;
                float env = EnvBell(t, duration, peakAt);

                float white = rng.NextFloat() * 2f - 1f;
                b0 = 0.99886f * b0 + white * 0.0555179f;
                b1 = 0.99332f * b1 + white * 0.0750759f;
                b2 = 0.96900f * b2 + white * 0.1538520f;
                float pink = (b0 + b1 + b2 + white * 0.5362f) * 0.11f;

                // Lọc thêm LP một cực để groan tối hơn roar
                // (reuse b2 như running avg — đủ cho mục đích perceptual)
                float modLow = math.sin(math.PI2 * 180f * t) * 0.04f; // rumble trầm mờ
                data[i] = (pink + modLow) * env;
            }

            Normalize(data, 0.85f);
            return MakeClip("CrowdGroan", data, sampleRate);
        }

        // ─────────────────────────────────────────────────────────────────
        // TIẾNG ỒN NỀN KHÁN ĐÀI (~4s, LOOP LIỀN MẠCH)
        // Pink noise biên độ thấp. Crossfade 1/8 cuối về đầu để loop
        // không có "click" hay "silence" tại điểm nối.
        // ─────────────────────────────────────────────────────────────────
        public static AudioClip CrowdAmbientLoop(int sampleRate = 44100)
        {
            const float duration      = 4.00f;
            const float baseAmplitude = 0.18f;

            int   n       = (int)(duration * sampleRate);
            int   xfadeN  = n / 8; // crossfade 1/8 clip cuối ↔ đầu
            var   data    = new float[n];
            var   rng     = new Unity.Mathematics.Random(0xDEAD_BEEFu);

            // Bước 1: sinh pink noise thuần túy
            float b0 = 0f, b1 = 0f, b2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float white = rng.NextFloat() * 2f - 1f;
                b0 = 0.99886f * b0 + white * 0.0555179f;
                b1 = 0.99332f * b1 + white * 0.0750759f;
                b2 = 0.96900f * b2 + white * 0.1538520f;
                data[i] = (b0 + b1 + b2 + white * 0.5362f) * 0.11f * baseAmplitude;
            }

            // Bước 2: crossfade vùng cuối → đầu để loop liền mạch
            // Tại sample i_end = n - xfadeN + k: pha ra nền_cuối, pha vào nền_đầu
            // Kết quả ghi lại vào n - xfadeN..n
            for (int k = 0; k < xfadeN; k++)
            {
                float alpha = (float)k / xfadeN;         // 0 → 1 (fade-in phần đầu)
                int   iEnd  = n - xfadeN + k;
                // Pha chồng: cuối fade-out * (1-alpha) + đầu fade-in * alpha
                data[iEnd] = data[iEnd] * (1f - alpha) + data[k] * alpha;
            }

            // Đánh dấu clip là loop → AudioSource.loop = true hoạt động hoàn hảo
            var clip = AudioClip.Create("CrowdAmbientLoop", n, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ─────────────────────────────────────────────────────────────────
        // CLICK NÚT UI (~30ms)
        // Xung sine ngắn decay ngay → cảm giác "snappy", không nghe "pop".
        // ─────────────────────────────────────────────────────────────────
        public static AudioClip UiClick(int sampleRate = 44100)
        {
            const float duration = 0.030f;
            const float freq     = 1200f;

            int   n    = (int)(duration * sampleRate);
            var   data = new float[n];

            for (int i = 0; i < n; i++)
            {
                float t    = (float)i / sampleRate;
                float env  = math.exp(-t * 120f); // decay cực nhanh → "click" không kéo dài
                data[i]    = math.sin(math.PI2 * freq * t) * env;
            }

            Normalize(data, 0.85f);
            return MakeClip("UiClick", data, sampleRate);
        }

        // ═════════════════════════════════════════════════════════════════
        // PHƯƠNG THỨC TRỢ GIÚP NỘI BỘ
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Envelope hình thang: attack tuyến tính → plateau → decay tuyến tính.
        /// decayStart tính từ t=0 (không phải từ cuối).
        /// </summary>
        private static float EnvTrapezoid(float t, float duration, float attackTime, float decayStart)
        {
            if (t < attackTime)
                return t / attackTime;
            if (t < decayStart)
                return 1f;
            // Decay tuyến tính từ decayStart đến duration
            float decayLen = duration - decayStart;
            if (decayLen <= 0f) return 1f;
            return math.saturate(1f - (t - decayStart) / decayLen);
        }

        /// <summary>
        /// Envelope hình chuông (bell) dùng cho tiếng đám đông.
        /// peakRatio [0..1]: vị trí đỉnh tương đối trong clip.
        /// Sườn lên: hàm mũ; sườn xuống: decay mũ chậm.
        /// </summary>
        private static float EnvBell(float t, float duration, float peakRatio)
        {
            float peakTime = duration * peakRatio;
            if (t <= peakTime)
            {
                // Lên nhanh: quadratic ease-in
                float r = t / math.max(peakTime, 1e-5f);
                return r * r;
            }
            else
            {
                // Xuống từ từ: decay mũ
                float afterPeak = t - peakTime;
                float tail      = duration - peakTime;
                return math.exp(-afterPeak / tail * 3.5f);
            }
        }

        /// <summary>
        /// Chuẩn hoá mảng về đỉnh targetPeak để chống clipping.
        /// Nếu mảng toàn 0 thì bỏ qua.
        /// </summary>
        private static void Normalize(float[] data, float targetPeak)
        {
            float peak = 0f;
            for (int i = 0; i < data.Length; i++)
                peak = math.max(peak, math.abs(data[i]));

            if (peak < 1e-6f) return; // tránh chia cho 0

            float scale = targetPeak / peak;
            for (int i = 0; i < data.Length; i++)
                data[i] *= scale;
        }

        /// <summary>
        /// Tạo AudioClip mono từ mảng float đã chuẩn bị.
        /// stream:false → toàn bộ dữ liệu trong RAM, không cần disk IO.
        /// </summary>
        private static AudioClip MakeClip(string name, float[] data, int sampleRate)
        {
            var clip = AudioClip.Create(name, data.Length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
