using NUnit.Framework;
using UnityEngine.TestTools.Constraints;   // extension AllocatingGCMemory()
using Is = NUnit.Framework.Is;             // khu nhap nhang voi lop Is cua Unity
using Unity.Mathematics;
using Eleven.Ball;
using Eleven.Shooter;

namespace Eleven.Tests.EditMode
{
    /// <summary>
    /// Nghiệm thu T15 — phần KnuckleForce. Tích phân thật gia tốc qua pha bay để kiểm
    /// ràng buộc cứng maxLateralDeviation. Phong cách bám theo BallSolverTests.
    /// </summary>
    [TestFixture]
    public class KnuckleForceTests
    {
        // ─── Hàm tiện ích ─────────────────────────────────────────────

        static bool HasNanOrInf(float3 v) =>
            math.any(math.isnan(v)) || math.any(math.isinf(v));

        /// <summary>So sánh từng bit hai float3, không dùng dung sai.</summary>
        static void AssertBitExact(float3 expected, float3 actual, string msg)
        {
            Assert.AreEqual(math.asuint(expected.x), math.asuint(actual.x),
                $"{msg} — X sai bit (expected {math.asuint(expected.x)}, actual {math.asuint(actual.x)})");
            Assert.AreEqual(math.asuint(expected.y), math.asuint(actual.y),
                $"{msg} — Y sai bit (expected {math.asuint(expected.y)}, actual {math.asuint(actual.y)})");
            Assert.AreEqual(math.asuint(expected.z), math.asuint(actual.z),
                $"{msg} — Z sai bit (expected {math.asuint(expected.z)}, actual {math.asuint(actual.z)})");
        }

        /// <summary>
        /// BallState chuẩn cho test: sút thẳng 28 m/s theo +Z, không xoáy.
        /// Tốc độ trên onsetSpeed mặc định (18 m/s) nên knuckle phải kích hoạt.
        /// </summary>
        static BallState FastStraightShot() => new BallState(
            float3.zero,
            new float3(0f, 2f, 28f),
            float3.zero);

        // ─── 1. maxLateralDeviation không bao giờ bị vượt — 500 seed, tích phân thật ───

        [Test]
        public void Knuckle_500Seed_DoLechNgangKhongBaoGioVuotMaxLateralDeviation()
        {
            // Tích phân gia tốc knuckle qua pha bay ~0.5s, dt = 1/120. Đo độ lệch ngang
            // tích luỹ so với quỹ đạo không có knuckle. Đây là test QUAN TRỌNG NHẤT:
            // nếu mô hình toán đúng thì 500 seed phải qua hết.
            var cfg = KnuckleConfig.Default;
            float dt = 1f / 120f;
            int steps = 60; // ~0.5 giây

            for (uint seed = 0; seed < 500; seed++)
            {
                var s = FastStraightShot();

                // Tích phân hai lần: vận tốc lệch += gia tốc * dt, vị trí lệch += vận tốc lệch * dt.
                // Chỉ tích phân thành phần knuckle, tách khỏi solver chính.
                float3 knuckleVel = float3.zero;
                float3 knucklePos = float3.zero;
                float maxDev = 0f;

                for (int i = 0; i < steps; i++)
                {
                    float elapsed = (i + 1) * dt;
                    float3 accel = KnuckleForce.Evaluate(s, cfg, elapsed, seed);

                    // Euler tích phân knuckle riêng: vì Evaluate trả đạo hàm bậc hai
                    // của hàm d(t) đã bị chặn, nên tích phân ra phải khớp d(t).
                    knuckleVel += accel * dt;
                    knucklePos += knuckleVel * dt;

                    float dev = math.length(knucklePos);
                    maxDev = math.max(maxDev, dev);
                }

                // Hai mức, cố ý kiểm cả hai:
                //
                // 1) Hợp đồng T15 nói "KHÔNG BAO GIỜ vượt maxLateralDeviation" — kiểm đúng chữ,
                //    không dung sai. Nới dung sai ở đây là tự tay biến ràng buộc cứng thành
                //    ràng buộc mềm rồi vẫn tick vào ô nghiệm thu.
                Assert.LessOrEqual(maxDev, cfg.maxLateralDeviation,
                    $"seed {seed}: độ lệch tích luỹ {maxDev:F4} vượt maxLateralDeviation {cfg.maxLateralDeviation}");

                // 2) Chặn THẬT của mô hình toán chặt hơn hợp đồng: |d(t)| ≤ min(amplitude,
                //    maxLateralDeviation). Kiểm mức này mới bắt được lỗi trong công thức —
                //    mức trên còn dư 37% khoảng trống nên một công thức sai vẫn lọt.
                //    1% dung sai để bao sai số tích phân Euler ở dt = 1/120 (ω·dt ≈ 0.06 rad
                //    nên sai số bậc hai cỡ 0.3%), không phải để che chỗ hụt của mô hình.
                float chanThat = math.min(cfg.amplitude, cfg.maxLateralDeviation);
                Assert.LessOrEqual(maxDev, chanThat * 1.01f,
                    $"seed {seed}: độ lệch {maxDev:F4} vượt chặn toán học {chanThat:F4} — " +
                    "công thức d(t) không còn bị chặn như chứng minh trong KnuckleForce.");
            }
        }

