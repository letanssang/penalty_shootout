# Hướng dẫn đóng nốt Phase 0

*Viết cho: 2026-08-25 · commit `33e7cb2` · Phase 0 đang ở 24/29 ô*

Tài liệu này chỉ nói về **5 ô còn lại**. Mọi ô khác đã đạt, có bằng chứng, xem
[phase-0-tinh-trang.md](phase-0-tinh-trang.md).

Năm ô chia làm ba nhóm, theo việc **ai** làm được:

| Nhóm | Ô | Ai làm |
|---|---|---|
| A | GC = 0 mỗi khung hình khi HUD bật | **bạn quyết hướng**, tôi làm |
| B | `Detect()` đúng trên ≥2 máy thật | bạn cắm máy thứ hai, tôi đọc kết quả |
| C | 3 ô lặt vặt cần Editor có giao diện | bạn bấm, tôi hướng dẫn từng bước |

---

# Nhóm A — Ô hỏng thật: GC không bằng 0

## Chuyện gì đang xảy ra

Hợp đồng T04 có ô: *"Cấp phát GC bằng 0 mỗi khung hình khi HUD đang bật"*.

Đo trên Pixel 7: **240 trên 240 khung đều cấp phát**, tổng 752 368 byte. Không có
khung nào bằng 0. Ô này hỏng, không phải nghi ngờ.

Thủ phạm **không phải** bộ lấy mẫu — chỗ đó tôi đã kiểm, nó sạch: nhiệt độ cache ở
2 Hz, mảng cấp sẵn, `ProfilerRecorder` là struct. Thủ phạm là **chính IMGUI**. Hàm
`OnGUI` của Unity cấp phát mỗi khung theo thiết kế — dựng `GUIContent`, gọi
`CalcSize`, sinh chuỗi bố cục — bất kể ta có dựng lại chuỗi hiển thị hay không.

Nói cách khác: **không có cách nào đạt 0 tuyệt đối khi còn dùng IMGUI.** Đây là giới
hạn của công cụ, không phải lỗi cẩu thả trong mã.

## Ba lựa chọn

### Lựa chọn 1 — Sửa cho đúng

Bỏ IMGUI, chuyển sang **TextMeshPro** với `SetCharArray(char[])` và bộ đệm ký tự cấp
sẵn. Đây là đường duy nhất đạt 0 thật.

- **Được**: ô nghiệm thu đạt thật, không phải nới lỏng gì.
- **Mất**: kéo thêm gói **UGUI + TextMeshPro** vào dự án. Việc này **vượt ra ngoài hợp
  đồng T04 đã đóng băng** — hợp đồng chỉ cho sửa file trong `Code/Presentation/Diagnostics`.
  Thêm gói là đổi `Packages/manifest.json`, tức đổi nền móng của dự án.
- **Công**: ước lượng 2–3h. Phải viết lại toàn bộ `PerfHud.Renderer`, dựng canvas,
  font asset, và kiểm lại safe area trên máy thật.
- **Rủi ro**: TextMeshPro kéo theo phụ thuộc mới cho **mọi** task sau này của Phase 1–6.

### Lựa chọn 2 — Nới hợp đồng *(tôi nghiêng về cái này)*

Đổi ô thành: *"Cấp phát GC dưới 2 KB mỗi khung hình khi HUD đang bật, và bằng 0 khi
HUD tắt."*

- **Được**: không thêm gói, không đổi nền móng, làm trong 15 phút.
- **Lý do hợp lý**: HUD là **công cụ chẩn đoán**, `PerfHud.Visible` mặc định **false**.
  Người chơi không bao giờ thấy nó, nên rác nó thải ra không chạm vào ai. Ràng buộc
  thật sự đáng giữ là "HUD tắt thì không tốn gì" — cái đó vẫn đạt.
- **Mất**: ô nghiệm thu không còn nói đúng chữ "bằng 0". Phải sửa
  `docs/backlog/phase-0-nen-tang.md` và ghi rõ lý do nới.
- **Cảnh báo thật thà**: đây là **hạ chuẩn**. Nếu sau này bạn muốn HUD bật thường trực
  trong bản chơi thử của người dùng, con số 1.5–2 KB/khung sẽ đẻ ra một đợt thu gom rác
  mỗi vài giây — đúng thứ gây khựng hình mà cả dự án đang cố tránh.

### Lựa chọn 3 — Để nợ

Ghi vào backlog thành một task riêng của Phase 6, đi tiếp Phase 1 ngay.

- **Được**: không mất thời gian nào lúc này. Phase 1 (vật lý bóng) không đụng tới HUD.
- **Mất**: Phase 0 đóng lại với **một ô đỏ**. Về sau HUD chính là công cụ bạn dùng để
  đo mọi task hiệu năng — đo bằng một cái cân tự nó nặng thì số đo lệch.
