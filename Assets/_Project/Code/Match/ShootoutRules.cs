using System;
using Unity.Collections;

namespace Eleven.Match {
  public enum KickResult : byte { Pending = 0, Scored = 1, Missed = 2 }

  /// <summary>
  /// Trạng thái thuần (pure data) của loạt luân lưu. Toàn bộ thao tác là bất biến:
  /// mọi thay đổi đi qua <see cref="ShootoutRules.ApplyKick"/> và trả về bản sao mới.
  /// </summary>
  public struct ShootoutState {
    public FixedList64Bytes<KickResult> home, away;
    public bool homeKicksFirst;

    /// <summary>Tổng số lượt CẢ HAI đội đã đá (các lượt Pending không được tính).</summary>
    public int TotalKicksTaken {
      get {
        int n = 0;
        for (int i = 0; i < home.Length; i++)
          if (home[i] != KickResult.Pending) n++;
        for (int i = 0; i < away.Length; i++)
          if (away[i] != KickResult.Pending) n++;
        return n;
      }
    }

    /// <summary>
    /// true nếu LƯỢT KẾ TIẾP là của home. Thứ tự xen kẽ tuyệt đối bắt đầu từ
    /// người đá trước theo homeKicksFirst: tổng số lượt đã đá chẵn → cùng phe
    /// với người đá trước; lẻ → phe kia.
    /// </summary>
    public bool IsHomeTurn {
      get {
        int n = TotalKicksTaken;
        bool even = (n % 2) == 0;
        return even ? homeKicksFirst : !homeKicksFirst;
      }
    }
  }

  /// <summary>
  /// Luật luân lưu 11m chuẩn IFAB/FIFA: 5 lượt đầu xen kẽ (có kết thúc sớm khi
  /// khoảng cách bàn thắng vượt quá số bàn tối đa đối thủ còn có thể ghi),
  /// sau đó sudden death theo từng cặp — chỉ phân định sau khi CẢ HAI đội đã
  /// đá xong lượt của mình trong cặp đó.
  ///
  /// Hàm ở đây THUẦN hoàn toàn: không phụ thuộc UnityEngine, không có trạng
  /// thái tĩnh, không sửa tham số đầu vào.
  /// </summary>
  public static class ShootoutRules {

    public const int RegulationKicks = 5;

    /// <summary>
    /// Trận đấu đã được định đoạt hay chưa.
    /// Quy ước winner: 0 = HOME thắng, 1 = AWAY thắng.
    /// Khi chưa phân định, winner được gán -1.
    /// </summary>
    public static bool IsDecided(in ShootoutState s, out int winner) {
      winner = -1;

      int sh = ScoreOf(in s, true);
      int sa = ScoreOf(in s, false);
      int th = TakenOf(in s, true);
      int ta = TakenOf(in s, false);

      bool inRegulation = th < RegulationKicks || ta < RegulationKicks;

      if (inRegulation) {
        // Giai đoạn 1: kiểm tra kết thúc sớm tại MỌI thời điểm.
        int remH = Math.Max(0, RegulationKicks - th);
        int remA = Math.Max(0, RegulationKicks - ta);
        if (sh > sa + remA) { winner = 0; return true; }   // home thắng sớm
        if (sa > sh + remH) { winner = 1; return true; }   // away thắng sớm
        return false;
      }

      // Cả hai đã đá xong (ít nhất) 5 lượt.
      // BUG ĐÃ SỬA (do model sinh code để lại): bản gốc dùng
      // "Math.Min(th, ta) - RegulationKicks < 1" để phát hiện cặp sudden-death
      // chưa hoàn chỉnh — nhưng biểu thức đó SAI khi một đội đã đá lượt chết
      // đầu tiên còn đội kia chưa (vd th=6, ta=5): Min(6,5)-5=0 vẫn rơi vào
      // nhánh "chưa hoàn chỉnh" nhưng nhánh đó lại KIỂM sh!=sa và tuyên bố
      // thắng ngay — tức là phân định khi CHỈ MỘT đội đã đá lượt chết, vi
      // phạm đúng yêu cầu "chỉ phân định khi cả hai đã đá đủ trong lượt đó".
      // Điều kiện đúng: so sánh th và ta trực tiếp. Vì ApplyKick chỉ cho một
      // đội đá mỗi lần và lượt luôn xen kẽ, th/ta chỉ lệch nhau tối đa 1.
      if (th != ta) {
        // Giữa một cặp sudden death: một đội đã đá, đội kia chưa — chưa phân định.
        return false;
      }

      // th == ta: một cặp (hoặc đúng 5-5 ban đầu) đã HOÀN CHỈNH — khác bàn mới phân định.
      if (sh != sa) { winner = sh > sa ? 0 : 1; return true; }
      return false;
    }

