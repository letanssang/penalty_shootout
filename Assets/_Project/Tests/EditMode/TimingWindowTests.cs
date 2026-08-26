using System.Text;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;   // gop extension AllocatingGCMemory()
using Is = NUnit.Framework.Is;             // khu nhap nhang voi lop Is cua Unity
using Unity.Mathematics;
using UnityEngine;
using Eleven.Shooter;

namespace Eleven.Tests.EditMode {

    /// <summary>
    /// T15 — cửa sổ thời điểm. Chấm (thời điểm bấm, thời điểm chuẩn) ra sai số, và hiện
    /// được sai số đó bằng mili-giây ở chế độ debug.
    ///
    /// Không có test nào ở đây đọc đồng hồ thật: <see cref="TimingWindow"/> nhận cả hai mốc
    /// thời gian qua tham số, nên chấm được nghìn thời điểm mà không phải chạy game.
    /// </summary>
    [TestFixture]
    public class TimingWindowTests {

        static TimingWindowConfig Cfg => TimingWindowConfig.Default;

        /// <summary>Bấm lệch dt giây so với thời điểm chuẩn. Gốc đồng hồ cố ý đặt lệch 0 để
        /// lộ ra ngay nếu có chỗ nào lỡ coi releaseTime là "giây kể từ lúc mở cửa sổ".</summary>
        static TimingResult Bam(float dt) => TimingWindow.Evaluate(100f + dt, 100f, Cfg);

        /// <summary>Như <see cref="Bam"/> nhưng gốc đồng hồ ở 0, nên sai số bằng ĐÚNG dt từng bit.
        /// Chỉ dùng cho các test đứng ngay trên mép cửa sổ: <c>100f + 0.05f</c> làm tròn thành
        /// 100.050003, tức lệch mép 3 micro-giây — đủ để mép đóng trông như mép hở.</summary>
        static TimingResult BamChinhXac(float dt) => TimingWindow.Evaluate(dt, 0f, Cfg);

        // ─── Sai số thô ────────────────────────────────────────────────

        [Test]
        public void BamDungKhoanhKhac_SaiSoBangKhongVaHangHoanHao() {
            var r = Bam(0f);

            Assert.AreEqual(0f, r.errorSeconds);
            Assert.AreEqual(0f, r.mappedErrorSeconds);
            Assert.AreEqual(TimingGrade.Perfect, r.grade);
        }

        [Test]
        public void BamSom_SaiSoAm_BamMuon_SaiSoDuong() {
            Assert.Less   (Bam(-0.08f).errorSeconds, 0f, "Bấm sớm phải cho sai số âm.");
            Assert.Greater(Bam( 0.08f).errorSeconds, 0f, "Bấm muộn phải cho sai số dương.");

            Assert.IsTrue (Bam(-0.08f).IsEarly);
            Assert.IsFalse(Bam( 0.08f).IsEarly);
            Assert.IsFalse(Bam(0f).IsEarly, "Đúng khoảnh khắc không tính là sớm.");
        }

        [Test]
        public void SaiSoTho_KhongBiThaThuKhongBiKep_DeHUDNoiThat() {
            // Đây là lý do có hai trường sai số: HUD debug phải hiện con số THẬT, kể cả khi
            // gameplay đã tha thứ hoặc đã kẹp nó.
            var r = Bam(0.5f);   // tệ gấp 2.5 lần trần

            Assert.AreEqual(0.5f, r.errorSeconds, 1e-6f);
            Assert.AreEqual(Cfg.maxErrorSeconds, r.mappedErrorSeconds, 1e-6f);
        }

        // ─── Vùng chết và sai số đưa vào gameplay ──────────────────────

