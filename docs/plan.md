# Eleven Metres — Kế hoạch sản xuất v2

Game sút luân lưu chân thực trên **Unity 6.3 LTS**, cho iOS và Android, làm bởi một người.

**Cập nhật:** 24/08/2026 · **v2** — đã sửa theo phản biện ngoài
**Đi kèm:** [backlog/](backlog/README.md) — 34 đầu việc code được, có checklist nghiệm thu

> **Thay đổi so với v1:** pin Unity `6000.3` · bỏ bảng thông số đối thủ · mọi bảng chi phí
> chuyển sang cột `Ngân sách → Đo được` · chèn M1.5 Visual Prototype ở tuần 4–7 ·
> tách ngân sách texture theo bậc · thêm 3 gate FUN/WOW/SHIP · thêm user test sau M4 ·
> viết lại mục pháp lý theo hướng rà soát IP.

---

## 01 — Luận điểm

eFootball và EA Sports FC Mobile phải render **22 cầu thủ ở 60fps** trên cùng chiếc điện thoại
mà bạn sẽ nhắm tới. Game này render **hai người và một quả bóng**.

Đó là toàn bộ chiến lược. Ngân sách đa giác, ngân sách texture, ngân sách ánh sáng, ngân sách
thời gian GPU — tất cả đều được dồn vào một khoảnh sân 12 mét quanh chấm phạt đền. Không phải
vì bạn giỏi hơn đội ngũ của Konami hay EA, mà vì bạn giải một bài toán nhỏ hơn nhiều lần.

**Nguyên tắc chi phối toàn dự án:**

> Mỗi khi phân vân giữa hai lựa chọn, chọn cái làm hai nhân vật và quả bóng đẹp hơn.
> Không bao giờ tiêu ngân sách vào thứ nằm ngoài 12 mét quanh chấm phạt đền.

Hệ quả: sân vận động chỉ cần khung thành, khoảng 40m mặt cỏ, và một góc khán đài.
Mọi thứ còn lại là bóng tối, sương mù, và bokeh.

### Về việc so sánh với đối thủ

"Đẹp hơn eFootball" là **ngôi sao dẫn đường nội bộ**, không phải khẩu hiệu marketing.
Nó nằm trong tài liệu này để bạn biết mình đang nhắm đâu.

Nhưng **không bao giờ nêu tên đối thủ ra công khai** — vừa là marketing yếu (bạn tự đặt mình
vào thế so đo với thứ người ta đã quen), vừa tạo rủi ro pháp lý về quảng cáo so sánh.

*v1 có một bảng so sánh thông số đối thủ (số đa giác, kích thước texture, số shadow caster).
Bảng đó đã bị gỡ: mình không có dữ liệu thật cho những con số đó, và trình bày phỏng đoán
trong một cái bảng khiến nó trông như đã đo. Điều còn lại đúng là: 22 so với 2 thì đếm bằng mắt cũng thấy.*

---

## 02 — "Đẹp hơn" nghĩa là gì, đo được

Bốn lời hứa cụ thể. Nếu bốn thứ này làm tốt, người chơi sẽ thấy khác biệt ngay trong 5 giây đầu.

| # | Lời hứa | Vì sao game khác không làm |
|---|---|---|
| 1 | **Da thật** — tán xạ dưới bề mặt + texture mặt 4K | Chỉ khả thi khi có 2 nhân vật, không phải 22 |
| 2 | **Lưới sống** — mô phỏng Verlet, bóng làm lưới phồng và rung | Money shot của thể loại. Chưa game mobile nào làm tốt. |
| 3 | **Cỏ thật 3D** — có vệt cắt, biến dạng theo giày, trong đĩa 12m | Game khác phải phủ cả sân nên buộc dùng texture |
| 4 | **Máy quay điện ảnh** — Cinemachine + replay slow-mo có DOF | Chỉ dựng 12m nên mọi góc quay đều đẹp |

---

## 03 — Ba bậc thiết bị

| Bậc | Máy | Mục tiêu | Render scale | Cắt gì |
|---|---|---|---|---|
| **A** | iPhone 13+ (A15+), SD 8 Gen 1+ | 60 fps | 1.0 | không cắt |
| **B** | iPhone XR–12 (A12–A14), SD 778G/870 | 60 fps | 0.80 | cỏ còn 40%, tắt light shaft, bloom 1 pass |
| **C** | A11 trở xuống, SD 6-series, 3GB RAM | 30 fps | 0.65 | cỏ → texture, lưới → tĩnh, tắt SSS, khán giả tĩnh |

