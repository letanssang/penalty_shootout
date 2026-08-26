using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Unity.Mathematics;
using Eleven.Ball;
using Eleven.Keeper;
using Eleven.Match;
using Is = NUnit.Framework.Is;

namespace Eleven.Tests.EditMode
{
    [TestFixture]
    public class ReachEnvelopeTests
    {
        KeeperProfile _defaultProfile;

        [SetUp]
        public void SetUp()
        {
            _defaultProfile = KeeperProfile.CreateDefault();
        }

        [TearDown]
        public void TearDown()
        {
            if (_defaultProfile != null)
                Object.DestroyImmediate(_defaultProfile);
        }

        [Test]
        public void Cell7_O_GiuaThap_VoiToiNhanhNhat()
        {
            float tMin = ReachEnvelope.TimeToReach(7, _defaultProfile);

            for (int cell = 0; cell < 9; cell++)
            {
                if (cell == 7)
                    continue;

                float t = ReachEnvelope.TimeToReach(cell, _defaultProfile);
                Assert.Greater(t, tMin, $"Cell {cell} có thời gian ({t:F3}s) phải lớn hơn Cell 7 ({tMin:F3}s)");
            }
        }

        [Test]
        public void Cell0_Va_Cell2_HaiGocTren_ChamNhat()
        {
            float tMax0 = ReachEnvelope.TimeToReach(0, _defaultProfile);
            float tMax2 = ReachEnvelope.TimeToReach(2, _defaultProfile);

            Assert.AreEqual(tMax0, tMax2, 1e-5f, "Hai góc trên phải có thời gian với tới bằng nhau");

            for (int cell = 0; cell < 9; cell++)
            {
                if (cell == 0 || cell == 2)
                    continue;

                float t = ReachEnvelope.TimeToReach(cell, _defaultProfile);
                Assert.Less(t, tMax0, $"Cell {cell} có thời gian ({t:F3}s) phải nhỏ hơn Cell 0/2 ({tMax0:F3}s)");
            }
        }

        [Test]
        public void DoiXung_TraiPhai_ThoiGianBangNhau()
        {
            // Lưới 3x3: hàng 0 (0 vs 2), hàng 1 (3 vs 5), hàng 2 (6 vs 8)
            Assert.AreEqual(ReachEnvelope.TimeToReach(0, _defaultProfile), ReachEnvelope.TimeToReach(2, _defaultProfile), 1e-5f);
            Assert.AreEqual(ReachEnvelope.TimeToReach(3, _defaultProfile), ReachEnvelope.TimeToReach(5, _defaultProfile), 1e-5f);
            Assert.AreEqual(ReachEnvelope.TimeToReach(6, _defaultProfile), ReachEnvelope.TimeToReach(8, _defaultProfile), 1e-5f);
        }

        [Test]
        public void CuSut28m_GocChuA_KhongTheCan_KhiCamKetMuon()
        {
            var p = KeeperProfile.CreateMedium();
            try
            {
                p.reachScale = 1.0f;
                // Cam kết muộn: tại thời điểm chạm bóng (0 ms) hoặc sau đó (+50 ms)
                p.commitOffsetMs = 0f;

                // Tính thời gian bóng bay thật của cú sút 28 m/s tới mặt phẳng khung thành (z = 11m)
                BallState start = new BallState(float3.zero, new float3(0f, 2f, 28f), float3.zero);
                bool crossed = TrajectoryPredictor.FirstCrossing(start, BallParams.Default, GoalGeometry.PenaltyDistance, 1f / 120f, out _, out float arrivalTime);

                Assert.IsTrue(crossed, "Bóng 28 m/s phải chạm mặt phẳng khung thành");
                Assert.Less(arrivalTime, 0.45f, "Thời gian bay 28 m/s tới khung thành 11m phải dưới 0.45s");

                // Ở góc chữ A (Cell 0 và Cell 2), thủ môn cam kết muộn KHÔNG THỂ cản phá
                Assert.IsFalse(ReachEnvelope.CanReach(0, arrivalTime, p), "Cú sút 28 m/s vào góc chữ A trái không thể cản khi cam kết muộn");
                Assert.IsFalse(ReachEnvelope.CanReach(2, arrivalTime, p), "Cú sút 28 m/s vào góc chữ A phải không thể cản khi cam kết muộn");
            }
            finally
            {
                Object.DestroyImmediate(p);
            }
        }

