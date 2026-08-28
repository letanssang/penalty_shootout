← [Phase 9: Âm thanh và cảm giác](phase-9-am-thanh-cam-giac.md) · [Mục lục](README.md)

---

# PHASE 10 — Tối ưu, đánh bóng và phát hành

**7 task · tuần 30–36**

Phase này tương ứng với hai mốc cuối của lộ trình: M7 (Tối ưu, tuần 30–32) và M8 (Đánh bóng +
phát hành, tuần 33–36). Rủi ro chính không phải là thiếu tính năng — những thứ đó đã có ở Phase
0–9 — mà là **ship một thứ không ổn định**, chiếm quá nhiều dung lượng, hoặc bị từ chối bởi cửa
hàng vì vi phạm quy định. Ba cổng chất lượng FUN/WOW/SHIP trong plan.md mục 08 phải qua theo thứ
tự; phase này chốt cổng SHIP.

---

## T52 — Ngân sách bộ nhớ texture theo bậc và cổng build tự động

**Phụ thuộc:** T03, T04, T05 · **Ước lượng:** ~2 ngày

Plan.md mục 03 đặt ngân sách ~400/250/140 MB bộ nhớ texture thường trú cho bậc A/B/C, nhưng cột
"Đo được" vẫn là TBD. Nguyên nhân: chưa có cách đo tự động — PerfHud hiện chỉ theo dõi frame
time và GC, không đo `Texture.totalGraphicsMemorySize`. Task này bổ sung phép đo, kết nối nó vào
BuildScript đang có (T05), và làm build đỏ khi bất kỳ bậc nào vượt ngân sách. Không có cổng này
thì mọi ngân sách trong plan.md mãi mãi là phỏng đoán.

```csharp
namespace Eleven.Core.Diagnostics
{
    /// <summary>
    /// Đo bộ nhớ texture thường trú và so với ngân sách theo bậc.
    /// Không cấp phát sau lần gọi đầu tiên — chỉ đọc giá trị từ UnityEngine.Profiling.
    /// </summary>
    public static class TextureMemoryAudit
    {
        /// <summary>Ngân sách bộ nhớ texture thường trú theo bậc, tính bằng byte.</summary>
        public static long BudgetBytesForTier(QualityTier tier);

        /// <summary>
        /// Bộ nhớ texture thường trú hiện tại, tính bằng byte.
        /// Đọc từ Profiling.Profiler.GetAllocatedMemoryForGraphicsDriver — không cấp phát.
        /// </summary>
        public static long CurrentTextureBytes();

        /// <summary>
        /// So sánh bộ nhớ hiện tại với ngân sách của tier.
        /// Trả về TextureAuditResult có trường Passed, OverBudgetBytes, TierBudgetBytes,
        /// CurrentBytes để caller tự quyết định hành động (in log hoặc fail build).
        /// </summary>
        public static TextureAuditResult Evaluate(QualityTier tier);
    }

    public struct TextureAuditResult
    {
        public bool   Passed;
        public long   CurrentBytes;
        public long   TierBudgetBytes;
        public long   OverBudgetBytes;   // 0 nếu Passed
        public QualityTier Tier;
    }
}

namespace Eleven.Editor
{
    /// <summary>
    /// Hook vào BuildScript (T05) — gọi TextureMemoryAudit sau khi build xong,
    /// ném BuildFailedException nếu bất kỳ bậc nào vượt ngân sách.
    /// </summary>
    public static class TextureBudgetBuildCheck
    {
        /// <summary>
        /// Chạy audit cho cả ba bậc A/B/C.
        /// Ghi kết quả ra Debug.Log dù pass hay fail.
        /// Ném BuildFailedException chỉ khi có bậc vượt ngân sách.
        /// </summary>
        public static void RunAll();
    }
}
```

