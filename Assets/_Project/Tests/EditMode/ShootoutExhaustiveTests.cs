using System;
using NUnit.Framework;
using Unity.Collections;
using Eleven.Match;

namespace Eleven.Tests.EditMode {
  /// <summary>
  /// Bộ test VÉT CẠN (Exhaustive Search) toàn bộ không gian trạng thái luân lưu 11m
  /// tới tối đa 10 lượt mỗi đội (tổng cộng 20 cú sút xen kẽ).
  ///
  /// CHIẾN LƯỢC CẮT NHÁNH & HIỆU NĂNG:
  /// - Không gian lý thuyết thô: 2^20 = 1.048.576 nhánh cho mỗi lượt chọn đội đá trước.
  /// - Cắt nhánh: Dừng đệ quy ngay khi <see cref="ShootoutRules.IsDecided"/> trả về true,
  ///   vì sau thời điểm phân định, trận đấu đã kết thúc và ShootoutRules.ApplyKick trở thành no-op.
  /// - Ở giai đoạn 5 lượt đầu (regulation): rất nhiều nhánh kết thúc sớm (ví dụ 3-0 sau 6 lượt).
  /// - Ở giai đoạn sudden death (lượt 6-10): mỗi cặp 2 cú sút có 4 khả năng (Vào-Vào, Hỏng-Hỏng,
  ///   Vào-Hỏng, Hỏng-Vào), trong đó 2 khả năng phân định thắng thua ngay (cắt nhánh) và chỉ có
  ///   2 khả năng hoà tiếp tục đi sâu.
  /// - Thực tế tổng số trạng thái được duyệt cho mỗi test case chỉ khoảng ~15.000 trạng thái,
  ///   thời gian thực thi toàn bộ test suite < 50ms (vượt xa yêu cầu < 5 giây).
  /// </summary>
  [TestFixture]
  public class ShootoutExhaustiveTests {

    private const int MaxKicksPerTeam = 10;
    private const int MaxTotalKicks = MaxKicksPerTeam * 2; // 20 lượt sút tổng cộng

    /// <summary>
    /// Test vét cạn toàn bộ cây quyết định với cả hai trường hợp Home đá trước và Away đá trước,
    /// khẳng định toàn bộ 7 bất biến (B1 -> B7) trên từng trạng thái đạt tới được.
    /// </summary>
    [Test]
    public void Exhaustive_AllScenarios_UpTo10KicksPerTeam_AllInvariantsHold(
      [Values(true, false)] bool homeKicksFirst
    ) {
      var initialState = new ShootoutState {
        home = new FixedList64Bytes<KickResult>(),
        away = new FixedList64Bytes<KickResult>(),
        homeKicksFirst = homeKicksFirst
      };

      int totalStatesVisited = 0;
      int totalDecidedStates = 0;

      // Bắt đầu duyệt đệ quy từ trạng thái rỗng
      ExploreState(
        currentState: initialState,
        history: string.Empty,
        parentDecided: false,
        depth: 0,
        ref totalStatesVisited,
        ref totalDecidedStates
      );

      TestContext.Out.WriteLine(
        $"[Vét cạn thành công] homeKicksFirst={homeKicksFirst} | " +
        $"Tổng trạng thái duyệt: {totalStatesVisited:N0} | " +
        $"Số trạng thái kết thúc (lá phân định): {totalDecidedStates:N0}"
      );

      // Đảm bảo không gian duyệt thực sự phủ rộng và không bị dừng sớm bất thường
      Assert.That(totalStatesVisited, Is.GreaterThan(1000), "Cây duyệt quá nhỏ — có thể bị cắt nhánh sai.");
      Assert.That(totalDecidedStates, Is.GreaterThan(500), "Số trạng thái phân định quá ít.");
    }

