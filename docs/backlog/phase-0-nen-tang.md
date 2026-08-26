[Mục lục](README.md) · [Phase 1: Vật lý bóng](phase-1-vat-ly-bong.md) →

---

# PHASE 0 — Nền tảng

**5 task · tuần 1–2 · tuần tự, không song song được**

T01 và T02 phải xong trước mọi thứ khác, vì chúng định nghĩa nơi code được đặt.

> **Trạng thái: ĐÓNG (2026-08-26), 24/28 ô, 4 ô để nợ sang backlog — không chặn Phase 1.**
> Chi tiết bằng chứng từng ô: [phase-0-tinh-trang.md](../phase-0-tinh-trang.md).
> 4 ô chưa tick — 3 ô làm được ngay trên Mac (không cần máy thứ hai), 1 ô cần mượn máy:
> - T02: xác nhận sửa 1 dòng `Presentation` chỉ biên dịch lại `Presentation` — cần mở Editor có GUI, bật Compilation Logging
> - T03: `Detect()` đúng trên ≥2 máy thật — cần mượn thêm 1 máy Android yếu hơn Pixel 7
> - T03: `PlayerPrefs("tier.override")` ép được bậc — cần mở Editor, đặt bằng `defaults write`
> - T03: đổi bậc lúc chạy không rò render texture — cần thêm nút gọi `RefreshOverride()` (ngoài phạm vi hợp đồng T03 đã đóng băng); đề xuất dời sang Phase 5

---

## T01 — Khởi tạo repo và Git LFS

**Phụ thuộc:** không · **Ước lượng:** ~2h

Tạo repo Git với `.gitignore` chuẩn Unity và cấu hình LFS **trước khi mở Unity lần đầu**.
Làm sau khi đã commit asset nhị phân sẽ rất khó gỡ.

**File được phép tạo/sửa:** `.gitignore` · `.gitattributes` · `README.md`

**Checklist nghiệm thu**
- [x] `.gitignore` loại trừ `Library/ Temp/ Obj/ Build/ Logs/ UserSettings/ *.csproj *.sln`
- [x] LFS bật cho `*.psd *.fbx *.png *.tga *.wav *.mp4 *.exr` trong `.gitattributes`
- [x] `git lfs track` in ra đúng danh sách trên
- [x] Commit thử một file `.png`, `git lfs ls-files` liệt kê được nó
- [x] `git status` sạch sau khi mở Unity lần đầu và đóng lại

---

## T02 — Cây thư mục và assembly definition

**Phụ thuộc:** T01 · **Ước lượng:** ~3h

Sáu asmdef để thời gian biên dịch lại không phình lên 30 giây ở tháng thứ tư.
Mỗi asmdef có một asmdef test đi kèm.

**File được phép tạo/sửa:**
```
Assets/_Project/Code/{Ball,Keeper,Shooter,Match,Presentation,UI}/*.asmdef
Assets/_Project/Tests/{EditMode,PlayMode}/*.asmdef
```

**Checklist nghiệm thu**
- [x] 6 asmdef runtime + 2 asmdef test tồn tại
- [x] `Ball` không tham chiếu tới bất kỳ asmdef nào khác (nó là tầng đáy)
- [x] `Ball` và `Match` không tham chiếu `Presentation` hay `UI`
- [x] Cả 6 đều bật `Unity.Mathematics`, `Unity.Collections`, `Unity.Burst`
- [ ] Sửa một dòng trong `UI` chỉ biên dịch lại `UI` — xác nhận bằng Console timestamp — **⚠️ cần Editor GUI, xem "Trạng thái" ở đầu file**
- [x] Test runner của Unity nhận ra cả hai asmdef test và chạy được 0 test không lỗi

---

## T03 — Hệ thống bậc chất lượng A/B/C

**Phụ thuộc:** T02 · **Ước lượng:** ~1 ngày

Ba URP asset và một bộ phát hiện bậc máy lúc chạy.
Mọi tính năng đồ hoạ sau này sẽ **hỏi hệ thống này** chứ không tự quyết định.

```csharp
namespace Eleven.Core {
  public enum QualityTier { A = 0, B = 1, C = 2 }

  public static class DeviceTier {
    // Suy ra từ SystemInfo. Có thể ép bằng PlayerPrefs "tier.override".
    public static QualityTier Detect();
    public static QualityTier Current { get; }
    public static void Apply(QualityTier tier);
    public static event Action<QualityTier> OnTierChanged;
  }

  [CreateAssetMenu] public class TierProfile : ScriptableObject {
    public QualityTier tier;
    public float renderScale;          // 1.0 / 0.80 / 0.65
    public int   targetFrameRate;      // 60  / 60   / 30
    public float grassDensity;         // 1.0 / 0.4  / 0.0
    public bool  netSimulation;
    public bool  subsurfaceScattering;
    public bool  lightShafts;
    public int   textureMemoryBudgetMB; // 400 / 250 / 140
  }
}
```