**Checklist nghiệm thu**
- [ ] Bảng ngân sách trong mã khớp plan.md mục 03: bậc A ≤ 400 MB, bậc B ≤ 250 MB, bậc C ≤ 140 MB — số lấy từ hằng số trong `TextureMemoryAudit`, không viết cứng ở nơi khác.
- [ ] `Evaluate` trả `Passed = true` khi dùng asset thật trên máy bậc B (Pixel 7); ghi số đo thực lên báo cáo benchmark cùng tên máy và ngày đo.
- [ ] Build bị dừng khi thêm một texture 512 MB vào project rồi chạy `TextureBudgetBuildCheck.RunAll()` — `BuildFailedException` được ném và Unity log hiện thông điệp có chứa "vượt ngân sách".
- [ ] `CurrentTextureBytes()` không cấp phát GC — đo bằng `Is.Not.AllocatingGCMemory()` qua 500 lần gọi liên tiếp.
- [ ] Kết quả audit xuất hiện trong CSV của `RegressionReport` (T33) — cột "Texture MB" có số thật từ máy thật, không phải 0 hay -1.

---

## T53 — Gỡ gói không dùng và danh sách shader variant được phép

**Phụ thuộc:** T05, T31 · **Ước lượng:** ~2 ngày

Nợ đo được: gói `com.unity.ai.inference` phiên bản 2.6.1 (xác nhận trong
`Packages/manifest.json:7`) biên dịch hàng nghìn biến thể compute shader mỗi lần build và phình
APK/IPA, trong khi dự án không dùng đến. Bên cạnh đó, `com.unity.ai.assistant`, `com.unity.ai.navigation`, `com.unity.analytics`, `com.unity.purchasing`, `com.unity.xr.legacyinputhelpers`, `com.unity.multiplayer.center` là những gói không có trong plan.md mục 09
và không được tham chiếu trong bất kỳ file .cs nào của dự án. Task này gỡ chúng, dựng danh sách
shader variant được phép (`ShaderVariantCollection`), và đo kích thước APK/IPA trước–sau. Nếu
không làm, thời gian build ~8 phút sẽ không giảm và kích thước tải về sẽ ảnh hưởng chuyển đổi
trên cửa hàng.

```csharp
namespace Eleven.Editor
{
    /// <summary>
    /// Kiểm tra manifest.json và tất cả .asmdef trong Assets/_Project/
    /// để tìm gói được khai trong manifest nhưng không có tham chiếu thực tế trong mã nguồn.
    /// Không sửa manifest — chỉ báo cáo để người xác nhận trước khi gỡ.
    /// </summary>
    public static class UnusedPackageAuditor
    {
        public struct PackageAuditResult
        {
            public string PackageId;
            public bool   IsReferencedInCode;  // true nếu có ít nhất 1 using/asmref
            public bool   IsReferencedInAsmdef; // true nếu có trong .asmdef references
            public string[] SampleUsages;       // tối đa 3 dòng, để người xác nhận
        }

        /// <summary>
        /// Quét toàn bộ Assets/_Project/ và trả danh sách audit cho mọi gói trong manifest.
        /// Không cấp phát runtime — chỉ chạy trong Editor (MenuItem hoặc BuildScript).
        /// </summary>
        public static PackageAuditResult[] Audit();

        /// <summary>
        /// Xuất báo cáo ra file văn bản tại đường dẫn chỉ định.
        /// </summary>
        public static void SaveReport(PackageAuditResult[] results, string outputPath);
    }

    /// <summary>
    /// Kiểm tra ShaderVariantCollection có đủ tất cả variant của shader
    /// được liệt kê trong AllowedShaders và không chứa variant của shader ngoài danh sách.
    /// Tích hợp vào BuildScript như TextureBudgetBuildCheck.
    /// </summary>
    public static class ShaderVariantBuildCheck
    {
        /// <summary>Danh sách shader được phép ship — phải bao gồm đúng tập đã dùng.</summary>
        public static readonly string[] AllowedShaderNames;

        /// <summary>
        /// Chạy kiểm tra. Ném BuildFailedException nếu có variant ngoài danh sách.
        /// Ghi số variant trước và sau khi strip ra Debug.Log.
        /// </summary>
        public static void RunCheck(ShaderVariantCollection allowedCollection);
    }
}
```

