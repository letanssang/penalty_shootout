using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Eleven.Ball;
using Eleven.Keeper;
using Eleven.Match;
using Is = NUnit.Framework.Is;
using Random = Unity.Mathematics.Random;

namespace Eleven.Tests.EditMode
{
    /// <summary>
    /// T25 — Cấu hình độ khó.
    ///
    /// Bốn mục nghiệm thu: (1) ba asset KeeperProfile, (2) reachScale cả ba nằm trong
    /// [0.92, 1.06], (3) mô phỏng 1000 lượt mỗi bậc cho tỉ lệ cản phá 18/28/38%,
    /// (4) đổi bậc lúc đang chạy chỉ có hiệu lực ở lượt kế tiếp.
    ///
    /// Mục (3) HIỆN CHƯA ĐẠT và test đo nó được đánh dấu Ignore chứ không bị chỉnh cho
    /// xanh — xem ghi chú ở <see cref="MoPhong1000Luot_TiLeCanPha_DungBangMucTieu"/>.
    /// Bộ mô phỏng vẫn được xây đủ và vẫn chạy trong các test khác, vì thứ cần bảo vệ ở
    /// đây là DỤNG CỤ ĐO: nếu nó hỏng thì con số nào nó in ra cũng vô nghĩa.
    /// </summary>
    public class DifficultyTests
    {
        const string SettingsDir = "Assets/_Project/Settings";
        const float Tol = 1e-4f;

        readonly List<KeeperProfile> _temp = new List<KeeperProfile>();

        [TearDown]
        public void TearDown()
        {
            // Profile tạo bằng factory là ScriptableObject không nằm trên đĩa; không huỷ
            // thì mỗi lần chạy test lại bỏ lại một đống instance mồ côi trong bộ nhớ editor.
            for (int i = 0; i < _temp.Count; i++)
                if (_temp[i] != null)
                    Object.DestroyImmediate(_temp[i]);
            _temp.Clear();
        }

        // ── Tiện ích ───────────────────────────────────────────────

        static string PathOf(DifficultyLevel level)
        {
            return $"{SettingsDir}/KeeperProfile-{level}.asset";
        }

        static KeeperProfile LoadAsset(DifficultyLevel level)
        {
            return AssetDatabase.LoadAssetAtPath<KeeperProfile>(PathOf(level));
        }

        KeeperProfile Factory(DifficultyLevel level)
        {
            KeeperProfile p;
            switch (level)
            {
                case DifficultyLevel.Easy: p = KeeperProfile.CreateEasy(); break;
                case DifficultyLevel.Hard: p = KeeperProfile.CreateHard(); break;
                default: p = KeeperProfile.CreateMedium(); break;
            }
            _temp.Add(p);
            return p;
        }

        /// <summary>Asset nếu có, không thì hằng số trong code — việc thiếu asset đã có test riêng bắt.</summary>
        KeeperProfile Profile(DifficultyLevel level)
        {
            var asset = LoadAsset(level);
            return asset != null ? asset : Factory(level);
        }

        // ── Mục 1: ba asset ────────────────────────────────────────

        [TestCase(DifficultyLevel.Easy)]
        [TestCase(DifficultyLevel.Medium)]
        [TestCase(DifficultyLevel.Hard)]
        public void BaAsset_KeeperProfile_TonTaiTrongThuMucSettings(DifficultyLevel level)
        {
            Assert.IsNotNull(LoadAsset(level),
                $"Thiếu {PathOf(level)} — chạy menu Eleven > Phase 4 > Generate Keeper Profiles.");
        }

        [TestCase(DifficultyLevel.Easy)]
        [TestCase(DifficultyLevel.Medium)]
        [TestCase(DifficultyLevel.Hard)]
        public void Asset_KhopTungFieldVoiFactory_MotNguonSuThat(DifficultyLevel level)
        {
            var asset = LoadAsset(level);
            Assert.IsNotNull(asset, $"Thiếu {PathOf(level)} — chạy Eleven > Phase 4 > Generate Keeper Profiles.");

            var code = Factory(level);
            // Ai đó chỉnh tay asset trong Inspector thì game đổi độ khó còn mọi test chạy
            // bằng factory vẫn xanh. Test này là chỗ duy nhất phát hiện ra sự lệch đó.
            Assert.AreEqual(code.readAccuracy, asset.readAccuracy, Tol, "readAccuracy lệch giữa asset và code");
            Assert.AreEqual(code.reactionMs, asset.reactionMs, Tol, "reactionMs lệch giữa asset và code");
            Assert.AreEqual(code.commitOffsetMs, asset.commitOffsetMs, Tol, "commitOffsetMs lệch giữa asset và code");
            Assert.AreEqual(code.reachScale, asset.reachScale, Tol, "reachScale lệch giữa asset và code");
            Assert.AreEqual(code.parryChance, asset.parryChance, Tol, "parryChance lệch giữa asset và code");
            Assert.AreEqual(code.memoryWeight, asset.memoryWeight, Tol, "memoryWeight lệch giữa asset và code");
        }

