← [Phase 7: Hoạt ảnh và IK](phase-7-hoat-anh-ik.md) · [Mục lục](README.md) · [Phase 9: Âm thanh và cảm giác](phase-9-am-thanh-cam-giac.md) →

---

# PHASE 8 — Vòng lặp game và chế độ chơi

**6 task · tuần 23–26**

Phần code được của mốc **M5**, tiêu chí thoát: *"Loạt luân lưu đầy đủ từ menu tới kết quả,
không crash"*.

Hiện tại game bắt đầu ngay khi nạp scene và không bao giờ kết thúc: `MatchGameLoop` tự mở
lượt kế tiếp, hết trận thì bấm một nút là sang trận mới. Không có menu, không có đường ra,
không có chế độ, không có cài đặt, và không có gì chịu trách nhiệm khi hệ điều hành đưa app
xuống nền giữa lúc bóng đang bay. Phase này lấp đúng khoảng đó.

**Nguyên tắc của cả phase:** `MatchGameLoop` đã là nhạc trưởng của MỘT lượt sút và MỘT trận.
Đừng nhồi thêm việc vào nó. Mọi thứ ở đây đứng **bên trên** nó và nói chuyện với nó qua một
bề mặt hẹp — nếu một task nào phải sửa quá ba chỗ trong `MatchGameLoop`, đó là dấu hiệu ranh
giới đặt sai.

---

## T41 — Máy trạng thái ứng dụng và luồng màn hình

**Phụ thuộc:** T23, T24 · **Ước lượng:** ~3 ngày

Một trận đấu là một trạng thái của ứng dụng, không phải toàn bộ ứng dụng. Task này dựng lớp
trên cùng: Boot → Menu → chọn chế độ → Trận → Kết quả → về Menu, và mỗi mũi tên đều đi được
cả hai chiều mà không rò rỉ scene hay component.

Vì sao tách khỏi `KickSequencer`: `KickSequencer` quản 8 pha **trong một lượt sút**, vòng đời
tính bằng giây. Máy trạng thái này quản vòng đời tính bằng phút và có nạp/dỡ scene. Gộp hai
thứ đó sẽ cho ra một enum 20 giá trị mà không ai đọc nổi.

```csharp
namespace Eleven.App {
  public enum AppScreen : byte {
    Boot = 0, MainMenu = 1, ModeSelect = 2, Match = 3, Result = 4, Settings = 5, Paused = 6
  }

  public interface IAppFlow {
    AppScreen Current { get; }
    AppScreen Previous { get; }
    event System.Action<AppScreen, AppScreen> OnScreenChanged;

    bool CanGoTo(AppScreen target);
    void GoTo(AppScreen target);
    void GoBack();
  }
}
```

**Checklist nghiệm thu**
- [ ] Mọi chuyển màn hình hợp lệ được liệt kê trong một bảng dữ liệu, không nằm rải rác trong `if`
- [ ] `CanGoTo` từ chối chuyển sai (ví dụ Boot → Result) thay vì âm thầm cho qua
- [ ] Vào trận rồi thoát ra menu **20 lần liên tiếp** không tăng bộ nhớ quá 5% — đo bằng `SoakTestRunner` đã có
- [ ] Không còn `GameObject` nào của trận cũ sót lại sau khi về menu — test đếm số component chủ chốt
- [ ] Không cấp phát bộ nhớ khi ở trạng thái đứng yên (menu tĩnh) — 0 byte GC mỗi khung
- [ ] Đo trên máy thật: chuyển từ Menu vào Match dưới **1.5 giây** trên máy bậc C (ngân sách, chưa đo)

---

## T42 — Chế độ Arcade: chuỗi trận và tiến trình

**Phụ thuộc:** T41, T22, T24, T25 · **Ước lượng:** ~2 ngày · `TẤT ĐỊNH`

Một loạt luân lưu lẻ là một bài tập. Một chuỗi trận có thứ để mất mới là một trò chơi. Chế độ
này xâu các trận lại: thắng thì đi tiếp và đối thủ khó lên, thua thì hết chuỗi.

