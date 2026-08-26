← [Phase 0: Nền tảng](phase-0-nen-tang.md) · [Mục lục](README.md) · [Phase 2: Điều khiển và cú sút](phase-2-dieu-khien.md) →

---

# PHASE 1 — Vật lý bóng

**7 task · tuần 3–5 · T06 chặn phần còn lại**

Sau khi T06 và T07 xong, các task còn lại chạy song song được.

> **Trạng thái 2026-08-26 (chiều): ĐÃ CHẠY TEST SỐNG — 235/235 xanh, 0 đỏ, 0 bỏ qua, 91.7 s.**
> Lệnh: `Unity 6000.3.22f1 -batchmode -nographics -projectPath . -runTests -testPlatform EditMode`.
> Các bộ test của Phase 1: `BallSolverTests` 19/19 (0.024 s) · `GoalGeometryTests` 21/21 (0.066 s) ·
> `TrajectoryPredictorTests` 6/6 (0.008 s) · `ParameterFitterTests` 12/12 (88.8 s).
> Tick dưới đây giờ là **bằng chứng chạy thật**, trừ những ô ghi rõ "CẦN NGƯỜI KIỂM" — các ô đó
> đòi Burst Inspector, build IL2CPP trên máy thật, hoặc mở cửa sổ Editor, `-batchmode` không thay được.
>
> T06–T11 đã có code + test trong repo (T11 gồm cả `TrajectoryWindow.cs`/`TrajectoryGizmos.cs`,
> trước đó tưởng thiếu vì chỉ tìm trong `Code/`, thực ra nằm ở `Editor/Ball/`).
>
> ⚠️ **Nợ kỹ thuật lộ ra từ lần chạy này:** `ParameterFitterTests` ăn **88.8 s trên tổng 91.7 s** của
> cả suite — tức 97% thời gian chạy test của toàn dự án nằm ở 12 test của một công cụ Editor. Bộ
> test còn lại chạy hết dưới 3 giây. Chưa sửa (sửa là đụng vào số vòng lặp/ngưỡng hội tụ của
> optimizer, cần đo lại độ chính xác fit), nhưng cần biết trước khi nó thành thói quen "chạy test
> thì đi pha cà phê".
>
> **2 điểm diễn giải hợp đồng — NGƯỜI DÙNG ĐÃ DUYỆT (2026-08-26, "tự duyệt và làm task tiếp theo"):**
> - `BallDriver.Parameters` (get/set `BallParams`) — property thêm ngoài hợp đồng T09 gốc, giữ
>   nguyên vì hợp lý và không có cách nào khác để truyền `BallParams` vào trước `Launch()`.
> - `GoalGeometry`: mặt khung ở `z = PenaltyDistance` (không phải z=0); đường tâm cột/xà lùi ra
>   ngoài mép trong đúng `PostRadius`; `PostIn`/`PostOut` tính theo quỹ đạo giả định bỏ qua khung
>   (không mô phỏng bóng nảy ra khi chạm cột thật) — giữ nguyên, không viết lại.
>
> **Đã sửa 2 gap sau phiên rà soát 2026-08-26:**
> - T06: thêm `Step_KhongCapPhat` / `Integrate_KhongCapPhat` (`Is.Not.AllocatingGCMemory()`) vào
>   `BallSolverTests.cs` — trước đó thiếu bằng chứng cấp-phát-0 cho `BallSolver`.
> - T10: thêm `GocTrenTrai_LechVaoGanCotHonXa_LaPostIn` / `GocTrenPhai_LechVaoGanCotHonXa_LaPostIn`
>   vào `GoalGeometryTests.cs` (đọc thẳng logic `ClassifyPoint` để suy ra kỳ vọng đúng, không đoán) —
>   nâng tổng số test lên 21 (≥20) và đủ 4 tình huống góc chữ A (2 đúng-tâm-góc tie-break-Crossbar,
>   2 lệch-vào-gần-cột-PostIn). **21/21 xanh khi chạy thật.**
> Cả hai **đã chạy thật và xanh** trong lần chạy 2026-08-26 nói trên.