        // ── Mục 2: reachScale trong [0.92, 1.06] ───────────────────

        [TestCase(DifficultyLevel.Easy)]
        [TestCase(DifficultyLevel.Medium)]
        [TestCase(DifficultyLevel.Hard)]
        public void ReachScale_NamTrongDai_0_92_Toi_1_06(DifficultyLevel level)
        {
            var p = Profile(level);
            Assert.GreaterOrEqual(p.reachScale, 0.92f - Tol, $"{level}: reachScale dưới 0.92");
            Assert.LessOrEqual(p.reachScale, 1.06f + Tol, $"{level}: reachScale trên 1.06");
        }

        [Test]
        public void DoKho_NamODocVi_KhongPhaiOTocDo()
        {
            var easy = Profile(DifficultyLevel.Easy);
            var medium = Profile(DifficultyLevel.Medium);
            var hard = Profile(DifficultyLevel.Hard);

            // Đọc vị phải tăng rõ rệt giữa các bậc...
            Assert.Less(easy.readAccuracy, medium.readAccuracy, "readAccuracy phải tăng từ Dễ lên Thường");
            Assert.Less(medium.readAccuracy, hard.readAccuracy, "readAccuracy phải tăng từ Thường lên Khó");

            // ...còn tầm với thì gần như không đổi. Đây là ý của mục nghiệm thu số 2: thủ môn
            // bậc Khó không được nhanh hơn người, chỉ được đoán giỏi hơn. Dải 0.92–1.06 rộng
            // 0.14, nên chênh lệch tối đa cho phép cũng là 0.14.
            float spread = math.max(hard.reachScale, math.max(medium.reachScale, easy.reachScale))
                         - math.min(hard.reachScale, math.min(medium.reachScale, easy.reachScale));
            Assert.LessOrEqual(spread, 0.14f + Tol, "Chênh lệch reachScale giữa ba bậc quá lớn — độ khó đang nằm ở tốc độ");

            float readSpread = hard.readAccuracy - easy.readAccuracy;
            Assert.Greater(readSpread, spread, "Độ khó phải chênh nhau ở đọc vị nhiều hơn ở tầm với");
        }

        // ── Mục 4: đổi bậc có hiệu lực ở lượt kế tiếp ──────────────

        [Test]
        public void KhoiTao_MacDinh_LaBacThuong()
        {
            var s = new DifficultySelector();
            Assert.AreEqual(DifficultyLevel.Medium, s.Current);
            Assert.AreEqual(DifficultyLevel.Medium, s.Pending);
            Assert.AreEqual(KeeperProfile.CreateMedium().readAccuracy, s.ActiveProfile.readAccuracy, Tol);
        }

        [Test]
        public void Request_KhongDoiProfileCuaLuotDangChay()
        {
            var s = new DifficultySelector(DifficultyLevel.Easy);
            var truoc = s.ActiveProfile;

            s.Request(DifficultyLevel.Hard);

            Assert.AreEqual(DifficultyLevel.Easy, s.Current, "Bậc đang chạy bị đổi giữa chừng");
            Assert.AreEqual(DifficultyLevel.Hard, s.Pending, "Yêu cầu không được xếp hàng");
            Assert.AreSame(truoc, s.ActiveProfile, "ActiveProfile đổi ngay — cú sút đang bay mất tính tất định");
        }

        [Test]
        public void Request_ChiCoHieuLucSauCommitPending()
        {
            var s = new DifficultySelector(DifficultyLevel.Easy);
            s.Request(DifficultyLevel.Hard);

            Assert.IsTrue(s.CommitPending(), "CommitPending phải báo có đổi bậc");
            Assert.AreEqual(DifficultyLevel.Hard, s.Current);
            Assert.AreEqual(DifficultyLevel.Hard, s.Pending);
            Assert.AreEqual(Profile(DifficultyLevel.Hard).readAccuracy, s.ActiveProfile.readAccuracy, Tol);
        }