        // ─── 2. Cùng seed → cùng kết quả từng bit ───

        [Test]
        public void Knuckle_CungSeed_KetQuaGiongTungBit()
        {
            var s = FastStraightShot();
            var cfg = KnuckleConfig.Default;

            for (uint seed = 0; seed < 20; seed++)
            {
                for (float elapsed = 0f; elapsed <= 0.5f; elapsed += 1f / 120f)
                {
                    var a = KnuckleForce.Evaluate(s, cfg, elapsed, seed);
                    var b = KnuckleForce.Evaluate(s, cfg, elapsed, seed);
                    AssertBitExact(a, b, $"seed {seed}, elapsed {elapsed:F6}");
                }
            }
        }

        // ─── 3. Seed khác → đường bay khác thật sự ───

        [Test]
        public void Knuckle_SeedKhac_DuongBayKhacThatSu()
        {
            var s = FastStraightShot();
            var cfg = KnuckleConfig.Default;
            float elapsed = 0.25f; // giữa pha bay, envelope đã lên cao

            float3 ref0 = KnuckleForce.Evaluate(s, cfg, elapsed, 1u);
            int khacCount = 0;

            for (uint seed = 2; seed <= 100; seed++)
            {
                float3 v = KnuckleForce.Evaluate(s, cfg, elapsed, seed);
                if (math.distance(v, ref0) > 0.1f)
                    khacCount++;
            }

            // Ít nhất 80% seed phải cho kết quả khác rõ ràng.
            Assert.Greater(khacCount, 79,
                $"Chỉ có {khacCount}/99 seed cho kết quả khác rõ — đa dạng không đủ.");
        }

        // ─── 4. Dưới onsetSpeed → lực bằng đúng 0 ───

        [Test]
        public void Knuckle_DuoiNguongTocDo_LucBangKhong()
        {
            var cfg = KnuckleConfig.Default;
            // Tốc độ 17.9 m/s, dưới onsetSpeed = 18 m/s.
            var s = new BallState(float3.zero, new float3(0f, 0f, 17.9f), float3.zero);

            for (float elapsed = 0f; elapsed <= 0.5f; elapsed += 0.05f)
            {
                float3 f = KnuckleForce.Evaluate(s, cfg, elapsed, 42u);
                Assert.AreEqual(float3.zero, f,
                    $"elapsed {elapsed}: dưới onsetSpeed phải trả float3.zero");
            }
        }

        // ─── 5. Ngay trên ngưỡng → lực khác 0 ───

        [Test]
        public void Knuckle_NgayTrenNguong_LucKhacKhong()
        {
            var cfg = KnuckleConfig.Default;
            // Tốc độ ngay trên onsetSpeed.
            var s = new BallState(float3.zero, new float3(0f, 0f, cfg.onsetSpeed + 0.1f), float3.zero);

            // Kiểm elapsed đủ lớn để envelope lên.
            float3 f = KnuckleForce.Evaluate(s, cfg, 0.2f, 7u);
            Assert.Greater(math.length(f), 0f,
                "Ngay trên ngưỡng onsetSpeed, gia tốc knuckle phải khác 0.");
        }

        // ─── 6. Xoáy khác 0 → lực bằng đúng 0 ───

        [Test]
        public void Knuckle_XoayKhacKhong_LucBangKhong()
        {
            var cfg = KnuckleConfig.Default;
            // Bóng nhanh nhưng có xoáy — knuckle phải tắt.
            var s = new BallState(float3.zero, new float3(0f, 2f, 28f), new float3(0f, 10f, 0f));

            for (float elapsed = 0f; elapsed <= 0.5f; elapsed += 0.05f)
            {
                float3 f = KnuckleForce.Evaluate(s, cfg, elapsed, 99u);
                Assert.AreEqual(float3.zero, f,
                    $"elapsed {elapsed}: xoáy khác 0 thì knuckle phải tắt hoàn toàn");
            }
        }

