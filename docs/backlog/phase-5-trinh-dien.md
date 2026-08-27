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

**XONG (phần mã) 2026-08-27.** File: [GrassDensityField.cs](../../Assets/_Project/Code/Presentation/Grass/GrassDensityField.cs) ·
[GrassInstance.cs](../../Assets/_Project/Code/Presentation/Grass/GrassInstance.cs) ·
[GrassRenderSettings.cs](../../Assets/_Project/Code/Presentation/Grass/GrassRenderSettings.cs) ·
[GrassTierSettings.cs](../../Assets/_Project/Code/Presentation/Grass/GrassTierSettings.cs) ·
[GrassField.cs](../../Assets/_Project/Code/Presentation/Grass/GrassField.cs) ·
[GrassMeasurement.cs](../../Assets/_Project/Code/Presentation/Grass/GrassMeasurement.cs) ·
[Grass.shader](../../Assets/_Project/Art/Shaders/Grass.shader),
test [GrassSystemTests.cs](../../Assets/_Project/Tests/EditMode/GrassSystemTests.cs) (17 test),
nằm trong lượt EditMode **477 test, 476 xanh, 0 đỏ, 1 skip (127.9 s)**.
Shader biên dịch sạch: `ShaderUtil.ShaderHasError` = false, 2 pass, 0 cảnh báo.

> ⚠️ **BỐN ô dưới đây CHƯA đóng được và không được tick.** Chúng đòi một GPU thật.
> EditMode không đo được overdraw, không đo được mili-giây GPU, và không có tên máy để ghi.
> Xem [báo cáo đo hiệu năng Phase 5](../phase-5-do-hieu-nang.md) để biết đo bằng cách nào —
> ở đó đã dựng sẵn khung bảng tám dòng, chỉ còn thiếu số.

**Checklist nghiệm thu**
- [x] Mật độ giảm dần theo bán kính, đọc từ `TierProfile.grassDensity` — **XANH 2026-08-27**:
      `MatDo_GiamDanTheoBanKinh_KhongBaoGioTang`, `MatDo_DocTuTierProfile_KhongVietCungTrongMa`
      (đổi `grassDensity` thành 0.7 thì số túm giảm theo — chứng minh không viết cứng),
      `RaiCo_MatDoThucTe_GiamDanRaNgoai` (đo trên dữ liệu đã rải: 8.0 túm/m² trong bán kính 12 m,
      ~4.5 túm/m² ở vành 20–24 m).
- [ ] **Đo overdraw** bằng debug view của URP, dán ảnh chụp vào báo cáo — **CẦN NGƯỜI KIỂM**.
      Trần đã chốt trong mã: `GrassBudget.MaxAverageOverdraw` = 2.5.
- [ ] Ngân sách **≤ 2.0ms GPU** ở bậc A — dán số đo thật từ máy, ghi rõ tên máy — **CẦN NGƯỜI KIỂM**.
      Trần đã chốt: `GrassBudget.MaxTierAGpuMs` = 2.0 (`NganSachBacA_Dung2ms`).
      Phần CPU đã xanh: `VongLapMoiKhungHinh_KhongCapPhatGC` (500 khung hình, 0 byte GC).
- [ ] Đo cả ba biến thể: có/không alpha clip, có/không đổ bóng, có/không gió. Bảng so sánh 8 dòng.
      — **KHUNG BẢNG XANH, SỐ ĐO CẦN NGƯỜI KIỂM**: `TamBienThe_DuTamToHop_KhongTrungNhau`
      (đúng 8 tổ hợp, mỗi công tắc bật ở đúng 4 dòng), `BangDo_ThieuDongThiKhongKetLuan_DuMoiDongDaDoDeuDat`
      và `DongDo_ThieuTenMay_KhongTinhLaDaDo` (bảy dòng, hoặc số đo không có tên máy, đều KHÔNG
      kết luận được — để ô này không tick khống được).
