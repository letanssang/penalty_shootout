# Phase 0 — Tình trạng thực tế

*Cập nhật: 2026-08-25 · commit `4afdd44` · đã chạy Unity thật và đã chạy trên Pixel 7 thật*

## Trả lời ngắn

**Phase 0 gần xong — còn đúng một ô nghiệm thu hỏng thật, cộng vài ô chưa kiểm được.**

T01, T02, T05 đạt trọn vẹn (**cả hai nền tảng — Android APK chạy trên máy thật, iOS ra
Xcode project sạch**). T03 và T07 đạt trên phần cứng thật. T04 đạt 5/6 ô; ô còn lại —
**"GC = 0 mỗi khung hình khi HUD bật"** — **hỏng thật, đo được, không phải nghi ngờ**:
240 trên 240 khung đều cấp phát. Chi tiết và hướng sửa ở mục riêng bên dưới.

Bản trước của tài liệu này nói *"chưa kích hoạt bản quyền, đây là nút thắt"*. **Sai.**
Giấy phép đã ở trạng thái `Assigned` từ trước; lệnh kiểm tra ghi trong bản cũ
(`Unity Hub -- --headless license --list`) không phải lệnh có thật của Hub nên nó không
bao giờ trả lời được câu hỏi đó. Lệnh đúng là:

```bash
"/Applications/Unity Hub.app/Contents/MacOS/Unity Hub" --headless -- --help
```

```bash
/Applications/Unity/Hub/Editor/6000.3.22f1/Unity.app/Contents/Frameworks/UnityLicensingClient.app/Contents/MacOS/Unity.Licensing.Client --showEntitlements
```

Kết quả thật: **Unity Personal · Assigned**, quyền dùng có cả
`com.unity.editor.platforms.android` và `com.unity.editor.platforms.ios`.

---

## Môi trường đã chạy thật

| Hạng mục | Giá trị đo được |
|---|---|
| Unity | 6000.3.22f1 (Apple silicon), giấy phép Personal · Assigned |
| Máy build | macOS, Xcode 26.6 (Build 17F113) — đã build ra `Unity-iPhone.xcodeproj` thật |
| Máy thử | Google Pixel 7 (`panther`), Tensor G2 (GS201) |
| Android | 16 · API 36 · 8 lõi · 7464 MB RAM · Vulkan |

---

## Bằng chứng chạy — từng lượt một

1. **Nhập và biên dịch trong Unity thật** (`-batchmode -quit`): thoát mã 0,
   **0 `error CS`, 0 cảnh báo, 0 lỗi Burst**.
2. **Test EditMode trong Unity thật**: `results.xml` ghi
   `total="35" passed="35" failed="0"`. (Trước khi sửa ba lỗi ở mục dưới là 30/35.)
3. **Build Android**: `android_build/ElevenMetres.apk`, **41 129 321 byte**, thoát mã 0.
4. **Cài và chạy APK trên Pixel 7**, logcat xác nhận:
   `Build type 'Release', Scripting Backend 'il2cpp', CPU 'arm64-v8a', Stripping 'Enabled'`
   và `Version '0.1.0+012ead8'`.
5. **Test PlayMode trên chính thiết bị** (`DeviceAcceptanceTests`), 2026-08-25 10:31.

Lưu ý về cách chạy lượt 5: **kênh gửi kết quả từ máy về Editor không hoạt động** —
`-runTests -testPlatform Android` chạy xong test trên máy nhưng Editor treo rồi bỏ cuộc
sau 600 giây (`Test execution timed out. No activity received from the player`), và
`device-results.xml` không bao giờ được ghi. Cách đi vòng đã dùng, và nên dùng lại:

```bash
adb logcat -G 16M && adb logcat -c && adb logcat -v time > devicelog.txt &
adb shell monkey -p com.UnityTestRunner.UnityTestRunner -c android.intent.category.LAUNCHER 1
grep -a "THIET BI" devicelog.txt
```