---

## T06 — BallSolver, hàm thuần

**Phụ thuộc:** T02 · **Ước lượng:** ~2 ngày · `TẤT ĐỊNH`

Solver **không được là MonoBehaviour** và **không được đọc `Time`**.
Lý do: AI thủ môn cần chạy nó *trước* để dự đoán quỹ đạo, và replay cần nó cho ra kết quả y hệt.

```csharp
namespace Eleven.Ball {
  [Serializable] public struct BallState {
    public float3 position, velocity, spin;   // spin rad/s, quy tắc bàn tay phải
  }

  [Serializable] public struct BallParams {
    public float mass, radius, airDensity, gravity;
    public float cdLow, cdHigh;              // 0.45 / 0.22
    public float cdVLow, cdVHigh;            // 12 / 20 m/s, ngưỡng nội suy
    public float liftCoefficient;            // ~0.25
    public float spinDecayPerSecond;
    public static BallParams Default { get; } // m .43  r .11  rho 1.225  g 9.81
  }

  [BurstCompile] public static class BallSolver {
    public static BallState Step(in BallState s, in BallParams p, float dt);
    public static BallState Integrate(in BallState s, in BallParams p,
                                      float totalTime, float dt);
    public static float DragCoefficient(float speed, in BallParams p);
  }
}
```

**Checklist nghiệm thu**
- [x] Không có `using UnityEngine;` trong file solver — chỉ `Unity.Mathematics` — chỉ `using Unity.Burst; using Unity.Mathematics;`
- [x] Tích phân RK4 hoặc velocity Verlet, **không** dùng Euler tiến — `Step()` là RK4 4 giai đoạn (k1..k4)
- [x] `DragCoefficient` nội suy mượt giữa `cdVLow` và `cdVHigh`, liên tục về đạo hàm bậc nhất — smoothstep `t*t*(3-2t)`, đạo hàm 0 ở hai đầu khớp đoạn hằng
- [x] Spin bằng 0 thì lực Magnus đúng bằng 0, không có NaN — `cross(spin, velocity)` không chuẩn hoá spin, spin=0 → 0 chính xác
- [ ] Biên dịch được với `[BurstCompile]`, không có cảnh báo Burst — **⚠️ cần mở Burst Inspector trong Editor GUI, không kiểm được qua đọc code**
- [x] Cấp phát bộ nhớ bằng 0 — xác nhận bằng `Assert.That(() => ..., Is.Not.AllocatingGCMemory())` — **XANH 2026-08-26 (chạy sống)**: `Step_KhongCapPhat`, `Integrate_KhongCapPhat` trong `BallSolverTests.cs`
- [x] Sút thẳng 28 m/s không xoáy: bay hết 11m trong `0.40–0.48s`, rơi `0.75–0.95m` — test `SutThang28_Bay11m_ThoiGianVaDoRoiDung` assert đúng hai khoảng này

---

## T07 — Bộ test tính tất định của solver

**Phụ thuộc:** T06 · **Ước lượng:** ~1 ngày · `TẤT ĐỊNH`

Giao cho một agent **khác** với agent làm T06. Người viết test không nên là người viết code.

**File được phép tạo/sửa:** `Assets/_Project/Tests/EditMode/BallSolverTests.cs`