        // ─── 7. elapsed = 0 → lực bằng 0 (envelope) và không NaN ───

        [Test]
        public void Knuckle_ElapsedBangKhong_LucBangKhong_KhongNaN()
        {
            var s = FastStraightShot();
            var cfg = KnuckleConfig.Default;

            float3 f = KnuckleForce.Evaluate(s, cfg, 0f, 1u);
            Assert.AreEqual(float3.zero, f,
                "elapsed = 0: envelope E(0) = 0 nên lực phải bằng 0");
            Assert.IsFalse(HasNanOrInf(f), "elapsed = 0: không được NaN/Inf");
        }

        // ─── 8. Biên: v = 0 ───

        [Test]
        public void Knuckle_Bien_VanTocKhong_KhongNaN()
        {
            var s = new BallState(float3.zero, float3.zero, float3.zero);
            var cfg = KnuckleConfig.Default;

            float3 f = KnuckleForce.Evaluate(s, cfg, 0.3f, 5u);
            Assert.IsFalse(HasNanOrInf(f), "v = 0: không được NaN/Inf");
            // v = 0 < onsetSpeed nên lực phải bằng 0 luôn.
            Assert.AreEqual(float3.zero, f, "v = 0: dưới onsetSpeed nên lực = 0");
        }

        // ─── 9. Biên: config toàn 0 ───

        [Test]
        public void Knuckle_Bien_ConfigToanKhong_KhongNaN()
        {
            var s = FastStraightShot();
            var cfg = new KnuckleConfig(); // tất cả trường = 0

            float3 f = KnuckleForce.Evaluate(s, cfg, 0.3f, 5u);
            Assert.IsFalse(HasNanOrInf(f), "config toàn 0: không được NaN/Inf");
            Assert.AreEqual(float3.zero, f, "config toàn 0: lực phải bằng 0");
        }

        // ─── 10. Biên: elapsed rất lớn ───

        [Test]
        public void Knuckle_Bien_ElapsedRatLon_KhongNaN()
        {
            var s = FastStraightShot();
            var cfg = KnuckleConfig.Default;

            // elapsed = 100s — envelope bão hoà, exp rất nhỏ.
            float3 f = KnuckleForce.Evaluate(s, cfg, 100f, 5u);
            Assert.IsFalse(HasNanOrInf(f), "elapsed rất lớn: không được NaN/Inf");
        }

        // ─── 11. Biên: v gần song song trục Y ───

        [Test]
        public void Knuckle_Bien_VanTocGanSongSongTrucY_KhongNaN()
        {
            var cfg = KnuckleConfig.Default;
            // Vận tốc gần như hoàn toàn theo trục Y, trên onsetSpeed.
            var s = new BallState(float3.zero, new float3(0f, 30f, 0.001f), float3.zero);

            float3 f = KnuckleForce.Evaluate(s, cfg, 0.2f, 42u);
            Assert.IsFalse(HasNanOrInf(f), "v gần song song Y: không được NaN/Inf");
        }

        // ─── 12. Lực luôn vuông góc với vận tốc ───

        [Test]
        public void Knuckle_LucLuonVuongGocVoiVanToc()
        {
            var cfg = KnuckleConfig.Default;
            var rng = Unity.Mathematics.Random.CreateFromIndex(12345u);

            for (int trial = 0; trial < 50; trial++)
            {
                // Sinh vận tốc ngẫu nhiên trên onsetSpeed, không xoáy.
                float3 dir = math.normalize(rng.NextFloat3Direction());
                float speed = rng.NextFloat(cfg.onsetSpeed + 1f, 40f);
                var s = new BallState(float3.zero, dir * speed, float3.zero);
                float elapsed = rng.NextFloat(0.05f, 0.5f);
                uint seed = rng.NextUInt(1u, 10000u);

                float3 f = KnuckleForce.Evaluate(s, cfg, elapsed, seed);

                if (math.lengthsq(f) < 1e-12f) continue; // lực quá nhỏ thì tích vô hướng không có nghĩa

                // Tích vô hướng giữa lực và vận tốc phải gần 0.
                float dot = math.dot(f, s.velocity);
                float relError = math.abs(dot) / (math.length(f) * math.length(s.velocity));
                Assert.Less(relError, 1e-4f,
                    $"trial {trial}: lực không vuông góc với vận tốc (cosine = {relError})");
            }
        }