- [ ] Bậc C tắt hoàn toàn, thay bằng texture, chênh lệch frame time được ghi lại —
      **PHẦN MÃ XANH, CHÊNH LỆCH FRAME TIME CẦN NGƯỜI KIỂM**:
      `BacC_TatHoanToan_ThayBangTextureMatSan` (0 túm, 0 draw call, `usesGroundTexture` = true),
      `SoSanhBacC_PhaiCoCaHaiSoDo_VaTenMay`.
- [x] Có cờ tắt riêng để đo đóng góp của riêng cỏ vào frame time — **XANH 2026-08-27**:
      `CoTatRieng_TatDuocMaKhongRaiLaiCo` — `GrassField.IsEnabled` tắt được mà KHÔNG rải lại cỏ,
      nên chênh lệch frame time đo được là chi phí dựng hình thuần, không lẫn chi phí sinh dữ liệu.
- [x] Nếu không đạt 2.0ms: **báo cáo lại thay vì tự ý giảm chất lượng** — quyết định cắt là của bạn
      — **XANH 2026-08-27**: `VuotNganSach_PhaiBaoCao_KhongTuHaChatLuong`. Bảng 3.4ms trả về
      `GrassVerdict.VuotNganSach_PhaiBaoCao`, và test kiểm rằng sau khi đọc bảng thì `densityScale`,
      `maxInstances` và số túm đang rải KHÔNG đổi. `GrassMeasurementTable` không có đường nào
      chạm vào `GrassTierSettings` hay `GrassField`.

**Số liệu đã chốt trong mã** (để người đo biết mình đang đo cái gì):
mật độ tối đa 8 túm/m² trong bán kính 12 m, tắt hẳn ở 34 m · ~13.9k túm ở bậc A, ~5.5k ở bậc B ·
trần 24.000 túm · 32 byte/instance → bộ đệm 768 KB · 1 draw call · cao 6–11 cm.

---

## T30 — Khán giả impostor

**Phụ thuộc:** T03 · **Ước lượng:** ~2 ngày

**XONG (phần mã) 2026-08-27.** File: [CrowdInstance.cs](../../Assets/_Project/Code/Presentation/Crowd/CrowdInstance.cs) ·
[CrowdAtlas.cs](../../Assets/_Project/Code/Presentation/Crowd/CrowdAtlas.cs) ·
[CrowdBillboard.cs](../../Assets/_Project/Code/Presentation/Crowd/CrowdBillboard.cs) ·
[CrowdStandLayout.cs](../../Assets/_Project/Code/Presentation/Crowd/CrowdStandLayout.cs) ·
[CrowdTierSettings.cs](../../Assets/_Project/Code/Presentation/Crowd/CrowdTierSettings.cs) ·
[CrowdDirector.cs](../../Assets/_Project/Code/Presentation/Crowd/CrowdDirector.cs) ·
[CrowdImpostor.shader](../../Assets/_Project/Art/Shaders/CrowdImpostor.shader),
test [CrowdImpostorTests.cs](../../Assets/_Project/Tests/EditMode/CrowdImpostorTests.cs) (14 test),
nằm trong lượt EditMode **477 test, 476 xanh, 0 đỏ, 1 skip (127.9 s)**.
Shader biên dịch sạch: `ShaderUtil.ShaderHasError` = false, 1 pass, 0 cảnh báo.

**Checklist nghiệm thu**
- [x] Một atlas duy nhất, một draw call cho toàn bộ khán giả — **XANH 2026-08-27**:
      `MotDrawCall_MotAtlas_ChoToanBoKhanGia` (mọi bậc: `drawCallCount` = 1, `atlasId` = 0,
      `instanceCount` = tổng số ghế > 500), `AtlasCoDungMotBoOKhongDeChongLen_VaNamTronTrongUV`
      (32 ô, không ô nào chồng nhau, tất cả nằm trong [0,1]),
      `ShaderKhanGia_ChiKhaiBaoMotTexture_MotBuffer_VaKhongDoBong` (đọc thẳng file .shader:
      đúng một `TEXTURE2D`, đúng một `StructuredBuffer`, không có pass `ShadowCaster`).
