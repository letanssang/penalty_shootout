← [Phase 1: Vật lý bóng](phase-1-vat-ly-bong.md) · [Mục lục](README.md) · [Phase 3: Thủ môn](phase-3-thu-mon.md) →

---

# PHASE 2 — Điều khiển và cú sút

**3 task · tuần 6–7**

Phần này sẽ phải làm lại sau user test. Đừng tối ưu sớm —
làm cho nó **chỉnh được**, đừng làm cho nó hoàn hảo.

---

## T13 — Thu và phân tích cử chỉ vuốt

**Phụ thuộc:** T02 · **Ước lượng:** ~1 ngày

```csharp
namespace Eleven.Shooter {
  public struct SwipeSample { public float2 position; public float time; }

  public struct SwipeFeatures {
    public float2 start, end;
    public float  length, duration, peakSpeed, endSpeed;
    public float  curvature;      // có dấu, dương = cong sang phải
    public float  straightness;   // 0..1, 1 = thẳng tuyệt đối
    public float  verticalRatio;  // dùng để nhận diện cú chip
  }

  public static class SwipeAnalyzer {
    public static SwipeFeatures Analyze(NativeSlice<SwipeSample> samples);
  }
}
```

**Checklist nghiệm thu**

> **ĐÃ CHẠY TEST SỐNG 2026-08-26** — không còn là đọc code tĩnh nữa.
> Lệnh: `Unity 6000.3.22f1 -batchmode -nographics -runTests -testPlatform EditMode`.
> Kết quả toàn dự án: **141/141 xanh, 0 đỏ, 0 bỏ qua** (79.5s).
>
> Trước lần chạy này **toàn bộ EditMode suite chưa từng biên dịch được** — 3 lỗi CS trong
> `ParameterFitterTests.cs` (thiếu `using System;` cho `FormattableString`) và
> `BallSolverTests.cs` (`AllocatingGCMemory` là extension method trong
> `UnityEngine.TestTools.Constraints`, thiếu `using`; import cả namespace lại đụng tên lớp
> `Is` của NUnit nên phải thêm alias `using Is = NUnit.Framework.Is;`). Đã sửa.
> Vì vậy MỌI khẳng định "test xanh" trong backlog trước ngày này đều chỉ là đọc code.
>
> Mục DPI trước đây là GAP thật, **nay đã lấp**: thêm `PhysicalUnits` + `SwipeCollector`
> (chuẩn hoá tại biên thu thập, `SwipeAnalyzer` giữ nguyên thuần toán trên đơn vị cm).

- [x] Lấy mẫu độc lập với tốc độ khung hình — 30fps và 60fps cho `curvature` chênh dưới 5% — **XANH 2026-08-26**: `FrameRateIndependence_CurvatureDiffersUnderFivePercent` + `FrameRateIndependence_PeakSpeedSimilar`
- [x] `curvature` tính bằng diện tích có dấu giữa đường vuốt và dây cung, chuẩn hoá theo độ dài — **XANH 2026-08-26**: `ArcToTheRight_CurvaturePositive`, `ArcToTheLeft_CurvatureNegative`, `ClearArc_CurvatureSignificantlyNonZero`, `LengthIsArcLengthNotChordDistance`, `Semicircle_LengthMatchesHalfCircumference`
- [x] Vuốt thẳng cho `curvature` gần 0 và `straightness` gần 1 — **XANH 2026-08-26**: `StraightLine_CurvatureNearZero_StraightnessNearOne`
- [ ] Lọc nhiễu ngón tay bằng làm mượt, nhưng không làm mất độ cong thật — bộ lọc trung bình 3 điểm có thật và các test gián tiếp (`ClearArc_...`, `ArcToTheRight/Left_...`) đều **xanh 2026-08-26**, không thấy dấu hiệu cong bị san phẳng — **VẪN CHƯA TICK: thiếu test TRỰC TIẾP so sánh có/không nhiễu trên dữ liệu tổng hợp** (đang làm), và chưa có test với nhiễu ngón tay thật
- [x] Vuốt dưới 3 mẫu bị từ chối, không làm crash — **XANH 2026-08-26**: `ZeroSamples_DoesNotCrash_AllZeros`, `OneSample_DoesNotCrash_StartEqualsEnd`, `TwoSamples_DoesNotCrash_StartEndCorrect_NoCrashFeatures`, `DegenerateAllSamePoint_NoNaNs`, và ở lớp thu: `End_WithLessThanThreeSamples_ReturnsTooFewSamples`
- [x] Chuẩn hoá theo DPI — cùng cử chỉ vật lý trên iPhone SE và iPad cho kết quả gần nhau — **XANH 2026-08-26**: `PhysicalInvariance_SamePhysicalSwipeOnDifferentDevices_YieldsIdenticalFeatures` dựng cùng một cung cong 4cm trên hai máy giả lập 326ppi/264ppi, `length` chênh dưới 1%. Hiện thực: [PhysicalUnits.cs](../../Assets/_Project/Code/Shooter/PhysicalUnits.cs) (toán thuần tách khỏi `Screen` để test được, DPI hỏng → dự phòng 290, kẹp [100,700]) + [SwipeCollector.cs](../../Assets/_Project/Code/Shooter/SwipeCollector.cs) (chốt hệ số k một lần mỗi vuốt, `NativeArray` cấp phát một lần tái dùng). Còn nợ: lớp input phía trên chưa tồn tại — xem `FlipYToBottomLeft` và hợp đồng gốc toạ độ trong `SwipeCollector.Begin`

