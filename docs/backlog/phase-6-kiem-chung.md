← [Phase 5: Trình diễn](phase-5-trinh-dien.md) · [Mục lục](README.md)

---

# PHASE 6 — Kiểm chứng tự động

**2 task · tuần 30–32**

Hai task này biến kỷ luật "ngân sách → đo được" thành thứ chạy tự động
thay vì phụ thuộc trí nhớ của bạn.

---

## T33 — Bộ đo hiệu năng hồi quy

**Phụ thuộc:** T04, T27 · **Ước lượng:** ~2 ngày

Chạy một tập replay cố định trên máy thật và so số đo với lần chạy trước. Đây là thứ phát hiện
được "commit hôm qua làm chậm 1.2ms" trước khi nó chồng lên nhau thành thảm hoạ.

**Checklist nghiệm thu**
- [ ] Chạy 20 replay cố định, thu p50/p95 frame time, draw call, tris, bộ nhớ
- [ ] Xuất CSV có gắn git commit hash
- [ ] So với lần chạy trước, cảnh báo nếu p95 tệ đi quá 5%
- [ ] Chạy được bằng một lệnh, không cần thao tác tay trên máy
- [ ] Có bảng **Ngân sách / Đo được / Chênh** cho cả 8 trụ cột hình ảnh
- [ ] Ghi lại tên máy, phiên bản OS, nhiệt độ máy lúc bắt đầu và kết thúc

---

## T34 — Test ngâm và kiểm nhiệt

**Phụ thuộc:** T33 · **Ước lượng:** ~1 ngày

Tiêu chí nghiệm thu chính thức của M7. Không phải chuyện lo sau.

**Checklist nghiệm thu**
- [ ] Chạy tự động **20 phút liên tục**, không cần người ngồi canh
- [ ] Ghi frame time và nhiệt độ mỗi 10 giây suốt thời gian đó
- [ ] **Máy không cắm sạc** — sạc làm sai lệch hoàn toàn kết quả nhiệt
- [ ] Đạt: bậc B không xuống dưới **55fps** trong toàn bộ 20 phút
- [ ] Đạt: bậc C giữ vững **30fps**
- [ ] Xuất biểu đồ frame time theo thời gian, thấy rõ điểm bắt đầu tụt nhiệt nếu có
- [ ] Không rò rỉ bộ nhớ: mức dùng cuối chênh dưới 5% so với sau phút đầu

---

← [Phase 5: Trình diễn](phase-5-trinh-dien.md) · [Mục lục](README.md)

> Trước khi giao việc, đọc [quy tắc giao việc](README.md#quy-tắc-giao-việc) và
> dùng [mẫu prompt](README.md#mẫu-prompt-giao-việc). Đừng gộp nhiều task vào một phiên.
