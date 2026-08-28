# Hoạt ảnh Mixamo — những gì đã đưa vào và vì sao

**Ngày:** 2026-08-28 · **Nguồn:** Mixamo Soccer Game Pack (55 clip) + 3 clip lẻ · **Đích:**
`Assets/_Project/Art/Animations/Mixamo/`

Tài liệu này ghi lại một quyết định lọc và lý do đằng sau nó, để lần sau không ai
phải mở lại 55 file mà đoán.

---

## 1. Đã đưa vào 22 clip

Tất cả đều **60 fps**, **Humanoid**, `humanMotion = true` — nghĩa là retarget được sang
model cầu thủ thật sau này mà không phải dựng lại Animator. Số đo đầy đủ nằm ở
[docs/data/mixamo-clip-report.tsv](data/mixamo-clip-report.tsv), sinh lại được bằng
menu **Eleven ▸ Art ▸ Report Mixamo Clips**.

### Người sút — `Kicker/`

| File | Giây | Khung | Dịch chuyển | Dự kiến dùng cho |
|---|---|---|---|---|
| `PenaltyKick.fbx` | 1.50 | 90 | 2.61 m | `StrikeInstep` — clip trung tâm của cả phase |
| `KickSoccerball_A.fbx` | 0.57 | 34 | 0.00 m | `StrikeInsideFoot` — sút tại chỗ, không có đà |
| `KickSoccerball_B.fbx` | 0.50 | 30 | 0.00 m | `StrikeChip` hoặc `StrikeKnuckle` — sút tại chỗ |
| `StrikeForwardJog.fbx` | 1.30 | 78 | 2.94 m | `StrikeKnuckle` — sút khi đang chạy |
| `JogForward.fbx` | 0.82 | 49 | 2.01 m | `RunUp` — clip lặp |
| `OffensiveIdle.fbx` | 10.55 | 633 | 0.00 m | `Idle` |
| `Celebrate.fbx` | 8.53 | 512 | 0.00 m | `Celebrate` — Mixamo *Victory* |
| `Dejected.fbx` | 4.80 | 288 | 0.00 m | `Dejected` — Mixamo *Rejected* |

### Thủ môn — `Keeper/`

| File | Giây | Khung | Dịch chuyển | Dự kiến dùng cho |
|---|---|---|---|---|
| `KeeperIdle_A.fbx` | 4.62 | 277 | 0.00 m | Đứng chờ, lặp — bản dài |
| `KeeperIdle_B.fbx` | 3.33 | 200 | 0.00 m | Đứng chờ, lặp — bản gọn |
| `KeeperSidestep_A.fbx` | 0.52 | 31 | 1.20 m | Dịch ngang trước khi bay |
| `KeeperSidestep_B.fbx` | 0.50 | 30 | 1.50 m | Dịch ngang, biên độ lớn hơn |
| `KeeperDivingSave_A.fbx` | 3.40 | 204 | 2.64 m | Bay người cứu thua |
| `KeeperDivingSave_B.fbx` | 3.23 | 194 | 2.81 m | Bay người cứu thua, hướng/độ cao khác |
| `KeeperBodyBlock_A/B/C.fbx` | 2.45–3.42 | 147–205 | 0.55–0.81 m | Chắn bóng ở giữa khung |
| `KeeperCatch_A/B/D.fbx` | 1.25–2.73 | 75–164 | 0.43–1.04 m | Bắt dính bóng |
| `KeeperMiss.fbx` | 2.93 | 176 | 0.46 m | Phản ứng khi thủng lưới |
| `KeeperCelebrate.fbx` | 6.18 | 371 | 1.11 m | Ăn mừng sau khi cản được — Mixamo *Rallying* |

Giữ nhiều biến thể của body block và catch là cố ý: một loạt sút có 5–10 quả, dùng
đúng một clip cứu thua cho cả loạt thì nhìn ra ngay là máy.

---

## 2. Đã loại, kèm lý do

