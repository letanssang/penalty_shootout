← [Phase 6: Kiểm chứng tự động](phase-6-kiem-chung.md) · [Mục lục](README.md) · [Phase 8: Vòng lặp game và chế độ chơi](phase-8-vong-lap-game.md) →

---

# PHASE 7 — Hoạt ảnh và IK

**6 task · tuần 11–14 · đã xong 1/6 (T35)**

Đây là phần code được của mốc **M3**. Phase này thay hai hình nhân bóng xám bằng nhân vật
thật, và đó là chỗ dự án dễ tự bắn vào chân nhất: chỉ cần để animation chạm vào vật lý một
lần, mọi thứ Phase 1–4 xây được sẽ mất tính tất định, mất tính công bằng, và mất khả năng
cân bằng độ khó.

> **Luật chi phối cả phase, chép từ [plan.md](../plan.md) mục 05:**
> Gameplay tính **vector phóng có thẩm quyền** tại khung hình chạm bóng. Animation được chọn
> theo loại cú sút, rồi **IK bẻ cẳng chân và bàn chân** để giày gặp đúng vị trí thật của bóng.
> **Animation không bao giờ được điều khiển vật lý.**

Hai ràng buộc nữa, phát sinh từ những gì đã dựng ở Phase 3 và Phase 5 — bỏ qua chúng là làm
hỏng thứ đang chạy:

1. **Xương người sút là dữ liệu gameplay, không phải đồ trang trí.** `KickerBoneCueSource`
   đọc `root`, `plantFoot`, `hips` mỗi khung hình và đó là toàn bộ nguồn tín hiệu để thủ môn
   đọc vị (T17→T18). Đổi bộ xương mà không đọc lại mục "Hiệu chuẩn tín hiệu" trong T37 thì
   thủ môn sẽ đọc ra nhiễu và quay về đứng giữa mọi quả — đúng cái lỗi đã mất một buổi để
   tìm ra ngày 2026-08-27.
2. **Thấy sao thì tính vậy.** `GoalkeeperView` hiện vẽ đường bay người bằng ĐÚNG công thức
   `KeeperReach.ReachProgress` mà `SaveResolver` dùng để phán kết quả. Khi thay bằng clip
   hoạt ảnh thật, bất biến đó phải được giữ hoặc được thay bằng một bất biến tương đương và
   có test. Mắt thấy tay chạm bóng mà máy báo thủng lưới là cách nhanh nhất để bị gọi là ăn gian.

**Gói đã có sẵn trong `Packages/manifest.json`:** `com.unity.animation.rigging` 1.4.1,
`com.unity.cinemachine` 3.1.4, `com.unity.timeline` 1.8.12. Không cần thêm gói mới.

**Việc KHÔNG giao cho agent trong phase này:** dựng model nhân vật, gán trọng số da, làm
sạch mocap, và quyết định "chuyển động đã thật chưa". Agent làm bộ khung điều khiển, bộ giải
IK, và **công cụ đo** — chính công cụ đo (T39) mới là thứ biến câu hỏi thẩm mỹ thành câu hỏi
trả lời được.

---

## T35 — Máy trạng thái hoạt ảnh người sút ✅ XONG (2026-08-28)

**Phụ thuộc:** T23 · **Ước lượng:** ~2 ngày · **Thực tế:** 1 ngày

`KickerAvatar` lái từng khớp bằng hàm lượng giác theo `t01` — đủ cho bản demo bóng xám,
không đủ cho nhân vật thật. Task này dựng lớp điều khiển đứng giữa `KickPhase` và `Animator`,
và giữ nguyên hợp đồng transform mà `KickerBoneCueSource` đang đọc.

Điểm quan trọng: lớp này **nhận** loại cú sút, **không** quyết định nó. `ShotType` do
`ShotMapper` (T14) sinh ra từ cử chỉ; animation chỉ chọn clip tương ứng. Làm ngược lại là
để animation quyết định gameplay.

### Đã dựng những gì