APK test tự chạy hết bài khi khởi động, không cần Editor. Mọi số đo đều `Debug.Log` kèm
tiền tố `[THIET BI]`. Phải ghi logcat **ra file trước khi khởi động** — lần đầu tôi đọc
từ vùng đệm vòng và bị lũ `Curl error 7: cdp.cloud.unity3d.com` đẩy hết chứng cứ ra ngoài.

---

## Bảng nghiệm thu chi tiết

Ký hiệu: **✅ đạt, có bằng chứng** · **❌ hỏng thật** · **⚠️ chưa kiểm được**

### T01 — Khởi tạo repo và Git LFS

| Ô nghiệm thu | TT | Bằng chứng |
|---|---|---|
| `.gitignore` loại trừ `Library/ Temp/ Obj/ Build/ Logs/ UserSettings/ *.csproj *.sln` | ✅ | có đủ; đợt này thêm `*.slnx`, `.utmp/`, log của test |
| LFS bật cho `*.psd *.fbx *.png *.tga *.wav *.mp4 *.exr` | ✅ | `git lfs track` in đủ 7 mẫu (+ `jpg/jpeg/hdr/tif`) |
| `git lfs track` in ra đúng danh sách | ✅ | đã chạy |
| Commit thử `.png`, `git lfs ls-files` liệt kê được | ✅ | `Assets/_Project/Art/lfs-test.png` |
| `git status` sạch sau khi mở Unity rồi đóng | ✅ | Unity đã mở, sinh asset, build Android — `git status` **sạch** |

### T02 — Cây thư mục và assembly definition

| Ô nghiệm thu | TT | Bằng chứng |
|---|---|---|
| 6 asmdef runtime + 2 asmdef test | ✅ | Ball, Keeper, Shooter, Match, Presentation, UI + Tests.EditMode/PlayMode |
| `Ball` không tham chiếu asmdef nào khác | ✅ | refs chỉ có Mathematics/Collections/Burst |
| `Ball` và `Match` không tham chiếu `Presentation`/`UI` | ✅ | đã đọc từng file |
| Cả 6 bật `Unity.Mathematics`, `Unity.Collections`, `Unity.Burst` | ✅ | đã đọc từng file |
| Test runner nhận ra cả hai asmdef test | ✅ | EditMode chạy 35 test; PlayMode build thành APK và chạy trên máy |
| Sửa một dòng trong `UI` chỉ biên dịch lại `UI` | ⚠️ | phải đọc timestamp trong Console của Editor có giao diện, `-batchmode` không in ra |

### T03 — Hệ thống bậc chất lượng A/B/C

| Ô nghiệm thu | TT | Bằng chứng |
|---|---|---|
| 3 URP asset + 3 `TierProfile` tồn tại, khớp bảng plan | ✅ | đã sinh; 7 test EditMode đối chiếu từng trường **sau khi sửa lỗi `SetDirty`** |
| `Detect()` trả A/B/C đúng trên máy thật | ✅ (1 máy) | `[T03 THIET BI] tier=A model=Google Pixel 7 RAM=7464MB VRAM=7564MB cores=8 gfx=Vulkan` |
| ...trên **≥2** máy thật | ⚠️ | mới có Pixel 7. Cần thêm một máy bậc B hoặc C |
| `PlayerPrefs("tier.override")` ép được bậc | ⚠️ | mã có, chưa chạy trên máy |
| Đổi bậc lúc chạy không crash, không rò render texture | ⚠️ | phải soi Profiler nối vào máy |
| `OnTierChanged` bắn đúng một lần mỗi lần đổi | ✅ | test EditMode; lỗi `initialized` đã sửa |
| Không có `#if UNITY_IOS` trong logic phân bậc | ✅ | không có chỉ thị nào trong `DeviceTier.cs` |

### T04 — HUD đo hiệu năng

