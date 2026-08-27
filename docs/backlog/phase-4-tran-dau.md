← [Phase 3: Thủ môn](phase-3-thu-mon.md) · [Mục lục](README.md) · [Phase 5: Trình diễn](phase-5-trinh-dien.md) →

---

# PHASE 4 — Luật và trận đấu

**4 task · tuần 11–12**

Phần dễ nhất về kỹ thuật nhưng nhiều lỗi biên nhất.
Luật kết thúc sớm là chỗ hầu hết bản tự làm đều sai.

---

## T22 — Luật luân lưu, hàm thuần

**Phụ thuộc:** T02 · **Ước lượng:** ~1 ngày · `TẤT ĐỊNH`

Tách hoàn toàn khỏi Unity để test được hàng nghìn kịch bản trong vài giây.

```csharp
namespace Eleven.Match {
  public enum KickResult : byte { Pending = 0, Scored = 1, Missed = 2 }

  public struct ShootoutState {
    public FixedList64Bytes<KickResult> home, away;
    public bool homeKicksFirst;
    public int  TotalKicksTaken { get; }
    public bool IsHomeTurn      { get; }
  }

  public static class ShootoutRules {
    public const int RegulationKicks = 5;
    public static bool IsDecided(in ShootoutState s, out int winner);
    public static int  MaxPossibleRemaining(in ShootoutState s, bool home);
    public static ShootoutState ApplyKick(in ShootoutState s, KickResult r);
  }
}
```

**XONG — rà lại và chốt 2026-08-26.** Code đã có sẵn từ trước nhưng chưa ai đối chiếu với
checklist. File: [ShootoutRules.cs](../../Assets/_Project/Code/Match/ShootoutRules.cs) ·
test [ShootoutRulesTests.cs](../../Assets/_Project/Tests/EditMode/ShootoutRulesTests.cs) (26 test)
và [ShootoutExhaustiveTests.cs](../../Assets/_Project/Tests/EditMode/ShootoutExhaustiveTests.cs)
(1 test chạy 2 lần theo `[Values]`). Cả 27 nằm trong lượt EditMode **260/260 xanh, 81 s**.

**Checklist nghiệm thu**
- [x] **Kết thúc sớm đúng:** dẫn 3–0 sau 3 lượt, đối thủ còn 2 lượt → đã phân định, không đá tiếp — **XANH**: `EarlyEnd_Home3_0_After3Rounds_Classic`. Kèm hai ô canh chiều ngược lại (`EarlyEnd_NotYet_At3_1_AfterThreeRounds_TieStillPossible`, `EarlyEnd_NotYet_WhenLeadEqualsOpponentRemainingPlusZero`) — thiếu chúng thì một hàm "lúc nào cũng phân định" vẫn qua được ô này.
- [x] Luân lưu chết: sau 5 lượt hoà, đá từng cặp, chỉ phân định khi *cả hai* đã đá đủ trong lượt đó — **XANH**: `SuddenDeath_MidPair_NeverDecided_EvenIfOneScored`, `SuddenDeath_PairComplete_DifferentResults_Decides`, `SuddenDeath_PairComplete_BothMissed_Continues`. Đây đúng chỗ bản sinh máy từng sai: điều kiện cũ `Math.Min(th, ta) - RegulationKicks < 1` tuyên bố thắng khi **chỉ một** đội đá xong lượt chết (th=6, ta=5). Đã thay bằng so `th != ta` trực tiếp, lý do ghi ngay trong code.
- [x] Thứ tự đá đúng: xen kẽ, đội đá trước cấu hình được — **XANH**: `IsHomeTurn_Alternates_HomeFirst`, `IsHomeTurn_Alternates_AwayFirst`, `ApplyKick_RoutesToCorrectSide_ByHomeKicksFirst`.
- [x] Test vét cạn mọi kịch bản tới 10 lượt bằng vòng lặp — không có trạng thái nào cho kết quả mâu thuẫn — **XANH**: `Exhaustive_AllScenarios_UpTo10KicksPerTeam_AllInvariantsHold` duyệt đệ quy toàn bộ cây quyết định tới 10 lượt/đội cho **cả hai** giá trị `homeKicksFirst`, kiểm 7 bất biến trên từng trạng thái. `BruteForce_VisitedEnoughStates` chốt số trạng thái đã duyệt — không có nó, một bộ duyệt hỏng (thoát sớm ở nút gốc) vẫn "không tìm thấy mâu thuẫn" và vẫn xanh.
- [x] Không có `using UnityEngine` trong file luật — **XANH**: `ShootoutRules.cs` chỉ có `using System;` và `using Unity.Collections;` (Unity.Collections là thư viện dữ liệu, không kéo theo engine).
- [x] Ít nhất 25 test, gồm 6 kịch bản kết thúc sớm khác nhau — **XANH**: 27 test, 6 hàm `EarlyEnd_*` (4 ca phân định sớm thật + 2 ca **chưa** được phân định).

---

