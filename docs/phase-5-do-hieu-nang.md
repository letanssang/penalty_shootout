# Phase 5 — Báo cáo đo hiệu năng

Tài liệu này là **chỗ dán số đo**, không phải chỗ dán cảm nhận
([quy tắc 7](backlog/README.md)). Mọi ô còn trống trong
[phase-5-trinh-dien.md](backlog/phase-5-trinh-dien.md) đều dẫn về đây.

Ba task T29 (cỏ), T30 (khán giả), T31 (da) đã xong phần mã và xanh trong EditMode.
Cái EditMode **không** làm được là chạy trên một GPU thật. Tám ô dưới đây cần một
người cầm máy — không ô nào tự đóng được, và không ô nào được tick nếu ô "tên máy"
còn trống.

**Quy ước ghi số:**

- Luôn ghi tên máy đầy đủ (`iPhone 12`, `Galaxy A54`), không ghi "máy bậc B".
- Luôn ghi bản build (`IL2CPP Release, Metal, ARM64`), không đo trên Development Build
  trừ khi ô đó nói rõ là được — Development Build tự nó chậm hơn.
- Đo GPU thì lấy **trung vị của 300 khung hình liên tiếp**, không lấy khung hình đầu
  và không lấy đỉnh.
- Máy phải nguội. Chạy game 5 phút rồi đo lại: nếu số tăng quá 20% thì máy đang bị
  thermal throttle, ghi cả hai số.

---

## Bảng tổng — tám ô cần người kiểm

| # | Task | Ô nghiệm thu | Trần đã chốt trong mã | Số đo | Máy | Ngày |
|---|------|--------------|----------------------|-------|-----|------|
| 1 | T29 | Overdraw trung bình của cỏ | `GrassBudget.MaxAverageOverdraw` = 2.5 | | | |
| 2 | T29 | GPU cỏ ở bậc A | `GrassBudget.MaxTierAGpuMs` = 2.0 ms | | | |
| 3 | T29 | Bảng 8 biến thể (clip × bóng × gió) | — | *(bảng riêng bên dưới)* | | |
| 4 | T29 | Chênh lệch frame time khi bậc C tắt cỏ | — | | | |
| 5 | T30 | GPU khán giả | `CrowdBudget.MaxGpuBudgetMs` = 0.8 ms | | | |
| 6 | T31 | GPU da, **hai** nhân vật | `SkinBudget.MaxGpuBudgetMs` = 0.5 ms | | | |
| 7 | T31 | Ảnh so sánh bật/tắt SSS | — | *(hai đường dẫn ảnh)* | | |
| 8 | T31 | Delay màn hình đầu vì biên dịch shader | `SkinBudget.MaxFirstScreenCompileMs` = 1000 ms | | | |

> Vượt trần thì **ghi số vào bảng và báo lại**, đừng tự hạ chất lượng. Mã đã dựng theo
> hướng đó: `GrassMeasurementTable`, `SkinBudgetCheck.Evaluate` và
> `CrowdBudget` đều trả về kết luận `VuotNganSach_PhaiBaoCao` chứ không có đường nào
> chạm vào cấu hình. Cắt cái gì là quyết định của người, không phải của hàm.

---

## Bảng 8 dòng của T29 — ô số 3

Ba công tắc, tám tổ hợp. Khung bảng đã được test khoá cứng
(`TamBienThe_DuTamToHop_KhongTrungNhau`): đúng 8 dòng, mỗi công tắc bật ở đúng 4 dòng.
Thiếu một dòng, hoặc một dòng thiếu tên máy, thì **không kết luận được** —
`BangDo_ThieuDongThiKhongKetLuan_DuMoiDongDaDoDeuDat` và
`DongDo_ThieuTenMay_KhongTinhLaDaDo` giữ chỗ đó.

