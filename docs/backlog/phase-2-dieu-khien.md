← [Phase 1: Vật lý bóng](phase-1-vat-ly-bong.md) · [Mục lục](README.md) · [Phase 3: Thủ môn](phase-3-thu-mon.md) →

---

# PHASE 2 — Điều khiển và cú sút

**3 task · tuần 6–7**

Phần này sẽ phải làm lại sau user test. Đừng tối ưu sớm —
làm cho nó **chỉnh được**, đừng làm cho nó hoàn hảo.

> **TRẠNG THÁI 2026-08-26 (chiều): T13, T14, T15 — XONG PHẦN CODE ĐƯỢC.**
> Bằng chứng sống: `Unity 6000.3.22f1 -batchmode -nographics -runTests -testPlatform EditMode`
> → **235/235 xanh, 0 đỏ, 0 bỏ qua, 91.7 s.** (Lần chạy trước trong ngày là 141 test; chênh lệch
> là các bộ test của T14/T15 và các test bù gap của T13.) Bộ test liên quan Phase 2:
> `ShotMapperTests` 25 · `TimingWindowTests` 21 · `SwipeAnalyzerTests` 18 · `KnuckleForceTests` 18 ·
> `SwipeSmoothingTests` 9 · `SwipeCollectorTests` 7 · `AimProjectorTests` 8 · `PhysicalUnitsTests` 2(+13 case).
>
> **4 điểm HỢP ĐỒNG BỊ DIỄN GIẢI RỘNG HƠN BẢN GỐC — cần biết trước khi đọc checklist:**
> 1. `ShotMapper.Map` có thêm tham số `float3 aimPoint` so với chữ ký trong tài liệu này. Bắt buộc:
>    hợp đồng gốc không có đường nào để điểm ngắm đi vào, mà ghi chú camera ngay dưới đây lại yêu cầu
>    ShotMapper KHÔNG được tự đọc camera. Phép chiếu nằm riêng ở
>    [AimProjector.cs](../../Assets/_Project/Code/Shooter/AimProjector.cs).
> 2. `ShotIntent` có thêm `unstable` (cờ knuckle) và `scatterRadius` (chẩn đoán). `unstable` là cách
>    duy nhất để thoả ô "không giả lập knuckle bằng xoáy ngẫu nhiên" mà vẫn báo được cho T15.
> 3. `SwipeFeatures` có thêm `straightnessSmooth` — đo độ thẳng trên đường ĐÃ làm mượt. Lý do đo được
>    ghi trong code: trên số liệu thô, cú vuốt thẳng của người tay run (0.839) trông còn kém thẳng hơn
>    cú ngoằn ngoèo cố ý (0.865), nên phân loại hình dáng bằng `straightness` thô là sai về nguyên tắc.
> 4. `KnuckleConfig` có thêm `envelopeRiseSeconds` và `KnuckleConfig.Default`; và **`TimingWindow`
>    là một kiểu MỚI hoàn toàn**, không có trong hợp đồng T15 — xem lý do ở phần T15 bên dưới.

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

