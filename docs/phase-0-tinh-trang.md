# Phase 0 — Tình trạng thực tế

*Cập nhật: 2026-08-25 · commit `c4c9acb`*

## Trả lời ngắn

**Phase 0 chưa xong.** Mã của T01–T05 đã viết đủ và **biên dịch sạch (0 lỗi, 0 cảnh
báo)** đối chiếu với DLL thật của Unity 6000.3.22f1. Nhưng phần lớn ô nghiệm thu của
Phase 0 đòi **chạy Unity và chạy trên máy thật**, mà Unity hiện **chưa kích hoạt bản
quyền** — không có bản quyền thì `-batchmode` thoát ngay với mã 198 và không làm được gì.

Nói cách khác: mã đã đúng về mặt API và cú pháp; **chưa có bằng chứng chạy**.

---

## Một việc duy nhất bạn cần làm để mở khoá

Mở **Unity Hub → đăng nhập Unity ID → chọn giấy phép Personal**. Đây là bước duy nhất
tôi không làm thay được, vì nó phải nhập tài khoản và mật khẩu của bạn.

Kiểm tra xong chưa bằng lệnh này (in ra danh sách giấy phép, không được rỗng):

```bash
"/Applications/Unity Hub.app/Contents/MacOS/Unity Hub" -- --headless license --list
```

Mọi thứ còn lại đã sẵn sàng:

| Hạng mục | Tình trạng |
|---|---|
| Unity 6000.3.22f1 (Apple silicon) | đã cài |
| iOS Build Support | đã cài (`PlaybackEngines/iOSSupport`) |
| Android Build Support + SDK + NDK + OpenJDK | đã cài (`PlaybackEngines/AndroidPlayer`) |
| Giấy phép | **chưa có — đây là nút thắt** |

---

## Đã kiểm được gì khi chưa có bản quyền

Không mở được Unity thì tôi kiểm bằng chính bộ công cụ Unity mang theo:

1. **Biên dịch ngoại tuyến toàn bộ 18 file `.cs`** bằng trình biên dịch Roslyn/Mono kèm
   theo Unity, tham chiếu DLL thật:
   - `UnityEngine.*Module.dll` + `UnityEditor.*Module.dll` của 6000.3.22f1
   - URP 17.3.0 (`Unity.RenderPipelines.Universal.Runtime/Editor.dll`)
   - `Unity.Mathematics.dll`, `Unity.Burst.dll`, `nunit.framework.dll`, TestRunner
   - Kết quả: **0 lỗi, 0 cảnh báo.**
2. **Chạy thật 17 test EditMode của T07** bằng một trình chạy NUnit tối giản dưới Mono:
   **16/17 PASS**. Test còn lại (`GoldenHash_...`) chỉ hỏng vì nó gọi
   `UnityEngine.Debug.Log`, thứ không chạy được ngoài player — logic bên trong nó tôi đã
   chạy riêng và đạt: hash lặp lại giống từng bit, khác 0.
3. **Golden hash quỹ đạo = `4094678572`** (Mono desktop, ARM64 macOS). Con số này **chưa
   phải bằng chứng tất định** — còn phải đối chiếu với Editor và với build IL2CPP trên
   thiết bị. Nếu ba giá trị khớp nhau thì T07 mới thật sự đạt.

---

## Bảng nghiệm thu chi tiết

Ký hiệu: **✅ đã kiểm bằng bằng chứng** · **⏸ chờ mở Unity** · **📱 chờ máy thật**

### T01 — Khởi tạo repo và Git LFS

| Ô nghiệm thu | TT | Bằng chứng / còn thiếu |
|---|---|---|
| `.gitignore` loại trừ `Library/ Temp/ Obj/ Build/ Logs/ UserSettings/ *.csproj *.sln` | ✅ | có đủ trong `.gitignore` |
| LFS bật cho `*.psd *.fbx *.png *.tga *.wav *.mp4 *.exr` | ✅ | `git lfs track` in đủ 7 mẫu (+ `jpg/jpeg/hdr/tif`) |
| `git lfs track` in ra đúng danh sách | ✅ | đã chạy |
| Commit thử `.png`, `git lfs ls-files` liệt kê được | ✅ | `Assets/_Project/Art/lfs-test.png` |
| `git status` sạch sau khi mở Unity lần đầu rồi đóng | ⏸ | Unity chưa mở được lần nào |

