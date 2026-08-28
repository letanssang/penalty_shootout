using Eleven.Shooter;
using NUnit.Framework;
using Unity.Mathematics;

namespace Eleven.Tests.EditMode
{
    /// <summary>
    /// Phân loại TẠM cú vuốt đang dở — thứ cho phép clip sút khởi động từ trong pha chạy đà,
    /// trước khi người chơi nhả ngón.
    ///
    /// Điều được bảo vệ ở đây KHÔNG phải "đoán luôn đúng". Đoán sai chỉ làm phát nhầm clip;
    /// bóng vẫn bay theo cử chỉ thật vì vector phóng tính lúc nhả ngón. Cái phải đúng là:
    /// bản đọc tạm không được làm hỏng bản chính thức, và khi cử chỉ đã lộ hình thì nó phải
    /// hội tụ về đúng kết quả cuối cùng.
    /// </summary>
    [TestFixture]
    public class SwipeProvisionalTypeTests
    {
        const float Dpi = 460f;
        ShotMappingConfig _cfg;

        [SetUp]    public void SetUp()    => _cfg = ShotMappingConfig.CreateDefault();
        [TearDown] public void TearDown()
        {
            if (_cfg != null) UnityEngine.Object.DestroyImmediate(_cfg);
            _cfg = null;
        }

        static float Px(float cm) => PhysicalUnits.ToPixels(cm, Dpi);

        /// <summary>Cùng hình dạng cử chỉ với ShotMapperTests: thẳng lên, phình ngang theo sin.</summary>
        static float2 At(float t, float lengthCm, float bowCm) => new float2(
            300f + math.sin(t * math.PI) * Px(bowCm),
            400f + Px(lengthCm) * t);

        /// <summary>
        /// Nạp mẫu tới <paramref name="fraction"/> của cú vuốt rồi ĐỌC TẠM, không kết thúc.
        /// fraction = 1 nghĩa là ngón tay vẫn còn trên màn hình ở điểm cuối cùng.
        /// </summary>
        SwipeFeatures PeekAt(float fraction, float lengthCm, float durationSec, float bowCm,
                             int samples = 24)
        {
            using (var c = new SwipeCollector(64))
            {
                c.Begin(At(0f, lengthCm, bowCm), 0f, Dpi);
                for (int i = 1; i < samples; i++)
                {
                    float t = (float)i / (samples - 1);
                    if (t > fraction) break;
                    c.Move(At(t, lengthCm, bowCm), t * durationSec);
                }
                Assert.IsTrue(c.TryPeek(out var f),
                              $"Đọc tạm ở {fraction:P0} phải thành công (đã nạp {c.SampleCount} mẫu).");
                return f;
            }
        }

        SwipeFeatures Full(float lengthCm, float durationSec, float bowCm, int samples = 24)
        {
            using (var c = new SwipeCollector(64))
            {
                c.Begin(At(0f, lengthCm, bowCm), 0f, Dpi);
                for (int i = 1; i < samples - 1; i++)
                {
                    float t = (float)i / (samples - 1);
                    c.Move(At(t, lengthCm, bowCm), t * durationSec);
                }
                var r = c.End(At(1f, lengthCm, bowCm), durationSec);
                Assert.IsTrue(r.valid);
                return r.features;
            }
        }

        /// <summary>
        /// Đúng đường đi mà TouchSwipeReceiver.TryPeekShotType dùng — và cũng đúng đường đi
        /// mà ShotMapper.Map dùng. Bản tạm và bản chính thức KHÔNG có luật riêng nào khác nhau.
        /// </summary>
        ShotType TypeOf(in SwipeFeatures f) => ShotMapper.Classify(f, _cfg, ShotMapper.SpeedT(f, _cfg));

        // ─────────────────────────────────────────────────── TryPeek không phá bộ đệm

        [Test]
        public void DocTam_DuoiBaMau_TraVeFalse()
        {
            using (var c = new SwipeCollector(64))
            {
                Assert.IsFalse(c.TryPeek(out _), "Chưa Begin thì không có gì để đọc.");
                c.Begin(new float2(300f, 400f), 0f, Dpi);
                Assert.IsFalse(c.TryPeek(out _), "1 mẫu là chưa đủ.");
                c.Move(new float2(300f, 420f), 0.02f);
                Assert.IsFalse(c.TryPeek(out _), "2 mẫu vẫn chưa đủ — cùng ngưỡng End() dùng.");
                c.Move(new float2(300f, 440f), 0.04f);
                Assert.IsTrue(c.TryPeek(out _), "3 mẫu là đủ.");
            }
        }

