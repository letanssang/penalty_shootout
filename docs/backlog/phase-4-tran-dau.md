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

**XONG 2026-08-27.** File: [KickPhase.cs](../../Assets/_Project/Code/Match/KickPhase.cs) (enum +
`KickPhaseDurations`) · [IKickSequencer.cs](../../Assets/_Project/Code/Match/IKickSequencer.cs)
(interface + `KickSequencerSnapshot`) · [KickSequencer.cs](../../Assets/_Project/Code/Match/KickSequencer.cs) ·
test [KickSequencerTests.cs](../../Assets/_Project/Tests/EditMode/KickSequencerTests.cs) (47 test),
nằm trong lượt EditMode **369/369 xanh, 82 s**.

Máy trạng thái thuần C#: không `MonoBehaviour`, không `Coroutine`, không đọc `Time.deltaTime` —
thời gian **bơm vào** qua `Tick(dt)`. Nhờ vậy 200 lượt sút chạy xong trong một phần nghìn giây ở
EditMode thay vì 200 lượt thời gian thực, và test tua thời gian được tuỳ ý.

`Complete` vừa là pha cuối vừa là **trạng thái nghỉ** — cố ý, để `BeginKick` chỉ có đúng một điều
kiện hợp lệ (`Phase == Complete`) thay vì phải phân biệt "chưa bắt đầu" với "đã xong".

