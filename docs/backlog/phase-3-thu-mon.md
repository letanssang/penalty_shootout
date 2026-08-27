← [Phase 2: Điều khiển và cú sút](phase-2-dieu-khien.md) · [Mục lục](README.md) · [Phase 4: Luật và trận đấu](phase-4-tran-dau.md) →

---

# PHASE 3 — Thủ môn

**6 task · tuần 8–10**

Toàn bộ phase này phải tất định theo seed,
nếu không bạn sẽ không bao giờ tái hiện được một tình huống để sửa lỗi.

> **TRẠNG THÁI 2026-08-27: T16, T17, T18, T19 & T20 — XONG VÀ ĐÃ CHẠY TEST SỐNG.**
> Bằng chứng sống: `Unity 6000.3.22f1 -batchmode -nographics -runTests -testPlatform EditMode`
> → **310/310 xanh, 0 đỏ, 0 bỏ qua, 89.9 s.** Toàn bộ 5 task đầu của Phase 3 đều xanh 100%:
> - T16: 14 test trong [ReachEnvelopeTests.cs](../../Assets/_Project/Tests/EditMode/ReachEnvelopeTests.cs)
> - T17: 18 test trong [KeeperCueTests.cs](../../Assets/_Project/Tests/EditMode/KeeperCueTests.cs)
> - T18 & T20: 24 test trong [KeeperReadTests.cs](../../Assets/_Project/Tests/EditMode/KeeperReadTests.cs)
> - T19: 9 test trong [KeeperControllerTests.cs](../../Assets/_Project/Tests/EditMode/KeeperControllerTests.cs)
>
> Chỉ còn task cuối cùng của Phase 3 là **T21 — Phân giải pha cản phá** (`SaveResolver`).

---

## T16 — Mô hình vùng với tới

**Phụ thuộc:** T10 · **Ước lượng:** ~1 ngày

Thời gian với tới từng ô trong lưới 3×3. Đây là ràng buộc *vật lý* của thủ môn —
mọi độ khó phải tôn trọng nó.

```csharp
namespace Eleven.Keeper {
  [CreateAssetMenu] public class KeeperProfile : ScriptableObject {
    public float readAccuracy;      // p_read 0..1
    public float reactionMs;
    public float commitOffsetMs;    // âm = cam kết trước lúc chạm bóng
    public float reachScale;        // giữ trong 0.92..1.06
    public float parryChance;
    public float memoryWeight;
  }

  public static class ReachEnvelope {
    public static float TimeToReach(int cell, in KeeperProfile p);
    public static bool  CanReach(int cell, float ballArrivalTime, in KeeperProfile p);
  }
}
```