        [Test]
        public void DocTam_KhongLamHongBanChinhThuc()
        {
            // Đọc tạm 5 lần giữa chừng rồi mới End. Kết quả phải trùng từng bit với một cú
            // vuốt y hệt không hề bị đọc tạm — nếu TryPeek lỡ tay xoá _count hay đặt
            // _isCollecting = false thì test này là chỗ phát hiện ra.
            const float len = 5f, dur = 0.15f, bow = 1.0f;
            const int samples = 24;

            SwipeFeatures withPeeks;
            using (var c = new SwipeCollector(64))
            {
                c.Begin(At(0f, len, bow), 0f, Dpi);
                for (int i = 1; i < samples - 1; i++)
                {
                    float t = (float)i / (samples - 1);
                    c.Move(At(t, len, bow), t * dur);
                    if (i % 4 == 0) c.TryPeek(out _);
                }
                var r = c.End(At(1f, len, bow), dur);
                Assert.IsTrue(r.valid, "Đọc tạm không được làm cú vuốt thành không hợp lệ.");
                // Begin(1) + 22 lần Move + mẫu nhấc ngón do End() ghi = 24.
                Assert.AreEqual(samples, r.sampleCount, "Đọc tạm không được nuốt mất mẫu nào.");
                withPeeks = r.features;
            }

            SwipeFeatures clean = Full(len, dur, bow, samples);
            Assert.AreEqual(clean.length,             withPeeks.length,             1e-6f);
            Assert.AreEqual(clean.curvature,          withPeeks.curvature,          1e-6f);
            Assert.AreEqual(clean.straightnessSmooth, withPeeks.straightnessSmooth, 1e-6f);
            Assert.AreEqual(clean.peakSpeed,          withPeeks.peakSpeed,          1e-6f);
        }

        [Test]
        public void DocTam_DungLuc_TraVeFalse_SauKhiDaEnd()
        {
            using (var c = new SwipeCollector(64))
            {
                c.Begin(At(0f, 5f, 0f), 0f, Dpi);
                for (int i = 1; i < 10; i++) c.Move(At(i / 10f, 5f, 0f), i * 0.015f);
                c.End(At(1f, 5f, 0f), 0.15f);
                Assert.IsFalse(c.TryPeek(out _), "Ngón đã nhấc thì không còn cử chỉ đang dở.");
            }
        }

        // ─────────────────────────────────────────────────── SpeedT khớp với Map

        [Test]
        public void SpeedT_ChoRaDungCongSuatMaMapDung()
        {
            // SpeedT được tách ra khỏi Map để TryPeekShotType dùng lại. Tách sai thì phân
            // loại tạm và phân loại chính thức xét trên hai con số công suất khác nhau.
            foreach (var f in new[] { Full(2f, 0.04f, 0f), Full(5f, 0.15f, 0f),
                                      Full(5f, 0.15f, 1.0f), Full(7.5f, 0.15f, 0f) })
            {
                var intent = ShotMapper.Map(f, new float3(0f, 1.2f, 11f), _cfg, 0f, 1u);
                float expected = math.clamp(math.lerp(_cfg.minSpeed, _cfg.maxSpeed,
                                                      ShotMapper.SpeedT(f, _cfg)),
                                            _cfg.minSpeed, _cfg.maxSpeed);
                Assert.AreEqual(expected, intent.speed, 1e-4f,
                                $"SpeedT lệch khỏi công suất Map tính (length {f.length:F2} cm).");
            }
        }

        // ─────────────────────────────────────────────────── hội tụ về kết quả cuối

        [Test]
        public void DocTam_ODiemCuoiCuVuot_TrungKieuSutChinhThuc()
        {
            // Ở mẫu cuối cùng trước khi nhấc ngón, bản tạm và bản chính thức nhìn gần như
            // cùng một tập mẫu, nên phải cho cùng kết luận. Đây là ràng buộc mạnh nhất mà
            // phân loại tạm phải giữ.
            var cases = new (string ten, float len, float dur, float bow)[]
            {
                ("giật ngắn (lốp)",  2.0f,  0.04f, 0f),
                ("vừa, thẳng",       5.0f,  0.15f, 0f),
                ("cong (má trong)",  5.0f,  0.15f, 1.0f),
                ("dài, thẳng đét",   7.5f,  0.15f, 0f),
            };

            foreach (var c in cases)
            {
                ShotType chinhThuc = TypeOf(Full(c.len, c.dur, c.bow));
                ShotType tam       = TypeOf(PeekAt(1f, c.len, c.dur, c.bow));
                Assert.AreEqual(chinhThuc, tam, $"Cử chỉ '{c.ten}' lệch kết luận ở mẫu cuối.");
            }
        }

