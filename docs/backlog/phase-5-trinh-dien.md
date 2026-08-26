← [Phase 4: Luật và trận đấu](phase-4-tran-dau.md) · [Mục lục](README.md) · [Phase 6: Kiểm chứng tự động](phase-6-kiem-chung.md) →

---

# PHASE 5 — Trình diễn, phần code được

**7 task · tuần 15–21**

Đây là phần agent làm được của M4 Golden Shot.
Phần còn lại của M4 — quyết định "đã đẹp chưa" — là việc của bạn.

---

## T26 — Đạo diễn camera

**Phụ thuộc:** T23 · **Ước lượng:** ~2 ngày

Cinemachine 3, chuyển góc theo pha của lượt sút. Ràng buộc camera là **trụ cột thiết kế**,
không phải hạn chế kỹ thuật — chiến lược "chỉ dựng 12m" sụp đổ ngay khi có camera tự do 360°.

> **Quyết định 2026-08-26 (người dùng):** giai đoạn đầu **camera đứng yên** — một góc cố định
> duy nhất cho pha ngắm+sút, không dolly, không lia. Lý do: mọi phép quy đổi cử chỉ vuốt →
> điểm ngắm trên khung thành trở thành một phép chiếu **hằng số**, tính một lần lúc khởi tạo,
> không phải tính lại mỗi khung hình.
> **Nhưng thiết kế phải mở**: không được hard-code ma trận chiếu vào chỗ ánh xạ. Ràng buộc cụ thể:
> - Phép quy đổi màn hình → điểm ngắm phải đi qua một chỗ duy nhất nhận `Camera` (hoặc ma trận
>   view-projection) làm **tham số truyền vào**, không đọc `Camera.main` rải rác.
> - Chỗ đó phải chịu được ma trận thay đổi giữa các khung hình; camera đứng yên chỉ là trường
>   hợp riêng "ma trận không đổi", không phải giả định được phép bake cứng.
> - `ICameraDirector` giữ nguyên như thiết kế; giai đoạn đầu chỉ hiện thực **một** `CameraShot`.
>
> Hệ quả sang T14: `ShotMapper` nhận điểm ngắm ở **không gian thế giới** đã quy đổi sẵn,
> không tự đọc camera — nhờ vậy đổi sang camera động sau này không phải sửa `ShotMapper`.

```csharp
namespace Eleven.Presentation {
  public enum CameraShot { Broadcast, BehindShooter, KeeperPOV,
                           LowAngle, NetCam, ReplayOrbit }

  public interface ICameraDirector {
    void CutTo(CameraShot shot, float blendSeconds);
    void BindToPhase(KickPhase phase, CameraShot shot);
    bool IsWithinAuthoredBounds(in float3 position);
  }
}
```

**Checklist nghiệm thu**
- [ ] Mọi vị trí camera nằm trong vùng đã dựng — `IsWithinAuthoredBounds` trả true ở **mọi** khung hình của mọi góc
- [ ] Có test tự động chạy hết mọi `CameraShot` và khẳng định điều trên
- [ ] Không có góc nào nhìn thấy mép sân đã bị cắt bỏ — chụp màn hình 6 góc để kiểm
- [ ] Chuyển góc dùng blend của Cinemachine, không cắt cứng trừ khi cố ý
- [ ] Camera không tốn quá 0.1ms CPU — đo bằng HUD
- [ ] `ReplayOrbit` có giới hạn góc quay cứng, không cho xoay tự do 360°

---

## T27 — Hệ thống replay

**Phụ thuộc:** T23 · **Ước lượng:** ~2 ngày · `TẤT ĐỊNH`

Ghi lại **seed và input**, không ghi lại transform. Nếu Phase 1–4 thật sự tất định
thì replay chỉ là chạy lại — nhẹ hơn hàng trăm lần và cũng là bài kiểm tra tính tất định tốt nhất.

**Checklist nghiệm thu**
- [ ] Một lượt sút ghi lại dưới **256 byte**
- [ ] Chạy lại cho quỹ đạo giống hệt — so từng khung hình, sai số dưới `1e-4`
- [ ] Replay chạy được ở tốc độ 0.25×, 0.5×, 1×
- [ ] Replay ghi trên máy này chạy đúng trên máy khác — test iOS ghi, Android chạy
- [ ] Nếu replay lệch, hệ thống *phát hiện* và báo lỗi thay vì âm thầm sai

---

## T28 — Mô phỏng lưới Verlet

**Phụ thuộc:** T09, T03 · **Ước lượng:** ~3 ngày

Money shot của cả thể loại. Burst + Job System, va chạm chỉ với quả bóng.

```csharp
namespace Eleven.Presentation.Net {
  public struct NetParticle { public float3 position, prevPosition; public byte pinned; }

  [BurstCompile] public struct NetStepJob : IJob {
    public NativeArray<NetParticle> particles;
    [ReadOnly] public NativeArray<int2>  constraints;
    [ReadOnly] public NativeArray<float> restLengths;
    public float3 ballPosition, ballVelocity;
    public float  ballRadius, dt, damping;
    public int    iterations;
  }
}
```