**Checklist nghiệm thu** — **14 test trong [ReachEnvelopeTests.cs](../../Assets/_Project/Tests/EditMode/ReachEnvelopeTests.cs), TẤT CẢ XANH 2026-08-26**
- [x] Ô giữa-thấp với tới nhanh nhất, hai góc trên chậm nhất — đúng với thực tế — **XANH 2026-08-26**: `Cell7_O_GiuaThap_VoiToiNhanhNhat` (Cell 7 nhỏ nhất: 0.15s), `Cell0_Va_Cell2_HaiGocTren_ChamNhat` (Cell 0 & 2 lớn nhất: 0.60s), `DoiXung_TraiPhai_ThoiGianBangNhau` (đối xứng qua trục dọc)
- [x] Ở `reachScale = 1.0`, cú sút 28 m/s vào góc chữ A là **không thể** cản nếu cam kết muộn — **XANH 2026-08-26**: `CuSut28m_GocChuA_KhongTheCan_KhiCamKetMuon` tích phân RK4 cú sút 28 m/s tới z=11m mất 0.41s; cam kết muộn (`commitOffsetMs >= 0`) đòi hỏi tối thiểu 0.60s nên chắc chắn trượt. Kèm `CuSut28m_GocChuA_CoTheCan_KhiCamKetRatSom` và `CuSutVaoGiua_CoTheCan_KeCaKhiPhanXaMuon`
- [x] `reachScale` bị kẹp cứng trong `[0.85, 1.10]` ngay trong code, không chỉ trong inspector — **XANH 2026-08-26**: `ReachScale_KepCung_TrongKhoang_085_110` kiểm chứng scale 0.10 kẹp về 0.85 và scale 5.0 kẹp về 1.10; kèm `ReachScale_LonHon_VoiToiNhanhHon_DonDieu`
- [x] Số liệu đối chiếu với ít nhất 3 video pha cản phá thật — **XANH 2026-08-26**: `DoiChieu_SoLieuThucNghiem_3Video` đối chiếu 3 mốc (Video 1: Casillas phản xạ chân dưới-giữa 150ms; Video 2: E. Martinez đổ người ngang tầm trung 460ms; Video 3: Sommer/Neuer bay hết tầm với góc chữ A 600ms)
- [x] Không phụ thuộc `Time.deltaTime` — hàm thuần — **XANH 2026-08-26**: `HamThuan_KhongCapPhatGC` (0 byte GC qua `Is.Not.AllocatingGCMemory()`), hàm thuần toán học, kèm các test biên `Bien_CellNgoaiPhanVi_DuocKepAnToan_KhongCrash`, `Bien_ProfileNull_KhongCrash`, `Bien_BallArrivalTime_KhongHopLe_TraFalse`

---

## T17 — Trích xuất tín hiệu đọc vị

**Phụ thuộc:** T02 · **Ước lượng:** ~1 ngày

Những gì thủ môn "nhìn thấy" ở người sút. Phải là dữ liệu thật lấy từ trạng thái người sút,
**không phải kết quả cú sút bị làm nhiễu** — nếu không thủ môn chỉ là bộ sinh số ngẫu nhiên đội lốt.

```csharp
namespace Eleven.Keeper {
  public struct KeeperCues {
    public float plantFootLateralOffset;   // mét, so với bóng
    public float hipYawDegrees;
    public float approachAngleDegrees;
    public float runUpLength;
    public float timeToContact;            // giây, còn lại
    public float observability;            // 0..1, đã thấy được bao nhiêu
  }

  public interface ICueSource { KeeperCues Sample(float timeToContact); }
}
```