> **ĐÃ CHẠY TEST SỐNG 2026-08-26 (lần chạy buổi sáng)** — không còn là đọc code tĩnh nữa.
> Lệnh: `Unity 6000.3.22f1 -batchmode -nographics -runTests -testPlatform EditMode`.
> Kết quả toàn dự án lúc đó: **141/141 xanh, 0 đỏ, 0 bỏ qua** (79.5 s).
> *(Lần chạy buổi chiều cùng ngày, sau khi có T14/T15: **235/235 xanh** — xem đầu trang.)*
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
- [x] Lọc nhiễu ngón tay bằng làm mượt, nhưng không làm mất độ cong thật — **XANH 2026-08-26 (chạy sống)**: gap "thiếu test TRỰC TIẾP" đã lấp bằng [SwipeSmoothingTests.cs](../../Assets/_Project/Tests/EditMode/SwipeSmoothingTests.cs) — 9 test dựng CÙNG một cú vuốt hai lần (sạch / cộng nhiễu) rồi so hai kết quả với nhau, không có con số kỳ vọng viết cứng nào. Nhiễu dùng là **răng cưa** (mẫu chẵn +A, mẫu lẻ −A) tức trường hợp xấu nhất, biên độ quy từ pixel thật trên máy 326 ppi: 3 px (rung cảm ứng thường) và 13 px (tay run). Kết quả: độ cong lệch dưới 8% ở mức tay run và dưới 2% ở mức thường (`LamMuot_GiuDuocDoCong_KhiCoNhieu`, `NhieuMucThuong_GanNhuKhongAnhHuongDoCong`), dấu độ cong không bao giờ đảo (`LamMuot_GiuDauDoCong_KhiCoNhieu`), suy giảm đơn điệu theo biên độ nhiễu nên không có cộng hưởng với chu kỳ răng cưa (`NhieuCangManh_DoCongCangIt_BiKeoLech`), và đường THẲNG có nhiễu không sinh nổi độ cong giả vượt ngưỡng knuckle (`DuongThangCoNhieu_KhongSinhDoCongGiaVuotNguongKnuckle` — kiểm thẳng hệ quả gameplay, không kiểm bằng một con số trừu tượng). Hai test khoá hợp đồng ngược lại: `length`/`peakSpeed`/`straightness` CỐ Ý đo trên mẫu thô, và hai đầu mút không bao giờ bị làm mượt (nếu không thì ngắm lệch). **Còn nợ, thuộc về nghiệm thu trên máy thật (T33/T34), không phải việc code:** chưa có dữ liệu vuốt của ngón tay THẬT — mọi nhiễu ở đây là tổng hợp.
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

**Checklist nghiệm thu** — **25 test trong [ShotMapperTests.cs](../../Assets/_Project/Tests/EditMode/ShotMapperTests.cs), TẤT CẢ XANH 2026-08-26 (chạy sống)**

