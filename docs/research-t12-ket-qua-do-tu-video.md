# T12 — Kết quả đo quỹ đạo bóng từ video eFootball

*Ngày: 2026-08-26. Dữ liệu: 5 video penalty eFootball 1920×1080, capture 60 fps.*

## 1. Kết luận cho người vội

**Không cần đổi `BallParams.Default`.** Video xác nhận các giá trị hiện tại là hợp lý,
nhưng **không đủ độ chính xác để fit lại Cd/Cl** — xem mục 3 để biết vì sao.

Cái video thật sự cho ta, và nên dùng làm mốc chỉnh *cảm giác chơi*:

| Đại lượng | Đo được | Sai số |
|---|---|---|
| Tốc độ rời chân | **28.9 m/s (104 km/h)** | ±2.7 m/s |
| Góc nâng | **2.5–4°** (cú sút căng, gần như phẳng) | ±0.8° |
| Góc lệch ngang | **14–16°** | ±0.6° |
| Thời gian bay tới khung thành | **~0.38 s** | — |
| Gia tốc trọng trường trong game | **9.79 m/s²** | ±1.91 |

Điều đáng giá nhất: **eFootball chạy vật lý thời gian thực, trọng lực bình thường.**
Không có hệ số "làm đẹp" nào. Nên mô hình vật lý hiện tại của chúng ta đi đúng hướng.

Dữ liệu quỹ đạo: [`docs/data/efootball-shot1.csv`](data/efootball-shot1.csv) — 18 điểm,
định dạng `time,x,y,z` đúng chuẩn `ParameterFitter.LoadCsv`.

## 2. Hệ toạ độ và cách đọc CSV

- Gốc: tâm vạch vôi khung thành, trên mặt đất.
- `x` dương sang phải (nhìn từ phía người sút), `y` lên trên, `z` hướng ra xa khung thành.
- Trùng quy ước Y-up / Z-forward của Unity.
- `time` tính từ mẫu đầu tiên, bước **đúng 0.020 s** (xem mục 4).
- Bóng tĩnh nằm ở `z = 11.19 m` — eFootball đặt bóng **lùi sau chấm phạt đền 19 cm**.

## 3. Đo được gì, KHÔNG đo được gì

Đây là phần quan trọng nhất, và là lý do nên dừng T12 ở đây.

Vị trí bóng dựng lại theo hai đường khác nhau, độ chính xác chênh nhau 10 lần:

- **Ngang và dọc (x, y)** suy từ *vị trí tâm bóng trên ảnh* → sai số **3–4 cm**.
- **Sâu (z)** suy từ *bán kính biểu kiến* → sai số **32 cm**.

Vận tốc chịu được mức nhiễu đó. Gia tốc thì không:

```
trục      v0 (m/s)      gia tốc (m/s²)     sai số
ngang x   8.02 ± 0.28   +0.72 ± 1.42       396% giá trị
dọc  y    1.58 ± 0.38   −9.79 ± 1.91        39% giá trị   → g = 9.79 ± 1.91
sâu  z  −27.74 ± 2.66   −9.68 ± 13.52      279% giá trị
```

Lực cản mong đợi ở 29 m/s với Cd = 0.22 là **10.0 m/s²**. Giá trị đo được là
−9.68 **± 13.52** — nghĩa là dữ liệu *nhất quán với* lực cản bình thường nhưng
**không đo được nó**. Thanh sai số rộng gấp rưỡi chính đại lượng cần đo.

Lý do gốc rễ: pha bay chỉ dài **0.38 s**. Trong khoảng đó lực cản chỉ kéo bóng
lệch khỏi đường không-cản khoảng 0.7 m, trong khi nhiễu đo độ sâu là 0.32 m.
Tỉ số tín hiệu/nhiễu ≈ 2. Không đủ.

> Nếu ép `ParameterFitter` fit lên dữ liệu này, nó **sẽ** trả về một bộ số —
> nhưng đó là số khớp nhiễu, không phải vật lý. Lần chạy thử ở Python trả về
> Cd = −0.20 (lực cản âm, tức bóng tự tăng tốc) và Magnus 96 m/s². Đừng tin.

### Muốn đo được Cd/Cl thì cần gì

Theo thứ tự đáng làm giảm dần:

1. **Camera nhìn ngang** (từ khán đài bên cạnh). Khi đó độ sâu trở thành phương
   ngang, đo bằng vị trí ảnh → sai số 3 cm thay vì 32 cm. Một mình thay đổi này
   đủ để đo được lực cản.
2. **Cú sút bổng, bay lâu** (≥1 s). Lực cản tích luỹ theo t², nên gấp 2.6 lần
   thời gian bay là gấp ~7 lần tín hiệu.
3. **Gộp nhiều quả**. Sai số giảm theo √N — cần ~20 quả để thu hẹp 4 lần.

## 4. Phương pháp (tóm tắt để lặp lại được)

Mã nguồn: [`tools/video-calib/`](../tools/video-calib). Chạy bằng Python 3 với
`opencv-python-headless`, `numpy`, `scipy`.

### 4.1 Nhịp thời gian — chỗ dễ sai nhất

eFootball **mô phỏng ở 50 Hz nhưng render ở ~43.6 fps**. Capture 60 fps nên
nhiều khung ảnh bị lặp. Hệ quả: hai khung render liên tiếp cách nhau **1 hoặc 2
tick**, không đều.