**Checklist nghiệm thu**
- [ ] `UnusedPackageAuditor.Audit()` xác định đúng các gói không tham chiếu — tự chạy trong Editor, in ra danh sách gồm ít nhất `com.unity.ai.inference` và `com.unity.ai.assistant`.
- [ ] Sau khi gỡ `com.unity.ai.inference` khỏi manifest.json và build lại: thời gian build giảm ít nhất 30 giây so với baseline trước đó (ghi số đo trước/sau kèm tên máy build).
- [ ] Kích thước APK (Android) giảm ít nhất 5 MB sau khi gỡ xong các gói không dùng — ghi số đo trước/sau kèm phiên bản build.
- [ ] `ShaderVariantBuildCheck` không ném lỗi khi chạy với `ShaderVariantCollection` hợp lệ đã dựng từ các shader thật trong project.
- [ ] Build CI vẫn xanh sau khi gỡ gói — test EditMode 534 test không giảm xuống dưới 530 (cho phép tối đa 4 test liên quan bị gỡ cùng gói).

---

## T54 — Thay ScoreboardUI từ IMGUI sang UGUI

**Phụ thuộc:** T03 · **Ước lượng:** ~2 ngày

Nợ đo được: `ScoreboardUI` hiện dùng `OnGUI` (IMGUI). Dù file hiện tại đã giảm thiểu cấp phát
bằng cách cache texture và style, `OnGUI` vẫn gây cấp phát GC mỗi khung hình do Unity gọi lại
toàn bộ pipeline IMGUI — không thể tránh mà không rời khỏi IMGUI. Trên thiết bị tầm trung, GC
spike ở đây làm frame time nhảy bậc thang rõ rệt. Task này dựng lại `ScoreboardUI` bằng UGUI
(`TextMeshPro` + `Image` + `Button`), **giữ nguyên toàn bộ API công khai mà `MatchGameLoop` đang
gọi** (xác nhận từ `MatchGameLoop.cs:48` nơi `[SerializeField] private ScoreboardUI scoreboard`
được gán và các lời gọi `scoreboard.UpdateScores`, `scoreboard.SetTurn`, v.v. rải rác trong file).
Lớp `ScoreboardUI` vẫn là `MonoBehaviour` gắn trên cùng `GameObject`, chỉ thay phần render.

```csharp
namespace Eleven.UI
{
    // Toàn bộ chữ ký công khai phải giữ nguyên — MatchGameLoop không được sửa.
    // Liệt kê để agent không tự ý thêm/bớt/đổi kiểu.

    public sealed class ScoreboardUI : MonoBehaviour
    {
        // Sự kiện — giữ nguyên
        public event Action OnReplayClicked;
        public event Action OnNextKickClicked;
        public event Action<DifficultyLevel> OnDifficultyChanged;

        // API — giữ nguyên chữ ký, thay phần thân
        public void UpdateScores(List<KickResult> home, List<KickResult> away, int kickIndex);
        public void SetTurn(bool isPlayerTurn, int roundNumber, bool suddenDeath);
        public void SetCurrentShotInfo(ShotType type, float speedMps);
        public void HideShotBadge();
        public void ShowBanner(string title, string subtitle, Color color, bool replayAvailable = true);
        public void HideBanner();
        public void SetPrompt(string text);
        public void SetTimingBar(bool visible, float progress01,
                                 float perfectCenter01, float perfectHalfWidth01,
                                 float goodHalfWidth01);
        public void ShowTimingGrade(TimingGrade grade, float errorMs);
        public void SetDifficulty(DifficultyLevel level);
        public void SetKeeperDebug(string text);
    }
}
```