### Ngân sách texture theo bậc

Đây là chỗ v1 sai: một con số 400MB cho cả ba bậc. Máy bậc C với 3GB RAM sẽ bị hệ điều hành
giết vì hết bộ nhớ. Đo bằng **bộ nhớ texture thường trú**, không phải kích thước file.

| Bậc | Ngân sách | Đo được |
|---|---|---|
| A | ~400 MB | *TBD — chờ T04* |
| B | ~250 MB | *TBD* |
| C | ~140 MB | *TBD* |

Lưu ý: texture mặt 4K **không** phải thủ phạm — nén ASTC thì cả hai nhân vật chỉ khoảng 40MB.
Vấn đề nằm ở tổng thể, không ở một asset nào.

---

## 04 — Tám trụ cột hình ảnh và ngân sách

**Mọi con số dưới đây là ngân sách, không phải số đo.** Cột "Đo được" chỉ được điền
sau khi chạy trên máy thật với T04 (HUD hiệu năng). Đây là kỷ luật quan trọng nhất của v2 —
v1 trình bày phỏng đoán trong bảng khiến chúng trông như đã benchmark.

| Trụ cột | Kỹ thuật | Ngân sách | Đo được |
|---|---|---|---|
| Cỏ 3D | GPU instancing, mật độ giảm theo bán kính | ≤ 2.0 ms GPU | *TBD* |
| Da | Pre-integrated SSS qua LUT độ cong | ≤ 0.5 ms | *TBD* |
| Lưới | Verlet 600 hạt, 8 vòng lặp, Burst | ≤ 0.5 ms CPU | *TBD* |
| Hậu kỳ | LUT 3D + bloom + vignette + grain | ≤ 1.5 ms | *TBD* |
| Khán giả | Impostor animated, 1 draw call | ≤ 0.8 ms | *TBD* |
| Ánh sáng | 4 giàn đèn, Forward+, APV | *TBD* | *TBD* |
| Bóng | Motion blur theo vật thể | *TBD* | *TBD* |
| Sân khấu | Lightmap tĩnh + sương + bokeh | *TBD* | *TBD* |

### Hai điều chỉnh về tính năng Unity 6

**GPU Resident Drawer** giảm chi phí CPU khi gửi draw call. Nó **không** giảm chi phí GPU
về vertex, overdraw, hay băng thông — và đó mới đúng là chỗ cỏ trên mobile chết.
v1 gọi nó là "gần như miễn phí", đó là nói quá.

**Adaptive Probe Volumes** cần được kiểm chứng trên máy thật ở M0, không phải giả định.
Giữ đường lui về lightmap + light probe cũ như một **công tắc cấu hình**, không dựng
tầng trừu tượng ba backend — đó là over-engineering cho một người làm.

---

## 05 — Vật lý bóng

```
// Bóng size 5: m = 0.43 kg, r = 0.11 m, A = 0.0380 m², rho = 1.225

Lực cản   F_d = -0.5 * rho * Cd * A * |v| * v
          Cd = 0.45 khi |v| < 12 m/s   (dòng chảy tầng)
          Cd = 0.22 khi |v| > 20 m/s   (khủng hoảng cản — bóng sút mạnh)
          nội suy mượt ở khoảng giữa

Magnus    F_m = 0.5 * rho * Cl * A * r * (omega × v)
          Cl ~ 0.25 với xoáy điển hình 8-10 vòng/giây

Trọng lực F_g = m * g,  g = 9.81
```

Khủng hoảng cản là khí động học thể thao có tài liệu, không phải con số bịa. Nhưng nó là
**điểm khởi đầu**, không phải điểm kết thúc — T12 sẽ fit lại tham số từ video penalty thật.

### Kiến trúc: điểm quan trọng nhất của cả dự án

> Gameplay tính **vector phóng có thẩm quyền** tại khung hình chạm bóng.
> Animation được chọn theo loại cú sút, rồi **IK của Animation Rigging** bẻ cẳng chân
> và bàn chân để giày gặp đúng vị trí thật của bóng.
> **Animation không bao giờ được điều khiển vật lý.**