| File | Vai trò |
|---|---|
| `Code/Presentation/Kicker/KickerClip.cs` | 9 giá trị enum, tên **trùng từng ký tự** với tên state trong controller |
| `Code/Presentation/Kicker/IKickerAnimator.cs` | Hợp đồng; cả model thật lẫn greybox đều hiện thực |
| `Code/Presentation/Kicker/KickerClipSelector.cs` | Struct thuần, không tham chiếu UnityEngine — nơi chứa toàn bộ logic chọn clip |
| `Code/Presentation/Kicker/MecanimKickerAnimator.cs` | Bám vào `Animator` của X Bot, `CrossFadeInFixedTime` theo hash |
| `Code/Presentation/Kicker/KickerPlacement.cs` | Một nguồn sự thật cho vị trí xuất phát/trụ, để greybox và model không lệch nhau |
| `Editor/Art/KickerAnimatorControllerBuilder.cs` | Sinh `KickerAnimator.controller`: 9 state, 0 transition, 0 parameter |
| `Editor/Art/StrikeContactProbe.cs` → `docs/data/strike-contact.tsv` | Đo khung chạm bóng bằng máy, không đoán bằng mắt |
| `Tests/EditMode/KickerAnimatorTests.cs` | 12 test phủ hết checklist bên dưới |

### Hợp đồng thực tế (khác bản phác ban đầu)

```csharp
namespace Eleven.Presentation.Kicker {
  public enum KickerClip : byte { Idle, RunUp, StrikeInstep, StrikeInsideFoot,
                                  StrikeChip, StrikeKnuckle, FollowThrough, Celebrate, Dejected }

  public interface IKickerAnimator {
    KickerClip CurrentClip { get; }
    float NormalizedTime { get; }
    float ContactNormalizedTime { get; }

    void PrepareFor(ShotType type);
    void SetOutcome(KickResult result);                            // BỔ SUNG
    void OnPhaseChanged(KickPhase oldPhase, KickPhase newPhase);
    void Tick(float dt, float phaseProgress01);
    void SetAimYawDegrees(float yawDegrees);                       // BỔ SUNG
    void ResetToStart(Unity.Mathematics.float3 ballPosition);      // BỔ SUNG
    void SetRunUpDuration(float seconds);                          // BỔ SUNG

    Transform Root { get; } Transform Hips { get; }
    Transform PlantFoot { get; } Transform KickFoot { get; }
  }
}
```

Bốn thành viên bổ sung, và lý do từng cái — không phải tiện tay thêm:

- **`SetOutcome`** — thiếu nó thì `Celebrate` và `Dejected` là hai state không đường tới. Bản
  phác có clip ăn mừng nhưng không có kênh nào báo kết quả vào lớp hoạt ảnh.
- **`SetRunUpDuration`** — scene `Match` ghi đè `runUp = 1.30s` trong khi mặc định là `0.90s`.
  Hardcode thời lượng vào animator là để hai nguồn sự thật cùng tồn tại rồi lệch nhau.
- **`ResetToStart` / `SetAimYawDegrees`** — `KickerAvatar` đã có sẵn hai hàm này và
  `MatchGameLoop` đang gọi. Đưa vào hợp đồng để hai bản hiện thực thay được cho nhau, chứ
  không phải để `MatchGameLoop` rẽ nhánh theo kiểu cụ thể. `SetAimYawDegrees` ở bản Mecanim
  hiện là chỗ đặt sẵn (chưa xoay thân theo hướng ngắm) — xem T37.

### Quyết định thiết kế đáng ghi

**Clip sút phải bắt đầu TRONG pha RunUp, không phải ở pha Contact.** Khung chạm bóng nằm
giữa clip (`PenaltyKick` chạm ở 48.9% của 1.500s = 0.733s). Nếu đợi tới pha `Contact` mới
phát thì bóng đã bay được 0.733s trước khi chân chạm tới nó. Nên `MecanimKickerAnimator`
tính `lead = contactNorm × length` rồi đổi clip khi `runUpSecondsRemaining <= lead`.
Kiểm tra vừa: `0.733s ≤ 0.90s` (mặc định) và `≤ 1.30s` (scene Match).

**`KickerClipSelector.Resolve` là hàm toàn phần trên cả 8 `KickPhase`.** Không có nhánh nào
trả về "giữ nguyên clip cũ". Đó là cách checklist "huỷ giữa chừng không kẹt tư thế" được bảo
đảm bằng cấu trúc thay vì bằng một danh sách dọn dẹp mà ai đó phải nhớ cập nhật.

**Chốt `_strikeLocked` khi clip sút đã bắt đầu.** Cú vung chân đang giữa chừng thì ngoài đời
cũng không đổi kiểu sút được nữa.