### T02 — Cây thư mục và assembly definition

| Ô nghiệm thu | TT | Bằng chứng / còn thiếu |
|---|---|---|
| 6 asmdef runtime + 2 asmdef test | ✅ | Ball, Keeper, Shooter, Match, Presentation, UI + Tests.EditMode/PlayMode |
| `Ball` không tham chiếu asmdef nào khác | ✅ | refs của `Eleven.Ball` chỉ có Mathematics/Collections/Burst |
| `Ball` và `Match` không tham chiếu `Presentation`/`UI` | ✅ | đã đọc từng file asmdef |
| Cả 6 bật `Unity.Mathematics`, `Unity.Collections`, `Unity.Burst` | ✅ | đã đọc từng file asmdef |
| Sửa một dòng trong `UI` chỉ biên dịch lại `UI` | ⏸ | phải xem timestamp Console trong Editor |
| Test runner nhận ra cả hai asmdef test | ⏸ | phải mở Test Runner |

### T03 — Hệ thống bậc chất lượng A/B/C

| Ô nghiệm thu | TT | Bằng chứng / còn thiếu |
|---|---|---|
| 3 URP asset + 3 `TierProfile` tồn tại, khớp bảng plan | ⏸ | **asset chưa được sinh ra lần nào** — cần chạy menu `Eleven ▸ Phase 0 ▸ Generate Tier Assets` |
| `Detect()` trả A/B/C đúng trên ≥2 máy thật | 📱 | chưa có máy nào |
| `PlayerPrefs("tier.override")` ép được bậc | ⏸ | mã có, chưa chạy |
| Đổi bậc lúc chạy không crash, không rò render texture | ⏸ | phải kiểm bằng Profiler |
| `OnTierChanged` bắn đúng một lần mỗi lần đổi | ✅ | **đã sửa lỗi** — `Apply()` trước đây không đặt `initialized = true` nên bắn lặp mỗi lần gọi |
| Không có `#if UNITY_IOS` trong logic phân bậc | ✅ | không có chỉ thị nào trong `DeviceTier.cs` |

### T04 — HUD đo hiệu năng

| Ô nghiệm thu | TT | Bằng chứng / còn thiếu |
|---|---|---|
| HUD hiện được trên build thiết bị thật | 📱 | **đã sửa lỗi thiết kế**: bản cũ dùng `TextMesh` (mesh trong không gian thế giới) nên gần như chắc chắn không nhìn thấy trên thiết bị; nay chuyển sang IMGUI `OnGUI`, né safe area |
| Hiện cả frame time trung bình và p95 | ⏸ | `PerfHud.HistoryLength == 600`, test EditMode có kiểm hằng số này |
| Đọc nhiệt độ máy (iOS `ProcessInfo.thermalState`, Android `PowerManager`) | 📱 | mã có (`ElevenDiagnostics.mm` + JNI), chưa chạy |
| HUD tốn dưới 0.2 ms | 📱 | chưa đo |
| `EndCapture()` trả CSV ghi ra `persistentDataPath`, lấy về máy được | ⏸ | test EditMode kiểm được dòng tiêu đề CSV, chưa kiểm việc ghi file |
| GC = 0 mỗi khung hình khi HUD bật | 📱 | **chưa đạt đúng nghĩa**: bản hiện tại cấp phát ~200 B, 4 lần/giây khi dựng lại chuỗi. Các khung còn lại bằng 0. Muốn đúng 0 tuyệt đối phải chuyển sang TextMeshPro `SetCharArray` |

### T05 — Script build một lệnh

| Ô nghiệm thu | TT | Bằng chứng / còn thiếu |
|---|---|---|
| `./tools/build.sh ios` / `android` chạy từ terminal | ⏸ | chặn bởi bản quyền; module iOS/Android đã cài xong |
| Build nhúng git commit hash, hiện trong HUD | ⏸ | `GetCommitShortHash()` biên dịch sạch, chưa chạy |
| Android Vulkan, iOS Metal, gỡ GLES3 | ⏸ | mã đặt `SetGraphicsAPIs(...Vulkan)` / `(...Metal)` với `SetUseDefaultGraphicsAPIs(false)` |
| IL2CPP + ARM64 + Release | ⏸ | mã đặt qua `NamedBuildTarget` |
| Exit code khác 0 khi build hỏng | ✅ | **đã sửa lỗi**: dưới `set -e`, nhánh in log lỗi là mã chết; nay dùng `\|\| EXIT_CODE=$?` |