        [Test]
        public void CuSut28m_GocChuA_CoTheCan_KhiCamKetRatSom()
        {
            var p = KeeperProfile.CreateHard();
            try
            {
                // Đoán trước hướng và cam kết sớm 500 ms trước khi sút
                p.commitOffsetMs = -500f;

                BallState start = new BallState(float3.zero, new float3(0f, 2f, 28f), float3.zero);
                TrajectoryPredictor.FirstCrossing(start, BallParams.Default, GoalGeometry.PenaltyDistance, 1f / 120f, out _, out float arrivalTime);

                Assert.IsTrue(ReachEnvelope.CanReach(0, arrivalTime, p), "Thủ môn cam kết sớm 500ms phải kịp với tới góc chữ A");
                Assert.IsTrue(ReachEnvelope.CanReach(2, arrivalTime, p), "Thủ môn cam kết sớm 500ms phải kịp với tới góc chữ A");
            }
            finally
            {
                Object.DestroyImmediate(p);
            }
        }

        [Test]
        public void CuSutVaoGiua_CoTheCan_KeCaKhiPhanXaMuon()
        {
            var p = KeeperProfile.CreateMedium();
            try
            {
                p.commitOffsetMs = 0f; // Cam kết lúc chạm bóng
                p.reactionMs = 240f;   // Trễ phản xạ 240 ms

                // Cú sút 28 m/s vào chính diện (Cell 7 - chân thủ môn)
                BallState start = new BallState(float3.zero, new float3(0f, 0.4f, 28f), float3.zero);
                TrajectoryPredictor.FirstCrossing(start, BallParams.Default, GoalGeometry.PenaltyDistance, 1f / 120f, out _, out float arrivalTime);

                // Ô giữa-thấp chỉ cần 0.15s di chuyển, tổng 0.24 + 0.15 = 0.39s < 0.41s arrivalTime
                Assert.IsTrue(ReachEnvelope.CanReach(7, arrivalTime, p), "Cú sút sệt chính diện thủ môn vẫn kịp cản bằng chân");
            }
            finally
            {
                Object.DestroyImmediate(p);
            }
        }

        [Test]
        public void ReachScale_KepCung_TrongKhoang_085_110()
        {
            var pLo = KeeperProfile.CreateMedium();
            var pHi = KeeperProfile.CreateMedium();
            var pExtremeLo = KeeperProfile.CreateMedium();
            var pExtremeHi = KeeperProfile.CreateMedium();

            try
            {
                pLo.reachScale = 0.85f;
                pExtremeLo.reachScale = 0.10f; // Rất nhỏ

                pHi.reachScale = 1.10f;
                pExtremeHi.reachScale = 5.0f; // Rất lớn

                for (int cell = 0; cell < 9; cell++)
                {
                    float tLo = ReachEnvelope.TimeToReach(cell, pLo);
                    float tExtremeLo = ReachEnvelope.TimeToReach(cell, pExtremeLo);
                    Assert.AreEqual(tLo, tExtremeLo, 1e-5f, $"Cell {cell}: reachScale = 0.10 phải bị kẹp về 0.85");

                    float tHi = ReachEnvelope.TimeToReach(cell, pHi);
                    float tExtremeHi = ReachEnvelope.TimeToReach(cell, pExtremeHi);
                    Assert.AreEqual(tHi, tExtremeHi, 1e-5f, $"Cell {cell}: reachScale = 5.0 phải bị kẹp về 1.10");
                }
            }
            finally
            {
                Object.DestroyImmediate(pLo);
                Object.DestroyImmediate(pHi);
                Object.DestroyImmediate(pExtremeLo);
                Object.DestroyImmediate(pExtremeHi);
            }
        }

        [Test]
        public void ReachScale_LonHon_VoiToiNhanhHon_DonDieu()
        {
            var p1 = KeeperProfile.CreateMedium();
            var p2 = KeeperProfile.CreateMedium();
            try
            {
                p1.reachScale = 0.90f;
                p2.reachScale = 1.05f;

                for (int cell = 0; cell < 9; cell++)
                {
                    float t1 = ReachEnvelope.TimeToReach(cell, p1);
                    float t2 = ReachEnvelope.TimeToReach(cell, p2);
                    Assert.Less(t2, t1, $"Cell {cell}: reachScale lớn hơn ({p2.reachScale}) phải có thời gian với tới ngắn hơn ({p1.reachScale})");
                }
            }
            finally
            {
                Object.DestroyImmediate(p1);
                Object.DestroyImmediate(p2);
            }
        }