- **Cụ thể lệch bao nhiêu**: ~1.5 KB/khung, đủ gây một lần GC nhỏ mỗi ~10 giây. Khi
  bạn đo p95 của một pha sút, con số p95 đó có thể dính ngay cái GC do HUD gây ra.

## Bạn cần trả lời gì

Nhắn đúng một trong ba câu:

- *"Chọn 1 — thêm TextMeshPro, sửa cho đúng."*
- *"Chọn 2 — nới hợp đồng xuống dưới 2 KB."*
- *"Chọn 3 — để nợ, đi tiếp Phase 1."*

Xong tôi làm ngay, không hỏi lại.

---

# Nhóm B — Đo trên máy thứ hai

Mục tiêu: đóng ô *"`Detect()` trả A/B/C đúng trên ≥2 máy thật"*. Hiện mới có Pixel 7
(ra bậc A). Cần **một máy yếu hơn** để thấy bậc B hoặc C.

## Máy nào dùng được

Bất kỳ điện thoại Android nào **khác Pixel 7**. Càng cũ càng tốt, vì ta đang cần chứng
minh nó **không** ra bậc A:

| Máy | Dự kiến ra bậc |
|---|---|
| Điện thoại Android 3–4 GB RAM, 4–6 lõi | B |
| Điện thoại Android ≤ 3 GB RAM | C |
| Máy Android đời 2020 trở lên, ≥ 6 GB RAM | A (không giúp gì — trùng Pixel 7) |

Mượn của người nhà cũng được, không cần cài gì vĩnh viễn, gỡ sạch trong 1 phút.

## Bước 1 — Mở chế độ nhà phát triển trên máy đó

Trên **chính điện thoại** (không phải trên Mac):

1. Vào **Cài đặt → Giới thiệu điện thoại**.
2. Tìm dòng **Số hiệu bản dựng** (Build number). Bấm liên tiếp **7 lần**.
3. Máy hiện *"Bạn đã là nhà phát triển"* và hỏi mã mở khoá — nhập mã màn hình.
4. Quay ra **Cài đặt → Hệ thống → Tuỳ chọn nhà phát triển**.
5. Bật **Gỡ lỗi qua USB** (USB debugging).

> Vài hãng giấu chỗ khác: Xiaomi để "Số hiệu bản dựng" trong *Giới thiệu → Toàn bộ
> thông số*; Samsung để trong *Giới thiệu điện thoại → Thông tin phần mềm*.

## Bước 2 — Cắm vào Mac

Cắm cáp USB. Trên điện thoại sẽ hiện hộp thoại **"Cho phép gỡ lỗi USB?"** — tích
*Luôn cho phép từ máy tính này* rồi bấm **Cho phép**.

Kiểm tra máy đã nhận chưa:

```bash
/Applications/Unity/Hub/Editor/6000.3.22f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb devices
```

Phải in ra một dòng có chữ `device` ở cuối. Nếu nó in `unauthorized` thì hộp thoại
trên điện thoại chưa được bấm Cho phép. Nếu không có dòng nào, đổi cáp — rất nhiều
cáp chỉ có dây sạc, không có dây dữ liệu.

## Bước 3 — Cài bản test

Bản test đã dựng sẵn, **không cần build lại**. Nó tự chạy hết bài đo khi khởi động và
tự bật HUD.

```bash
/Applications/Unity/Hub/Editor/6000.3.22f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb install -r /Users/tansangle/Projects/penalty_shootout/android_build/ElevenMetres-Tests.apk
```

Chờ khoảng 30 giây, phải in ra `Success`.

## Bước 4 — Chạy và ghi kết quả

Dán nguyên khối này. Nó ghi nhật ký ra file **trước**, rồi mới khởi động app — thứ tự
này quan trọng, lần đầu tôi làm ngược và bị mất sạch chứng cứ.

```bash
ADB=/Applications/Unity/Hub/Editor/6000.3.22f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb && $ADB logcat -G 16M && $ADB logcat -c && ($ADB logcat -v time > may2.log &) && sleep 2 && $ADB shell monkey -p com.UnityTestRunner.UnityTestRunner -c android.intent.category.LAUNCHER 1
```

Bây giờ **nhìn vào màn hình điện thoại**. Bài đo chạy khoảng **50 giây**. Có một đoạn
8 giây HUD hiện lên góc trên bên trái — chụp màn hình lúc đó thì càng tốt.

Chờ cho app chạy xong (màn hình test runner đứng yên), rồi đọc kết quả:

```bash
cd /Users/tansangle/Projects/penalty_shootout && grep -a "THIET BI" may2.log
```

## Bước 5 — Đọc kết quả thế nào là đạt

Bạn sẽ thấy khoảng 8–10 dòng. Dòng quan trọng nhất là dòng đầu:

```
[T03 THIET BI] tier=? model=... RAM=...MB VRAM=...MB cores=... gfx=Vulkan
```

**Ô này đạt khi `tier=` khớp với bảng ở Bước "Máy nào dùng được"** — tức là máy yếu
phải ra `B` hoặc `C`, không phải `A`.

| Kết quả | Nghĩa là |
|---|---|
| `tier=B` hoặc `tier=C` trên máy yếu | ✅ **Ô đóng.** Gửi tôi cả khối `grep` ở trên |
| `tier=A` trên máy yếu | ❌ Luật chấm điểm sai. Gửi tôi dòng đó — có đủ RAM/lõi để tôi sửa ngưỡng |
| Không có dòng `T03` nào | App chưa chạy xong, hoặc chưa khởi động. Làm lại Bước 4 |

Các dòng còn lại (`T04`, `T07`) là số đo hiệu năng và hash trên máy đó — cứ gửi hết,
càng nhiều máy càng chắc phần tất định.

## Bước 6 — Gỡ sạch khỏi máy mượn

```bash
/Applications/Unity/Hub/Editor/6000.3.22f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb uninstall com.UnityTestRunner.UnityTestRunner
```

Rồi tắt lại **Gỡ lỗi qua USB** trong Tuỳ chọn nhà phát triển.

---

# Nhóm C — Ba ô cần Editor có giao diện

Ba ô này `-batchmode` không kiểm được, vì chúng đòi nhìn vào cửa sổ Editor.

## Mở dự án

Mở **Unity Hub → Add → chọn thư mục** `/Users/tansangle/Projects/penalty_shootout`
→ bấm vào dòng dự án để mở. Lần mở đầu mất 2–5 phút.

## C1 — T02: sửa một dòng chỉ biên dịch lại đúng một assembly

Ô này chứng minh việc chia assembly có tác dụng thật — sửa một chỗ không bắt cả dự án
biên dịch lại.

> **Lưu ý về câu chữ của ô nghiệm thu.** Hợp đồng T02 viết là *"sửa một dòng trong `UI`"*,
> nhưng ở Phase 0 `Eleven.UI` **mới chỉ có file asmdef, chưa có dòng mã nào** — cũng như
> `Keeper`, `Shooter`, `Match`. Đó là đúng kế hoạch: chúng là chỗ trống dành sẵn cho
> Phase 2–4. Hiện chỉ `Ball` (3 file) và `Presentation` (8 file) có mã.
>
> Nên kiểm bằng `Presentation` thay cho `UI`. Phép kiểm vẫn đúng bản chất — thậm chí
> **chặt hơn**, vì `Presentation` là assembly to nhất và nằm ở tầng trên cùng: nếu sửa
> nó mà `Ball` không bị biên dịch lại, thì chiều phụ thuộc đang đúng.

1. Trong Editor, mở cửa sổ **Console** (menu **Window → General → Console**).
2. Vào **Unity → Settings → Diagnostics**, bật **Enable Compilation Logging**.
   Không có bước này thì Console không in tên assembly, và bạn chỉ đoán được qua cảm giác nhanh chậm.
3. Trong **Project**, mở `Assets/_Project/Code/Presentation/Diagnostics/PerfHud.cs`.
4. Thêm một dòng trống ở cuối file, **lưu** (Cmd+S).
5. Quay lại Unity, chờ vòng xoay biên dịch ở góc phải dưới chạy xong.

**Đạt khi** Console in ra dòng biên dịch **chỉ có `Eleven.Presentation`** (và các assembly
test phụ thuộc nó), **không có `Eleven.Ball`**.

Cảm giác bằng mắt: vòng xoay chạy khoảng **1–3 giây**. Nếu nó chạy 20–40 giây thì cả dự
án biên dịch lại, ô này hỏng.

Nhớ **tắt lại Enable Compilation Logging** sau khi xong, không thì Console ngập log.

Thử thêm chiều ngược lại cho chắc: sửa một dòng trong
`Assets/_Project/Code/Ball/BallSolver.cs`. Lần này `Eleven.Presentation` **phải** biên
dịch lại theo, vì nó phụ thuộc `Ball`. Thấy đúng cả hai chiều thì mới là chia assembly
đúng, chứ không phải may mắn.

## C2 — T03: ép bậc bằng `tier.override`

Ô này chứng minh có thể ép máy chạy ở bậc B hoặc C để thử, không phụ thuộc phần cứng.