**Lượt người chơi giờ cũng ra đúng 4 clip, bằng cách phân loại TẠM cú vuốt đang dở
(2026-08-28).** `ShotType` chính thức chỉ ngã ngũ lúc nhả ngón (`BuildIntent`), nhưng lúc đó
clip sút đã phải chạy được vài phần mười giây rồi (xem lead-time ở trên). Chuỗi
`SwipeCollector.TryPeek` → `TouchSwipeReceiver.TryPeekShotType` → `MatchGameLoop.PeekPlayerShotType`
đọc đặc trưng cử chỉ đang diễn ra mỗi khung hình trong pha `RunUp` và gọi `PrepareFor` với kết
quả tạm, dùng THẲNG `ShotMapper.Classify`/`SpeedT` — không có luật riêng cho bản đọc tạm, để
hai đường phân loại không lệch nhau theo thời gian. Đoán sai chỉ phát nhầm clip; vector phóng
vẫn tính từ cử chỉ thật lúc nhả ngón nên bóng không bao giờ sai theo animation.

Giới hạn đã biết, đo bằng số: một cú vuốt dài đang ở 20–40% chặng đường đọc ra giống hệt một
cú lốp (`SwipeProvisionalTypeTests.DocTam_CuVuotDaiDangVeDoDang_CoTheDocNhamThanhLop`). Đã thử
chặn bằng ngưỡng thời lượng và bỏ: luật chơi cho phép giữ ngón sau khi vẽ xong cử chỉ, nên một
cú lốp thật hoàn toàn có thể bị giữ lâu trước khi nhả — chặn theo thời lượng sẽ cướp mất hoạt
ảnh lốp của đúng người vuốt lốp. Chấp nhận được vì lối chơi thật là vẽ nhanh rồi giữ, và có
test riêng xác nhận bản đọc tạm hội tụ đúng kết quả trong tình huống đó.

### Nợ còn lại

| Nợ | Loại | Đổi ra thế nào |
|---|---|---|
| `StrikeKnuckle` dùng lại clip `PenaltyKick` của `StrikeInstep` | **Nghệ thuật**, không phải mã | Pack Soccer Game chỉ có 3 clip sút thật. Có clip knuckle thì sửa 1 dòng ánh xạ trong `KickerAnimatorControllerBuilder` + 2 hằng số `KnuckleLength/KnuckleContact` |
| `FollowThrough` mượn `OffensiveIdle` | Nghệ thuật | Không có clip theo đà riêng. Không được dùng `PenaltyKick` — sẽ chạy đà lần hai lúc bóng đang bay |

**Checklist nghiệm thu**
- [x] Bốn `ShotType` cho ra bốn clip khác nhau, cả lượt người chơi lẫn AI — `BonKieuSut_ChoBonClipKhacNhau`, `SwipeProvisionalTypeTests`
- [x] `ContactNormalizedTime` khớp khung chạm bóng thật, sai số dưới 1 khung ở 60fps — `KhungCham_KhopSoDoTrongStrikeContactTsv`, đối chiếu `docs/data/strike-contact.tsv`
- [x] Không dòng nào ghi vào `BallDriver`/`BallState`/`ShotIntent` — `LopHoatAnh_KhongThamChieuKieuVatLy` quét reflection cả namespace
- [x] `Root`/`Hips`/`PlantFoot`/`KickFoot` không null sau khởi tạo; `KickerBoneCueSource` không phải sửa dòng nào
- [x] Đổi pha giữa chừng không kẹt tư thế — `MoiPha_ChoRaDungMotClip_KhongPhaNaoKet` + `HuyGiuaChung_VeIdle_KhongGiuKetQuaLuotTruoc`
- [x] Không cấp phát trong `Tick` — `Tick_KhongCapPhatGC`, `Resolve_KhongCapPhatGC`
- [ ] **Chưa đo:** chi phí CPU dưới 0.15 ms trên máy thật. Cần thiết bị + T58

**Trạng thái:** scene `Match.unity` đã dựng bằng X Bot thật (`MecanimKickerAnimator`, 0 tham
chiếu `KickerAvatar`). Toàn bộ EditMode sau khi thêm phân loại tạm cho lượt người chơi
(2026-08-28): 554 test, 553 pass, 0 fail thật, 1 skip có sẵn từ trước.
`BurstCompatibilityTests.BallSolverStep_BurstBienDichDuoc_VaCungKetQuaVoiManaged` timeout
180s ở lần chạy đầu (Burst compile nguội) — chạy lại riêng fixture đó (Burst cache ấm) thì
pass ở 38s. Không liên quan tới thay đổi của task này (không đụng job Burst nào).

---

## T36 — IK chân sút gặp bóng

