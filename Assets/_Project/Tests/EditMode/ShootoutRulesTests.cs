using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Eleven.Match;

namespace Eleven.Tests.EditMode {
  [TestFixture]
  public class ShootoutRulesTests {

    const KickResult S = KickResult.Scored;
    const KickResult M = KickResult.Missed;

    // ---------- helpers ----------

    /// <summary>Xây state bằng cách áp dụng tuần tự chuỗi kết quả, xen kẽ bắt đầu
    /// từ người đá trước theo homeKicksFirst.</summary>
    private static ShootoutState Build(bool homeFirst, params KickResult[] seq) {
      var st = new ShootoutState {
        home = new FixedList64Bytes<KickResult>(),
        away = new FixedList64Bytes<KickResult>(),
        homeKicksFirst = homeFirst
      };
      foreach (var r in seq) st = ShootoutRules.ApplyKick(st, r);
      return st;
    }

    /// <summary>
    /// SỬA LỖI TEST (do model sinh code để lại): nhiều test gốc dùng <see cref="Build"/>
    /// với MỘT chuỗi đan xen phẳng, nhưng comment mô tả lại giả định sai thứ tự lượt
    /// thật (vd tưởng "HHH AAA" trong khi ApplyKick luôn xen kẽ H,A,H,A,...). Helper
    /// này nhận RIÊNG chuỗi của từng đội, tự lắp xen kẽ đúng theo IsHomeTurn — không
    /// còn chỗ để tính nhầm ai đá lượt nào.
    /// </summary>
    private static ShootoutState BuildFromSides(bool homeFirst, KickResult[] homeSeq, KickResult[] awaySeq) {
      var st = new ShootoutState {
        home = new FixedList64Bytes<KickResult>(),
        away = new FixedList64Bytes<KickResult>(),
        homeKicksFirst = homeFirst
      };
      int i = 0, j = 0;
      while (i < homeSeq.Length || j < awaySeq.Length) {
        if (st.IsHomeTurn) {
          Assert.Less(i, homeSeq.Length, "Hết chuỗi home nhưng vẫn tới lượt home — kịch bản test sai");
          st = ShootoutRules.ApplyKick(st, homeSeq[i]); i++;
        } else {
          Assert.Less(j, awaySeq.Length, "Hết chuỗi away nhưng vẫn tới lượt away — kịch bản test sai");
          st = ShootoutRules.ApplyKick(st, awaySeq[j]); j++;
        }
      }
      return st;
    }

    private static string Desc(ShootoutState s) {
      return $"homeKicksFirst={s.homeKicksFirst} total={s.TotalKicksTaken} " +
             $"home=[{Join(s.home)}] away=[{Join(s.away)}]";
    }
    private static string Join(FixedList64Bytes<KickResult> l) {
      var parts = new List<string>();
      for (int i = 0; i < l.Length; i++) parts.Add(l[i].ToString());
      return string.Join(",", parts);
    }

    // ========== 1..6: kết thúc sớm ở giai đoạn 1 (nhiều tỉ số/lượt khác nhau) ==========

    [Test]
    public void EarlyEnd_Home3_0_After3Rounds_Classic() {
      // Home ghi cả 3, away trượt cả 3: 3-0 sau 3 lượt mỗi bên, away còn tối đa 2 → home thắng ngay.
      var s = BuildFromSides(true, new[] { S, S, S }, new[] { M, M, M });
      Assert.IsTrue(ShootoutRules.IsDecided(s, out int w), Desc(s));
      Assert.AreEqual(0, w); // home
    }

