using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Unity.Mathematics;
using Eleven.Ball;
using Eleven.Shooter;
using Eleven.Match;
using Eleven.Presentation;
using Is = NUnit.Framework.Is;

namespace Eleven.Tests.EditMode
{
    [TestFixture]
    public class ReplaySystemTests
    {
        private const float MatchTol = 1e-4f;

        private ReplayKickData CreateSampleKick(uint seed, float3 aim, float speed, float3 spin, ShotType type)
        {
            var p = BallParams.Default;
            float3 origin = new float3(0f, p.radius, 0f);
            float3 dir = math.normalize(aim - origin);
            float3 initVel = dir * speed;

            var startState = new BallState
            {
                position = origin,
                velocity = initVel,
                spin = spin
            };

            ShotOutcome outcome = GoalGeometry.Classify(in startState, in p, out float3 crossing, out int cell);

            return new ReplayKickData
            {
                seed = seed,
                intent = new ShotIntent
                {
                    aimPoint = aim,
                    spin = spin,
                    speed = speed,
                    type = type,
                    quality = 0.95f,
                    unstable = type == ShotType.Knuckle,
                    scatterRadius = 0.05f
                },
                expectedOutcome = outcome,
                expectedCrossing = crossing,
                expectedCell = cell
            };
        }

        [Test]
        public void ReplayKickData_KichThuocNhiPhan_Duoi256Byte()
        {
            var kick = CreateSampleKick(12345u, new float3(1.5f, 1.8f, 11f), 26f, new float3(0f, 15f, 0f), ShotType.InsideFoot);
            byte[] bytes = kick.ToBytes();

            Assert.IsNotNull(bytes);
            Assert.Less(bytes.Length, ReplayKickData.MaxAllowedPayloadBytes,
                $"Kích thước dữ liệu replay ({bytes.Length} bytes) phải nhỏ hơn ngân sách 256 bytes.");
            Assert.Greater(bytes.Length, 30, "Kích thước dữ liệu quá nhỏ, có thể bị thiếu trường.");
        }

        [Test]
        public void ReplayKickData_DongGoiVaGiaiMa_KhopTuyetDoi()
        {
            var original = CreateSampleKick(98765u, new float3(-2.2f, 1.1f, 11f), 28f, new float3(0f, -20f, 5f), ShotType.Instep);
            byte[] bytes = original.ToBytes();

            bool ok = ReplayKickData.TryFromBytes(bytes, out ReplayKickData deserialized, out string error);

            Assert.IsTrue(ok, $"Giải mã thất bại: {error}");
            Assert.IsNull(error);

            Assert.AreEqual(original.seed, deserialized.seed);
            Assert.AreEqual(original.intent.aimPoint.x, deserialized.intent.aimPoint.x, MatchTol);
            Assert.AreEqual(original.intent.aimPoint.y, deserialized.intent.aimPoint.y, MatchTol);
            Assert.AreEqual(original.intent.aimPoint.z, deserialized.intent.aimPoint.z, MatchTol);
            Assert.AreEqual(original.intent.speed, deserialized.intent.speed, MatchTol);
            Assert.AreEqual(original.intent.type, deserialized.intent.type);
            Assert.AreEqual(original.intent.unstable, deserialized.intent.unstable);
            Assert.AreEqual(original.expectedOutcome, deserialized.expectedOutcome);
            Assert.AreEqual(original.expectedCell, deserialized.expectedCell);
            Assert.AreEqual(original.expectedCrossing.x, deserialized.expectedCrossing.x, MatchTol);
            Assert.AreEqual(original.expectedCrossing.y, deserialized.expectedCrossing.y, MatchTol);
        }

        [Test]
        public void ReplayKickData_SuaMotByte_PhatHienVaTuChoi()
        {
            var original = CreateSampleKick(55555u, new float3(0.5f, 2.0f, 11f), 24f, float3.zero, ShotType.Chip);
            byte[] bytes = original.ToBytes();

            // Sửa đúng một byte ở giữa payload
            int corruptIndex = bytes.Length / 2;
            bytes[corruptIndex] ^= 0xFF;

            bool ok = ReplayKickData.TryFromBytes(bytes, out ReplayKickData _, out string error);

            Assert.IsFalse(ok, "Dữ liệu bị sửa đổi thủ công nhưng hệ thống không phát hiện!");
            Assert.IsNotNull(error);
            Assert.That(error, Does.Contain("Checksum").IgnoreCase);
        }