        [Test]
        public void TrongVungHoanHao_SaiSoGameplayDungBangKhong_KhongPhaiGanBang() {
            // "Gần bằng 0" là không đủ: ShotMapper cho quality = 1 - |err|/max, rồi
            // qualityToScatter phải chạm đúng nhánh scatter = 0. Chỉ số 0 chính xác mới tới đó.
            foreach (float dt in new[] { -0.049f, -0.03f, 0f, 0.02f, 0.049f }) {
                var r = Bam(dt);
                Assert.AreEqual(0f, r.mappedErrorSeconds,
                    $"Bấm lệch {dt}s vẫn nằm trong vùng hoàn hảo nên sai số gameplay phải là 0.");
                Assert.AreEqual(TimingGrade.Perfect, r.grade);
            }

            // Đúng mép: mép ĐÓNG, lệch đúng bằng nửa cửa sổ vẫn là hoàn hảo.
            foreach (float dt in new[] { -Cfg.perfectHalfWidthSeconds, Cfg.perfectHalfWidthSeconds }) {
                var r = BamChinhXac(dt);
                Assert.AreEqual(0f, r.mappedErrorSeconds, $"Đúng mép {dt}s phải còn là hoàn hảo.");
                Assert.AreEqual(TimingGrade.Perfect, r.grade);
            }
        }

        [Test]
        public void NgoaiVungHoanHao_SaiSoDoTuMepVungChet_KhongDoTuKhong() {
            // Đo từ 0 sẽ tạo bậc nhảy 50 ms ngay sát mép vùng hoàn hảo. Đo từ mép thì hàm
            // liên tục: lệch 0.051s chỉ phạt 1 ms.
            var r = Bam(0.051f);

            Assert.AreEqual(0.001f, r.mappedErrorSeconds, 1e-5f,
                "Sai số gameplay phải tính từ mép vùng hoàn hảo trở ra.");
        }

        [Test]
        public void SomHayMuon_PhatNhuNhau() {
            var som  = Bam(-0.15f);
            var muon = Bam( 0.15f);

            Assert.AreEqual(math.abs(som.mappedErrorSeconds), math.abs(muon.mappedErrorSeconds), 1e-6f);
            Assert.AreEqual(som.grade, muon.grade);
            Assert.Less   (som.mappedErrorSeconds, 0f, "Dấu vẫn phải giữ để HUD/hiệu ứng biết chiều.");
            Assert.Greater(muon.mappedErrorSeconds, 0f);
        }

        [Test]
        public void SaiSoGameplay_KhongBaoGioVuotTran() {
            for (float dt = -2f; dt <= 2f; dt += 0.017f) {
                float mapped = math.abs(Bam(dt).mappedErrorSeconds);
                Assert.LessOrEqual(mapped, Cfg.maxErrorSeconds + 1e-6f,
                    $"Bấm lệch {dt}s cho sai số gameplay {mapped}s, vượt trần {Cfg.maxErrorSeconds}s.");
            }
        }

        [Test]
        public void SaiSoCangLon_SaiSoGameplayCangLon_DonDieu() {
            float truoc = -1f;
            foreach (float dt in new[] { 0f, 0.05f, 0.06f, 0.1f, 0.15f, 0.2f, 0.25f, 0.3f }) {
                float mapped = math.abs(Bam(dt).mappedErrorSeconds);
                Assert.GreaterOrEqual(mapped, truoc, $"Không đơn điệu tại {dt}s.");
                truoc = mapped;
            }
        }

        // ─── Phân hạng ────────────────────────────────────────────────

        [Test]
        public void PhanHang_DungTheoBaDaiCuaConfig() {
            Assert.AreEqual(TimingGrade.Perfect, Bam(0.049f).grade);
            Assert.AreEqual(TimingGrade.Perfect, BamChinhXac(Cfg.perfectHalfWidthSeconds).grade,
                "Đúng mép vùng hoàn hảo vẫn tính là hoàn hảo — mép đóng, không hở.");
            Assert.AreEqual(TimingGrade.Good,    Bam(0.051f).grade);
            Assert.AreEqual(TimingGrade.Good,    BamChinhXac(Cfg.goodHalfWidthSeconds).grade);
            Assert.AreEqual(TimingGrade.Poor,    Bam(0.121f).grade);
            Assert.AreEqual(TimingGrade.Poor,    Bam(5f).grade);
        }

        // ─── Config lộn xộn / suy biến ────────────────────────────────

