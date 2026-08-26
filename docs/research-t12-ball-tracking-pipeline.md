← [Phase 1: Vật lý bóng](backlog/phase-1-vat-ly-bong.md)

---

# Nghiên cứu tham khảo cho T12 — Pipeline trích xuất quỹ đạo 3D bóng từ video eFootball

> **Nguồn: AI nghiên cứu (qua 9router, model `stealth/ox-alpha`), ngày 2026-08-26.**
> Trả lời cho yêu cầu thiết kế cụ thể một pipeline Python + OpenCV, offline, dùng camera
> tĩnh sau lưng cầu thủ (xem quyết định nguồn dữ liệu đã chốt trong memory
> `project_t12_data_source`). **Chưa được người kiểm chứng lại, chưa chạy trên video thật.**
> Câu trả lời của model bị cắt cụt ở đúng phần cuối do hết `max_tokens=16000` (`finish_reason:
> "length"`) — nội dung 6 mục chính vẫn đầy đủ, nhưng danh sách "giả định rủi ro nhất" mà tôi
> yêu cầu ở cuối prompt không lọt vào `content`. Tôi phục hồi danh sách đó từ phần `reasoning`
> (chuỗi suy luận nội bộ) của model — model đã liệt kê xong 7 mục ở đó trước khi bị cắt, nên nội
> dung không bị mất, chỉ nằm ở trường khác trong response thô. Đã đánh dấu rõ đoạn này bên dưới.

## Xác nhận thực nghiệm (tự làm, không qua 9router)

Trước khi đọc phần thiết kế, một phát hiện quan trọng đã tự kiểm chứng trên `1.mp4`
(trích khung hình bằng `ffmpeg -ss <t> -frames:v 1`, xem bằng mắt):

- t=0.5s: camera tĩnh, sau lưng Ronaldo, nhìn thẳng khung thành, tỉ số POR 0–0.
- t=1.2s–1.6s: **vẫn cùng camera tĩnh đó**, thấy rõ cú sút, bóng bay và vào lưới, tỉ số đổi
  thành POR 1–0.
- t=1.8s: **vẫn cùng camera tĩnh**, Ronaldo quay lưng đi về, bóng nằm trong lưới.
- t=2.0s: **cắt cảnh** sang góc quay cận cảnh khác (celebration/replay).

→ Điểm cắt cảnh nằm **sau khi pha bóng đã kết thúc hoàn toàn** (bóng đã vào lưới, đứng yên),
không nằm giữa quỹ đạo bay. Kế hoạch "camera tĩnh suốt cả pha sút" mà tôi đã báo với người dùng
**vẫn đúng** — chỉ cần cắt video ở đúng thời điểm bóng chạm lưới/cột/đất là an toàn, không cần lo
pipeline ăn phải khung hình sau cắt cảnh miễn là dừng track đúng lúc (mục 5 của thiết kế dưới đây
cũng tự nói phải dừng track sau khi bóng chạm lưới). Đã xem nhanh khung đầu (t=0.5s) của cả
`2.mp4`–`5.mp4`: cùng kiểu góc camera tĩnh sau lưng cầu thủ, cùng khuôn hình khung thành — chưa
xem chi tiết diễn biến cắt cảnh riêng từng video, nhưng do cùng một engine/UI game nên rủi ro
thấp; **sẽ tự động dò điểm dừng track theo va chạm (mục 5) thay vì tin thời điểm cắt cảnh cố
định**, nên kể cả nếu video khác cắt cảnh sớm/muộn hơn cũng không ảnh hưởng.

---

## 1. Tách khung hình & mốc thời gian

Screen recording thường là **VFR (variable frame rate)** — khớp với phát hiện `ffprobe` trước đó
rằng 4/5 video có `avg_frame_rate` không phải tỉ số tròn. Đừng tin `fps` khai báo, lấy timestamp
từng khung hình trực tiếp:

```bash
ffprobe -v error -select_streams v:0 \
  -show_entries frame=best_effort_timestamp_time \
  -of csv=p=0 input.mp4 > timestamps.txt

ffmpeg -i input.mp4 -vsync 0 -start_number 0 frames/f_%05d.png
```

`-vsync 0` (hoặc `-fps_mode passthrough` ở ffmpeg ≥ 5.1) bắt buộc — mặc định `image2` muxer sẽ
drop/nhân bản khung hình theo fps giả định, làm lệch khớp timestamp ↔ file ảnh. Ghép 1-1 trong
Python bằng `assert len(ts) == len(frames)`.

## 2. Dò bóng mỗi khung hình

**Không dùng Hough làm chính** — kém với bóng < 12px và bị nhiễu bởi vạch vôi/biển quảng cáo
trắng. Thiết kế đề xuất: tận dụng lợi thế camera tĩnh bằng **median background subtraction**
(sân cỏ, vạch vôi, biển quảng cáo đều tĩnh → biến mất khỏi ảnh trừ nền) kết hợp mặt nạ trắng HSV,
lọc theo hình dạng blob, và cổng tìm kiếm theo vị trí dự đoán (constant-velocity gating):

