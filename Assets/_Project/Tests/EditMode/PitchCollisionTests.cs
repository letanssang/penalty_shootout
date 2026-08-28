using NUnit.Framework;
using Unity.Mathematics;
using Eleven.Ball;
using Eleven.Match;
using Eleven.Presentation;

namespace Eleven.Tests.EditMode
{
    /// <summary>
    /// Chốt hành vi "quả bóng dừng ở đâu sau khi cú sút đã xong".
    ///
    /// Bộ test này sinh ra từ một lỗi thật, báo bởi người chơi thử ngày 2026-08-28: bóng
    /// biến mất khi sút lên khán đài và khi vào lưới. Nguyên nhân là MatchGameLoop tắt toàn
    /// bộ va chạm cùng lúc với việc tắt chấm điểm, nên bóng rơi xuyên qua sân. Vì vậy phần
    /// lớn các test dưới đây kiểm đúng một điều: BÓNG KHÔNG BAO GIỜ ĐƯỢC LỌT XUỐNG DƯỚI MẶT.
    /// </summary>
    [TestFixture]
    public class PitchCollisionTests
    {
        const float R = 0.11f;
        const float Dt = 1f / 120f;

        [Test]
        public void MatCo_ChanBongLai_KhongChoLotXuongDuoiSan()
        {
            // Bóng đang ở dưới mặt cỏ và vẫn lao xuống — đúng trạng thái sinh ra lỗi cũ.
            var s = new BallState(new float3(0f, -0.4f, 3f), new float3(0f, -9f, 2f), float3.zero);

            bool changed = PitchCollision.Resolve(in s, Dt, R, out BallState next, out _);

            Assert.IsTrue(changed, "Bóng nằm dưới mặt cỏ mà va chạm không đụng gì tới nó.");
            Assert.GreaterOrEqual(next.position.y, R - 1e-4f,
                "Bóng bị bỏ lại dưới mặt sân — đây chính là lỗi 'bóng biến mất'.");
            Assert.Greater(next.velocity.y, 0f, "Chạm đất thì phải nảy lên, không lao tiếp xuống.");
        }

        [Test]
        public void KhanDai_LaBacThangDac_BongNamTrenBacChuKhongChuiXuongChanTuong()
        {
            // Trên cỏ thì mặt đất là 0.
            Assert.AreEqual(0f, PitchCollision.SurfaceHeight(0f, 0f), 1e-4f);
            Assert.AreEqual(0f, PitchCollision.SurfaceHeight(0f, 10f), 1e-4f);

            // Ngay trước khán đài chính vẫn là cỏ, vừa qua mép thì có bậc.
            Assert.AreEqual(0f, PitchCollision.SurfaceHeight(0f, 15.4f), 1e-4f);
            Assert.Greater(PitchCollision.SurfaceHeight(0f, 15.6f), 0.5f);

            // Càng lùi vào trong khán đài bậc càng cao.
            float front = PitchCollision.SurfaceHeight(0f, 16f);
            float back = PitchCollision.SurfaceHeight(0f, 24f);
            Assert.Greater(back, front, "Khán đài phải cao dần về phía sau.");

            // Khán đài cánh ở hai bên.
            Assert.Greater(PitchCollision.SurfaceHeight(12f, 4f), 0.5f);
            Assert.Greater(PitchCollision.SurfaceHeight(-12f, 4f), 0.5f);
        }

        [Test]
        public void SutLenKhanDai_BongDungLaiTrenBac_KhongRoiXuyenQuaSan()
        {
            // Đá bổng qua xà, bay vào giữa khán đài chính rồi rơi xuống.
            var s = new BallState(new float3(0f, 1.0f, 20f), new float3(0f, -8f, 6f), float3.zero);
            float bac = PitchCollision.SurfaceHeight(0f, 20f);

            PitchCollision.Resolve(in s, Dt, R, out BallState next, out _);

            Assert.GreaterOrEqual(next.position.y, bac + R - 1e-4f,
                $"Bóng phải nằm trên mặt bậc khán đài (cao {bac:F2}m), không lọt xuống dưới.");
        }

        [Test]
        public void TrongLongLuoi_VanTocBiHam_DeCuSutGamChuKhongXuyenQua()
        {
            float3 v = new float3(0f, 0f, 25f);
            var s = new BallState(new float3(0f, 1.2f, GoalGeometry.PenaltyDistance + 0.5f), v, float3.zero);

            bool changed = PitchCollision.Resolve(in s, Dt, R, out BallState next, out _);

            Assert.IsTrue(changed, "Bóng trong lòng lưới mà không bị hãm.");
            Assert.Less(next.velocity.z, v.z, "Lưới phải hãm bóng lại.");
            Assert.Greater(next.velocity.z, 0f, "Một bước 1/120s không được hãm tới mức đảo chiều.");
        }

