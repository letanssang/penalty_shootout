using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Eleven.Ball;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Eleven.Tests.EditMode
{
    // GHI CHÚ về mục "Dự đoán 0.5s ở dt 1/120 mất dưới 0.05ms".
    //
    // Con số 0.05ms TRÊN THIẾT BỊ thì đúng là phải đo trên thiết bị, và không có mẹo nào
    // lách được: máy Mac chạy Editor và điện thoại chạy Burst AOT lệch nhau theo HAI chiều
    // ngược nhau — Editor (Mono, không AOT) chậm hơn, nhưng CPU Mac lại nhanh hơn CPU điện
    // thoại rất nhiều. Hai hiệu ứng đó không triệt tiêu nhau theo một hướng biết trước, nên
    // "nhanh trên Mac" KHÔNG suy ra "nhanh trên máy" và ngược lại. Đây là lý do đúng, khác
    // với ghi chú cũ chỉ nói chung chung "timing ở Editor không phản ánh Burst AOT".
    //
    // Nhưng phần LỚN rủi ro thì kiểm được tự động, vì thứ hay làm hỏng perf không phải là
    // hằng số nhân của CPU mà là KHỐI LƯỢNG CÔNG VIỆC: ai đó thêm sub-step, đổi vòng lặp
    // thành O(n²), hay cấp phát trong vòng. Cả ba đều lộ ra ở số bước, và số bước thì tất
    // định trên mọi máy. Xem Predict_NuaGiay_Dung61Mau_SoBuocLaTatDinh bên dưới.
    // Phần còn lại cho người đo: đúng MỘT con số trên build thật.

    [TestFixture]
    public class TrajectoryPredictorTests
    {
        static BallParams NoForceParams()
        {
            var p = BallParams.Default;
            p.airDensity = 0f;
            p.gravity = 0f;
            p.liftCoefficient = 0f;
            p.spinDecayPerSecond = 0f;
            p.cdLow = 0f;
            p.cdHigh = 0f;
            return p;
        }

        // ─── 1. Predict cấp phát 0 byte ───

        [Test]
        public void Predict_KhongCapPhat()
        {
            var p = BallParams.Default;
            var s = new BallState
            {
                position = float3.zero,
                velocity = new float3(0f, 5f, 28f),
                spin = float3.zero
            };
            float dt = 1f / 120f;
            float maxTime = 0.5f;

            using var buffer = new NativeArray<TrajectorySample>(128, Allocator.Temp);

            Assert.That(() =>
            {
                TrajectoryPredictor.Predict(s, p, dt, maxTime, buffer);
            }, Is.Not.AllocatingGCMemory());
        }

        // ─── 2. Buffer nhỏ hơn số mẫu cần: dừng đúng lúc đầy, không tràn ───

        [Test]
        public void Predict_BufferNho_DungDungLucDay_KhongTran()
        {
            var p = BallParams.Default;
            var s = new BallState
            {
                position = float3.zero,
                velocity = new float3(0f, 5f, 28f),
                spin = float3.zero
            };
            float dt = 1f / 120f;
            float maxTime = 1f; // cần 121 mẫu nếu buffer đủ lớn

            using var buffer = new NativeArray<TrajectorySample>(5, Allocator.Temp);

            int written = TrajectoryPredictor.Predict(s, p, dt, maxTime, buffer);

            Assert.AreEqual(5, written, "Buffer chỉ có 5 chỗ, phải dừng đúng lúc đầy");

            for (int i = 0; i < written; i++)
                Assert.AreEqual(i * dt, buffer[i].time, 1e-5f, $"Mẫu {i}: thời gian phải là {i * dt}");
        }

        // ─── 3. Điểm cuối của Predict trùng BallSolver.Integrate cùng tham số ───

        [Test]
        public void Predict_DiemCuoi_TrungVoiIntegrate()
        {
            var p = BallParams.Default;
            var s = new BallState
            {
                position = new float3(0f, 1f, 0f),
                velocity = new float3(3f, 8f, 28f),
                spin = new float3(10f, -20f, 5f)
            };
            float dt = 1f / 120f;
            int steps = 60;
            float maxTime = steps * dt; // chia hết cho dt

            using var buffer = new NativeArray<TrajectorySample>(steps + 1, Allocator.Temp);

            int written = TrajectoryPredictor.Predict(s, p, dt, maxTime, buffer);
            Assert.AreEqual(steps + 1, written);

            var lastSample = buffer[written - 1];
            var integrated = BallSolver.Integrate(s, p, maxTime, dt);

            Assert.Less(math.distance(lastSample.position, integrated.position), 1e-4f,
                $"Điểm cuối Predict ({lastSample.position}) phải trùng Integrate ({integrated.position}) sai số dưới 1e-4");
        }

        // ─── 4. FirstCrossing nội suy tuyến tính chính xác, không phải mẫu gần nhất ───

        [Test]
        public void FirstCrossing_NoiSuyTuyenTinh_ChinhXac()
        {
            // Không lực nào tác dụng, vận tốc hằng theo Z → RK4 tích phân tuyệt đối chính xác,
            // nên nội suy tuyến tính giữa hai mẫu phải khớp đúng điểm cắt phân tích.
            var p = NoForceParams();
            var s = new BallState
            {
                position = new float3(0f, 1f, -1f),
                velocity = new float3(0f, 0f, 10f), // 10 m/s theo Z
                spin = float3.zero
            };
            float dt = 0.03f; // không chia hết 0.1 → buộc phải nội suy, không rơi đúng vào mẫu

            bool crossed = TrajectoryPredictor.FirstCrossing(s, p, planeZ: 0f, dt: dt,
                out float3 point, out float time);

            Assert.IsTrue(crossed, "Bóng bay thẳng qua z=0 phải được phát hiện");
            // Phân tích: z(t) = -1 + 10*t = 0 → t = 0.1s đúng
            Assert.AreEqual(0.1f, time, 1e-4f,
                $"Thời gian cắt phải khớp phân tích (0.1s), nhận được {time} — nếu sai đây là dấu hiệu code trả mẫu gần nhất thay vì nội suy");
            Assert.AreEqual(0f, point.z, 1e-4f, "Điểm cắt phải có z = 0 (mặt phẳng)");
            Assert.AreEqual(1f, point.y, 1e-4f, "Y không đổi vì không có lực nào tác dụng");
        }

        // ─── 5. Bóng không bao giờ tới mặt phẳng → trả false, không treo vòng lặp ───

        [Test]
        public void FirstCrossing_KhongBaoGioToiMatPhang_TraFalse()
        {
            var p = NoForceParams();
            var s = new BallState
            {
                position = new float3(0f, 1f, -1f),
                velocity = new float3(0f, 0f, -10f), // bay ngược, xa mặt phẳng z=0 mãi
                spin = float3.zero
            };
            float dt = 1f / 120f;

            bool crossed = TrajectoryPredictor.FirstCrossing(s, p, planeZ: 0f, dt: dt,
                out float3 point, out float time);

            Assert.IsFalse(crossed, "Bóng bay ngược hướng, không bao giờ tới mặt phẳng → phải trả false");
        }

        // ─── 6. Predict với maxTime/dt không hợp lệ không NaN, không treo ───

        [Test]
        public void Predict_ThamSoBien_KhongNaN()
        {
            var p = BallParams.Default;
            var s = new BallState { position = float3.zero, velocity = new float3(0f, 0f, 10f), spin = float3.zero };

            using var buffer = new NativeArray<TrajectorySample>(10, Allocator.Temp);

            Assert.AreEqual(0, TrajectoryPredictor.Predict(s, p, dt: 0f, maxTime: 1f, buffer),
                "dt = 0 phải trả 0 mẫu, không chia cho 0");
            Assert.AreEqual(0, TrajectoryPredictor.Predict(s, p, dt: 1f / 120f, maxTime: 0f, buffer),
                "maxTime = 0 phải trả 0 mẫu");

            using var emptyBuffer = new NativeArray<TrajectorySample>(0, Allocator.Temp);
            Assert.AreEqual(0, TrajectoryPredictor.Predict(s, p, dt: 1f / 120f, maxTime: 1f, emptyBuffer),
                "Buffer rỗng phải trả 0, không index ngoài biên");
        }

        // ─── 7. Khối lượng công việc của kịch bản perf là tất định ───

        /// <summary>
        /// Chốt cứng SỐ BƯỚC của đúng kịch bản trong ô nghiệm thu perf (0.5s ở dt 1/120).
        ///
        /// Đây là nửa kiểm được của ô đó. Nếu ai sửa Predict thành chạy sub-step cho ổn định
        /// hơn, hay đổi điều kiện dừng, thời gian chạy sẽ nhân lên tương ứng — và test này đỏ
        /// ngay trên máy build, không phải đợi có người cầm điện thoại đo lại. Ngược lại, khi
        /// test này xanh thì con số đo trên thiết bị hôm nay vẫn còn giá trị cho ngày mai:
        /// cùng số bước, cùng khối lượng việc.
        /// </summary>
        [Test]
        public void Predict_NuaGiay_Dung61Mau_SoBuocLaTatDinh()
        {
            var p = BallParams.Default;
            var s = new BallState
            {
                position = float3.zero,
                velocity = new float3(3f, 8f, 28f),
                spin = new float3(10f, -20f, 5f)
            };
            const float dt = 1f / 120f;
            const float maxTime = 0.5f;

            using var buffer = new NativeArray<TrajectorySample>(256, Allocator.Temp);
            int written = TrajectoryPredictor.Predict(s, p, dt, maxTime, buffer);

            // 0.5s / (1/120) = 60 bước tích phân, cộng mẫu ở t = 0 là 61 mẫu.
            Assert.AreEqual(61, written,
                $"Kịch bản perf phải là đúng 61 mẫu (60 bước + mẫu gốc), đang là {written}. " +
                "Số bước đổi nghĩa là khối lượng việc đổi — con số 0.05ms đo trên thiết bị " +
                "không còn nói về cùng một phép tính nữa.");

            // Đo và IN RA, không assert ngưỡng tuyệt đối: xem ghi chú đầu file về việc vì sao
            // ngưỡng ms trên máy này không nói được gì về máy kia. Con số dưới đây chỉ để đối
            // chiếu tương đối giữa hai lần chạy trên CÙNG một máy.
            for (int i = 0; i < 50; i++) TrajectoryPredictor.Predict(s, p, dt, maxTime, buffer); // làm nóng JIT

            const int soLan = 2000;
            var dongHo = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < soLan; i++) TrajectoryPredictor.Predict(s, p, dt, maxTime, buffer);
            dongHo.Stop();

            double msMoiLan = dongHo.Elapsed.TotalMilliseconds / soLan;
            TestContext.WriteLine(
                $"[T08 THAM CHIEU] Predict(0.5s, dt=1/120) = {written} mẫu, " +
                $"{msMoiLan:F4} ms/lần trong Editor trên máy này ({soLan} lần đo). " +
                "Ngưỡng nghiệm thu 0.05 ms là ngưỡng TRÊN THIẾT BỊ — con số này không thay thế nó.");
        }
    }
}
