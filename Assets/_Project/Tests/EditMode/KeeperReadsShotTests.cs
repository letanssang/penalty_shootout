using NUnit.Framework;
using Unity.Mathematics;
using Eleven.Keeper;

namespace Eleven.Tests.EditMode
{
    /// <summary>
    /// ĐO xem thủ môn có THẬT SỰ đọc được hướng sút hay không.
    ///
    /// Vì sao cần bộ test này: mọi mảnh của Phase 3 đều có test riêng và đều xanh, nhưng khi
    /// ghép vào scene thật thì thủ môn đứng giữa ở MỌI lượt (đo trên Pixel 7, 2026-08-27:
    /// "ô 4 | tin cậy 0.06" lặp lại không đổi). Lỗi không nằm trong bất kỳ mảnh nào — nó nằm
    /// ở ĐỘ LỚN của tín hiệu mà vòng lặp trận đấu bơm vào. Không có test nào canh chỗ đó.
    ///
    /// Bộ test này canh đúng chỗ đó: dựng lại bộ tín hiệu MÀ MatchGameLoop sinh ra tại đúng
    /// khoảnh khắc SimpleKeeperController cam kết, rồi đo tỉ lệ đọc đúng CỘT.
    /// Đoán mò = 33%.
    /// </summary>
    public class KeeperReadsShotTests
    {
        // ── Số liệu lấy từ MatchGameLoop, giữ đồng bộ bằng tay ────────────────
        const float RunUpSeconds = 1.30f;
        const float IdealContactFraction = 0.80f;
        const float LateralShiftMax = 0.28f;   // _aimLateralShift
        const float HipYawMax = 18f;           // SetAimYawDegrees(-lateral * 18)
        const float RunUpDistance = 2.5f;      // quãng đường chạy đà của KickerAvatar
        const float FootOffsetX = -0.10f;      // chân trụ lệch so với gốc người sút

        static float IdealContactTime => RunUpSeconds * IdealContactFraction;

        /// <summary>
        /// Bộ tín hiệu tại khoảnh khắc cam kết, dựng theo đúng công thức của MatchGameLoop.
        /// lateral01: -1 = ngắm sát cột trái (cột 0), 0 = giữa, +1 = sát cột phải (cột 2).
        /// </summary>
        static KeeperCues CuesAtCommit(float lateral01, float timeToContact)
        {
            float elapsed = math.max(0f, IdealContactTime - timeToContact);
            float t01 = math.saturate(elapsed / RunUpSeconds);

            // Dạt ngang lộ dần theo sqrt(t01); dấu NGƯỢC hướng bóng (quy ước T18).
            float reveal = -lateral01 * LateralShiftMax * math.sqrt(t01);

            // Mốc 0 đặt lại mỗi khung: tín hiệu còn lại đúng bằng phần dạt do ý đồ sút.
            float lateralOffset = reveal;

            float hipYaw = -lateral01 * HipYawMax;

            // Góc chạy đà: gốc người sút đã dạt, còn mốc thì không → dx = footOffset - reveal.
            float easedPath = t01 * t01;
            float rootZ = math.lerp(-2.6f, -0.15f, easedPath);
            float dx = FootOffsetX - reveal;
            float approach = math.degrees(math.atan2(dx, -rootZ));

            float runUpLength = easedPath * RunUpDistance;
            float observability = math.saturate(1f - timeToContact / RunUpSeconds);

            return new KeeperCues
            {
                plantFootLateralOffset = lateralOffset,
                hipYawDegrees = hipYaw,
                approachAngleDegrees = approach,
                runUpLength = runUpLength,
                timeToContact = timeToContact,
                observability = observability
            };
        }

        /// <summary>Cột lưới (0 = trái, 1 = giữa, 2 = phải) mà một lateral01 nhắm tới.</summary>
        static int ColumnFor(float lateral01) => lateral01 < -0.33f ? 0 : (lateral01 > 0.33f ? 2 : 1);