    [Test]
    public void EarlyEnd_HomeLeads4_0_AfterAwayFourthMiss() {
      // SỬA KỊCH BẢN (phát hiện khi chạy test thật): với home đá trước trong mọi
      // cặp, kiểm tra "đã phân định" luôn chạy TRƯỚC lượt kế — nên nếu home dẫn
      // đứt điểm 3-0 ở vòng 3 (remA=2, 3>0+2), trận đã xong NGAY sau lượt home,
      // trước khi away kịp đá lượt tương ứng. Không thể có kịch bản "home dẫn
      // đứt điểm, phân định đúng lúc away vừa trượt lượt thứ N" — về toán học nó
      // luôn phân định ngay sau lượt của HOME, không phải sau lượt của AWAY.
      // Kịch bản đúng: home ghi 3/4 vòng đầu (không ghi liền mạch để né mốc 3-0
      // đứt điểm sớm), quyết định thật sự chỉ chốt ngay sau lượt thứ 4 của home.
      var s = BuildFromSides(true, new[] { S, M, S, S }, new[] { M, M, M });
      Assert.IsTrue(ShootoutRules.IsDecided(s, out int w), Desc(s));
      Assert.AreEqual(0, w);
    }

    [Test]
    public void EarlyEnd_AwayWins_Early_AwayKicksFirst() {
      // away đá trước, away ghi 3, home trượt cả 3 → away thắng sớm.
      var s = Build(false, S, M, S, M, S, M);
      Assert.IsTrue(ShootoutRules.IsDecided(s, out int w));
      Assert.AreEqual(1, w); // away
    }

    [Test]
    public void EarlyEnd_HomeWins_AfterOnlyFiveKicks_2ndRoundBlowout() {
      // Home ghi 2 trong 2 lượt đầu rồi trượt hết; away trượt cả 4 lượt đầu.
      // Sau lượt thứ 5 của home (th=5, ta=4): remA=1, sh=2>sa(0)+remA(1)=1 → xong.
      var s = BuildFromSides(true, new[] { S, S, M, M, M }, new[] { M, M, M, M });
      Assert.IsTrue(ShootoutRules.IsDecided(s, out int w), Desc(s));
      Assert.AreEqual(0, w);
    }

    [Test]
    public void EarlyEnd_NotYet_WhenLeadEqualsOpponentRemainingPlusZero() {
      // HHMMSS: home 2, away 0, remA=3 → 2 không > 3 → CHƯA xong (away còn hoà được).
      var s = Build(true, S, S, M, M, S, S);
      Assert.IsFalse(ShootoutRules.IsDecided(s, out int w));
      Assert.AreEqual(-1, w);
    }

    [Test]
    public void EarlyEnd_NotYet_At3_1_AfterThreeRounds_TieStillPossible() {
      // HHS SMA SHM: home 2-1 sau 3 vòng? Tính: H S,H M,A S | A S,H S,A M | H M...
      // Dùng chuỗi rõ: home S,S,M ; away S,M,M → 2-1, remA=2 → away tối đa 3 → chưa xong.
      var s = Build(true, S, S, M, S, S, M, M, M);
      Assert.IsFalse(ShootoutRules.IsDecided(s, out _), Desc(s));
    }

    // ========== 7..10: chạy đủ 5 vòng ==========

    [Test]
    public void FullFiveRounds_HomeWins_ImmediatelyAtFifthPairDifference() {
      // Home ghi cả 5; away ghi 3/4 lượt đầu. Ngay sau lượt 5 của home (th=5,
      // ta=4): remA=1, sh=5 > sa=3+1 → xong, KHÔNG cần đợi lượt 5 của away.
      var s = BuildFromSides(true, new[] { S, S, S, S, S }, new[] { S, S, S, M });
      Assert.IsTrue(ShootoutRules.IsDecided(s, out int w), Desc(s));
      Assert.AreEqual(0, w);
    }

    [Test]
    public void FullFiveRounds_AwayWins() {
      // Cả hai đá đủ 5 lượt, away ghi nhiều hơn ở lượt cuối → away thắng.
      var s = BuildFromSides(true, new[] { S, S, M, S, M }, new[] { S, S, M, S, S });
      Assert.IsTrue(ShootoutRules.IsDecided(s, out int w), Desc(s));
      Assert.AreEqual(1, w);
    }

    [Test]
    public void FiveAll_Tie_IsNotDecided_EntersSuddenDeath() {
      var s = Build(true, S, S, M, S, S, M, S, S, M, M);
      Assert.IsFalse(ShootoutRules.IsDecided(s, out _));
      Assert.AreEqual(10, s.TotalKicksTaken);
    }

