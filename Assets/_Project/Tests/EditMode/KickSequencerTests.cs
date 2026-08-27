using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;   // gop extension AllocatingGCMemory()
using Is = NUnit.Framework.Is;             // khu nhap nhang voi lop Is cua Unity
using Unity.Mathematics;
using Eleven.Match;
using Eleven.Shooter;

namespace Eleven.Tests.EditMode
{
    /// <summary>
    /// T23 — Máy trạng thái lượt sút (<see cref="KickSequencer"/>).
    ///
    /// Bộ test này viết theo ĐÚNG 5 mục nghiệm thu trong docs/backlog/phase-4-tran-dau.md,
    /// mỗi mục là một region:
    ///   1. Mỗi lượt nhận một seed, ghi lại được để tái hiện
    ///   2. Chuyển pha không bao giờ nhảy cóc
    ///   3. Thoát app giữa pha Flight rồi vào lại không hỏng trạng thái
    ///   4. 200 lượt liên tiếp không tăng bộ nhớ
    ///   5. Abort() ở bất kỳ pha nào đều về Complete sạch sẽ
    ///
    /// Nguyên tắc: test KHÔNG được tin lời cài đặt. Chuỗi pha luôn được kiểm bằng cách ghi
    /// lại TOÀN BỘ sự kiện OnPhaseChanged rồi so với chuỗi mong đợi, chứ không chỉ xem
    /// pha cuối cùng — pha cuối đúng mà đường đi sai thì vẫn là hỏng.
    /// </summary>
    [TestFixture]
    public class KickSequencerTests
    {
        // ───────────────────────── helpers ─────────────────────────

        /// <summary>Tám pha, mỗi pha đúng một lần — dùng để duyệt "ở MỌI pha thì...".</summary>
        static readonly KickPhase[] AllPhases =
        {
            KickPhase.Placing, KickPhase.Aiming, KickPhase.RunUp, KickPhase.Contact,
            KickPhase.Flight, KickPhase.Resolution, KickPhase.Reaction, KickPhase.Complete
        };

        /// <summary>
        /// Đường đi ĐẦY ĐỦ của một lượt tính từ trạng thái nghỉ: 9 mốc = 8 bước chuyển,
        /// kể cả bước Complete→Placing do BeginKick tạo ra.
        /// </summary>
        static readonly KickPhase[] FullPath =
        {
            KickPhase.Complete,
            KickPhase.Placing, KickPhase.Aiming, KickPhase.RunUp, KickPhase.Contact,
            KickPhase.Flight, KickPhase.Resolution, KickPhase.Reaction, KickPhase.Complete
        };

        const int BuocMoiLuot = 8;

        /// <summary>
        /// Sổ ghi sự kiện. Ghi cả pha đọc được TỪ BÊN TRONG handler để bắt lỗi
        /// "bắn sự kiện trước khi cập nhật trạng thái".
        /// </summary>
        sealed class PhaseLog
        {
            public readonly List<KickPhase> From = new List<KickPhase>();
            public readonly List<KickPhase> To = new List<KickPhase>();
            public readonly List<KickPhase> PhaseSeenInsideHandler = new List<KickPhase>();
            public KickSequencer Watched;

            public int Count { get { return From.Count; } }

            public void Handle(KickPhase from, KickPhase to)
            {
                From.Add(from);
                To.Add(to);
                if (Watched != null) PhaseSeenInsideHandler.Add(Watched.Phase);
            }

            public void Clear()
            {
                From.Clear();
                To.Clear();
                PhaseSeenInsideHandler.Clear();
            }