| # | Alpha clip | Đổ bóng | Gió | GPU (ms) | Overdraw TB | Máy | Ghi chú |
|---|-----------|---------|-----|----------|-------------|-----|---------|
| 1 | tắt | tắt | tắt | | | | nền so sánh |
| 2 | **bật** | tắt | tắt | | | | |
| 3 | tắt | **bật** | tắt | | | | |
| 4 | tắt | tắt | **bật** | | | | |
| 5 | **bật** | **bật** | tắt | | | | |
| 6 | **bật** | tắt | **bật** | | | | |
| 7 | tắt | **bật** | **bật** | | | | |
| 8 | **bật** | **bật** | **bật** | | | | cấu hình bậc A |

Đọc bảng này theo **hiệu số**, không theo số tuyệt đối: dòng 2 trừ dòng 1 là giá của
alpha clip, dòng 3 trừ dòng 1 là giá của đổ bóng, v.v. Nếu dòng 8 vượt 2.0 ms thì hiệu
số cho biết nên cắt cái nào trước.

---

## A. Đo thời gian GPU trên iPhone bằng Xcode

Cần: máy Mac có Xcode, iPhone cắm cáp, tài khoản Apple Developer (tài khoản miễn phí đủ dùng).

1. Unity → **File ▸ Build Settings** → chọn **iOS** → **Build** (đừng bấm Build And Run).
   Trong **Player Settings ▸ Other Settings**: Scripting Backend = **IL2CPP**,
   Architecture = **ARM64**, Graphics API = **Metal**.
2. Mở `Unity-iPhone.xcodeproj` vừa sinh ra. Chọn scheme **Unity-iPhone**, thiết bị là
   iPhone thật.
3. **Product ▸ Scheme ▸ Edit Scheme ▸ Run ▸ Info**: Build Configuration = **Release**.
   Sang tab **Options**: **GPU Frame Capture** = **Metal**.
4. **Product ▸ Run**. Đợi vào đúng cảnh cần đo (cỏ trong khung hình, hai nhân vật trên
   sân, khán đài hiện đầy).
5. Trên thanh debug dưới cùng, bấm nút **camera** (Capture GPU Frame). Xcode chụp một
   khung hình rồi mở trình xem.
6. Bên trái chọn **Performance ▸ Counters**. Cột **Time** là mili-giây GPU của từng
   draw call.
7. Tìm draw call của thứ cần đo — lọc theo tên shader ở cột **Pipeline State**:
   `Eleven/Grass`, `Eleven/CrowdImpostor`, `Eleven/Skin`. Cộng tất cả draw call có
   cùng shader đó lại. **Đó là số điền vào bảng.**
8. Với T31 nhớ đảm bảo đúng **hai** nhân vật da trong khung hình. Nếu chỉ có một,
   ghi `characterCount = 1` — `SkinGpuMeasurement.NormalizedToTwoCharactersMs` sẽ nhân đôi
   trước khi so với trần.

Đo **overdraw** (ô số 1) trên iPhone: trong cùng trình xem GPU frame, chọn
**Performance ▸ Pipeline Statistics**, đọc **Fragment Shader Invocations**, chia cho số
điểm ảnh vùng cỏ chiếm trên màn hình. Cách dễ hơn: xem mục C bên dưới.

## B. Đo thời gian GPU trên Android bằng Android GPU Inspector

Cần: máy Android hỗ trợ AGI (Pixel, hoặc Galaxy dùng Snapdragon), AGI cài trên máy tính,
USB debugging đã bật.

1. Unity → **Build Settings ▸ Android**, **Player Settings ▸ Other Settings**:
   Graphics API = **Vulkan**, Scripting Backend = **IL2CPP**, Target Architecture = **ARM64**.
2. Trong **Publishing Settings**, bật **Custom Main Manifest** rồi thêm
   `android:debuggable="true"` vào thẻ `<application>`. AGI **bắt buộc** phải có cờ này,
   nhưng vẫn build **Release** — cờ này không làm chậm GPU.
3. Build ra `.apk`, cài bằng `adb install -r <file>.apk`.
4. Mở AGI → **Capture a new trace** → chọn thiết bị, chọn ứng dụng, kiểu trace là
   **System profile**.