        [Test]
        public void ConfigToanKhong_KhongChiaKhong_KhongNaN() {
            var r = TimingWindow.Evaluate(1.2f, 1f, default);

            Assert.IsTrue(math.isfinite(r.errorSeconds));
            Assert.IsTrue(math.isfinite(r.mappedErrorSeconds));
            // Trần bằng 0 nghĩa là sai số thời điểm không ảnh hưởng gì cả — hợp lý, và phải
            // ra đúng 0 chứ không phải một số rác.
            Assert.AreEqual(0f, r.mappedErrorSeconds);
            Assert.AreEqual(TimingGrade.Poor, r.grade);
        }

        [Test]
        public void ConfigSaiThuTu_DuocSapLai_KhongImLangSai() {
            // Vùng "tốt" hẹp hơn vùng "hoàn hảo" là vô nghĩa: nếu để nguyên thì việc phân hạng
            // phụ thuộc thứ tự viết if chứ không phụ thuộc con số.
            var xau = new TimingWindowConfig {
                perfectHalfWidthSeconds = 0.12f,
                goodHalfWidthSeconds    = 0.03f,
                maxErrorSeconds         = 0.2f,
            };

            var sach = TimingWindow.Sanitize(xau);
            Assert.GreaterOrEqual(sach.goodHalfWidthSeconds, sach.perfectHalfWidthSeconds);

            // Và Evaluate phải tự sắp, không đòi người gọi nhớ gọi Sanitize trước.
            Assert.AreEqual(TimingGrade.Perfect, TimingWindow.Evaluate(0.1f, 0f, xau).grade);
        }

        [Test]
        public void ThoiDiemVoCuc_HoacNaN_KhongLamHongKetQua() {
            foreach (float t in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity }) {
                var r = TimingWindow.Evaluate(t, 0f, Cfg);
                Assert.IsTrue(math.isfinite(r.errorSeconds),       $"errorSeconds không hữu hạn với t={t}");
                Assert.IsTrue(math.isfinite(r.mappedErrorSeconds), $"mappedErrorSeconds không hữu hạn với t={t}");
            }

            var xau = new TimingWindowConfig {
                perfectHalfWidthSeconds = float.NaN,
                goodHalfWidthSeconds    = float.PositiveInfinity,
                maxErrorSeconds         = float.NaN,
            };
            Assert.IsTrue(math.isfinite(TimingWindow.Evaluate(1f, 0f, xau).mappedErrorSeconds));
        }

        [Test]
        public void CungDauVao_ChoKetQuaGiongTungBit() {
            var a = TimingWindow.Evaluate(3.14159f, 3f, Cfg);
            var b = TimingWindow.Evaluate(3.14159f, 3f, Cfg);

            Assert.AreEqual(a.errorSeconds,       b.errorSeconds,       "Không dùng dung sai: phải giống từng bit.");
            Assert.AreEqual(a.mappedErrorSeconds, b.mappedErrorSeconds);
            Assert.AreEqual(a.grade,              b.grade);
        }

        // ─── Hiển thị debug bằng mili-giây ────────────────────────────

        [Test]
        public void HienThiDebug_RaDungSoMiliGiayCoDau() {
            Assert.AreEqual("+0 ms Perfect",  TimingWindow.Describe(Bam(0f)));
            Assert.AreEqual("+42 ms Perfect", TimingWindow.Describe(Bam(0.042f)));
            Assert.AreEqual("-42 ms Perfect", TimingWindow.Describe(Bam(-0.042f)));
            Assert.AreEqual("+80 ms Good",    TimingWindow.Describe(Bam(0.08f)));
            Assert.AreEqual("-150 ms Poor",   TimingWindow.Describe(Bam(-0.15f)));
        }

        [Test]
        public void HienThiDebug_LamTronToiMiliGiay_KhongHienNhieu() {
            // 0.0424 s = 42.4 ms → 42; 0.0426 s = 42.6 ms → 43.
            Assert.AreEqual("+42 ms Perfect", TimingWindow.Describe(Bam(0.0424f)));
            Assert.AreEqual("+43 ms Perfect", TimingWindow.Describe(Bam(0.0426f)));
        }