```python
sample = [cv2.imread(f, cv2.IMREAD_GRAYSCALE) for f in frames[::10][:40]]
bg = np.median(np.stack(sample), axis=0).astype(np.uint8)

def detect(frame_bgr, prev_c, prev_r, v_pred):
    gray = cv2.cvtColor(frame_bgr, cv2.COLOR_BGR2GRAY)
    hsv  = cv2.cvtColor(frame_bgr, cv2.COLOR_BGR2HSV)
    white = cv2.inRange(hsv, (0, 0, 165), (179, 70, 255))
    _, fg = cv2.threshold(cv2.absdiff(gray, bg), 25, 255, cv2.THRESH_BINARY)
    mask  = cv2.bitwise_and(white, fg)
    mask  = cv2.morphologyEx(mask, cv2.MORPH_OPEN, np.ones((3,3), np.uint8))
    # cổng quanh vị trí dự đoán (constant velocity), lọc theo tỉ lệ khung/độ đặc,
    # fit tròn subpixel (Kasa least-squares) trên blob thắng cuộc
    ...
```

**Che khuất/trùng màu:** chân cầu thủ lúc chạm bóng → chấp nhận mất dấu vài khung hình, đánh dấu
`None`, nội suy sau (đừng ép fit). Vạch vôi bị background-median diệt sẵn vì tĩnh; nếu bóng lăn
dọc vạch làm dính blob thì bộ lọc tỉ lệ khung loại. Thủ môn/găng trắng: cổng tìm kiếm hẹp quanh dự
đoán + yêu cầu chuyển động liên tục là đủ. **Sau khi chạm lưới/cột: dừng track hẳn tại đó** — hình
dạng biến dạng, tốc độ đứt quãng, và đoạn này không dùng để fit khí động nữa (đây chính là cơ chế
tự nhiên giải quyết vấn đề cắt cảnh nêu trên). Luôn xuất video debug (vẽ vòng tròn đỏ mỗi khung
hình) để xem mắt trước khi tin số liệu.

## 3. Hiệu chỉnh camera từ khung thành đã biết

Hệ toạ độ: gốc = tâm khung thành trên mặt đất, x ngang, y lên, z dọc trục sút.

| Điểm ảnh (click tay) | Thế giới (m) |
|---|---|
| mép trong cột trái – trên | (−3.66, 2.44, 0) |
| mép trong cột phải – trên | (+3.66, 2.44, 0) |
| mép trong cột phải – dưới | (+3.66, 0, 0) |
| mép trong cột trái – dưới | (−3.66, 0, 0) |
| **chấm phạt đền** | (0, 0, 11) |

**Lưu ý quan trọng:** 4 góc khung thành đồng phẳng, và với góc quay gần fronto-parallel (đúng
kiểu góc eFootball) thì bài toán PnP-phẳng suy biến (ambiguous) — **bắt buộc** thêm chấm phạt
đền (một điểm ngoài mặt phẳng khung thành) để phá tính suy biến này, không chỉ dùng 4 góc khung
thành. Nếu thấy rõ vạch 5m50 (goal area) thì click thêm càng tốt.

**Không có intrinsics** (đây là screen recording một game render). Giải pháp: game render là
pinhole thuần (zero distortion — lợi thế so với camera thật), pixel vuông, principal point ≈ tâm
ảnh → chỉ còn 1 ẩn số `f`. Quét/tối ưu `f` sao cho reprojection error của `solvePnP` nhỏ nhất:

```python
def rep_err(f):
    K = np.array([[f,0,W/2],[0,f,H/2],[0,0,1]])
    ok, rvec, tvec = cv2.solvePnP(world, img, K, None, flags=cv2.SOLVEPNP_ITERATIVE)
    prj, _ = cv2.projectPoints(world, rvec, tvec, K, None)
    return np.mean(np.linalg.norm(prj[:,0]-img, axis=1)), rvec, tvec

res = minimize_scalar(lambda f: rep_err(f)[0], bounds=(0.4*W, 3*W), method="bounded")
```

Kỳ vọng reprojection RMS < 1.5px; nếu > 2–3px thì kiểm tra lại điểm click hoặc đưa cả `cx, cy`
vào bộ tối ưu.

## 4. Suy toạ độ 3D từ vị trí pixel + bán kính pixel

Bán kính bóng thật `R_b = 0.11m`. Vì trục z của bóng gần trùng trục camera, dùng bán kính ảnh làm
tín hiệu độ sâu chính:

```
Zc = f · R_b / r_px                    (độ sâu theo trục camera)
d  = normalize(K⁻¹ · [u, v, 1]ᵀ)       (tia qua pixel)
X_cam = Zc · d
X_world = Rᵀ · (X_cam − t)             (→ x, y, z đúng hệ cần)
```

**Hiệu chỉnh tỉ lệ toàn cục (mẹo quan trọng nhất của thiết kế này):** sai số hệ thống của `f`
hoặc phép đo `r_px` nhân thẳng vào mọi `z`. Neo bằng 2 sự kiện biết chắc tọa độ: khung hình chạm
bóng đầu tiên (`z ≈ 11`, tại chấm phạt đền) và khung hình bóng cắt mặt phẳng khung thành
(`z ≈ 0`). Quét một hệ số scale `γ` cho `Zc` sao cho khớp cả hai neo này — hấp thụ được sai lệch
hệ thống của `f` lẫn của phép đo bán kính, không chỉ dựa vào `f` ước lượng ở bước 3.