**Phụ thuộc:** T35, T09 · **Ước lượng:** ~3 ngày

Đây là task quyết định M3 đạt hay trượt. Bóng nằm ở đúng một chỗ trong không gian; clip
hoạt ảnh thì được thu ở một chỗ khác. IK có nhiệm vụ kéo cẳng chân và bàn chân để hai chỗ
đó gặp nhau tại đúng khung hình chạm, mà không làm gãy phần còn lại của tư thế.

Ràng buộc cứng: IK **không được** đổi thời điểm chạm bóng, và **không được** đổi vector
phóng. Vector phóng đã được tính trước bởi tầng gameplay; giày chỉ việc có mặt đúng chỗ.

```csharp
namespace Eleven.Presentation.Kicker {
  public struct FootIkTarget {
    public float3 position;      // vị trí giày mong muốn tại khung chạm, không gian thế giới
    public quaternion rotation;
    public float weight;         // 0..1, tăng dần khi tới gần khung chạm
  }

  public static class FootStrikeSolver {
    /// <summary>Điểm mà giày phải tới để chạm bóng đúng vector phóng đã tính.</summary>
    public static FootIkTarget SolveContact(float3 ballCenter, float ballRadius,
                                            float3 launchVelocity, ShotType type);

    /// <summary>Trọng số IK theo thời gian chuẩn hoá của clip — 0 ở đầu, 1 tại khung chạm.</summary>
    public static float WeightCurve(float normalizedTime, float contactNormalizedTime);
  }
}
```

**Checklist nghiệm thu**
- [ ] Sai số tâm giày ↔ điểm chạm mong muốn tại khung chạm **dưới 2 cm** ở cả 4 `ShotType` — đo bằng T39, không bằng mắt
- [ ] Vector phóng sau khi bật IK giống hệt vector trước khi bật, từng bit — test so sánh `BallState` khởi tạo
- [ ] Trọng số IK về 0 trước khi vào pha `RunUp` và sau khi ra khỏi `FollowThrough` — không có tư thế dính IK
- [ ] Không thấy gối bẻ ngược hoặc bàn chân xoắn ở bất kỳ điểm ngắm nào trong 9 ô — quét toàn bộ lưới ô bằng test tự động, kiểm giới hạn góc khớp
- [ ] Chi phí IK dưới **0.3 ms** trên máy bậc B (ngân sách, chưa đo) — đo bằng `PerfHud`
- [ ] Cùng seed cho cùng tư thế từng bit giữa Editor và build IL2CPP

---

## T37 — Đặt chân trụ và hiệu chuẩn lại tín hiệu đọc vị

**Phụ thuộc:** T36, T17 · **Ước lượng:** ~2 ngày · `TẤT ĐỊNH`

Chân trụ là **cái tell** của một quả 11m. Ở bản bóng xám, tín hiệu đó được tạo bằng cách dạt
ngang cả người sút theo hướng ngắm, rồi đo lệch so với đường chạy đà trung tính — hiệu chuẩn
lại mốc 0 mỗi khung hình. Khi có hoạt ảnh thật, chân trụ đặt ở đâu là do clip và IK quyết
định, nên phép hiệu chuẩn đó phải được làm lại từ đầu.

Đây là chỗ đã trả giá một lần rồi, ghi lại để không trả lần thứ hai: nếu mốc 0 sai, tín hiệu
sẽ mang một **thiên lệch hằng số**, `BayesianKeeperBrain` nhận vào một phân phối phẳng, độ tin
cậy tụt xuống ~0.06, và thủ môn đứng giữa ở **100%** số quả — mà không một test đơn lẻ nào
của Phase 3 đỏ, vì mỗi lớp vẫn đúng khi đứng riêng.

```csharp
namespace Eleven.Presentation.Kicker {
  /// <summary>
  /// Mốc 0 của tín hiệu chân trụ: chân trụ SẼ ở đâu tại khoảnh khắc này nếu người sút
  /// ngắm thẳng giữa. Tín hiệu mà thủ môn đọc là ĐỘ LỆCH so với mốc này.
  /// </summary>
  public interface IPlantFootBaseline {
    float3 NeutralPlantPosition(float runUpProgress01);
    float LateralDeviation(float3 actualPlantPosition, float runUpProgress01);
  }
}
```