**Checklist nghiệm thu**
- [ ] `MatchGameLoop` biên dịch sạch sau khi thay `ScoreboardUI` — không sửa một dòng nào trong `MatchGameLoop.cs`.
- [ ] **0 byte GC mỗi khung hình** khi UI đang hiện đầy đủ (scoreboard + turn band + timing bar + badge): đo bằng `Profiler.GetMonoUsedSizeLong()` trong 500 khung hình liên tiếp, không có khung nào tăng.
- [ ] Số đo từ máy thật (ghi tên máy): GC alloc mỗi khung hình **trước** và **sau** khi thay — phải có cả hai con số để xác nhận cải thiện.
- [ ] Tất cả sự kiện (`OnReplayClicked`, `OnNextKickClicked`, `OnDifficultyChanged`) bắn đúng khi nhấn nút — kiểm bằng test EditMode gắn mock listener và mô phỏng nhấn.
- [ ] Bố cục co giãn đúng ở ba tỉ lệ màn hình: 16:9 landscape, 19.5:9 landscape, và màn hình nhỏ 5 inch 720p — ghi ảnh chụp màn hình từ máy thật vào báo cáo đo.
- [ ] `OnDestroy` không rò bộ nhớ — không còn texture 2×2 tạo bằng `new Texture2D` vì UGUI dùng Sprite asset thay thế.

---

## T55 — Mật độ và kích thước cỏ, khán giả: điều chỉnh khoảng cách và đo GPU

**Phụ thuộc:** T29, T30, T04 · **Ước lượng:** ~2 ngày

Nợ đo được: cỏ hiện vẽ 13.901 lá (số từ `DebugHotkeys.LogRenderCounts`) và khán giả 1.244 người
mỗi khung, nhưng cả hai gần như không nhìn thấy — lá cỏ cao 6–11 cm (chốt trong mã tại
`GrassField.cs`) ở khoảng cách camera điển hình dưới một pixel, và khán đài tối vì
`PostProcessTierConfig` chưa nối vào URP Volume nào. Task này không quyết định màu sắc hay bố cục
(đó là thẩm mỹ, việc của người) mà giải quyết hai vấn đề kỹ thuật có thể đo được: (1) mật độ
phân tầng theo khoảng cách camera để số lá vẽ tỉ lệ thuận với pixel thực tế chúng chiếm, và (2)
PostProcessTierConfig nối vào URP Volume Runtime để số trong bảng thực sự áp dụng lúc chạy. Kết
quả phải đo được bằng số GPU ms, không phải cảm nhận.

```csharp
namespace Eleven.Presentation.Grass
{
    /// <summary>
    /// Bộ lọc LOD cho cỏ: nhận khoảng cách từ camera tới từng vùng ô và trả về
    /// mật độ thực tế (0.0–1.0) áp dụng cho ô đó.
    /// Không cấp phát. Chạy trên main thread trong GrassField.Update().
    /// </summary>
    public static class GrassLodFilter
    {
        /// <summary>
        /// Tính hệ số mật độ cho một ô dựa trên khoảng cách camera.
        /// distanceMeters: khoảng cách từ camera tới tâm ô.
        /// maxDensityRadius: bán kính trong đó mật độ là 1.0 (lấy từ TierProfile).
        /// cullRadius: khoảng cách cắt hoàn toàn — ô xa hơn này không vẽ.
        /// Trả về giá trị trong [0.0, 1.0]; 0.0 nghĩa là không vẽ.
        /// </summary>
        public static float DensityAt(float distanceMeters,
                                      float maxDensityRadius,
                                      float cullRadius);
    }
}

namespace Eleven.Presentation
{
    /// <summary>
    /// Nối PostProcessTierConfig vào URP Volume Runtime khi bậc thay đổi.
    /// Gắn trên cùng GameObject với Volume. Không cấp phát trong Update.
    /// </summary>
    public sealed class PostProcessTierApplier : MonoBehaviour
    {
        /// <summary>
        /// Áp dụng bộ thông số của tier lên Volume đang gắn.
        /// Gọi một lần khi khởi động và mỗi khi TierBootstrap.OnTierChanged bắn.
        /// </summary>
        public void Apply(QualityTier tier, in PostProcessTierConfig config);
    }
}
```