        [Test]
        public void HienThiDebug_SaiSoRatNho_KhongRaAmKhong() {
            // -0.0001 s làm tròn về 0. Nếu lấy dấu từ float thay vì từ số nguyên đã làm tròn
            // thì ra "-0 ms", trông như HUD hỏng.
            Assert.AreEqual("+0 ms Perfect", TimingWindow.Describe(Bam(-0.0001f)));
        }

        [Test]
        public void HienThiDebug_KhongCapPhat() {
            // Chế độ debug hiện con số này mỗi khung hình. Cấp phát ở đây là tự tay đẻ ra
            // đúng loại hiện tượng giật mà HUD sinh ra để đi tìm.
            var sb = new StringBuilder(64);   // cấp sẵn dư sức chứa: StringBuilder xin chunk mới mới là cấp phát
            var r  = Bam(-0.137f);

            TimingWindow.AppendDebug(sb, r);  // hâm nóng, loại JIT khỏi phép đo
            sb.Length = 0;

            Assert.That(() => {
                TimingWindow.AppendDebug(sb, r);
                sb.Length = 0;
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void ChamThoiDiem_KhongCapPhat() {
            TimingWindow.Evaluate(1.1f, 1f, Cfg);   // hâm nóng

            // Thân lambda phải là CÂU LỆNH, không phải biểu thức: lambda có giá trị trả về
            // là Func<T> chứ không phải TestDelegate, và NUnit từ chối nó lúc chạy.
            Assert.That(() => { TimingWindow.Evaluate(1.1f, 1f, Cfg); }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void HienThiDebug_BoDemNull_KhongNemLoi() {
            // HUD debug hỏng không được kéo theo cả trận đấu.
            Assert.DoesNotThrow(() => TimingWindow.AppendDebug(null, Bam(0.05f)));
        }

        // ─── Nối với ShotMapper ───────────────────────────────────────

        [Test]
        public void NoiVoiShotMapper_BamHoanHao_KhongTanMat() {
            // Đây là mục đích tồn tại của vùng chết, kiểm đầu-cuối chứ không kiểm bằng lời hứa.
            var cfg = ShotMappingConfig.CreateDefault();
            try {
                var f = VuotThangManh();

                var hoanHao = TimingWindow.Evaluate(10.03f, 10f, Cfg);   // lệch 30 ms, vẫn trong vùng chết
                var intent  = ShotMapper.Map(f, new float3(0f, 1f, 11f), cfg,
                                             hoanHao.mappedErrorSeconds, seed: 7u);

                Assert.AreEqual(1f, intent.quality, 1e-6f, "Bấm trong vùng hoàn hảo phải cho quality = 1.");
                Assert.AreEqual(0f, intent.scatterRadius, 1e-6f, "quality = 1 thì tản mát phải bằng 0.");
            } finally {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void NoiVoiShotMapper_BamTe_TanMatRongHon_NhungVanRaCuSut() {
            var cfg = ShotMappingConfig.CreateDefault();
            try {
                var f = VuotThangManh();

                var te     = TimingWindow.Evaluate(10.4f, 10f, Cfg);     // lệch 400 ms
                var intent = ShotMapper.Map(f, new float3(0f, 1f, 11f), cfg,
                                            te.mappedErrorSeconds, seed: 7u);

                Assert.Less(intent.quality, 0.05f, "Bấm tệ hết mức phải cho quality gần 0.");
                Assert.Greater(intent.scatterRadius, 0f, "Bấm tệ phải làm lệch.");
                Assert.Greater(intent.speed, 0f, "…nhưng vẫn phải ra một cú sút, không phải hỏng hẳn.");
            } finally {
                Object.DestroyImmediate(cfg);
            }
        }

        /// <summary>Cú vuốt thẳng, dài, nhanh — đủ để ShotMapper cho ra một cú sút bình thường.</summary>
        static SwipeFeatures VuotThangManh() => new SwipeFeatures {
            start              = new float2(0f, 0f),
            end                = new float2(0f, 6f),
            length             = 6f,
            duration           = 0.12f,
            peakSpeed          = 60f,
            endSpeed           = 55f,
            curvature          = 0.3f,     // cong vừa: rơi vào má trong, không phải knuckle
            straightness       = 0.99f,
            straightnessSmooth = 0.99f,
            verticalRatio      = 1f,
        };
    }
}