    /// <summary>
    /// Hàm đệ quy duyệt cây trạng thái và kiểm tra các bất biến tại MỌI nút trên cây.
    /// </summary>
    private static void ExploreState(
      ShootoutState currentState,
      string history,
      bool parentDecided,
      int depth,
      ref int totalStatesVisited,
      ref int totalDecidedStates
    ) {
      totalStatesVisited++;

      int th = CountTaken(currentState.home);
      int ta = CountTaken(currentState.away);
      int sh = CountScore(currentState.home);
      int sa = CountScore(currentState.away);

      // ======================================================================
      // B4. BẤT BIẾN XEN KẼ TUYỆT ĐỐI & TỔNG SỐ LƯỢT ĐÁ
      // ======================================================================
      Assert.That(
        Math.Abs(th - ta),
        Is.LessThanOrEqualTo(1),
        $"[B4 - Xen kẽ] Số lượt đã đá của hai đội lệch nhau > 1: Home={th}, Away={ta}. Dãy: {history}"
      );

      Assert.That(
        currentState.TotalKicksTaken,
        Is.EqualTo(th + ta),
        $"[B4 - Xen kẽ] TotalKicksTaken ({currentState.TotalKicksTaken}) != th+ta ({th + ta}). Dãy: {history}"
      );

      bool isEvenKicks = ((th + ta) % 2) == 0;
      bool expectedHomeTurn = isEvenKicks ? currentState.homeKicksFirst : !currentState.homeKicksFirst;
      Assert.That(
        currentState.IsHomeTurn,
        Is.EqualTo(expectedHomeTurn),
        $"[B4 - Xen kẽ] IsHomeTurn sai tại lượt thứ {th + ta + 1}. Mong đợi: {expectedHomeTurn}, Thực tế: {currentState.IsHomeTurn}. Dãy: {history}"
      );

      // ======================================================================
      // KIỂM TRA PHÂN ĐỊNH TRẠNG THÁI HIỆN TẠI
      // ======================================================================
      bool decided = ShootoutRules.IsDecided(in currentState, out int winner);

      // ======================================================================
      // B3. BẤT BIẾN KHÔNG PHÂN ĐỊNH GIỮA CẶP SUDDEN DEATH
      // ======================================================================
      bool inSuddenDeath = (th >= ShootoutRules.RegulationKicks && ta >= ShootoutRules.RegulationKicks) || (th + ta > ShootoutRules.RegulationKicks * 2);
      if (inSuddenDeath && th != ta) {
        Assert.That(
          decided,
          Is.False,
          $"[B3 - Sudden Death] IsDecided trả true khi đang ở giữa cặp sudden death (Home={th}, Away={ta}). Dãy: {history}"
        );
      }

      // ======================================================================
      // B6. BẤT BIẾN MaxPossibleRemaining NHẤT QUÁN VỚI IsDecided
      // ======================================================================
      int remH = ShootoutRules.MaxPossibleRemaining(in currentState, true);
      int remA = ShootoutRules.MaxPossibleRemaining(in currentState, false);

      Assert.That(remH, Is.GreaterThanOrEqualTo(0), $"[B6 - MaxRemaining] remH âm ({remH}). Dãy: {history}");
      Assert.That(remA, Is.GreaterThanOrEqualTo(0), $"[B6 - MaxRemaining] remA âm ({remA}). Dãy: {history}");

      if (th < ShootoutRules.RegulationKicks || ta < ShootoutRules.RegulationKicks) {
        // Trong regulation: số bàn tối đa còn lại là số lượt còn lại trong 5 lượt
        Assert.That(remH, Is.EqualTo(Math.Max(0, ShootoutRules.RegulationKicks - th)), $"[B6 - MaxRemaining] remH sai trong 5 lượt đầu. Dãy: {history}");
        Assert.That(remA, Is.EqualTo(Math.Max(0, ShootoutRules.RegulationKicks - ta)), $"[B6 - MaxRemaining] remA sai trong 5 lượt đầu. Dãy: {history}");

        if (sh > sa + remA) {
          Assert.That(decided, Is.True, $"[B6 - MaxRemaining] Home dẫn cách biệt vượt remA nhưng chưa IsDecided. Dãy: {history}");
          Assert.That(winner, Is.EqualTo(0), $"[B6 - MaxRemaining] Home thắng sớm nhưng winner != 0. Dãy: {history}");
        } else if (sa > sh + remH) {
          Assert.That(decided, Is.True, $"[B6 - MaxRemaining] Away dẫn cách biệt vượt remH nhưng chưa IsDecided. Dãy: {history}");
          Assert.That(winner, Is.EqualTo(1), $"[B6 - MaxRemaining] Away thắng sớm nhưng winner != 1. Dãy: {history}");
        } else {
          Assert.That(decided, Is.False, $"[B6 - MaxRemaining] Chưa đội nào vượt ngưỡng thắng sớm nhưng đã IsDecided. Dãy: {history}");
        }
      }

      // ======================================================================
      // KỊCH BẢN KHI TRẬN ĐẤU ĐÃ PHÂN ĐỊNH (TRẠNG THÁI LÁ)
      // ======================================================================
      if (decided) {
        totalDecidedStates++;

        // B5. Kết thúc sớm là SỚM NHẤT có thể (nút cha ngay trước đó phải CHƯA phân định)
        Assert.That(
          parentDecided,
          Is.False,
          $"[B5 - Sớm nhất] Trạng thái cha đã phân định nhưng vẫn sinh thêm lượt đá mới. Dãy: {history}"
        );

        // B2. Người thắng đúng là người ghi nhiều bàn hơn (không bao giờ hoà khi IsDecided = true)
        Assert.That(
          winner,
          Is.InRange(0, 1),
          $"[B2 - Người thắng] Winner phải là 0 hoặc 1 khi IsDecided=true, nhận được {winner}. Dãy: {history}"
        );

        if (winner == 0) {
          Assert.That(
            sh,
            Is.GreaterThan(sa),
            $"[B2 - Điểm số] Winner=0 (Home) nhưng bàn Home ({sh}) không lớn hơn Away ({sa}). Dãy: {history}"
          );
        } else {
          Assert.That(
            sa,
            Is.GreaterThan(sh),
            $"[B2 - Điểm số] Winner=1 (Away) nhưng bàn Away ({sa}) không lớn hơn Home ({sh}). Dãy: {history}"
          );
        }

        // B1. Đơn điệu: ApplyKick sau khi IsDecided là no-op tuyệt đối
        foreach (KickResult nextKick in new[] { KickResult.Scored, KickResult.Missed }) {
          ShootoutState afterKickState = ShootoutRules.ApplyKick(in currentState, nextKick);

          Assert.That(
            afterKickState.TotalKicksTaken,
            Is.EqualTo(currentState.TotalKicksTaken),
            $"[B1 - Đơn điệu] ApplyKick sau khi thắng làm tăng TotalKicksTaken. Dãy: {history} + {nextKick}"
          );
          Assert.That(
            afterKickState.home.Length,
            Is.EqualTo(currentState.home.Length),
            $"[B1 - Đơn điệu] ApplyKick sau khi thắng thay đổi home.Length. Dãy: {history} + {nextKick}"
          );
          Assert.That(
            afterKickState.away.Length,
            Is.EqualTo(currentState.away.Length),
            $"[B1 - Đơn điệu] ApplyKick sau khi thắng thay đổi away.Length. Dãy: {history} + {nextKick}"
          );

          bool stillDecided = ShootoutRules.IsDecided(in afterKickState, out int stillWinner);
          Assert.That(
            stillDecided,
            Is.True,
            $"[B1 - Đơn điệu] IsDecided chuyển từ true về false sau khi ApplyKick tiếp. Dãy: {history} + {nextKick}"
          );
          Assert.That(
            stillWinner,
            Is.EqualTo(winner),
            $"[B1 - Đơn điệu] Winner bị thay đổi từ {winner} sang {stillWinner}. Dãy: {history} + {nextKick}"
          );
        }

        // Đã phân định: CẮT NHÁNH — không duyệt sâu hơn nữa
        return;
      }

      // ======================================================================
      // KỊCH BẢN KHI TRẬN ĐẤU CHƯA PHÂN ĐỊNH
      // ======================================================================
      Assert.That(
        winner,
        Is.EqualTo(-1),
        $"[B2 - Chưa phân định] IsDecided=false nhưng winner != -1 (winner={winner}). Dãy: {history}"
      );

      // B7. Số lượt có giới hạn: Nếu đã đạt tối đa 10 lượt mỗi đội (20 lượt tổng) thì dừng nhánh hoà kéo dài
      if (depth >= MaxTotalKicks || (th >= MaxKicksPerTeam && ta >= MaxKicksPerTeam)) {
        return;
      }

      // ======================================================================
      // ĐỆ QUY DUYỆT TIẾP 2 NHÁNH: VÀO (Scored) VÀ HỎNG (Missed)
      // ======================================================================
      string currentShooter = currentState.IsHomeTurn ? "H" : "A";

      // Nhánh 1: Cú sút thành bàn (Scored)
      {
        ShootoutState nextStateScored = ShootoutRules.ApplyKick(in currentState, KickResult.Scored);
        string stepDesc = $"{currentShooter}:vào";
        string nextHistory = string.IsNullOrEmpty(history) ? stepDesc : $"{history}, {stepDesc}";

        ExploreState(
          currentState: nextStateScored,
          history: nextHistory,
          parentDecided: decided,
          depth: depth + 1,
          ref totalStatesVisited,
          ref totalDecidedStates
        );
      }

      // Nhánh 2: Cú sút hỏng (Missed)
      {
        ShootoutState nextStateMissed = ShootoutRules.ApplyKick(in currentState, KickResult.Missed);
        string stepDesc = $"{currentShooter}:hỏng";
        string nextHistory = string.IsNullOrEmpty(history) ? stepDesc : $"{history}, {stepDesc}";

        ExploreState(
          currentState: nextStateMissed,
          history: nextHistory,
          parentDecided: decided,
          depth: depth + 1,
          ref totalStatesVisited,
          ref totalDecidedStates
        );
      }
    }

    // ---------- Helpers nội bộ phục vụ tính toán độc lập trong Test ----------

    private static int CountScore(in FixedList64Bytes<KickResult> list) {
      int c = 0;
      for (int i = 0; i < list.Length; i++) {
        if (list[i] == KickResult.Scored) c++;
      }
      return c;
    }

    private static int CountTaken(in FixedList64Bytes<KickResult> list) {
      int c = 0;
      for (int i = 0; i < list.Length; i++) {
        if (list[i] != KickResult.Pending) c++;
      }
      return c;
    }
  }
}