5. Đặt **Duration** khoảng 5 giây, bấm **Start**. Trong lúc đó thao tác trên máy để vào
   đúng cảnh cần đo.
6. Trace mở ra: hàng **GPU Queue** cho thời gian GPU mỗi khung hình; hàng **Vulkan Events**
   tách theo render pass.
7. Muốn xem từng draw call thì chụp kiểu **Frame profile** thay vì System profile, rồi
   dùng bảng **Render Passes** để lọc theo pipeline.
8. Lấy trung vị 300 khung hình như quy ước trên, không lấy khung hình đầu tiên.

## C. Đo overdraw của cỏ bằng debug view của URP — ô số 1

Cái này làm được ngay trong Editor và trên Development Build, không cần Xcode/AGI.

1. Chạy game (Editor hoặc Development Build có **Frame Debugger** bật).
2. **Window ▸ Analysis ▸ Rendering Debugger** (URP 17: cùng chỗ, tên panel là
   *Rendering Debugger*).
3. Sang tab **Rendering** → mục **Overdraw** → **Overdraw Mode** = **Overdraw**.
   Màn hình chuyển thành thang nhiệt: càng đỏ càng nhiều lớp chồng lên nhau.
4. Đặt camera vào đúng góc quay penalty của T26 — đây là góc duy nhất người chơi thật
   sự nhìn thấy, đo góc khác là đo thứ không ai xem.
5. Trong cùng panel, **Max Overdraw Count** đặt = 5 để thang màu không bão hoà.
6. Chụp màn hình, lưu vào `docs/anh/` và dán đường dẫn vào bảng.
7. Bật/tắt cỏ bằng cờ `GrassField.IsEnabled` rồi chụp lại: hai ảnh cạnh nhau cho thấy
   phần overdraw nào là của cỏ, phần nào là của mặt sân.
8. Con số điền vào bảng là **overdraw trung bình vùng cỏ chiếm**, không phải overdraw
   toàn màn hình. Trần là 2.5.

## D. Chụp ảnh so sánh bật/tắt SSS — ô số 7

Ô này dễ tick khống nhất trong cả phase, nên `SkinSideBySideComparison` chặn sẵn: hai ảnh
ở hai góc camera khác nhau, hai cấu hình đèn khác nhau, hoặc trỏ cùng một file, đều
không tính.

1. Dựng một cảnh tĩnh: camera **không** chạy T26 đạo diễn, đặt cứng một transform và
   ghi lại toạ độ.
2. Khoá ánh sáng: một directional light, không đổi góc, không đổi cường độ, tắt mọi
   hiệu ứng ngẫu nhiên.
3. Tắt hậu kỳ sai lệch màu của T32 (nó chỉ bật lúc chạm bóng, nhưng nếu lỡ bật thì hai
   ảnh khác nhau vì lý do không liên quan).
4. Chạy build ở bậc A. Chụp ảnh thứ nhất — SSS bật.
5. **Không** di chuyển camera, **không** đổi đèn. Đổi `TierProfile.subsurfaceScattering`
   sang false, chụp ảnh thứ hai.
6. Lưu hai ảnh với tên nói rõ, ví dụ `sss-on-ip12-goc-thumon.png` và
   `sss-off-ip12-goc-thumon.png`.
7. Điền vào `SkinSideBySideComparison`: hai đường dẫn, `cameraSetup` (toạ độ hoặc shot id
   của T26), `lightingSetup`, `deviceName`. Thiếu bất kỳ trường nào thì `IsRecorded` = false.
8. Nhìn vào đâu để biết SSS có ăn hay không: **rìa bóng trên mặt** — chỗ chuyển từ sáng
   sang tối phải ửng đỏ, không phải xám. Đó là toàn bộ hiệu ứng. Nếu hai ảnh trông y
   hệt nhau thì keyword chưa bật, không phải hiệu ứng yếu.

## E. Đo delay màn hình đầu do biên dịch shader — ô số 8

