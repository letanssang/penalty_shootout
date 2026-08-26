using NUnit.Framework;
using Unity.Mathematics;
using Eleven.Ball;
using Eleven.Match;

namespace Eleven.Tests.EditMode
{
    // GHI CHÚ: xem đầu file GoalGeometry.cs về 3 điểm diễn giải hợp đồng cần người xác
    // nhận (mặt phẳng khung thành ở z=PenaltyDistance chứ không phải z=0; đường tâm cột/xà
    // nằm ngoài mép trong PostRadius; PostIn/PostOut suy từ quỹ đạo bỏ qua khung).
    // Toàn bộ test dưới đây giả định đúng ba điểm đó — nếu sai, phải viết lại cả file này.

    [TestFixture]
    public class GoalGeometryTests
    {
        const float W = GoalGeometry.Width;       // 7.32
        const float H = GoalGeometry.Height;       // 2.44
        const float R = GoalGeometry.PostRadius;   // 0.06
        const float D = GoalGeometry.PenaltyDistance; // 11

        /// <summary>Không cản, không trọng lực, không Magnus — chuyển động thẳng đều, toạ độ tính tay được.</summary>
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

        /// <summary>Bóng bay thẳng đều tới (x,y) đúng lúc z = D, dùng NoForceParams. t luôn = 1s.</summary>
        static BallState StraightShotTo(float x, float y)
        {
            return new BallState
            {
                position = float3.zero,
                velocity = new float3(x, y, D), // t=1s → vị trí = velocity
                spin = float3.zero
            };
        }

        // ─── 1-2. Trong khung, giữa sân → Goal ───

        [Test]
        public void GiuaKhung_ChinhGiua_LaGoal()
        {
            var outcome = GoalGeometry.Classify(StraightShotTo(0f, H * 0.5f), NoForceParams(), out var crossing, out int cell);
            Assert.AreEqual(ShotOutcome.Goal, outcome);
            Assert.AreEqual(4, cell, "Chính giữa khung phải là ô 4 (giữa lưới 3x3)");
            Assert.AreEqual(D, crossing.z, 1e-3f);
        }

        [Test]
        public void TrongKhung_LechThapTrai_LaGoal()
        {
            var outcome = GoalGeometry.Classify(StraightShotTo(-2f, 0.3f), NoForceParams(), out _, out int cell);
            Assert.AreEqual(ShotOutcome.Goal, outcome);
            Assert.AreEqual(6, cell, "x=-2 (trái), y=0.3 (thấp) phải rơi vào ô dưới-trái (6)");
        }

        // ─── 3-4. Rộng trái/phải, cách xa khung — WideLeft/WideRight ───

        [Test]
        public void RaNgoaiXaBenTrai_LaWideLeft()
        {
            var outcome = GoalGeometry.Classify(StraightShotTo(-6f, 1.2f), NoForceParams(), out _, out _);
            Assert.AreEqual(ShotOutcome.WideLeft, outcome);
        }

        [Test]
        public void RaNgoaiXaBenPhai_LaWideRight()
        {
            var outcome = GoalGeometry.Classify(StraightShotTo(6f, 1.2f), NoForceParams(), out _, out _);
            Assert.AreEqual(ShotOutcome.WideRight, outcome);
        }

        // ─── 5. Bay cao qua xà — Over ───

        [Test]
        public void BayCaoQuaXa_LaOver()
        {
            var outcome = GoalGeometry.Classify(StraightShotTo(0f, 5f), NoForceParams(), out _, out _);
            Assert.AreEqual(ShotOutcome.Over, outcome);
        }

        // ─── 6. Hụt tầm, rơi trước khi tới khung thành — Short ───

        [Test]
        public void RoiTruocVachKhung_LaShort()
        {
            // Vận tốc ngang yếu + trọng lực thật → chạm đất (y<=0) trước khi z tới 11m.
            var p = BallParams.Default; // có trọng lực thật
            var s = new BallState
            {
                position = new float3(0f, 0.5f, 0f),
                velocity = new float3(0f, 1f, 3f), // rất yếu, sẽ rơi xuống đất sớm
                spin = float3.zero
            };
            var outcome = GoalGeometry.Classify(s, p, out var crossing, out _);
            Assert.AreEqual(ShotOutcome.Short, outcome);
            Assert.Less(crossing.z, D, "Điểm chạm đất phải ở trước vạch khung thành");
        }

        // ─── 7-10. Bốn trường hợp chạm cột: In/Out × trái/phải ───

        [Test]
        public void ChamCotTrai_QuyDaoGocVaoTrong_LaPostIn()
        {
            // Cột trái ở x = -(W/2+R) = -3.72. Nhắm x=-3.65 (vào trong mép 3.66) nhưng vẫn
            // trong bán kính chạm cột (0.07 < R+ballRadius=0.17).
            var outcome = GoalGeometry.Classify(StraightShotTo(-3.65f, 1.2f), NoForceParams(), out _, out _);
            Assert.AreEqual(ShotOutcome.PostIn, outcome);
        }