**Checklist nghiệm thu**
- [ ] Số lá cỏ được vẽ ở bậc A trên Pixel 7: đo bằng `GrassFieldRenderer.DrawnInstanceCount` sau khi LOD filter áp dụng, ghi số trước/sau kèm tên máy — mục tiêu là giảm ít nhất 30% số lá ở khoảng cách > 15m mà không thay đổi mật độ trong 12m gần camera.
- [ ] GPU time của cỏ tại bậc A: đo bằng RenderDoc hoặc Snapdragon Profiler, ghi số thật kèm tên máy — so với ngân sách 2.0ms trong plan.md mục 04.
- [ ] `PostProcessTierApplier.Apply()` không cấp phát GC — đo bằng `Is.Not.AllocatingGCMemory()` qua 1000 lần gọi.
- [ ] `DensityAt(0f, ...)` = 1.0 và `DensityAt(cullRadius, ...)` = 0.0 — hai biên này được test tự động trong EditMode.
- [ ] Bloom và vignette trên khán đài thay đổi rõ khi chuyển bậc A → C trên máy thật — ghi ảnh chụp màn hình hai bậc cùng góc camera vào báo cáo; nếu không thay đổi thì kết luận PostProcessTierApplier chưa nối đúng.
- [ ] Số GPU ms của khán giả tại bậc A: ghi số thật kèm tên máy — so với ngân sách 0.8ms trong plan.md mục 04.

---

## T56 — Cổng ổn định nhiệt và pin: SoakTestRunner thành cổng SHIP tự động

**Phụ thuộc:** T34, T04 · **Ước lượng:** ~1 ngày

`SoakTestRunner` (T34) đã chạy được và ghi 120 mẫu trong 20 phút, nhưng còn hai nợ chưa trả:
(1) chưa có phím tắt để khởi động test — `DebugHotkeys` hiện chỉ nối `BenchmarkRunner` vào F2,
không có phím cho Soak; (2) kết quả 20 phút chỉ xuất CSV để người ngồi đọc tay, chưa tự động
phát hiện fail và chặn build. Task này nối F3 cho SoakTestRunner trong `DebugHotkeys`, và dựng
một hook BuildScript đọc file CSV mới nhất, kiểm tiêu chí thoát M7 (`minFps >= 55` ở bậc B và
`minFps >= 30` ở bậc C), rồi fail build nếu chưa có kết quả hợp lệ. Không chạy lại Soak trong
build — đó là test thủ công một lần; BuildScript chỉ đọc kết quả đã lưu.

```csharp
namespace Eleven.Presentation.Diagnostics
{
    public sealed class DebugHotkeys : MonoBehaviour
    {
        // Thêm vào API hiện có (không xoá hay sửa phần cũ):

        /// <summary>
        /// Khởi động SoakTestRunner 20 phút. F3 trên bàn phím.
        /// Trên cảm ứng: 4 ngón đồng thời (tránh đụng gesture 3 ngón đang dùng).
        /// Bọc try/catch giống RunBenchmarkNow — không ném lên game.
        /// </summary>
        public void StartSoakTest();
    }
}

namespace Eleven.Editor
{
    /// <summary>
    /// Đọc file CSV Soak mới nhất trong Application.persistentDataPath và
    /// kiểm tiêu chí thoát M7. Gọi từ BuildScript sau khi build APK/IPA.
    /// </summary>
    public static class SoakGateBuildCheck
    {
        /// <summary>
        /// Tìm file soak_test_report.csv mới nhất trong thư mục chỉ định.
        /// Phân tích SoakTestResult.minFps và qualityTier.
        /// Ném BuildFailedException nếu:
        ///   - Không tìm thấy file CSV nào.
        ///   - File cũ hơn 7 ngày (kết quả cũ không đại diện cho build hiện tại).
        ///   - Bậc B: minFps < 55.
        ///   - Bậc C: minFps < 30.
        ///   - isChargingDetected = true (máy đang sạc khi chạy test — không hợp lệ).
        /// </summary>
        public static void RunCheck(string soakReportDirectory);
    }
}
```