Ràng buộc quan trọng, chép từ [plan.md](../plan.md) mục 06: **độ khó chỉ được nằm ở `p_read`
và `t_commit`, không bao giờ ở `reach`.** Chuỗi trận khó dần bằng cách đổi `KeeperProfile`,
không bằng cách cho thủ môn tay dài ra.

```csharp
namespace Eleven.App.Arcade {
  public struct ArcadeRun {
    public int round;                 // vòng hiện tại, bắt đầu từ 1
    public int wins;
    public uint seed;                 // seed của cả chuỗi — cùng seed cho cùng chuỗi đối thủ
    public DifficultyLevel baseDifficulty;
  }

  public static class ArcadeRules {
    public static KeeperProfile OpponentFor(in ArcadeRun run, KeeperProfile easy,
                                            KeeperProfile medium, KeeperProfile hard);
    public static ArcadeRun ApplyResult(in ArcadeRun run, bool playerWon);
    public static bool IsRunOver(in ArcadeRun run);
    public static int ScoreOf(in ArcadeRun run);
  }
}
```

**Checklist nghiệm thu**
- [ ] Cùng `seed` cho ra cùng chuỗi đối thủ, từng bit, trên hai lần chạy khác nhau
- [ ] Độ khó tăng đơn điệu theo vòng — có test khẳng định `p_read` không bao giờ giảm khi vòng tăng
- [ ] `reachScale` **không đổi** giữa các vòng — test khẳng định thẳng điều này
- [ ] Thoát app giữa chuỗi rồi mở lại vẫn đúng vòng, đúng seed, đúng điểm — qua `MatchSave`
- [ ] Chuỗi kết thúc đúng lúc: thua là hết, không có đường đi tiếp ngầm
- [ ] `ArcadeRules` là hàm thuần, không `MonoBehaviour`, không đọc `Time`, 0 byte GC

---

## T43 — Chế độ người chơi làm thủ môn

**Phụ thuộc:** T41, T19, T21 · **Ước lượng:** ~3 ngày

Đảo vai: máy sút, người chơi bay người. Đây là chế độ dùng lại gần như toàn bộ hệ thống đã
có — `BayesianKeeperBrain` đổi vai thành bộ sinh tín hiệu cho người đọc, `ReachEnvelope` trở
thành ngân sách thời gian mà người chơi phải sống chung, `SaveResolver` vẫn là trọng tài.

Điểm thiết kế phải giữ: người chơi cũng **phải cam kết trước khi bóng rời chân**, đúng ràng
buộc sinh học ở [plan.md](../plan.md) mục 06. Cho người chơi phản xạ sau khi thấy bóng bay là
biến quả 11m thành trò bấm nút, và mọi cân bằng của Phase 3 mất nghĩa.

> **Đây là task nên cắt đầu tiên nếu tuần bị trượt.** Nó không nằm trong tiêu chí thoát của
> M5, và nó nhân đôi bề mặt cần đánh bóng ở M8. Ghi vào đây vì nó rẻ bất ngờ khi mọi mảnh đã
> có sẵn, không phải vì nó bắt buộc.

```csharp
namespace Eleven.App.KeeperMode {
  public struct KeeperInput {
    public int   targetCell;      // 0..8, ô người chơi chọn để đổ người
    public float commitTime;      // giây trước khi chạm bóng, dương = cam kết sớm
    public bool  isFullDive;
  }

  public interface IKeeperModeController {
    /// <summary>Tín hiệu hiển thị cho người chơi đọc — cùng nguồn mà AI dùng ở T17.</summary>
    KeeperCues VisibleCues { get; }
    bool HasCommitted { get; }

    void BeginKick(uint seed);
    bool TryCommit(in KeeperInput input);
    SaveResult Resolve(in BallState atCrossing, out float3 deflectVelocity);
  }
}
```