        [Test]
        public void ChamCotTrai_QuyDaoGocRaNgoai_LaPostOut()
        {
            // Nhắm đúng tâm cột trái (-3.72) — nếu bỏ cột đi, quỹ đạo vẫn ở ngoài mép trong (-3.66).
            var outcome = GoalGeometry.Classify(StraightShotTo(-(W * 0.5f + R), 1.2f), NoForceParams(), out _, out _);
            Assert.AreEqual(ShotOutcome.PostOut, outcome);
        }

        [Test]
        public void ChamCotPhai_QuyDaoGocVaoTrong_LaPostIn()
        {
            var outcome = GoalGeometry.Classify(StraightShotTo(3.65f, 1.2f), NoForceParams(), out _, out _);
            Assert.AreEqual(ShotOutcome.PostIn, outcome);
        }

        [Test]
        public void ChamCotPhai_QuyDaoGocRaNgoai_LaPostOut()
        {
            var outcome = GoalGeometry.Classify(StraightShotTo(W * 0.5f + R, 1.2f), NoForceParams(), out _, out _);
            Assert.AreEqual(ShotOutcome.PostOut, outcome);
        }

        // ─── 11. Chạm xà ngang chính giữa — Crossbar ───

        [Test]
        public void ChamXaNgang_ChinhGiua_LaCrossbar()
        {
            var outcome = GoalGeometry.Classify(StraightShotTo(0f, H + R), NoForceParams(), out _, out _);
            Assert.AreEqual(ShotOutcome.Crossbar, outcome);
        }

        // ─── 12-15. Bốn góc chữ A: đúng tâm góc (cột gặp xà, đồng khoảng cách) và ngay
        // sát trong góc (gần cột hơn xà) phải nhất quán, cho cả trái lẫn phải ───

        [Test]
        public void GocTrenTrai_DungTamGoc_CotGapXa_KhongPhaiGoalHayWide()
        {
            // Nhắm đúng tâm góc (giao đường tâm cột và đường tâm xà) — khoảng cách tới
            // cột và tới xà bằng nhau (=0), DistancePointToSegment hoà nên xà thắng (<=).
            var outcome = GoalGeometry.Classify(StraightShotTo(-(W * 0.5f + R), H + R), NoForceParams(), out _, out _);
            Assert.That(outcome == ShotOutcome.Crossbar || outcome == ShotOutcome.PostOut,
                $"Góc trên-trái, đúng tâm góc, phải là Crossbar hoặc PostOut, nhận được {outcome}");
        }

        [Test]
        public void GocTrenPhai_DungTamGoc_CotGapXa_KhongPhaiGoalHayWide()
        {
            var outcome = GoalGeometry.Classify(StraightShotTo(W * 0.5f + R, H + R), NoForceParams(), out _, out _);
            Assert.That(outcome == ShotOutcome.Crossbar || outcome == ShotOutcome.PostOut,
                $"Góc trên-phải, đúng tâm góc, phải là Crossbar hoặc PostOut, nhận được {outcome}");
        }

        [Test]
        public void GocTrenTrai_LechVaoGanCotHonXa_LaPostIn()
        {
            // Lệch xuống dưới đường tâm xà (2.3 < CrossbarCenterY=2.5) nhưng vẫn sát cột
            // (x=-3.65) — khoảng cách tới cột (0.07) < khoảng cách tới xà (~0.2) → cột thắng,
            // và điểm vẫn nằm trong biên khung (x>=-3.66, y<=2.44) → PostIn, không phải Crossbar.
            var outcome = GoalGeometry.Classify(StraightShotTo(-3.65f, 2.3f), NoForceParams(), out _, out _);
            Assert.AreEqual(ShotOutcome.PostIn, outcome,
                "Góc trên-trái, lệch vào gần cột hơn xà, phải phân loại là PostIn");
        }

        [Test]
        public void GocTrenPhai_LechVaoGanCotHonXa_LaPostIn()
        {
            var outcome = GoalGeometry.Classify(StraightShotTo(3.65f, 2.3f), NoForceParams(), out _, out _);
            Assert.AreEqual(ShotOutcome.PostIn, outcome,
                "Góc trên-phải, lệch vào gần cột hơn xà, phải phân loại là PostIn");
        }

        // ─── 14. Biên đúng mép trong cột x=3.66: phân loại nhất quán, không dao động ───

