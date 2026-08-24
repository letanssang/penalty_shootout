← [Phase 0: Nền tảng](phase-0-nen-tang.md) · [Mục lục](README.md) · [Phase 2: Điều khiển và cú sút](phase-2-dieu-khien.md) →

---

# PHASE 1 — Vật lý bóng

**7 task · tuần 3–5 · T06 chặn phần còn lại**

Sau khi T06 và T07 xong, các task còn lại chạy song song được.

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
- [ ] Không có `using UnityEngine;` trong file solver — chỉ `Unity.Mathematics`
- [ ] Tích phân RK4 hoặc velocity Verlet, **không** dùng Euler tiến
- [ ] `DragCoefficient` nội suy mượt giữa `cdVLow` và `cdVHigh`, liên tục về đạo hàm bậc nhất
- [ ] Spin bằng 0 thì lực Magnus đúng bằng 0, không có NaN
- [ ] Biên dịch được với `[BurstCompile]`, không có cảnh báo Burst
- [ ] Cấp phát bộ nhớ bằng 0 — xác nhận bằng `Assert.That(() => ..., Is.Not.AllocatingGCMemory())`
- [ ] Sút thẳng 28 m/s không xoáy: bay hết 11m trong `0.40–0.48s`, rơi `0.75–0.95m`

---

## T07 — Bộ test tính tất định của solver

**Phụ thuộc:** T06 · **Ước lượng:** ~1 ngày · `TẤT ĐỊNH`

Giao cho một agent **khác** với agent làm T06. Người viết test không nên là người viết code.

**File được phép tạo/sửa:** `Assets/_Project/Tests/EditMode/BallSolverTests.cs`

**Checklist nghiệm thu**
- [ ] Chạy cùng input hai lần cho ra `float3` giống nhau **từng bit**, không phải "gần bằng"
- [ ] Kết quả giống nhau giữa Editor và build IL2CPP trên thiết bị
- [ ] Test bảo toàn năng lượng: không cản, không xoáy, không trọng lực → tốc độ không đổi sau 1000 bước
- [ ] Test đối xứng: xoáy trái và xoáy phải cùng độ lớn cho độ lệch bằng nhau, ngược dấu
- [ ] Test biên: vận tốc 0, xoáy cực lớn, dt cực nhỏ — không NaN, không Infinity
- [ ] Ít nhất 12 test, tất cả xanh, chạy dưới 2 giây

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
- [ ] `Predict` cấp phát 0 byte khi buffer được truyền vào
- [ ] Buffer nhỏ hơn số mẫu cần: dừng đúng lúc đầy, không tràn
- [ ] `FirstCrossing` nội suy tuyến tính giữa hai bước để lấy điểm cắt chính xác, không trả về mẫu gần nhất
- [ ] Bóng không bao giờ tới mặt phẳng → trả `false`, không treo vòng lặp
- [ ] Điểm cuối của `Predict` trùng với chạy `BallSolver.Integrate` cùng tham số, sai số dưới `1e-4`
- [ ] Dự đoán 0.5s ở dt 1/120 mất dưới 0.05ms — đo bằng `PerfHud.BeginCapture`

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
- [ ] `Time.fixedDeltaTime` giữ nguyên mặc định — grep toàn project không thấy chỗ nào gán nó
- [ ] Tích luỹ thời gian dư giữa các khung hình, không bỏ và không lặp bước
- [ ] Có trần số bước mỗi khung hình (ví dụ 8) để tránh xoáy chết khi máy khựng
- [ ] Transform hiển thị nội suy giữa hai bước sim — bóng mượt ở 60fps dù sim 120Hz
- [ ] Chạy ở 30fps và 60fps cho ra cùng quỹ đạo, sai số dưới `1e-3`
- [ ] `OnSimStep` bắn đúng 120 lần trong 1 giây thời gian game

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
- [ ] Kích thước đúng luật IFAB: `7.32 × 2.44`, chấm phạt đền `11m`
- [ ] Cột dọc là hình trụ bán kính `0.06`, không phải mặt phẳng — bóng chạm cột phải xử lý đúng
- [ ] Test biên: sút đúng vào `x = 3.66` (mép trong cột) phân loại nhất quán, không dao động
- [ ] 9 ô lưới phủ kín khung thành, không chồng lấn, không hở
- [ ] Bóng cong ra ngoài rồi cong vào lại vẫn tính đúng theo *giao điểm đầu tiên*
- [ ] Ít nhất 20 test tình huống, gồm cả 4 góc chữ A và 4 trường hợp chạm cột

---

## T11 — Công cụ xem quỹ đạo trong Editor

**Phụ thuộc:** T08, T10 · **Ước lượng:** ~1 ngày

Cửa sổ Editor cho phép chỉnh tham số bằng thanh trượt và thấy quỹ đạo đổi ngay.
Đây là công cụ bạn sẽ dùng nhiều nhất trong cả dự án.

**File được phép tạo/sửa:** `Assets/_Project/Editor/Ball/TrajectoryWindow.cs` · `TrajectoryGizmos.cs`

**Checklist nghiệm thu**
- [ ] Thanh trượt cho tốc độ, góc ngang, góc dọc, ba trục xoáy
- [ ] Quỹ đạo vẽ trong Scene view, đổi ngay khi kéo thanh trượt
- [ ] Hiện điểm cắt mặt phẳng khung thành, ô lưới, và kết quả phân loại
- [ ] So sánh chồng được nhiều quỹ đạo cùng lúc, mỗi cái một màu
- [ ] Lưu và tải được preset thành `ScriptableObject`
- [ ] Nằm hoàn toàn trong thư mục `Editor/`, không lọt vào build — kiểm bằng kích thước build

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
- [ ] Nhập được CSV điểm bám vết (thời gian, x, y, z)
- [ ] Fit trên dữ liệu tổng hợp (sinh từ chính solver) khôi phục tham số gốc, sai số dưới 2%
- [ ] Fit trên ít nhất **5 quả penalty thật**, RMS dưới `0.15m`
- [ ] Ghi lại bộ tham số cuối vào `BallParams.Default` kèm ghi chú nguồn dữ liệu
- [ ] Xử lý được dữ liệu nhiễu và thiếu khung hình, không bị NaN
- [ ] Báo cáo ghi rõ: quả nào, nguồn video, số điểm, sai số từng quả

---

← [Phase 0: Nền tảng](phase-0-nen-tang.md) · [Mục lục](README.md) · [Phase 2: Điều khiển và cú sút](phase-2-dieu-khien.md) →

> Trước khi giao việc, đọc [quy tắc giao việc](README.md#quy-tắc-giao-việc) và
> dùng [mẫu prompt](README.md#mẫu-prompt-giao-việc). Đừng gộp nhiều task vào một phiên.
