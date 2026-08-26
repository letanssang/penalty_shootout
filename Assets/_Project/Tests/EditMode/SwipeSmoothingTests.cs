using Eleven.Shooter;
using UnityEngine;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Eleven.Tests.EditMode {

    /// <summary>
    /// T13 — chứng minh việc làm mượt trong SwipeAnalyzer THẬT SỰ có tác dụng.
    ///
    /// Cách làm: dựng CÙNG một cú vuốt hai lần, một lần sạch một lần cộng nhiễu, rồi so hai
    /// kết quả VỚI NHAU. Không có con số kỳ vọng nào viết cứng trong file này ngoài các ngưỡng
    /// sai lệch — nhờ vậy khi ai đó chỉnh lại thuật toán làm mượt, test vẫn còn đúng nghĩa.
    ///
    /// KIỂU NHIỄU: răng cưa (mẫu chẵn lệch +A, mẫu lẻ lệch -A). Đây là trường hợp XẤU NHẤT,
    /// cố ý chọn vậy. Nó xảy ra thật khi bộ số hoá cảm ứng nhảy qua lại giữa hai vị trí nội
    /// suy. Nhiễu ngẫu nhiên thường gặp hơn nhưng hiền hơn nhiều (đo được: cùng biên độ thì
    /// sai số chỉ bằng khoảng một nửa), nên qua được test này là qua được cả trường hợp kia.
    /// </summary>
    [TestFixture]
    public class SwipeSmoothingTests {

        const int   Samples    = 25;
        const float LengthCm   = 6f;
        const float DurationSec = 0.15f;

        // Biên độ nhiễu quy từ pixel thật trên máy 326 dpi (iPhone SE) sang cm, để con số có
        // nghĩa vật lý chứ không phải tự bịa:
        const float Dpi = 326f;
        /// Rung cảm ứng thường gặp trên panel tử tế: cỡ vài pixel.
        static float NhieuThuong => PhysicalUnits.ToCentimeters(3f, Dpi);
        /// Tay run / panel rẻ tiền: khoảng 4 lần mức thường. Đây là mức TỆ NHẤT còn thực tế.
        static float NhieuTayRun => PhysicalUnits.ToCentimeters(13f, Dpi);

        /// <summary>
        /// Cung hình sin phình sang phải bowCm, cộng nhiễu răng cưa biên độ noiseCm.
        /// Đi qua đúng SwipeAnalyzer thật, không mô phỏng lại thuật toán.
        /// </summary>
        static SwipeFeatures Analyze(float bowCm, float noiseCm) {
            var buf = new NativeArray<SwipeSample>(Samples, Allocator.Temp);
            try {
                for (int i = 0; i < Samples; i++) {
                    float t = (float)i / (Samples - 1);
                    float zigzag = ((i & 1) == 0 ? 1f : -1f) * noiseCm;
                    buf[i] = new SwipeSample {
                        position = new float2(math.sin(t * math.PI) * bowCm + zigzag, t * LengthCm),
                        time     = t * DurationSec,
                    };
                }
                return SwipeAnalyzer.Analyze(buf);
            } finally {
                buf.Dispose();
            }
        }

        static float SaiLechTuongDoi(float sach, float nhieu) => math.abs(nhieu - sach) / math.abs(sach);

        [Test]
        public void Nhieu_LamPhinhChieuDaiTho_DayLaTienDeCuaMoiTestConLai() {
            // Nếu test này hỏng thì nhiễu tổng hợp không đủ mạnh và mọi test dưới đây vô nghĩa.
            var sach  = Analyze(bowCm: 1f, noiseCm: 0f);
            var nhieu = Analyze(bowCm: 1f, noiseCm: NhieuTayRun);

            Assert.Greater(nhieu.length, sach.length * 1.15f,
                $"Nhiễu chưa đủ mạnh để test có ý nghĩa: sạch={sach.length} nhiễu={nhieu.length}");
        }

        [Test]
        public void LamMuot_GiuDuocDoCong_KhiCoNhieu() {
            // Đây là lý do việc làm mượt tồn tại: người chơi vuốt cong bằng tay run vẫn phải
            // được ăn xoáy đúng như người vuốt cong bằng tay vững.
            var sach  = Analyze(bowCm: 1f, noiseCm: 0f);
            var nhieu = Analyze(bowCm: 1f, noiseCm: NhieuTayRun);

            Assert.Greater(sach.curvature, 0f, "Tiền đề: cung sạch phải cong sang phải.");

            float saiLech = SaiLechTuongDoi(sach.curvature, nhieu.curvature);
            Assert.Less(saiLech, 0.08f,
                $"Độ cong lệch {saiLech:P1} khi tay run — mất chừng đó xoáy là người chơi cảm nhận được. " +
                $"sạch={sach.curvature} nhiễu={nhieu.curvature}");
        }

        [Test]
        public void NhieuMucThuong_GanNhuKhongAnhHuongDoCong() {
            var sach  = Analyze(bowCm: 1f, noiseCm: 0f);
            var nhieu = Analyze(bowCm: 1f, noiseCm: NhieuThuong);

            Assert.Less(SaiLechTuongDoi(sach.curvature, nhieu.curvature), 0.02f,
                "Rung cảm ứng bình thường không được làm suy suyển độ cong quá 2%.");
        }

        [Test]
        public void LamMuot_GiuDauDoCong_KhiCoNhieu() {
            // Sai dấu là hỏng nặng nhất: bóng xoáy ngược hướng người chơi vuốt.
            Assert.Greater(Analyze(bowCm:  1f, noiseCm: NhieuTayRun).curvature, 0f,
                "Vuốt cong sang phải mà ra xoáy trái.");
            Assert.Less   (Analyze(bowCm: -1f, noiseCm: NhieuTayRun).curvature, 0f,
                "Vuốt cong sang trái mà ra xoáy phải.");
        }

        [Test]
        public void NhieuCangManh_DoCongCangIt_BiKeoLech() {
            // Suy giảm phải ĐƠN ĐIỆU. Nếu sai số nhảy lung tung theo biên độ nhiễu thì có chỗ
            // nào đó trong thuật toán đang cộng hưởng với chu kỳ răng cưa.
            float goc = Analyze(bowCm: 1f, noiseCm: 0f).curvature;
            float truoc = 0f;
            foreach (float px in new[] { 1f, 3f, 6f, 13f, 26f }) {
                float sai = math.abs(Analyze(bowCm: 1f, noiseCm: PhysicalUnits.ToCentimeters(px, Dpi)).curvature - goc);
                Assert.GreaterOrEqual(sai, truoc - math.abs(goc) * 0.02f,
                    $"Sai số không đơn điệu ở mức nhiễu {px} px: trước={truoc} giờ={sai}");
                truoc = sai;
            }
        }

        [Test]
        public void DuongThangCoNhieu_KhongSinhDoCongGiaVuotNguongKnuckle() {
            // Hệ quả gameplay cụ thể của độ cong GIẢ: ShotMapper phân loại Knuckle theo
            // |curvature| <= knuckleMaxCurvatureCm. Nhiễu sinh ra độ cong giả vượt ngưỡng đó
            // thì người chơi vuốt thẳng bằng tay run sẽ mất cú knuckle — máy tưởng họ vuốt cong.
            // (Riêng điều kiện straightness của Knuckle là chuyện khác, xử lý ở test của T14.)
            var cfg = ShotMappingConfig.CreateDefault();
            try {
                float congGia = math.abs(Analyze(bowCm: 0f, noiseCm: NhieuTayRun).curvature);
                Assert.Less(congGia, cfg.knuckleMaxCurvatureCm,
                    $"Vuốt thẳng với tay run sinh độ cong giả {congGia} cm, vượt ngưỡng knuckle " +
                    $"{cfg.knuckleMaxCurvatureCm} cm.");
            } finally {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void DuongThangCoNhieu_VanKhongSinhDoCongGia() {
            // Ngưỡng tham chiếu: độ cong của một cung THẬT nhưng rất nhẹ (phình 0.2 cm).
            // Nhiễu không được giả mạo nổi tới mức đó.
            var nhieu = Analyze(bowCm: 0f, noiseCm: NhieuTayRun);
            float congThatRatNhe = math.abs(Analyze(bowCm: 0.2f, noiseCm: 0f).curvature);

            Assert.Less(math.abs(nhieu.curvature), congThatRatNhe,
                $"Nhiễu răng cưa trên đường thẳng đang bị đọc thành cú vuốt cong: " +
                $"giả={math.abs(nhieu.curvature)} so với cung thật rất nhẹ={congThatRatNhe}");
        }

        [Test]
        public void LamMuot_CHI_ApVaoDoCong_CacDacTrungKhacVanDungTrenMauTho() {
            // Đây là hợp đồng có chủ ý: length/peakSpeed/straightness cố tình đo trên mẫu THÔ,
            // vì chúng phản ánh việc ngón tay THẬT SỰ đã đi bao xa và nhanh cỡ nào.
            var sach  = Analyze(bowCm: 1f, noiseCm: 0f);
            var nhieu = Analyze(bowCm: 1f, noiseCm: NhieuTayRun);

            Assert.Greater(nhieu.length, sach.length,           "length phải đo trên mẫu thô");
            Assert.Greater(nhieu.peakSpeed, sach.peakSpeed,     "peakSpeed phải đo trên mẫu thô");
            Assert.Less(nhieu.straightness, sach.straightness,  "straightness phải đo trên mẫu thô");
        }

        [Test]
        public void LamMuot_KhongDoiHaiDauMut() {
            // f.start/f.end dùng để NGẮM, phải nằm đúng chỗ ngón tay chạm và nhấc lên.
            // Việc làm mượt (kể cả phần ngoại suy ở hai đầu) tuyệt đối không được rò vào đây.
            float noise = NhieuTayRun;
            var f = Analyze(bowCm: 1f, noiseCm: noise);

            Assert.AreEqual(noise, f.start.x, 1e-5f, "start.x bị làm mượt — sẽ ngắm lệch");
            Assert.AreEqual(0f, f.start.y, 1e-5f);
            Assert.AreEqual(noise, f.end.x, 1e-5f, "end.x bị làm mượt — sẽ ngắm lệch");
            Assert.AreEqual(LengthCm, f.end.y, 1e-5f);
        }
    }
}