**Checklist nghiệm thu**
- [ ] Ngắm thẳng giữa: `LateralDeviation` nằm trong **±2 cm** ở mọi điểm của đà chạy — không có thiên lệch hằng số
- [ ] Ngắm sát cột trái và sát cột phải cho hai độ lệch **trái dấu nhau**, độ lớn nằm trong dải mà bảng tín hiệu của T18 mong đợi (±0.20 m)
- [ ] Chạy `KeeperReadsShotTests` (đã có): tỉ lệ thủ môn đọc đúng cột phải **cao hơn 45%** ở bậc Thường; đoán mò là 33%
- [ ] Chạy `DifficultyTests` (đã có): tỉ lệ bị ép đứng giữa ở bậc Khó **dưới 35%** số quả trúng khung
- [ ] Thứ tự độ khó giữ nguyên: Dễ < Thường < Khó ở cả tỉ lệ đọc đúng lẫn tỉ lệ cản phá
- [ ] Cùng seed cho cùng chuỗi tín hiệu từng bit; không dùng `UnityEngine.Random` ở bất kỳ đâu

---

## T38 — Hoạt ảnh và IK thủ môn

**Phụ thuộc:** T35, T19, T21 · **Ước lượng:** ~3 ngày

Thủ môn có hai việc phải làm cùng lúc: trông như đang bay người thật, và có mặt ở đúng chỗ
mà `SaveResolver` đã tính. Hai việc đó dễ tách đôi nếu không cẩn thận.

Bất biến phải giữ: vị trí bàn tay hiển thị tại khoảnh khắc bóng qua vạch vôi phải trùng với
`KeeperReach.HandPositionAt(cell, arrivalTime, profile)` trong sai số cho phép. Nếu clip
hoạt ảnh không thể đạt tư thế đó, **clip phải nhường**, không phải kết quả nhường.

```csharp
namespace Eleven.Presentation.Keeper {
  public enum KeeperClip { Set, StepLeft, StepRight, DiveLowLeft, DiveLowRight,
                           DiveHighLeft, DiveHighRight, StandSave, Recover, Celebrate }

  public interface IKeeperAnimator {
    KeeperClip CurrentClip { get; }

    /// <summary>Chọn clip theo quyết định đã CHỐT của T19. Không tự chọn hướng.</summary>
    void PlayDive(in DiveDecision decision, float ballArrivalTime, KeeperProfile profile);

    /// <summary>Vị trí bàn tay hiển thị tại thời điểm t giây sau khi chạm bóng.</summary>
    float3 HandPositionAt(float timeSinceContact);

    void Reset();
  }
}
```

**Checklist nghiệm thu**
- [ ] Sai lệch giữa `IKeeperAnimator.HandPositionAt` và `KeeperReach.HandPositionAt` **dưới 5 cm** tại khoảnh khắc bóng qua vạch vôi, quét cả 9 ô và cả 3 hồ sơ độ khó
- [ ] Có test tự động khẳng định: mọi pha `SaveResult.Caught` hoặc `Parried` đều có bàn tay hiển thị nằm trong bán kính chạm bóng của `KeeperReach.CatchRadius`
- [ ] Thủ môn không bao giờ đổi hướng sau khi `DiveDecision` đã chốt — test quét toàn bộ dải `commitTime`
- [ ] Clip bay người bên trái và bên phải đối xứng nhau về thời gian, chênh dưới 1 khung ở 60fps
- [ ] Không có pha trượt chân trên mặt cỏ ở tốc độ phát 0.25× — đo bằng T39
- [ ] Chi phí hoạt ảnh + IK thủ môn dưới **0.3 ms** trên máy bậc B (ngân sách, chưa đo)

---

## T39 — Bộ đo chất lượng hoạt ảnh

**Phụ thuộc:** T36, T38 · **Ước lượng:** ~2 ngày

Đây là task biến bốn tiêu chí M3 trong [plan.md](../plan.md) từ câu chữ thành con số. Không
có nó, "đã thật chưa" mãi là tranh cãi; có nó, mỗi lần sửa clip đều biết mình làm tốt lên hay
tệ đi bao nhiêu centimet.

Chạy trong Editor trên một tập tư thế cố định, xuất CSV, so được giữa hai lần chạy — cùng
khuôn với `BenchmarkRunner` (T33) đã có, đừng dựng công cụ đo thứ hai.