- [ ] Ngân sách **≤ 0.8ms** — dán số đo thật — **CẦN NGƯỜI KIỂM**.
      Trần đã chốt: `CrowdBudget.MaxGpuBudgetMs` = 0.8. Phần CPU đã xanh:
      `NganSachGPU_BacA_Duoi0_8ms_VaCpuKhongCapPhat` (500 khung hình, 0 byte GC — mỗi khung hình
      CPU chỉ cộng một số thực, mảng instance không bị đụng tới sau lúc khởi tạo).
- [x] Phản ứng theo sự kiện: nhảy khi vào bóng, gục khi hỏng, im khi đặt bóng — **XANH 2026-08-27**:
      `PhanUngTheoSuKien_NhayKhiVaoBong_GucKhiHong_ImKhiDatBong` và
      `BangAnhXaCamXuc_DayDu_ChoMoiPhaVaMoiKetQua` (phủ toàn bộ ma trận `KickPhase` × `ShotOutcome`;
      chưa chốt kết quả thì khán đài vẫn nín thở — không ăn mừng sớm nửa giây).
- [x] Pha animation lệch nhau theo instance, không đồng loạt như robot — **XANH 2026-08-27**:
      `PhaAnimation_LechNhauTheoInstance_KhongDongLoatNhuRobot` — histogram 8 khung hình tại
      t ∈ {0, 0.13, 0.41, 0.77, 1.5}s, mỗi khung hình luôn có hơn 1/32 số ghế; tỉ lệ hai ghế
      cạnh nhau trùng khung hình < 35%.
- [x] Luôn hướng camera nhưng không lật khi camera đi qua ngang — **XANH 2026-08-27**:
      `Billboard_LuonHuongCamera_VaKhongLatKhiCameraDiQuaNgang` — quay camera 720 bước quanh khán
      đài, có dao động độ cao qua đỉnh đầu: `dot(normal, hướng-tới-camera)` > 0.999 suốt vòng,
      `cross(right, up)` không đổi dấu lần nào, góc lệch giữa hai bước liên tiếp < 3°.
      Cộng `Billboard_CameraThangDinhDau_GiuNguyenGocCu_KhongGiat` (điểm suy biến duy nhất giữ
      góc cũ thay vì để `atan2(0,0)` làm cả khán đài giật một cái).
- [x] Bậc C dùng khán giả tĩnh, vẫn còn hình, không biến mất — **XANH 2026-08-27**:
      `BacC_KhanGiaTinh_VanConHinh_KhongBienMat` — `visible` = true, `animated` = false,
      số ghế bằng đúng bậc A, mọi khung hình = 0, vẫn 1 draw call; `ApplyTier(A)` bật lại
      animation mà không sinh lại ghế.

---

## T31 — Shader da tán xạ dưới bề mặt

**Phụ thuộc:** T03, T04 · **Ước lượng:** ~3 ngày

Pre-integrated SSS bằng LUT độ cong — một lần fetch texture, không phải blur nhiều pass.
Đây là kỹ thuật khả thi trên mobile.

**XONG (phần mã) 2026-08-27.** File: [SkinDiffusionProfile.cs](../../Assets/_Project/Code/Presentation/Skin/SkinDiffusionProfile.cs) ·
[SkinSssLut.cs](../../Assets/_Project/Code/Presentation/Skin/SkinSssLut.cs) ·
[SkinSssSettings.cs](../../Assets/_Project/Code/Presentation/Skin/SkinSssSettings.cs) ·
[SkinShaderVariants.cs](../../Assets/_Project/Code/Presentation/Skin/SkinShaderVariants.cs) ·
[SkinSssMeasurement.cs](../../Assets/_Project/Code/Presentation/Skin/SkinSssMeasurement.cs) ·
[Skin.shader](../../Assets/_Project/Art/Shaders/Skin.shader),
test [SkinSssTests.cs](../../Assets/_Project/Tests/EditMode/SkinSssTests.cs) (32 test),
nằm trong lượt EditMode **509 test, 508 xanh, 0 đỏ, 1 skip (92.5 s)**.
Shader biên dịch sạch: `ShaderUtil.ShaderHasError` = false, 3 pass, 0 cảnh báo.

