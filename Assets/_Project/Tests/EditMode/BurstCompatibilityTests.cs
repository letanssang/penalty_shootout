using System;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Unity.Burst;
using Unity.Mathematics;
using Eleven.Ball;
using Eleven.Shooter;

namespace Eleven.Tests.EditMode {

    /// <summary>
    /// T06 — ô "biên dịch được với [BurstCompile], không có cảnh báo Burst".
    ///
    /// VÌ SAO FILE NÀY TỒN TẠI. Ô đó trước giờ ghi "cần mở Burst Inspector bằng tay". Nhưng
    /// build Android AOT ngày 2026-08-27 cho thấy một chuyện tệ hơn là chưa kiểm: Burst
    /// **chưa từng biên dịch một dòng nào của dự án**. File
    /// `lib_burst_generated.txt` của bản build liệt kê đúng 99 hàm, tất cả thuộc
    /// Unity.Collections / Unity.Mathematics / URP / Splines — **không có một hàm `Eleven.*` nào**.
    ///
    /// Lý do nằm ở chính thiết kế đã chọn (và vẫn đúng): `[BurstCompile]` chỉ đặt ở CẤP LỚP,
    /// không đặt lên method — đặt lên method sẽ bật Direct Call và ABI của nó cấm trả struct
    /// theo giá trị (BC1064/BC1067), làm hỏng AOT lúc build player. Hệ quả là thân hàm chỉ được
    /// Burst biên dịch KHI CÓ JOB gọi vào và inline chúng. Job đó thuộc về T20 (thủ môn), chưa
    /// tồn tại. Nên "không có cảnh báo Burst" hiện là **đúng một cách rỗng**: không có cảnh báo
    /// vì không có gì được biên dịch.
    ///
    /// Cách lấp: ép Burst biên dịch thật các hàm này ngay bây giờ bằng
    /// <see cref="BurstCompiler.CompileFunctionPointer{T}"/>. Bất kỳ lỗi BC nào trong thân hàm
    /// sẽ nổ ra ở đây thay vì nổ lúc T20 dựng job đầu tiên — tức là sớm hơn vài tuần.
    ///
    /// Vẫn CÒN NỢ so với ô nghiệm thu gốc: đây là Burst JIT trong Editor (LLVM, kiến trúc máy
    /// chủ), không phải AOT ARM64. Hai đường dùng chung frontend nên gần như mọi lỗi BC lộ ra ở
    /// cả hai, nhưng lỗi riêng của backend ARM64 thì chỉ build player mới thấy — và chỉ thấy khi
    /// T20 có job thật. Xem lại ô này lúc đó.
    /// </summary>
    [TestFixture]
    public class BurstCompatibilityTests {

        // ─── Vỏ bọc để gọi được qua con trỏ hàm ────────────────────────
        //
        // Không gọi thẳng BallSolver.Step qua con trỏ hàm được: ABI của con trỏ hàm Burst cấm
        // TRẢ struct/vector theo giá trị, đúng cái ràng buộc đã ghi trong BallSolver.cs. Nên mọi
        // vỏ bọc dưới đây trả kết quả qua `ref`, còn giá trị trả về để cho kiểu vô hướng.
        //
        // Vỏ bọc nằm trong assembly TEST, không phải assembly sản phẩm: mục đích của nó thuần
        // tuý là ép Burst biên dịch thân hàm thật ở BallSolver/KnuckleForce. Thêm code sản phẩm
        // chỉ để phục vụ việc kiểm tra là cái giá không đáng trả — job thật sẽ đến ở T20.

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void StepDelegate(ref BallState s, in BallParams p, float dt);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate float DragDelegate(float speed, in BallParams p);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void KnuckleDelegate(in BallState s, in KnuckleConfig c,
                                             float elapsed, uint seed, ref float3 result);

        [BurstCompile]
        static class Probes {
            [BurstCompile]
            public static void Step(ref BallState s, in BallParams p, float dt) {
                s = BallSolver.Step(s, p, dt);
            }

            [BurstCompile]
            public static float Drag(float speed, in BallParams p) {
                return BallSolver.DragCoefficient(speed, p);
            }

