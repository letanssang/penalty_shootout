using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Unity.Mathematics;
using Eleven.Core;
using Eleven.Presentation.Net;
using Is = NUnit.Framework.Is;

namespace Eleven.Tests.EditMode
{
    [TestFixture]
    public class NetSimulationTests
    {
        [Test]
        public void CauHinhLuoi_Duoi600Hat_VaVongLapDuoi8()
        {
            using (var sim = new NetSimulator())
            {
                Assert.LessOrEqual(sim.ParticleCount, NetGridGenerator.MaxAllowedParticles,
                    $"Tổng số hạt ({sim.ParticleCount}) vượt quá ngân sách {NetGridGenerator.MaxAllowedParticles} hạt.");
                Assert.Greater(sim.ParticleCount, 100, "Số lượng hạt quá ít, không đủ độ mịn để tạo hình lưới.");
                Assert.LessOrEqual(NetGridGenerator.DefaultIterations, 8, "Số vòng lặp vượt quá giới hạn 8.");
            }
        }

        [Test]
        public void BongKhongXuyenLuoi_O30mPerSecond_200CuSutNhieuGoc()
        {
            float ballRadius = 0.11f;
            float dt = 1f / 120f;
            int passCount = 0;

            var rng = new Unity.Mathematics.Random(20260827u);

            for (int kick = 0; kick < 200; kick++)
            {
                using (var sim = new NetSimulator())
                {
                    // Chọn điểm ngắm ngẫu nhiên trên mặt phẳng khung thành (z = 11.0m)
                    float targetX = rng.NextFloat(-3.2f, 3.2f);
                    float targetY = rng.NextFloat(0.3f, 2.2f);

                    float3 ballPos = new float3(targetX, targetY, 10.8f);
                    // Hướng bay vào trong lưới với vận tốc 30 m/s
                    float3 ballDir = math.normalize(new float3(rng.NextFloat(-0.15f, 0.15f), rng.NextFloat(-0.1f, 0.1f), 1.0f));
                    float3 ballVel = ballDir * 30.0f;

                    bool penetrated = false;

                    // Mô phỏng 60 bước va chạm (~0.5 giây)
                    for (int step = 0; step < 60; step++)
                    {
                        sim.StepWithBall(ref ballPos, ref ballVel, ballRadius, dt, 0.88f, 6);

                        // Ranh giới mặt sau của khung lưới tối đa là Z = 13.0m
                        // Nếu bóng vượt quá 13.0m nghĩa là đã xuyên thủng qua lưới
                        if (ballPos.z > 13.0f)
                        {
                            penetrated = true;
                            break;
                        }
                    }

                    Assert.IsFalse(penetrated,
                        $"Cú sút #{kick} ở vận tốc 30m/s tại ({targetX:F2}, {targetY:F2}) đã bắn XUYÊN qua mặt sau của lưới! Toạ độ cuối z={ballPos.z}");
                    passCount++;
                }
            }

            Assert.AreEqual(200, passCount, "Cần hoàn thành đủ 200 kịch bản thử nghiệm góc sút.");
        }

        [Test]
        public void LuoiOnDinhSau3Giay_KhongRungVinhVien()
        {
            using (var sim = new NetSimulator())
            {
                float dt = 1f / 60f;
                float ballRadius = 0.11f;

                // 1. Tác động một lực làm biến dạng căng lưới khi bóng bay vào
                float3 impactPos = new float3(0f, 1.22f, 11.5f);
                float3 impactVel = new float3(0f, 0f, 20f);

                for (int i = 0; i < 15; i++)
                {
                    impactPos += impactVel * dt;
                    sim.StepSynchronous(impactPos, impactVel, ballRadius, dt, 0.85f, 6);
                }

                float speedAfterImpact = sim.GetMaxParticleSpeed(dt);
                Assert.Greater(speedAfterImpact, 0.05f, "Lưới phải rung động sau va chạm.");

                // 2. Chạy tiếp 3 giây mô phỏng (~180 bước) không có bóng tiếp xúc
                float3 awayPos = new float3(0f, -50f, 0f);
                float3 awayVel = float3.zero;

                for (int step = 0; step < 180; step++)
                {
                    sim.StepSynchronous(awayPos, awayVel, ballRadius, dt, 0.85f, 6);
                }

                // 3. Khẳng định vận tốc cực đại của lưới đã triệt tiêu hoàn toàn (< 1 cm/s)
                float finalSpeed = sim.GetMaxParticleSpeed(dt);
                Assert.Less(finalSpeed, 0.01f,
                    $"Sau 3 giây, lưới vẫn tiếp tục dao động với vận tốc cực đại {finalSpeed:E3} m/s! " +
                    "Hệ quả: Lưới bị rung vĩnh viễn, không ổn định năng lượng.");
            }
        }

        [Test]
        public void TierProfile_TatDuocOBacC()
        {
            using (var sim = new NetSimulator())
            {
                float3 initialPos = sim.Particles[50].position;
                float dt = 1f / 60f;

                // Mô phỏng với cờ isSimulationEnabled = false (bậc C)
                sim.StepSynchronous(new float3(0f, 1.22f, 12.0f), new float3(0f, 0f, 25f), 0.11f, dt,
                                    0.85f, 6, isSimulationEnabled: false);

                float3 afterPos = sim.Particles[50].position;
                Assert.AreEqual(initialPos.x, afterPos.x, 1e-6f);
                Assert.AreEqual(initialPos.y, afterPos.y, 1e-6f);
                Assert.AreEqual(initialPos.z, afterPos.z, 1e-6f);
            }
        }

        [Test]
        public void NetStepJob_KhongCapPhatGC()
        {
            using (var sim = new NetSimulator())
            {
                float dt = 1f / 60f;
                float3 ballPos = new float3(0f, 1.22f, 12.0f);
                float3 ballVel = new float3(0f, 0f, 10f);

                // Warm-up JIT
                sim.StepSynchronous(ballPos, ballVel, 0.11f, dt, 0.85f, 6);

                Assert.That(() =>
                {
                    for (int i = 0; i < 50; i++)
                    {
                        sim.StepSynchronous(ballPos, ballVel, 0.11f, dt, 0.85f, 6);
                    }
                }, Is.Not.AllocatingGCMemory());
            }
        }
    }
}