        /// <summary>
        /// Chạy hồ sơ đã cho qua đúng chuỗi não → máy trạng thái, trả tỉ lệ CAM KẾT ĐÚNG CỘT
        /// và độ tin cậy trung bình.
        /// </summary>
        static void Measure(KeeperProfile profile, out float columnAccuracy, out float meanConfidence,
                            out float centreRate)
        {
            const int trialsPerColumn = 200;
            float[] aims = { -0.85f, 0f, 0.85f };

            int correct = 0, total = 0, centre = 0;
            float confSum = 0f;
            var brain = new BayesianKeeperBrain();
            ShotHistory history = default;

            for (int a = 0; a < aims.Length; a++)
            {
                for (int i = 0; i < trialsPerColumn; i++)
                {
                    uint seed = (uint)(1 + a * 7919 + i * 104729);
                    var controller = new SimpleKeeperController();

                    // Bơm thời gian đúng như vòng lặp trận: đọc lại mỗi khung 1/60s cho tới
                    // khi máy trạng thái chịu cam kết.
                    DiveDecision decision = default;
                    bool committed = false;
                    float confidence = 0f;

                    for (float t = IdealContactTime; t > 0f; t -= 1f / 60f)
                    {
                        KeeperCues cues = CuesAtCommit(aims[a], t);
                        KeeperRead read = brain.Infer(cues, history, profile, seed);
                        confidence = read.confidence;

                        if (controller.TryCommit(read, t, profile, out decision))
                        {
                            committed = true;
                            break;
                        }
                    }

                    if (!committed) continue;

                    total++;
                    confSum += confidence;
                    if (decision.targetCell % 3 == ColumnFor(aims[a])) correct++;
                    if (decision.targetCell == 4) centre++;
                }
            }

            columnAccuracy = total > 0 ? (float)correct / total : 0f;
            meanConfidence = total > 0 ? confSum / total : 0f;
            centreRate = total > 0 ? (float)centre / total : 0f;
        }

        [Test]
        public void ThuMon_DocDungCot_TotHonDoanMo()
        {
            Measure(KeeperProfile.CreateMedium(), out float acc, out float conf, out float centre);

            UnityEngine.Debug.Log(
                $"[Thủ môn/Thường] đọc đúng cột {acc:P1} | tin cậy trung bình {conf:F2} | đứng giữa {centre:P1}");

            Assert.Greater(acc, 0.45f,
                $"Thủ môn bậc Thường chỉ đọc đúng cột {acc:P1} — đoán mò đã là 33%. " +
                "Tín hiệu mà MatchGameLoop bơm vào quá yếu hoặc sai dấu, cả Phase 3 thành đồ trang trí.");

            Assert.Less(centre, 0.55f,
                $"Thủ môn đứng giữa {centre:P1} số lượt — không có pha bay người thì quả 11m mất hết kịch tính.");
        }

        [Test]
        public void DoKho_DoiThuMon_ChuKhongChiDoiConSo()
        {
            Measure(KeeperProfile.CreateEasy(), out float easy, out _, out _);
            Measure(KeeperProfile.CreateMedium(), out float medium, out _, out _);
            Measure(KeeperProfile.CreateHard(), out float hard, out _, out _);

            UnityEngine.Debug.Log($"[Thủ môn] đọc đúng cột — Dễ {easy:P1} | Thường {medium:P1} | Khó {hard:P1}");

            Assert.Greater(hard, easy,
                $"Bậc Khó ({hard:P1}) phải đọc vị tốt hơn bậc Dễ ({easy:P1}). " +
                "Nếu bằng nhau thì ba nút độ khó chỉ là ba cái nhãn.");
        }

        [Test]
        public void ThuMon_KhongThienLechMotBen()
        {
            var profile = KeeperProfile.CreateMedium();
            var brain = new BayesianKeeperBrain();
            ShotHistory history = default;

            int left = 0, right = 0;
            for (int i = 0; i < 300; i++)
            {
                uint seed = (uint)(1 + i * 104729);
                var controller = new SimpleKeeperController();

                for (float t = IdealContactTime; t > 0f; t -= 1f / 60f)
                {
                    KeeperCues cues = CuesAtCommit(0f, t); // ngắm THẲNG GIỮA
                    KeeperRead read = brain.Infer(cues, history, profile, seed);
                    if (controller.TryCommit(read, t, profile, out DiveDecision d))
                    {
                        if (d.targetCell % 3 == 0) left++;
                        else if (d.targetCell % 3 == 2) right++;
                        break;
                    }
                }
            }

            int spread = math.abs(left - right);
            UnityEngine.Debug.Log($"[Thủ môn/ngắm giữa] đổ trái {left} | đổ phải {right}");

            Assert.Less(spread, 60,
                $"Ngắm thẳng giữa mà thủ môn đổ trái {left} lần, đổ phải {right} lần — " +
                "chênh lệch này nghĩa là có một thiên lệch hằng số trong chuỗi tín hiệu.");
        }
    }
}