**Checklist nghiệm thu**
- [x] Mỗi lượt sút nhận một `seed`, ghi lại được để tái hiện — **XANH 2026-08-27**: `BeginKick_GhiLaiSeed_VaMoPhaPlacing`, `HaiMuoiLuot_MoiLuotMotSeed_DeuGhiDungSeedCuaMinh` (20 lượt liên tiếp, mỗi lượt một seed khác, khẳng định `CurrentSeed` luôn là seed của **lượt hiện tại**), `CungSeed_ChoRaCungChuoiPha_TaiHienDuocTungBuoc`. Hai ô canh chiều ngược lại: `BeginKick_KhiDangGiuaLuot_BiBoQuaHoanToan_KhongDeSeedCu` (gọi `BeginKick` giữa lượt phải bị bỏ qua **hoàn toàn**, kể cả không được đè seed cũ) và `Abort_GiuNguyenSeed_DeConGhiLogLuotHuy` (`Abort` xoá sạch mọi thứ **trừ** seed — mất seed là mất luôn khả năng dựng lại lượt bị huỷ trong log).
- [x] Chuyển pha không bao giờ nhảy cóc — có test khẳng định thứ tự — **XANH 2026-08-27**: `TickNhoDan_ChuoiPha_DungThuTuTuyetDoi` (nhích 1/240 s) và `TickMotLanRatLon_VanBanSuKienTungBuoc_KhongNhayThangToiComplete` (`Tick(100f)` từ `Placing` vẫn phải bắn đủ 7 sự kiện đúng thứ tự, **không** nhảy thẳng tới `Complete`) — đây là chỗ dễ hỏng nhất, một `Tick` bị khựng vì hệ điều hành lag sẽ nuốt cả pha `Contact` (0,05 s) nếu bản cài đặt gán thẳng pha cuối. `MoiSuKien_PhaCu_LuonBangPhaMoi_CuaSuKienTruoc` khoá chuỗi sự kiện thành một dây liền không đứt đoạn, `TrongHandler_DocPhase_ThayNgayPhaMOI_KhongPhaiPhaCu` chốt rằng bên trong handler `Phase` đã là pha mới (nếu không, mọi bên nghe đều đọc trạng thái cũ). Kèm hai bẫy thời gian: `Tick_GiuPhanDuThoiGian_KhongGanVeKhong` và `TongThoiGianMotLuot_BangDungTongThoiLuongCacPha_KhongTroi` — gán `PhaseElapsed = 0` sau mỗi pha thì mỗi lượt sút trôi thêm vài chục ms, và `ThoiLuongBangKhong_VanTienDungMotBuocMoiVong_KhongLapVoHan` chặn vòng lặp vô hạn khi ai đó đặt thời lượng một pha bằng 0.
- [x] Thoát app giữa pha `Flight` rồi vào lại không làm hỏng trạng thái — **XANH 2026-08-27**: `ThoatGiuaFlight_VaoLai_VeDauFlight_GiuNguyenSeedVaIntent` và `SauKhiKhoiPhucGiuaFlight_LuotChayTiepBinhThuongToiComplete`. Quy tắc tua lại đã chốt: `{Placing, Aiming, RunUp}` → tua về **đầu lượt** (chưa cam kết cú sút nào, cùng seed nên đá lại y hệt); `{Contact, Flight}` → về **đầu `Flight`** với `intent` đã lưu (trạng thái quả bóng không nằm trong sequencer nên không thể "bay tiếp", nhưng mô phỏng tất định nên đá lại đường bay cho ra đúng kết quả cũ); `{Resolution, Reaction}` → giữ `Resolution` **nếu** đã có kết quả, còn `Pending` thì coi như chưa đá xong và quay về `Flight` (`Restore_TuReaction_MaOutcomeConPending_ThiPhaiDaLaiFlight_KhongDuocBiaKetQua`). Bản lưu hỏng cũng có chủ: `Restore_ByteNgoaiDai_CoiNhuComplete_Pending_KhongNemException` và `Restore_OutcomeNgoaiDai_OPhaResolution_CoiLaPending_NenDaLaiFlight` — file bị sửa tay làm crash lúc mở app là mất trắng dữ liệu người chơi.
- [x] Chạy được 200 lượt liên tiếp không tăng bộ nhớ — kiểm bằng Profiler — **XANH 2026-08-27**: `ChayHaiTramLuotLienTiep_KhongCapPhatMotByteGC_Nao` bọc 200 lượt × 8 bước trong `Assert.That(..., Is.Not.AllocatingGCMemory())` — ràng buộc này do **Profiler** của Unity chống lưng và đỏ ngay từ **byte đầu tiên** được cấp phát, chặt hơn hẳn kiểu đo `GC.GetTotalMemory` trước/sau vốn có thể trôi qua khe giữa hai lần thu gom. `ChayHaiTramLuotLienTiep_CoNguoiDangKySuKien_VanKhongCapPhatGC` canh riêng đường sự kiện (`Action<,>` gọi qua field đã cache, không tạo delegate mới mỗi lần bắn), `ChayHaiTramLuotLienTiep_TrangThaiCuoiVanSach` khẳng định sau 200 lượt trạng thái vẫn đúng chứ không chỉ "im lặng".
- [x] `Abort()` gọi ở bất kỳ pha nào đều về `Complete` sạch sẽ — **XANH 2026-08-27**: `Abort_OMoiPha_DeuVeCompleteSachSe` chạy `[TestCase]` qua **cả 7 pha đang chạy**, mỗi ca dựng trạng thái "bẩn" thật (đã có `intent`, đã có `outcome`) rồi khẳng định sau `Abort` thì `Phase == Complete`, `Outcome == Pending`, `HasIntent == false`, `PhaseElapsed == 0`. `Abort_KhiDaOComplete_NoOp_KhongBanSuKien` và `Abort_GoiHaiLan_LienTiep_KhongBanSuKienThuHai` canh chiều ngược lại, `Abort_XongThi_BeginKickMoi_ChayDuocNgay` chốt rằng huỷ xong vẫn đá tiếp được ngay.