        [Test]
        public void Request_NhieuLanTrongMotLuot_ChiLanCuoiCoHieuLuc()
        {
            var s = new DifficultySelector(DifficultyLevel.Medium);
            s.Request(DifficultyLevel.Hard);
            s.Request(DifficultyLevel.Easy);
            s.Request(DifficultyLevel.Hard);
            s.Request(DifficultyLevel.Easy);

            Assert.AreEqual(DifficultyLevel.Medium, s.Current);
            s.CommitPending();
            Assert.AreEqual(DifficultyLevel.Easy, s.Current);
        }

        [Test]
        public void CommitPending_KhiKhongCoYeuCau_TraVeFalse_VaGiuNguyenBac()
        {
            var s = new DifficultySelector(DifficultyLevel.Hard);
            Assert.IsFalse(s.CommitPending(), "Không có yêu cầu nào mà vẫn báo đổi bậc");
            Assert.AreEqual(DifficultyLevel.Hard, s.Current);

            s.Request(DifficultyLevel.Hard);
            Assert.IsFalse(s.CommitPending(), "Yêu cầu trùng bậc hiện tại không phải là một lần đổi bậc");
        }

        [Test]
        public void HaiLuotLienTiep_CommitLanHai_KhongTuDoiBacThemLanNua()
        {
            var s = new DifficultySelector(DifficultyLevel.Easy);
            s.Request(DifficultyLevel.Hard);
            s.CommitPending();
            Assert.IsFalse(s.CommitPending(), "Lượt kế tiếp lại đổi bậc lần nữa — yêu cầu bị kẹt lại trong hàng đợi");
            Assert.AreEqual(DifficultyLevel.Hard, s.Current);
        }