| Ô nghiệm thu | TT | Bằng chứng |
|---|---|---|
| HUD hiện được trên build thiết bị thật | ✅ | đã chụp màn hình Pixel 7; `[T04 THIET BI] het 8 giay, HUD van dang hien` |
| Hiện cả frame time trung bình và p95 | ✅ | HUD in `avg 33.4ms  p95 33.5ms`; `HistoryLength == 600` |
| Đọc nhiệt độ máy | ✅ | `[T04 THIET BI] thermalState=1 battery=0.66` — JNI `PowerManager` chạy thật |
| HUD tốn dưới 0.2 ms | ✅ ⚠️ | `tắt HUD=33.2995ms · bật HUD=33.2996ms · chênh=0.0001ms`. **Đọc kèm cảnh báo bên dưới** |
| `EndCapture()` trả CSV ghi ra `persistentDataPath`, lấy về máy được | ✅ | 2 file × 5938 byte; đã `adb pull` về `captures/`, 120 dòng số liệu + tiêu đề |
| **GC = 0 mỗi khung hình khi HUD bật** | ❌ | **240/240 khung đều cấp phát**, tổng 752 368 B, đỉnh 19 592 B |

### T05 — Script build một lệnh

| Ô nghiệm thu | TT | Bằng chứng |
|---|---|---|
| `./tools/build.sh android` chạy từ terminal | ✅ | ra APK 41 129 321 byte, thoát mã 0 |
| `./tools/build.sh ios` chạy từ terminal | ✅ | `[BuildScript] Build OK: iOS → ios_build (1166 MB, 40s, commit 4afdd44)`, thoát mã 0 — **sau khi sửa lỗi #11** |
| Build nhúng git commit hash, hiện trong HUD | ✅ | Android — logcat `Version '0.1.0+012ead8'`. iOS — `CFBundleShortVersionString = 0.1.0.78634308`, đọc ngược `printf '%x' 78634308` → `4afdd44` = đúng `HEAD`. Dòng 1 của HUD in `Application.version` |
| Android Vulkan, gỡ GLES3 | ✅ | sau build: `m_APIs: 15000000` (0x15 = Vulkan), `m_Automatic: 0` |
| iOS Metal | ✅ | `Info.plist`: `UIRequiredDeviceCapabilities = [arm64, metal]`; `ProjectSettings`: `m_APIs: 10000000` (0x10 = Metal), `m_Automatic: 0` |
| IL2CPP + ARM64 + Release | ✅ | Android — logcat: `Scripting Backend 'il2cpp', CPU 'arm64-v8a', Build type 'Release'`. iOS — có `Il2CppOutputProject/`, `ARCHS = arm64`, `iOSXcodeBuildConfig = Release` |
| Exit code khác 0 khi build hỏng | ✅ | **chứng minh bằng một lần hỏng thật**: build iOS thất bại → `build.sh` trả `1`, có in lỗi ra stderr |

---

## Ô hỏng thật: GC không bằng 0

Số đo trên Pixel 7:

```
[T04 THIET BI] GC qua 240 khung: tong=752368B so khung co cap phat=240 lon nhat=19592B
```

CSV kéo từ máy về cũng nói y hệt — cột `gc_alloc_bytes` không có dòng nào bằng 0:

```
frame,total_ms,gpu_ms,cpu_main_ms,cpu_render_ms,draw_calls,...,gc_alloc_bytes,...
0,33.663,1.096,7.005,1.004,0,0,0,2440,...
1,32.990,1.856,18.134,1.140,0,0,0,1484,...
```

**Nguyên nhân**: không phải bộ lấy mẫu. `PerfHud.Sampler` đã không cấp phát — nhiệt độ
cache ở 2 Hz, mảng `FrameTiming[1]` cấp sẵn, `ProfilerRecorder` là struct. Thủ phạm là
**chính IMGUI**: `OnGUI` cấp phát mỗi khung theo thiết kế (`GUIContent`, `CalcSize`,
chuỗi bố cục), bất kể ta có dựng lại chuỗi hay không. Ghi chú trong
`PerfHud.Renderer.cs` nói *"các khung còn lại cấp phát bằng 0"* — **ghi chú đó sai**, số
đo trên máy bác bỏ nó.

Con số 752 368 B nói trên đo trong app test, nơi giao diện test runner cũng đang vẽ IMGUI
nên có phần nhiễu. Đo riêng HUD lúc chạy app game, HUD tự báo **~1500 B mỗi khung** — vẫn
khác 0.