**Checklist nghiệm thu**
- [ ] F3 khởi động SoakTestRunner trên Pixel 7 — xác nhận bằng log `[DebugHotkeys] SoakTest bắt đầu` xuất hiện trong adb logcat.
- [ ] File `soak_test_report.csv` được ghi sau 20 phút và `SoakTestResult.minFps` không phải 0 — ghi số thật từ Pixel 7 ở bậc B kèm ngày đo.
- [ ] `SoakGateBuildCheck.RunCheck()` ném `BuildFailedException` khi truyền một file CSV giả có `minFps = 50.0` ở bậc B — test tự động trong EditMode.
- [ ] `SoakGateBuildCheck.RunCheck()` ném `BuildFailedException` khi truyền đường dẫn thư mục không có file CSV nào — test tự động trong EditMode.
- [ ] `SoakGateBuildCheck.RunCheck()` không ném lỗi khi file CSV hợp lệ có `minFps = 56.2` ở bậc B và `minFps = 30.5` ở bậc C và `isChargingDetected = false` — test tự động trong EditMode.
- [ ] Trên Pixel 7 thật, bậc B đạt tiêu chí thoát M7: `minFps >= 55` trong toàn bộ 20 phút — ghi số thật, nếu không đạt thì **không tick mục này** và ghi rõ giá trị đo được để người quyết định cắt tính năng.

---

## T57 — Chuẩn bị phát hành: định danh, ký, quyền và tuân thủ cửa hàng

**Phụ thuộc:** T05 · **Ước lượng:** ~2 ngày

Phần code được của việc chuẩn bị phát hành. Định danh hiện tại là `com.eleven.metres` cho Android
(xác nhận từ `ProjectSettings.asset`), `productName` là `Eleven Metres`, `companyName` là
`DefaultCompany` — cần đổi `companyName` trước khi ship. Gói iOS bundleIdentifier chưa được xác
nhận trong file đọc được — cần kiểm và đặt đồng nhất. Task này dựng script kiểm tra tự động các
trường bắt buộc (không để quên điền lúc bận), và dựng danh sách công việc giấy tờ/tài khoản
mà code không làm thay được. Xử lý sai một trường trong các mục này có thể làm bị từ chối build.

```csharp
namespace Eleven.Editor
{
    /// <summary>
    /// Kiểm tra các trường PlayerSettings bắt buộc trước khi submit lên cửa hàng.
    /// Chạy tự động trong BuildScript; không yêu cầu kết nối internet.
    /// </summary>
    public static class StoreSubmissionCheck
    {
        public struct SubmissionIssue
        {
            public string Field;
            public string CurrentValue;
            public string ExpectedPattern;  // mô tả yêu cầu, không phải regex
            public bool   IsBlocker;        // true = build bị chặn, false = cảnh báo
        }

        /// <summary>
        /// Kiểm tra các trường: applicationIdentifier (Android + iOS), companyName,
        /// productName, bundleVersion, Android minSdkVersion, iOS deployment target,
        /// scripting backend (IL2CPP là bắt buộc), architecture (ARM64 bắt buộc cho iOS).
        /// Trả danh sách issue; danh sách rỗng nghĩa là tất cả pass.
        /// </summary>
        public static SubmissionIssue[] Check();

        /// <summary>
        /// Ném BuildFailedException nếu có ít nhất một IsBlocker = true.
        /// Ghi mọi issue (kể cả cảnh báo) ra Debug.Log trước khi ném.
        /// </summary>
        public static void EnforceInBuild();
    }

    /// <summary>
    /// Kiểm tra quyền (permission) được khai báo trong AndroidManifest.xml và
    /// Info.plist không chứa quyền ngoài danh sách cho phép.
    /// Dự án này không cần camera, microphone, location, contact hay bất kỳ
    /// quyền nhạy cảm nào — mọi quyền ngoài danh sách đều là lỗi.
    /// </summary>
    public static class PermissionAudit
    {
        /// <summary>Danh sách quyền Android được phép ship.</summary>
        public static readonly string[] AllowedAndroidPermissions;

        /// <summary>Danh sách Usage Description key iOS được phép ship.</summary>
        public static readonly string[] AllowedIosUsageKeys;

        /// <summary>
        /// Đọc AndroidManifest.xml và Info.plist, trả danh sách quyền/key không nằm trong
        /// danh sách cho phép. Danh sách rỗng nghĩa là sạch.
        /// </summary>
        public static string[] FindUnauthorizedPermissions();
    }
}
```