---

## T14 — Ánh xạ cử chỉ sang thông số cú sút

**Phụ thuộc:** T13, T06 · **Ước lượng:** ~1 ngày

Toàn bộ đường cong ánh xạ phải nằm trong `ScriptableObject`, không hard-code.
Bạn sẽ chỉnh nó hàng chục lần.

> **Ràng buộc từ quyết định camera 2026-08-26** (chi tiết ở [T26](phase-5-trinh-dien.md#t26-đạo-diễn-camera)):
> giai đoạn đầu camera **đứng yên**, nên phép chiếu màn hình → điểm ngắm là hằng số.
> Nhưng `ShotMapper` **không được tự đọc camera**: nó nhận `aimPoint` đã ở **không gian thế giới**,
> do một chỗ quy đổi riêng (nhận `Camera`/ma trận view-projection làm tham số) tính sẵn.
> Nhờ vậy khi chuyển sang camera động, `ShotMapper` không phải sửa một dòng nào.

```csharp
namespace Eleven.Shooter {
  public enum ShotType { Instep, InsideFoot, Chip, Knuckle }

  public struct ShotIntent {
    public float3   aimPoint, spin;
    public float    speed;
    public ShotType type;
    public float    quality;   // 0..1, gộp từ sai số thời điểm
  }

  [CreateAssetMenu] public class ShotMappingConfig : ScriptableObject {
    public AnimationCurve lengthToSpeed, curvatureToSpin, qualityToScatter;
    public float minSpeed, maxSpeed, maxSpinRadPerSec;
  }

  public static class ShotMapper {
    public static ShotIntent Map(in SwipeFeatures f, ShotMappingConfig cfg,
                                 float timingError, uint seed);
  }
}
```

**Checklist nghiệm thu**
- [ ] Không có hằng số ma thuật trong `ShotMapper` — mọi số đến từ config
- [ ] Cùng `seed` và cùng input cho ra `ShotIntent` giống hệt
- [ ] Sai số thời điểm làm *lệch* cú sút chứ không làm hỏng hoàn toàn — kiểm bằng biểu đồ phân tán 200 cú
- [ ] Vuốt hết biên độ cho tốc độ đúng `maxSpeed`, không vượt
- [ ] 4 `ShotType` đều đạt tới được bằng cử chỉ, không cần nút bấm
- [ ] Cú `Knuckle` đặt `spin` gần 0 và bật cờ bất ổn định riêng, **không** giả lập bằng cách gán xoáy ngẫu nhiên

---

## T15 — Cửa sổ thời điểm và bất ổn định knuckle

**Phụ thuộc:** T14 · **Ước lượng:** ~1 ngày · `TẤT ĐỊNH`

Tách rõ hai thứ: **mô hình vật lý** (trọng lực, cản, Magnus) và
**bổ sung gameplay** (bất ổn định knuckle). Đừng trộn cái thứ hai vào solver và gọi nó là vật lý.

```csharp
namespace Eleven.Shooter {
  [Serializable] public struct KnuckleConfig {
    public float maxLateralDeviation;   // mét, ràng buộc cứng
    public float frequencyHz, amplitude;
    public float onsetSpeed;            // chỉ kích hoạt trên ngưỡng này
  }

  [BurstCompile] public static class KnuckleForce {
    public static float3 Evaluate(in BallState s, in KnuckleConfig c,
                                  float elapsed, uint seed);
  }
}
```

**Checklist nghiệm thu**
- [ ] `KnuckleForce` nằm ở assembly `Shooter`, **không** nằm trong `BallSolver`
- [ ] Độ lệch ngang tổng cộng không bao giờ vượt `maxLateralDeviation` — test 500 seed ngẫu nhiên
- [ ] Cùng seed cho cùng đường bay, byte giống byte
- [ ] Dưới `onsetSpeed` lực bằng đúng 0
- [ ] Xoáy khác 0 thì knuckle tắt — hai hiệu ứng loại trừ nhau
- [ ] Cửa sổ thời điểm hiển thị được bằng số ms trong chế độ debug

---

← [Phase 1: Vật lý bóng](phase-1-vat-ly-bong.md) · [Mục lục](README.md) · [Phase 3: Thủ môn](phase-3-thu-mon.md) →

> Trước khi giao việc, đọc [quy tắc giao việc](README.md#quy-tắc-giao-việc) và
> dùng [mẫu prompt](README.md#mẫu-prompt-giao-việc). Đừng gộp nhiều task vào một phiên.