## T23 — Máy trạng thái lượt sút

**Phụ thuộc:** T22, T09, T19 · **Ước lượng:** ~2 ngày

```csharp
namespace Eleven.Match {
  public enum KickPhase { Placing, Aiming, RunUp, Contact, Flight,
                          Resolution, Reaction, Complete }

  public interface IKickSequencer {
    KickPhase Phase { get; }
    event Action<KickPhase, KickPhase> OnPhaseChanged;
    void BeginKick(uint seed);
    void Abort();
  }
}
```

**Checklist nghiệm thu**
- [ ] Mỗi lượt sút nhận một `seed`, ghi lại được để tái hiện
- [ ] Chuyển pha không bao giờ nhảy cóc — có test khẳng định thứ tự
- [ ] Thoát app giữa pha `Flight` rồi vào lại không làm hỏng trạng thái
- [ ] Chạy được 200 lượt liên tiếp không tăng bộ nhớ — kiểm bằng Profiler
- [ ] `Abort()` gọi ở bất kỳ pha nào đều về `Complete` sạch sẽ

---

## T24 — Lưu tiến trình

**Phụ thuộc:** T22 · **Ước lượng:** ~1 ngày

**XONG 2026-08-26 — cả lớp dữ liệu lẫn lớp vòng đời.** File:
[MatchSave.cs](../../Assets/_Project/Code/Match/MatchSave.cs) (dữ liệu) ·
[MatchSaveLifecycle.cs](../../Assets/_Project/Code/Match/MatchSaveLifecycle.cs) (vòng đời) ·
test [MatchSaveTests.cs](../../Assets/_Project/Tests/EditMode/MatchSaveTests.cs) (11 test EditMode) và
[MatchSaveLifecycleTests.cs](../../Assets/_Project/Tests/PlayMode/MatchSaveLifecycleTests.cs) (11 test PlayMode).
Chia hai lớp là cố ý: `MatchSave` không biết *khi nào* cần lưu, `MatchSaveLifecycle` không biết
*lưu cái gì* — nhờ vậy toàn bộ phần định dạng/checksum test được ở EditMode, chỉ phần bắt tín hiệu
hệ điều hành mới phải vào PlayMode.

`MatchSave` là `static class` thuần, không `MonoBehaviour`, không `Coroutine`. Nó chỉ biết
`TrySave` / `TryLoad` với một `string filePath`. Chỗ duy nhất chạm `UnityEngine.Application` là
`DefaultPath()` — nên toàn bộ phần còn lại test được ở EditMode, không cần vào Play.
Định dạng: `key=value` văn bản thuần, xuống dòng `\n`, `CultureInfo.InvariantCulture` ở mọi chỗ
đọc/ghi số (nếu không, máy đặt locale tiếng Việt sẽ ghi `1,5` rồi máy khác đọc thành lỗi).

**Checklist nghiệm thu**
- [x] Có số phiên bản schema, đọc được file của phiên bản cũ hơn — **XANH 2026-08-26**: `CurrentSchemaVersion = 2`. `DocFileSchemaV1_MacDinhHomeKicksFirstLaTrue` dựng tay một file v1 (thiếu hẳn trường `homeKicksFirst`) và khẳng định nó đọc ra được với mặc định `true`. `TuChoiFile_CoSoPhienBanLonHonHienTai` chặn chiều ngược lại: file mới hơn thì **từ chối**, không đoán bừa.
- [x] Ghi kiểu nguyên tử: ghi file tạm rồi đổi tên, không ghi đè trực tiếp — **XANH 2026-08-26**: ghi `path.tmp`, `stream.Flush(true)` (ép xuống đĩa thật, không chỉ vào cache OS), rồi `File.Replace` nếu file đích đã có / `File.Move` nếu chưa. `SauKhiLuuXong_KhongConFileTmpSotLai` và `GhiDeLenFileDaTonTai_VanThanhCongVaDungDuLieu` canh cả hai nhánh.
- [x] File hỏng hoặc bị cắt cụt → về mặc định, không crash — **XANH 2026-08-26, nửa còn thiếu đã xong**. Nửa "không crash" vốn đã chốt ở lớp dữ liệu (`FileBiCatCut_TryLoadTraFalseVaKhongNem`, `FileToanRacNhiPhan_TryLoadTraFalseVaKhongNem`, `DuongDanThuMucKhongTonTai_TrySaveTraFalseVaKhongNem`). Nửa "về mặc định" giờ có chủ: [MatchSaveLifecycle.cs](../../Assets/_Project/Code/Match/MatchSaveLifecycle.cs) là chỗ đầu tiên trong dự án thật sự gọi tới `MatchSave`, và khi `TryLoad` trả `false` nó đặt `State` về trận rỗng (`homeKicksFirst = true`) kèm `LogWarning`. `FileHong_VeTranRong_KhongCrash` ghi rác vào file rồi khẳng định cả ba: không ném exception, `home.Length == 0`, `away.Length == 0`.
- [x] Lưu khi app vào nền (`OnApplicationPause`), không chỉ khi thoát — **XANH 2026-08-26**: `MatchSaveLifecycle` bắt cả **ba** tín hiệu, vì không tín hiệu nào một mình đủ tin — `OnApplicationPause(true)` đáng tin nhất trên Android, `OnApplicationFocus(false)` trên iOS, còn `OnApplicationQuit()` thì trên di động **nhiều khi không bao giờ được gọi** vì hệ điều hành giết thẳng tiến trình. Test PlayMode [MatchSaveLifecycleTests.cs](../../Assets/_Project/Tests/PlayMode/MatchSaveLifecycleTests.cs) (11 test, nằm trong lượt **26/26 xanh**) canh từng nhánh: `PauseTrue_ThiLuu`, `FocusMat_ThiLuu`, `Thoat_ThiLuu`, và `PauseFalse_KhongLuu` cho chiều ngược lại. Ba sự kiện có thể bắn liên tiếp trong **một** lần chuyển nền, nên có cờ `dirty` — `PauseRoiFocusMat_ChiLuuMotLan` chứng minh chỉ chạm đĩa một lần, `SetStateGiuaChung_ThiLuuLanNua` chứng minh cờ không kẹt ở trạng thái "sạch".
- [x] Có checksum để phát hiện sửa file thủ công — **XANH 2026-08-26**: FNV-1a 32-bit trên phần thân, ghi ở dòng đầu. `SuaMotKyTuTrongPhanThan_ChecksumPhatHienVaTuChoi` sửa **đúng một ký tự** rồi khẳng định `TryLoad` từ chối. Lúc đọc có chuẩn hoá `\r\n → \n` trước khi băm, nếu không thì file đi qua Git trên Windows sẽ tự hỏng checksum dù nội dung không đổi.

