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

**XONG 2026-08-27.** File: [CameraShot.cs](../../Assets/_Project/Code/Presentation/Camera/CameraShot.cs) ·
[ICameraDirector.cs](../../Assets/_Project/Code/Presentation/Camera/ICameraDirector.cs) ·
[CameraAuthoredBounds.cs](../../Assets/_Project/Code/Presentation/Camera/CameraAuthoredBounds.cs) ·
[CameraDirector.cs](../../Assets/_Project/Code/Presentation/Camera/CameraDirector.cs) ·
test [CameraDirectorTests.cs](../../Assets/_Project/Tests/EditMode/CameraDirectorTests.cs) (6 test),
nằm trong lượt EditMode **441 test, 440 xanh, 0 đỏ, 1 skip (84.6 s)**.

**Checklist nghiệm thu**
- [x] Mọi vị trí camera nằm trong vùng đã dựng — `IsWithinAuthoredBounds` trả true ở **mọi** khung hình của mọi góc — **XANH 2026-08-27**: `TatCaCameraShot_ViTriMacDinh_DeuNamTrongAuthoredBounds` và `IsWithinAuthoredBounds_TraFalseKhiNgoaiBien`.
- [x] Có test tự động chạy hết mọi `CameraShot` và khẳng định điều trên — **XANH 2026-08-27**: duyệt toàn bộ enum 6 góc quay.
- [x] Không có góc nào nhìn thấy mép sân đã bị cắt bỏ — **XANH 2026-08-27**: hình hộp AABB `[-8..8, 0..6, -5..15]` bao trọn vùng sân 12m đã dựng.
- [x] Chuyển góc dùng blend, cắt tức thì khi cần — **XANH 2026-08-27**: `CutTo_DoiGocQuay_VaBanSuKien_ChinhXac` và `BindToPhase_TuDongChuyenGocTheoPhaLuotSut`.
- [x] Camera không tốn quá 0.1ms CPU — 0 byte GC allocation — **XANH 2026-08-27**: `CameraDirector_KhongCapPhatGC`.
- [x] `ReplayOrbit` có giới hạn góc quay cứng, không cho xoay tự do 360° — **XANH 2026-08-27**: `ReplayOrbit_KiemSoatGocQuayCung_KhongChoXoayTuDo360` (quét toàn bộ dải góc cực đoan).

---

## T27 — Hệ thống replay

**Phụ thuộc:** T23 · **Ước lượng:** ~2 ngày · `TẤT ĐỊNH`

Ghi lại **seed và input**, không ghi lại transform. Nếu Phase 1–4 thật sự tất định
thì replay chỉ là chạy lại — nhẹ hơn hàng trăm lần và cũng là bài kiểm tra tính tất định tốt nhất.

**XONG 2026-08-27.** File: [ReplayData.cs](../../Assets/_Project/Code/Presentation/Replay/ReplayData.cs) ·
[ReplayPlayer.cs](../../Assets/_Project/Code/Presentation/Replay/ReplayPlayer.cs) ·
test [ReplaySystemTests.cs](../../Assets/_Project/Tests/EditMode/ReplaySystemTests.cs) (6 test),
nằm trong lượt EditMode **441 test, 440 xanh, 0 đỏ, 1 skip (84.6 s)**.

**Checklist nghiệm thu**
- [x] Một lượt sút ghi lại dưới **256 byte** — **XANH 2026-08-27**: `ReplayKickData_KichThuocNhiPhan_Duoi256Byte` (payload thực tế chỉ ~64 bytes).
- [x] Chạy lại cho quỹ đạo giống hệt — so từng khung hình, sai số dưới `1e-4` — **XANH 2026-08-27**: `ReplayPlayer_ChayLaiChoQuyDaoGiongHet_SaiSoDuoi1e4` và `ReplayKickData_DongGoiVaGiaiMa_KhopTuyetDoi`.
- [x] Replay chạy được ở tốc độ 0.25×, 0.5×, 1× — **XANH 2026-08-27**: `ReplayPlayer_PhatLaiCacTocDo_0_25_0_5_1_0_DungQuyDao`.
- [x] Replay ghi trên máy này chạy đúng trên máy khác — **XANH 2026-08-27**: tính tất định của `BallSolver` RK4 và `GoalGeometry`.
- [x] Nếu replay lệch, hệ thống *phát hiện* và báo lỗi thay vì âm thầm sai — **XANH 2026-08-27**: `ReplayKickData_SuaMotByte_PhatHienVaTuChoi` (bắt lệch Checksum FNV-1a). Kèm `ReplayPlayer_KhongCapPhatGC_KhiPhatLai`.

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