            [BurstCompile]
            public static void Knuckle(in BallState s, in KnuckleConfig c,
                                       float elapsed, uint seed, ref float3 result) {
                result = KnuckleForce.Evaluate(s, c, elapsed, seed);
            }
        }

        /// <summary>
        /// Burst tắt (người dùng bỏ tick "Enable Burst Compilation", hoặc chạy trên nền không
        /// hỗ trợ) thì <c>CompileFunctionPointer</c> lặng lẽ trả về bản managed. Test vẫn xanh
        /// nhưng chẳng chứng minh gì — nên thà bỏ qua có ghi lý do còn hơn xanh dối.
        /// </summary>
        bool _dongBoCu;

        [SetUp]
        public void BoQuaNeuBurstTat() {
            if (!BurstCompiler.IsEnabled)
                Assert.Ignore("Burst đang TẮT trong Editor — con trỏ hàm sẽ rơi về managed nên " +
                              "test này không chứng minh được gì. Bật lại ở Jobs > Burst > Enable Compilation.");

            // BẮT BUỘC, đây là chỗ quyết định test này có nghĩa hay không.
            // Mặc định Burst biên dịch BẤT ĐỒNG BỘ: CompileFunctionPointer trả về ngay một
            // stub managed rồi mới tráo mã đã biên dịch vào sau. Để nguyên vậy thì Invoke rất
            // có thể chạy bản managed, mọi assert vẫn xanh, và test chẳng chứng minh được gì —
            // đúng kiểu "xanh dối" mà chính file này lập ra để chống. Dấu hiệu nhận ra:
            // cả bộ 4 test chạy hết 0.01 s, nhanh hơn một lần biên dịch Burst thật.
            //
            // Bật đồng bộ còn được thêm một thứ: lỗi BC (nếu có) được log NGAY trong lúc test
            // chạy, mà Unity Test Framework thì đánh trượt test khi có Debug.LogError bất ngờ.
            // Nói cách khác, lỗi Burst tự biến thành test đỏ, không cần assert riêng.
            _dongBoCu = BurstCompiler.Options.EnableBurstCompileSynchronously;
            BurstCompiler.Options.EnableBurstCompileSynchronously = true;
        }

        [TearDown]
        public void TraLaiCheDoBienDich() {
            // Trả về nguyên trạng: đây là cờ TOÀN CỤC của Editor, để sót lại thì mọi lần
            // vào Play Mode sau đó đều khựng chờ Burst biên dịch xong.
            if (BurstCompiler.IsEnabled)
                BurstCompiler.Options.EnableBurstCompileSynchronously = _dongBoCu;
        }

        static BallState MauTrangThai() => new BallState(
            float3.zero, new float3(3f, 8f, 28f), new float3(10f, -20f, 5f));

        // ─── 1. Thân hàm solver qua được Burst ────────────────────────

        [Test]
        public void BallSolverStep_BurstBienDichDuoc_VaCungKetQuaVoiManaged() {
            var fp = BurstCompiler.CompileFunctionPointer<StepDelegate>(Probes.Step);
            Assert.IsTrue(fp.IsCreated, "Burst không dựng nổi con trỏ hàm cho BallSolver.Step");

            var p = BallParams.Default;
            float dt = 1f / 120f;

            var qua_burst = MauTrangThai();
            fp.Invoke(ref qua_burst, p, dt);

            var qua_managed = BallSolver.Step(MauTrangThai(), p, dt);

            // Chỉ đòi khớp tới 1e-5 chứ không đòi từng bit: Burst dùng LLVM còn Editor dùng
            // Mono, hai backend có quyền gộp phép nhân-cộng (FMA) khác nhau. Tính TẤT ĐỊNH mà
            // dự án cần là "cùng một backend cho cùng kết quả" — điều đó đã được
            // BallSolverTests khoá bằng so sánh từng bit. Ở đây chỉ cần biết Burst không làm
            // lệch quỹ đạo đi đâu cả.
            Assert.AreEqual(qua_managed.position.x, qua_burst.position.x, 1e-5f);
            Assert.AreEqual(qua_managed.position.y, qua_burst.position.y, 1e-5f);
            Assert.AreEqual(qua_managed.position.z, qua_burst.position.z, 1e-5f);
            Assert.AreEqual(qua_managed.velocity.x, qua_burst.velocity.x, 1e-5f);
            Assert.AreEqual(qua_managed.velocity.y, qua_burst.velocity.y, 1e-5f);
            Assert.AreEqual(qua_managed.velocity.z, qua_burst.velocity.z, 1e-5f);
        }