`tier.override` là một `PlayerPrefs` kiểu số nguyên: **0 = A, 1 = B, 2 = C**.

Trong Editor, PlayerPrefs nằm trong một file của macOS, nên đặt được bằng lệnh —
**đóng Unity trước khi chạy lệnh này**, nếu không Unity sẽ ghi đè lại:

```bash
defaults write unity.DefaultCompany.penalty_shootout tier.override -int 2
```

Rồi mở Unity lại, mở scene `Assets/_Project/Scenes/Boot.unity`, bấm **Play**.

**Đạt khi** Console in:

```
[TierBootstrap] Bậc phát hiện được: C (model ..., RAM ...MB, ... lõi)
```

Chữ **C** ở đây là điểm mấu chốt — máy Mac của bạn thừa sức chạy bậc A, nên nếu nó
vẫn in `A` thì `tier.override` không có tác dụng và ô này hỏng.

Thử lại với `-int 1` (phải ra `B`) rồi trả về mặc định:

```bash
defaults delete unity.DefaultCompany.penalty_shootout tier.override
```

## C3 — T03: đổi bậc lúc chạy không crash, không rò render texture

Ô này khó nhất, và thật lòng mà nói: **nó chỉ đáng làm khi bạn thật sự định cho phép
đổi bậc giữa trận**. Nếu bậc chỉ chọn một lần lúc khởi động — mà hiện tại đúng là như
vậy, `TierBootstrap.Awake()` gọi một lần rồi thôi — thì ô này đang kiểm một tình huống
chưa tồn tại.

Nếu vẫn muốn kiểm:

1. Mở **Window → Analysis → Profiler**.
2. Trong Profiler, chọn mô-đun **Memory**, đổi sang chế độ **Detailed**, bấm
   **Take Sample**.
3. Bấm Play, ghi lại con số **Render Textures** trong bảng.
4. Đổi bậc (bằng cách gọi `DeviceTier.RefreshOverride()` — cái này cần một nút bấm,
   tức là **cần tôi viết thêm mã**).
5. Take Sample lần nữa, so con số **Render Textures**.

**Đạt khi** con số không tăng dần sau mỗi lần đổi.

> Bước 4 cần mã mới, mà hợp đồng T03 không cho phép thêm file. **Đề nghị của tôi**:
> để ô này lại cho Phase 5 (Trình diễn), nơi việc đổi bậc lúc chạy mới thật sự có
> mặt. Nhắn *"để C3 sang Phase 5"* là tôi ghi vào backlog.

---

# Một lỗ hổng tôi tìm thấy khi viết tài liệu này

Trong **bản game thật** (`ElevenMetres.apk`), **không có cách nào bật HUD lên.**

`PerfHud.Visible` mặc định là `false`, và **không dòng mã nào trong dự án đặt nó thành
`true`** — trừ mấy file test. Không có phím tắt, không có cử chỉ chạm, không có nút.

Nghĩa là: HUD *vẽ được* trên máy thật (đã chứng minh, đã chụp màn hình), nhưng bạn cầm
bản game trong tay thì **không bao giờ thấy nó**. Ảnh chụp HUD hôm trước là chụp trong
**bản test**, nơi mã test tự bật.

Ô nghiệm thu T04 viết là *"HUD hiện được trên build thiết bị thật"* — hiểu theo nghĩa
đen thì vẫn đạt. Nhưng về công dụng thì nó vô dụng: HUD sinh ra để bạn cầm máy đo hiệu
năng, mà lại không bật được.

**Cách sửa rẻ nhất**: chạm 4 ngón tay vào màn hình để bật/tắt. Khoảng 10 dòng trong
`PerfHud.cs`, nằm gọn trong phạm vi file mà hợp đồng T04 cho phép sửa.

Nhắn *"thêm cử chỉ bật HUD"* là tôi làm. Tôi **không tự làm** vì nó thêm hành vi mới
vào một hợp đồng đã đóng băng, và đó là quyết định của bạn chứ không phải của tôi.

---

# Tóm tắt: bạn cần nhắn gì

| Việc | Câu trả lời cần có |
|---|---|
| Ô GC | *"Chọn 1"* / *"Chọn 2"* / *"Chọn 3"* |
| Máy thứ hai | dán kết quả `grep -a "THIET BI" may2.log` |
| C1, C2 | nói đạt hay không đạt |
| C3 | *"để C3 sang Phase 5"* hoặc *"làm luôn"* |
| Bật HUD | *"thêm cử chỉ bật HUD"* hoặc *"để đó"* |

Trả lời được ô GC thôi là tôi đã đi tiếp được. Bốn việc còn lại làm dần cũng không sao.