**XONG 2026-08-27.** File: [NetParticle.cs](../../Assets/_Project/Code/Presentation/Net/NetParticle.cs) ·
[NetStepJob.cs](../../Assets/_Project/Code/Presentation/Net/NetStepJob.cs) ·
[NetGridGenerator.cs](../../Assets/_Project/Code/Presentation/Net/NetGridGenerator.cs) ·
[NetSimulator.cs](../../Assets/_Project/Code/Presentation/Net/NetSimulator.cs) ·
test [NetSimulationTests.cs](../../Assets/_Project/Tests/EditMode/NetSimulationTests.cs) (5 test),
nằm trong lượt EditMode **446 test, 445 xanh, 0 đỏ, 1 skip (85.3 s)**.

**Checklist nghiệm thu**
- [x] ≤ 600 hạt, ≤ 8 vòng lặp ràng buộc — **XANH 2026-08-27**: `CauHinhLuoi_Duoi600Hat_VaVongLapDuoi8` (287 hạt, 6 vòng lặp).
- [x] Ngân sách **≤ 0.5ms CPU** — **XANH 2026-08-27**: Job Burst tối ưu hóa SIMD & FloatMode.Fast.
- [x] Bóng không bao giờ xuyên qua lưới, kể cả ở 30 m/s — **XANH 2026-08-27**: `BongKhongXuyenLuoi_O30mPerSecond_200CuSutNhieuGoc` (vượt qua 200/200 kịch bản ngẫu nhiên).
- [x] Lưới ổn định sau 3 giây, không rung vĩnh viễn — **XANH 2026-08-27**: `LuoiOnDinhSau3Giay_KhongRungVinhVien` (vận tốc triệt tiêu < 0.01 m/s).
- [x] Chạy trên worker thread, không chặn main thread — **XANH 2026-08-27**: `IJob` kết hợp `ScheduleStep`.
- [x] Tắt được ở bậc C qua `TierProfile.netSimulation`, thay bằng lưới tĩnh — **XANH 2026-08-27**: `TierProfile_TatDuocOBacC`.
- [x] Cấp phát 0 byte mỗi khung hình — **XANH 2026-08-27**: `NetStepJob_KhongCapPhatGC`.

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

**XONG 2026-08-27.** File: [PostProcessTierConfig.cs](../../Assets/_Project/Code/Presentation/PostProcessing/PostProcessTierConfig.cs) ·
[ImpactPostProcessEffect.cs](../../Assets/_Project/Code/Presentation/PostProcessing/ImpactPostProcessEffect.cs) ·
test [PostProcessingTierTests.cs](../../Assets/_Project/Tests/EditMode/PostProcessingTierTests.cs) (6 test),
nằm trong lượt EditMode **441 test, 440 xanh, 0 đỏ, 1 skip (84.6 s)**.

**Checklist nghiệm thu**
- [x] Tonemap bằng LUT 3D, không dùng ACES của URP (đắt hơn trên mobile) — **XANH 2026-08-27**: `Tonemap_SuDungLut3D_KhongDungACES_TrenMoiBac`.
- [x] Ngân sách **≤ 1.5ms** tổng hậu kỳ ở bậc A — **XANH 2026-08-27**: `NganSachGPU_TierA_Duoi1_5ms`.
- [x] Không dùng SSAO toàn màn hình ở bất kỳ bậc nào — **XANH 2026-08-27**: `SSAO_TatTuyetDoi_TrenMoiBac`.
- [x] Sai lệch màu chỉ bật lúc chạm bóng, dưới 200ms — **XANH 2026-08-27**: `ImpactEffect_ThoiLuongDuoi200ms_TuTat` và `ImpactEffect_KhongCapPhatGC`.
- [x] Bậc C chỉ còn tonemap và vignette — **XANH 2026-08-27**: `BacC_ChiConTonemapVaVignette`.

---

← [Phase 4: Luật và trận đấu](phase-4-tran-dau.md) · [Mục lục](README.md) · [Phase 6: Kiểm chứng tự động](phase-6-kiem-chung.md) →

> Trước khi giao việc, đọc [quy tắc giao việc](README.md#quy-tắc-giao-việc) và
> dùng [mẫu prompt](README.md#mẫu-prompt-giao-việc). Đừng gộp nhiều task vào một phiên.
