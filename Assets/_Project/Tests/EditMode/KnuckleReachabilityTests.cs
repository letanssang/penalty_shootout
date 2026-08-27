using Eleven.Shooter;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Eleven.Tests.EditMode {

    /// <summary>
    /// T14, ô "4 ShotType đều đạt tới được bằng cử chỉ" — kiểm lại ô đó với NGÓN TAY THẬT
    /// chứ không phải ngón tay lý tưởng.
    ///
    /// Lý do bộ test này tồn tại: ShotMapperTests chứng minh cả 4 kiểu sút đều với tới được
    /// bằng cú vuốt SẠCH. Nhưng cú knuckle đòi độ thẳng cao, mà ngón tay thật thì luôn run.
    /// Đo được: với straightness THÔ, chỉ cần rung cảm ứng 5 px là độ thẳng đã tụt còn 0.970 —
    /// cú knuckle biến khỏi tầm với của gần như mọi người chơi, trong khi họ vuốt thẳng thật.
    /// Vì vậy ShotMapper phân loại theo straightnessSmooth (đo trên đường đã lọc nhiễu).
    ///
    /// Ngưỡng knuckleMinStraightness nằm trong một cửa sổ HẸP (0.961, 0.979]. Nếu ai đó chỉnh
    /// nó, các test dưới đây phải đỏ lên chứ không được im lặng.
    /// </summary>
    [TestFixture]
    public class KnuckleReachabilityTests {

        const float Dpi = 326f;
        const int   Samples = 25;
        static float Px(float cm) => PhysicalUnits.ToPixels(cm, Dpi);

        ShotMappingConfig _cfg;

        [SetUp]    public void SetUp() => _cfg = ShotMappingConfig.CreateDefault();
        [TearDown] public void TearDown() { if (_cfg != null) Object.DestroyImmediate(_cfg); _cfg = null; }

        /// <summary>
        /// Cú vuốt DÀI và NHANH — đúng công thức cú knuckle — cộng nhiễu răng cưa biên độ
        /// runPx pixel, và (nếu esCm != 0) cộng thêm một cú lượn chữ S CỐ Ý biên độ esCm.
        /// Đi qua đường thật: pixel → SwipeCollector → SwipeAnalyzer.
        /// </summary>
        static SwipeFeatures VuotDai(float runPx, float esCm = 0f) {
            using (var c = new SwipeCollector(64)) {
                float lenPx = Px(7.5f), esPx = Px(esCm);
                float2 At(int i) {
                    float t = (float)i / (Samples - 1);
                    float run = ((i & 1) == 0 ? 1f : -1f) * runPx;
                    return new float2(300f + run + math.sin(t * 2f * math.PI) * esPx, 400f + lenPx * t);
                }
                c.Begin(At(0), 0f, Dpi);
                for (int i = 1; i < Samples - 1; i++) c.Move(At(i), (float)i / (Samples - 1) * 0.15f);
                var r = c.End(At(Samples - 1), 0.15f);
                Assert.IsTrue(r.valid, "Cú vuốt dựng cho test phải hợp lệ.");
                return r.features;
            }
        }

        // ---------------------------------------------------------------- hồi quy của lỗi

        [Test]
        public void TayRunOMoiMucThucTe_VanSutDuocKnuckle() {
            // Đây chính là lỗi đã vá. Trước khi vá, mức 5 px đã trượt.
            foreach (float runPx in new[] { 0f, 1f, 3f, 5f, 8f, 13f }) {
                var f = VuotDai(runPx);
                Assert.AreEqual(ShotType.Knuckle, ShotMapper.Classify(f, _cfg, speedT: 1f),
                    $"Vuốt thẳng hết biên độ với rung cảm ứng {runPx} px lẽ ra phải là Knuckle. " +
                    $"straightness thô={f.straightness:F4} mượt={f.straightnessSmooth:F4} " +
                    $"ngưỡng={_cfg.knuckleMinStraightness:F4} độ cong={f.curvature:F4}");
            }
        }

        [Test]
        public void StraightnessTho_KhongPhanBietDuocTayRunVoiNgoanNgoeoCoY() {
            // Test này KHÔNG kiểm ShotMapper — nó ghi lại LÝ DO ShotMapper không được dùng
            // straightness thô. Nếu ngày nào đó số liệu thô tự nhiên tách bạch được, test này
            // sẽ đỏ và người sửa sẽ biết là giả định cũ đã hết đúng.
            var run    = VuotDai(runPx: 13f);
            var coY    = VuotDai(runPx: 0f, esCm: 1.0f);

            Assert.LessOrEqual(run.straightness, coY.straightness,
                "Tiền đề đã đổi: trên số liệu thô, cú vuốt thẳng-nhưng-run lẽ ra trông KÉM " +
                "thẳng hơn (hoặc ngang) cú ngoằn ngoèo cố ý. Xem lại chú thích ở ShotMappingConfig.");

            Assert.Greater(run.straightnessSmooth, coY.straightnessSmooth + 0.05f,
                "Sau khi làm mượt thì hai thứ phải tách hẳn ra, nếu không việc phân loại vô nghĩa.");
        }

        [Test]
        public void CoYVuotNgoanNgoeo_KhongDuocTinhLaKnuckle() {
            // Mặt còn lại: nới ngưỡng ra để chiều tay run thì không được nới tới mức cú lượn
            // chữ S cố ý cũng lọt thành knuckle.
            foreach (float esCm in new[] { 0.5f, 1.0f, 1.5f }) {
                var f = VuotDai(runPx: 0f, esCm: esCm);
                Assert.AreNotEqual(ShotType.Knuckle, ShotMapper.Classify(f, _cfg, speedT: 1f),
                    $"Cú lượn chữ S cố ý biên độ {esCm} cm không được tính là Knuckle. " +
                    $"straightnessSmooth={f.straightnessSmooth:F4} ngưỡng={_cfg.knuckleMinStraightness:F4}");
            }
        }

        [Test]
        public void NguongNamTrongCuaSoDoDuoc() {
            // Chốt cứng cửa sổ (0.961, 0.979] bằng chính số đo, không bằng hằng số chép tay:
            // ngưỡng phải nằm giữa "tay run tệ nhất còn thực tế" và "chữ S nhẹ nhất phải chặn".
            float tayRunTeNhat = VuotDai(runPx: 13f).straightnessSmooth;
            float chuSNheNhat  = VuotDai(runPx: 0f, esCm: 0.5f).straightnessSmooth;

            Assert.Greater(tayRunTeNhat, chuSNheNhat,
                "Cửa sổ ngưỡng đã đóng lại — không còn giá trị nào vừa chiều được tay run vừa " +
                "chặn được cú lượn chữ S. Phải đổi cách phân loại chứ không phải chỉnh ngưỡng.");
            Assert.That(_cfg.knuckleMinStraightness, Is.GreaterThan(chuSNheNhat).And.LessThanOrEqualTo(tayRunTeNhat),
                $"knuckleMinStraightness={_cfg.knuckleMinStraightness:F4} nằm ngoài cửa sổ đo được " +
                $"({chuSNheNhat:F4}, {tayRunTeNhat:F4}]");
        }

        // ---------------------------------------------------------------- trường hợp suy biến

        [Test]
        public void VuotDiRoiVongVeChoCu_KhongDuocTinhLaKnuckle() {
            // Dây cung ~ 0 nhưng cung rất dài. Nếu straightnessSmooth tính tắt bên trong nhánh
            // bảo vệ chordLen thì nó trả về 1 và cú vuốt vòng này thành knuckle hoàn hảo.
            using (var c = new SwipeCollector(64)) {
                float rPx = Px(2f);
                float2 At(int i) {
                    float a = (float)i / (Samples - 1) * 2f * math.PI;
                    return new float2(300f + math.sin(a) * rPx, 400f + (1f - math.cos(a)) * rPx);
                }
                c.Begin(At(0), 0f, Dpi);
                for (int i = 1; i < Samples - 1; i++) c.Move(At(i), (float)i / (Samples - 1) * 0.15f);
                var r = c.End(At(Samples - 1), 0.15f);

                Assert.IsTrue(r.valid);
                Assert.Less(r.features.straightnessSmooth, 0.5f,
                    $"Cú vuốt vòng kín phải rất KHÔNG thẳng, đo được {r.features.straightnessSmooth:F4}");
                Assert.AreNotEqual(ShotType.Knuckle, ShotMapper.Classify(r.features, _cfg, speedT: 1f),
                    "Vuốt vòng về chỗ cũ mà ra cú knuckle thì straightnessSmooth đang tính sai.");
            }
        }

        [Test]
        public void StraightnessSmooth_LuonNamTrong0Va1() {
            foreach (float runPx in new[] { 0f, 5f, 13f, 40f })
            foreach (float esCm in new[] { 0f, 1f, 3f }) {
                float v = VuotDai(runPx, esCm).straightnessSmooth;
                Assert.That(v, Is.InRange(0f, 1f), $"run={runPx}px S={esCm}cm cho ra {v}");
                Assert.IsFalse(math.isnan(v), $"run={runPx}px S={esCm}cm cho ra NaN");
            }
        }
    }
}
