← [Phase 2: Điều khiển](backlog/phase-2-dieu-khien.md)

---

# Nghiên cứu tham khảo cho T13 — Chuẩn hoá DPI cho toạ độ vuốt trên iOS

> **Nguồn: AI nghiên cứu (qua 9router, model `stealth/ox-alpha`), ngày 2026-08-26.**
> Trả lời cho câu hỏi cụ thể: `SwipeSample.position` hiện là pixel màn hình thô, chưa qua
> chuẩn hoá DPI nào (gap đã xác minh bằng grep — xem
> [research-t13-review-swipeanalyzer.md](research-t13-review-swipeanalyzer.md)). **Chưa được
> người kiểm chứng lại trên thiết bị thật.** Lần gọi đầu bị cắt cụt do hết ngân sách token suy
> luận (`max_tokens=6000` → toàn bộ rơi vào `reasoning`, `content` rỗng); đã gọi lại với
> `max_tokens=16000` và nhận được câu trả lời đầy đủ (finish_reason=`stop`).

## 1) Unity báo toạ độ touch trên iOS theo đơn vị gì?

Cả Input Manager cũ lẫn Input System mới đều trả **pixel trong không gian `Screen` của Unity**
(`Screen.width × Screen.height`) — **không phải UIKit "points"**, chưa qua chuẩn hoá DPI. Với
scaling mặc định, không gian này trùng pixel Retina thật (iPhone SE @2x → 750×1334; iPhone Pro
@3x → 1179×2556) — cùng một vuốt 4cm cho ra số pixel khác hẳn giữa hai máy.

Khác biệt giữa 2 hệ input không nằm ở đơn vị mà ở **gốc toạ độ**: legacy dùng gốc dưới-trái,
Input System dùng trên-trái cho vị trí pointer (`y` đảo chiều) — nguồn bug "vuốt ngược" kinh
điển khi migrate.

## 2) Công thức quy đổi & độ tin cậy `Screen.dpi`

```
khoảng_cách(cm) = Δpx / Screen.dpi × 2.54
vận_tốc(cm/s)   = (Δpx / Δt) × 2.54 / Screen.dpi
```

Apple không có API ppi vật lý (`UIScreen` chỉ có `bounds` theo points + `scale`) — Unity tra
bảng cứng model→ppi. Rủi ro: thiết bị quá mới chưa có trong bảng → trả 0/sai; Editor/Device
Simulator trả giá trị vô nghĩa (đừng tune cảm giác trên Editor). `dpi` không đổi khi vào Split
View (chỉ `width/height` đổi). Nếu cần chắc chắn tuyệt đối: tự giữ bảng `SystemInfo.deviceModel`
→ ppi riêng.

## 3) Chuẩn hoá ở đâu — điểm thu thập hay trong `SwipeAnalyzer`?

**Khuyến nghị của model: chuẩn hoá ngay tại điểm thu thập** (biên vào Analyzer), quy sang cm,
coi "SwipeAnalyzer làm việc trên cm" là hợp đồng. Lý do chính: `SwipeAnalyzer` giữ được thuần
toán/test được bằng dữ liệu synthetic (edit-mode test không có `Screen.dpi` đáng tin); nếu
chuẩn hoá bên trong Analyzer thì dính trạng thái toàn cục `Screen.*`, khó unit test.

**Lưu ý toán học quan trọng đã chỉ ra:** nếu `curvature` thuần tỉ lệ hình học (diện tích/độ dài),
scale tuyến tính đồng nhất là bất biến — thứ thực sự gây lệch kết quả giữa SE/iPad là các
**hằng số tuyệt đối tính bằng px** ẩn bên trong analyzer (epsilon khử nhiễu, ngưỡng tốc độ...) —
cần rà lại và đổi sang cm.

## 4) Các bẫy thường gặp

- 3 hệ quy chiếu dễ nhầm: UIKit points ≠ native px (points×scale) ≠ `Screen.width` của Unity.
  URP Render Scale **không** đổi toạ độ touch; Player Setting *Resolution Scaling Mode* thì có.
- Gốc toạ độ legacy vs Input System (mục 1).
- Safe Area: hệ thống có thể nuốt vuốt sát mép dưới (home indicator)/góc (Control Center).
- Split View/Slide Over/Stage Manager (iPad) đổi `Screen.width/height` đột ngột — khuyến nghị
  bật *Requires Fullscreen* để tắt hẳn multitasking cho game.
- Xoay màn hình giữa lúc vuốt → dữ liệu rác, nên discard stroke vắt qua sự kiện xoay.
- 60Hz vs 120Hz ProMotion: số mẫu/vuốt khác nhau — tính đặc trưng theo `time` thực, không giả
  định số mẫu cố định (điểm này khớp với thiết kế hiện tại của `SwipeAnalyzer`).

## 5) Code minh hoạ (tham khảo, chưa đưa vào code chính)

```csharp
public static class PhysicalUnits
{
    const float FallbackDpi = 290f;  // trung bình thiết bị mục tiêu (SE 326, iPad ~264)
    const float MinPlausibleDpi = 100f, MaxPlausibleDpi = 700f;
    static float _dpi; static int _w, _h;

    public static float Dpi {
        get {
            if (_dpi > 0 && _w == Screen.width && _h == Screen.height) return _dpi;
            float d = Screen.dpi;
            if (d < MinPlausibleDpi || d > MaxPlausibleDpi) d = FallbackDpi;
            _w = Screen.width; _h = Screen.height; _dpi = d;
            return _dpi;
        }
    }

    public static float2 NormalizeToPhysicalUnits(float2 pixelPos) => pixelPos * (2.54f / Dpi);
}
```

Áp tại nơi tạo `SwipeSample` (chuẩn hoá tại biên, tính `k` một lần mỗi vuốt, không gọi
`Screen.dpi` bên trong Burst job).

## Phần model tự nhận không chắc chắn — cần kiểm chứng trên thiết bị thật

1. Chiều trục Y/gốc toạ độ của Input System trên đúng Unity 6000.3 — nên log một chạm ở 4 góc
   màn hình để chốt trước khi migrate.
2. Giá trị `Screen.dpi` thật trên đúng các model target (đặc biệt máy đời mới nhất).
3. Hành vi `Screen.width/height` khi bật *Resolution Scaling Mode ≠ Native*, và trên
   Simulator/Stage Manager.
4. Toàn bộ kết luận nên xác nhận bằng một scene debug đơn giản trên iPhone SE và iPad thật.

## Cách dùng bản này

Tài liệu tham khảo cho lớp input phía trên `SwipeAnalyzer` (chưa tồn tại — xem gap đã xác minh
trong [research-t13-review-swipeanalyzer.md](research-t13-review-swipeanalyzer.md)), **không**
tự dùng để tick checklist T13 trong [phase-2-dieu-khien.md](backlog/phase-2-dieu-khien.md) —
vẫn cần code thật + test thật + xác nhận trên thiết bị thật.