> **Đã dọn khi rà lại bản giao:** `Abort()` bản nhận về **bắn sự kiện trước rồi mới dọn state**.
> Hậu quả xếp theo mức độ: (1) mọi handler đọc `Phase` trong lúc nhận `(phaCũ, Complete)` đều thấy
> pha **cũ**, ngược hẳn với đường đi qua `ChangePhase`; (2) handler gọi `BeginKick` ngay trong sự
> kiện — chính là cách nối lượt kế tiếp — sẽ bị mấy dòng gán phía sau **ghi đè im lặng**, lượt mới
> biến mất không dấu vết; (3) handler gọi lại `Abort()` thì chốt chặn `_phase == Complete` chưa kịp
> đặt nên **đệ quy vô hạn**. Đã đổi thành dọn sạch trước, bắn sự kiện sau — chuỗi sự kiện quan sát
> từ ngoài không đổi, chỉ trạng thái nhìn thấy *bên trong* handler là đúng lên. Thêm chốt chặn
> `_ticking` cho `Tick`: không có nó, handler mở lượt mới lúc nhận `(Reaction, Complete)` sẽ bị vòng
> lặp cũ vẫn đang chạy đem `PhaseElapsed` thừa của lượt trước áp vào lượt mới.
>
> **Kiểm tra ngược bằng đột biến (mutation testing).** Ba bản giao trước của 9router đều xanh mà
> không đo gì, nên lần này bộ test bị đem ra thử lửa: sửa hỏng `KickSequencer.cs` 8 kiểu rồi xem
> có test nào đỏ không. Kết quả — M1 vứt phần dư thời gian (`_phaseElapsed = 0f`): **6 test đỏ**;
> M2 chỉ bắn sự kiện khi tới `Complete` (nhảy cóc): **12 đỏ**; M3 trả `Abort` về thứ tự cũ của
> 9router: **Unity chết hẳn bằng SIGILL** — tràn stack đúng như dự đoán ở (3); M4 `Restore` giữ
> nguyên pha thay vì tua về `Flight`: **1 đỏ**; M5 tự bịa `Missed` khi hết `Flight` mà chưa ai báo
> kết quả: **3 đỏ**; M6 `Abort` quên xoá `HasIntent`: **1 đỏ**; M8 bỏ kiểm tra `outcome` hỏng:
> **1 đỏ**. Chỉ M7 (bỏ kiểm tra `phase` hỏng) sống sót, và đó là **đột biến tương đương** chứ không
> phải lỗ hổng test: nhánh `default:` của `switch` trong `Restore` đã nuốt mọi giá trị ngoài dải
> 0–7 về `Complete` rồi, nên `IsValidPhase` là lớp phòng thủ thứ hai, không phải nhánh sống.
> **7/7 đột biến không-tương-đương đều bị giết.**

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

**XONG MỘT PHẦN 2026-08-27** — 3/4 mục đạt. Mục tỉ lệ cản phá KHÔNG đạt và **bị chặn bởi
T21**; chi tiết bên dưới. File: `DifficultyLevel.cs`, `DifficultySelector.cs`,
`KeeperProfileAssetGenerator.cs`, `DifficultyTests.cs` (27 test, 26 xanh + 1 Ignore có lý do).

**Checklist nghiệm thu**
- [x] 3 `KeeperProfile` asset: Dễ, Thường, Khó — **XANH 2026-08-27**
      `Assets/_Project/Settings/KeeperProfile-{Easy,Medium,Hard}.asset`, sinh bằng menu
      *Eleven > Phase 4 > Generate Keeper Profiles*. Chứng minh bởi
      `BaAsset_KeeperProfile_TonTaiTrongThuMucSettings` và
      `Asset_KhopTungFieldVoiFactory_MotNguonSuThat` — test thứ hai so từng field của asset
      với factory trong `KeeperProfile`, nên chỉnh tay asset trong Inspector sẽ làm test đỏ.
- [x] Cả ba đều có `reachScale` trong `[0.92, 1.06]` — **XANH 2026-08-27**
      0.92 / 1.00 / 1.06. `ReachScale_NamTrongDai_0_92_Toi_1_06` kiểm dải;
      `DoKho_NamODocVi_KhongPhaiOTocDo` kiểm đúng cái *ý* của mục này: `readAccuracy` phải
      tăng đều giữa ba bậc, và chênh lệch `reachScale` phải nhỏ hơn chênh lệch `readAccuracy`.