> **Đã dọn khi rà lại bản giao (lớp vòng đời, 2026-08-26):** lỗi nặng nhất là
> `LogAssert.ignoreFailingMessages = true` được bật trong test file-hỏng mà **không bao giờ tắt**.
> Đó là cờ **tĩnh**, sống xuyên suốt cả lượt chạy — nghĩa là mọi test chạy sau nó
> (`BallDriverTests`, `DeviceAcceptanceTests`…) sẽ âm thầm bỏ qua mọi log lỗi, và một test đáng lẽ
> phải đỏ vì Unity log Error sẽ xanh mà không ai biết. Đã thay bằng `LogAssert.Expect` (vừa không
> đánh đỏ, vừa **bắt buộc** cảnh báo phải xuất hiện) và trả cờ về `false` trong `TearDown` sau mọi
> test. Đây là lần thứ ba một bản giao của 9router mắc đúng một kiểu lỗi: **test xanh cả khi thứ nó
> đo đã hỏng hoàn toàn.** Thêm hai test bản giao còn thiếu: `Thoat_ThiLuu` (nhánh
> `OnApplicationQuit` chỉ được gọi ở ca "không có gì thay đổi", nên chưa ai chứng minh nó thật sự
> lưu) và `GhiThatBai_GiuNguyenDirty_DeConCoHoiThuLai` (component cố ý chỉ xoá cờ `dirty` khi ghi
> **thành công** — quyết định đúng nhưng chưa ai canh; ai "dọn code" thành xoá vô điều kiện sẽ làm
> một lần ghi hỏng nuốt luôn cơ hội lưu ở lần chuyển nền kế tiếp).
>
> **Đã dọn khi rà lại bản giao (lớp dữ liệu):** bản nhận về có struct `MatchSaveData` không ai dựng
> và không ai đọc — xoá. Giới hạn số lượt sút cũng viết cứng số `62`; thay bằng
> `default(FixedList64Bytes<KickResult>).Capacity` để sau này đổi sang `FixedList128Bytes` thì
> giới hạn tự nới theo chứ không âm thầm sai.

---

## T25 — Cấu hình độ khó

**Phụ thuộc:** T16 · **Ước lượng:** ~4h

**Checklist nghiệm thu**
- [ ] 3 `KeeperProfile` asset: Dễ, Thường, Khó
- [ ] Cả ba đều có `reachScale` trong `[0.92, 1.06]` — độ khó nằm ở đọc vị, không ở tốc độ
- [ ] Chạy mô phỏng 1000 lượt mỗi profile, tỉ lệ cản phá lần lượt rơi vào `18±3%`, `28±3%`, `38±3%`
- [ ] Đổi profile lúc đang chạy có hiệu lực ở lượt kế tiếp

---

← [Phase 3: Thủ môn](phase-3-thu-mon.md) · [Mục lục](README.md) · [Phase 5: Trình diễn](phase-5-trinh-dien.md) →

> Trước khi giao việc, đọc [quy tắc giao việc](README.md#quy-tắc-giao-việc) và
> dùng [mẫu prompt](README.md#mẫu-prompt-giao-việc). Đừng gộp nhiều task vào một phiên.
