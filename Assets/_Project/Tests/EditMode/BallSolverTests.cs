using NUnit.Framework;
using UnityEngine.TestTools.Constraints;   // gop extension AllocatingGCMemory()
using Is = NUnit.Framework.Is;             // khu nhap nhang voi lop Is cua Unity
using Unity.Mathematics;
using Eleven.Ball;

namespace Eleven.Tests.EditMode
{
    // GHI CHÚ: Mục "kết quả giống nhau giữa Editor và build IL2CPP trên thiết bị"
    // KHÔNG THỂ tự động kiểm trong EditMode. Test GoldenHash_QuyDao_DeThuCongDoiChieuTrenThietBi
    // sẽ in ra hash quỹ đạo. CẦN NGƯỜI KIỂM chạy test tương đương trên build thật
    // và đối chiếu giá trị hash khớp từng bit.

    [TestFixture]
    public class BallSolverTests
    {
        // ─── Hàm tiện ích ─────────────────────────────────────────────

        /// <summary>So sánh từng bit hai float3, không dùng dung sai.</summary>
        static void AssertBitExact(float3 expected, float3 actual, string msg)
        {
            Assert.AreEqual(math.asuint(expected.x), math.asuint(actual.x),
                $"{msg} — thành phần X sai bit (expected bits {math.asuint(expected.x)}, actual {math.asuint(actual.x)})");
            Assert.AreEqual(math.asuint(expected.y), math.asuint(actual.y),
                $"{msg} — thành phần Y sai bit (expected bits {math.asuint(expected.y)}, actual {math.asuint(actual.y)})");
            Assert.AreEqual(math.asuint(expected.z), math.asuint(actual.z),
                $"{msg} — thành phần Z sai bit (expected bits {math.asuint(expected.z)}, actual {math.asuint(actual.z)})");
        }

        static void AssertBitExactState(BallState expected, BallState actual, string msg)
        {
            AssertBitExact(expected.position, actual.position, $"{msg} — position");
            AssertBitExact(expected.velocity, actual.velocity, $"{msg} — velocity");
            AssertBitExact(expected.spin, actual.spin, $"{msg} — spin");
        }

        static bool HasNanOrInf(float3 v)
        {
            return math.any(math.isnan(v)) || math.any(math.isinf(v));
        }

        static BallParams DefaultParams()
        {
            return BallParams.Default;
        }

        /// <summary>Tạo BallParams không cản, không trọng lực, không spin decay — chỉ giữ nguyên động năng.</summary>
        static BallParams NoForceParams()
        {
            var p = DefaultParams();
            p.airDensity = 0f;
            p.gravity = 0f;
            p.liftCoefficient = 0f;
            p.spinDecayPerSecond = 0f;
            p.cdLow = 0f;
            p.cdHigh = 0f;
            return p;
        }

        /// <summary>Hash đơn giản cho quỹ đạo: XOR tất cả bit uint của position, velocity qua mỗi bước.</summary>
        static uint HashTrajectory(BallState initial, BallParams p, int steps, float dt)
        {
            uint h = 0;
            var s = initial;
            for (int i = 0; i < steps; i++)
            {
                s = BallSolver.Step(s, p, dt);
                h ^= math.asuint(s.position.x) + (uint)i * 2654435761u;
                h ^= math.asuint(s.position.y) + (uint)i * 2246822519u;
                h ^= math.asuint(s.position.z) + (uint)i * 3266489917u;
                h ^= math.asuint(s.velocity.x);
                h ^= math.asuint(s.velocity.y);
                h ^= math.asuint(s.velocity.z);
            }
            return h;
        }

        // ─── 1. Tất định từng bit: cùng input hai lần cho ra kết quả giống nhau ───

        [Test]
        public void Step_CungInput_HaiLan_KetQuaGiongTungBit()
        {
            // Chạy cùng input hai lần, kết quả phải giống từng bit
            var p = DefaultParams();
            var s = new BallState
            {
                position = new float3(0f, 1f, 0f),
                velocity = new float3(5f, 10f, 28f),
                spin = new float3(0f, 30f, 0f)
            };
            float dt = 1f / 120f;

            var r1 = BallSolver.Step(s, p, dt);
            var r2 = BallSolver.Step(s, p, dt);

            AssertBitExactState(r1, r2, "Step cùng input phải cho kết quả giống từng bit");
        }

