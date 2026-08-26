using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Eleven.Ball;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Eleven.Tests.EditMode
{
    // GHI CHÚ: Mục "Dự đoán 0.5s ở dt 1/120 mất dưới 0.05ms" KHÔNG kiểm được đáng tin
    // trong EditMode — timing ở Editor không phản ánh Burst AOT trên thiết bị (xem
    // cảnh báo tương tự trong PerfHud.Sampler). CẦN NGƯỜI KIỂM đo lại bằng
    // PerfHud.BeginCapture trên build thật.

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
    }
}