        // ─── 13. Cấp phát bộ nhớ bằng 0 ───

        [Test]
        public void Knuckle_KhongCapPhat()
        {
            var s = FastStraightShot();
            var cfg = KnuckleConfig.Default;

            // Warm-up: loại JIT ra khỏi phép đo.
            KnuckleForce.Evaluate(s, cfg, 0.1f, 1u);

            Assert.That(() =>
            {
                KnuckleForce.Evaluate(s, cfg, 0.1f, 1u);
            }, Is.Not.AllocatingGCMemory());
        }

        // ─── 14. Xoáy cực nhỏ nhưng khác 0 vẫn tắt knuckle ───

        [Test]
        public void Knuckle_XoayCucNhoKhacKhong_VanTat()
        {
            var cfg = KnuckleConfig.Default;
            // Xoáy rất nhỏ nhưng KHÁC 0 — lengthsq > 0 nên knuckle phải tắt.
            var s = new BallState(float3.zero, new float3(0f, 0f, 28f), new float3(1e-6f, 0f, 0f));

            float3 f = KnuckleForce.Evaluate(s, cfg, 0.2f, 5u);
            Assert.AreEqual(float3.zero, f,
                "Xoáy cực nhỏ nhưng khác 0: knuckle phải tắt — hai hiệu ứng loại trừ nhau.");
        }

        // ─── 15. Default: amplitude ≤ maxLateralDeviation ───

        [Test]
        public void KnuckleConfig_Default_AmplitudeKhongVuotMaxDeviation()
        {
            var cfg = KnuckleConfig.Default;
            Assert.LessOrEqual(cfg.amplitude, cfg.maxLateralDeviation,
                "Default amplitude phải ≤ maxLateralDeviation để ràng buộc cứng tự thoả.");
            Assert.Greater(cfg.maxLateralDeviation, 0f, "maxLateralDeviation phải dương.");
            Assert.Greater(cfg.frequencyHz, 0f, "frequencyHz phải dương.");
            Assert.Greater(cfg.onsetSpeed, 0f, "onsetSpeed phải dương.");
            Assert.Greater(cfg.envelopeRiseSeconds, 0f,
                "envelopeRiseSeconds phải dương, nếu không hiệu ứng tắt hẳn.");
        }

        // ─── 16. elapsed không hợp lệ ───

        [Test]
        public void Knuckle_ElapsedAmHoacNaN_LucBangKhong()
        {
            var s = FastStraightShot();
            var cfg = KnuckleConfig.Default;

            // NaN là ca nguy hiểm nhất: mọi so sánh với NaN đều false nên nó lọt qua hết các
            // câu chặn khác rồi biến gia tốc thành NaN, và NaN đó đi tiếp vào solver.
            foreach (float elapsed in new[] { -0.1f, -1e-6f, float.NaN })
            {
                float3 f = KnuckleForce.Evaluate(s, cfg, elapsed, 3u);
                Assert.AreEqual(float3.zero, f, $"elapsed {elapsed} phải cho lực đúng bằng 0");
                Assert.IsFalse(HasNanOrInf(f), $"elapsed {elapsed} sinh NaN/Inf");
            }
        }

        // ─── 17. Hiệu ứng PHẢI có tác dụng thật ───

        [Test]
        public void Knuckle_TrongPhaBayThat_DoLechDuLonDeCoNghiaGameplay()
        {
            // Mọi test ở trên đều là test CHẶN TRÊN: một cài đặt trả về 0 vĩnh viễn sẽ qua
            // sạch cả 15 test. Đây là test chặn DƯỚI, và nó là thứ duy nhất phân biệt
            // "hiệu ứng đúng" với "hiệu ứng không tồn tại".
            //
            // Mốc thời gian lấy từ T12 đo trên video eFootball: bóng bay 11 m hết ~0.42 s.
            var cfg = KnuckleConfig.Default;
            float dt = 1f / 120f;
            int steps = (int)math.round(0.42f / dt);

            float tong = 0f, lonNhat = 0f;
            int soSeedDangKe = 0;
            float nguongDangKe = 0.4f * cfg.amplitude;

            for (uint seed = 0; seed < 500; seed++)
            {
                float dev = MoPhongDoLechLonNhat(cfg, seed, steps, dt);
                tong += dev;
                lonNhat = math.max(lonNhat, dev);
                if (dev >= nguongDangKe) soSeedDangKe++;
            }

            float trungBinh = tong / 500f;

            // Thủ môn với tay ~1 m; lệch dưới 5 cm thì không đổi được kết cục pha bóng nào cả.
            Assert.Greater(trungBinh, 0.05f,
                $"Độ lệch trung bình chỉ {trungBinh:F4} m — hiệu ứng knuckle gần như không tồn tại " +
                $"trong pha bay 0.42 s. (lớn nhất {lonNhat:F4} m, {soSeedDangKe}/500 seed đáng kể)");

            Assert.Greater(lonNhat, 0.5f * cfg.amplitude,
                $"Seed mạnh nhất chỉ lệch {lonNhat:F4} m, chưa tới nửa biên độ {cfg.amplitude} m — " +
                "bao hình đang mở quá chậm so với thời gian bay.");

            Assert.Greater(soSeedDangKe, 100,
                $"Chỉ {soSeedDangKe}/500 seed lệch quá {nguongDangKe:F3} m — hiệu ứng quá hiếm khi " +
                "đáng kể, người chơi sẽ không nhận ra cú knuckle khác gì cú thường.");
        }