        [Test]
        public void Integrate_CungInput_HaiLan_KetQuaGiongTungBit()
        {
            var p = DefaultParams();
            var s = new BallState
            {
                position = float3.zero,
                velocity = new float3(3f, 15f, 25f),
                spin = new float3(10f, 0f, -5f)
            };
            float dt = 1f / 200f;
            float totalTime = 0.5f;

            var r1 = BallSolver.Integrate(s, p, totalTime, dt);
            var r2 = BallSolver.Integrate(s, p, totalTime, dt);

            AssertBitExactState(r1, r2, "Integrate cùng input phải cho kết quả giống từng bit");
        }

        // ─── 2. Integrate phải trùng từng bit với Step lặp ───

        [Test]
        public void Integrate_TrungTungBit_VoiStepLap()
        {
            // Integrate(s, p, totalTime, dt) phải cho kết quả TRÙNG TỪNG BIT
            // với việc gọi Step lặp đúng số bước tương ứng
            var p = DefaultParams();
            var s = new BallState
            {
                position = new float3(0f, 0.5f, 0f),
                velocity = new float3(2f, 12f, 26f),
                spin = new float3(-5f, 10f, 3f)
            };
            float dt = 1f / 120f;
            float totalTime = 0.5f;
            int steps = (int)(totalTime / dt);
            // Đảm bảo totalTime chia hết cho dt để so sánh chính xác
            totalTime = steps * dt;

            var fromIntegrate = BallSolver.Integrate(s, p, totalTime, dt);

            var manual = s;
            for (int i = 0; i < steps; i++)
                manual = BallSolver.Step(manual, p, dt);

            AssertBitExactState(manual, fromIntegrate,
                "Integrate phải trùng từng bit với vòng lặp Step thủ công");
        }

        // ─── 3. Bảo toàn năng lượng: không cản, không xoáy, không trọng lực → tốc độ không đổi ───

        [Test]
        public void BaoToanNangLuong_KhongLuc_TocDoKhongDoi_1000Buoc()
        {
            var p = NoForceParams();
            var s = new BallState
            {
                position = float3.zero,
                velocity = new float3(10f, 5f, 20f),
                spin = float3.zero
            };
            float dt = 1f / 120f;
            float speedBanDau = math.length(s.velocity);

            var current = s;
            for (int i = 0; i < 1000; i++)
            {
                current = BallSolver.Step(current, p, dt);
                float speed = math.length(current.velocity);
                // Không cản, không trọng lực, không xoáy → tốc độ phải bằng chính xác ban đầu
                Assert.AreEqual(speedBanDau, speed, 1e-4f,
                    $"Bước {i}: tốc độ phải không đổi khi không có lực nào tác dụng");
            }
        }

        // ─── 4. Đối xứng: xoáy trái và xoáy phải cho độ lệch bằng nhau, ngược dấu ───

        [Test]
        public void DoiXung_XoayTraiPhai_DoLechBangNhau_NguocDau()
        {
            var p = DefaultParams();
            float dt = 1f / 200f;
            float totalTime = 0.45f;

            // Xoáy quanh trục +Y (lệch ngang do Magnus)
            float spinMag = 50f;

            var sLeft = new BallState
            {
                position = float3.zero,
                velocity = new float3(0f, 5f, 28f),
                spin = new float3(0f, spinMag, 0f) // xoáy trái
            };
            var sRight = new BallState
            {
                position = float3.zero,
                velocity = new float3(0f, 5f, 28f),
                spin = new float3(0f, -spinMag, 0f) // xoáy phải
            };

            var rLeft = BallSolver.Integrate(sLeft, p, totalTime, dt);
            var rRight = BallSolver.Integrate(sRight, p, totalTime, dt);

            // Thành phần Z và Y phải giống nhau (đối xứng không ảnh hưởng)
            Assert.AreEqual(rLeft.position.z, rRight.position.z, 1e-4f,
                "Xoáy trái/phải cùng độ lớn: Z phải bằng nhau");
            Assert.AreEqual(rLeft.position.y, rRight.position.y, 1e-4f,
                "Xoáy trái/phải cùng độ lớn: Y phải bằng nhau");

            // Thành phần X phải ngược dấu và cùng độ lớn
            Assert.AreEqual(rLeft.position.x, -rRight.position.x, 1e-4f,
                "Xoáy trái/phải cùng độ lớn: X phải ngược dấu và bằng nhau về giá trị tuyệt đối");

            // Độ lệch X phải khác 0 (Magnus thực sự tạo lệch)
            Assert.Greater(math.abs(rLeft.position.x), 0.01f,
                "Magnus phải tạo ra độ lệch X đáng kể khi có xoáy");
        }

