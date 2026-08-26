← [Phase 2: Điều khiển](backlog/phase-2-dieu-khien.md)

---

# Rà soát checklist nghiệm thu T13 — SwipeAnalyzer

> **Nguồn: AI review (qua 9router, model `stealth/ox-alpha`), ngày 2026-08-26.**
> Review chỉ dựa trên nội dung dán vào prompt: toàn bộ `SwipeAnalyzer.cs` (đọc trực tiếp từ
> `Assets/_Project/Code/Shooter/SwipeAnalyzer.cs`) và **tên** 16 test trong
> `SwipeAnalyzerTests.cs` (không dán nội dung assert đầy đủ để giữ prompt gọn) — mô hình
> **không** tự chạy code, không thấy nội dung assert thật. **Chưa được người kiểm chứng lại
> bằng cách chạy `-batchmode -runTests` thật.** Mục DPI (mục 6) đã được tự grep lại thủ công
> để xác minh — xem ghi chú riêng bên dưới, không chỉ dựa lời AI.

## Tóm tắt theo từng mục checklist T13

| # | Mục checklist | Kết luận (đọc code tĩnh) |
|---|---|---|
| 1 | Độc lập tốc độ khung hình (30fps/60fps, <5%) | Có bằng chứng một phần — test tên đúng (`FrameRateIndependence_CurvatureDiffersUnderFivePercent`), thuật toán tích phân hình thang hội tụ về lý thuyết khi đổi mật độ mẫu. Chưa thấy threshold/assert thật. |
| 2 | `curvature` = diện tích có dấu / độ dài | Có bằng chứng rõ trong code: `signedArea` tích lũy qua `Cross(smooth[i]-start, d)` (khoảng cách có dấu tới dây cung), chia cho `len` (arc length). |
| 3 | Vuốt thẳng → curvature≈0, straightness≈1 | Có bằng chứng — test tên khớp chính xác, logic nhất quán (điểm trên dây cung → Cross≈0). |
| 4 | Làm mượt nhưng giữ cong thật | Có bằng chứng gián tiếp — lọc trung bình 3 điểm, test arc không cho thấy cong bị san phẳng. **Thiếu test trực tiếp so sánh có/không smoothing trên dữ liệu nhiễu tổng hợp**, và chưa có test với nhiễu ngón tay thật. |
| 5 | <3 mẫu bị từ chối, không crash | Có bằng chứng đầy đủ — 3 guard clause khớp đúng 3 test biên (0/1/2 mẫu). |
| 6 | Chuẩn hoá DPI (iPhone SE vs iPad) | **GAP — không có bằng chứng nào**, xem xác minh riêng bên dưới. |

## Xác minh riêng của tôi cho mục 6 (không chỉ dựa lời AI)

`SwipeAnalyzer.Analyze(NativeSlice<SwipeSample> samples)` nhận thẳng `float2 position` —
không có tham số DPI/screen size, không có phép chia nào theo `Screen.dpi` hay quy đổi
pixel → cm/inch ở bất kỳ đâu trong file.

```
grep -rln "SwipeAnalyzer\|SwipeSample" Assets/_Project/Code
→ chỉ ra đúng 1 file: Assets/_Project/Code/Shooter/SwipeAnalyzer.cs

grep -rniE "dpi|screen\.dpi|referencedpi|inch" Assets/_Project/Code Assets/_Project/Tests
→ chỉ có PerfHud.Renderer.cs (cỡ chữ HUD debug), không liên quan input/swipe
```

Kết luận: **chưa có lớp input/touch nào gọi `SwipeAnalyzer`** — nghĩa là pixel màn hình chưa
từng được chuyển thành `SwipeSample`, nên câu hỏi "DPI normalization đã đúng chưa" chưa có gì
để kiểm — code đó chưa tồn tại. Đây là việc thật còn thiếu, không phải hiểu lầm của AI review.

## Các mục không thể xác nhận chỉ bằng đọc code (theo đánh giá của AI review)

1. **Mục 1 (frame-rate independence):** cần chạy `FrameRateIndependence_CurvatureDiffersUnderFivePercent` thật trong Unity Test Runner để biết threshold/assert có thực sự pass.
2. **Mục 4 (giữ cong thật khi làm mượt):** cần test trên thiết bị thật với dữ liệu chạm ngón tay có nhiễu, hoặc bổ sung test tổng hợp (thêm jitter ngẫu nhiên vào đường cong biết trước, kiểm tra curvature output vẫn gần giá trị lý thuyết).
3. **Mục 6 (DPI):** cần viết lớp input chuyển pixel → đơn vị vật lý (hoặc xác nhận thiết kế là "không cần", nếu vậy phải sửa lại chính checklist), rồi test trên thiết bị thật (iPhone SE ~326 dpi vs iPad ~264 dpi) đo chênh lệch cùng một cử chỉ vật lý.

## Cách dùng bản này

Dùng làm điểm khởi đầu để ưu tiên việc còn thiếu ở T13/T14 (input pipeline + DPI), **không**
dùng để tự tick các mục checklist trong [phase-2-dieu-khien.md § T13](backlog/phase-2-dieu-khien.md) —
mục đó vẫn cần chạy test sống để xác nhận xanh, theo đúng quy ước chung của backlog dự án.
