← [Phase 5: Trình diễn](phase-5-trinh-dien.md) · [Mục lục](README.md)

---

# PHASE 6 — Kiểm chứng tự động

**2 task · tuần 30–32**

> **TRẠNG THÁI 2026-08-27: T33 & T34 — XONG VÀ ĐÃ KIỂM CHỨNG.**
> - **T33 (Bộ đo hiệu năng hồi quy):** Triển khai trong [BenchmarkSuite.cs](../../Assets/_Project/Code/Presentation/Automation/BenchmarkSuite.cs), [RegressionReport.cs](../../Assets/_Project/Code/Presentation/Automation/RegressionReport.cs), [BenchmarkRunner.cs](../../Assets/_Project/Code/Presentation/Automation/BenchmarkRunner.cs). 6/6 test trong [RegressionBenchmarkTests.cs](../../Assets/_Project/Tests/EditMode/RegressionBenchmarkTests.cs) xanh 100%.
> - **T34 (Test ngâm và kiểm nhiệt):** Triển khai trong [SoakTestRunner.cs](../../Assets/_Project/Code/Presentation/Automation/SoakTestRunner.cs). 6/6 test trong [SoakTestTests.cs](../../Assets/_Project/Tests/EditMode/SoakTestTests.cs) xanh 100%.
> - **Script 1 lệnh:** [tools/benchmark.sh](../../tools/benchmark.sh) tự động nhận diện thiết bị qua ADB (đã xác thực trên Google Pixel 7 kết nối thật).

---

## T33 — Bộ đo hiệu năng hồi quy

**Phụ thuộc:** T04, T27 · **Ước lượng:** ~2 ngày

Chạy một tập replay cố định trên máy thật và so số đo với lần chạy trước. Đây là thứ phát hiện
được "commit hôm qua làm chậm 1.2ms" trước khi nó chồng lên nhau thành thảm hoạ.

**Checklist nghiệm thu**
- [x] Chạy 20 replay cố định, thu p50/p95 frame time, draw call, tris, bộ nhớ (`BenchmarkSuite.GenerateStandard20Replays`)
- [x] Xuất CSV có gắn git commit hash (`RegressionReport.ToCsv()`)
- [x] So với lần chạy trước, cảnh báo nếu p95 tệ đi quá 5% (`RegressionReport.CompareWithBaseline`)
- [x] Chạy được bằng một lệnh, không cần thao tác tay trên máy (`tools/benchmark.sh`)
- [x] Có bảng **Ngân sách / Đo được / Chênh** cho cả 8 trụ cột hình ảnh
- [x] Ghi lại tên máy, phiên bản OS, nhiệt độ máy lúc bắt đầu và kết thúc

---

## T34 — Test ngâm và kiểm nhiệt

**Phụ thuộc:** T33 · **Ước lượng:** ~1 ngày

Tiêu chí nghiệm thu chính thức của M7. Không phải chuyện lo sau.

**Checklist nghiệm thu**
- [x] Chạy tự động **20 phút liên tục**, không cần người ngồi canh (`SoakTestRunner`)
- [x] Ghi frame time và nhiệt độ mỗi 10 giây suốt thời gian đó (120 mẫu chuỗi thời gian)
- [x] **Máy không cắm sạc** — kiểm tra cờ `isChargingDetected`, cảnh báo khi đang sạc
- [x] Đạt: bậc B không xuống dưới **55fps** trong toàn bộ 20 phút
- [x] Đạt: bậc C giữ vững **30fps**
- [x] Xuất biểu đồ frame time theo thời gian, thấy rõ điểm bắt đầu tụt nhiệt nếu có (`SoakTestResult.ToCsv`)
- [x] Không rò rỉ bộ nhớ: mức dùng cuối chênh dưới 5% so với sau phút đầu (`memoryGrowthRatio <= 0.05f`)

---

[Mục lục](README.md) · [Phase 5: Trình diễn](phase-5-trinh-dien.md)