Làm ngược lại thì kết quả không tất định, không công bằng, và không thể cân bằng độ khó.

### Đồng hồ riêng cho solver

Solver chạy ở **1/120**, nhưng **không** đổi `Time.fixedDeltaTime` toàn cục —
làm vậy khiến mọi vật lý khác trong game chạy gấp đôi số bước.

Lý do sâu hơn để tách: solver phải là **hàm thuần gọi trước được**, để AI thủ môn
dự đoán quỹ đạo và UI ngắm vẽ được đường bay. Đây không chỉ là chuyện timestep.

### Knuckleball: tách vật lý khỏi gameplay

Cú knuckle có xoáy ~0 nên không có Magnus. Thay vào đó là một lực ngang tần số thấp.

Đây là **bổ sung gameplay**, không phải mô hình vật lý. Phải để nó ở assembly riêng,
có ràng buộc cứng về độ lệch tối đa / tần số / biên độ / seed — và đừng gọi nó là vật lý.

---

## 06 — Thủ môn

Bóng bay hết 11m trong khoảng **450ms**. Người cần khoảng **600ms** để với tới một góc.
Kết luận sinh học: **thủ môn phải đoán, không phản xạ.**

| Tham số | Ý nghĩa | Dễ | Thường | Khó |
|---|---|---|---|---|
| `p_read` | xác suất đọc đúng hướng từ chân trụ + góc hông | 0.30 | 0.52 | 0.72 |
| `t_react` | độ trễ phản ứng sau khi cam kết (ms) | 320 | 240 | 185 |
| `t_commit` | thời điểm bay so với lúc chạm bóng (ms) | −60 | −110 | −150 |
| `reach` | hệ số nhân bán kính bay chuẩn | 0.92 | 1.00 | 1.06 |
| `p_parry` | chạm được nhưng đẩy ra thay vì bắt dính | 0.70 | 0.45 | 0.28 |

**Độ khó chỉ được nằm ở `p_read` và `t_commit`, không bao giờ ở `reach`.**
Đẩy tốc độ bay vượt mức hợp lý về mặt vật lý là cách nhanh nhất để bị gọi là "ăn gian",
kể cả khi người chơi không diễn đạt được vì sao.

### Não thủ môn chia tầng

Sửa từ v1 — không tung xúc xắc nhị phân đúng/sai, mà trả về **độ tin cậy**:

1. **Tín hiệu trước sút** — chân trụ, góc hông, góc chạy đà, độ dài đà
2. **Suy luận** → phân phối xác suất trên 9 ô + một giá trị `confidence`
3. **Thời điểm cam kết** — `confidence` thấp thì hoãn, rất thấp thì đứng giữa
4. **Quỹ đạo bay** — sau khi cam kết **không được sửa hướng**
5. **Vùng với tới** — ràng buộc vật lý, mọi độ khó phải tôn trọng

Cái `confidence` này chính là thứ tạo ra hành vi do dự nửa chừng — thứ khiến thủ môn
trông như đang thật sự đọc người sút.

### Trí nhớ

Thủ môn nhớ 20 cú gần nhất. Sút một góc nhiều lần thì góc đó khó dần.
Cơ chế gần như miễn phí về code nhưng biến arcade từ trò may rủi thành cuộc đấu trí.

---

## 07 — Camera là trụ cột thiết kế

Không phải hạn chế kỹ thuật. **Trụ cột thiết kế.**

Chiến lược "chỉ dựng 12 mét" sụp đổ ngay khoảnh khắc có một camera replay tự do 360 độ.
Vì vậy mọi góc quay đều được đạo diễn sẵn, `ReplayOrbit` có giới hạn góc cứng, và có
test tự động khẳng định camera không bao giờ ra khỏi vùng đã dựng.

Đây là ràng buộc bạn chấp nhận **từ đầu**, không phải thứ phát hiện ở tuần 20.

---

## 08 — Lộ trình

**36 tuần cơ sở + 6 tuần dự phòng = envelope 42 tuần.** Với một người làm toàn thời gian.

36 tuần cho ra **beta đủ tính năng**, không phải bản phát hành đã đánh bóng.
Con số này thật, không massage cho dễ nhìn.