        [Test]
        public void NgoaiLongLuoi_KhongBiHam()
        {
            // Cùng độ cao, cùng vận tốc, nhưng lệch ra ngoài mép cột.
            float3 v = new float3(0f, 0f, 25f);
            float x = GoalGeometry.Width * 0.5f + 1.5f;
            var s = new BallState(new float3(x, 1.2f, GoalGeometry.PenaltyDistance + 0.5f), v, float3.zero);

            PitchCollision.Resolve(in s, Dt, R, out BallState next, out _);

            Assert.AreEqual(v.z, next.velocity.z, 1e-4f, "Ngoài khung thành thì không có lưới nào để hãm.");
        }

        [Test]
        public void BongChamDatVaDaChamThiCoiNhuChet_KhongNayLanTanMai()
        {
            var s = new BallState(new float3(1f, R - 0.001f, 4f), new float3(0.05f, -0.05f, 0.05f), float3.zero);

            PitchCollision.Resolve(in s, Dt, R, out BallState next, out bool atRest);

            Assert.IsTrue(atRest, "Bóng chậm và đã chạm đất thì phải được báo là nằm yên.");
            Assert.AreEqual(0f, math.length(next.velocity), 1e-5f, "Nằm yên thì vận tốc phải bằng 0.");
        }

        [Test]
        public void BongConNhanhThiChuaChet()
        {
            var s = new BallState(new float3(1f, R - 0.001f, 4f), new float3(0f, -6f, 4f), float3.zero);

            PitchCollision.Resolve(in s, Dt, R, out _, out bool atRest);

            Assert.IsFalse(atRest, "Bóng đang nảy mạnh mà đã bị coi là chết thì cú sút cụt lủn.");
        }

        [Test]
        public void RaKhoiVungDoHoa_ThiBaoChet_DeVongLapTatSim()
        {
            Assert.IsTrue(PitchCollision.IsOutOfWorld(new float3(40f, 2f, 5f)));
            Assert.IsTrue(PitchCollision.IsOutOfWorld(new float3(0f, 2f, 60f)));
            Assert.IsTrue(PitchCollision.IsOutOfWorld(new float3(0f, 2f, -30f)));
            Assert.IsFalse(PitchCollision.IsOutOfWorld(new float3(0f, 2f, 5f)));

            var s = new BallState(new float3(40f, 2f, 5f), new float3(0f, -5f, 0f), float3.zero);
            PitchCollision.Resolve(in s, Dt, R, out _, out bool atRest);
            Assert.IsTrue(atRest, "Bóng đã ra ngoài vùng có đồ hoạ thì phải cho dừng sim.");
        }

        [Test]
        public void ChayLienTuc_BongLuonKetThucTrenMatDat_KhongBaoGioBienMat()
        {
            // Đây là test bao trùm: bắn đủ kiểu, mô phỏng tới lúc dừng, và đòi hỏi
            // bóng không bao giờ nằm dưới mặt cứng ở bất kỳ bước nào.
            float3[] launches =
            {
                new float3(0f, 8f, 22f),      // sút thẳng vào khung
                new float3(0f, 16f, 14f),     // sút bổng qua xà, lên khán đài
                new float3(9f, 6f, 18f),      // lệch sang cánh
                new float3(-9f, 6f, 18f),
                new float3(0f, 2f, 30f),      // sút căng
                new float3(0f, 20f, 4f),      // gần như thẳng đứng
            };

            foreach (float3 v0 in launches)
            {
                var s = new BallState(new float3(0f, R, 0f), v0, float3.zero);
                bool rested = false;

                for (int i = 0; i < 2400 && !rested; i++)   // 20 giây ở 120Hz
                {
                    s = BallSolver.Step(s, BallParams.Default, Dt);
                    if (PitchCollision.Resolve(in s, Dt, R, out BallState next, out bool atRest))
                        s = next;
                    else if (atRest)
                        rested = true;

                    if (atRest) rested = true;

                    float floor = PitchCollision.SurfaceHeight(s.position.x, s.position.z);
                    Assert.GreaterOrEqual(s.position.y, floor - 0.05f,
                        $"Cú sút {v0} làm bóng lọt xuống dưới mặt cứng ở bước {i} " +
                        $"(y = {s.position.y:F2}, mặt = {floor:F2}). Đây là lỗi 'bóng biến mất'.");
                }

                Assert.IsTrue(rested, $"Cú sút {v0} không bao giờ dừng lại sau 20 giây.");
            }
        }
    }
}