        // ─── 17. Cửa tốc độ không được đóng giữa pha bay ───

        [Test]
        public void Knuckle_CuaTocDo_KhongTheDongGiuaPhaBay_VoiCauHinhMacDinh()
        {
            // Chứng minh chặn độ lệch chỉ đúng khi hiệu ứng chạy LIÊN TỤC từ t = 0. Nếu cửa
            // onsetSpeed đóng giữa chừng tại lúc d'(t) ≠ 0, phần vận tốc lệch còn lại không
            // bị triệt tiêu và bóng trôi ngang đều cho tới khung thành — độ lệch cuối vượt
            // maxLateralDeviation dù công thức không sai một dấu nào.
            //
            // Test này khoá điều kiện đó lại bằng SỐ THẬT của hai config, để lần sau ai hạ
            // maxSpeed, nâng knuckleMinPower hay nâng onsetSpeed thì đỏ ngay tại đây.
            var knuckle = KnuckleConfig.Default;
            var mapping = ShotMappingConfig.CreateDefault();
            try
            {
                // Cú Knuckle CHẬM NHẤT mà ShotMapper có thể xếp loại: speedT đúng bằng
                // knuckleMinPower (dưới mức đó Classify trả về Instep).
                float tocDoChamNhat = math.lerp(mapping.minSpeed, mapping.maxSpeed, mapping.knuckleMinPower);
                Assert.Greater(tocDoChamNhat, knuckle.onsetSpeed,
                    $"Cú knuckle chậm nhất rời chân ở {tocDoChamNhat} m/s, đã dưới onsetSpeed " +
                    $"{knuckle.onsetSpeed} m/s — hiệu ứng không bao giờ bật.");

                // Bay 0.6 s: dài hơn hẳn pha bay thật (~0.42 s), nên qua được đây là qua với dư.
                var cuoi = BallSolver.Integrate(
                    new BallState(float3.zero, new float3(0f, 2f, tocDoChamNhat), float3.zero),
                    BallParams.Default, totalTime: 0.6f, dt: 1f / 120f);

                float tocDoCuoi = math.length(cuoi.velocity);
                Assert.Greater(tocDoCuoi, knuckle.onsetSpeed,
                    $"Cuối pha bay cú knuckle chậm nhất chỉ còn {tocDoCuoi:F2} m/s, đã tụt dưới " +
                    $"onsetSpeed {knuckle.onsetSpeed} m/s. Cửa đóng giữa chừng thì chặn độ lệch " +
                    "trong KnuckleForce không còn hiệu lực — xem ghi chú 'cửa tốc độ' ở đó.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mapping);
            }
        }

        /// <summary>
        /// Tích phân riêng thành phần knuckle qua <paramref name="steps"/> bước và trả về độ
        /// lệch ngang LỚN NHẤT gặp phải. Cùng cách tích phân với test 500 seed ở trên.
        /// </summary>
        static float MoPhongDoLechLonNhat(in KnuckleConfig cfg, uint seed, int steps, float dt)
        {
            var s = FastStraightShot();
            float3 vel = float3.zero, pos = float3.zero;
            float maxDev = 0f;

            for (int i = 0; i < steps; i++)
            {
                vel += KnuckleForce.Evaluate(s, cfg, (i + 1) * dt, seed) * dt;
                pos += vel * dt;
                maxDev = math.max(maxDev, math.length(pos));
            }
            return maxDev;
        }
    }
}