**Đường sửa** (đã ghi sẵn trong chính ghi chú của renderer): bỏ IMGUI, chuyển sang
TextMeshPro `SetCharArray(char[])` với bộ đệm ký tự cấp sẵn. Việc này **kéo thêm gói UGUI
vào dự án** — tức là mở rộng phạm vi so với hợp đồng T04 đã đóng băng, nên **tôi dừng ở
đây để bạn quyết**, thay vì tự ý làm.

Ba lựa chọn:

- **Sửa cho đúng**: thêm UGUI + TextMeshPro, viết lại renderer. Ô nghiệm thu đạt thật.
- **Nới hợp đồng**: đổi ô thành "dưới X byte mỗi khung" — HUD chỉ là công cụ chẩn đoán,
  mặc định tắt (`PerfHud.Visible == false`), nên rác nó thải ra không chạm vào người chơi.
- **Để nợ**: ghi vào backlog, đi tiếp Phase 1.

---

## Cảnh báo phải đọc kèm số "chênh 0.0001 ms"

Số đo là thật, nhưng **cả hai vế đều ~33.3 ms, tức khoá vsync ở 30 fps**. Ở trạng thái
đó phần lớn mỗi khung là thời gian *chờ* vsync, nên chi phí của HUD chìm trong khoảng
chờ đó và phép đo không phân giải nổi. Kết luận đúng là: **HUD không làm vỡ ngân sách
khung hình** — chứ không phải "HUD chỉ tốn 0.0001 ms".

Muốn có số sạch thì phải đo lại khi tắt vsync (`QualitySettings.vSyncCount = 0` và
`Application.targetFrameRate = -1`), hoặc đọc thẳng `cpu_main_ms` trong CSV.

---

## Bất thường đã thấy, chưa giải thích xong

| Hiện tượng | Ghi chú |
|---|---|
| `tex 0MB` — `texture_memory_bytes` luôn bằng 0 trong CSV | recorder "Total Texture Memory" trả 0 trên Android. Cần kiểm lại tên recorder cho nền tảng này |
| `avg 33.4ms` ≈ 30 fps dù bậc A đặt `targetFrameRate = 60` | trong app test, scene `Boot` không nạp nên `TierBootstrap.Awake()` không chạy, `Application.targetFrameRate` không bao giờ được áp |
| `applicationIdentifier` rỗng → APK mang tên `com.DefaultCompany.penalty_shootout` | không nằm trong ô nghiệm thu nào của Phase 0, nhưng **phải đặt trước khi nộp store** |
| Build test runner tự bật Unity Analytics trong `ProjectSettings` | chính là nguồn của lũ `Curl error 7`. Đã hoàn tác; để ý mỗi lần chạy test trên máy |
| API đồ hoạ chỉ được đặt **lúc build** bởi `BuildScript`, không lưu trong repo | build bằng nút bấm trong Editor sẽ ra mặc định (GLES3 + Vulkan), không phải Vulkan-only |
| `Failed to load native plugin: _burst_0_0` trong app test | build test không nhúng thư viện Burst. Không ảnh hưởng T07 vì hash vẫn khớp từng bit |

---

## Mười một lỗi đã tìm ra và sửa

Bảy lỗi đầu tìm được khi biên dịch ngoại tuyến (giữ nguyên từ bản trước). **Bốn lỗi cuối
chỉ lộ ra khi chạy Unity thật và build thật** — không có cách nào thấy chúng nếu chỉ đọc mã.