        // ─── 5. Biên: vận tốc 0 ───

        [Test]
        public void Bien_VanTocKhong_KhongNaN_KhongInfinity()
        {
            var p = DefaultParams();
            var s = new BallState
            {
                position = new float3(0f, 1f, 0f),
                velocity = float3.zero,
                spin = float3.zero
            };
            float dt = 1f / 120f;

            var r = BallSolver.Step(s, p, dt);
            Assert.IsFalse(HasNanOrInf(r.position), "Vận tốc 0: position không được NaN/Inf");
            Assert.IsFalse(HasNanOrInf(r.velocity), "Vận tốc 0: velocity không được NaN/Inf");
            Assert.IsFalse(HasNanOrInf(r.spin), "Vận tốc 0: spin không được NaN/Inf");
        }

        // ─── 6. Biên: xoáy cực lớn ───

        [Test]
        public void Bien_XoayCucLon_KhongNaN_KhongInfinity()
        {
            var p = DefaultParams();
            var s = new BallState
            {
                position = float3.zero,
                velocity = new float3(0f, 5f, 20f),
                spin = new float3(10000f, 10000f, 10000f) // xoáy cực lớn
            };
            float dt = 1f / 120f;

            for (int i = 0; i < 100; i++)
            {
                s = BallSolver.Step(s, p, dt);
                Assert.IsFalse(HasNanOrInf(s.position),
                    $"Bước {i}: xoáy cực lớn, position không được NaN/Inf");
                Assert.IsFalse(HasNanOrInf(s.velocity),
                    $"Bước {i}: xoáy cực lớn, velocity không được NaN/Inf");
                Assert.IsFalse(HasNanOrInf(s.spin),
                    $"Bước {i}: xoáy cực lớn, spin không được NaN/Inf");
            }
        }

        // ─── 7. Biên: dt cực nhỏ ───

        [Test]
        public void Bien_DtCucNho_KhongNaN_KhongInfinity()
        {
            var p = DefaultParams();
            var s = new BallState
            {
                position = float3.zero,
                velocity = new float3(3f, 8f, 25f),
                spin = new float3(10f, -20f, 5f)
            };
            float dt = 1e-7f; // dt cực nhỏ

            for (int i = 0; i < 100; i++)
            {
                s = BallSolver.Step(s, p, dt);
                Assert.IsFalse(HasNanOrInf(s.position),
                    $"Bước {i}: dt cực nhỏ, position không được NaN/Inf");
                Assert.IsFalse(HasNanOrInf(s.velocity),
                    $"Bước {i}: dt cực nhỏ, velocity không được NaN/Inf");
                Assert.IsFalse(HasNanOrInf(s.spin),
                    $"Bước {i}: dt cực nhỏ, spin không được NaN/Inf");
            }
        }

        // ─── 8. Sút thẳng 28 m/s không xoáy: bay 11 m trong 0.40–0.48 s, rơi 0.75–0.95 m ───