**Checklist nghiệm thu**
- [ ] `StoreSubmissionCheck.Check()` phát hiện `companyName = "DefaultCompany"` là blocker — test tự động trong EditMode.
- [ ] `StoreSubmissionCheck.Check()` phát hiện `applicationIdentifier` không hợp lệ (chứa khoảng trắng hoặc ký tự hoa) là blocker — test tự động trong EditMode.
- [ ] `PermissionAudit.FindUnauthorizedPermissions()` trả danh sách rỗng khi AndroidManifest.xml chỉ có quyền `INTERNET` (nếu cần crash reporting) và không có quyền nào khác — test tự động trong EditMode.
- [ ] Build APK với scripting backend là Mono bị chặn bởi `StoreSubmissionCheck` — IL2CPP là bắt buộc.
- [ ] Build iOS với architecture x86_64 bị chặn — ARM64 là bắt buộc cho App Store.

> **Việc của người — code không làm thay được:**
> - Đăng ký tài khoản Apple Developer ($99/năm) và Google Play ($25 một lần) nếu chưa có.
> - Tạo App ID trên App Store Connect và Google Play Console.
> - Tạo và giữ an toàn provisioning profile, certificate (.p12), keystore (.jks/.aab key).
> - Viết mô tả ứng dụng, keywords, screenshot cho từng ngôn ngữ.
> - Điền rating questionnaire (độ tuổi) trên cả hai cửa hàng.
> - Đọc kỹ App Store Review Guidelines 4.2.6 (minimum functionality) và Google Play policy.
> - Rà soát IP trước khi submit — xem plan.md mục 12.

---

## T58 — Báo cáo lỗi sau phát hành và ranh giới thu thập dữ liệu

**Phụ thuộc:** T57 · **Ước lượng:** ~2 ngày

Game cần biết khi crash trên thiết bị người chơi mà nhà phát triển không có trong tay. Nhưng
ranh giới thu thập dữ liệu phải rõ và phải trung thực trong chính sách riêng tư: không thu thập
gì không cần thiết cho mục đích kỹ thuật. Dự án này không có lý do kỹ thuật rõ ràng để theo dõi
hành vi người chơi (session length, button tap, progression) — nên không làm. Chỉ thu crash log
và thông tin thiết bị tối thiểu đủ để tái hiện lỗi. Task này dựng `CrashReporter` wrapper mỏng
quanh Unity Cloud Diagnostics (hoặc Firebase Crashlytics nếu người chọn), đảm bảo không có event
analytics nào được bật ngầm, và tạo file `PrivacyDataInventory` mô tả chính xác dữ liệu thu.