---

## Bảy lỗi đã tìm ra và sửa trong đợt này

Tất cả đều là lỗi **sẽ chặn biên dịch hoặc sai lúc chạy**, không phải góp ý phong cách.

| # | Chỗ | Vấn đề | Cách sửa |
|---|---|---|---|
| 1 | `TierAssetGenerator.ApplyQualityLevels` | `QualitySettings.AddCustomLevel` / `DeleteCustomLevel` **không tồn tại** ở Unity 6 | viết lại bằng `SerializedObject` trên `ProjectSettings/QualitySettings.asset`; ép đúng 3 level và ghim `m_PerPlatformDefaultQuality` vào `[0,2]` vì các chỉ số cũ (5, 2) nay đã ngoài phạm vi |
| 2 | `PerfHud.Sampler` | `FrameTiming.cpuMainThreadPresentTime` / `cpuRenderThreadPresentTime` không tồn tại | dùng `cpuMainThreadFrameTime` / `cpuRenderThreadFrameTime` |
| 3 | `PerfHud.Sampler` | `GetLatestTimings` trả `uint`, gán vào `int` là lỗi biên dịch | đổi kiểu biến |
| 4 | `PerfHud.Renderer` | `TextMesh` là mesh không gian thế giới → HUD gần như không nhìn thấy trên build thiết bị | chuyển sang IMGUI `OnGUI`, luôn vẽ đè, né safe area (lật trục vì `safeArea` đo từ đáy còn GUI đo từ đỉnh) |
| 5 | `DeviceTier.Apply` | không đặt `initialized = true` → `OnTierChanged` bắn lặp | đặt cờ; thêm chặn `SetQualityLevel` khi chưa đủ 3 level |
| 6 | `BuildScript` | `EditorUserBuildSettings.iOSBuildConfigType` đã bị gỡ | `iOSXcodeBuildConfig` + enum `XcodeBuildConfig`; đồng thời chuyển `SetScriptingBackend`/`SetIl2CppCompilerConfiguration` sang `NamedBuildTarget` và `symlinkLibraries` → `symlinkSources` cho hết cảnh báo obsolete |
| 7 | `tools/build.sh` | nhánh in log lỗi là mã chết dưới `set -e` | `\|\| EXIT_CODE=$?` |

Ngoài ra `Packages/manifest.json` đổi URP `17.2.0 → 17.3.0` và test-framework `1.4.6 → 1.6.0`,
đúng bản mà Unity 6000.3.22f1 đóng gói sẵn (đỡ một lượt tải và tránh lệch phiên bản).

---

## Sau khi kích hoạt bản quyền, chạy theo thứ tự này

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.3.22f1/Unity.app/Contents/MacOS/Unity
"$UNITY" -batchmode -quit -projectPath . -logFile open.log
```

Lượt mở đầu tiên sẽ tải các gói còn lại, sinh `ProjectSettings/`, `Library/` và toàn bộ
file `.meta`. Sau đó:

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.3.22f1/Unity.app/Contents/MacOS/Unity
"$UNITY" -batchmode -quit -projectPath . -logFile tier.log -executeMethod Eleven.Editor.Tools.TierAssetGenerator.Generate
```

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.3.22f1/Unity.app/Contents/MacOS/Unity
"$UNITY" -batchmode -quit -projectPath . -logFile boot.log -executeMethod Eleven.Editor.Tools.BootSceneGenerator.Generate
```

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.3.22f1/Unity.app/Contents/MacOS/Unity
"$UNITY" -batchmode -projectPath . -logFile tests.log -runTests -testPlatform EditMode -testResults results.xml
```

```bash
./tools/build.sh android
```

```bash
./tools/build.sh ios
```

Ba việc **không có cách nào tự động** — phải cắm máy thật vào:
`Detect()` trả đúng bậc trên ≥2 máy, HUD tốn dưới 0.2 ms, và golden hash IL2CPP khớp
`4094678572`.