| File | Lý do |
|---|---|
| `goalkeeper catch (3)` | Root dịch **4.06 m** — đây là chạy ra bắt bóng bổng giữa sân, không phải cứu phạt đền |
| `goalkeeper scoop` | Root dịch **3.88 m** — cùng lý do |
| `soccer trip` | Ngã vì bị phạm lỗi trong lúc chạy; không có chỗ dùng trong loạt luân lưu |
| `transition` | 0.77 s, không rõ nội dung, không khớp pha nào trong `KickPhase` |

Ngưỡng "dịch chuyển > 3 m thì loại" là tiêu chí đo được, không phải cảm tính: thủ môn
phạt đền xuất phát trên vạch và pha cứu thua gói gọn trong 1–2 m. Clip nào đi xa hơn thế
là mocap bóng đá sân lớn, ép vào chỉ tổ phải cắt.

36 clip còn lại trong gói (`header`, `kneeing`, `stall`, `scissor kick`, `throw in`,
`soccer tackle`, `jog backward/strafe`, `goalkeeper drop kick/overhand throw/pass/placing
ball/directing`…) là bóng đá sân lớn, không dính gì tới luân lưu. File gốc vẫn nằm ở
`~/Downloads/Soccer Game Pack` nếu sau này cần.

---

## 3. Hai clip tải lẻ — đã đủ `KickerClip`

Ba clip phản ứng không có trong gói bóng đá, tải riêng từ Mixamo: `Celebrate` (*Victory*),
`Dejected` (*Rejected*) và `KeeperCelebrate` (*Rallying*). Cả ba đều **60 fps**. Hai clip đầu
dịch chuyển **0.00 m** — ăn mừng và cúi mặt tại chỗ, đúng thứ cần khi nhân vật đứng nguyên
chỗ sau cú sút.

Hai điều cần biết trước khi dựng Animator:

- **`Celebrate` dài 8.53 s (512 khung)** và **`KeeperCelebrate` dài 6.18 s (371 khung)** —
  dài gấp hai tới ba nhịp cần cho một quả trong loạt luân lưu. Nên cắt lấy khoảng 2–3 s đầu
  bằng cách thêm một clip con trong Inspector, đừng phát hết. Đây là quyết định của người
  dựng animation, không phải của import.
- **`KeeperCelebrate` dịch 1.11 m** (0.18 m/s — chậm). XZ không bake vào pose nên nó sẽ chạy
  tại chỗ; ở tốc độ đó thì không lộ. Nếu sau này thấy trượt chân thì bake XZ vào pose riêng
  cho clip này.
- **`FollowThrough` vẫn chưa có clip riêng** và có lẽ không cần: bốn clip sút đều đã chứa
  đoạn vung chân theo đà ở cuối. Cắt sub-range từ đuôi `PenaltyKick` (90 khung) là đủ. Chốt
  ở T35 khi đo `ContactNormalizedTime` — biết khung chạm bóng nằm ở đâu thì biết luôn đoạn
  sau nó dài bao nhiêu.

Đến đây bộ clip đã phủ hết `KickerClip` và mọi trạng thái thủ môn mà Phase 7 cần. Không
còn khoảng trống nào chặn T35.

---

## 4. Avatar gốc — đã xong

`Assets/_Project/Art/Characters/XBot.fbx` (Mixamo ▸ Characters ▸ X Bot) đang là Avatar gốc
của cả bộ; 22 clip đều **CopyFromOther** từ nó, nên muscle mapping đồng nhất — cột `avatar`
trong báo cáo ghi `copied`, đó là dấu hiệu đúng chứ không phải lỗi.

Đã đo, không phải tin theo tên file:

- **68 transform**, `GetBoneTransform()` trả đủ `Hips / LeftFoot / RightFoot / Toes / Head / Spine`
- **2 SkinnedMeshRenderer, 28 374 đỉnh** — có mesh thật, không phải bộ xương trần
- **Cánh tay chúc 0.0° so với phương ngang** → đúng T-pose, Unity auto-map xương Humanoid
  không cần Enforce T-Pose

`MixamoModelImport` tự dò FBX đầu tiên trong `Art/Characters/`, nên sau này thay X Bot bằng
model cầu thủ thật chỉ cần đổi file, không sửa code. Khi thư mục trống thì nó lùi về
`Kicker/OffensiveIdle.fbx` làm nguồn tạm.