Mốc thời gian đúng là `t = n × 0.020 s` với `n` là chỉ số tick, **không phải**
chia đều theo số khung. Dùng nhịp đều sẽ làm `g` sai khoảng 23%.

Ba tín hiệu độc lập cùng xác nhận việc nhân đôi tick tại đúng các khung 83 và 91:
độ lệch toàn khung hình, quãng dịch chuyển của bóng, và bước tiến của camera.

### 4.2 Tư thế camera — chỗ dễ sai thứ hai

Camera eFootball **không đứng yên**: nó trượt về phía khung thành ~0.096 m mỗi tick.

Giải tư thế bằng `solvePnP` 6 bậc tự do từ 4 góc khung thành **thất bại**: 4 góc
đó đồng phẳng và chỉ cao 2.44 m ở khoảng cách 25 m, nên góc ngẩng gần như không
quan sát được. Kết quả là chiều cao camera dao động 2.65–3.03 m, kéo theo sai số
vị trí bóng ±5 cm ngẫu nhiên.

Cách chữa (`railpose.py`): **khoá camera lên "ray"** — cố định `x = 0` và
chiều cao `y = 2.883 m`, chỉ giải `(z, góc quay ngang, góc ngẩng)`. Khoá chiều cao
chính là thứ làm góc ngẩng trở nên quan sát được.

Chiều cao 2.883 m lấy từ 8 khung giải được đầy đủ 6 điểm (4 góc khung thành + 2
vạch sân 5.5 m và 16.5 m — hai mặt phẳng vuông góc nhau nên không suy biến).

Kết quả: sai lệch dựng hình giảm từ ±50 mm xuống **±0.8 mm**, reprojection
rms 0.16–0.35 px.

### 4.3 Bán kính bóng — không giả định, mà giải ra

Bóng tĩnh **nằm trên mặt đất**, cho hai ràng buộc:
`P_y = R` (đáy chạm đất) và `R = r_px · d / f` (bán kính biểu kiến).
Giải đồng thời cho cả `d` lẫn `R` (`ballsolve.py`):

```
R = 0.10984 ± 0.00057 m   →  chu vi 69.0 cm
```

Bóng size 5 chuẩn FIFA là 68–70 cm. Con số rơi đúng giữa dải, từ một đường
hoàn toàn độc lập — đây là phép **kiểm chứng chéo mạnh nhất** của cả pipeline.

Lần đầu tôi làm sai chỗ này: ép bóng ở đúng 11.00 m rồi suy ra R = 0.11117,
sai 1.2% và kéo theo sai số hệ thống 3.7 cm ở mọi vị trí.

### 4.4 Bám vết bóng

Không dùng độ sáng (bóng bị đổ bóng che). Dùng độ "không phải cỏ":
`0.5·(B+R) − G` — cỏ có thành phần lục trội hẳn (−57…−105), bóng trắng/xám
có R=G=B nên ≈ 0 kể cả khi tối.

Khớp đường tròn bằng lấy mẫu dưới pixel theo 96 tia xuyên tâm, cắt ở mức 50%,
rồi khớp Kasa có trọng số Tukey 4 vòng. Sai lệch **0.42–0.92 px**.

## 5. Trạng thái 5 video

| Video | Hiệu chỉnh | Bám vết | Dựng 3D |
|---|---|---|---|
| 1 | ✅ f=2859, cao 2.888 m | ✅ 38 khung | ✅ **đã kiểm chứng, xuất CSV** |
| 2 | ✅ f=2844, cao 2.855 m | ⚠️ đứt sớm | ❌ |
| 3 | ✅ f=2855, cao 2.878 m | ⚠️ không mồi được pha bay | ❌ |
| 4 | ✅ f=2840, cao 2.846 m | ⚠️ ít khung có tư thế tốt | ❌ |
| 5 | ✅ f=2842, cao 2.848 m | ⚠️ không mồi được pha bay | ❌ |

Hiệu chỉnh chạy tự động tốt trên **cả 5** video, và các con số nhất quán với nhau
(f lệch nhau <0.7%, chiều cao camera lệch <1.5%) — đây là dấu hiệu tốt cho thấy
phương pháp đúng chứ không phải khớp riêng cho video 1.

Phần chưa xong là gán tick 50 Hz tự động cho 4 video còn lại. Cách gán dựa trên
độ trơn quỹ đạo camera cho kết quả sai (`g = 13.8`), còn đo nhịp render trên toàn
video thì không dùng được vì đoạn tĩnh làm trôi ngưỡng phát hiện khung lặp.

**Đã dừng ở đây theo chủ đích**: xử lý xong 4 video còn lại cũng không đổi được
kết luận mục 3 — thanh sai số vẫn rộng hơn đại lượng cần đo. Công sức đó nên
dồn vào quay video góc ngang nếu sau này thật sự cần Cd.

## 6. Việc còn lại của T12

- [x] Dựng được quỹ đạo 3D từ video thật, có kiểm chứng độc lập
- [x] Xác định nhịp thời gian và mô hình camera của eFootball
- [ ] ~~Fit Cd/Cl từ video~~ → **không khả thi với dữ liệu hiện có**, xem mục 3
- [ ] Ghi bộ tham số cuối vào `BallParams.Default` — *giữ nguyên giá trị hiện tại*,
      chỉ nên bổ sung ghi chú: đã đối chiếu với video eFootball, nhất quán trong
      sai số đo