- [ ] Chạy mô phỏng 1000 lượt mỗi profile, tỉ lệ cản phá `18±3%`, `28±3%`, `38±3%`
      — **CHƯA ĐẠT.** Bộ mô phỏng đã xây xong và chạy được (`PenaltySim` trong
      `DifficultyTests.cs`), nhưng số đo ra là **2.1% / 8.4% / 10.8%**. Xem phần dưới.
- [x] Đổi profile lúc đang chạy có hiệu lực ở lượt kế tiếp — **XANH 2026-08-27**
      `DifficultySelector`: `Request()` xếp hàng vào `Pending`, `CommitPending()` gọi ở đầu
      lượt mới mới áp vào `Current`. 8 test phủ: không đổi profile giữa lượt
      (`Request_KhongDoiProfileCuaLuotDangChay`, so cả `AreSame` trên `ActiveProfile`),
      chỉ lần Request cuối có tác dụng, commit lần hai không tự đổi tiếp, không cấp phát GC,
      slot asset null rơi về hằng số trong code, bậc ngoài dải trả về Thường.

> **Vì sao đổi bậc phải trễ một lượt.** Áp ngay lập tức thì thủ môn đang bay người theo tính
> toán của hồ sơ cũ sẽ đột ngột dùng `reachScale` mới ở giữa cú sút. Quả đó mất tính tất định
> — phát lại từ cùng seed cho kết quả khác — và bản lưu T24 ghi lại một lượt không thể tái dựng.

---

### Mục 3 chưa đạt: số đo, nguyên nhân, và phụ thuộc chưa khai báo

Bộ mô phỏng dùng **đúng code đang chạy**, không mô hình hoá lại: `BallSolver` /
`TrajectoryPredictor` cho đường bóng, `GoalGeometry` cho ô lưới, `BayesianKeeperBrain` cho
đọc vị, `SimpleKeeperController` cho cam kết, `ReachEnvelope` cho tầm với. Chỉ hai thứ phải
tự dựng vì repo chưa có: mô hình **người sút** (chọn ô theo phân bố penalty thật, lực 25±3 m/s,
sai số ngắm 0.28 m) và **luật cản phá** tạm (`cam kết đúng ô bóng vào` **và** `kịp tầm với`).

Cue được sinh **không nhiễu**, đặt đúng tâm bảng tra của chính `BayesianKeeperBrain`. Đây là
cố ý: nó cho thủ môn điều kiện đọc vị lý tưởng, nên mọi con số dưới đây là **trần**, không
phải kỳ vọng.

1000 lượt, seed 20260827, gọi `TryCommit` tại `commitOffsetMs` của từng hồ sơ:

| bậc | cản phá | mục tiêu | đọc đúng ô | confidence tb | bị ép đứng giữa |
|---|---|---|---|---|---|
| Dễ | **2.1%** | 18±3% | 38.2% | 0.099 | 756/843 |
| Thường | **8.4%** | 28±3% | 40.8% | 0.111 | 745/843 |
| Khó | **10.8%** | 38±3% | 41.3% | 0.130 | 698/843 |

Thứ tự Dễ < Thường < Khó thì đúng, nhưng cả ba đều thấp hơn mục tiêu 3–4 lần. **Ba nguyên nhân,
đều đã đo chứ không phải suy đoán:**

1. **Cue hàng gần như không mang thông tin.** Chỉ có `runUpLength` phân biệt ba hàng, với
   `SigmaRunUp = 1.5` m trong khi tâm ba hàng cách nhau 1.0 m, và trọng số thấp nhất bảng
   (0.3). Kết quả: đoán cột gần như chính xác, đoán hàng gần như ngẫu nhiên, nên đọc đúng ô
   chặn cứng quanh **41%** với mọi hồ sơ.
2. **`readAccuracy` gần như không điều khiển được gì.** Nó chỉ vào một chỗ duy nhất:
   `sharpness = 1 + readAcc² × 10` trong hàm softmax, và giá trị đó đã bão hoà. Chênh lệch
   đọc đúng ô giữa bậc Thường (0.52) và bậc Khó (0.72) chỉ là **0.5 điểm phần trăm** — 40.8%
   so với 41.3%. Nút vặn độ khó gần như không nối vào đâu cả.
