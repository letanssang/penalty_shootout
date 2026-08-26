using System;
using Eleven.Shooter;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Eleven.Tests.EditMode {
    /// <summary>
    /// Nghiệm thu T14. Nguyên tắc của bộ test này: những ô đòi "đạt tới được bằng CỬ CHỈ"
    /// thì phải dựng cú vuốt bằng TOẠ ĐỘ PIXEL thật rồi bơm qua SwipeCollector → SwipeAnalyzer
    /// → ShotMapper. Tự tay điền vào struct SwipeFeatures thì chỉ chứng minh được ShotMapper
    /// biết đọc struct, không chứng minh được ngón tay người chơi với tới nổi cú sút đó.
    /// </summary>
    [TestFixture]
    public class ShotMapperTests {
        const float Dpi = 326f;                // iPhone SE
        static float Px(float cm) => PhysicalUnits.ToPixels(cm, Dpi);

        ShotMappingConfig _cfg;
        static readonly float3 Aim = new float3(1.5f, 1.2f, 11f);   // trong khung, hơi lệch phải

        [SetUp]
        public void SetUp() => _cfg = ShotMappingConfig.CreateDefault();

        [TearDown]
        public void TearDown() {
            if (_cfg != null) UnityEngine.Object.DestroyImmediate(_cfg);
            _cfg = null;
        }

        // ------------------------------------------------------------------ dựng cử chỉ

        /// <summary>
        /// Dựng một cú vuốt thật: đi thẳng lên trên màn hình, phình sang ngang bowCm ở giữa
        /// theo hình sin. bowCm dương = cong sang phải.
        /// </summary>
        static SwipeFeatures Swipe(float lengthCm, float durationSec, float bowCm, int samples = 24) {
            using (var c = new SwipeCollector(64)) {
                float lenPx = Px(lengthCm);
                float bowPx = Px(bowCm);
                float2 At(float t) => new float2(
                    300f + math.sin(t * math.PI) * bowPx,
                    400f + lenPx * t);

                c.Begin(At(0f), 0f, Dpi);
                for (int i = 1; i < samples - 1; i++) {
                    float t = (float)i / (samples - 1);
                    c.Move(At(t), t * durationSec);
                }
                var r = c.End(At(1f), durationSec);
                Assert.IsTrue(r.valid, "Cú vuốt dựng cho test phải hợp lệ.");
                return r.features;
            }
        }

        // Bốn cử chỉ mẫu, đặt tên theo cái người chơi làm chứ không theo kết quả mong đợi.
        static SwipeFeatures VuotVuaThang()   => Swipe(5f,  0.15f, 0f);      // vừa phải, thẳng
        static SwipeFeatures VuotDaiThang()   => Swipe(7.5f, 0.15f, 0f);     // dài, thẳng đét
        static SwipeFeatures VuotCong()       => Swipe(5f,  0.15f, 1.0f);    // cong rõ sang phải
        static SwipeFeatures GiatNgan()       => Swipe(2f,  0.04f, 0f);      // giật ngắn mà nhanh

        // ------------------------------------------------------------------ 4 loại sút

        [Test]
        public void BonLoaiSut_DeuDatToiDuocBangCuChi_KhongCanNutBam() {
            Assert.AreEqual(ShotType.Instep,     Map(VuotVuaThang()).type, "vuốt vừa + thẳng");
            Assert.AreEqual(ShotType.Knuckle,    Map(VuotDaiThang()).type, "vuốt dài + thẳng đét");
            Assert.AreEqual(ShotType.InsideFoot, Map(VuotCong()).type,     "vuốt cong");
            Assert.AreEqual(ShotType.Chip,       Map(GiatNgan()).type,     "giật ngắn mà nhanh");
        }

        [Test]
        public void GiatNganMaCham_KhongPhaiLop_MaLaCuSutNhe() {
            // Cùng độ dài 2 cm nhưng kéo lê 0.5 s: peakSpeed tụt dưới ngưỡng giật.
            var f = Swipe(2f, 0.5f, 0f);
            Assert.Less(f.peakSpeed, _cfg.chipMinPeakSpeedCmPerSec,
                "Tiền đề của test: cú vuốt này phải chậm hơn ngưỡng giật.");
            Assert.AreEqual(ShotType.Instep, Map(f).type);
        }

        [Test]
        public void VuotHinhChuS_KhongBiNhamThanhKnuckle() {
            // Hai bướu ngược chiều: độ cong tổng triệt tiêu về ~0, nhưng đây rõ ràng
            // không phải cú vuốt thẳng. straightness là cái chặn nó lại.
            using (var c = new SwipeCollector(64)) {
                float lenPx = Px(7.5f), bowPx = Px(1.2f);
                float2 At(float t) => new float2(300f + math.sin(t * 2f * math.PI) * bowPx,
                                                 400f + lenPx * t);
                c.Begin(At(0f), 0f, Dpi);
                for (int i = 1; i < 31; i++) c.Move(At(i / 31f), i / 31f * 0.15f);
                var f = c.End(At(1f), 0.15f).features;

                Assert.Less(math.abs(f.curvature), _cfg.knuckleMaxCurvatureCm,
                    "Tiền đề: hai bướu phải triệt tiêu nhau về độ cong.");
                Assert.AreNotEqual(ShotType.Knuckle, ShotMapper.Map(f, Aim, _cfg, 0f, 1u).type);
            }
        }

        // ------------------------------------------------------------------ knuckle

        [Test]
        public void Knuckle_XoayDungBangKhong_VaBatCoBatOnDinh() {
            var intent = Map(VuotDaiThang());

            Assert.AreEqual(ShotType.Knuckle, intent.type);
            Assert.AreEqual(0f, math.length(intent.spin), 1e-6f, "Knuckle phải KHÔNG xoáy.");
            Assert.IsTrue(intent.unstable, "Knuckle phải bật cờ bất ổn định riêng.");
        }

        [Test]
        public void Knuckle_KhongPhaiGiaLapBangXoayNgauNhien() {
            // Đây là ô nghiệm thu dễ bị lách nhất: gán spin ngẫu nhiên thì cú sút cũng "bay
            // loạn" và mắt thường không phân biệt nổi. Cách chứng minh: đổi seed 60 lần.
            // Nếu bất ổn định được nhét vào spin thì spin phải nhảy theo seed.
            var f = VuotDaiThang();
            for (uint seed = 1; seed <= 60; seed++) {
                var intent = ShotMapper.Map(f, Aim, _cfg, 0.15f, seed);   // có cả sai số thời điểm
                Assert.AreEqual(0f, math.length(intent.spin), 1e-6f,
                    $"seed {seed}: spin của knuckle phải luôn bằng 0, kể cả khi bấm lệch nhịp.");
                Assert.IsTrue(intent.unstable);
            }
        }

        [Test]
        public void CacLoaiKhac_KhongBatCoBatOnDinh() {
            Assert.IsFalse(Map(VuotVuaThang()).unstable);
            Assert.IsFalse(Map(VuotCong()).unstable);
            Assert.IsFalse(Map(GiatNgan()).unstable);
        }

        // ------------------------------------------------------------------ tốc độ

        [Test]
        public void VuotHetBienDo_ChoDungMaxSpeed_KhongVuot() {
            // Vuốt 12 cm, dài hơn hẳn maxSwipeLengthCm 8 cm.
            var intent = Map(Swipe(12f, 0.2f, 0f));
            Assert.AreEqual(_cfg.maxSpeed, intent.speed, 1e-4f);
            Assert.LessOrEqual(intent.speed, _cfg.maxSpeed);
        }

        [Test]
        public void DuongCongVongLenTrenMot_VanKhongLamTocDoVuotMaxSpeed() {
            // Kéo tiếp tuyến trong Inspector có thể làm AnimationCurve vọt quá 1.
            // Nếu ShotMapper không kẹp, lời hứa "không vượt" vỡ vì một cú kéo chuột.
            _cfg.lengthToSpeed = new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.5f, 2.5f), new Keyframe(1f, 1f));

            for (float cm = 0.5f; cm <= 12f; cm += 0.5f) {
                var intent = Map(Swipe(cm, 0.2f, 0f));
                Assert.LessOrEqual(intent.speed, _cfg.maxSpeed + 1e-4f, $"vuốt {cm} cm");
                Assert.GreaterOrEqual(intent.speed, _cfg.minSpeed - 1e-4f, $"vuốt {cm} cm");
            }
        }

        [Test]
        public void VuotCangDai_TocDoCangLon_KhongGiamNguoc() {
            float prev = -1f;
            for (float cm = 0.5f; cm <= 9f; cm += 0.5f) {
                float s = Map(Swipe(cm, 0.2f, 0f)).speed;
                Assert.GreaterOrEqual(s, prev - 1e-4f, $"tốc độ tụt khi vuốt dài hơn ({cm} cm)");
                prev = s;
            }
        }

        // ------------------------------------------------------------------ xoáy

        [Test]
        public void VuotCongPhai_XoayDuong_VuotCongTrai_XoayAm() {
            // Quy ước khớp BallSolver: Magnus ∝ cross(spin, v), bóng bay +Z,
            // cross(+Y, +Z) = +X, mà +X là bên phải theo mắt người sút.
            var phai = Map(Swipe(5f, 0.15f,  1.0f));
            var trai = Map(Swipe(5f, 0.15f, -1.0f));

            Assert.Greater(phai.spin.y, 0f, "Vuốt cong sang phải phải cho xoáy đẩy bóng sang phải.");
            Assert.Less(trai.spin.y, 0f);
            Assert.AreEqual(phai.spin.y, -trai.spin.y, 1e-3f, "Hai chiều phải đối xứng.");
            Assert.AreEqual(0f, phai.spin.x, 1e-6f, "Giai đoạn này chỉ có xoáy ngang quanh trục Y.");
            Assert.AreEqual(0f, phai.spin.z, 1e-6f);
        }

        [Test]
        public void VuotThang_XoayDungBangKhong_KhongPhaiSoHatTieu() {
            var f = VuotVuaThang();
            Assert.AreEqual(ShotType.Instep, Map(f).type, "Tiền đề: đây là cú mu bàn chân.");
            Assert.AreEqual(0f, math.abs(Map(f).spin.y), 1e-4f);
        }

        [Test]
        public void XoayKhongBaoGioVuotMaxSpinRadPerSec() {
            for (float bow = 0f; bow <= 3f; bow += 0.25f) {
                var intent = Map(Swipe(5f, 0.15f, bow));
                Assert.LessOrEqual(math.abs(intent.spin.y), _cfg.maxSpinRadPerSec + 1e-4f,
                    $"phình {bow} cm");
            }
        }

        // ------------------------------------------------------------------ tất định

        [Test]
        public void CungSeedCungInput_ChoShotIntentGiongHet() {
            var f = VuotCong();
            for (uint seed = 0; seed < 8; seed++) {
                var a = ShotMapper.Map(f, Aim, _cfg, 0.12f, seed);
                var b = ShotMapper.Map(f, Aim, _cfg, 0.12f, seed);

                Assert.AreEqual(a.aimPoint.x, b.aimPoint.x);
                Assert.AreEqual(a.aimPoint.y, b.aimPoint.y);
                Assert.AreEqual(a.aimPoint.z, b.aimPoint.z);
                Assert.AreEqual(a.spin.y, b.spin.y);
                Assert.AreEqual(a.speed, b.speed);
                Assert.AreEqual(a.type, b.type);
                Assert.AreEqual(a.quality, b.quality);
                Assert.AreEqual(a.unstable, b.unstable);
                Assert.AreEqual(a.scatterRadius, b.scatterRadius);
            }
        }

        [Test]
        public void SeedKhac_ChoTanMatKhac_NhungKhongDoiTocDoLoaiVaXoay() {
            var f = VuotCong();
            var goc = ShotMapper.Map(f, Aim, _cfg, 0.12f, 1u);

            bool coKhacBiet = false;
            for (uint seed = 2; seed <= 40; seed++) {
                var s = ShotMapper.Map(f, Aim, _cfg, 0.12f, seed);
                if (math.distance(s.aimPoint, goc.aimPoint) > 1e-4f) coKhacBiet = true;

                Assert.AreEqual(goc.speed, s.speed, 1e-6f,   "Seed không được đụng vào tốc độ.");
                Assert.AreEqual(goc.spin.y, s.spin.y, 1e-6f, "Seed không được đụng vào xoáy.");
                Assert.AreEqual(goc.type, s.type,            "Seed không được đụng vào loại sút.");
            }
            Assert.IsTrue(coKhacBiet, "Đổi seed phải làm điểm ngắm xê dịch, nếu không tản mát vô nghĩa.");
        }

        [Test]
        public void Seed0_VanChayDuoc() {
            // new Unity.Mathematics.Random(0) là trạng thái hỏng. CreateFromIndex băm seed
            // nên seed 0 vẫn dùng được — test này khoá cái quyết định đó lại.
            var intent = ShotMapper.Map(VuotCong(), Aim, _cfg, 0.12f, 0u);
            Assert.IsTrue(math.all(math.isfinite(intent.aimPoint)));
            Assert.Greater(intent.speed, 0f);
        }

        // ------------------------------------------------------- sai số thời điểm / tản mát

        [Test]
        public void BamChuanNhip_KhongTanMat_QualityBangMot() {
            var intent = ShotMapper.Map(VuotCong(), Aim, _cfg, 0f, 12345u);
            Assert.AreEqual(1f, intent.quality, 1e-6f);
            Assert.AreEqual(0f, intent.scatterRadius, 1e-6f);
            Assert.AreEqual(0f, math.distance(intent.aimPoint, Aim), 1e-6f,
                "Cú bấm hoàn hảo phải đi ĐÚNG chỗ ngắm.");
        }

        [Test]
        public void SomHayMuon_PhatNhuNhau() {
            var f = VuotCong();
            Assert.AreEqual(ShotMapper.Map(f, Aim, _cfg,  0.1f, 7u).quality,
                            ShotMapper.Map(f, Aim, _cfg, -0.1f, 7u).quality, 1e-6f);
        }

        [Test]
        public void PhanTan200Cu_LamLechChuKhongLamHong() {
            // Ô nghiệm thu "biểu đồ phân tán 200 cú", viết thành khẳng định số học.
            const int N = 200;
            var f = VuotCong();
            float timingError = _cfg.maxTimingErrorSeconds;      // tệ nhất có thể

            float3 tong = float3.zero;
            float xaNhat = 0f;
            float banKinh = ShotMapper.Map(f, Aim, _cfg, timingError, 1u).scatterRadius;
            Assert.Greater(banKinh, 0f, "Tiền đề: bấm lệch tối đa phải sinh tản mát.");

            for (uint i = 0; i < N; i++) {
                var s = ShotMapper.Map(f, Aim, _cfg, timingError, i);
                float3 lech = s.aimPoint - Aim;

                // 1. CÓ CHẶN TRÊN: đây là cái phân biệt "lệch" với "hỏng hoàn toàn".
                Assert.LessOrEqual(math.length(lech), banKinh + 1e-4f,
                    $"cú {i} văng ra ngoài bán kính tản mát");
                // 2. Chỉ lệch trong mặt phẳng khung thành, không thay đổi độ sâu.
                Assert.AreEqual(0f, lech.z, 1e-6f, $"cú {i} bị đẩy lệch theo trục Z");
                Assert.IsTrue(math.all(math.isfinite(s.aimPoint)), $"cú {i} ra NaN/Inf");

                tong += lech;
                xaNhat = math.max(xaNhat, math.length(lech));
            }

            // 3. Không thiên lệch một phía: tâm đám mây phải nằm gần điểm ngắm.
            float3 trungBinh = tong / N;
            Assert.Less(math.length(trungBinh), banKinh * 0.25f,
                "Đám mây tản mát lệch hẳn về một phía — người chơi sẽ học cách ngắm bù, hỏng ý đồ.");

            // 4. Phải THẬT SỰ có tản mát, không phải một hằng số hay toàn số 0.
            Assert.Greater(xaNhat, banKinh * 0.5f,
                "200 cú mà không cú nào ra tới nửa bán kính — phân bố bị dồn cục vào tâm.");
        }

        [Test]
        public void SaiSoCangLon_TanMatCangRong_DonDieu() {
            var f = VuotCong();
            float prev = -1f;
            for (float err = 0f; err <= _cfg.maxTimingErrorSeconds * 1.5f; err += 0.02f) {
                float r = ShotMapper.Map(f, Aim, _cfg, err, 3u).scatterRadius;
                Assert.GreaterOrEqual(r, prev - 1e-5f, $"tản mát giảm khi bấm lệch hơn ({err}s)");
                prev = r;
            }
        }

        [Test]
        public void SaiSoVuotTran_QualityKepVeKhong_KhongAm() {
            var intent = ShotMapper.Map(VuotCong(), Aim, _cfg,
                                        _cfg.maxTimingErrorSeconds * 10f, 5u);
            Assert.AreEqual(0f, intent.quality, 1e-6f);
        }

        // ------------------------------------------------------- không hằng số ma thuật

        [Test]
        public void DoiMaxSpeed_KetQuaDoiTheo() {
            var f = Swipe(12f, 0.2f, 0f);
            Assert.AreEqual(_cfg.maxSpeed, Map(f).speed, 1e-4f);

            _cfg.maxSpeed = 45f;
            Assert.AreEqual(45f, Map(f).speed, 1e-4f, "maxSpeed đang bị nướng cứng ở đâu đó.");
        }

        [Test]
        public void DoiMaxSpin_KetQuaDoiTheo() {
            var f = Swipe(5f, 0.15f, 3f);          // cong quá đà -> chạm trần xoáy
            float truoc = math.abs(Map(f).spin.y);

            _cfg.maxSpinRadPerSec *= 2f;
            Assert.AreEqual(truoc * 2f, math.abs(Map(f).spin.y), 1e-3f,
                "maxSpinRadPerSec đang bị nướng cứng ở đâu đó.");
        }

        [Test]
        public void DoiNguongNhanDangLop_KetQuaDoiTheo() {
            var f = GiatNgan();
            Assert.AreEqual(ShotType.Chip, Map(f).type);

            _cfg.chipMaxLengthCm = 0.5f;           // ngắn hơn cú giật -> không còn là lốp
            Assert.AreNotEqual(ShotType.Chip, Map(f).type, "Ngưỡng lốp đang bị nướng cứng.");
        }

        [Test]
        public void DoiNguongCongMaTrong_KetQuaDoiTheo() {
            var f = Swipe(5f, 0.15f, 0.4f);        // cong nhẹ
            _cfg.insideFootMinCurvatureCm = 0.01f;
            Assert.AreEqual(ShotType.InsideFoot, Map(f).type);

            _cfg.insideFootMinCurvatureCm = 5f;    // gần như không thể chạm tới
            Assert.AreNotEqual(ShotType.InsideFoot, Map(f).type, "Ngưỡng má trong đang bị nướng cứng.");
        }

        [Test]
        public void ThieuConfig_NemLoiNgayChuKhongAmThamDungSoBia() {
            var f = VuotCong();
            Assert.Throws<ArgumentNullException>(() => ShotMapper.Map(f, Aim, null, 0f, 1u));
        }

        // ------------------------------------------------------------------ tiện ích

        ShotIntent Map(in SwipeFeatures f) => ShotMapper.Map(f, Aim, _cfg, 0f, 1u);
    }
}