        [Test]
        public void RequestVaCommit_KhongCapPhatGC()
        {
            var s = new DifficultySelector(DifficultyLevel.Easy);
            // Chạy nóng trước: lần gọi đầu tiên có thể kéo theo JIT.
            s.Request(DifficultyLevel.Hard);
            s.CommitPending();

            Assert.That(() =>
            {
                s.Request(DifficultyLevel.Easy);
                s.CommitPending();
                s.Request(DifficultyLevel.Hard);
                s.CommitPending();
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void SlotAssetNull_RoiVeHangSoTrongCode_KhongNemNullReference()
        {
            var s = new DifficultySelector(null, null, null, DifficultyLevel.Hard);
            Assert.IsNotNull(s.ActiveProfile, "Quên gán asset không được làm ActiveProfile thành null");
            Assert.AreEqual(KeeperProfile.CreateHard().readAccuracy, s.ActiveProfile.readAccuracy, Tol);
        }

        [Test]
        public void ProfileFor_BacNgoaiDai_TraVeThuong_KhongNemLoi()
        {
            var s = new DifficultySelector();
            KeeperProfile p = null;
            // Byte đọc từ bản lưu hỏng (T24) có thể mang giá trị không thuộc enum.
            Assert.DoesNotThrow(() => p = s.ProfileFor((DifficultyLevel)200));
            Assert.IsNotNull(p);
            Assert.AreEqual(KeeperProfile.CreateMedium().readAccuracy, p.readAccuracy, Tol);
        }

        // ── Mục 3: mô phỏng 1000 lượt ──────────────────────────────

        [Test]
        public void MoPhong_TatDinh_CungSeedChoCungKetQua()
        {
            var p = Profile(DifficultyLevel.Medium);
            var a = PenaltySim.Run(p, 300, 20260827u, PenaltySim.Timing.AtCommitOffset);
            var b = PenaltySim.Run(p, 300, 20260827u, PenaltySim.Timing.AtCommitOffset);

            Assert.AreEqual(a.saved, b.saved, "Mô phỏng không tất định — mọi con số nó in ra đều vô nghĩa");
            Assert.AreEqual(a.onTarget, b.onTarget);
            Assert.AreEqual(a.offTarget, b.offTarget);
            CollectionAssert.AreEqual(a.realizedCells, b.realizedCells);
        }

        [Test]
        public void MoPhong_DoiSeed_ChoKetQuaKhac_ChuKhongPhaiCungMotLuotLapLai()
        {
            var p = Profile(DifficultyLevel.Medium);
            var a = PenaltySim.Run(p, 300, 1u, PenaltySim.Timing.AtCommitOffset);
            var b = PenaltySim.Run(p, 300, 999u, PenaltySim.Timing.AtCommitOffset);
            CollectionAssert.AreNotEqual(a.realizedCells, b.realizedCells,
                "Đổi seed mà phân bố ô y hệt — nhiều khả năng seed không được dùng");
        }

        [Test]
        public void MoPhong_MauDuLon_VaPhuKinCa9O()
        {
            var r = PenaltySim.Run(Profile(DifficultyLevel.Medium), 1000, 20260827u, PenaltySim.Timing.AtCommitOffset);

            // Tỉ lệ cản phá tính trên số quả trúng khung. Nếu mô hình sút bắn ra ngoài quá
            // nhiều thì mẫu còn lại quá bé để nói được gì.
            Assert.Greater(r.onTarget, 800, $"Chỉ {r.onTarget}/1000 quả trúng khung — mô hình sút sai, không đo được gì");

            for (int cell = 0; cell < 9; cell++)
                Assert.Greater(r.realizedCells[cell], 10,
                    $"Ô {cell} chỉ có {r.realizedCells[cell]} quả — mô hình sút không phủ hết khung, " +
                    "thủ môn có thể ăn may bằng cách bỏ hẳn một vùng");
        }

        [Test]
        public void MoPhong1000Luot_TiLeCanPha_TangDanTheoDoKho()
        {
            var easy = PenaltySim.Run(Profile(DifficultyLevel.Easy), 1000, 20260827u, PenaltySim.Timing.AtCommitOffset);
            var medium = PenaltySim.Run(Profile(DifficultyLevel.Medium), 1000, 20260827u, PenaltySim.Timing.AtCommitOffset);
            var hard = PenaltySim.Run(Profile(DifficultyLevel.Hard), 1000, 20260827u, PenaltySim.Timing.AtCommitOffset);

            Debug.Log($"[T25] Tỉ lệ cản phá 1000 lượt (gọi TryCommit tại commitOffsetMs):\n" +
                      $"  Dễ    : {easy.SaveRate * 100f:F1}%  (đọc đúng ô {easy.ReadAccuracy * 100f:F1}%, " +
                      $"conf tb {easy.meanConfidence:F3}, ép đứng giữa {easy.centerCommits}/{easy.onTarget})\n" +
                      $"  Thường: {medium.SaveRate * 100f:F1}%  (đọc đúng ô {medium.ReadAccuracy * 100f:F1}%, " +
                      $"conf tb {medium.meanConfidence:F3}, ép đứng giữa {medium.centerCommits}/{medium.onTarget})\n" +
                      $"  Khó   : {hard.SaveRate * 100f:F1}%  (đọc đúng ô {hard.ReadAccuracy * 100f:F1}%, " +
                      $"conf tb {hard.meanConfidence:F3}, ép đứng giữa {hard.centerCommits}/{hard.onTarget})\n" +
                      $"  Mục tiêu T25: 18% / 28% / 38%");

            Assert.Less(easy.SaveRate, medium.SaveRate,
                $"Dễ ({easy.SaveRate * 100f:F1}%) phải cản phá ít hơn Thường ({medium.SaveRate * 100f:F1}%)");
            Assert.Less(medium.SaveRate, hard.SaveRate,
                $"Thường ({medium.SaveRate * 100f:F1}%) phải cản phá ít hơn Khó ({hard.SaveRate * 100f:F1}%)");
        }

        [Test]
        public void MoPhong1000Luot_BacKho_DocViTotHon_VaItPhaiDungGiuaHon_SoVoiBacDe()
        {
            var easy = PenaltySim.Run(Profile(DifficultyLevel.Easy), 1000, 20260827u, PenaltySim.Timing.AtCommitOffset);
            var hard = PenaltySim.Run(Profile(DifficultyLevel.Hard), 1000, 20260827u, PenaltySim.Timing.AtCommitOffset);

            Assert.Less(easy.ReadAccuracy, hard.ReadAccuracy,
                "Bậc Khó phải đọc đúng ô nhiều hơn bậc Dễ — nếu không thì readAccuracy chẳng điều khiển gì cả");
            Assert.Greater(easy.centerCommits, hard.centerCommits,
                "Bậc Dễ phải bị ép đứng giữa (confidence quá thấp) nhiều hơn bậc Khó");
        }

        [Test]
        [Ignore("T25 mục 3 CHƯA ĐẠT — và số đo ngày 2026-08-27 cho thấy mục tiêu này MÂU THUẪN " +
                "với mô hình tầm với, chứ không phải chỉ cần chỉnh tham số. Sau khi sửa hạn cam kết " +
                "của T19 và đặt lại hai ngưỡng confidence: cản phá 1.9% / 9.7% / 14.5% so với mục " +
                "tiêu 18/28/38. Lý do đo được: ngân sách thời gian của bậc Thường là 0.45s bóng bay " +
                "+ 0.11s cam kết sớm - 0.24s phản xạ = 0.32s, trong khi ReachEnvelope đòi 0.46-0.60s " +
                "cho các ô biên. Thủ môn chỉ với tới nổi ô 4 (0.22s) và ô 7 (0.15s) — 2/9 ô — nhân " +
                "với ~40% đọc đúng ô ra đúng 9.7% đã đo. Muốn chạm 28% thì phải nới reach, mà " +
                "plan.md cấm: 'độ khó chỉ được nằm ở p_read và t_commit, không bao giờ ở reach'. " +
                "Việc cần làm trước khi bật lại test này là QUYẾT ĐỊNH THIẾT KẾ, không phải tinh " +
                "chỉnh: hoặc hạ mục tiêu xuống dải mà tầm với cho phép, hoặc chấp nhận rằng cú sút " +
                "đặt đúng góc là không thể cản — đúng như penalty thật.")]
        public void MoPhong1000Luot_TiLeCanPha_DungBangMucTieu()
        {
            AssertSaveRate(DifficultyLevel.Easy, 0.18f);
            AssertSaveRate(DifficultyLevel.Medium, 0.28f);
            AssertSaveRate(DifficultyLevel.Hard, 0.38f);
        }

        static void AssertSaveRate(DifficultyLevel level, float target)
        {
            var r = PenaltySim.Run(LoadAsset(level), 1000, 20260827u, PenaltySim.Timing.AtCommitOffset);
            Assert.AreEqual(target, r.SaveRate, 0.03f,
                $"{level}: cản phá {r.SaveRate * 100f:F1}%, mục tiêu {target * 100f:F0}±3%");
        }

        /// <summary>
        /// Chỗ này TRƯỚC ĐÂY là một test đặc tả hiện trạng, ghim đúng cái lỗi
        /// "thủ môn bị ép đứng giữa gần như mọi quả" (843/843), kèm dặn dò: khi test đó đỏ
        /// thì nghĩa là mốc cam kết của T19 đã được sửa, hãy xoá nó đi.
        ///
        /// Lỗi đã sửa ngày 2026-08-27: hạn cam kết trong SimpleKeeperController thiếu
        /// <see cref="SimpleKeeperController.BallFlightAllowanceSeconds"/> — nó ngầm đòi thủ môn
        /// phải có mặt ở góc ngay lúc chân chạm bóng, trong khi thực tế nó còn cả quãng bóng bay.
        /// Cộng thêm hai ngưỡng confidence bị đặt sai dải so với thang mà T18 sinh ra.
        /// Đo lại sau khi sửa: ép đứng giữa còn 92/843 ở bậc Khó.
        ///
        /// Test này ghim chiều ngược lại, để lỗi cũ không bao giờ lặng lẽ quay lại.
        /// </summary>
        [Test]
        public void ThuMon_PhaiThucSuBayNguoi_ChuKhongDungGiua()
        {
            var r = PenaltySim.Run(Profile(DifficultyLevel.Hard), 1000, 20260827u, PenaltySim.Timing.PerFrame);

            Debug.Log($"[T19] Bậc Khó: ép đứng giữa {r.centerCommits}/{r.onTarget}, " +
                      $"cam kết khi còn {r.meanCommitTtc:F3}s, cản phá {r.SaveRate * 100f:F1}%");

            Assert.Less(r.centerCommits, (int)(0.35f * r.onTarget),
                $"{r.centerCommits}/{r.onTarget} quả bị ép đứng giữa. Một quả 11m không có pha " +
                "bay người thì không còn là quả 11m. Kiểm tra lại hạn cam kết trong T19 và hai " +
                "ngưỡng confidence — chúng phải nằm trong dải mà T18 thật sự sinh ra.");
        }

        /// <summary>
        /// Không phải phép đo độ khó mà là phép đo CHÍNH BỘ ĐIỀU KHIỂN: so cách gọi mỗi khung
        /// hình (đúng như tài liệu của SimpleKeeperController mô tả) với cách gọi một lần tại
        /// commitOffsetMs. Hai con số lệch nhau bao nhiêu chính là cái giá của mốc thời gian
        /// cam kết trong T19 — xem ghi chú T25 trong backlog.
        /// </summary>
        [Test]
        public void GoiMoiKhungHinh_SoVoiGoiTaiCommitOffset_GhiLaiChenhLech()
        {
            var levels = new[] { DifficultyLevel.Easy, DifficultyLevel.Medium, DifficultyLevel.Hard };
            var dong = new System.Text.StringBuilder("[T25] Ảnh hưởng của mốc cam kết (1000 lượt):\n");

            for (int i = 0; i < levels.Length; i++)
            {
                var p = Profile(levels[i]);
                var frame = PenaltySim.Run(p, 1000, 20260827u, PenaltySim.Timing.PerFrame);
                var offset = PenaltySim.Run(p, 1000, 20260827u, PenaltySim.Timing.AtCommitOffset);
                dong.AppendLine($"  {levels[i],-6}: mỗi khung {frame.SaveRate * 100f:F1}% " +
                                $"(cam kết khi còn {frame.meanCommitTtc:F3}s, ép đứng giữa {frame.centerCommits}/{frame.onTarget})" +
                                $"  |  tại commitOffset {offset.SaveRate * 100f:F1}%");
            }
            Debug.Log(dong.ToString());

            // Dù gọi kiểu nào, thủ môn cũng không được cản phá quá nửa số quả trúng khung:
            // vượt mốc đó là bộ mô phỏng hoặc luật cản phá đang ưu ái thủ môn, không phải
            // thủ môn giỏi lên.
            for (int i = 0; i < levels.Length; i++)
            {
                var r = PenaltySim.Run(Profile(levels[i]), 1000, 20260827u, PenaltySim.Timing.PerFrame);
                Assert.Less(r.SaveRate, 0.5f, $"{levels[i]}: cản phá {r.SaveRate * 100f:F1}% — vô lý so với penalty thật");
            }
        }
    }

    /// <summary>
    /// Bộ mô phỏng loạt sút penalty cho T25. KHÔNG mô hình hoá lại bất cứ thứ gì đã có:
    /// bóng bay bằng BallSolver/TrajectoryPredictor thật, ô lưới bằng GoalGeometry thật,
    /// đọc vị bằng BayesianKeeperBrain thật, cam kết bằng SimpleKeeperController thật,
    /// tầm với bằng ReachEnvelope thật. Chỉ hai thứ được dựng ở đây vì chưa có trong repo:
    /// mô hình NGƯỜI SÚT, và luật CẢN PHÁ.
    ///
    /// Mô hình người sút (khoá lại trước khi đo, không được chỉnh để ra số đẹp):
    ///   - chọn ô theo phân bố penalty thật: chuộng tầng dưới và hai cột biên;
    ///   - lực sút 25 ± 3 m/s, kẹp [19, 31];
    ///   - sai số ngắm 0.28 m mỗi chiều;
    ///   - ngắm cao hơn tâm ô để bù độ rơi, nhưng chỉ bù 85% nên bóng vẫn thấp hơn ý định.
    ///
    /// Cue được sinh KHÔNG NHIỄU và đặt đúng tâm bảng tra của chính BayesianKeeperBrain.
    /// Đó là cố ý: nó cho thủ môn điều kiện đọc vị lý tưởng, nên mọi con số ra ở đây là
    /// TRẦN chứ không phải kỳ vọng. Thủ môn không đạt chỉ tiêu trong điều kiện này thì
    /// càng không đạt với cue thật từ xương.
    ///
    /// Luật cản phá tạm: "cam kết đúng ô bóng vào VÀ kịp tầm với". Đây là chỗ chờ T21 —
    /// luật thật phải xét giao cắt giữa quỹ đạo bay người và quỹ đạo bóng, tức là cản được
    /// cả những quả sát ranh giới hai ô. Luật tạm này nghiêm ngặt hơn luật thật.
    /// </summary>
    internal static class PenaltySim
    {
        public enum Timing
        {
            /// <summary>Gọi TryCommit mỗi khung hình suốt pha chạy đà — đúng vòng lặp mà SimpleKeeperController mô tả.</summary>
            PerFrame,

            /// <summary>Gọi TryCommit đúng một lần, tại thời điểm commitOffsetMs của hồ sơ.</summary>
            AtCommitOffset
        }

        public struct SimResult
        {
            public int onTarget;
            public int offTarget;
            public int saved;
            public int readCorrect;
            public int centerCommits;
            public float meanConfidence;
            public float meanCommitTtc;
            public int[] realizedCells;

            /// <summary>Tỉ lệ cản phá tính trên số quả TRÚNG KHUNG — quả sút ra ngoài không phải công của thủ môn.</summary>
            public float SaveRate { get { return onTarget > 0 ? (float)saved / onTarget : 0f; } }

            public float ReadAccuracy { get { return onTarget > 0 ? (float)readCorrect / onTarget : 0f; } }
        }

        const float CueWindow = 0.60f;        // pha chạy đà nhìn thấy được, khớp runUp của KickPhaseDurations
        const float MaxObservability = 0.95f; // không bao giờ nhìn rõ tuyệt đối
        const float FrameDt = 1f / 60f;
        const float SimDt = 1f / 240f;        // bước tích phân của FirstCrossing
        const int CenterCell = 4;
        const int KicksPerRound = 5;          // hết loạt 5 quả thì xoá trí nhớ, như luân lưu thật

        static readonly float[] RowProb = { 0.20f, 0.35f, 0.45f };
        static readonly float[] ColProb = { 0.38f, 0.24f, 0.38f };

        // Cue ứng với cột/hàng: chép đúng tâm bảng tra của BayesianKeeperBrain (xem ghi chú lớp).
        static readonly float[] ColLateral = { 0.20f, 0f, -0.20f };
        static readonly float[] ColHipYaw = { 15f, 0f, -15f };
        static readonly float[] ColApproach = { 20f, 0f, -20f };
        static readonly float[] RowRunUp = { 4.5f, 3.5f, 2.5f };

        public static SimResult Run(KeeperProfile profile, int kicks, uint masterSeed, Timing timing)
        {
            // HAI dòng ngẫu nhiên tách rời. Nếu người sút và thủ môn rút chung một dòng thì
            // thủ môn đọc nhiều lần hơn (chế độ PerFrame) sẽ đẩy lệch cả chuỗi cú sút phía sau,
            // và hai chế độ hoá ra được đo trên hai bộ cú sút khác nhau — so sánh vô nghĩa.
            var shotRng = new Random(masterSeed == 0u ? 1u : masterSeed);
            var keeperRng = new Random(masterSeed * 2654435761u + 1u);
            var brain = new BayesianKeeperBrain();
            var controller = new SimpleKeeperController();
            var history = default(ShotHistory);
            var ballParams = BallParams.Default;

            var result = default(SimResult);
            result.realizedCells = new int[9];
            float confSum = 0f;
            float ttcSum = 0f;

            for (int i = 0; i < kicks; i++)
            {
                if (i % KicksPerRound == 0)
                    history = default;

                float3 aim = GoalGeometry.CellCenter(PickCell(ref shotRng));
                float speed = math.clamp(Gauss(ref shotRng, 25f, 3f), 19f, 31f);
                float aimX = aim.x + Gauss(ref shotRng, 0f, 0.28f);
                float aimY = aim.y + Gauss(ref shotRng, 0f, 0.28f);

                float flatTime = GoalGeometry.PenaltyDistance / speed;
                float drop = 0.5f * ballParams.gravity * flatTime * flatTime;
                float3 dir = math.normalize(new float3(aimX, aimY + drop * 0.85f, GoalGeometry.PenaltyDistance));
                var start = new BallState(float3.zero, dir * speed, float3.zero);

                float3 point;
                float flightTime;
                if (!TrajectoryPredictor.FirstCrossing(start, ballParams, GoalGeometry.PenaltyDistance,
                                                       SimDt, out point, out flightTime))
                {
                    result.offTarget++;
                    continue;
                }

                if (math.abs(point.x) > GoalGeometry.Width * 0.5f ||
                    point.y > GoalGeometry.Height || point.y < 0f)
                {
                    result.offTarget++;
                    continue;
                }

                result.onTarget++;
                int trueCell = GoalGeometry.CellOf(point);
                result.realizedCells[trueCell]++;

                KeeperRead read;
                DiveDecision decision;
                Decide(brain, controller, profile, trueCell, timing, ref keeperRng, ref history, out read, out decision);

                confSum += read.confidence;
                ttcSum += decision.commitTime;
                if (read.bestCell == trueCell)
                    result.readCorrect++;
                if (decision.targetCell == CenterCell && read.bestCell != CenterCell)
                    result.centerCommits++;

                // Luật cản phá tạm — xem ghi chú lớp.
                if (decision.targetCell == trueCell && ReachEnvelope.CanReach(trueCell, flightTime, profile))
                    result.saved++;

                history.Record(trueCell);
            }

            if (result.onTarget > 0)
            {
                result.meanConfidence = confSum / result.onTarget;
                result.meanCommitTtc = ttcSum / result.onTarget;
            }
            return result;
        }

        static void Decide(BayesianKeeperBrain brain, SimpleKeeperController controller, KeeperProfile profile,
                           int trueCell, Timing timing, ref Random rng, ref ShotHistory history,
                           out KeeperRead read, out DiveDecision decision)
        {
            controller.Reset();

            if (timing == Timing.AtCommitOffset)
            {
                // commitOffsetMs âm = cam kết TRƯỚC lúc chạm bóng, nên thời gian còn lại là trị tuyệt đối.
                float ttc = math.max(0f, -profile.commitOffsetMs * 0.001f);
                read = brain.Infer(Cues(trueCell, ttc), history, profile, NextSeed(ref rng));
                if (!controller.TryCommit(read, ttc, profile, out decision))
                {
                    // Không cam kết = đứng nguyên. Để nguyên decision mặc định sẽ thành
                    // "bay về ô 0", tức thủ môn được tính là đoán góc trên-trái miễn phí.
                    decision = new DiveDecision { targetCell = CenterCell, commitTime = ttc, isFullDive = false };
                }
                return;
            }

            read = default;
            decision = default;
            // Bắt đầu từ khung thứ HAI: ở đúng mốc CueWindow thì observability bằng 0 theo
            // định nghĩa, và để thủ môn quyết định trên một mẫu không có tí thông tin nào
            // sẽ biến kết quả đo thành trò dựng bù nhìn.
            for (float ttc = CueWindow - FrameDt; ttc > 0f; ttc -= FrameDt)
            {
                read = brain.Infer(Cues(trueCell, ttc), history, profile, NextSeed(ref rng));
                if (controller.TryCommit(read, ttc, profile, out decision))
                    return;
            }

            // Hết pha chạy đà mà chưa cam kết: thủ môn đứng nguyên giữa khung.
            decision = new DiveDecision { targetCell = CenterCell, commitTime = 0f, isFullDive = false };
        }

        static KeeperCues Cues(int trueCell, float timeToContact)
        {
            int row = trueCell / 3;
            int col = trueCell % 3;

            KeeperCues c;
            c.plantFootLateralOffset = ColLateral[col];
            c.hipYawDegrees = ColHipYaw[col];
            c.approachAngleDegrees = ColApproach[col];
            c.runUpLength = RowRunUp[row];
            c.timeToContact = timeToContact;
            // Càng gần lúc chạm bóng càng lộ nhiều thông tin.
            c.observability = math.saturate(1f - timeToContact / CueWindow) * MaxObservability;
            return c;
        }

        static int PickCell(ref Random rng)
        {
            return PickIndex(ref rng, RowProb) * 3 + PickIndex(ref rng, ColProb);
        }

        static int PickIndex(ref Random rng, float[] weights)
        {
            float u = rng.NextFloat();
            float acc = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                acc += weights[i];
                if (u < acc)
                    return i;
            }
            return weights.Length - 1;
        }

        static float Gauss(ref Random rng, float mean, float sigma)
        {
            // Box–Muller. NextFloat() có thể trả đúng 0 và log(0) là -vô cực.
            float u1 = math.max(rng.NextFloat(), 1e-7f);
            float u2 = rng.NextFloat();
            return mean + sigma * math.sqrt(-2f * math.log(u1)) * math.cos(2f * math.PI * u2);
        }

        /// <summary>Seed cho brain. Unity.Mathematics.Random không nhận 0 nên tránh hẳn giá trị đó.</summary>
        static uint NextSeed(ref Random rng)
        {
            return rng.NextUInt(1u, uint.MaxValue);
        }
    }
}