| M | Nội dung | Tuần | Tiêu chí thoát |
|---|---|---|---|
| **M0** | Nền tảng | 1–2 | Scene rỗng với quả cầu xám build và chạy được trên iOS + Android **thật**, đã đo frame time |
| **M1** | Vật lý bóng | 3–5 | Cú sút cong vào góc chữ A bằng khối xám; quỹ đạo khớp khung hình với video penalty thật |
| **M1.5** | **Visual prototype** | 4–7 | **Một nhân vật hero + thủ môn + ánh sáng + camera đứng trong sân. Không cần gameplay.** |
| **M2** | Vòng lặp sút/cản | 8–10 | 20 lượt liên tiếp vẫn thấy vui khi chưa có art nào |
| **M3** | Animation + IK | 11–14 | Giày chạm bóng đúng ở cả 4 loại cú sút. Có tiêu chí đo được (xem dưới). |
| **M4** | Golden Shot | 15–21 | **Một** cú sút duy nhất. Video 15 giây trên máy thật. |
| **M4.5** | User test | 22 | 10–20 người, blind test |
| **M5** | Vòng lặp game + Arcade | 23–26 | Loạt luân lưu đầy đủ từ menu tới kết quả, không crash |
| **M6** | Âm thanh + game feel | 27–29 | Test bật/tắt tiếng phải thấy khác biệt rõ rệt |
| **M7** | Tối ưu | 30–32 | 20 phút liên tục ở bậc B không dưới 55fps; bậc C giữ 30 |
| **M8** | Đánh bóng + phát hành | 33–36 | Được duyệt trên cả App Store và Google Play |
| — | **Dự phòng** | 37–42 | |

### Vì sao M1.5 tồn tại

Đây là điểm sửa quan trọng nhất về mặt lộ trình. Với thứ tự M1→M2→M3→M4 của v1,
bạn sẽ **không biết mình có làm nổi chất lượng hình ảnh đã hứa hay không cho tới tuần 17**.

Nếu đến lúc đó mới phát hiện asset nhân vật, animation, hay camera không đạt bar,
bạn đã tiêu 4 tháng. M1.5 đưa rủi ro lớn nhất lên trước — đan xen với M1, xen kẽ ngày code
và ngày art, không cần gameplay gì cả. Chỉ cần **một nhân vật đẹp đứng trong sân**.

### Tiêu chí đo được cho M3

v1 viết rằng những chỗ chưa hoàn hảo của mocap "chính là thứ khiến nó trông thật".
Sai. Trượt chân, giật gối, chạm sai, chuyển trọng tâm hỏng — đó là **lỗi, không phải nét duyên**.
Penalty đặc biệt nhạy vì có chân trụ và điểm chạm bóng.

- Sai số điểm chạm giày–bóng dưới ngưỡng đã đặt (tính bằng cm)
- Sai số vị trí chân trụ dưới ngưỡng đã đặt
- Không thấy trượt chân ở tốc độ 0.25×
- Vận tốc chậu hông / gối / khuỷu liên tục, không nhảy bậc

### Ba cổng chất lượng

| Gate | Ở đâu | Câu hỏi | Nếu trượt |
|---|---|---|---|
| **FUN** | cuối M2, tuần 10 | 20 lượt liên tiếp bằng khối xám vẫn vui? | Sửa gameplay. **Đừng đi tiếp.** |
| **WOW** | cuối M4, tuần 21 | Người lạ xem video 15s có thốt lên không? | Cắt scope, đừng cắt chất lượng |
| **SHIP** | cuối M7, tuần 32 | Chạy 20 phút không tụt nhiệt? | Cắt tính năng cho tới khi đạt |

### User test ở M4.5

10–20 người, **blind test**. Quan trọng: **quay bằng camera trong game thật, ở ngân sách
frame time sẽ phát hành, trên máy bậc B.** Một video cinematic 15 giây dựng riêng có thể
đánh bại eFootball trong khi chất lượng tổng thể thì không — đó là cái bẫy cần tránh.

Không dùng bảng chấm 5 tiêu chí. Đó vẫn là phán đoán chủ quan khoác áo bảng biểu.
Hỏi thẳng: *cái này trông như game gì?*

---

## 09 — Cấu trúc dự án