- [x] Không có hằng số ma thuật trong `ShotMapper` — mọi số đến từ config — hằng số duy nhất còn lại trong file là `Epsilon = 1e-6f` để chặn chia cho 0, không phải số chỉnh tay. Không tự nhận suông: 4 test đổi config rồi kiểm kết quả đổi theo — `DoiMaxSpeed_KetQuaDoiTheo`, `DoiMaxSpin_KetQuaDoiTheo`, `DoiNguongNhanDangLop_KetQuaDoiTheo`, `DoiNguongCongMaTrong_KetQuaDoiTheo`. Số nào bị chôn cứng trong code thì 4 test này đỏ.
- [x] Cùng `seed` và cùng input cho ra `ShotIntent` giống hệt — `CungSeedCungInput_ChoShotIntentGiongHet`; `SeedKhac_ChoTanMatKhac_NhungKhongDoiTocDoLoaiVaXoay` khoá thêm một điều quan trọng: seed CHỈ được đụng vào tản mát, không được đụng tốc độ/loại/xoáy. `Seed0_VanChayDuoc` (dùng `Random.CreateFromIndex` nên seed 0 không làm hỏng bộ sinh).
- [x] Sai số thời điểm làm *lệch* cú sút chứ không làm hỏng hoàn toàn — kiểm bằng biểu đồ phân tán 200 cú — `PhanTan200Cu_LamLechChuKhongLamHong` chạy đúng 200 seed và kiểm cả hai chiều: có tản mát thật (không phải mọi cú đều trúng tâm) NHƯNG không cú nào bay ra ngoài vùng chấp nhận được. Kèm `SaiSoCangLon_TanMatCangRong_DonDieu` và `SaiSoVuotTran_QualityKepVeKhong_KhongAm`.
- [x] Vuốt hết biên độ cho tốc độ đúng `maxSpeed`, không vượt — `VuotHetBienDo_ChoDungMaxSpeed_KhongVuot`; và `DuongCongVongLenTrenMot_VanKhongLamTocDoVuotMaxSpeed` bịt lỗ thật: `AnimationCurve` vọt trên 1 khi ai đó kéo tiếp tuyến trong Inspector, không kẹp đầu ra thì lời hứa "không bao giờ vượt maxSpeed" vỡ vì một thao tác kéo chuột. `VuotCangDai_TocDoCangLon_KhongGiamNguoc` chặn đường cong bị kéo ngược.
- [x] 4 `ShotType` đều đạt tới được bằng cử chỉ, không cần nút bấm — `BonLoaiSut_DeuDatToiDuocBangCuChi_KhongCanNutBam` dựng 4 cử chỉ và nhận đúng 4 loại. Hai test chặn nhầm lẫn: `GiatNganMaCham_KhongPhaiLop_MaLaCuSutNhe` (vuốt ngắn mà chậm không được thành lốp) và `VuotHinhChuS_KhongBiNhamThanhKnuckle` (hai bướu ngược chiều triệt tiêu nhau cho độ cong ≈ 0, nhưng đó rõ ràng không phải vuốt thẳng — đây là lý do tồn tại của `straightnessSmooth`).
- [x] Cú `Knuckle` đặt `spin` gần 0 và bật cờ bất ổn định riêng, **không** giả lập bằng cách gán xoáy ngẫu nhiên — `Knuckle_XoayDungBangKhong_VaBatCoBatOnDinh` (xoáy đúng bằng 0, không phải "gần 0"), `Knuckle_KhongPhaiGiaLapBangXoayNgauNhien` (nhiều seed khác nhau vẫn cho xoáy 0 — nếu ai đó lén gán xoáy ngẫu nhiên thì test này đỏ), `CacLoaiKhac_KhongBatCoBatOnDinh`. Cờ `unstable` được T15 đọc. Kèm `VuotThang_XoayDungBangKhong_KhongPhaiSoHatTieu` và `XoayKhongBaoGioVuotMaxSpinRadPerSec`.

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
    public float envelopeRiseSeconds;   // THÊM — xem ghi chú (2) bên dưới
    public static KnuckleConfig Default { get; }   // THÊM
  }

  [BurstCompile] public static class KnuckleForce {
    public static float3 Evaluate(in BallState s, in KnuckleConfig c,
                                  float elapsed, uint seed);   // trả GIA TỐC m/s², xem (1)
  }

  // ── PHẦN "CỬA SỔ THỜI ĐIỂM" — không có trong hợp đồng gốc, xem ghi chú (3) ──
  public enum TimingGrade { Perfect, Good, Poor }

  [Serializable] public struct TimingWindowConfig {
    public float perfectHalfWidthSeconds, goodHalfWidthSeconds, maxErrorSeconds;
    public static TimingWindowConfig Default { get; }
  }

  public struct TimingResult {
    public float errorSeconds;        // sự thật, cho HUD debug
    public float mappedErrorSeconds;  // đã tha thứ + kẹp, cho ShotMapper
    public TimingGrade grade;
    public float ErrorMilliseconds { get; }
    public bool  IsEarly { get; }
  }

  public static class TimingWindow {
    public static TimingResult Evaluate(float releaseTime, float idealTime,
                                        in TimingWindowConfig cfg);
    public static TimingWindowConfig Sanitize(in TimingWindowConfig cfg);
    public static void   AppendDebug(StringBuilder sb, in TimingResult r);  // 0 cấp phát
    public static string Describe(in TimingResult r);                       // có cấp phát
  }
}
```

**Ba chỗ đi khác hợp đồng gốc, và lý do:**

1. **`Evaluate` trả GIA TỐC (m/s²), không phải lực (Newton).** Bắt buộc: `BallState` không mang
   khối lượng, và `BallSolver.Acceleration` cộng dồn gia tốc chứ không cộng lực. Trả Newton thì
   người gọi phải tự đi tìm khối lượng ở chỗ khác — đúng kiểu sai đơn vị mà không ai phát hiện.
2. **Thêm `envelopeRiseSeconds`.** Bản đầu tiên (agent 9router viết) suy bao hình từ
   `frequencyHz` (`β = f·π`). Cột hai thứ đó vào nhau làm hỏng cả hai: hạ tần số cho bóng đỡ rung
   thì bao hình chậm theo, và ở 1.1 Hz cả pha bay 0.42 s không kịp mở — hiệu ứng biến mất trong
   im lặng mà **không test nào bắt được**, vì mọi test lúc đó đều là test chặn TRÊN.
   Đã tách thành tham số riêng và bổ sung test chặn DƯỚI (xem ô cuối).
3. **`TimingWindow` là kiểu mới hoàn toàn.** Ô nghiệm thu cuối đòi "cửa sổ thời điểm hiển thị được
   bằng số ms trong chế độ debug", nhưng hợp đồng gốc không có chỗ nào định nghĩa cửa sổ đó, cũng
   không có ai tính ra `timingError` mà `ShotMapper.Map` đang nhận. Đây là mảnh còn thiếu giữa hai
   task, không phải tính năng thêm thắt.

**Checklist nghiệm thu** — **18 test [KnuckleForceTests.cs](../../Assets/_Project/Tests/EditMode/KnuckleForceTests.cs) + 21 test [TimingWindowTests.cs](../../Assets/_Project/Tests/EditMode/TimingWindowTests.cs), TẤT CẢ XANH 2026-08-26 (chạy sống)**

- [x] `KnuckleForce` nằm ở assembly `Shooter`, **không** nằm trong `BallSolver` — [KnuckleForce.cs](../../Assets/_Project/Code/Shooter/KnuckleForce.cs) nằm trong `Eleven.Shooter`, `Eleven.Ball` không tham chiếu ngược lại (chiều phụ thuộc là Shooter → Ball). File solver không có một chữ "knuckle" nào: mô hình vật lý và bổ sung gameplay tách hẳn, đúng như đầu mục T15 yêu cầu.
- [x] Độ lệch ngang tổng cộng không bao giờ vượt `maxLateralDeviation` — test 500 seed ngẫu nhiên — `Knuckle_500Seed_DoLechNgangKhongBaoGioVuotMaxLateralDeviation` tích phân THẬT gia tốc qua pha bay (dt = 1/120) và kiểm **hai mức**: đúng chữ hợp đồng (≤ `maxLateralDeviation`, không dung sai) và mức chặt hơn là chặn toán học thật `min(amplitude, maxLateralDeviation)` với 1% dung sai tích phân. Mức thứ hai mới là mức bắt được lỗi công thức — mức thứ nhất còn dư 37% khoảng trống nên một công thức sai vẫn lọt qua. Ràng buộc là hệ quả toán học chứ không phải một câu `clamp`: gia tốc trả về là `d''(t)` của một hàm độ lệch `d(t) = A·E(t)·O(t)` đã chứng minh `|d| ≤ A` (chứng minh đầy đủ trong doc comment của `KnuckleForce`). **Điều kiện của chứng minh** — nó chỉ đúng khi hiệu ứng chạy liên tục từ t=0, nên cửa `onsetSpeed` không được đóng giữa pha bay; điều kiện đó được khoá lại bằng số thật của cả hai config trong `Knuckle_CuaTocDo_KhongTheDongGiuaPhaBay_VoiCauHinhMacDinh` (cú knuckle chậm nhất rời chân 26.4 m/s, còn 23.4 m/s lúc tới khung thành ở 0.42 s và 22.5 m/s ở mốc 0.6 s mà test dùng cho dư, so với onsetSpeed 18 m/s).
- [x] Cùng seed cho cùng đường bay, byte giống byte — `Knuckle_CungSeed_KetQuaGiongTungBit` so bằng `math.asuint` trên cả ba trục, 20 seed × 60 mốc thời gian, **không dùng dung sai**. `Knuckle_SeedKhac_DuongBayKhacThatSu` chặn chiều ngược lại (khác seed phải khác thật, không phải khác vài hạt tiêu).
- [x] Dưới `onsetSpeed` lực bằng đúng 0 — `Knuckle_DuoiNguongTocDo_LucBangKhong` (17.9 m/s so với ngưỡng 18), `Knuckle_NgayTrenNguong_LucKhacKhong` chặn chiều ngược lại.
- [x] Xoáy khác 0 thì knuckle tắt — hai hiệu ứng loại trừ nhau — `Knuckle_XoayKhacKhong_LucBangKhong` và `Knuckle_XoayCucNhoKhacKhong_VanTat` (xoáy 1e-6 rad/s vẫn tắt: điều kiện là `lengthsq(spin) > 0`, không có vùng xám). Lý do vật lý ghi trong code: knuckle bất ổn vì bóng KHÔNG xoáy nên điểm tách dòng khí nhảy quanh; có xoáy thì dòng tách ổn định theo Magnus.
- [x] Cửa sổ thời điểm hiển thị được bằng số ms trong chế độ debug — [TimingWindow.cs](../../Assets/_Project/Code/Shooter/TimingWindow.cs). `AppendDebug` viết `"+42 ms Perfect"` vào `StringBuilder` có sẵn và **cấp phát 0 byte** (`HienThiDebug_KhongCapPhat`, `Is.Not.AllocatingGCMemory()`) nên gọi được mỗi khung hình mà không đẻ rác — đúng quy ước GC của `PerfHud`. Tự ghép chữ số thay vì `ToString()` để khỏi phụ thuộc locale và cài đặt runtime. Bốn test khoá cách hiển thị: đúng số và đúng dấu, làm tròn tới ms (không hiện nhiễu phần lẻ), không bao giờ ra `"-0 ms"`, và bộ đệm null không ném lỗi (HUD debug hỏng không được kéo theo cả trận đấu).

**Ba ô KHÔNG có trong hợp đồng nhưng đã bổ sung, vì thiếu chúng thì 6 ô trên vẫn xanh mà tính năng vẫn hỏng:**

- [x] **Hiệu ứng phải thật sự có tác dụng** — `Knuckle_TrongPhaBayThat_DoLechDuLonDeCoNghiaGameplay`. Cả 6 ô trên đều là ràng buộc chặn TRÊN: một cài đặt trả `float3.zero` vĩnh viễn qua sạch tất cả. Test này chặn DƯỚI, trên đúng pha bay thật 0.42 s (số đo được từ video eFootball, T12): độ lệch trung bình của 500 seed > 5 cm, seed mạnh nhất > 11 cm, và > 100/500 seed vượt 8.8 cm. Chính test này bắt được lỗi bao hình mô tả ở ghi chú (2).
- [x] **Sai số thời điểm nối được từ đầu tới cuối** — `NoiVoiShotMapper_BamHoanHao_KhongTanMat` và `NoiVoiShotMapper_BamTe_TanMatRongHon_NhungVanRaCuSut` chạy thẳng `TimingWindow → ShotMapper`. Cái đầu chứng minh vùng chết ±50 ms có tác dụng thật: bấm lệch 30 ms vẫn cho `quality = 1` và tản mát **đúng bằng 0**. Không có vùng chết thì nhánh `scatter = 0` vĩnh viễn không chạy tới và cú sút hoàn hảo vẫn lệch vài chục cm — người chơi không bao giờ được thưởng cho việc bấm chuẩn.
- [x] **Không NaN ở mọi biên** — `Knuckle_Bien_VanTocKhong`, `Knuckle_Bien_ConfigToanKhong`, `Knuckle_Bien_ElapsedRatLon`, `Knuckle_Bien_VanTocGanSongSongTrucY` (trục lệch suy biến → đổi trục phụ), `Knuckle_ElapsedAmHoacNaN_LucBangKhong`, `Knuckle_LucLuonVuongGocVoiVanToc`, `Knuckle_KhongCapPhat`; phía cửa sổ thời điểm: `ThoiDiemVoCuc_HoacNaN_KhongLamHongKetQua`, `ConfigSaiThuTu_DuocSapLai_KhongImLangSai`, `ChamThoiDiem_KhongCapPhat`.

**Còn nợ ở T15 (không phải việc code, ghi ra để không quên):**
- Nối `KnuckleForce` vào khâu thực thi cú sút: `BallDriver` hiện chưa cộng gia tốc knuckle vào mỗi bước sim, vì cờ `ShotIntent.unstable` mới chỉ được đặt chứ chưa có ai đọc. Việc nối nằm ở **T16** (khâu thực thi cú sút), đúng theo thứ tự phụ thuộc của backlog — T15 chỉ có nhiệm vụ cung cấp hàm thuần.
- `TimingWindowConfig` mới chỉ có `Default` dựng bằng code; chưa có asset `ScriptableObject` để chỉnh trên máy thật. Cùng lý do như trên: chưa có UI cửa sổ thời điểm để chỉnh cho ai xem.
- Ba con số cửa sổ (±50 / ±120 / trần 200 ms) là **giả thiết thiết kế, chưa qua user test**. Đúng tinh thần đầu trang: "làm cho nó chỉnh được, đừng làm cho nó hoàn hảo".

---

← [Phase 1: Vật lý bóng](phase-1-vat-ly-bong.md) · [Mục lục](README.md) · [Phase 3: Thủ môn](phase-3-thu-mon.md) →

> Trước khi giao việc, đọc [quy tắc giao việc](README.md#quy-tắc-giao-việc) và
> dùng [mẫu prompt](README.md#mẫu-prompt-giao-việc). Đừng gộp nhiều task vào một phiên.