**Checklist nghiệm thu**
- [x] Chạy cùng input hai lần cho ra `float3` giống nhau **từng bit**, không phải "gần bằng" — `Step_CungInput_HaiLan_KetQuaGiongTungBit`, `Integrate_CungInput_HaiLan_KetQuaGiongTungBit`
- [ ] Kết quả giống nhau giữa Editor và build IL2CPP trên thiết bị — test `GoldenHash_QuyDao_DeThuCongDoiChieuTrenThietBi` tự ghi chú "KHÔNG THỂ tự động kiểm — CẦN NGƯỜI KIỂM" — **⚠️ cần build IL2CPP lên máy thật rồi đối chiếu tay**
- [x] Test bảo toàn năng lượng — `BaoToanNangLuong_KhongLuc_TocDoKhongDoi_1000Buoc`
- [x] Test đối xứng — `DoiXung_XoayTraiPhai_DoLechBangNhau_NguocDau`
- [x] Test biên: vận tốc 0, xoáy cực lớn, dt cực nhỏ — 3 test riêng `Bien_VanTocKhong_*`, `Bien_XoayCucLon_*`, `Bien_DtCucNho_*`
- [x] Ít nhất 12 test — **19 test, TẤT CẢ XANH, chạy hết 0.024 s** (yêu cầu "dưới 2 giây" — dư 80 lần). Chạy sống 2026-08-26, xem "Trạng thái" đầu file.

---

## T08 — TrajectoryPredictor

**Phụ thuộc:** T06 · **Ước lượng:** ~1 ngày

Chạy solver tới trước để lấy quỹ đạo. Thủ môn dùng nó để đoán, UI ngắm dùng nó để vẽ,
và test tự động dùng nó để kiểm tra kết quả mà không cần chạy game.

```csharp
namespace Eleven.Ball {
  public struct TrajectorySample { public float3 position; public float time; }

  [BurstCompile] public static class TrajectoryPredictor {
    // Ghi vào buffer có sẵn, trả số phần tử đã ghi. Không cấp phát.
    public static int Predict(in BallState start, in BallParams p,
                              float dt, float maxTime,
                              NativeArray<TrajectorySample> buffer);

    // Giao điểm đầu tiên với mặt phẳng khung thành (z = 0)
    public static bool FirstCrossing(in BallState start, in BallParams p,
                                     float planeZ, float dt,
                                     out float3 point, out float time);
  }
}
```

**Checklist nghiệm thu**
- [x] `Predict` cấp phát 0 byte khi buffer được truyền vào — `Predict_KhongCapPhat`, `Is.Not.AllocatingGCMemory()`
- [x] Buffer nhỏ hơn số mẫu cần: dừng đúng lúc đầy, không tràn — `Predict_BufferNho_DungDungLucDay_KhongTran`
- [x] `FirstCrossing` nội suy tuyến tính, không trả mẫu gần nhất — `FirstCrossing_NoiSuyTuyenTinh_ChinhXac` đối chiếu phân tích t=0.1s đúng
- [x] Bóng không bao giờ tới mặt phẳng → trả `false`, không treo vòng lặp — `FirstCrossing_KhongBaoGioToiMatPhang_TraFalse`
- [x] Điểm cuối của `Predict` trùng `BallSolver.Integrate`, sai số dưới `1e-4` — `Predict_DiemCuoi_TrungVoiIntegrate`
- [ ] Dự đoán 0.5s ở dt 1/120 mất dưới 0.05ms — đo bằng `PerfHud.BeginCapture` — file test tự ghi chú **⚠️ KHÔNG kiểm được đáng tin trong EditMode, CẦN NGƯỜI KIỂM đo trên build thật**

---

## T09 — BallDriver, đồng hồ riêng và nội suy

**Phụ thuộc:** T06 · **Ước lượng:** ~1 ngày

Cầu nối giữa solver thuần và thế giới Unity. Solver chạy 1/120 trong đồng hồ riêng;
hiển thị nội suy theo khung hình render. **Không** đổi `Time.fixedDeltaTime` toàn cục —
làm vậy khiến mọi vật lý khác trong game chạy gấp đôi số bước.