**Checklist nghiệm thu**
- [ ] ≤ 600 hạt, ≤ 8 vòng lặp ràng buộc
- [ ] Ngân sách **≤ 0.5ms CPU** — dán số đo thật từ máy bậc B, không phải Editor
- [ ] Bóng không bao giờ xuyên qua lưới, kể cả ở 30 m/s — test 200 cú ở nhiều góc
- [ ] Lưới ổn định sau 3 giây, không rung vĩnh viễn
- [ ] Chạy trên worker thread, không chặn main thread — xác nhận trong Profiler timeline
- [ ] Tắt được ở bậc C qua `TierProfile.netSimulation`, thay bằng lưới tĩnh
- [ ] Cấp phát 0 byte mỗi khung hình

---

## T29 — Hệ thống cỏ instanced

**Phụ thuộc:** T03, T04 · **Ước lượng:** ~3 ngày

> ⚠️ **Rủi ro GPU cao nhất trong toàn dự án.** Cỏ là thứ dễ trở thành nút thắt GPU nhất
> trên mobile — alpha clipping, overdraw và băng thông đều tệ. GPU Resident Drawer giảm chi phí
> CPU chứ *không* giảm những thứ đó. Task này phải đo trước, tối ưu sau, và sẵn sàng bị cắt.

**Checklist nghiệm thu**
- [ ] Mật độ giảm dần theo bán kính, đọc từ `TierProfile.grassDensity`
- [ ] **Đo overdraw** bằng debug view của URP, dán ảnh chụp vào báo cáo
- [ ] Ngân sách **≤ 2.0ms GPU** ở bậc A — dán số đo thật từ máy, ghi rõ tên máy
- [ ] Đo cả ba biến thể: có/không alpha clip, có/không đổ bóng, có/không gió. Bảng so sánh 8 dòng.
- [ ] Bậc C tắt hoàn toàn, thay bằng texture, chênh lệch frame time được ghi lại
- [ ] Có cờ tắt riêng để đo đóng góp của riêng cỏ vào frame time
- [ ] Nếu không đạt 2.0ms: **báo cáo lại thay vì tự ý giảm chất lượng** — quyết định cắt là của bạn

---

## T30 — Khán giả impostor

**Phụ thuộc:** T03 · **Ước lượng:** ~2 ngày

**Checklist nghiệm thu**
- [ ] Một atlas duy nhất, một draw call cho toàn bộ khán giả
- [ ] Ngân sách **≤ 0.8ms** — dán số đo thật
- [ ] Phản ứng theo sự kiện: nhảy khi vào bóng, gục khi hỏng, im khi đặt bóng
- [ ] Pha animation lệch nhau theo instance, không đồng loạt như robot
- [ ] Luôn hướng camera nhưng không lật khi camera đi qua ngang
- [ ] Bậc C dùng khán giả tĩnh, vẫn còn hình, không biến mất

---

## T31 — Shader da tán xạ dưới bề mặt

**Phụ thuộc:** T03, T04 · **Ước lượng:** ~3 ngày

Pre-integrated SSS bằng LUT độ cong — một lần fetch texture, không phải blur nhiều pass.
Đây là kỹ thuật khả thi trên mobile.

**Checklist nghiệm thu**
- [ ] Shader Graph hoặc HLSL, tương thích URP Forward+
- [ ] Ngân sách **≤ 0.5ms** cho 2 nhân vật — số đo thật từ máy bậc B
- [ ] So sánh cạnh nhau: bật/tắt SSS, chụp cùng góc cùng ánh sáng
- [ ] Không có shader variant nào bị strip nhầm — kiểm bằng build thật, không phải Editor
- [ ] Tắt được ở bậc C qua `TierProfile.subsurfaceScattering`, về Lit thường
- [ ] Thời gian biên dịch shader không làm màn hình đầu tiên delay quá 1 giây

---

## T32 — Cấu hình hậu kỳ theo bậc

**Phụ thuộc:** T03 · **Ước lượng:** ~1 ngày

**Checklist nghiệm thu**
- [ ] Tonemap bằng LUT 3D, không dùng ACES của URP (đắt hơn trên mobile)
- [ ] Ngân sách **≤ 1.5ms** tổng hậu kỳ ở bậc A — số đo thật
- [ ] Đo riêng từng hiệu ứng, có bảng đóng góp ms của từng cái
- [ ] Không dùng SSAO toàn màn hình ở bất kỳ bậc nào
- [ ] Sai lệch màu chỉ bật lúc chạm bóng, dưới 200ms
- [ ] Bậc C chỉ còn tonemap và vignette

---

← [Phase 4: Luật và trận đấu](phase-4-tran-dau.md) · [Mục lục](README.md) · [Phase 6: Kiểm chứng tự động](phase-6-kiem-chung.md) →

> Trước khi giao việc, đọc [quy tắc giao việc](README.md#quy-tắc-giao-việc) và
> dùng [mẫu prompt](README.md#mẫu-prompt-giao-việc). Đừng gộp nhiều task vào một phiên.