        [Test]
        public void SutThang28_Bay11m_ThoiGianVaDoRoiDung()
        {
            var p = DefaultParams();
            var s = new BallState
            {
                position = float3.zero,
                velocity = new float3(0f, 0f, 28f), // sút thẳng theo +Z
                spin = float3.zero
            };
            float dt = 1f / 1000f; // dt nhỏ cho chính xác
            float totalTime = 0f;
            bool datMuc = false;

            var current = s;
            // Mô phỏng tối đa 1 giây
            for (int i = 0; i < 1000; i++)
            {
                current = BallSolver.Step(current, p, dt);
                totalTime += dt;

                if (current.position.z >= 11f)
                {
                    datMuc = true;
                    break;
                }
            }

            Assert.IsTrue(datMuc, "Bóng phải bay được 11 m trong 1 giây mô phỏng");
            Assert.GreaterOrEqual(totalTime, 0.40f,
                $"Thời gian bay 11 m = {totalTime}s, phải >= 0.40s");
            Assert.LessOrEqual(totalTime, 0.48f,
                $"Thời gian bay 11 m = {totalTime}s, phải <= 0.48s");

            // Rơi: position.y phải âm (rơi xuống), kiểm tra độ rơi
            float doRoi = -current.position.y;
            Assert.GreaterOrEqual(doRoi, 0.75f,
                $"Độ rơi = {doRoi}m, phải >= 0.75m");
            Assert.LessOrEqual(doRoi, 0.95f,
                $"Độ rơi = {doRoi}m, phải <= 0.95m");
        }

        // ─── 9. Spin bằng 0 thì lực Magnus đúng bằng 0 — quỹ đạo nằm trong mặt phẳng sút ───

        [Test]
        public void SpinKhong_QuiDaoNamTrongMatPhangSut()
        {
            // Sút trong mặt phẳng YZ (velocity chỉ có Y và Z), spin = 0
            // → không có lực nào đẩy ra khỏi mặt phẳng YZ → X phải luôn = 0
            var p = DefaultParams();
            var s = new BallState
            {
                position = float3.zero,
                velocity = new float3(0f, 5f, 28f),
                spin = float3.zero
            };
            float dt = 1f / 200f;

            var current = s;
            for (int i = 0; i < 200; i++)
            {
                current = BallSolver.Step(current, p, dt);

                // X phải đúng bằng 0 (không có lực nào theo X)
                Assert.AreEqual(0f, current.position.x, 1e-6f,
                    $"Bước {i}: spin=0, quỹ đạo phải nằm trong mặt phẳng sút, X phải = 0");
                Assert.IsFalse(HasNanOrInf(current.position), $"Bước {i}: không được NaN/Inf");
                Assert.IsFalse(HasNanOrInf(current.velocity), $"Bước {i}: không được NaN/Inf");
            }
        }

        // ─── 10. DragCoefficient: ngoài khoảng nội suy phải đúng bằng cdLow / cdHigh ───

        [Test]
        public void DragCoefficient_NgoaiKhoang_DungBangCdLowCdHigh()
        {
            var p = DefaultParams();

            // Dưới cdVLow (12 m/s) → Cd = cdLow (0.45)
            float cdAt5 = BallSolver.DragCoefficient(5f, p);
            Assert.AreEqual(p.cdLow, cdAt5, 1e-6f,
                $"Tốc độ 5 m/s (< cdVLow): Cd phải = cdLow = {p.cdLow}, nhận được {cdAt5}");

            float cdAt0 = BallSolver.DragCoefficient(0f, p);
            Assert.AreEqual(p.cdLow, cdAt0, 1e-6f,
                $"Tốc độ 0 m/s: Cd phải = cdLow = {p.cdLow}");

            float cdAt11 = BallSolver.DragCoefficient(11f, p);
            Assert.AreEqual(p.cdLow, cdAt11, 1e-6f,
                $"Tốc độ 11 m/s (< cdVLow=12): Cd phải = cdLow");

            // Trên cdVHigh (20 m/s) → Cd = cdHigh (0.22)
            float cdAt25 = BallSolver.DragCoefficient(25f, p);
            Assert.AreEqual(p.cdHigh, cdAt25, 1e-6f,
                $"Tốc độ 25 m/s (> cdVHigh): Cd phải = cdHigh = {p.cdHigh}, nhận được {cdAt25}");

            float cdAt50 = BallSolver.DragCoefficient(50f, p);
            Assert.AreEqual(p.cdHigh, cdAt50, 1e-6f,
                $"Tốc độ 50 m/s: Cd phải = cdHigh");

            float cdAt21 = BallSolver.DragCoefficient(21f, p);
            Assert.AreEqual(p.cdHigh, cdAt21, 1e-6f,
                $"Tốc độ 21 m/s (> cdVHigh=20): Cd phải = cdHigh");
        }

        // ─── 11. DragCoefficient: nội suy mượt, liên tục đạo hàm bậc nhất ───