## 5. Lọc nhiễu/outlier

Loại khung hình khi: mất dấu hoàn toàn; tâm nhảy vượt cổng tìm kiếm (nghi sai); bán kính lệch
>30% so với trung vị lân cận (nghi dính vạch/bị che một phần); **và mọi khung hình sau lần chạm
đầu tiên (cột/lưới/đất)** — đoạn này không dùng để fit khí động.

- Robust polyfit từng trục (đa thức bậc 2, `x` có Magnus thì bậc 3), sigma-clip 3×MAD, lặp 3 lần.
- Mượt cuối bằng Savitzky–Golay (window 5–9, polyorder 2) trên từng trục, đặc biệt `z`.
- CSV chỉ ghi khung hình có detection hợp lệ + thời gian tăng đơn điệu; gap ≤3 khung hình → nội
  suy spline; gap dài hơn → tách thành đoạn riêng.

## 6. Sai số kỳ vọng

Ví dụ số: 1080p, camera cách khung thành ~14m → `f ≈ 700–900px`; bóng ở chấm phạt đền (cách
camera ~3m) đường kính ~55–65px, sát khung thành còn ~12px.

| Vị trí bóng | Khoảng cách tới camera | σ_z (độ sâu) | σ_x, σ_y |
|---|---|---|---|
| Đầu quỹ đạo (z≈11m) | ~3m | ~2–3cm | ~0.1cm |
| Giữa hành trình (z≈6m) | ~8m | ~20–25cm (thô) | ~0.4cm |
| Sát khung thành (z≈0) | ~14m | ~50–70cm (thô) | ~0.6cm |

Ngang (x) và cao (y) rất tốt (cm-level). **Độ sâu (z) là trục yếu**, đặc biệt xa camera — nhưng
sau khi mượt (Savitzky-Golay/polyfit trên ~20–30 khung hình) và hiệu chỉnh scale bằng 2 neo ở mục
4, ước tính RMS tổng còn **~10–25cm**. Đối chiếu ngưỡng T12 yêu cầu (RMS < 0.15m): **đạt được
nhưng không dư địa** — cần đủ 60fps, ≥1080p, bán kính fit subpixel, và bước mượt/hiệu chỉnh scale
đầy đủ; nếu thiếu một trong các điều kiện này thì rủi ro vượt ngưỡng.

---

## Danh sách giả định rủi ro nhất (phục hồi từ `reasoning`, xem cảnh báo ở đầu tài liệu)

1. **Principal point tại đúng tâm ảnh + pixel vuông** — đúng với hầu hết game render, nhưng nếu
   bản ghi màn hình có letterbox/UI che góc thì giả định sai; cần kiểm tra crop trước khi hiệu
   chỉnh.
2. **Bán kính↔độ sâu giả định bóng hiện đầy đủ, sắc nét** — motion blur hoặc che khuất một phần
   làm lệch bán kính đo được, kéo theo lệch độ sâu.
3. **Hình học 4 góc khung thành gần fronto-parallel là ill-conditioned** cho việc giải `f` — độ
   chính xác điểm click ảnh hưởng lớn; đã giảm thiểu bằng cách thêm chấm phạt đền, nhưng nên làm
   thêm kiểm tra độ nhạy (đổi `f` ±5% xem quỹ đạo suy ra lệch bao nhiêu).
4. **Dò bóng có thể nhầm với vạch vôi/găng thủ môn trắng** — cần cổng tìm kiếm theo thời gian
   (temporal gating) hoạt động đúng, không chỉ dựa màu.
5. **Giả định camera hoàn toàn tĩnh suốt pha sút** — tôi đã tự xác nhận điều này đúng cho đoạn
   trước khi bóng chạm lưới (xem mục "Xác nhận thực nghiệm" ở trên) nhưng chưa kiểm tra bằng
   diff pixel định lượng ở góc khung thành qua các khung hình, chỉ xem mắt.
6. **Đồng bộ timestamp khung hình với thời điểm chạm bóng thật** — có thể lệch vài khung hình so
   với thời điểm chân chạm bóng chính xác.
7. **Tỉ lệ khung hình/scale của bản ghi màn hình phải là tỉ lệ gốc (native)**, không bị co giãn
   phi tuyến (anamorphic) — cần kiểm tra trước khi tin `Screen.width/height` dùng làm principal
   point.

## Cách dùng bản này

Tài liệu tham khảo thiết kế cho pipeline trích xuất T12 (`docs/backlog/phase-1-vat-ly-bong.md`,
mục "Fit trên ít nhất 5 quả penalty thật"), **không** tự dùng để tick checklist T12 — vẫn cần
viết code thật, chạy trên 5 video đã có (`/Users/tansangle/penalty_video/`), và xác nhận RMS thật
trước khi coi là xong.
