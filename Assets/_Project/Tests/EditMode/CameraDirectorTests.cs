using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Unity.Mathematics;
using Eleven.Match;
using Eleven.Presentation;
using Is = NUnit.Framework.Is;

namespace Eleven.Tests.EditMode
{
    [TestFixture]
    public class CameraDirectorTests
    {
        [Test]
        public void TatCaCameraShot_ViTriMacDinh_DeuNamTrongAuthoredBounds()
        {
            var director = new CameraDirector();

            foreach (CameraShot shot in Enum.GetValues(typeof(CameraShot)))
            {
                float3 pos = CameraAuthoredBounds.GetDefaultShotPosition(shot);
                bool within = director.IsWithinAuthoredBounds(pos);

                Assert.IsTrue(within,
                    $"Góc quay {shot} có toạ độ mặc định ({pos.x}, {pos.y}, {pos.z}) nằm NGOÀI ranh giới dựng 12m! " +
                    $"Hệ quả: Camera sẽ làm lộ mép sân bị cắt bỏ.");
            }
        }

        [Test]
        public void ReplayOrbit_KiemSoatGocQuayCung_KhongChoXoayTuDo360()
        {
            var director = new CameraDirector();
            float3 goalCenter = new float3(0.0f, 1.22f, 11.0f);

            // Thử nghiệm các góc cực đoan ngoài dải an toàn [-60°, +60°] và [5°, 45°]
            float[] testYaws = new[] { -180f, -120f, -90f, -60f, -30f, 0f, 30f, 60f, 90f, 120f, 180f, 360f };
            float[] testPitches = new[] { -90f, -45f, 0f, 5f, 20f, 45f, 60f, 90f };
            float[] testDistances = new[] { 0.5f, 2.0f, 3.5f, 5.0f, 10.0f, 50.0f };

            int testCount = 0;
            foreach (float yaw in testYaws)
            foreach (float pitch in testPitches)
            foreach (float dist in testDistances)
            {
                float3 pos = CameraAuthoredBounds.ComputeOrbitPosition(yaw, pitch, dist, goalCenter);
                bool within = director.IsWithinAuthoredBounds(pos);

                Assert.IsTrue(within,
                    $"ReplayOrbit tại (yaw: {yaw}°, pitch: {pitch}°, dist: {dist}m) tính ra toạ độ " +
                    $"({pos.x}, {pos.y}, {pos.z}) nằm ngoài ranh giới cho phép!");
                testCount++;
            }

            Assert.Greater(testCount, 50, "Cần quét đủ các góc quay thử nghiệm");
        }

        [Test]
        public void IsWithinAuthoredBounds_TraFalseKhiNgoaiBien()
        {
            var director = new CameraDirector();

            // Điểm quá xa về bên trái/phải (X)
            Assert.IsFalse(director.IsWithinAuthoredBounds(new float3(-10.0f, 2.0f, 5.0f)));
            Assert.IsFalse(director.IsWithinAuthoredBounds(new float3(10.0f, 2.0f, 5.0f)));

            // Điểm dưới mặt đất hoặc quá cao (Y)
            Assert.IsFalse(director.IsWithinAuthoredBounds(new float3(0.0f, -1.0f, 5.0f)));
            Assert.IsFalse(director.IsWithinAuthoredBounds(new float3(0.0f, 8.0f, 5.0f)));

            // Điểm quá xa phía sau người sút hoặc quá sâu sau khung thành (Z)
            Assert.IsFalse(director.IsWithinAuthoredBounds(new float3(0.0f, 2.0f, -8.0f)));
            Assert.IsFalse(director.IsWithinAuthoredBounds(new float3(0.0f, 2.0f, 20.0f)));
        }

        [Test]
        public void CutTo_DoiGocQuay_VaBanSuKien_ChinhXac()
        {
            var director = new CameraDirector();
            Assert.AreEqual(CameraShot.BehindShooter, director.CurrentShot);

            CameraShot eventOld = CameraShot.BehindShooter;
            CameraShot eventNew = CameraShot.BehindShooter;
            float eventBlend = -1f;
            int eventCount = 0;

            director.OnShotChanged += (oldShot, newShot, blend) =>
            {
                eventOld = oldShot;
                eventNew = newShot;
                eventBlend = blend;
                eventCount++;
            };

            // Chuyển sang Broadcast với blend 0.5s
            director.CutTo(CameraShot.Broadcast, 0.5f);

            Assert.AreEqual(1, eventCount);
            Assert.AreEqual(CameraShot.BehindShooter, eventOld);
            Assert.AreEqual(CameraShot.Broadcast, eventNew);
            Assert.AreEqual(0.5f, eventBlend, 1e-4f);
            Assert.AreEqual(CameraShot.Broadcast, director.CurrentShot);
            Assert.AreEqual(CameraShot.BehindShooter, director.PreviousShot);
            Assert.IsTrue(director.IsBlending);

            // Tua blend
            director.Tick(0.3f);
            Assert.IsTrue(director.IsBlending);
            director.Tick(0.25f);
            Assert.IsFalse(director.IsBlending);
        }

        [Test]
        public void BindToPhase_TuDongChuyenGocTheoPhaLuotSut()
        {
            var director = new CameraDirector();

            // Mặc định Placing, Aiming, RunUp, Contact, Flight đều là BehindShooter
            Assert.AreEqual(CameraShot.BehindShooter, director.GetShotForPhase(KickPhase.Placing));
            Assert.AreEqual(CameraShot.BehindShooter, director.GetShotForPhase(KickPhase.Aiming));
            Assert.AreEqual(CameraShot.BehindShooter, director.GetShotForPhase(KickPhase.Flight));
            Assert.AreEqual(CameraShot.Broadcast, director.GetShotForPhase(KickPhase.Resolution));

            // Tùy biến gắn pha Reaction sang KeeperPOV
            director.BindToPhase(KickPhase.Reaction, CameraShot.KeeperPOV);
            Assert.AreEqual(CameraShot.KeeperPOV, director.GetShotForPhase(KickPhase.Reaction));

            // Giả lập chuyển pha từ Flight sang Reaction
            director.OnKickPhaseChanged(KickPhase.Flight, KickPhase.Reaction);
            Assert.AreEqual(CameraShot.KeeperPOV, director.CurrentShot);
        }

        [Test]
        public void CameraDirector_KhongCapPhatGC()
        {
            var director = new CameraDirector();
            float3 testPos = new float3(0f, 1.5f, 5f);
            bool sink = false;

            // Warm-up JIT
            sink ^= director.IsWithinAuthoredBounds(testPos);
            director.CutTo(CameraShot.Broadcast, 0.5f);
            director.Tick(0.016f);
            director.OnKickPhaseChanged(KickPhase.Flight, KickPhase.Resolution);

            Assert.That(() =>
            {
                for (int i = 0; i < 50; i++)
                {
                    sink ^= director.IsWithinAuthoredBounds(testPos);
                    director.Tick(0.016f);
                }
            }, Is.Not.AllocatingGCMemory());

            Assert.IsTrue(sink || !sink); // Tránh optimizer loại bỏ
        }
    }
}