```
penalty_shootout/
├─ Assets/
│  ├─ _Project/
│  │  ├─ Art/                Characters · Stadium · Ball · VFX
│  │  ├─ Audio/
│  │  ├─ Code/
│  │  │  ├─ Ball/            BallSolver, TrajectoryPredictor  (asmdef)
│  │  │  ├─ Keeper/          KeeperBrain, DiveTable           (asmdef)
│  │  │  ├─ Shooter/         ShotResolver, StrikeIK           (asmdef)
│  │  │  ├─ Match/           ShootoutStateMachine, Scoring    (asmdef)
│  │  │  ├─ Presentation/    CameraDirector, ReplaySystem     (asmdef)
│  │  │  └─ UI/                                               (asmdef)
│  │  ├─ Settings/           URP asset cho từng bậc A/B/C
│  │  └─ Scenes/
│  └─ Plugins/
├─ Packages/manifest.json
└─ docs/
```

**Package:** `universal RP` · `cinemachine 3.x` · `animation.rigging` · `burst` ·
`collections` · `mathematics` · `inputsystem` · `addressables` · `localization` · `timeline`

**asmdef từ ngày đầu.** Không có chúng, Unity trên Mac biên dịch lại 20–40 giây mỗi lần sửa một dòng.

---

## 10 — Phiên bản Unity

**Pin `6000.3` (Unity 6.3 LTS). Không nâng engine giữa chừng sản xuất.**

| Phiên bản | Trạng thái |
|---|---|
| Unity 6000.0 (6.0 LTS) | **Hết hỗ trợ 16/10/2026** — còn ~7 tuần kể từ hôm nay |
| Unity 6000.3 (6.3 LTS) | Hỗ trợ tới **04/12/2027** |

v1 chỉ ghi "Unity 6 LTS", quá mơ hồ. Unity Hub nhiều khả năng sẽ cài đúng bản sắp chết.

Nguồn: [Unity 6 Releases & Support](https://unity.com/releases/unity-6/support) ·
[Unity 6.3 LTS is Now Available](https://unity.com/blog/unity-6-3-lts-is-now-available)

---

## 11 — Ngân sách

| Mức | Số tiền | Gồm gì |
|---|---|---|
| Tiết kiệm | $500 – 1,200 | Tài khoản dev, vài asset store, mocap miễn phí |
| Thực tế | $1,500 – 3,000 | Thêm mocap có phí, âm thanh, một máy test cũ |
| An toàn | $3,000 – 5,000 | Thêm thuê ngoài phần art khó, và đệm cho sự cố |

Chi phí cố định không tránh được: Apple Developer $99/năm, Google Play $25 một lần.

---

## 12 — Rà soát sở hữu trí tuệ

Đây **không phải tư vấn pháp lý**. Đây là danh sách cần rà soát trước khi phát hành,
và nếu có nghi ngờ thì hỏi luật sư.

**Chắc chắn không dùng:** tên cầu thủ thật · mặt cầu thủ thật · logo câu lạc bộ ·
thiết kế áo đấu có bản quyền · tên giải đấu (Premier League, La Liga, Champions League, V.League) ·
huy hiệu FIFA/UEFA/AFC/VFF · thiết kế bóng Adidas/Nike.

**An toàn:** kích thước sân và khung thành theo IFAB — đó là dữ kiện kỹ thuật.

**Cần rà soát, không tự phán quyết:** v1 viết quốc kỳ "được dùng thoải mái". Không đúng —
một số nước có quy định hạn chế dùng biểu tượng quốc gia trong sản phẩm thương mại.
Rà soát nhãn hiệu / bản quyền / giấy phép trước khi lên store, đừng suy ra từ một tài liệu kỹ thuật.

---

## 13 — Bước tiếp theo

1. Cài Unity Hub, tải đúng **`6000.3`**
2. Chạy [backlog/](backlog/README.md) từ **T01** — repo và LFS trước khi mở Unity lần đầu
3. **T04 (HUD hiệu năng) trước khi viết bất kỳ shader nào** — không có nó thì mọi ngân sách
   trong tài liệu này mãi mãi là phỏng đoán
4. Sau Phase 0, tự tay build lên điện thoại và cầm nó lên

> Cột "Đo được" trong tài liệu này đang trống. Việc điền nó là công việc thật của dự án,
> không phải thủ tục hành chính.