        [Test]
        public void DragCoefficient_NoiSuyMuot_DaoHamKhongNhayBac()
        {
            var p = DefaultParams();

            // Kiểm tra nội suy liên tục bậc nhất bằng sai phân số
            // Lấy mẫu dày trong khoảng [11, 21] bao gồm vùng biên
            float epsilon = 0.001f;
            float step = 0.1f;

            // Trước tiên kiểm tra Cd tại các biên liên tục (không nhảy giá trị)
            float cdAtVLow = BallSolver.DragCoefficient(p.cdVLow, p);
            float cdJustBelow = BallSolver.DragCoefficient(p.cdVLow - epsilon, p);
            Assert.AreEqual(cdAtVLow, cdJustBelow, 0.01f,
                "Cd phải liên tục tại biên cdVLow");

            float cdAtVHigh = BallSolver.DragCoefficient(p.cdVHigh, p);
            float cdJustAbove = BallSolver.DragCoefficient(p.cdVHigh + epsilon, p);
            Assert.AreEqual(cdAtVHigh, cdJustAbove, 0.01f,
                "Cd phải liên tục tại biên cdVHigh");

            // Kiểm tra đạo hàm bậc nhất liên tục: sai phân bậc 2 phải nhỏ (không nhảy bậc)
            // d'(v) ≈ (Cd(v+eps) - Cd(v-eps)) / (2*eps)
            // Đạo hàm không nhảy bậc → d'(v) biến thiên mượt → sai phân bậc 2 nhỏ
            float prevDeriv = float.NaN;
            int sampleCount = 0;
            for (float v = p.cdVLow - 1f; v <= p.cdVHigh + 1f; v += step)
            {
                float cdPlus = BallSolver.DragCoefficient(v + epsilon, p);
                float cdMinus = BallSolver.DragCoefficient(v - epsilon, p);
                float deriv = (cdPlus - cdMinus) / (2f * epsilon);

                if (!float.IsNaN(prevDeriv))
                {
                    float derivChange = math.abs(deriv - prevDeriv);
                    // Cho phép biến thiên đạo hàm tối đa 0.5 trên mỗi bước 0.1 m/s
                    // Nếu nội suy mượt (Hermite/smoothstep), biến thiên sẽ nhỏ
                    Assert.Less(derivChange, 0.5f,
                        $"Tại v={v}: đạo hàm Cd nhảy quá lớn ({derivChange}), nội suy không mượt");
                }
                prevDeriv = deriv;
                sampleCount++;
            }

            Assert.Greater(sampleCount, 10, "Phải lấy đủ mẫu để kiểm tra nội suy");

            // Kiểm tra đạo hàm = 0 ngoài khoảng nội suy (hằng số)
            float derivOutsideLow = (BallSolver.DragCoefficient(5f + epsilon, p)
                                   - BallSolver.DragCoefficient(5f - epsilon, p)) / (2f * epsilon);
            Assert.AreEqual(0f, derivOutsideLow, 1e-3f,
                "Ngoài khoảng nội suy (dưới cdVLow): đạo hàm Cd phải = 0");

            float derivOutsideHigh = (BallSolver.DragCoefficient(30f + epsilon, p)
                                    - BallSolver.DragCoefficient(30f - epsilon, p)) / (2f * epsilon);
            Assert.AreEqual(0f, derivOutsideHigh, 1e-3f,
                "Ngoài khoảng nội suy (trên cdVHigh): đạo hàm Cd phải = 0");

            // Đạo hàm tại biên cdVLow và cdVHigh phải = 0 nếu liên tục bậc nhất
            // (smoothstep có đạo hàm = 0 tại hai đầu)
            float derivAtVLow = (BallSolver.DragCoefficient(p.cdVLow + epsilon, p)
                               - BallSolver.DragCoefficient(p.cdVLow - epsilon, p)) / (2f * epsilon);
            Assert.AreEqual(0f, derivAtVLow, 0.05f,
                "Đạo hàm Cd tại cdVLow phải gần 0 (liên tục bậc nhất với hằng số hai bên)");

            float derivAtVHigh = (BallSolver.DragCoefficient(p.cdVHigh + epsilon, p)
                                - BallSolver.DragCoefficient(p.cdVHigh - epsilon, p)) / (2f * epsilon);
            Assert.AreEqual(0f, derivAtVHigh, 0.05f,
                "Đạo hàm Cd tại cdVHigh phải gần 0 (liên tục bậc nhất với hằng số hai bên)");
        }