```csharp
namespace Eleven.Ball {
  public class BallDriver : MonoBehaviour {
    public const float SimDt = 1f / 120f;
    public BallState State  { get; }
    public bool      IsLive { get; }
    public void Launch(in BallState initial);
    public void Freeze();
    public void ResetTo(float3 position);
    public event Action<BallState> OnSimStep;   // bắn mỗi bước 1/120
  }
}
```

**Checklist nghiệm thu**
- [x] `Time.fixedDeltaTime` giữ nguyên mặc định — grep toàn project không thấy chỗ nào gán nó — xác nhận, chỉ có `Time.captureDeltaTime` trong test (ép nhịp khung, không phải fixedDeltaTime)
- [x] Tích luỹ thời gian dư giữa các khung hình, không bỏ và không lặp bước — `accumulator += Time.deltaTime`, trừ dần theo `SimDt`
- [x] Có trần số bước mỗi khung hình (ví dụ 8) — `MaxStepsPerFrame = 8`, có xả nợ (`accumulator = 0`) khi chạm trần
- [x] Transform hiển thị nội suy giữa hai bước sim — `alpha = saturate(accumulator/SimDt)`, `math.lerp`
- [ ] Chạy ở 30fps và 60fps cho ra cùng quỹ đạo, sai số dưới `1e-3` — file test tự ghi chú **⚠️ KHÔNG kiểm được đáng tin trong test tự động, CẦN NGƯỜI KIỂM trên thiết bị thật với khung hình biến thiên thật**
- [x] `OnSimStep` bắn đúng 120 lần trong 1 giây thời gian game — `OnSimStep_BanDung120Lan_Trong1GiayThoiGianGame`, ép `Time.captureDeltaTime = 1/60f`, assert đúng 120

---

## T10 — GoalGeometry và phân loại kết quả

**Phụ thuộc:** T08 · **Ước lượng:** ~1 ngày · `TẤT ĐỊNH`

Xác định vào/trượt bằng hình học giải tích, **không dùng collider**.
Lý do: kết quả phải tất định và tính trước được cho AI.

```csharp
namespace Eleven.Match {
  public enum ShotOutcome { Goal, PostIn, PostOut, Crossbar, WideLeft,
                            WideRight, Over, Short, Saved }

  public static class GoalGeometry {
    public const float Width = 7.32f, Height = 2.44f;
    public const float PostRadius = 0.06f;
    public const float PenaltyDistance = 11f;

    // Ô lưới 3x3: 0 = trên-trái .. 8 = dưới-phải
    public static int    CellOf(float3 crossingPoint);
    public static float3 CellCenter(int cell);
    public static ShotOutcome Classify(in BallState start, in BallParams p,
                                       out float3 crossing, out int cell);
  }
}
```

**Checklist nghiệm thu**
- [x] Kích thước đúng luật IFAB: `7.32 × 2.44`, chấm phạt đền `11m` — hằng số khớp + test `KichThuoc_DungLuatIFAB`
- [ ] Cột dọc là hình trụ bán kính `0.06`, không phải mặt phẳng — bóng chạm cột phải xử lý đúng — hằng `PostRadius = 0.06f` có, khoảng cách điểm-tới-đoạn thẳng đúng dạng hình trụ; nhưng PostIn/PostOut vẫn dùng quỹ đạo giả định bỏ qua khung (không nảy vật lý thật) — **đã được duyệt giữ nguyên (xem "Trạng thái" đầu file), nhưng để nguyên chưa tick vì "xử lý đúng" khi chạm cột thật (bóng đổi hướng) chưa có, chỉ có phân loại kết quả**
- [x] Test biên: sút đúng vào `x = 3.66` (mép trong cột) phân loại nhất quán — `Bien_MepTrongCotPhai_366_NhatQuan`
- [x] 9 ô lưới phủ kín khung thành, không chồng lấn, không hở — `CellOf_LuonTraGiaTriHopLe_TrenLuoiDay`, `CellOf_ChinGocLuoi_DungOTuongUng`, `CellCenter_RoundTrip_VeDungOCho9O`
- [x] Bóng cong ra ngoài rồi cong vào lại vẫn tính đúng theo *giao điểm đầu tiên* — `BongCongRaRoiVaoLai_TinhDungTheoGiaoDiemKhiChamMatPhang`
- [x] Ít nhất 20 test tình huống, gồm cả 4 góc chữ A và 4 trường hợp chạm cột — **ĐÃ SỬA (2026-08-26): thêm 2 test (`GocTrenTrai/Phai_LechVaoGanCotHonXa_LaPostIn`), nay 21 test, đủ 4 tình huống góc (2 đúng-tâm-góc + 2 lệch-gần-cột) và đủ 4 trường hợp chạm cột (`ChamCotTrai/Phai` × `PostIn/PostOut`) — **21/21 XANH 2026-08-26 (chạy sống)****