```csharp
namespace Eleven.Editor.Animation {
  public struct AnimationQualityReport {
    public float contactErrorCm;         // sai số giày ↔ bóng tại khung chạm
    public float plantFootErrorCm;        // sai số chân trụ so với mốc
    public float maxFootSlideCmPerFrame;  // trượt chân lớn nhất khi chân đang chạm đất
    public float maxJointJerk;            // đạo hàm bậc ba lớn nhất của vị trí khớp
    public int   sampleCount;
    public string ToCsv();
  }

  public static class AnimationQualityMeter {
    public static AnimationQualityReport Measure(GameObject rig, KickerClip clip, int sampleRate);
    public static bool CompareWithBaseline(in AnimationQualityReport current,
                                           in AnimationQualityReport baseline,
                                           out string report);
  }
}
```

**Checklist nghiệm thu**
- [ ] Đo được sai số điểm chạm giày–bóng theo **centimet**, cho cả 4 `ShotType`
- [ ] Đo được sai số vị trí chân trụ theo **centimet**
- [ ] Phát hiện được trượt chân: bàn chân đang chạm đất mà dịch ngang quá **0.5 cm/khung** thì báo đỏ
- [ ] Đo được độ liên tục vận tốc hông/gối/khuỷu — báo đỏ khi có bước nhảy bậc
- [ ] Xuất CSV có gắn git commit hash, so được với lần chạy trước
- [ ] Chạy được bằng một lệnh từ dòng lệnh, không cần thao tác tay trong Editor
- [ ] Bộ đo tự nó có test: đưa vào một clip cố ý trượt chân thì phải bắt được

---

## T40 — Gắn shader da tán xạ vào nhân vật thật

**Phụ thuộc:** T31, T35 · **Ước lượng:** ~2 ngày

Trả nợ đã đo ngày 2026-08-27: `SkinSssLut` đã sinh xong bảng tra 128×32 và có test, nhưng
chưa gắn được vào đâu vì chưa có mesh người. Đây là một trong bốn lời hứa hình ảnh ở
[plan.md](../plan.md) mục 02, nên nó không được phép ở mãi trạng thái "đã tính xong nhưng
chưa thấy".

Phần code: đường ống vật liệu, biến thể shader theo bậc thiết bị, và đường lui khi bậc C tắt
SSS. Phần chọn thông số da cho từng nhân vật là việc của mắt, không giao được.

```csharp
namespace Eleven.Presentation.Skin {
  public interface ISkinMaterialBinder {
    /// <summary>Gắn LUT và tham số tán xạ vào toàn bộ renderer của một nhân vật.</summary>
    void Bind(GameObject character, in SkinSssSettings settings, QualityTier tier);

    /// <summary>Đổi bậc lúc đang chạy mà không dựng lại vật liệu.</summary>
    void ApplyTier(QualityTier tier);

    bool IsSssActive { get; }
  }
}
```

**Checklist nghiệm thu**
- [ ] Bậc A và B bật SSS, bậc C tắt hoàn toàn và rơi về shader Lit thường — kiểm bằng test đọc `IsSssActive`
- [ ] Đổi bậc lúc đang chạy không sinh vật liệu mới — 0 byte GC, đo bằng test
- [ ] LUT được nạp đúng một lần cho cả hai nhân vật, không nhân đôi bộ nhớ texture
- [ ] Chi phí SSS dưới **0.5 ms** — đúng ngân sách ở [plan.md](../plan.md) mục 04, đo trên máy thật bằng `PerfHud`
- [ ] Bộ nhớ texture thường trú sau khi gắn vẫn nằm trong ngân sách của bậc (400/250/140 MB)
- [ ] Ảnh chụp so sánh bật/tắt SSS ở cùng một khung hình, cùng ánh sáng — **phần đánh giá đẹp hay chưa là việc của bạn, không giao được**

---

## Thứ tự làm và chỗ song song được

```
T35 (đã xong) ──┬── T36 ── T37
      │            
      └── T38      
           │       
      T36+T38 ── T39
           
T31 (đã xong) ── T40
```

- ~~**T35 trước tiên**~~ — đã xong 2026-08-28. T36 và T38 mở khoá.
- **T36 và T38 song song được** — một người sút, một thủ môn, không đụng file nhau.
- **T37 phải làm sau T36**, vì mốc chân trụ chỉ đứng yên khi IK đã ổn định.
- **T39 làm cuối**, nhưng viết checklist của nó **trước khi** bắt đầu T36 — biết trước mình
  bị đo bằng thước nào thì code hướng tới thước đó.
- **T40 độc lập hoàn toàn**, làm xen kẽ lúc chờ mesh nhân vật.

---

[Mục lục](README.md) · [Phase 6: Kiểm chứng tự động](phase-6-kiem-chung.md)