**Checklist nghiệm thu**
- [ ] Cam kết sau khi bóng đã rời chân bị **từ chối**, không phải bị phạt nhẹ — test khẳng định
- [ ] Cùng `SaveResolver` và cùng `ReachEnvelope` với chế độ thường — không có luật cản phá thứ hai trong repo
- [ ] Tín hiệu người chơi nhìn thấy đúng bằng tín hiệu AI nhận được — test so sánh `VisibleCues` với `KickerBoneCueSource.Sample`
- [ ] Tỉ lệ cản phá của một người chơi đoán ngẫu nhiên nằm quanh mức mà `ReachEnvelope` cho phép, không cao hơn — chạy 1000 lượt bằng bộ mô phỏng của `DifficultyTests`
- [ ] Chuyển vào và ra khỏi chế độ này không để lại trạng thái bẩn ở `GoalkeeperView`
- [ ] Đo trên máy thật: độ trễ từ chạm màn hình tới lúc thủ môn bắt đầu bay dưới **80 ms** (ngân sách, chưa đo)

---

## T44 — Cài đặt và hồ sơ người chơi

**Phụ thuộc:** T41, T24 · **Ước lượng:** ~2 ngày

Những thứ người chơi đổi một lần rồi quên: âm lượng từng lớp, bật/tắt rung, thuận chân trái
hay phải, độ nhạy vuốt, ngôn ngữ. Nhỏ nhưng phải làm sớm, vì mỗi tính năng thêm sau đều muốn
có một công tắc riêng, và nếu chưa có chỗ chứa thì công tắc sẽ mọc lung tung.

Thuận chân trái không phải chuyện trang trí: nó lật dấu của tín hiệu chân trụ mà thủ môn đọc
(T17/T37). Phải có test cho điều đó, nếu không người chơi thuận trái sẽ gặp một thủ môn đọc
ngược mọi quả.

```csharp
namespace Eleven.App.Settings {
  public struct PlayerSettings {
    public float masterVolume, sfxVolume, crowdVolume, uiVolume;  // 0..1
    public bool  hapticsEnabled;
    public bool  leftFooted;
    public float swipeSensitivity;    // hệ số nhân, kẹp [0.5, 2.0]
    public byte  languageId;
    public DifficultyLevel preferredDifficulty;

    public static PlayerSettings Default { get; }
  }

  public static class SettingsStore {
    public static bool TrySave(in PlayerSettings s, string path, out string error);
    public static bool TryLoad(string path, out PlayerSettings s, out string error);
  }
}
```

**Checklist nghiệm thu**
- [ ] File cài đặt hỏng hoặc thiếu trường thì rơi về mặc định, **không** làm sập app — test với file rác và file cắt cụt
- [ ] Đổi `leftFooted` lật đúng dấu tín hiệu chân trụ — test khẳng định thủ môn vẫn đọc đúng cột với người thuận trái
- [ ] `swipeSensitivity` bị kẹp trong dải hợp lệ, giá trị điên rồ không lọt vào `ShotMapper`
- [ ] Cài đặt còn nguyên sau khi tắt hẳn app và mở lại
- [ ] Mọi giá trị mặc định nằm đúng một chỗ (`PlayerSettings.Default`), không rải rác
- [ ] Đọc/ghi cài đặt không cấp phát trong lúc chơi — chỉ lúc vào/ra màn hình cài đặt

---

## T45 — Thống kê cú sút và trí nhớ dài hạn của thủ môn

**Phụ thuộc:** T42, T20 · **Ước lượng:** ~2 ngày · `TẤT ĐỊNH`

`ShotHistory` (T20) nhớ 20 cú gần nhất **trong một trận**. Task này nâng nó lên mức hồ sơ:
thủ môn của vòng sau biết thói quen bạn để lộ ở vòng trước. Đây là thứ biến chuỗi arcade từ
một dãy trận rời rạc thành một cuộc đấu trí kéo dài.

Kèm theo là mặt hiển thị: bản đồ 3×3 cho người chơi thấy chính mình đang nghiện góc nào —
thông tin đó vừa là phần thưởng, vừa là lời cảnh báo công bằng rằng máy cũng đang nhìn nó.

```csharp
namespace Eleven.App.Stats {
  public struct ShotProfile {
    public FixedList64Bytes<ushort> cellCounts;   // 9 ô, đếm dồn qua nhiều trận
    public ushort totalShots, totalGoals;

    public float FrequencyOf(int cell);
    public int MostUsedCell { get; }
  }

  public static class ShotProfileStore {
    public static ShotProfile Merge(in ShotProfile stored, in ShotHistory match);
    public static bool TrySave(in ShotProfile p, string path, out string error);
    public static bool TryLoad(string path, out ShotProfile p, out string error);
  }
}
```

