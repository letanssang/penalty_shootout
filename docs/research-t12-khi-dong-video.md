← [Phase 1: Vật lý bóng](backlog/phase-1-vat-ly-bong.md)

---

# Nghiên cứu tham khảo cho T12 — Khí động học bóng đá & trích xuất quỹ đạo từ video

> **Nguồn: AI nghiên cứu (qua 9router, model `stealth/ox-alpha`), ngày 2026-08-26.**
> Đây là bản tóm tắt tham khảo để tăng tốc T12 (fit `BallParams` từ video penalty thật) —
> **chưa được người kiểm chứng lại**, số liệu và tên nguồn cần đối chiếu trước khi dùng làm
> căn cứ chính thức. Coi như điểm khởi đầu hợp lý cho `cdVLow`/`cdVHigh` khởi tạo, không phải
> số liệu đã đo thật (T12 vẫn cần fit trên ≥5 quả penalty thật để có bằng chứng, xem
> [phase-1-vat-ly-bong.md § T12](backlog/phase-1-vat-ly-bong.md)).

## 1) Tổng quan khí động học

**Tham số chuẩn (FIFA size 5):** m ≈ 0.435 kg, D ≈ 0.22 m, A ≈ 0.038 m²; không khí 20°C: ρ ≈ 1.225 kg/m³, ν ≈ 1.5×10⁻⁵ m²/s.

**Hệ số cản Cd & drag crisis** (Re = VD/ν):
- Re tới hạn của bóng đá ≈ **2.1–2.4×10⁵** → vận tốc chuyển pha **≈ 13–16 m/s (~47–58 km/h)**
- Dưới tới hạn (V < ~12 m/s): **Cd ≈ 0.42–0.50**
- Siêu tới hạn (V > ~18 m/s): **Cd giảm đột xuống ≈ 0.18–0.25**; một số nghiên cứu ghi nhận Cd tăng nhẹ trở lại trên 30 m/s
- Sút phạt đền thật: 27–36 m/s → gần như toàn quỹ đạo nằm ở miền siêu tới hạn. Cho nội suy `cdVLow`/`cdVHigh`: hợp lý là **cdVLow ≈ 12–14 m/s, cdVHigh ≈ 16–19 m/s**

**Lực Magnus Cl theo spin ratio S = rω/V:**
- S ≈ 0.05–0.10 → Cl ≈ 0.08–0.15
- S ≈ 0.20–0.30 → Cl ≈ 0.20–0.30, bão hoà quanh 0.30–0.35 khi S > 0.4
- Fit thực dụng: `Cl = Cl_max·tanh(kS)` hoặc `Cl = a·S/(b+S)`; Cl_max ≈ 0.3–0.35
- Sút penalty điển hình: 6–12 vòng/s → ω ≈ 40–75 rad/s → **S ≈ 0.15–0.30**

**Spin decay:** rất chậm — mất khoảng **2–10% mỗi giây bay** (hằng số thời gian τ ≈ 10–50 s). Với cú sút < 0.6 s, coi ω = const là chấp nhận được; nếu mô hình hóa: dω/dt = −kω với k ≈ 0.02–0.1 s⁻¹.

*Nguồn (chưa đối chiếu lại): Mehta (1985); Asai et al. (2007); Goff & Carré, Am. J. Phys. 77, 1020 (2009); Goff & Carré, Eur. J. Phys. 31, 775 (2010); Kiratidis & Leinweber (2018).*

## 2) Trích xuất quỹ đạo 3D từ video

**Số camera:** Tối thiểu **2 camera stereo đồng bộ** (tam giác hóa cho z); đặt hai bên góc 60°–120°, bao trọn toàn quỹ đạo. **3 camera** tăng độ bền trước che khuất. Phương án 1 camera rẻ nhất chỉ đúng nếu giả định quỹ đạo nằm trong mặt phẳng thẳng đứng (chấm 11m – tâm khung thành) rồi dùng homography — nhưng mất chuyển động ngoài mặt phẳng (knuckleball).

**Khung hình/giây:** Bóng vào lưới ~0.45–0.55s, tốc độ đầu 28–34 m/s:
- 60 fps → ~0.5 m/khung hình (**quá thô**)
- **≥ 240 fps** (slow-mo smartphone/GoPro) → ~10–15 cm/khung — khuyến nghị
- Tối thiểu tuyệt đối 120 fps. Shutter ≥ 1/1000s để giảm motion blur; nếu shutter chậm, dùng tâm blob mờ làm vị trí trung bình. Đồng bộ 2 camera bằng flash/clapperboard hoặc âm thanh.

**Hiệu chỉnh camera đơn giản (không cần lab):**
- Vật tham chiếu đã biết: cột trong rộng 7.32m, xà cao 2.44m, chấm phạt đền cách vạch cầu môn 11m, góc ô 5.5m
- Chọn ≥ 6 điểm không đồng phẳng mỗi camera (chân/đỉnh 4 cột, 2 đầu xà, chấm 11m, góc ô 5.5m) → giải **DLT (Abdel-Aziz & Karara 1971)** cho ma trận chiếu 3×4
- Tam giác hóa từng cặp tọa độ ảnh giữa 2 camera → (x, y, z). Chính xác kỳ vọng: ±2–5cm

**Phát hiện bóng:** phân ngưỡng màu + Hough circle/contour tròn, tính trọng tâm sub-pixel; hoặc tracker CSRT/KCF có tinh chỉnh mỗi khung.

**Xử lý che khuất:**
- Không nội suy mù quáng: đánh dấu gap, cho Nelder-Mead fit trực tiếp trên tập điểm còn lại (RK4 tích hợp từ trạng thái đầu — giữ điểm khởi đầu chính xác nhất vì sai số cộng dồn theo t)
- Gap ngắn (< 5 khung): nội suy spline bậc 5 / Chebyshev
- Hoặc Kalman filter mô hình gia tốc (gravity + drag) dự báo xuyên gap
- Lọc outlier bằng RANSAC trước khi fit; cân trọng số phần dư theo độ tin cậy tam giác hóa (điểm xa camera/trôi nhanh → trọng số thấp)

*(Nguồn phương pháp, chưa đối chiếu lại: Goff & Carré 2009; Nathan, Am. J. Phys. 76, 119 (2008).)*

---

← [Phase 1: Vật lý bóng](backlog/phase-1-vat-ly-bong.md)