```csharp
namespace Eleven.Core.Diagnostics
{
    /// <summary>
    /// Wrapper mỏng quanh nền tảng crash reporting được chọn.
    /// Giao diện này cố định — nền tảng bên dưới có thể đổi mà không sửa code game.
    /// </summary>
    public static class CrashReporter
    {
        /// <summary>
        /// Khởi tạo crash reporter. Gọi một lần khi khởi động app.
        /// Không thu thập gì ngoài: exception message, stack trace,
        /// DeviceTier (bậc A/B/C), QualityTier, Unity version, OS version.
        /// Không thu email, IDFA, GAID, location, hay bất kỳ PII nào.
        /// </summary>
        public static void Initialise(CrashReportConfig config);

        /// <summary>
        /// Ghi một key-value context không chứa PII để đính kèm crash report.
        /// Chỉ được phép với key nằm trong AllowedContextKeys.
        /// Ném ArgumentException nếu key không hợp lệ.
        /// </summary>
        public static void SetContext(string key, string value);

        /// <summary>
        /// Danh sách key được phép — đóng băng tại compile time.
        /// Thêm key mới phải sửa danh sách này và cập nhật PrivacyDataInventory.
        /// </summary>
        public static readonly string[] AllowedContextKeys;  // e.g. "device_tier", "quality_tier", "match_seed"

        /// <summary>
        /// Báo cáo thủ công một exception không gây crash (caught exception đáng chú ý).
        /// Không cấp phát ngoài lúc gọi — phù hợp dùng trong catch block.
        /// </summary>
        public static void ReportNonFatal(Exception ex, string context = null);
    }

    public struct CrashReportConfig
    {
        /// <summary>false = tắt hoàn toàn (dùng trong test build).</summary>
        public bool Enabled;
        /// <summary>Tên nền tảng để log, không dùng cho routing code.</summary>
        public string BackendName;
    }
}
```

**Checklist nghiệm thu**
- [ ] `SetContext` ném `ArgumentException` khi truyền key ngoài `AllowedContextKeys` — test tự động trong EditMode, không cần kết nối crash backend.
- [ ] `Initialise` với `Enabled = false` không gọi bất kỳ API nào của crash backend — kiểm bằng mock backend trong test EditMode.
- [ ] Không có event analytics nào được bật: `com.unity.analytics` đã bị gỡ ở T53; kiểm thêm rằng không có lời gọi `Analytics.CustomEvent` hay `FirebaseAnalytics.LogEvent` trong toàn bộ codebase — chạy `UnusedPackageAuditor` và grep xác nhận.
- [ ] File `Assets/_Project/docs/PrivacyDataInventory.md` tồn tại và liệt kê chính xác từng loại dữ liệu thu, mục đích kỹ thuật, thời gian lưu, và bên thứ ba nào nhận — **nội dung file này là việc của người** (không giao agent viết chính sách riêng tư); task chỉ tạo file rỗng có cấu trúc đúng để người điền.
- [ ] Crash report từ Pixel 7 (giả lập bằng `Debug.LogException` qua F4 trong DebugHotkeys) xuất hiện trên dashboard của backend trong vòng 5 phút — ghi ảnh chụp màn hình dashboard kèm ngày kiểm.
- [ ] `ReportNonFatal` không cấp phát GC khi `Enabled = false` — đo bằng `Is.Not.AllocatingGCMemory()`.

> **Việc của người — code không làm thay được:**
> - Viết nội dung chính sách riêng tư thật bằng ngôn ngữ người chơi hiểu được và host nó ở URL cố định (App Store và Google Play đều yêu cầu URL, không phải file đính kèm).
> - Điền App Privacy Nutrition Label trên App Store Connect (Data Not Collected nếu chỉ có crash log không liên kết với danh tính).
> - Điền Data Safety section trên Google Play Console.
> - Quyết định chọn Unity Cloud Diagnostics hay Firebase Crashlytics hay backend khác — đây là quyết định về tài khoản và chi phí vận hành, không phải quyết định kỹ thuật thuần.

---

[Mục lục](README.md) · [Phase 9: Âm thanh và cảm giác](phase-9-am-thanh-cam-giac.md)