**Checklist nghiệm thu**
- [ ] Trộn lịch sử một trận vào hồ sơ dài hạn là phép cộng thuần, cùng đầu vào cho cùng đầu ra
- [ ] Thủ môn dùng hồ sơ dài hạn qua đúng `memoryWeight` đã có, không thêm đường truyền thứ hai
- [ ] Có test đo được: sút 10 quả liên tiếp vào cùng một ô thì tỉ lệ bị cản ở ô đó **tăng lên** rõ rệt
- [ ] Bộ đếm không tràn: 65535 cú sút vẫn đúng, không quấn vòng âm thầm
- [ ] Xoá hồ sơ được từ màn hình cài đặt, và xoá xong thủ môn quên thật — test khẳng định
- [ ] Cấu trúc là `struct` với `FixedList`, 0 byte GC khi cập nhật mỗi lượt

---

## T46 — Vòng đời ứng dụng trên máy thật

**Phụ thuộc:** T41, T24 · **Ước lượng:** ~2 ngày

Trên điện thoại, app bị đưa xuống nền giữa lúc bóng đang bay là chuyện bình thường: có cuộc
gọi, có thông báo, có người khoá màn hình. Đo được ngày 2026-08-27 trên Pixel 7: khoá máy là
Unity nhận `APP_CMD_PAUSE` rồi `APP_CMD_STOP` trong vòng 7 mili giây, và mọi thứ trong
`Update` dừng giữa chừng.

Task này quyết định chuyện gì xảy ra khi quay lại, và làm cho nó không bao giờ mất tiến trình.

```csharp
namespace Eleven.App.Lifecycle {
  public interface IAppLifecycle {
    bool IsSuspended { get; }
    event System.Action OnSuspending;   // bắn TRƯỚC khi hệ điều hành dừng app
    event System.Action OnResumed;

    /// <summary>Ghi ngay lập tức mọi thứ không được phép mất. Phải chạy dưới 16 ms.</summary>
    void FlushCriticalState();
  }
}
```

**Checklist nghiệm thu**
- [ ] Khoá máy giữa lúc bóng đang bay, mở lại: lượt sút đó bị huỷ sạch sẽ và đá lại, **không** treo ở pha dở
- [ ] Tắt hẳn app giữa trận, mở lại: đúng tỉ số, đúng lượt, đúng bên đá — qua `MatchSave` đã có
- [ ] `FlushCriticalState` chạy dưới **16 ms** trên máy bậc C — đo trên máy thật, không ước lượng
- [ ] Không ghi file ở mỗi khung hình — chỉ ghi ở ranh giới lượt và lúc bị treo
- [ ] Âm thanh không kẹt tiếng khi quay lại — mọi `AudioSource` được dừng đúng cách lúc treo
- [ ] Kiểm trên **cả hai** nền: iOS và Android, ghi lại log của cả hai

---

## Thứ tự làm và chỗ song song được

```
T41 ──┬── T42 ── T45
      ├── T43        (cắt được nếu trượt tuần)
      ├── T44
      └── T46
```

- **T41 trước tiên và một mình** — bốn task còn lại đều treo vào nó.
- **T42, T44, T46 song song được**, ba người ba file khác nhau.
- **T45 sau T42**, vì hồ sơ dài hạn chỉ có nghĩa khi đã có chuỗi trận.
- **T43 độc lập**, và là task đầu tiên nên cắt nếu tuần bị trượt.

> **Cổng FUN nằm ở đây, không phải ở M2.** Tiêu chí gốc trong [plan.md](../plan.md) là
> *"20 lượt liên tiếp vẫn thấy vui khi chưa có art nào"*. Bản demo bóng xám ngày 2026-08-27
> đã chơi được nhưng chưa ai ngồi chơi đủ 20 lượt liên tiếp. Làm việc đó **trước khi** bắt
> đầu T42 — nếu 20 lượt mà chán thì thêm chế độ chỉ là nhân cái chán lên.

---

[Mục lục](README.md) · [Phase 7: Hoạt ảnh và IK](phase-7-hoat-anh-ik.md)