    [Test]
    public void Regulation_MaxRemaining_EqualsKicksLeft() {
      var s = Build(true, S, S, M); // home đã đá 2, away đã đá 1
      Assert.AreEqual(3, ShootoutRules.MaxPossibleRemaining(s, true));  // 5-2
      Assert.AreEqual(4, ShootoutRules.MaxPossibleRemaining(s, false)); // 5-1
    }

    // ========== 11..16: sudden death ==========

    [Test]
    public void SuddenDeath_MidPair_NeverDecided_EvenIfOneScored() {
      // Hoà sau 5 vòng, vòng SD: home ghi, away CHƯA đá → chưa phân định.
      var s = Build(true, S, S, M, S, S, M, S, S, M, M, S);
      Assert.IsFalse(ShootoutRules.IsDecided(s, out _), Desc(s));
    }

    [Test]
    public void SuddenDeath_PairComplete_DifferentResults_Decides() {
      var s = Build(true, S, S, M, S, S, M, S, S, M, M, S, M);
      Assert.IsTrue(ShootoutRules.IsDecided(s, out int w));
      Assert.AreEqual(0, w);
    }

    [Test]
    public void SuddenDeath_PairComplete_BothMissed_Continues() {
      var s = Build(true, S, S, M, S, S, M, S, S, M, M, M, M);
      Assert.IsFalse(ShootoutRules.IsDecided(s, out _), Desc(s));
    }

    [Test]
    public void SuddenDeath_SecondPair_Decides_ForAway() {
      // Cả hai ghi đủ 5 -> hoà 5-5. SD cặp 1: cả hai ghi (vẫn hoà). SD cặp 2:
      // home trượt, away ghi -> away thắng.
      var s = BuildFromSides(false,
        homeSeq: new[] { S, S, S, S, S, S, M },
        awaySeq: new[] { S, S, S, S, S, S, S });
      Assert.IsTrue(ShootoutRules.IsDecided(s, out int w), Desc(s));
      Assert.AreEqual(1, w);
    }

    [Test]
    public void SuddenDeath_MaxRemaining_OneOrZero_InCurrentPair() {
      var tied = Build(true, S, S, M, S, S, M, S, S, M, M); // 3-3 sau 5 vòng
      Assert.AreEqual(1, ShootoutRules.MaxPossibleRemaining(tied, true));
      Assert.AreEqual(1, ShootoutRules.MaxPossibleRemaining(tied, false));

      var afterHome = ShootoutRules.ApplyKick(tied, S); // home đã đá lượt SD
      Assert.AreEqual(0, ShootoutRules.MaxPossibleRemaining(afterHome, true));
      Assert.AreEqual(1, ShootoutRules.MaxPossibleRemaining(afterHome, false));
    }

    [Test]
    public void SuddenDeath_LongRun_EventuallyDecides() {
      // 4 cặp hoà rồi home ghi / away trượt ở cặp 5.
      var seq = new List<KickResult>();
      var baseSeq = new[] { S, S, M, S, S, M, S, S, M, M };
      seq.AddRange(baseSeq);
      for (int i = 0; i < 4; i++) { seq.Add(S); seq.Add(S); }
      seq.Add(S); seq.Add(M);
      var s = Build(true, seq.ToArray());
      Assert.IsTrue(ShootoutRules.IsDecided(s, out int w));
      Assert.AreEqual(0, w);
    }

    // ========== 17..21: thứ tự đá, IsHomeTurn, TotalKicksTaken ==========

    [Test]
    public void IsHomeTurn_Alternates_HomeFirst() {
      var st = EmptyState(true);
      Assert.IsTrue(st.IsHomeTurn);
      st = ShootoutRules.ApplyKick(st, S);
      Assert.IsFalse(st.IsHomeTurn);
      st = ShootoutRules.ApplyKick(st, M);
      Assert.IsTrue(st.IsHomeTurn);
      st = ShootoutRules.ApplyKick(st, M);
      Assert.IsFalse(st.IsHomeTurn);
    }