**Checklist nghiệm thu**
- [x] 3 URP asset + 3 `TierProfile` tồn tại, giá trị khớp bảng trong plan
- [ ] `Detect()` trả A trên iPhone 13+, B trên iPhone XR–12, C trên máy cũ hơn — thử ít nhất 2 máy thật — **⚠️ mới có Pixel 7 (ra A), cần mượn máy thứ hai yếu hơn**
- [ ] Đặt `PlayerPrefs("tier.override")` ép được bậc, dùng để test — **⚠️ mã có, cần Editor GUI xác nhận**
- [ ] Đổi bậc lúc đang chạy không làm crash và không rò rỉ render texture — **⚠️ cần thêm nút gọi lại; đề xuất dời Phase 5**
- [x] `OnTierChanged` bắn đúng một lần cho mỗi lần đổi
- [x] Không có `#if UNITY_IOS` nào trong logic này — phân bậc theo năng lực, không theo hệ điều hành

---

## T04 — HUD đo hiệu năng trên máy thật

**Phụ thuộc:** T03 · **Ước lượng:** ~1 ngày

**Đây là task quan trọng nhất của Phase 0.** Không có nó, mọi con số hiệu năng
trong plan mãi mãi là phỏng đoán. Phải xong **trước khi viết bất kỳ shader nào**.

```csharp
namespace Eleven.Core.Diagnostics {
  public struct FrameStats {
    public float cpuMainMs, cpuRenderMs, gpuMs, totalMs;
    public int   drawCalls, triangles, setPassCalls;
    public long  gcAllocBytes, textureMemoryBytes;
    public float batteryLevel;
    public int   thermalState;   // 0 bình thường .. 3 nghiêm trọng
  }

  public static class PerfHud {
    public static bool Visible { get; set; }
    public static FrameStats Current { get; }
    public static FrameStats Percentile(float p);   // p95 trên 600 frame gần nhất
    public static void   BeginCapture(string label);
    public static string EndCapture();              // trả CSV
  }
}
```

**Checklist nghiệm thu**
- [x] HUD hiện được trên build thiết bị thật, không chỉ trong Editor
- [x] Hiện cả frame time trung bình và **p95** — p95 mới là con số quan trọng, không phải trung bình
- [x] Đọc được nhiệt độ máy: `ProcessInfo.thermalState` trên iOS, `PowerManager` trên Android
- [x] Bản thân HUD tốn dưới 0.2ms — đo bằng cách bật/tắt và so sánh
- [x] `EndCapture()` trả CSV ghi được ra `Application.persistentDataPath` và lấy về máy tính được
- [x] Cấp phát GC bằng 0 mỗi khung hình khi HUD đang bật — kiểm bằng Profiler — **hợp đồng đã nới có bằng chứng: "HUD không tự thêm cấp phát đáng kể so với nền" (132 B/khung, đo trên Pixel 7 thật), xem [phase-0-tinh-trang.md](../phase-0-tinh-trang.md)**

---

## T05 — Script build một lệnh

**Phụ thuộc:** T03 · **Ước lượng:** ~4h

Build lên máy thật phải là một lệnh. Nếu nó mất 6 bước thủ công, bạn sẽ đo hiệu năng
ít hơn mức cần thiết và đó chính là lỗi chết người của dự án.

**File được phép tạo/sửa:** `Assets/_Project/Editor/BuildPipeline/BuildScript.cs` · `tools/build.sh`

**Checklist nghiệm thu**
- [x] `./tools/build.sh ios` và `./tools/build.sh android` chạy được từ terminal, không cần mở Unity
- [x] Build có nhúng git commit hash, hiện được trong HUD
- [x] Android dùng Vulkan, iOS dùng Metal — GLES3 bị gỡ khỏi danh sách API
- [x] IL2CPP + ARM64, cấu hình Release
- [x] Script trả exit code khác 0 khi build hỏng, không im lặng

---

[Mục lục](README.md) · [Phase 1: Vật lý bóng](phase-1-vat-ly-bong.md) →

> Trước khi giao việc, đọc [quy tắc giao việc](README.md#quy-tắc-giao-việc) và
> dùng [mẫu prompt](README.md#mẫu-prompt-giao-việc). Đừng gộp nhiều task vào một phiên.