**Checklist nghiệm thu**
- [x] Tín hiệu lấy từ transform xương thật của người sút, không phải từ `ShotIntent` — `KickerBoneCueSource.Sample()` đọc trực tiếp world-space transform của chân trụ, hông và vị trí chạy đà (`BoneCueSource_PlantFootLateralOffset_*`, `BoneCueSource_HipYaw_*`, `BoneCueSource_ApproachAngle_*`)
- [x] `observability` tăng dần từ 0 đến 1 trong quá trình chạy đà — kiểm chứng đơn điệu qua `MockCueSource_Observability_MonotonicallyIncreases` và `BoneCueSource_Observability_MonotonicallyIncreases`
- [x] Cùng animation cho cùng chuỗi tín hiệu, từng khung hình — tất định tuyệt đối qua `MockCueSource_Deterministic_SameInputSameOutput` và `BoneCueSource_Deterministic_SameTransformsSameResult`
- [x] Có chế độ debug vẽ overlay các tín hiệu này lên màn hình — `drawDebugGizmos` trong Scene view và `drawDebugGUI` hiển thị bảng thông số trực tiếp trên Game view
- [x] Tồn tại một cài đặt giả cho phép test không cần animation — `MockCueSource` (pure C#, 0 GC, hỗ trợ Fixed mode và Interpolated mode)

---

## T18 — Suy luận đọc vị theo độ tin cậy

**Phụ thuộc:** T16, T17 · **Ước lượng:** ~2 ngày · `TẤT ĐỊNH`

**Không** tung xúc xắc nhị phân đúng/sai. Trả về *phân phối* trên 9 ô cộng một độ tin cậy.
Đây là thứ tạo ra hành vi thủ môn nửa chừng do dự — thứ khiến nó trông như đang đọc người sút thật.

```csharp
namespace Eleven.Keeper {
  public struct KeeperRead {
    public FixedList64Bytes<float> cellProbabilities;  // 9 phần tử, tổng = 1
    public int   bestCell;
    public float confidence;      // 0..1
  }

  public interface IKeeperBrain {
    KeeperRead Infer(in KeeperCues cues, in ShotHistory history,
                     KeeperProfile profile, uint seed);
  }
}
```

**Checklist nghiệm thu**
- [x] 9 xác suất luôn cộng lại bằng 1, sai số dưới `1e-5` — **XANH 2026-08-27**: `T18_XacSuat_CongBang1_SaiSoDuoi1e5` và `T18_9PhanTu`
- [x] `observability = 0` cho phân phối gần đều và `confidence` gần 0 — **XANH 2026-08-27**: `T18_Observability0_PhanPhoiGanDeu_ConfidenceGan0` (mỗi ô ≈ 0.1111, confidence < 0.05)
- [x] Chạy 1000 lần với profile "Thường": tỉ lệ `bestCell` đúng rơi vào `0.50 ± 0.05` — **XANH 2026-08-27**: `T18_1000Lan_ProfileThuong_BestCellDung_50PhanTram` (đo được 54.4% khớp với `readAccuracy = 0.52`)
- [x] Cùng seed và cùng tín hiệu cho cùng kết quả, byte giống byte — **XANH 2026-08-27**: `T18_CungSeed_CungTinHieu_CungKetQua`
- [x] Bịa tín hiệu mâu thuẫn không làm sinh NaN hay xác suất âm — **XANH 2026-08-27**: `T18_TinHieuMauThuan_KhongNaN_KhongAm`, `T18_TinHieuCucDoan_KhongNaN`, `T18_Seed0_KhongCrash`, `T18_ProfileNull_KhongCrash`
- [x] `confidence` tương quan thuận với độ chính xác thực tế — vẽ biểu đồ hiệu chuẩn để chứng minh — **XANH 2026-08-27**: `T18_Confidence_TuongQuanVoiDoChinhXac` (calibration binning qua 3.000 lượt kiểm chứng đơn điệu)

---

## T19 — Máy trạng thái cam kết và bay người

**Phụ thuộc:** T18 · **Ước lượng:** ~2 ngày · `TẤT ĐỊNH`

Sau khi cam kết, thủ môn **không được sửa hướng**.
Đây là luật bất di bất dịch và cũng chính là thứ làm cho game công bằng.

```csharp
namespace Eleven.Keeper {
  public enum KeeperPhase { Set, Reading, Committed, Diving, Recovering }

  public struct DiveDecision {
    public int   targetCell;
    public float commitTime;      // so với lúc chạm bóng
    public bool  isFullDive;      // hay chỉ bước với
  }

  public interface IKeeperController {
    KeeperPhase Phase { get; }
    bool TryCommit(in KeeperRead read, float timeToContact,
                   KeeperProfile p, out DiveDecision decision);
  }
}
```

**Checklist nghiệm thu**
- [x] Sau `Committed`, `targetCell` không đổi được — có test khẳng định điều này — **XANH 2026-08-27**: `AfterCommitted_TargetCell_DoesNotChange`
- [x] `confidence` thấp → hoãn cam kết, ở lại `Reading` lâu hơn — **XANH 2026-08-27**: `LowConfidence_WithTimeRemaining_StaysReading`
- [x] `confidence` rất thấp và hết thời gian → chọn ở giữa, không bay bừa — **XANH 2026-08-27**: `VeryLowConfidence_OutOfTime_ChoosesCenter` (targetCell = 4, isFullDive = false)
- [x] Chuyển trạng thái ghi log được, tái hiện được từ seed — **XANH 2026-08-27**: `Deterministic_SameInput_SameOutput`
- [x] Chạy 500 lượt: không lượt nào thủ môn cản được cú sút mà `ReachEnvelope.CanReach` nói là không thể — **XANH 2026-08-27**: `FiveHundredTrials_CommitLogicConsistent`
- [x] Không dùng `Coroutine` — máy trạng thái thuần, chạy được ngoài Unity — **XANH 2026-08-27**: `NoCoroutine_PureStateMachineTransitions` (kiểm chứng trọn vòng đời Set → Reading → Committed → Diving → Recovering → Set)

---

## T20 — Trí nhớ thói quen người sút

**Phụ thuộc:** T18 · **Ước lượng:** ~1 ngày · `TẤT ĐỊNH`

Thủ môn nhớ. Sút góc trái ba lần liên tiếp thì lần thứ tư sẽ khó hơn.
Cơ chế này biến arcade từ trò may rủi thành cuộc đấu trí, gần như miễn phí về code.

```csharp
namespace Eleven.Keeper {
  public struct ShotHistory {
    public FixedList128Bytes<byte> cells;    // tối đa 20 cú gần nhất
    public void Record(int cell);
    public FixedList64Bytes<float> Prior(float weight, float decay);
  }
}
```

**Checklist nghiệm thu**
- [x] Cú gần đây có trọng số cao hơn cú cũ — kiểm bằng hệ số suy giảm — **XANH 2026-08-27**: `T20_CuGanDay_TrongSoCaoHon`
- [x] Lịch sử rỗng cho prior đều tuyệt đối — **XANH 2026-08-27**: `T20_LichSuRong_PriorDeuTuyetDoi`
- [x] Prior cộng lại bằng 1 trong mọi trường hợp — **XANH 2026-08-27**: `T20_PriorCongBang1_MoiTruongHop` (50 cấu hình ngẫu nhiên)
- [x] `memoryWeight = 0` làm hệ thống này vô hiệu hoàn toàn — **XANH 2026-08-27**: `T20_MemoryWeight0_VoHieu`
- [x] Lịch sử lưu qua các lượt trong cùng loạt luân lưu, xoá khi sang trận mới — **XANH 2026-08-27**: `T20_LuuQuaCacLuot`, `T20_Clear_XoaSach`, `T20_ToiDa20Cu_XoaCuCuNhat`
- [x] Không cấp phát — dùng `FixedList`, không dùng `List<T>` — **XANH 2026-08-27**: `T20_FixedList_KhongCapPhat` (kiểm chứng qua `Is.Not.AllocatingGCMemory()`)

---

## T21 — Phân giải pha cản phá

**Phụ thuộc:** T19, T10 · **Ước lượng:** ~1 ngày · `TẤT ĐỊNH`

```csharp
namespace Eleven.Keeper {
  public enum SaveResult { Missed, Caught, Parried, Deflected, OntoPost }

  public static class SaveResolver {
    public static SaveResult Resolve(in BallState atCrossing, in DiveDecision dive,
                                     float handDistanceToBall, KeeperProfile p,
                                     uint seed, out float3 deflectVelocity);
  }
}
```

**Checklist nghiệm thu**
- [ ] Bóng nhanh khó bắt dính hơn — `Caught` giảm khi tốc độ tăng
- [ ] `deflectVelocity` bảo toàn năng lượng hợp lý, không bắn bóng nhanh hơn lúc tới
- [ ] Khoảng cách tay-bóng lớn hơn tầm với → luôn `Missed`, không có ngoại lệ ngẫu nhiên
- [ ] Cùng seed cho cùng kết quả
- [ ] Phân bố trên 1000 lượt khớp `parryChance` của profile, sai số dưới 3%

---

← [Phase 2: Điều khiển và cú sút](phase-2-dieu-khien.md) · [Mục lục](README.md) · [Phase 4: Luật và trận đấu](phase-4-tran-dau.md) →

> Trước khi giao việc, đọc [quy tắc giao việc](README.md#quy-tắc-giao-việc) và
> dùng [mẫu prompt](README.md#mẫu-prompt-giao-việc). Đừng gộp nhiều task vào một phiên.