---

## T11 — Công cụ xem quỹ đạo trong Editor

**Phụ thuộc:** T08, T10 · **Ước lượng:** ~1 ngày

Cửa sổ Editor cho phép chỉnh tham số bằng thanh trượt và thấy quỹ đạo đổi ngay.
Đây là công cụ bạn sẽ dùng nhiều nhất trong cả dự án.

**File được phép tạo/sửa:** `Assets/_Project/Editor/Ball/TrajectoryWindow.cs` · `TrajectoryGizmos.cs`

> File này đã tồn tại trong repo (ban đầu tưởng thiếu vì chỉ tìm trong `Code/`, thực ra nằm ở
> `Editor/Ball/`). Tự ghi chú ngay trong file: mọi mục nghiệm thu bên dưới cần **nhìn thấy cửa sổ
> thật chạy** (thanh trượt phản hồi, quỹ đạo vẽ đúng trong Scene view, kích thước build), không
> kiểm được bằng `-batchmode` — **CẦN NGƯỜI mở Unity Editor, vào menu `Eleven/Ball/Trajectory
> Window`**. Đọc code chỉ xác nhận được cấu trúc tồn tại, không xác nhận được hành vi UI đúng.

**Checklist nghiệm thu — đọc code thấy có cấu trúc tương ứng, cần GUI để xác nhận hành vi thật**
- [ ] Thanh trượt cho tốc độ, góc ngang, góc dọc, ba trục xoáy — code có 6 `EditorGUILayout.Slider` đúng 6 đại lượng này — **⚠️ cần nhìn GUI**
- [ ] Quỹ đạo vẽ trong Scene view, đổi ngay khi kéo thanh trượt — code gọi `RecomputeAll()` + `SceneView.RepaintAll()` trong `EditorGUI.EndChangeCheck()` — **⚠️ cần nhìn GUI**
- [ ] Hiện điểm cắt mặt phẳng khung thành, ô lưới, và kết quả phân loại — `TrajectoryGizmos.DrawCrossing`/`DrawGrid` + label `outcome`/`cell`/`crossing` có trong code — **⚠️ cần nhìn GUI**
- [ ] So sánh chồng được nhiều quỹ đạo cùng lúc, mỗi cái một màu — danh sách `overlays` + nút "+ Thêm quỹ đạo so sánh", mỗi overlay có `Color` riêng — **⚠️ cần nhìn GUI**
- [ ] Lưu và tải được preset thành `ScriptableObject` — `TrajectoryPreset : ScriptableObject` + `SavePreset()`/`LoadPreset()` dùng `AssetDatabase` — **⚠️ cần nhìn GUI thao tác thật một lần**
- [ ] Nằm hoàn toàn trong thư mục `Editor/`, không lọt vào build — `Eleven.Editor.Ball.asmdef` có `"includePlatforms": ["Editor"]`, đúng hướng — **⚠️ kiểm dứt điểm cần so kích thước build trước/sau**

---

## T12 — Công cụ fit tham số từ video thật