        // ─── 12. Golden hash quỹ đạo — để đối chiếu trên thiết bị ───

        [Test]
        public void GoldenHash_QuyDao_DeThuCongDoiChieuTrenThietBi()
        {
            // CẦN NGƯỜI KIỂM: Chạy test này trên build IL2CPP trên thiết bị thật
            // và đối chiếu giá trị hash in ra khớp từng bit với giá trị từ Editor.
            // Nếu khác → solver không tất định giữa Editor và build.
            var p = DefaultParams();
            var s = new BallState
            {
                position = float3.zero,
                velocity = new float3(3f, 8f, 28f),
                spin = new float3(10f, -20f, 5f)
            };
            float dt = 1f / 200f;
            int steps = 200; // 1 giây mô phỏng

            uint hash = HashTrajectory(s, p, steps, dt);

            // In ra golden hash để người kiểm đối chiếu trên thiết bị
            UnityEngine.Debug.Log($"[T07 GOLDEN HASH] hash quỹ đạo = {hash} — " +
                $"đối chiếu giá trị này trên build IL2CPP thiết bị thật");

            // Chạy lần 2 trong cùng Editor, hash phải giống từng bit
            uint hash2 = HashTrajectory(s, p, steps, dt);
            Assert.AreEqual(hash, hash2,
                "Hash quỹ đạo chạy hai lần trong cùng Editor phải giống từng bit");

            // Kiểm tra hash khác 0 (quỹ đạo phải có biến thiên)
            Assert.AreNotEqual(0u, hash, "Hash quỹ đạo không được = 0, bóng phải di chuyển");
        }

        // ─── 13. Trọng lực thuần: không cản, không xoáy → rơi tự do đúng công thức ───

        [Test]
        public void TrongLucThuan_RoiTuDo_DungCongThuc()
        {
            var p = DefaultParams();
            p.airDensity = 0f; // tắt cản và Magnus
            p.liftCoefficient = 0f;
            p.spinDecayPerSecond = 0f;

            var s = new BallState
            {
                position = new float3(0f, 100f, 0f), // cao để không chạm đất
                velocity = float3.zero,
                spin = float3.zero
            };
            float dt = 1f / 1000f;
            float totalTime = 1.0f;
            int steps = (int)(totalTime / dt);

            var result = s;
            for (int i = 0; i < steps; i++)
                result = BallSolver.Step(result, p, dt);

            // Rơi tự do: y = y0 - 0.5*g*t^2 = 100 - 0.5*9.81*1 = 95.095
            float expectedY = 100f - 0.5f * p.gravity * totalTime * totalTime;
            Assert.AreEqual(expectedY, result.position.y, 0.05f,
                $"Rơi tự do 1s: Y kỳ vọng ≈ {expectedY}, nhận được {result.position.y}");

            // Vận tốc Y: vy = -g*t = -9.81
            float expectedVy = -p.gravity * totalTime;
            Assert.AreEqual(expectedVy, result.velocity.y, 0.05f,
                $"Rơi tự do 1s: Vy kỳ vọng ≈ {expectedVy}, nhận được {result.velocity.y}");

            // X và Z phải = 0
            Assert.AreEqual(0f, result.position.x, 1e-6f, "Rơi tự do: X phải = 0");
            Assert.AreEqual(0f, result.position.z, 1e-6f, "Rơi tự do: Z phải = 0");
        }

        // ─── 14. DragCoefficient giá trị nội suy đúng khoảng giữa ───