| # | Chỗ | Vấn đề | Cách sửa |
|---|---|---|---|
| 1 | `TierAssetGenerator.ApplyQualityLevels` | `QualitySettings.AddCustomLevel` không tồn tại ở Unity 6 | viết lại bằng `SerializedObject` trên `QualitySettings.asset` |
| 2 | `PerfHud.Sampler` | `FrameTiming.cpuMainThreadPresentTime` không tồn tại | dùng `cpuMainThreadFrameTime` |
| 3 | `PerfHud.Sampler` | `GetLatestTimings` trả `uint`, gán vào `int` | đổi kiểu biến |
| 4 | `PerfHud.Renderer` | `TextMesh` là mesh không gian thế giới → HUD không nhìn thấy trên máy | chuyển sang IMGUI `OnGUI`, né safe area |
| 5 | `DeviceTier.Apply` | không đặt `initialized = true` → `OnTierChanged` bắn lặp | đặt cờ |
| 6 | `BuildScript` | `EditorUserBuildSettings.iOSBuildConfigType` đã bị gỡ | `iOSXcodeBuildConfig` + `NamedBuildTarget` |
| 7 | `tools/build.sh` | nhánh in log lỗi là mã chết dưới `set -e` | `\|\| EXIT_CODE=$?` |
| **8** | `BallSolver` | `[BurstCompile]` gắn lên **từng method** bật Direct Call; ABI của stub cấm truyền/trả struct và vector theo giá trị → **BC1064/BC1067, hỏng AOT lúc build player** | gỡ 4 attribute cấp method, giữ đúng một cái ở cấp class như hợp đồng T06 quy định |
| **9** | `TierAssetGenerator` | thiếu `EditorUtility.SetDirty` sau khi gán trường → `AssetDatabase` chỉ lưu trạng thái *lúc tạo*. Hậu quả: **`TierProfile-B/C` và `URP-TierB/C` mang nguyên giá trị của bậc A** (vì field initializer của `TierProfile` chính là bậc A). 4 test EditMode hỏng | bảng `TierRow` có kiểu thay vì `string[][]`, luôn áp giá trị chứ không thoát sớm khi asset đã tồn tại, và `SetDirty` sau mỗi lần sửa |
| **10** | `PerfHud.EnsureComponents` | `DontDestroyOnLoad` chỉ hợp lệ ở play mode → test EditMode ném `InvalidOperationException` | ngoài play mode dùng `HideFlags.HideAndDontSave` |
| **11** | `BuildScript` — nhúng git hash | `bundleVersion = "0.1.0+012ead8"` chạy tốt trên Android nhưng **làm build iOS chết ngay**: `UnityException: iOS Version has not been set up correctly, it must consist only of '.'s and numbers`. Chỉ lộ ra khi build thật cho iOS | iOS đổi hash hex sang thập phân → `0.1.0.<số>`; hợp lệ, không mất thông tin, vẫn hiện trong HUD. Đọc ngược bằng `printf '%x\n' <số>` |

Lỗi #8 và #11 đáng chú ý nhất, vì cùng một kiểu: mã **biên dịch sạch**, **test EditMode
xanh hết**, **build Android chạy ngon** — rồi nổ ở nơi khác. #8 nổ ở khâu AOT, #11 nổ ở
khâu kiểm tra PlayerSettings của iOS. Không lượt build nào cho nền tảng kia bắt được chúng.

---

## Còn lại để đóng Phase 0

1. **Quyết hướng cho ô GC** (ba lựa chọn ở trên) — việc này cần bạn.
2. **Mượn máy thứ hai** ở bậc B hoặc C để đóng ô "`Detect()` đúng trên ≥2 máy".
3. Kiểm `tier.override` và việc đổi bậc lúc chạy trên máy — cần Profiler nối vào máy.
4. Đặt `applicationIdentifier` trước khi nghĩ tới store.
5. Vá recorder texture memory trên Android.

---

## Lệnh chạy lại toàn bộ

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.3.22f1/Unity.app/Contents/MacOS/Unity
"$UNITY" -batchmode -quit -projectPath . -logFile tier.log -executeMethod Eleven.Editor.Tools.TierAssetGenerator.Generate
```

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.3.22f1/Unity.app/Contents/MacOS/Unity
"$UNITY" -batchmode -quit -projectPath . -logFile tests.log -runTests -testPlatform EditMode -testResults results.xml
```

```bash
./tools/build.sh android
```

```bash
ADB=/Applications/Unity/Hub/Editor/6000.3.22f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
$ADB install -r android_build/ElevenMetres.apk
```

Chạy bài nghiệm thu trên máy — **không** dùng `-runTests -testPlatform Android` vì kênh
gửi kết quả về Editor treo; xem mục "Bằng chứng chạy" ở trên để biết cách đi vòng.