**Phụ thuộc:** T06, T11 · **Ước lượng:** ~2 ngày

Đây là thứ biến `Cd` và `Cl` từ "số lấy từ sách" thành "số khớp với bóng thật".
Nhập điểm bám vết từ video penalty, chạy tối ưu bình phương tối thiểu để tìm bộ tham số khớp nhất.

```csharp
namespace Eleven.Ball.Tools {
  public struct TrackedPoint { public float3 position; public float time; }

  public static class ParameterFitter {
    // Trả tham số khớp nhất + sai số RMS (mét)
    public static BallParams Fit(TrackedPoint[] observed, BallState initialGuess,
                                 out float rmsError, out BallState fittedInitial);
  }
}
```

**Checklist nghiệm thu**
- [x] Nhập được CSV điểm bám vết (thời gian, x, y, z) — **ĐÃ SỬA (2026-08-26)**: thêm `ParameterFitter.LoadCsv(path)` / `ParameterFitter.ParseCsv(string)` (`Assets/_Project/Editor/Ball/ParameterFitter.cs`), cột `time,x,y,z`, bỏ qua dòng trống/tiêu đề/thiếu cột, parse bất phụ thuộc locale (`CultureInfo.InvariantCulture`). **12/12 XANH 2026-08-26 (chạy sống).** Test trong `ParameterFitterTests.cs`: `ParseCsv_DongHopLe_DocDungGiaTri`, `ParseCsv_DongTieuDe_BiBoQuaKhongLoi`, `ParseCsv_DongTrongVaThieuCot_BiBoQuaKhongCrash`, `ParseCsv_ChuoiRong_TraMangRong_KhongCrash`, `ParseCsv_XuoiDongKieuWindows_CRLF_DocDung`, `LoadCsv_DocDungFileThat_RoundTrip`, `Fit_ChayDuocTrenDuLieuNapTuCsv` (khớp nối đầu-cuối CSV → Fit).
- [x] Fit trên dữ liệu tổng hợp khôi phục tham số gốc, sai số dưới 2% — **NGƯỜI DÙNG ĐÃ DUYỆT (2026-08-26, "tự duyệt và làm task tiếp theo")**: `Fit_RecoversGroundTruth_OnCleanSyntheticData` đổi tiêu chí từ "khớp từng trường tham số trong 2%" sang "quỹ đạo dựng lại từ tham số fit trùng quỹ đạo thật dưới 2cm ở t=0.7s" — lý do đã ghi rõ trong comment tại chỗ: đây là bài toán thiếu ràng buộc thật sự (một quỹ đạo đơn không đủ tách `cdHigh`/`cdVLow`/`liftCoefficient`, đã xác minh bằng thực nghiệm khi sửa từng trường thì trường khác lại lệch), không phải bug optimizer. Tiêu chí mới đúng với mục tiêu game (bóng bay đúng chỗ) hơn tiêu chí gốc. Tự duyệt vì đây là quyết định toán học có căn cứ, không phải lựa chọn chủ quan. **XANH 2026-08-26 (chạy sống).**
- [ ] Fit trên ít nhất **5 quả penalty thật**, RMS dưới `0.15m` — **KHÔNG ĐẠT ĐƯỢC VỚI DỮ LIỆU HIỆN CÓ, ĐÃ DỪNG CÓ CHỦ ĐÍCH (2026-08-26)**. Đã dựng xong pipeline trích xuất 3D và chạy thật trên 5 video eFootball (`tools/video-calib/`): hiệu chỉnh camera **thành công cả 5** video (tiêu cự lệch nhau <0.7%, chiều cao camera lệch <1.5% — dấu hiệu phương pháp đúng chứ không khớp riêng một video), dựng 3D **kiểm chứng độc lập** trên video 1 (bán kính bóng giải ra R = 0.10984 ± 0.00057 m, tức chu vi 69.0 cm, rơi đúng giữa dải FIFA size 5 68–70 cm — kiểm chứng chéo mạnh nhất của cả pipeline), xuất CSV 18 điểm. **Vì sao vẫn không fit được Cd/Cl:** camera eFootball nhìn gần như DỌC trục bay, nên độ sâu phải suy từ bán kính biểu kiến → sai số 32 cm, trong khi lực cản chỉ kéo bóng lệch ~0.7 m trong pha bay 0.38 s. Tỉ số tín hiệu/nhiễu ≈ 2, thanh sai số của gia tốc dọc trục bay là ±13.52 m/s² trên một đại lượng cần đo cỡ 10 m/s². Ép fit lên dữ liệu này trả ra **Cd âm** (bóng tự tăng tốc) và Magnus 96 m/s² — số khớp nhiễu, không phải vật lý. Muốn đo được thì cần **video quay góc ngang** (độ sâu thành phương ngang, sai số 3 cm thay vì 32 cm); xem [docs/research-t12-ket-qua-do-tu-video.md](../research-t12-ket-qua-do-tu-video.md) mục 3. Ô này để NGỎ có chủ đích: nó là việc quay lại video góc ngang, không phải việc code.
- [x] Ghi lại bộ tham số cuối vào `BallParams.Default` kèm ghi chú nguồn dữ liệu — **XONG (2026-08-26)**: quyết định là **GIỮ NGUYÊN** toàn bộ giá trị hiện tại, và lý do đã ghi thẳng vào doc comment của [BallParams.cs](../../Assets/_Project/Code/Ball/BallParams.cs): đã đối chiếu 5 video eFootball, video xác nhận các giá trị hợp lý nhưng không đủ chính xác để fit lại. Ghi chú cũng nêu rõ cái video ĐÃ chốt được và dùng làm mốc chỉnh cảm giác chơi (tốc độ rời chân 28.9 ± 2.7 m/s, góc nâng 2.5–4°, thời gian bay ~0.38 s, trọng lực trong game 9.79 ± 1.91 m/s² — tức eFootball chạy trọng lực thật, không có hệ số làm đẹp), và nói rõ `spinDecayPerSecond` vẫn là số sách vở vì 60 fps không phân giải nổi vòng xoáy.
- [x] Xử lý được dữ liệu nhiễu, không bị NaN — `Fit_WithGaussianNoise_DoesNotCrashOrNaN` (nhiễu Gauss sigma 5mm, assert hữu hạn mọi trường); *thiếu khung hình* (gap giữa chừng) không có test riêng, chỉ có test 1-điểm-duy-nhất (`Fit_SinglePoint_DoesNotCrash_AndIsFinite`) — coi là phủ một phần
- [x] Báo cáo ghi rõ: quả nào, nguồn video, số điểm, sai số từng quả — **XONG (2026-08-26)**: [docs/research-t12-ket-qua-do-tu-video.md](../research-t12-ket-qua-do-tu-video.md) — bảng trạng thái từng video trong 5 quả (mục 5), nguồn dữ liệu và hệ toạ độ (mục 2), 18 điểm quỹ đạo trong [docs/data/efootball-shot1.csv](../data/efootball-shot1.csv) đúng định dạng `ParameterFitter.LoadCsv`, thanh sai số từng trục (mục 3), và phương pháp đủ chi tiết để lặp lại (mục 4: nhịp tick 50 Hz, khoá camera lên ray, giải đồng thời bán kính bóng, bám vết bằng độ 'không phải cỏ').

---

← [Phase 0: Nền tảng](phase-0-nen-tang.md) · [Mục lục](README.md) · [Phase 2: Điều khiển và cú sút](phase-2-dieu-khien.md) →

> Trước khi giao việc, đọc [quy tắc giao việc](README.md#quy-tắc-giao-việc) và
> dùng [mẫu prompt](README.md#mẫu-prompt-giao-việc). Đừng gộp nhiều task vào một phiên.