        [Test]
        public void DragCoefficient_BurstBienDichDuoc_VaCungKetQuaVoiManaged() {
            var fp = BurstCompiler.CompileFunctionPointer<DragDelegate>(Probes.Drag);
            Assert.IsTrue(fp.IsCreated, "Burst không dựng nổi con trỏ hàm cho DragCoefficient");

            var p = BallParams.Default;
            // Quét qua cả ba vùng: hằng dưới, đoạn nội suy smoothstep, hằng trên.
            foreach (float v in new[] { 0f, 5f, 12f, 14f, 16f, 20f, 28f, 40f })
                Assert.AreEqual(BallSolver.DragCoefficient(v, p), fp.Invoke(v, p), 1e-6f,
                    $"Burst và managed lệch nhau ở tốc độ {v} m/s");
        }

        // ─── 2. Thân hàm knuckle qua được Burst ───────────────────────

        [Test]
        public void KnuckleForce_BurstBienDichDuoc_VaCungKetQuaVoiManaged() {
            var fp = BurstCompiler.CompileFunctionPointer<KnuckleDelegate>(Probes.Knuckle);
            Assert.IsTrue(fp.IsCreated, "Burst không dựng nổi con trỏ hàm cho KnuckleForce.Evaluate");

            // Bóng không xoáy, đủ nhanh — nếu không thì hàm thoát sớm và chẳng biên dịch tới
            // phần toán nào cả, test thành ra rỗng.
            var s = new BallState(float3.zero, new float3(0f, 2f, 28f), float3.zero);
            var c = KnuckleConfig.Default;

            for (uint seed = 0; seed < 8; seed++) {
                float3 qua_burst = float3.zero;
                fp.Invoke(s, c, 0.25f, seed, ref qua_burst);
                float3 qua_managed = KnuckleForce.Evaluate(s, c, 0.25f, seed);

                Assert.AreEqual(qua_managed.x, qua_burst.x, 1e-4f, $"seed {seed} lệch trục X");
                Assert.AreEqual(qua_managed.y, qua_burst.y, 1e-4f, $"seed {seed} lệch trục Y");
                Assert.AreEqual(qua_managed.z, qua_burst.z, 1e-4f, $"seed {seed} lệch trục Z");

                Assert.IsFalse(math.any(math.isnan(qua_burst)), $"seed {seed}: Burst cho ra NaN");
            }
        }

        // ─── 3. Chạy dài qua Burst vẫn ra cùng quỹ đạo ────────────────

        [Test]
        public void ChayHetPhaBayQuaBurst_KhopVoiManaged_TrongMotMiliMet() {
            // Một bước khớp nhau chưa nói lên gì: sai số của backend tích luỹ qua các bước.
            // Chạy đủ 0.42 s (đúng pha bay thật) rồi mới so — nếu Burst làm lệch, chỗ này lộ.
            var fp = BurstCompiler.CompileFunctionPointer<StepDelegate>(Probes.Step);
            var p = BallParams.Default;
            float dt = 1f / 120f;
            int steps = 50;

            var qua_burst = MauTrangThai();
            var qua_managed = MauTrangThai();
            for (int i = 0; i < steps; i++) {
                fp.Invoke(ref qua_burst, p, dt);
                qua_managed = BallSolver.Step(qua_managed, p, dt);
            }

            float lech = math.distance(qua_burst.position, qua_managed.position);
            Assert.Less(lech, 1e-3f,
                $"Sau {steps} bước, quỹ đạo qua Burst lệch quỹ đạo managed {lech:E3} m " +
                $"(burst={qua_burst.position} managed={qua_managed.position})");
        }
    }
}