        [Test]
        public void HamThuan_KhongCapPhatGC()
        {
            // Kiểm chứng 0 byte GC allocation khi gọi lặp đi lặp lại
            Assert.That(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    _ = ReachEnvelope.TimeToReach(i % 9, _defaultProfile);
                    _ = ReachEnvelope.CanReach(i % 9, 0.45f, _defaultProfile);
                }
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void Bien_CellNgoaiPhanVi_DuocKepAnToan_KhongCrash()
        {
            Assert.DoesNotThrow(() =>
            {
                float tNeg = ReachEnvelope.TimeToReach(-5, _defaultProfile);
                float t0 = ReachEnvelope.TimeToReach(0, _defaultProfile);
                Assert.AreEqual(t0, tNeg, 1e-5f);

                float tOver = ReachEnvelope.TimeToReach(99, _defaultProfile);
                float t8 = ReachEnvelope.TimeToReach(8, _defaultProfile);
                Assert.AreEqual(t8, tOver, 1e-5f);

                bool canNeg = ReachEnvelope.CanReach(-1, 0.5f, _defaultProfile);
                bool canOver = ReachEnvelope.CanReach(10, 0.5f, _defaultProfile);
                Assert.IsNotNull(canNeg);
                Assert.IsNotNull(canOver);
            });
        }

        [Test]
        public void Bien_ProfileNull_KhongCrash()
        {
            Assert.DoesNotThrow(() =>
            {
                float t = ReachEnvelope.TimeToReach(4, null);
                Assert.Greater(t, 0f);

                bool can = ReachEnvelope.CanReach(4, 0.5f, null);
                Assert.IsNotNull(can);
            });
        }

        [Test]
        public void Bien_BallArrivalTime_KhongHopLe_TraFalse()
        {
            Assert.IsFalse(ReachEnvelope.CanReach(4, 0f, _defaultProfile), "Thời gian bóng tới = 0 phải trả false");
            Assert.IsFalse(ReachEnvelope.CanReach(4, -1f, _defaultProfile), "Thời gian bóng tới âm phải trả false");
        }

        [Test]
        public void DoiChieu_SoLieuThucNghiem_3Video()
        {
            // Profile chuẩn reachScale = 1.0
            var p = KeeperProfile.CreateMedium();
            try
            {
                p.reachScale = 1.0f;

                // Video 1: Dưới-giữa (Cell 7) ~ 150 ms
                float tCell7 = ReachEnvelope.TimeToReach(7, p);
                Assert.AreEqual(0.15f, tCell7, 0.001f, "Cell 7 khớp mốc video cản phá phản xạ bằng chân 150ms");

                // Video 2: Giữa-trái / Giữa-phải (Cell 3, 5) ~ 460 ms
                float tCell3 = ReachEnvelope.TimeToReach(3, p);
                float tCell5 = ReachEnvelope.TimeToReach(5, p);
                Assert.AreEqual(0.46f, tCell3, 0.001f, "Cell 3 khớp mốc video đổ người tầm trung 460ms");
                Assert.AreEqual(0.46f, tCell5, 0.001f, "Cell 5 khớp mốc video đổ người tầm trung 460ms");

                // Video 3: Góc chữ A (Cell 0, 2) ~ 600 ms
                float tCell0 = ReachEnvelope.TimeToReach(0, p);
                float tCell2 = ReachEnvelope.TimeToReach(2, p);
                Assert.AreEqual(0.60f, tCell0, 0.001f, "Cell 0 khớp mốc video bay người hết tầm với góc chữ A 600ms");
                Assert.AreEqual(0.60f, tCell2, 0.001f, "Cell 2 khớp mốc video bay người hết tầm với góc chữ A 600ms");
            }
            finally
            {
                Object.DestroyImmediate(p);
            }
        }

        [Test]
        public void ProfilePresets_DungThongSoPlan()
        {
            var easy = KeeperProfile.CreateEasy();
            var med = KeeperProfile.CreateMedium();
            var hard = KeeperProfile.CreateHard();

            try
            {
                // Easy
                Assert.AreEqual(0.30f, easy.readAccuracy, 1e-4f);
                Assert.AreEqual(320f, easy.reactionMs, 1e-4f);
                Assert.AreEqual(-60f, easy.commitOffsetMs, 1e-4f);
                Assert.AreEqual(0.92f, easy.reachScale, 1e-4f);
                Assert.AreEqual(0.70f, easy.parryChance, 1e-4f);

                // Medium
                Assert.AreEqual(0.52f, med.readAccuracy, 1e-4f);
                Assert.AreEqual(240f, med.reactionMs, 1e-4f);
                Assert.AreEqual(-110f, med.commitOffsetMs, 1e-4f);
                Assert.AreEqual(1.00f, med.reachScale, 1e-4f);
                Assert.AreEqual(0.45f, med.parryChance, 1e-4f);

                // Hard
                Assert.AreEqual(0.72f, hard.readAccuracy, 1e-4f);
                Assert.AreEqual(185f, hard.reactionMs, 1e-4f);
                Assert.AreEqual(-150f, hard.commitOffsetMs, 1e-4f);
                Assert.AreEqual(1.06f, hard.reachScale, 1e-4f);
                Assert.AreEqual(0.28f, hard.parryChance, 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(easy);
                Object.DestroyImmediate(med);
                Object.DestroyImmediate(hard);
            }
        }
    }
}