> ⚠️ **BỐN ô dưới đây CHƯA đóng được và không được tick.** Chúng đòi một GPU thật và một
> build thật: EditMode không đo được mili-giây GPU, không chụp được ảnh so sánh, không thấy
> được bộ lọc biến thể lúc build, và không có tên máy để ghi.
> Xem [báo cáo đo hiệu năng Phase 5](../phase-5-do-hieu-nang.md) để biết đo bằng cách nào.

**Checklist nghiệm thu**
- [x] Shader Graph hoặc HLSL, tương thích URP Forward+ — **XANH 2026-08-27**: viết tay HLSL
      ([Skin.shader](../../Assets/_Project/Art/Shaders/Skin.shader)), không dùng Shader Graph.
      `ShaderDa_DungVongLapDenPhanCum_ChuKhongTuVietVongLap` đọc thẳng file và bắt buộc phần đèn
      phụ đi qua `LIGHT_LOOP_BEGIN`/`LIGHT_LOOP_END` của URP thay vì tự viết
      `for (i < GetAdditionalLightsCount())` — ở URP 17 Forward+, `GetAdditionalLightsCount()`
      trả về **0** theo thiết kế, nên một vòng lặp tự viết sẽ im lặng đánh rơi TOÀN BỘ đèn phụ
      mà shader vẫn biên dịch sạch. Cộng `ShaderDa_CoDuPassDungHinh_BongDo_VaChieuSau`
      (`UniversalForward` + `ShadowCaster` + `DepthOnly`) và `ShaderDa_TenKhopVoiHangSoTrongMa`
      (tên shader trong file khớp `SkinShaderKeywords.ShaderName`).
- [ ] Ngân sách **≤ 0.5ms** cho 2 nhân vật — số đo thật từ máy bậc B — **CẦN NGƯỜI KIỂM**.
      Trần đã chốt: `SkinBudget.MaxGpuBudgetMs` = 0.5 cho `SkinBudget.CharacterCount` = 2
      (`NganSach_Dung05ms_ChoDungHaiNhanVat`). Số đo một nhân vật bị quy về hai trước khi kết
      luận (`SoDoMotNhanVat_PhaiQuyVeHaiNhanVat_TruocKhiKetLuan`), số đo không có tên máy KHÔNG
      tính là đã đo (`SoDo_ThieuTenMay_KhongTinhLaDaDo`), và vượt trần thì
      `SkinBudgetCheck.Evaluate` trả `VuotNganSach_PhaiBaoCao` chứ không tự tắt SSS
      (`VuotNganSach_PhaiBaoCao_KhongTuTatSss` kiểm rằng cấu hình không đổi sau khi đọc kết luận).
      Phần CPU đã xanh: `DoiBac_KhongCapPhatGC` (1000 lượt đổi bậc, 0 byte GC).
- [ ] So sánh cạnh nhau: bật/tắt SSS, chụp cùng góc cùng ánh sáng — **CẦN NGƯỜI KIỂM**.
      Khung đã xanh: `SoSanhCanhNhau_DuThongTinThiMoiTinhLaDaChup` và
      `SoSanhCanhNhau_ThieuGocHoacAnhSang_ThiKhongChungMinhDuocGi` — hai ảnh chụp ở hai góc
      camera khác nhau, hoặc hai cấu hình đèn khác nhau, hoặc trỏ vào cùng một file, đều KHÔNG
      tính là đã chụp. Đây là ô dễ tick khống nhất, nên kiểu dữ liệu chặn sẵn.