        [Test]
        public void ReplayPlayer_ChayLaiChoQuyDaoGiongHet_SaiSoDuoi1e4()
        {
            var testKicks = new[]
            {
                CreateSampleKick(101u, new float3(-3.0f, 2.1f, 11f), 27f, new float3(0f, 25f, 0f), ShotType.InsideFoot),
                CreateSampleKick(102u, new float3(2.8f, 0.4f, 11f), 30f, new float3(0f, -15f, 0f), ShotType.Instep),
                CreateSampleKick(103u, new float3(0.0f, 2.3f, 11f), 18f, new float3(10f, 0f, 0f), ShotType.Chip),
                CreateSampleKick(104u, new float3(1.2f, 1.5f, 11f), 29f, float3.zero, ShotType.Knuckle)
            };

            var p = BallParams.Default;

            foreach (var kick in testKicks)
            {
                bool verified = ReplayPlayer.VerifyTrajectory(in kick, in p, MatchTol, out string report);
                Assert.IsTrue(verified, $"Xác thực quỹ đạo replay thất bại cho hạt giống {kick.seed}: {report}");
            }
        }

        [Test]
        public void ReplayPlayer_PhatLaiCacTocDo_0_25_0_5_1_0_DungQuyDao()
        {
            var kick = CreateSampleKick(2026u, new float3(-1.8f, 1.9f, 11f), 25f, new float3(0f, 18f, 0f), ShotType.InsideFoot);

            var playerNormal = new ReplayPlayer();
            var playerHalf = new ReplayPlayer();
            var playerQuarter = new ReplayPlayer();

            playerNormal.Load(in kick);
            playerNormal.SetPlaybackSpeed(1.0f);
            playerNormal.Play();

            playerHalf.Load(in kick);
            playerHalf.SetPlaybackSpeed(0.5f);
            playerHalf.Play();

            playerQuarter.Load(in kick);
            playerQuarter.SetPlaybackSpeed(0.25f);
            playerQuarter.Play();

            float simDt = 1f / 60f;

            // Chạy cho tới khi cả 3 player hoàn thành
            for (int step = 0; step < 500; step++)
            {
                playerNormal.Tick(simDt);
                playerHalf.Tick(simDt);
                playerQuarter.Tick(simDt);
            }

            Assert.IsTrue(playerNormal.HasCompleted);
            Assert.IsTrue(playerHalf.HasCompleted);
            Assert.IsTrue(playerQuarter.HasCompleted);

            // Khẳng định toạ độ kết thúc của quả bóng ở cả 3 tốc độ trùng khớp sai số < 1e-4m
            float distNormalHalf = math.distance(playerNormal.CurrentBallState.position, playerHalf.CurrentBallState.position);
            float distNormalQuarter = math.distance(playerNormal.CurrentBallState.position, playerQuarter.CurrentBallState.position);

            Assert.Less(distNormalHalf, MatchTol, $"Tốc độ 0.5x làm lệch quỹ đạo bóng: {distNormalHalf:E3} m");
            Assert.Less(distNormalQuarter, MatchTol, $"Tốc độ 0.25x làm lệch quỹ đạo bóng: {distNormalQuarter:E3} m");
        }

        [Test]
        public void ReplayPlayer_KhongCapPhatGC_KhiPhatLai()
        {
            var kick = CreateSampleKick(777u, new float3(0f, 1.2f, 11f), 25f, float3.zero, ShotType.Instep);
            var player = new ReplayPlayer();
            player.Load(in kick);
            player.Play();

            // Warm-up JIT
            player.Tick(0.016f);

            Assert.That(() =>
            {
                for (int i = 0; i < 50; i++)
                {
                    player.Tick(0.016f);
                }
            }, Is.Not.AllocatingGCMemory());
        }
    }
}