3. **Ngưỡng của `SimpleKeeperController` nằm trên hẳn thang confidence mà brain thực sự sinh ra.**
   `k_ConfidenceThreshold = 0.45` và `k_VeryLowConfidence = 0.20`, trong khi confidence trung
   bình đo được là 0.099 / 0.111 / 0.130. Hệ quả: nhánh "confidence quá thấp thì đứng giữa"
   nuốt **83–90%** số quả. Thủ môn phần lớn thời gian chỉ đứng yên giữa khung.

**Nguyên nhân của cả ba nguyên nhân trên:** `KeeperControllerTests` chỉ nạp confidence dựng
sẵn (0.9, 0.2, 0.10…) vào controller và **chưa bao giờ cho output thật của
`BayesianKeeperBrain` chạy qua nó**. Từng đơn vị đều xanh, còn mối nối giữa hai đơn vị thì
chưa ai đo. T25 là chỗ đầu tiên nối hai thứ đó lại, và mối nối gãy ngay.

> **Phát hiện thêm về mốc cam kết trong T19.** `deadlineMargin = reactionMs + TimeToReach(bestCell)`
> — với bậc Thường và ô góc là 0.24 + 0.60 = **0.84 s**, dài hơn cả pha `runUp` (0.90 s, phần
> nhìn thấy được còn ngắn hơn). Nên `outOfTime` đúng gần như ngay từ khung hình đầu: gọi
> `TryCommit` mỗi khung như tài liệu của chính controller mô tả thì thủ môn chốt khi trung bình
> vẫn còn 0.53–0.58 s, tức chưa kịp nhìn gì, và tỉ lệ cản phá tụt xuống 2.1% / 8.5% / 8.5% —
> bậc Khó rơi xuống ngang bậc Thường. Đã ghim bằng test đặc tả hiện trạng
> `GoiMoiKhungHinh_ThuMonBiEpDungGiuaGanNhuMoiQua_HIENTRANG`: **test đó đỏ nghĩa là mốc cam kết
> đã được sửa** — lúc đó xoá nó đi, đừng nới ngưỡng.

**Phụ thuộc chưa khai báo: mục này cần T21 (phân giải pha cản phá).** Luật cản phá tạm ở trên
đòi đoán trúng *đúng ô*; luật thật của T21 xét giao cắt giữa quỹ đạo bay người và quỹ đạo
bóng, nên cản được cả những quả sát ranh giới hai ô. Thay luật tạm bằng luật "cùng cột"
(ước lượng thô cho giao cắt thật) và bỏ cổng confidence thì số đo lên **11.7% / 24.9% / 52.1%**
— đúng vùng độ lớn của mục tiêu. Tức là **mốc 18/28/38 là hợp lý, chỉ đang bị đo sớm**.

Vì vậy test `MoPhong1000Luot_TiLeCanPha_DungBangMucTieu` được viết đầy đủ nhưng đánh
`[Ignore]` kèm lý do, thay vì bị nới cho xanh. **Không hiệu chỉnh thông số ba hồ sơ ở thời
điểm này** — hiệu chỉnh bây giờ là hiệu chỉnh vào một luật cản phá sắp bị thay, và sẽ phải
làm lại toàn bộ sau T21.

**Việc còn lại của T25, làm sau khi T21 xong:** bật lại test dải mục tiêu, sửa mốc cam kết của
T19, nối `readAccuracy` vào một chỗ thực sự có tác dụng (hoặc thêm cue phân biệt hàng), rồi
mới vặn thông số ba hồ sơ cho khớp 18/28/38.

---

← [Phase 3: Thủ môn](phase-3-thu-mon.md) · [Mục lục](README.md) · [Phase 5: Trình diễn](phase-5-trinh-dien.md) →

> Trước khi giao việc, đọc [quy tắc giao việc](README.md#quy-tắc-giao-việc) và
> dùng [mẫu prompt](README.md#mẫu-prompt-giao-việc). Đừng gộp nhiều task vào một phiên.
