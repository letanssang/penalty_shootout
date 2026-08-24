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

**Checklist nghiệm thu**
- [ ] **Kết thúc sớm đúng:** dẫn 3–0 sau 3 lượt, đối thủ còn 2 lượt → đã phân định, không đá tiếp
- [ ] Luân lưu chết: sau 5 lượt hoà, đá từng cặp, chỉ phân định khi *cả hai* đã đá đủ trong lượt đó
- [ ] Thứ tự đá đúng: xen kẽ, đội đá trước cấu hình được
- [ ] Test vét cạn mọi kịch bản tới 10 lượt bằng vòng lặp — không có trạng thái nào cho kết quả mâu thuẫn
- [ ] Không có `using UnityEngine` trong file luật
- [ ] Ít nhất 25 test, gồm 6 kịch bản kết thúc sớm khác nhau

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

**Checklist nghiệm thu**
- [ ] Có số phiên bản schema, đọc được file của phiên bản cũ hơn
- [ ] Ghi kiểu nguyên tử: ghi file tạm rồi đổi tên, không ghi đè trực tiếp
- [ ] File hỏng hoặc bị cắt cụt → về mặc định, không crash
- [ ] Lưu khi app vào nền (`OnApplicationPause`), không chỉ khi thoát
- [ ] Có checksum để phát hiện sửa file thủ công

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