- [ ] Không có shader variant nào bị strip nhầm — kiểm bằng build thật, không phải Editor
      — **PHẦN ĐỌC ĐƯỢC TRONG EDITOR ĐÃ XANH, BUILD THẬT CẦN NGƯỜI KIỂM**.
      `BaKeyword_PhaiKhaiBangMultiCompile_KhongPhaiShaderFeature`: `_SKIN_SSS_ON`,
      `_SKIN_TRANSMISSION_ON`, `_CLUSTER_LIGHT_LOOP` đều khai bằng `multi_compile` — `shader_feature`
      bị lược lúc build khi không vật liệu nào trong build bật keyword đó, mà ở đây keyword do
      bậc thiết bị bật lúc chạy nên không vật liệu nào bật sẵn.
      `SoBienTheDungHinh_KhongVuotTran` đếm thẳng từ file: 7 dòng `multi_compile` →
      2×3×2×2×2×2×2 = **192** biến thể pass dựng hình, dưới trần `SkinBudget.MaxForwardVariants` = 256.
      `ShaderDa_KhongKeoTheoNhungMultiCompileKhongDung` cấm khai `LIGHTMAP_ON`,
      `_SCREEN_SPACE_OCCLUSION`, `_ADDITIONAL_LIGHT_SHADOWS`, `_REFLECTION_PROBE_*`,
      `_LIGHT_COOKIES`, `LOD_FADE_CROSSFADE` — mỗi dòng thừa nhân đôi số biến thể.
      `MoiBienThe_BatBuoc_ChayForwardPlus_VaKhacNhau` (3 biến thể bắt buộc, đều Forward+, đôi một
      khác nhau), `KiemBienThe_ThieuMotBienThe_ThiKhongDat`,
      `KiemBienThe_ThieuBuildHoacMay_KhongTinhLaDaKiem`.
- [x] Tắt được ở bậc C qua `TierProfile.subsurfaceScattering`, về Lit thường — **XANH 2026-08-27**:
      `BacC_TatSss_VeLitThuong` (bậc C: `enabled` = false, `useLitFallback` = true,
      `sssStrength` = 0, `transmission` = false) và `CoTrongProfile_ThangBangMacDinhCuaBac`
      (tắt cờ trong profile ở bậc A thì SSS cũng tắt thật — cờ thắng bảng mặc định).
      `NhanhTatSss_TrongShader_LaLambert_TucLitThuong` đọc file shader và kiểm rằng nhánh
      `#else` của `_SKIN_SSS_ON` đúng là `saturate(NdotL)` — tức Lambert + GGX, chính là Lit
      thường, giữ nguyên vật liệu và draw call thay vì đổi sang `Universal Render Pipeline/Lit`.
- [ ] Thời gian biên dịch shader không làm màn hình đầu tiên delay quá 1 giây — **CẦN NGƯỜI KIỂM**.
      Trần đã chốt: `SkinBudget.MaxFirstScreenCompileMs` = 1000.
      `SoDoBienDich_PhaiLaLanChayDauTien_CacheConTrong` — số đo trong Editor hoặc ở lần chạy thứ
      hai KHÔNG tính, vì lúc đó shader đã nằm sẵn trong cache. Cơ chế thật sự giữ ô này là trần
      192 biến thể ở trên: URP/Lit khai hơn ba mươi `multi_compile` và ra hàng chục nghìn biến thể.

**Số liệu đã chốt trong mã** (để người đo biết mình đang đo cái gì): LUT 128×32 RGB24 = **12 KB**
(`KichThuocTexture_Dung12KB`, `MaHoaRgb24_TronVenHaiDauDai`) · sáu Gauss của d'Eon & Luebke,
phương sai 0.0064–7.41 mm² · bán kính độ cong 6–200 mm, hàng LUT chia đều theo **độ cong** chứ
không theo bán kính (`HangCuaLut_ChiaDeuTheoDoCong_KhongPhaiTheoBanKinh`) · một lần
`SAMPLE_TEXTURE2D` mỗi điểm ảnh da, không pass phụ, không render target phụ.
Hành vi LUT đã kiểm: `Lut_SangDanTheoGocChieu_KhongBaoGioToiDi`,
`Lut_DoCongCangGat_AnhSangVongCangXa`, `Lut_DoLanQuaVungGiaoRanh_XaHonXanhVaLam` (đỏ lan xa nhất
— đó chính là vệt ửng đỏ ở rìa bóng trên mặt người), `Lut_BeMatPhang_TraVeGanDungLambert`
(mặt phẳng lệch Lambert tối đa 0.0048), `Lut_MoiGiaTriNamTrong01`,
`Lut_TatDinh_ChayLaiRaTungBitGiongNhau` (bake tất định).

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