    /// <summary>
    /// Số bàn TỐI ĐA đội chỉ định (home=true → đội home) CÒN CÓ THỂ ghi thêm:
    /// - Giai đoạn 1: số lượt còn lại của đội đó trong 5 lượt đầu.
    /// - Sudden death: 1 nếu đội đó CHƯA đá lượt của mình trong cặp hiện tại,
    ///   0 nếu đã đá.
    /// </summary>
    public static int MaxPossibleRemaining(in ShootoutState s, bool home) {
      int tMe  = TakenOf(in s, home);
      int tOpp = TakenOf(in s, !home);

      bool inRegulation = tMe < RegulationKicks || tOpp < RegulationKicks;
      if (inRegulation)
        return Math.Max(0, RegulationKicks - tMe);

      // BUG ĐÃ SỬA (do model sinh code để lại): "tMe == tOpp ? 1 : 0" coi hai
      // đội đối xứng, nhưng trong một cặp sudden-death dở dang, đội đá TRƯỚC
      // trong cặp (theo homeKicksFirst — cùng đội mở màn ở mọi cặp vì mỗi cặp
      // cộng đúng 2 lượt, giữ nguyên tính chẵn/lẻ) và đội đá SAU có ý nghĩa
      // khác nhau khi tMe != tOpp:
      //  - Nếu tôi là đội đá trước: tMe > tOpp nghĩa là TÔI đã đá xong lượt
      //    này rồi (hết lượt, = 0); nếu chưa (tMe == tOpp, cặp mới) thì còn 1.
      //  - Nếu tôi là đội đá sau: tOpp > tMe nghĩa là ĐỐI THỦ đã đá, tới lượt
      //    tôi (còn 1); nếu tMe == tOpp (cặp mới, đối thủ chưa đá) tôi cũng
      //    còn 1 (lượt của tôi trong cặp này chưa tới nhưng vẫn sẽ tới).
      if (tMe == tOpp)
        return 1; // cặp mới bắt đầu (hoặc vừa vào sudden death 5-5) — ai cũng còn lượt

      bool amFirstInPair = home ? s.homeKicksFirst : !s.homeKicksFirst;
      if (amFirstInPair)
        return tMe > tOpp ? 0 : 1;
      return tOpp > tMe ? 1 : 0;
    }

    /// <summary>
    /// Ghi nhận một cú sút cho đội đang có lượt (theo <see cref="ShootoutState.IsHomeTurn"/>),
    /// trả về ShootoutState MỚI — không sửa state đầu vào.
    ///
    /// Hành vi khi gọi SAU khi trận đã IsDecided: BỎ QUA lượt đá mới và trả về
    /// state nguyên vẹn (không ném exception, không ghi nhận kết quả rác).
    /// </summary>
    public static ShootoutState ApplyKick(in ShootoutState s, KickResult r) {
      if (IsDecided(in s, out _))
        return s; // đã phân định: no-op

      ShootoutState ns = s; // struct copy — FixedList64Bytes nằm trong struct nên copy giá trị
      if (ns.IsHomeTurn) {
        if (ns.home.Length >= ns.home.Capacity) return s; // an toàn trên dữ liệu lỗi
        ns.home.Add(r);
      } else {
        if (ns.away.Length >= ns.away.Capacity) return s;
        ns.away.Add(r);
      }
      return ns;
    }

    // ---------- helpers nội bộ ----------

    private static int ScoreOf(in ShootoutState s, bool home) {
      var list = home ? s.home : s.away;
      int n = 0;
      for (int i = 0; i < list.Length; i++)
        if (list[i] == KickResult.Scored) n++;
      return n;
    }

    private static int TakenOf(in ShootoutState s, bool home) {
      var list = home ? s.home : s.away;
      int n = 0;
      for (int i = 0; i < list.Length; i++)
        if (list[i] != KickResult.Pending) n++;
      return n;
    }
  }
}