            public string Dump()
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < From.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(From[i]).Append("->").Append(To[i]);
                }
                return sb.Length == 0 ? "(không có sự kiện nào)" : sb.ToString();
            }
        }

        static PhaseLog Attach(KickSequencer s)
        {
            var log = new PhaseLog { Watched = s };
            s.OnPhaseChanged += log.Handle;
            return log;
        }

        static ShotIntent MakeIntent(float speed)
        {
            return new ShotIntent
            {
                aimPoint = new float3(1.25f, 1.60f, 11f),
                spin = new float3(0f, 32f, 0f),
                speed = speed,
                type = ShotType.InsideFoot,
                quality = 0.83f,
                unstable = false,
                scatterRadius = 0.12f
            };
        }

        /// <summary>Bơm thời gian từng nhịp nhỏ cho tới khi tới <paramref name="target"/>.</summary>
        static void TickUntil(KickSequencer s, KickPhase target, float dt = 1f / 240f)
        {
            const int maxSteps = 200000;
            int steps = 0;
            while (s.Phase != target)
            {
                s.Tick(dt);
                steps++;
                Assert.Less(steps, maxSteps,
                    $"Không tới được pha {target} sau {maxSteps} nhịp — nghi vòng lặp treo. Đang ở {s.Phase}.");
            }
        }

        /// <summary>Dựng một sequencer đang đứng đúng ở <paramref name="target"/>.</summary>
        static KickSequencer DriveTo(KickPhase target, uint seed = 4242u)
        {
            var s = new KickSequencer();
            if (target == KickPhase.Complete) return s;   // Complete cũng là trạng thái nghỉ
            s.BeginKick(seed);
            TickUntil(s, target);
            return s;
        }

        /// <summary>
        /// Như <see cref="DriveTo"/> nhưng cố ý làm trạng thái "bẩn": đã ghi ý đồ cú sút
        /// (từ RunUp trở đi) và đã có kết quả (từ Resolution trở đi). Có bẩn thì mới kiểm
        /// được <c>Abort()</c> có dọn sạch thật hay không.
        /// </summary>
        static KickSequencer DriveDirtyTo(KickPhase target, uint seed)
        {
            var s = new KickSequencer();
            if (target == KickPhase.Complete) return s;

            s.BeginKick(seed);

            if (target != KickPhase.Placing && target != KickPhase.Aiming)
            {
                TickUntil(s, KickPhase.RunUp);
                s.SetIntent(MakeIntent(26f));
            }

            if (target == KickPhase.Resolution || target == KickPhase.Reaction)
            {
                TickUntil(s, KickPhase.Flight);
                s.ReportOutcome(KickResult.Scored);   // Flight -> Resolution
            }

            TickUntil(s, target);
            return s;
        }

        static void AssertPath(PhaseLog log, params KickPhase[] expected)
        {
            Assert.AreEqual(expected.Length - 1, log.Count,
                $"Số bước chuyển pha sai. Thực tế: {log.Dump()}");

            for (int i = 0; i < log.Count; i++)
            {
                Assert.AreEqual(expected[i], log.From[i],
                    $"Bước {i}: pha CŨ sai. Thực tế: {log.Dump()}");
                Assert.AreEqual(expected[i + 1], log.To[i],
                    $"Bước {i}: pha MỚI sai. Thực tế: {log.Dump()}");
            }
        }

        // ═══════════════════ MỤC 1 — SEED ═══════════════════
        #region 1. Seed ghi lại được để tái hiện

        [Test]
        public void KhoiTao_TrangThaiNghi_LaComplete_VaSeedBangKhong()
        {
            var s = new KickSequencer();
            Assert.AreEqual(KickPhase.Complete, s.Phase);
            Assert.AreEqual(0u, s.CurrentSeed);
            Assert.AreEqual(KickResult.Pending, s.Outcome);
            Assert.IsFalse(s.HasIntent);
            Assert.AreEqual(0f, s.PhaseElapsed, 1e-6f);
        }

        [Test]
        public void BeginKick_GhiLaiSeed_VaMoPhaPlacing()
        {
            var s = new KickSequencer();
            var log = Attach(s);

            s.BeginKick(0xC0FFEEu);

            Assert.AreEqual(0xC0FFEEu, s.CurrentSeed);
            Assert.AreEqual(KickPhase.Placing, s.Phase);
            AssertPath(log, KickPhase.Complete, KickPhase.Placing);
        }

        [Test]
        public void BeginKick_KhiDangGiuaLuot_BiBoQuaHoanToan_KhongDeSeedCu()
        {
            var s = new KickSequencer();
            s.BeginKick(111u);
            TickUntil(s, KickPhase.RunUp);

            var log = Attach(s);
            s.BeginKick(999u);       // gọi nhầm giữa lượt

            Assert.AreEqual(111u, s.CurrentSeed, "Seed lượt đang chạy bị ghi đè");
            Assert.AreEqual(KickPhase.RunUp, s.Phase, "Lượt đang chạy bị đá về đầu");
            Assert.AreEqual(0, log.Count, $"Không được bắn sự kiện nào. Thực tế: {log.Dump()}");
        }

        [Test]
        public void Abort_GiuNguyenSeed_DeConGhiLogLuotHuy()
        {
            var s = DriveTo(KickPhase.Flight, seed: 777u);
            s.Abort();
            Assert.AreEqual(777u, s.CurrentSeed);
        }

        [Test]
        public void CungSeed_ChoRaCungChuoiPha_TaiHienDuocTungBuoc()
        {
            // "Tái hiện" ở tầng sequencer nghĩa là: cùng seed + cùng chuỗi lệnh
            // ⇒ cùng chuỗi pha, cùng thời điểm, từng bit.
            var a = new KickSequencer();
            var b = new KickSequencer();
            var la = Attach(a);
            var lb = Attach(b);

            for (int r = 0; r < 2; r++)
            {
                KickSequencer s = r == 0 ? a : b;
                s.BeginKick(20260827u);
                TickUntil(s, KickPhase.Aiming);
                s.ConfirmAim();
                TickUntil(s, KickPhase.Flight);
                s.SetIntent(MakeIntent(26.5f));
                s.ReportOutcome(KickResult.Scored);
                TickUntil(s, KickPhase.Complete);
            }

            Assert.AreEqual(a.CurrentSeed, b.CurrentSeed);
            Assert.AreEqual(la.Count, lb.Count, "Số bước khác nhau giữa hai lần chạy cùng seed");
            for (int i = 0; i < la.Count; i++)
            {
                Assert.AreEqual(la.From[i], lb.From[i], $"Bước {i} lệch pha cũ");
                Assert.AreEqual(la.To[i], lb.To[i], $"Bước {i} lệch pha mới");
            }
            Assert.AreEqual(a.Outcome, b.Outcome);
        }

        [Test]
        public void Capture_GhiLaiSeed_VaRestore_TraLaiDungSeed()
        {
            var s = DriveTo(KickPhase.Flight, seed: 0xABCDEFu);
            s.SetIntent(MakeIntent(24f));
            KickSequencerSnapshot snap = s.Capture();

            Assert.AreEqual(0xABCDEFu, snap.seed);
            Assert.AreEqual((byte)KickPhase.Flight, snap.phase);

            var fresh = new KickSequencer();
            fresh.Restore(snap);
            Assert.AreEqual(0xABCDEFu, fresh.CurrentSeed);
        }

        [Test]
        public void HaiMuoiLuot_MoiLuotMotSeed_DeuGhiDungSeedCuaMinh()
        {
            var s = new KickSequencer();
            for (uint i = 1; i <= 20; i++)
            {
                s.BeginKick(i * 7919u);
                Assert.AreEqual(i * 7919u, s.CurrentSeed, $"Lượt {i} ghi sai seed");
                TickUntil(s, KickPhase.Complete);
                Assert.AreEqual(i * 7919u, s.CurrentSeed, $"Lượt {i} mất seed sau khi kết thúc");
            }
        }

        #endregion

        // ═══════════════════ MỤC 2 — KHÔNG NHẢY CÓC ═══════════════════
        #region 2. Chuyển pha không bao giờ nhảy cóc

        [Test]
        public void TickNhoDan_ChuoiPha_DungThuTuTuyetDoi()
        {
            var s = new KickSequencer();
            var log = Attach(s);

            s.BeginKick(1u);
            TickUntil(s, KickPhase.Complete);

            AssertPath(log, FullPath);
        }

        [Test]
        public void TickMotLanRatLon_VanBanSuKienTungBuoc_KhongNhayThangToiComplete()
        {
            var s = new KickSequencer();
            var log = Attach(s);

            s.BeginKick(2u);
            s.Tick(100f);   // một khung hình "khổng lồ": phải đi hết 7 bước, không được nhảy cóc

            Assert.AreEqual(KickPhase.Complete, s.Phase);
            AssertPath(log, FullPath);
        }

        [Test]
        public void MoiSuKien_PhaCu_LuonBangPhaMoi_CuaSuKienTruoc()
        {
            // Tính liên tục của chuỗi: không có kẽ hở nào giữa hai sự kiện liên tiếp,
            // kể cả khi có ConfirmAim và ReportOutcome cắt ngang nhịp thời gian.
            var s = new KickSequencer();
            var log = Attach(s);

            s.BeginKick(3u);
            TickUntil(s, KickPhase.Aiming);
            s.ConfirmAim();
            TickUntil(s, KickPhase.Flight);
            s.ReportOutcome(KickResult.Missed);
            TickUntil(s, KickPhase.Complete);

            AssertPath(log, FullPath);
            for (int i = 1; i < log.Count; i++)
            {
                Assert.AreEqual(log.To[i - 1], log.From[i],
                    $"Kẽ hở giữa bước {i - 1} và {i}. Thực tế: {log.Dump()}");
            }
        }

        [Test]
        public void TrongHandler_DocPhase_ThayNgayPhaMOI_KhongPhaiPhaCu()
        {
            // Nếu cài đặt bắn sự kiện TRƯỚC khi gán trạng thái thì handler sẽ đọc ra pha cũ,
            // và mọi thứ đăng ký theo sự kiện (UI, âm thanh, thủ môn) sẽ lệch một nhịp.
            var s = new KickSequencer();
            var log = Attach(s);

            s.BeginKick(4u);
            s.Tick(100f);

            Assert.AreEqual(log.Count, log.PhaseSeenInsideHandler.Count);
            for (int i = 0; i < log.Count; i++)
            {
                Assert.AreEqual(log.To[i], log.PhaseSeenInsideHandler[i],
                    $"Bước {i}: trong handler đọc Phase ra {log.PhaseSeenInsideHandler[i]} " +
                    $"trong khi sự kiện báo đã sang {log.To[i]}");
            }
        }

        [Test]
        public void Tick_GiuPhanDuThoiGian_KhongGanVeKhong()
        {
            var s = new KickSequencer();
            s.Durations = new KickPhaseDurations
            {
                placing = 1f, aiming = 1f, runUp = 1f,
                contact = 1f, flight = 1f, resolution = 1f, reaction = 1f
            };

            s.BeginKick(5u);
            s.Tick(1.5f);

            Assert.AreEqual(KickPhase.Aiming, s.Phase);
            Assert.AreEqual(0.5f, s.PhaseElapsed, 1e-5f,
                "Phần dư 0.5s bị vứt — nhịp trận sẽ trôi chậm dần mỗi lần rớt khung hình");
        }

        [Test]
        public void TongThoiGianMotLuot_BangDungTongThoiLuongCacPha_KhongTroi()
        {
            var d = KickPhaseDurations.Default;
            float expected = d.placing + d.aiming + d.runUp + d.contact + d.flight + d.resolution + d.reaction;

            var s = new KickSequencer();
            s.BeginKick(6u);

            const float dt = 1f / 240f;
            float pumped = 0f;
            while (s.Phase != KickPhase.Complete)
            {
                s.Tick(dt);
                pumped += dt;
                Assert.Less(pumped, 60f, "Lượt không kết thúc sau 60 giây mô phỏng");
            }

            // Bơm từng nhịp dt nên thời điểm kết thúc nằm trong [expected, expected + dt].
            Assert.GreaterOrEqual(pumped, expected - 2e-3f,
                $"Lượt kết thúc SỚM hơn tổng thời lượng ({pumped:F4}s < {expected:F4}s) — có pha bị nuốt");
            Assert.LessOrEqual(pumped, expected + dt + 2e-3f,
                $"Lượt kết thúc MUỘN hơn tổng thời lượng ({pumped:F4}s > {expected:F4}s) — thời gian bị trôi");
        }

        [Test]
        public void ThoiLuongBangKhong_VanTienDungMotBuocMoiVong_KhongLapVoHan()
        {
            var s = new KickSequencer();
            s.Durations = new KickPhaseDurations();   // tất cả = 0
            var log = Attach(s);

            s.BeginKick(7u);      // BeginKick đã bắn Complete->Placing
            s.Tick(0.001f);       // một nhịp bé xíu phải quét hết phần còn lại

            Assert.AreEqual(KickPhase.Complete, s.Phase);
            AssertPath(log, FullPath);
        }

        [Test]
        public void Tick_DeltaAmKhongHoacNaN_KhongLamGi()
        {
            var s = DriveTo(KickPhase.Aiming, seed: 8u);
            float before = s.PhaseElapsed;
            var log = Attach(s);

            s.Tick(0f);
            s.Tick(-1f);
            s.Tick(float.NaN);

            Assert.AreEqual(KickPhase.Aiming, s.Phase);
            Assert.AreEqual(before, s.PhaseElapsed, 1e-6f);
            Assert.AreEqual(0, log.Count, $"Thực tế: {log.Dump()}");
        }

        [Test]
        public void Tick_KhiDaComplete_KhongLamGi()
        {
            var s = new KickSequencer();
            var log = Attach(s);

            s.Tick(100f);

            Assert.AreEqual(KickPhase.Complete, s.Phase);
            Assert.AreEqual(0, log.Count);
        }

        [Test]
        public void ConfirmAim_ChiCoTacDungOAiming()
        {
            // Ở Aiming: nhảy ngay sang RunUp, không chờ hết 3 giây.
            var s = DriveTo(KickPhase.Aiming, seed: 9u);
            var log = Attach(s);
            s.ConfirmAim();
            Assert.AreEqual(KickPhase.RunUp, s.Phase);
            Assert.AreEqual(0f, s.PhaseElapsed, 1e-6f);
            AssertPath(log, KickPhase.Aiming, KickPhase.RunUp);

            // Ở mọi pha khác: im lặng, không đổi gì.
            foreach (KickPhase p in AllPhases)
            {
                if (p == KickPhase.Aiming) continue;
                var other = DriveTo(p, seed: 10u);
                var l2 = Attach(other);
                other.ConfirmAim();
                Assert.AreEqual(p, other.Phase, $"ConfirmAim ở {p} lại đổi pha");
                Assert.AreEqual(0, l2.Count, $"ConfirmAim ở {p} lại bắn sự kiện");
            }
        }

        [Test]
        public void SetIntent_ChiNhanO_RunUp_Contact_Flight()
        {
            foreach (KickPhase p in AllPhases)
            {
                var s = DriveTo(p, seed: 11u);
                s.SetIntent(MakeIntent(27f));

                bool nhan = p == KickPhase.RunUp || p == KickPhase.Contact || p == KickPhase.Flight;
                Assert.AreEqual(nhan, s.HasIntent, $"SetIntent ở pha {p}: kỳ vọng nhận = {nhan}");
                if (nhan) Assert.AreEqual(27f, s.Intent.speed, 1e-5f);
            }
        }

        [Test]
        public void ReportOutcome_OFlight_GhiKetQua_VaTienSangResolution()
        {
            var s = DriveTo(KickPhase.Flight, seed: 12u);
            var log = Attach(s);

            s.ReportOutcome(KickResult.Scored);

            Assert.AreEqual(KickResult.Scored, s.Outcome);
            Assert.AreEqual(KickPhase.Resolution, s.Phase);
            Assert.AreEqual(0f, s.PhaseElapsed, 1e-6f);
            AssertPath(log, KickPhase.Flight, KickPhase.Resolution);
        }

        [Test]
        public void ReportOutcome_OResolution_ChiGhiDe_KhongDoiPha()
        {
            // Kịch bản thật: bóng bật cột (Missed) rồi lăn vào lưới (Scored).
            var s = DriveTo(KickPhase.Flight, seed: 13u);
            s.ReportOutcome(KickResult.Missed);
            var log = Attach(s);

            s.ReportOutcome(KickResult.Scored);

            Assert.AreEqual(KickResult.Scored, s.Outcome);
            Assert.AreEqual(KickPhase.Resolution, s.Phase);
            Assert.AreEqual(0, log.Count, $"Không được đổi pha. Thực tế: {log.Dump()}");
        }

        [Test]
        public void ReportOutcome_OPhaKhac_KhongCoTacDung()
        {
            foreach (KickPhase p in AllPhases)
            {
                if (p == KickPhase.Flight || p == KickPhase.Resolution) continue;
                var s = DriveTo(p, seed: 14u);
                s.ReportOutcome(KickResult.Scored);
                Assert.AreEqual(KickResult.Pending, s.Outcome, $"ReportOutcome ở pha {p} lại ăn");
                Assert.AreEqual(p, s.Phase, $"ReportOutcome ở pha {p} lại đổi pha");
            }
        }

        [Test]
        public void KhongAiGoiReportOutcome_ThiOutcomeVanLaPending_KhongTuBia()
        {
            var s = new KickSequencer();
            s.BeginKick(15u);
            TickUntil(s, KickPhase.Complete);

            Assert.AreEqual(KickResult.Pending, s.Outcome,
                "Sequencer tự bịa ra kết quả — nó không biết gì về quả bóng, phải để Pending");
        }

        [Test]
        public void Durations_Default_DungBangGiaTriDaChot()
        {
            var d = KickPhaseDurations.Default;
            Assert.AreEqual(0.80f, d.placing, 1e-6f);
            Assert.AreEqual(3.00f, d.aiming, 1e-6f);
            Assert.AreEqual(0.90f, d.runUp, 1e-6f);
            Assert.AreEqual(0.05f, d.contact, 1e-6f);
            Assert.AreEqual(1.20f, d.flight, 1e-6f);
            Assert.AreEqual(0.60f, d.resolution, 1e-6f);
            Assert.AreEqual(1.50f, d.reaction, 1e-6f);
            Assert.AreEqual(0f, d.For(KickPhase.Complete), 1e-6f, "Complete là điểm dừng, không có thời lượng");
        }

        #endregion

        // ═══════════════════ MỤC 3 — THOÁT APP GIỮA FLIGHT ═══════════════════
        #region 3. Thoát giữa Flight rồi vào lại

        [Test]
        public void ThoatGiuaFlight_VaoLai_VeDauFlight_GiuNguyenSeedVaIntent()
        {
            var truoc = DriveTo(KickPhase.Flight, seed: 31337u);
            truoc.SetIntent(MakeIntent(28.5f));
            truoc.Tick(0.4f);                       // đã bay được 0.4s thì app bị giết
            KickSequencerSnapshot snap = truoc.Capture();

            var sau = new KickSequencer();          // phiên mới, như vừa mở lại app
            var log = Attach(sau);
            sau.Restore(snap);

            Assert.AreEqual(KickPhase.Flight, sau.Phase);
            Assert.AreEqual(0f, sau.PhaseElapsed, 1e-6f, "Phải đá lại đường bay từ đầu, không bay tiếp từ giữa");
            Assert.AreEqual(31337u, sau.CurrentSeed);
            Assert.IsTrue(sau.HasIntent);
            Assert.AreEqual(28.5f, sau.Intent.speed, 1e-5f);
            Assert.AreEqual(KickResult.Pending, sau.Outcome);
            AssertPath(log, KickPhase.Complete, KickPhase.Flight);
        }

        [Test]
        public void SauKhiKhoiPhucGiuaFlight_LuotChayTiepBinhThuongToiComplete()
        {
            var truoc = DriveTo(KickPhase.Flight, seed: 41u);
            truoc.SetIntent(MakeIntent(25f));
            var sau = new KickSequencer();
            sau.Restore(truoc.Capture());

            var log = Attach(sau);
            sau.ReportOutcome(KickResult.Scored);
            TickUntil(sau, KickPhase.Complete);

            Assert.AreEqual(KickResult.Scored, sau.Outcome);
            AssertPath(log, KickPhase.Flight, KickPhase.Resolution, KickPhase.Reaction, KickPhase.Complete);

            // và còn mở được lượt kế tiếp
            sau.BeginKick(42u);
            Assert.AreEqual(KickPhase.Placing, sau.Phase);
            Assert.AreEqual(42u, sau.CurrentSeed);
        }

        [Test]
        public void Restore_TuPlacingAimingRunUp_TuaVeDauLuot_CungSeed()
        {
            KickPhase[] chuaCamKet = { KickPhase.Placing, KickPhase.Aiming, KickPhase.RunUp };
            foreach (KickPhase p in chuaCamKet)
            {
                var truoc = DriveTo(p, seed: 5150u);
                truoc.SetIntent(MakeIntent(30f));    // chỉ ăn ở RunUp
                var sau = new KickSequencer();
                sau.Restore(truoc.Capture());

                Assert.AreEqual(KickPhase.Placing, sau.Phase, $"Từ {p} phải tua về đầu lượt");
                Assert.AreEqual(5150u, sau.CurrentSeed, $"Từ {p} mất seed");
                Assert.AreEqual(0f, sau.PhaseElapsed, 1e-6f);
                Assert.AreEqual(KickResult.Pending, sau.Outcome);
                Assert.IsFalse(sau.HasIntent, $"Từ {p} chưa cam kết cú sút nào, không được giữ intent");
            }
        }

        [Test]
        public void Restore_TuContact_CungVeDauFlight()
        {
            var truoc = DriveTo(KickPhase.Contact, seed: 61u);
            truoc.SetIntent(MakeIntent(23f));
            var sau = new KickSequencer();
            sau.Restore(truoc.Capture());

            Assert.AreEqual(KickPhase.Flight, sau.Phase);
            Assert.IsTrue(sau.HasIntent);
            Assert.AreEqual(23f, sau.Intent.speed, 1e-5f);
        }

        [Test]
        public void Restore_TuResolutionCoKetQua_GiuNguyenKetQua()
        {
            var truoc = DriveTo(KickPhase.Flight, seed: 71u);
            truoc.SetIntent(MakeIntent(22f));
            truoc.ReportOutcome(KickResult.Missed);        // đang ở Resolution

            var sau = new KickSequencer();
            sau.Restore(truoc.Capture());

            Assert.AreEqual(KickPhase.Resolution, sau.Phase);
            Assert.AreEqual(KickResult.Missed, sau.Outcome, "Kết quả đã chốt mà bị xoá khi khôi phục");
            Assert.IsTrue(sau.HasIntent);
        }

        [Test]
        public void Restore_TuReaction_MaOutcomeConPending_ThiPhaiDaLaiFlight_KhongDuocBiaKetQua()
        {
            // Lượt trôi hết Flight mà tầng vật lý chưa kịp báo kết quả (đúng lúc app bị giết).
            var truoc = DriveTo(KickPhase.Reaction, seed: 81u);
            Assert.AreEqual(KickResult.Pending, truoc.Outcome, "Tiền đề: chưa ai báo kết quả");

            var sau = new KickSequencer();
            sau.Restore(truoc.Capture());

            Assert.AreEqual(KickPhase.Flight, sau.Phase,
                "Outcome còn Pending nghĩa là kết quả CHƯA từng được ghi — phải mô phỏng lại, không được vào thẳng Resolution");
            Assert.AreEqual(KickResult.Pending, sau.Outcome);
        }

        [Test]
        public void Restore_TuReaction_CoKetQua_VeResolution_GiuKetQua()
        {
            var s = DriveTo(KickPhase.Flight, seed: 91u);
            s.SetIntent(MakeIntent(26f));
            s.ReportOutcome(KickResult.Scored);
            TickUntil(s, KickPhase.Reaction);

            var sau = new KickSequencer();
            sau.Restore(s.Capture());

            Assert.AreEqual(KickPhase.Resolution, sau.Phase);
            Assert.AreEqual(KickResult.Scored, sau.Outcome);
        }

        [Test]
        public void Restore_SnapshotComplete_VeTrangThaiNghiSach()
        {
            var s = DriveTo(KickPhase.Flight, seed: 101u);
            s.SetIntent(MakeIntent(20f));

            KickSequencerSnapshot snap = default;
            snap.phase = (byte)KickPhase.Complete;
            snap.seed = 202u;
            snap.outcome = (byte)KickResult.Scored;
            snap.hasIntent = true;

            s.Restore(snap);

            Assert.AreEqual(KickPhase.Complete, s.Phase);
            Assert.AreEqual(202u, s.CurrentSeed);
            Assert.AreEqual(KickResult.Pending, s.Outcome);
            Assert.IsFalse(s.HasIntent);
        }

        [Test]
        public void Restore_ByteNgoaiDai_CoiNhuComplete_Pending_KhongNemException()
        {
            var s = DriveTo(KickPhase.Flight, seed: 111u);

            KickSequencerSnapshot hong = default;
            hong.phase = 200;          // file lưu bị sửa tay
            hong.outcome = 99;
            hong.seed = 123u;
            hong.hasIntent = true;

            Assert.DoesNotThrow(() => s.Restore(hong),
                "Bản lưu hỏng làm crash lúc mở app là mất trắng dữ liệu người chơi");
            Assert.AreEqual(KickPhase.Complete, s.Phase);
            Assert.AreEqual(KickResult.Pending, s.Outcome);
            Assert.IsFalse(s.HasIntent);
            Assert.AreEqual(123u, s.CurrentSeed);
        }

        [Test]
        public void Restore_OutcomeNgoaiDai_OPhaResolution_CoiLaPending_NenDaLaiFlight()
        {
            KickSequencerSnapshot snap = default;
            snap.phase = (byte)KickPhase.Resolution;
            snap.outcome = 77;          // rác
            snap.seed = 131u;
            snap.hasIntent = true;
            snap.intent = MakeIntent(21f);

            var s = new KickSequencer();
            Assert.DoesNotThrow(() => s.Restore(snap));
            Assert.AreEqual(KickPhase.Flight, s.Phase);
            Assert.AreEqual(KickResult.Pending, s.Outcome);
        }

        [Test]
        public void Restore_KhiPhaKhongDoi_ThiKhongBanSuKien()
        {
            var s = DriveTo(KickPhase.Flight, seed: 141u);
            s.SetIntent(MakeIntent(24f));
            KickSequencerSnapshot snap = s.Capture();

            var log = Attach(s);
            s.Restore(snap);            // Flight -> Flight

            Assert.AreEqual(KickPhase.Flight, s.Phase);
            Assert.AreEqual(0, log.Count, $"Pha không đổi mà vẫn bắn sự kiện. Thực tế: {log.Dump()}");
        }

        [Test]
        public void Restore_HopLe_KeCaKhiDangGiuaMotLuotKhac()
        {
            var nguon = DriveTo(KickPhase.Flight, seed: 151u);
            nguon.SetIntent(MakeIntent(29f));
            KickSequencerSnapshot snap = nguon.Capture();

            var dich = DriveTo(KickPhase.Aiming, seed: 999u);   // đang giữa lượt khác
            var log = Attach(dich);
            dich.Restore(snap);

            Assert.AreEqual(KickPhase.Flight, dich.Phase);
            Assert.AreEqual(151u, dich.CurrentSeed);
            Assert.AreEqual(29f, dich.Intent.speed, 1e-5f);
            AssertPath(log, KickPhase.Aiming, KickPhase.Flight);
        }

        [Test]
        public void Capture_ChupDungNguyenTrang()
        {
            var s = DriveTo(KickPhase.Flight, seed: 161u);
            s.SetIntent(MakeIntent(27.5f));
            s.Tick(0.25f);

            KickSequencerSnapshot snap = s.Capture();

            Assert.AreEqual((byte)KickPhase.Flight, snap.phase);
            Assert.AreEqual(161u, snap.seed);
            Assert.AreEqual(s.PhaseElapsed, snap.phaseElapsed, 1e-6f);
            Assert.AreEqual((byte)KickResult.Pending, snap.outcome);
            Assert.IsTrue(snap.hasIntent);
            Assert.AreEqual(27.5f, snap.intent.speed, 1e-5f);
            Assert.AreEqual(ShotType.InsideFoot, snap.intent.type);
        }

        #endregion

        // ═══════════════════ MỤC 4 — 200 LƯỢT, 0 GC ═══════════════════
        #region 4. 200 lượt liên tiếp không tăng bộ nhớ

        /// <summary>
        /// <c>Is.Not.AllocatingGCMemory()</c> của Unity đo bằng chính bộ đếm cấp phát của
        /// Profiler (GC.Alloc recorder) quanh đoạn mã — chặt hơn việc mở cửa sổ Profiler nhìn
        /// bằng mắt, vì nó thất bại ở BYTE ĐẦU TIÊN bị cấp phát chứ không đợi đồ thị dốc lên.
        /// </summary>
        [Test]
        public void ChayHaiTramLuotLienTiep_KhongCapPhatMotByteGC_Nao()
        {
            var s = new KickSequencer();
            var intent = MakeIntent(26f);

            // Làm nóng: lần chạy đầu có thể chạm JIT/khởi tạo, không tính vào phép đo.
            RunKicks(s, intent, 2);

            Assert.That(() => { RunKicks(s, intent, 200); }, Is.Not.AllocatingGCMemory(),
                "200 lượt liên tiếp phải cấp phát 0 byte GC — mỗi byte ở đây là một cú khựng " +
                "khi GC dọn giữa loạt luân lưu trên máy yếu");
        }

        [Test]
        public void ChayHaiTramLuotLienTiep_CoNguoiDangKySuKien_VanKhongCapPhatGC()
        {
            var s = new KickSequencer();
            var intent = MakeIntent(26f);
            var dem = new PhaseCounter();
            s.OnPhaseChanged += dem.Handle;      // delegate tạo MỘT lần, ngoài vùng đo

            RunKicks(s, intent, 2);
            dem.Count = 0;

            Assert.That(() => { RunKicks(s, intent, 200); }, Is.Not.AllocatingGCMemory());
            Assert.AreEqual(200 * BuocMoiLuot, dem.Count, "Thiếu/thừa bước chuyển pha trong 200 lượt");
        }

        [Test]
        public void ChayHaiTramLuotLienTiep_TrangThaiCuoiVanSach()
        {
            var s = new KickSequencer();
            var log = Attach(s);
            var intent = MakeIntent(26f);

            RunKicks(s, intent, 200);

            Assert.AreEqual(KickPhase.Complete, s.Phase);
            Assert.AreEqual(0f, s.PhaseElapsed, 1e-4f);
            Assert.AreEqual(200u, s.CurrentSeed);
            Assert.AreEqual(200 * BuocMoiLuot, log.Count, "200 lượt × 8 bước = 1600 lần chuyển pha");

            // Và toàn bộ 1400 bước vẫn đúng thứ tự, không có lượt nào nhảy cóc.
            for (int k = 0; k < 200; k++)
            {
                for (int i = 0; i < BuocMoiLuot; i++)
                {
                    int idx = k * BuocMoiLuot + i;
                    Assert.AreEqual(FullPath[i], log.From[idx], $"Lượt {k}, bước {i}: pha cũ sai");
                    Assert.AreEqual(FullPath[i + 1], log.To[idx], $"Lượt {k}, bước {i}: pha mới sai");
                }
            }
        }

        sealed class PhaseCounter
        {
            public int Count;
            public void Handle(KickPhase from, KickPhase to) { Count++; }
        }

        /// <summary>
        /// Một lượt "đầy đủ" như lúc chơi thật: mở lượt, chốt ngắm, ghi ý đồ, báo kết quả,
        /// rồi tua tới hết. Dùng Tick lớn để không tốn hàng trăm nghìn vòng lặp trong phép đo GC.
        /// </summary>
        static void RunKicks(KickSequencer s, in ShotIntent intent, int count)
        {
            for (int i = 0; i < count; i++)
            {
                s.BeginKick((uint)(i + 1));
                s.Tick(1.0f);              // qua Placing
                s.ConfirmAim();
                s.SetIntent(intent);
                s.Tick(1.0f);              // RunUp -> Contact -> Flight
                s.ReportOutcome((i & 1) == 0 ? KickResult.Scored : KickResult.Missed);
                s.Tick(100f);              // Resolution -> Reaction -> Complete
            }
        }

        #endregion

        // ═══════════════════ MỤC 5 — ABORT ═══════════════════
        #region 5. Abort() ở bất kỳ pha nào đều về Complete sạch sẽ

        [Test]
        public void Abort_OMoiPha_DeuVeCompleteSachSe()
        {
            foreach (KickPhase p in AllPhases)
            {
                if (p == KickPhase.Complete) continue;   // có ca riêng ở dưới

                var s = DriveDirtyTo(p, seed: 1717u);
                Assert.AreEqual(p, s.Phase, $"Tiền đề hỏng: không dựng được trạng thái ở pha {p}");

                var log = Attach(s);
                s.Abort();

                Assert.AreEqual(KickPhase.Complete, s.Phase, $"Abort ở {p} không về Complete");
                Assert.AreEqual(0f, s.PhaseElapsed, 1e-6f, $"Abort ở {p} còn sót thời gian");
                Assert.AreEqual(KickResult.Pending, s.Outcome, $"Abort ở {p} còn sót kết quả");
                Assert.IsFalse(s.HasIntent, $"Abort ở {p} còn sót ý đồ cú sút");
                Assert.AreEqual(1717u, s.CurrentSeed, $"Abort ở {p} xoá mất seed (còn phải ghi log lượt huỷ)");
                AssertPath(log, p, KickPhase.Complete);
            }
        }

        [Test]
        public void Abort_KhiDaOComplete_NoOp_KhongBanSuKien()
        {
            var s = new KickSequencer();
            var log = Attach(s);

            s.Abort();
            s.Abort();

            Assert.AreEqual(KickPhase.Complete, s.Phase);
            Assert.AreEqual(0, log.Count, $"Thực tế: {log.Dump()}");
        }

        [Test]
        public void Abort_XongThi_BeginKickMoi_ChayDuocNgay()
        {
            foreach (KickPhase p in AllPhases)
            {
                if (p == KickPhase.Complete) continue;

                var s = DriveTo(p, seed: 1u);
                s.Abort();
                s.BeginKick(2u);

                Assert.AreEqual(KickPhase.Placing, s.Phase, $"Sau khi huỷ ở {p} không mở được lượt mới");
                Assert.AreEqual(2u, s.CurrentSeed);

                TickUntil(s, KickPhase.Complete);       // và chạy trọn vẹn được
                Assert.AreEqual(KickPhase.Complete, s.Phase);
            }
        }

        [Test]
        public void Abort_GoiHaiLan_LienTiep_KhongBanSuKienThuHai()
        {
            var s = DriveTo(KickPhase.RunUp, seed: 3u);
            var log = Attach(s);

            s.Abort();
            s.Abort();

            AssertPath(log, KickPhase.RunUp, KickPhase.Complete);
        }

        [Test]
        public void Abort_GoiTuTrongHandler_VongTickDungLai_KhongChayTiep()
        {
            // Kịch bản thật: người chơi bấm "thoát trận" đúng lúc bóng vừa rời chân.
            var s = new KickSequencer();
            var log = Attach(s);
            System.Action<KickPhase, KickPhase> huyKhiBay = null;
            huyKhiBay = (from, to) => { if (to == KickPhase.Flight) s.Abort(); };
            s.OnPhaseChanged += huyKhiBay;

            s.BeginKick(4u);
            s.Tick(100f);

            Assert.AreEqual(KickPhase.Complete, s.Phase);
            Assert.AreEqual(0f, s.PhaseElapsed, 1e-6f);
            AssertPath(log,
                KickPhase.Complete, KickPhase.Placing, KickPhase.Aiming, KickPhase.RunUp,
                KickPhase.Contact, KickPhase.Flight, KickPhase.Complete);
        }

        [Test]
        public void BeginKick_GoiTuTrongHandlerLucVaoComplete_NoiDuocLuotKeTiep()
        {
            // Bộ điều phối loạt luân lưu sẽ nối lượt theo đúng cách này, nên nó phải chạy được:
            // khi handler chạy thì trạng thái đã dọn xong, BeginKick không bị ghi đè.
            var s = new KickSequencer();
            int soLuot = 0;
            System.Action<KickPhase, KickPhase> noiLuot = null;
            noiLuot = (from, to) =>
            {
                if (to != KickPhase.Complete) return;
                soLuot++;
                if (soLuot < 3) s.BeginKick((uint)(100 + soLuot));
            };
            s.OnPhaseChanged += noiLuot;

            s.BeginKick(100u);
            for (int i = 0; i < 40 && soLuot < 3; i++) s.Tick(1f);

            Assert.AreEqual(3, soLuot, "Không nối được 3 lượt liên tiếp qua sự kiện Complete");
            Assert.AreEqual(KickPhase.Complete, s.Phase);
            Assert.AreEqual(102u, s.CurrentSeed);
        }

        [Test]
        public void Abort_HandlerGoiBeginKickLucNhanComplete_LuotMoiKhongBiGhiDe()
        {
            // Đây chính là cách bộ điều phối loạt luân lưu sẽ nối lượt sau khi người chơi
            // huỷ một quả. Nếu Abort bắn sự kiện TRƯỚC khi dọn trạng thái thì cú BeginKick
            // trong handler sẽ bị chính Abort ghi đè, và lượt kế tiếp im lặng biến mất.
            var s = DriveDirtyTo(KickPhase.Flight, seed: 6u);
            System.Action<KickPhase, KickPhase> noiLuot = null;
            noiLuot = (from, to) => { if (to == KickPhase.Complete) s.BeginKick(555u); };
            s.OnPhaseChanged += noiLuot;

            s.Abort();

            Assert.AreEqual(KickPhase.Placing, s.Phase,
                "Lượt mở trong handler bị Abort ghi đè — sequencer đứng lại ở Complete");
            Assert.AreEqual(555u, s.CurrentSeed);
            Assert.IsFalse(s.HasIntent, "Lượt mới mà còn giữ ý đồ của lượt bị huỷ");
            Assert.AreEqual(KickResult.Pending, s.Outcome);
        }

        [Test]
        public void Abort_TuTrongHandler_DungNgayTaiPhaDo_KhongBanThemSuKienNao()
        {
            var s = DriveTo(KickPhase.Aiming, seed: 5u);
            var log = Attach(s);
            int soLanGoi = 0;
            System.Action<KickPhase, KickPhase> huy = null;
            huy = (from, to) => { soLanGoi++; s.Abort(); };
            s.OnPhaseChanged += huy;

            s.ConfirmAim();      // Aiming -> RunUp, rồi handler huỷ ngay

            Assert.AreEqual(KickPhase.Complete, s.Phase);
            AssertPath(log, KickPhase.Aiming, KickPhase.RunUp, KickPhase.Complete);
            Assert.AreEqual(2, soLanGoi, "Handler phải được gọi đúng 2 lần: sang RunUp, rồi sang Complete");
        }

        #endregion
    }
}