**Phải đo trên build thật, ở lần chạy đầu tiên sau khi cài.** Trong Editor shader đã nằm
sẵn trong cache nên số đo luôn đẹp và luôn vô nghĩa —
`SoDoBienDich_PhaiLaLanChayDauTien_CacheConTrong` từ chối số đo không đánh dấu `coldStart`.

1. Gỡ hẳn ứng dụng khỏi máy (`adb uninstall <package>`, hoặc xoá app trên iOS). Gỡ, không
   phải cài đè — cache shader nằm trong thư mục dữ liệu ứng dụng.
2. Cài lại bản build Release.
3. Bật ghi log: `adb logcat -s Unity` (Android) hoặc **Window ▸ Devices and Simulators ▸
   Open Console** trong Xcode (iOS).
4. Mở app, bấm đồng hồ từ lúc màn hình khởi động biến mất đến lúc khung hình đầu tiên có
   nhân vật hiện ra.
5. Chính xác hơn: trong mã, `Time.realtimeSinceStartup` tại `Awake` của cảnh trận đấu và
   tại `OnPostRender` khung hình đầu tiên — hiệu số là con số cần.
6. Đếm số biến thể thật sự biên dịch: **Edit ▸ Project Settings ▸ Graphics**, mục
   **Shader Loading**, bật **Log Shader Compilation**. Log sẽ in mỗi biến thể được biên dịch.
7. Điền `SkinCompileMeasurement`: `firstScreenCompileMs`, `compiledVariantCount`,
   `usedWarmup`, `coldStart = true`, `deviceName`.
8. Nếu vượt 1000 ms: cách sửa **không** phải là cắt tính năng, mà là dựng một
   `ShaderVariantCollection` cho 3 biến thể bắt buộc (`SkinVariantManifest.Required()`) và
   gọi `WarmUp()` ở màn hình loading. Ghi lại `usedWarmup = true` khi làm vậy.

---

## F. Kiểm biến thể trên build thật — ô nghiệm thu T31

EditMode chỉ chứng minh được rằng ba keyword khai bằng `multi_compile` chứ không phải
`shader_feature`, và rằng pass dựng hình có đúng 192 biến thể. Cái EditMode **không** thấy
là bộ lọc biến thể lúc build — nó cắt `_CLUSTER_LIGHT_LOOP` nếu URP Asset trong build không
bật Forward+, và thứ bị cắt hiện ra là nhân vật màu hồng, hoặc tệ hơn, nhân vật trông bình
thường nhưng chạy nhánh sai.

1. Build Release cho từng bậc thiết bị (A, B, C).
2. Ở mỗi bậc, vào cảnh có nhân vật da, chụp GPU frame (mục A hoặc B).
3. Trong trình xem, mở pipeline state của draw call nhân vật, đọc danh sách keyword đang bật.
4. Đối chiếu với `SkinVariantManifest.ForTier(bậc)`:
   bậc A = `sss+ xuyên+ cluster+`, bậc B = `sss+ xuyên- cluster+`, bậc C = `sss- xuyên- cluster+`.
5. Ghi vào `SkinVariantAudit`: `buildTarget`, `deviceName`, và `RecordSurvivor` cho mỗi biến
   thể thấy được. `AllRequiredSurvived()` phải trả true thì ô mới đóng.
6. Nhân vật màu hồng = biến thể bị strip. Nhân vật bình thường nhưng **không có đèn phụ nào
   ăn** = `_CLUSTER_LIGHT_LOOP` bị cắt, đây là ca khó thấy nhất.

---

## Ghi chú lịch sử

- **2026-08-27** — Dựng tài liệu. Cả tám ô còn trống; phần mã của T29, T30, T31 đã xanh
  trong lượt EditMode 509 test / 508 xanh / 0 đỏ / 1 skip (92.5 s), ba shader
  (`Eleven/Grass`, `Eleven/CrowdImpostor`, `Eleven/Skin`) đều biên dịch sạch, 0 cảnh báo.