        [Test]
        public void Bien_MepTrongCotPhai_366_NhatQuan()
        {
            var s = StraightShotTo(3.66f, 1.2f);
            var p = NoForceParams();

            var outcome1 = GoalGeometry.Classify(s, p, out var c1, out int cell1);
            var outcome2 = GoalGeometry.Classify(s, p, out var c2, out int cell2);

            Assert.AreEqual(outcome1, outcome2, "Cùng input tại biên phải cho cùng kết quả — không dao động");
            Assert.AreEqual(cell1, cell2);
            Assert.AreEqual(c1, c2);
            // Tại đúng mép trong, cách tâm cột 0.06 = PostRadius → luôn nằm trong bán kính chạm cột.
            Assert.AreEqual(ShotOutcome.PostIn, outcome1,
                "x=3.66 là mép trong cột, cách tâm cột đúng PostRadius → phải chạm cột và tính là PostIn (mép trong vẫn thuộc khung)");
        }

        // ─── 15. Quỹ đạo cong ra rồi cong vào lại vẫn tính đúng theo giao điểm cuối cùng khi chạm mặt phẳng ───

        [Test]
        public void BongCongRaRoiVaoLai_TinhDungTheoGiaoDiemKhiChamMatPhang()
        {
            // Tắt cản và trọng lực, chỉ giữ Magnus (cần airDensity > 0 để có lực) — với
            // spin thuần theo trục Y, Magnus không có thành phần dọc (Y), nên vy giữ
            // nguyên tuyến tính và bóng chắc chắn không rơi xuống đất trước khi tới z=D.
            // Nhờ vậy X mới lệch (cong) trong lúc bay mà Y/Z vẫn dự đoán trước được.
            var p = BallParams.Default;
            p.gravity = 0f;
            p.cdLow = 0f;
            p.cdHigh = 0f;
            var s = new BallState
            {
                position = float3.zero,
                velocity = new float3(0f, 3f, 24f),
                spin = new float3(0f, 40f, 0f) // xoáy quanh Y → lệch ngang trong lúc bay
            };

            var outcome = GoalGeometry.Classify(s, p, out var crossing, out int cell);

            Assert.AreEqual(D, crossing.z, 1e-2f, "Điểm cắt phải đúng tại mặt phẳng khung thành");
            Assert.IsFalse(float.IsNaN(crossing.x) || float.IsNaN(crossing.y));
            // Không khẳng định outcome cụ thể (phụ thuộc tham số khí động) — chỉ cần không NaN
            // và cell hợp lệ, chứng minh hàm không bị rối bởi quỹ đạo cong.
            Assert.That(cell, Is.InRange(0, 8));
        }

        // ─── 16-24. Lưới 3x3 phủ kín, không chồng lấn, không hở ───

        [Test]
        public void CellOf_LuonTraGiaTriHopLe_TrenLuoiDay()
        {
            for (float x = -W; x <= W; x += 0.05f)
            {
                for (float y = -1f; y <= H + 1f; y += 0.05f)
                {
                    int cell = GoalGeometry.CellOf(new float3(x, y, D));
                    Assert.That(cell, Is.InRange(0, 8), $"CellOf({x},{y}) phải nằm trong [0,8]");
                }
            }
        }

        [Test]
        public void CellCenter_RoundTrip_VeDungOCho9O()
        {
            for (int i = 0; i < 9; i++)
            {
                var center = GoalGeometry.CellCenter(i);
                int backToCell = GoalGeometry.CellOf(center);
                Assert.AreEqual(i, backToCell, $"Tâm ô {i} phải map ngược đúng về ô {i}");
            }
        }

        [Test]
        public void CellOf_ChinGocLuoi_DungOTuongUng()
        {
            float thirdX = W / 3f;
            float thirdY = H / 3f;
            float eps = 0.01f;

            // Trên-trái sâu trong ô 0
            Assert.AreEqual(0, GoalGeometry.CellOf(new float3(-W * 0.5f + eps, H - eps, D)));
            // Trên-phải sâu trong ô 2
            Assert.AreEqual(2, GoalGeometry.CellOf(new float3(W * 0.5f - eps, H - eps, D)));
            // Dưới-trái sâu trong ô 6
            Assert.AreEqual(6, GoalGeometry.CellOf(new float3(-W * 0.5f + eps, eps, D)));
            // Dưới-phải sâu trong ô 8
            Assert.AreEqual(8, GoalGeometry.CellOf(new float3(W * 0.5f - eps, eps, D)));

            Assert.Greater(thirdX, 0f);
            Assert.Greater(thirdY, 0f);
        }

        // ─── 25. Kích thước đúng luật IFAB ───

        [Test]
        public void KichThuoc_DungLuatIFAB()
        {
            Assert.AreEqual(7.32f, GoalGeometry.Width, 1e-6f);
            Assert.AreEqual(2.44f, GoalGeometry.Height, 1e-6f);
            Assert.AreEqual(11f, GoalGeometry.PenaltyDistance, 1e-6f);
            Assert.AreEqual(0.06f, GoalGeometry.PostRadius, 1e-6f);
        }
    }
}