> 28 374 đỉnh là hơi nặng cho mục tiêu di động của Phase 10. Với greybox thì không sao;
> đến lúc thay model thật thì đó là ngân sách cần nhìn lại.

---

## 4b. Cảnh báo: gói tải lần hai ở 30 fps

Bản tải lại `~/Downloads/Soccer Game Pack t pose` (bản có kèm X Bot) **không** dùng được để
thay bộ animation hiện tại. Đo bằng cách import thử `goalkeeper idle.fbx` của bản đó rồi so
với clip cùng tên đã có:

| | Giây | fps | Khung |
|---|---|---|---|
| Bản đã import (lần tải đầu) | 4.617 | **60** | **277** |
| Bản tải lần hai | 4.633 | **30** | **139** |

Mất đúng một nửa số khung. T35 đòi sai số khung chạm bóng dưới 1 khung ở 60 fps — 30 fps là
không đủ độ phân giải để đo. Chỉ lấy **`X Bot.fbx`** từ bản tải lần hai; animation giữ nguyên
bản đầu.

Nếu sau này tải thêm clip, kiểm lại đúng chỗ này: cột `fps` trong
[mixamo-clip-report.tsv](data/mixamo-clip-report.tsv) phải là 60 cho mọi dòng.

---

## 5. Thiết lập import và lý do — đọc trước khi đổi

Tất cả nằm trong [MixamoModelImport.cs](../Assets/_Project/Editor/Art/MixamoModelImport.cs)
chứ không nằm trong file `.meta`, vì đây là thiết lập quyết định đúng-sai của gameplay và
một cú bấm nhầm trong Inspector sẽ không để lại dấu vết nào trong code review.

| Thiết lập | Giá trị | Lý do |
|---|---|---|
| `animationCompression` | `Off` | T35 đòi sai số khung chạm bóng dưới 1 khung ở 60 fps. Nén keyframe làm mượt đúng đoạn chân vung nhanh nhất — tức là làm sai chính con số sắp đem đi đo. Nén lại được sau khi đã chốt số. |
| `optimizeGameObjects` | `false` | `KickerBoneCueSource` đọc transform `root`/`plantFoot`/`hips` mỗi khung. Bật optimize là Unity xoá hierarchy xương và thủ môn mất sạch tín hiệu đọc vị. |
| `animationType` | `Human` | Điều kiện để đổi sang model cầu thủ thật mà không làm lại Animator. |
| Root Transform **Position XZ** | *không* bake vào pose | Chuyển động ngang rơi vào kênh root motion; tắt Apply Root Motion là clip chạy tại chỗ, vị trí ngang hoàn toàn do gameplay đặt. Đây là cách giữ luật "animation không bao giờ điều khiển vật lý" ngay từ khâu import. |
| Root Transform **Position Y** | *có* bake vào pose | Cú bay người của thủ môn giữ được chiều cao thật của clip, mà root vẫn dính đất. |
| Root Transform **Rotation** | *có* bake vào pose | Thân người xoay theo clip, hướng mặt nhân vật vẫn do gameplay đặt. |
| Tên clip | = tên file | Mixamo đặt tên **mọi** take là `mixamo.com`. Để nguyên thì Animator hiện 22 clip trùng tên. |

Ba dòng cuối (Y và Rotation bake vào pose) là lựa chọn cần xem lại ở T37/T38: nếu đường
bay người của thủ môn phải khớp từng centimet với `KeeperReach.ReachProgress` thì có khả
năng phải bake cả Y ra ngoài và để gameplay dựng luôn quỹ đạo dọc.

---

## 6. Việc tiếp theo

1. T35: đo `ContactNormalizedTime` cho từng clip sút. Bốn clip sút hiện có 30–90 khung ở
   60 fps, đủ độ phân giải cho yêu cầu sai số dưới 1 khung.
2. Kiểm `KeeperDivingSave_A` và `_B` bay về bên nào. Nếu cùng một bên thì dùng cờ **Mirror**
   của clip Humanoid để có bên còn lại, không cần tải thêm.