    [Test]
    public void IsHomeTurn_Alternates_AwayFirst() {
      var st = EmptyState(false);
      Assert.IsFalse(st.IsHomeTurn);
      st = ShootoutRules.ApplyKick(st, S);
      Assert.IsTrue(st.IsHomeTurn);
      st = ShootoutRules.ApplyKick(st, M);
      Assert.IsFalse(st.IsHomeTurn);
    }

    [Test]
    public void TotalKicksTaken_IgnoresPending() {
      var st = EmptyState(true);
      st.home.Add(S); st.home.Add(KickResult.Pending); st.away.Add(S);
      Assert.AreEqual(2, st.TotalKicksTaken);
    }

    [Test]
    public void ApplyKick_RoutesToCorrectSide_ByHomeKicksFirst() {
      var homeFirst = EmptyState(true).ApplyBoth(S, M);
      Assert.AreEqual(S, homeFirst.home[0]);
      Assert.AreEqual(M, homeFirst.away[0]);

      var awayFirst = EmptyState(false).ApplyBoth(S, M);
      Assert.AreEqual(M, awayFirst.home[0]);  // lượt đầu là AWAY
      Assert.AreEqual(S, awayFirst.away[0]);
    }

    [Test]
    public void ApplyKick_IsPure_InputUnchanged() {
      // SỬA LỖI TEST (do model sinh code để lại): bản gốc so sánh SnapshotHash(before)
      // với SnapshotHash(after) — nhưng before/after LẼ RA phải khác nhau (đã thêm
      // một lượt đá mới)! Ý định thật của tên test là: biến `before` không bị SỬA TẠI
      // CHỖ sau khi truyền vào ApplyKick (tham số `in` đã đảm bảo điều này ở cấp ngôn
      // ngữ, nhưng vẫn đáng để test hiển thị rõ ý định và bắt hồi quy nếu chữ ký đổi).
      var before = Build(true, S, S, M);
      var beforeHashPriorToCall = SnapshotHash(before);

      var after = ShootoutRules.ApplyKick(before, M);

      Assert.AreEqual(beforeHashPriorToCall, SnapshotHash(before),
        "Biến 'before' không được đổi sau khi gọi ApplyKick (hàm phải thuần)");
      Assert.AreNotEqual(SnapshotHash(before), SnapshotHash(after),
        "after phải khác before — đã ghi nhận thêm một lượt đá mới");
      Assert.AreEqual(before.TotalKicksTaken + 1, after.TotalKicksTaken);
    }

    // ========== 22..24: hành vi biên ==========

    [Test]
    public void ApplyKick_AfterDecided_IsNoOp() {
      var decided = BuildFromSides(true, new[] { S, S, S }, new[] { M, M, M }); // home thắng sớm 3-0
      Assert.IsTrue(ShootoutRules.IsDecided(decided, out _), Desc(decided));
      var after = ShootoutRules.ApplyKick(decided, S);
      Assert.AreEqual(SnapshotHash(decided), SnapshotHash(after));
      Assert.AreEqual(6, after.TotalKicksTaken);
    }

    [Test]
    public void EmptyState_IsNotDecided_AndPendingWinnerMinusOne() {
      var st = EmptyState(true);
      Assert.IsFalse(ShootoutRules.IsDecided(st, out int w));
      Assert.AreEqual(-1, w);
      Assert.AreEqual(0, st.TotalKicksTaken);
    }

    [Test]
    public void MaxPossibleRemaining_NeverNegative() {
      var s = Build(true, S, S, M, S, S, M, S, S, M, M, S, S, M, M); // 4 cặp SD
      Assert.GreaterOrEqual(ShootoutRules.MaxPossibleRemaining(s, true), 0);
      Assert.GreaterOrEqual(ShootoutRules.MaxPossibleRemaining(s, false), 0);
    }

    // ========== 25+: brute-force nhất quán ==========

    [Test]
    public void BruteForce_AllSequences_UpTo10Kicks_ConsistentAndMonotone() {
      var results = new[] { S, M };
      Traverse(new ShootoutState {
        home = new FixedList64Bytes<KickResult>(),
        away = new FixedList64Bytes<KickResult>(),
        homeKicksFirst = true
      }, results, 0, 10);

      Traverse(new ShootoutState {
        home = new FixedList64Bytes<KickResult>(),
        away = new FixedList64Bytes<KickResult>(),
        homeKicksFirst = false
      }, results, 0, 10);
    }