        [Test]
        public void DragCoefficient_NoiSuy_GiaTriGiuaKhoang()
        {
            var p = DefaultParams();

            // Tại điểm giữa khoảng nội suy (16 m/s), Cd phải nằm giữa cdLow và cdHigh
            float cdMid = BallSolver.DragCoefficient(16f, p);
            Assert.Greater(cdMid, p.cdHigh,
                $"Cd tại 16 m/s phải > cdHigh ({p.cdHigh}), nhận được {cdMid}");
            Assert.Less(cdMid, p.cdLow,
                $"Cd tại 16 m/s phải < cdLow ({p.cdLow}), nhận được {cdMid}");

            // Cd phải đơn điệu giảm trong khoảng nội suy (cdLow > cdHigh)
            float prev = BallSolver.DragCoefficient(p.cdVLow, p);
            for (float v = p.cdVLow + 0.5f; v <= p.cdVHigh; v += 0.5f)
            {
                float cd = BallSolver.DragCoefficient(v, p);
                Assert.LessOrEqual(cd, prev + 1e-6f,
                    $"Cd phải đơn điệu giảm trong khoảng nội suy, tại v={v}");
                prev = cd;
            }
        }

        // ─── 15. Nhiều seed khác nhau — tất định với mọi input đa dạng ───

        [Test]
        public void TatDinh_NhieuInput_KhacNhau_DeuGiongTungBit()
        {
            var p = DefaultParams();
            var rng = new Unity.Mathematics.Random(42);
            float dt = 1f / 120f;

            for (int trial = 0; trial < 20; trial++)
            {
                var s = new BallState
                {
                    position = rng.NextFloat3(-10f, 10f),
                    velocity = rng.NextFloat3(-30f, 30f),
                    spin = rng.NextFloat3(-100f, 100f)
                };

                var r1 = BallSolver.Step(s, p, dt);
                var r2 = BallSolver.Step(s, p, dt);

                AssertBitExactState(r1, r2,
                    $"Trial {trial}: cùng input ngẫu nhiên phải cho kết quả giống từng bit");

                Assert.IsFalse(HasNanOrInf(r1.position),
                    $"Trial {trial}: position không được NaN/Inf");
                Assert.IsFalse(HasNanOrInf(r1.velocity),
                    $"Trial {trial}: velocity không được NaN/Inf");
                Assert.IsFalse(HasNanOrInf(r1.spin),
                    $"Trial {trial}: spin không được NaN/Inf");
            }
        }

        // ─── 16. Spin decay: spin phải giảm theo thời gian ───

        [Test]
        public void SpinDecay_SpinGiamTheoThoiGian()
        {
            var p = DefaultParams();
            // Chỉ kiểm spin decay, tắt các lực khác để đơn giản
            p.airDensity = 0f;
            p.gravity = 0f;

            var s = new BallState
            {
                position = float3.zero,
                velocity = new float3(0f, 0f, 20f),
                spin = new float3(0f, 100f, 0f)
            };
            float dt = 1f / 120f;

            float spinBanDau = math.length(s.spin);
            var current = s;
            for (int i = 0; i < 120; i++) // 1 giây
                current = BallSolver.Step(current, p, dt);

            float spinSau1s = math.length(current.spin);

            // Nếu spinDecayPerSecond > 0, spin phải giảm sau 1 giây
            if (p.spinDecayPerSecond > 0f)
            {
                Assert.Less(spinSau1s, spinBanDau,
                    $"Spin phải giảm sau 1s: ban đầu {spinBanDau}, sau {spinSau1s}");
                Assert.Greater(spinSau1s, 0f,
                    "Spin không nên giảm về 0 hoàn toàn sau chỉ 1 giây (trừ khi decay = 1)");
            }
        }

        // ─── 17. Cấp phát bộ nhớ bằng 0 — Step và Integrate đều là hàm thuần trên struct ───

        [Test]
        public void Step_KhongCapPhat()
        {
            var p = DefaultParams();
            var s = new BallState
            {
                position = float3.zero,
                velocity = new float3(3f, 8f, 25f),
                spin = new float3(10f, -20f, 5f)
            };
            float dt = 1f / 120f;

            TestDelegate action = () => BallSolver.Step(s, p, dt);
            action();

            Assert.That(action, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void Integrate_KhongCapPhat()
        {
            var p = DefaultParams();
            var s = new BallState
            {
                position = float3.zero,
                velocity = new float3(3f, 8f, 25f),
                spin = new float3(10f, -20f, 5f)
            };
            float dt = 1f / 120f;
            float totalTime = 0.5f;

            TestDelegate action = () => BallSolver.Integrate(s, p, totalTime, dt);
            action();

            Assert.That(action, Is.Not.AllocatingGCMemory());
        }
    }
}