        [Test]
        public void DocTam_CuVuotCong_NhanRaMaTrongTuKhoangGiuaCuVuot()
        {
            // Độ cong là dấu hiệu lộ sớm nhất: chỉ cần qua đỉnh bướu là thấy. Nếu ngưỡng này
            // tụt xuống, người chơi sẽ vuốt má trong mà nhìn thấy chân sút mu bàn chân.
            for (float frac = 0.7f; frac <= 1.001f; frac += 0.1f)
            {
                var f = PeekAt(frac, 5f, 0.15f, 1.0f);
                Assert.AreEqual(ShotType.InsideFoot, TypeOf(f),
                                $"Ở {frac:P0} cú vuốt cong phải đã ra má trong " +
                                $"(độ cong đọc được {f.curvature:F3} cm).");
            }
        }

        /// <summary>
        /// Đây là kiểu nhập liệu THẬT của trò chơi: luật bắt người chơi nhả ngón đúng lúc chân
        /// chạm bóng, nên họ vẽ xong cử chỉ rồi GIỮ ngón chờ. Trong lúc giữ, quãng đường không
        /// dài thêm còn thời gian thì có — tức đặc trưng đã là bản cuối. Bản đọc tạm ở đúng
        /// khoảnh khắc đó phải trùng bản chính thức, nếu không thì cả cơ chế này vô nghĩa.
        /// </summary>
        [Test]
        public void DocTam_TrongLucGiuNgonChoNhaDungLuc_TrungKieuSutChinhThuc()
        {
            var cases = new (string ten, float len, float dur, float bow)[]
            {
                ("giật ngắn (lốp)",  2.0f,  0.04f, 0f),
                ("vừa, thẳng",       5.0f,  0.15f, 0f),
                ("cong (má trong)",  5.0f,  0.15f, 1.0f),
                ("dài, thẳng đét",   7.5f,  0.15f, 0f),
            };

            foreach (var c in cases)
            {
                ShotType chinhThuc = TypeOf(Full(c.len, c.dur, c.bow));

                using (var col = new SwipeCollector(256))
                {
                    col.Begin(At(0f, c.len, c.bow), 0f, Dpi);
                    for (int i = 1; i < 24; i++)
                        col.Move(At(i / 23f, c.len, c.bow), i / 23f * c.dur);

                    // Giữ nguyên vị trí 0.5 giây, 60 khung hình — đúng như ngón tay đứng yên.
                    for (int k = 1; k <= 30; k++)
                        col.Move(At(1f, c.len, c.bow), c.dur + k * (0.5f / 30f));

                    Assert.IsTrue(col.TryPeek(out var f));
                    Assert.AreEqual(chinhThuc, TypeOf(f),
                                    $"Cử chỉ '{c.ten}' bị giữ 0.5s thì đọc ra {TypeOf(f)} " +
                                    $"thay vì {chinhThuc}.");
                }
            }
        }

        /// <summary>
        /// Giới hạn ĐÃ BIẾT, ghi lại bằng số chứ không giấu: một cú vuốt dài đang ở 20-40%
        /// chặng đường vừa ngắn vừa nhanh — không phân biệt được với cú lốp, vì ở khoảnh khắc
        /// đó nó ĐÚNG LÀ giống hệt cú lốp.
        ///
        /// Chấp nhận được, vì hai lẽ. Một: sai ở đây chỉ làm phát nhầm clip, bóng vẫn bay theo
        /// cử chỉ thật do vector phóng tính lúc nhả ngón. Hai: chỉ trúng khi người chơi còn
        /// đang vẽ dở tại đúng thời điểm chốt clip, mà lối chơi thật là vẽ nhanh rồi giữ —
        /// trường hợp đã có test ở trên.
        ///
        /// Đã thử chặn bằng ngưỡng thời lượng và BỎ: nó cướp mất hoạt ảnh lốp của người vuốt
        /// lốp rồi giữ ngón chờ nhả. Xem chú thích trong TouchSwipeReceiver.TryPeekShotType.
        /// </summary>
        [Test]
        public void DocTam_CuVuotDaiDangVeDoDang_CoTheDocNhamThanhLop()
        {
            var f = PeekAt(0.2f, 7.5f, 0.15f, 0f);
            Assert.AreEqual(ShotType.Chip, TypeOf(f),
                            "Nếu test này đổi kết quả thì hành vi đã đổi — cập nhật lại phần " +
                            "'Nợ còn lại' trong docs/backlog/phase-7-hoat-anh-ik.md.");
            Assert.Less(f.length, _cfg.chipMaxLengthCm,
                        $"Lý do đọc nhầm phải là 'còn ngắn' ({f.length:F2} cm), không phải gì khác.");
        }
    }
}