    private static int visitedStates;

    private static void Traverse(ShootoutState s, KickResult[] options, int depth, int maxDepth) {
      // SỬA LỖI TEST (do model sinh code để lại): bản gốc dùng Assert.IsTrue(IsDecided(...))
      // — tức là BẮT BUỘC mọi trạng thái (kể cả trạng thái rỗng 0-0!) phải decided, điều
      // này sai hoàn toàn với ý định thật của comment "Nhất quán: gọi IsDecided hai lần
      // phải cùng kết quả" (chỉ cần HAI LẦN GỌI khớp nhau, không cần khớp = true).
      bool decided1 = ShootoutRules.IsDecided(s, out int w1);
      bool decided2 = ShootoutRules.IsDecided(s, out int w2);
      Assert.AreEqual(decided1, decided2, "IsDecided không deterministic (decided) tại: " + Desc(s));
      Assert.AreEqual(w1, w2, "IsDecided không deterministic (winner) tại: " + Desc(s));

      // Hợp lệ: winner chỉ được 0/1 khi decided, -1 khi chưa.
      if (decided1) CollectionAssert.Contains(new[] { 0, 1 }, w1);
      else Assert.AreEqual(-1, w1, "Chưa decided thì winner phải là -1: " + Desc(s));

      // Monotone: một khi đã decided, mọi ApplyKick tiếp theo không đổi state
      // và IsDecided vẫn decided với cùng winner.
      if (decided1) {
        foreach (var r in options) {
          var after = ShootoutRules.ApplyKick(s, r);
          Assert.AreEqual(SnapshotHash(s), SnapshotHash(after),
            "ApplyKick sau khi decided phải no-op tại: " + Desc(s));
          Assert.IsTrue(ShootoutRules.IsDecided(after, out int wAfter));
          Assert.AreEqual(w1, wAfter);
        }
        return; // nhánh đã kết thúc
      }

      visitedStates++;
      if (depth >= maxDepth) return;

      foreach (var r in options) {
        var next = ShootoutRules.ApplyKick(s, r);
        // Bất biến: lượt kế luôn đổi phe so với trước khi đá.
        Assert.AreNotEqual(s.IsHomeTurn, next.IsHomeTurn);
        // Bất biến: MaxPossibleRemaining không âm.
        Assert.GreaterOrEqual(ShootoutRules.MaxPossibleRemaining(next, true), 0);
        Assert.GreaterOrEqual(ShootoutRules.MaxPossibleRemaining(next, false), 0);
        Traverse(next, options, depth + 1, maxDepth);
      }
    }

    [Test]
    public void BruteForce_VisitedEnoughStates() {
      // Đảm bảo brute-force phía trên thật sự quét lượng lớn trạng thái
      // (chạy độc lập bằng cách duyệt lại nhanh).
      var counter = new List<int>();
      CountStates(Build(true, S), counter, 9);
      Assert.Greater(counter.Count, 100);
    }

    private static void CountStates(ShootoutState s, List<int> sink, int depth) {
      sink.Add(0);
      if (ShootoutRules.IsDecided(s, out _) || depth <= 0) return;
      CountStates(ShootoutRules.ApplyKick(s, S), sink, depth - 1);
      CountStates(ShootoutRules.ApplyKick(s, M), sink, depth - 1);
    }

    // ========== tiện ích test ==========

    private static ShootoutState EmptyState(bool homeFirst) => new ShootoutState {
      home = new FixedList64Bytes<KickResult>(),
      away = new FixedList64Bytes<KickResult>(),
      homeKicksFirst = homeFirst
    };

    private static string SnapshotHash(ShootoutState s) {
      return $"{s.homeKicksFirst}|{s.TotalKicksTaken}|{Desc(s)}";
    }
  }

  internal static class ShootoutTestExt {
    public static ShootoutState ApplyBoth(this ShootoutState s, KickResult a, KickResult b) {
      s = ShootoutRules.ApplyKick(s, a);
      return ShootoutRules.ApplyKick(s, b);
    }
  }
}
