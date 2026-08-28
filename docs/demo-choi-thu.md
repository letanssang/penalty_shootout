# Eleven Metres — Hướng dẫn chơi thử (bản demo bóng xám)

Tài liệu này mô tả bản demo hiện tại: toàn bộ scene được dựng bằng primitive từ code
(`MatchSceneGenerator.cs`), không có model hay animation thật. Đọc trước khi chơi thử,
đặc biệt phần "Những gì chưa có".

---

## Cách chơi trong 30 giây

Một lượt sút diễn ra theo ba bước liên tiếp, không có nút bấm riêng:

**1. Chạm ngón tay xuống màn hình.**
Khoảnh khắc này chốt vị trí ngắm và bắt đầu pha chạy đà (1,30 giây). Thủ môn bắt đầu
đọc tín hiệu ngay từ lúc này — nó theo dõi góc xoay hông, chân trụ và hướng đà chạy
của hình nhân sút (xương thật, không phải ý đồ cú sút).

**2. Giữ và vuốt về phía góc muốn sút.**
Thân người sút nghiêng theo hướng ngón tay kéo — đây là tín hiệu thủ môn đọc được.
Vuốt cong hay thẳng, dài hay ngắn sẽ quyết định kiểu sút (xem mục tiếp theo).

**3. Nhả ngón tay đúng lúc chân chạm bóng.**
Thanh thời điểm ở đáy màn hình cho thấy vị trí hiện tại trong pha chạy đà. Vùng xanh
tương ứng cửa sổ PERFECT: ±50 ms quanh điểm tiếp xúc chuẩn (80% thời lượng pha chạy đà,
tức 1,04 giây sau khi bắt đầu). Vùng tốt kéo dài thêm đến ±120 ms. Ra ngoài cả hai vùng
là POOR — cú sút vẫn được tính nhưng tản mát điểm ngắm tăng lên đáng kể.

Nếu hết đà mà không vuốt, hệ thống tự tạo cú sút hỏng chân (tốc độ 11–15 m/s, điểm ngắm
ngẫu nhiên) và hiển thị kết quả POOR.

### Mẹo: lừa thủ môn

Thủ môn cam kết hướng bay trước khi bóng rời chân (−150 ms ở mức Khó, −60 ms ở mức Dễ).
Sau khi cam kết, nó không sửa hướng được nữa. Cơ chế này cho phép một chiến thuật cụ thể:

- Nghiêng người (kéo ngón tay) về một phía suốt pha chạy đà để thủ môn đọc sai hướng.
- Ở khoảnh khắc cuối, giật cổ tay sang hướng ngược lại khi nhả ngón tay.

Vì quyết định cuối cùng về điểm ngắm được chốt tại thời điểm nhả tay (không phải trong
suốt đà chạy), và vì thủ môn đọc xương người sút chứ không đọc ý đồ cú sút trực tiếp,
cú giật này hoạt động đúng như trong thực tế.

---

## Bốn kiểu sút và cử chỉ tương ứng

Phân loại do `ShotMapper.Classify()` thực hiện theo thứ tự ưu tiên từ trên xuống.
Ba kiểu đầu loại trừ nhau; kiểu thứ tư là mặc định.

| Kiểu sút | Tên hiển thị | Cử chỉ |
|---|---|---|
| **Chip (Panenka)** | cú lốp bóng Panenka | Vuốt **ngắn** mà **nhanh** — giật dứt khoát, không cần dài. Điều kiện: độ dài vuốt ≤ `chipMaxLengthCm` VÀ tốc độ đỉnh ≥ `chipMinPeakSpeedCmPerSec`. |
| **InsideFoot** | cú cứa lòng má trong | Vuốt **cong rõ rệt** — độ cong tuyệt đối ≥ `insideFootMinCurvatureCm`. Vuốt cong phải → bóng xoáy sang phải (Magnus); cong trái → xoáy trái. |
| **Knuckle** | cú sút không xoáy | Vuốt **thẳng đét** và **mạnh**: độ cong ≤ `knuckleMaxCurvatureCm`, chỉ số thẳng (đường mịn) ≥ `knuckleMinStraightness`, công suất vuốt ≥ `knuckleMinPower`. Bóng không có xoáy Magnus — thay vào đó hệ thống bơm lực ngang tần số thấp ngẫu nhiên (`KnuckleForce`), tạo quỹ đạo loạn khó đoán. |
| **Instep** | cú nã mu bàn chân | Mọi cú vuốt không rơi vào ba kiểu trên. Đây là cú mặc định — người mới luôn rơi vào đây. |

**Lưu ý kỹ thuật:** `ShotMapper` nhận đơn vị centimet từ `SwipeCollector`. Ngưỡng cụ thể
(`chipMaxLengthCm`, `insideFootMinCurvatureCm`, v.v.) nằm trong `ShotMappingConfig` và
được cấu hình ngoài Inspector — không cố định trong code.

**Màu vệt bóng theo kiểu sút** (chỉ để phân biệt trong bản bóng xám):
- Instep: cam-đỏ
- InsideFoot: xanh lam
- Knuckle: vàng-cam
- Chip: xanh lá

---

## Bảng đối chiếu 7 phase

| Phase | Hệ thống lõi | Nó xuất hiện ở đâu trong bản demo |
|---|---|---|
| **Phase 0** — Nền tảng | `DeviceTier`, `PerfHud`, `TierBootstrap` | Thông tin bậc máy in ra log khi khởi động (`DebugHotkeys.Awake`). HUD hiệu năng bật/tắt bằng F1 hoặc 3 ngón. |
| **Phase 1** — Vật lý bóng | `BallSolver`, `BallDriver` (đồng hồ 120 Hz), `TrajectoryPredictor`, `GoalGeometry` | Quỹ đạo bóng xám bay qua không khí sau mỗi cú sút. Kết quả vào/trượt/cột/xà do `GoalGeometry.Classify()` phán trong `MatchGameLoop.OnBallSimStep()`. |
| **Phase 2** — Điều khiển | `TouchSwipeReceiver`, `SwipeCollector`, `SwipeAnalyzer`, `ShotMapper`, `TimingWindow`, `KnuckleForce` | Thanh thời điểm ở đáy màn hình. Badge kiểu sút và tốc độ hiện sau cú sút. Vệt màu bóng thay đổi theo kiểu. |
| **Phase 3** — Thủ môn | `KickerBoneCueSource`, `BayesianKeeperBrain`, `SimpleKeeperController`, `ReachEnvelope`, `SaveResolver`, `ShotHistory` | Thủ môn hình chữ nhật xám bay người cản phá. Dòng debug `keeper: ô X | tin cậy Y | cam kết Zms` hiện ở HUD dưới bảng tỷ số. |
| **Phase 4** — Luật trận | `ShootoutRules`, `KickSequencer`, `MatchSave`/`MatchSaveLifecycle`, `DifficultySelector` | Bảng tỷ số luân lưu (5 lượt mỗi bên, sau đó đột tử). Nút đổi độ khó (Dễ/Thường/Khó). Tiến trình lưu tự động sau mỗi lượt. |
| **Phase 5** — Trình diễn | `CameraRig`, `CameraDirector`, `ReplayPlayer`, `GoalNetView` (lưới Verlet), `AudioDirector`, `ImpactPostProcessEffect` | Camera chuyển góc sau mỗi pha (BehindShooter → NetCam/KeeperPOV/Broadcast). Replay 0,35× gọi được bằng nút "XEM LẠI". Lưới phản ứng khi bóng vào. Tiếng còi, tiếng sút, tiếng đám đông sinh bằng code. |
| **Phase 6** — Kiểm chứng | `BenchmarkRunner`, `SoakTestRunner`, `DebugHotkeys` | Bộ benchmark 20 kịch bản chạy bằng F2, kết quả xuất CSV. `SoakTestRunner` chạy ngầm để kiểm nhiệt dài hạn. |

---

## Phím tắt và công cụ đo

Nguồn: `Assets/_Project/Code/Presentation/Diagnostics/DebugHotkeys.cs`

| Phím / Cử chỉ | Hành động |
|---|---|
| **F1** | Bật/tắt PerfHud overlay (FPS, frame time, bậc thiết bị). Đo nền vẫn tiếp tục khi overlay tắt. |
| **F2** | Chạy suite benchmark 20 kịch bản, lưu báo cáo CSV, in đường dẫn và chỉ số `p95` ra Console. |
| **3 ngón tay cùng lúc** (cảm ứng) | Tương đương F1 — bật/tắt PerfHud. Chỉ kích hoạt một lần cho đến khi nhả hết ngón. |

Đường dẫn báo cáo benchmark gần nhất có thể đọc qua `DebugHotkeys.LastReportPath`.

---

## Những gì chưa có trong bản này

Danh sách dưới đây kiểm chứng bằng code, không phải ước đoán.

**Không có model hay animation thật.**
`MatchSceneGenerator.cs` dựng toàn bộ scene từ primitive Unity (capsule, sphere, cube).
Người sút và thủ môn là hình chữ nhật/capsule xám. Không có mesh nhân vật, không có rig,
không có clip animation từ mocap.

**Shader da SSS chưa gắn vào nhân vật.**
`SkinSssLut.cs` và `SkinDiffusionProfile.cs` đã tính và lưu bảng tra LUT 128×32
(pre-integrated SSS theo Penner), nhưng không có mesh nhân vật để gắn shader vào.
Hiệu ứng tán xạ dưới bề mặt chưa hiện ra trong bản demo.

**Âm thanh tổng hợp bằng code, không phải file thu thật.**
`ProceduralClips.cs` tạo mọi `AudioClip` trong bộ nhớ lúc runtime (còi trọng tài bằng
sóng vuông 2 600 Hz + vibrato 18 Hz, v.v.). Không có file `.wav`/`.ogg` nào trong dự án.

**Chưa có chế độ người chơi làm thủ môn.**
`MatchGameLoop.cs` chỉ có hai vai: người chơi sút (`IsHomeTurn`) và AI sút
(`FireAiShot()`). Không có đường dẫn input nào cho phép người chơi điều khiển thủ môn.

**Cỏ 3D đã có bộ vẽ nhưng chưa có vệt cắt và biến dạng theo giày.**
`GrassFieldRenderer.cs` được đặt vào scene và vẽ lá cỏ instanced trong đĩa quanh chấm
phạt đền, nhưng mặt sân nền vẫn là một Plane primitive: chưa có vệt cắt cỏ thật, chưa
có biến dạng theo bước chân, chưa có texture.

**Khán giả là billboard sinh bằng code, chưa phải impostor chụp từ model.**
`CrowdRenderer.cs` nằm trong scene, tự sinh atlas 4x2 ô bằng code (đầu tròn + thân hình
thang) và nhún theo tâm trạng đám đông. Đây là chỗ giữ vị trí cho impostor thật — chưa
có model người để bake atlas.

**Hậu kỳ theo bậc chưa nối vào URP Volume.**
`PostProcessTierConfig.cs` đã có bảng số theo bậc A/B/C, nhưng scene chưa có Volume nào
đọc bảng đó. Hiệu ứng va chạm hiện chỉ đi qua `ImpactPostProcessEffect` để rung máy và
nhấn nhịp, chưa có bloom/vignette/grain.

**Test ngâm chưa có phím tắt.**
`SoakTestRunner.cs` chạy được nhưng `DebugHotkeys.cs` mới nối `BenchmarkRunner` (F2).
Muốn chạy test ngâm 20 phút thì hiện phải gọi từ code.

---

## Thủ môn: số đo, không phải cảm nhận

Đo ngày 2026-08-27 bằng `DifficultyTests` (1000 lượt, gọi `TryCommit` mỗi khung hình như game
thật chạy), bậc Khó:

| | trước khi sửa | sau khi sửa |
|---|---|---|
| Bị ép đứng giữa | 843/843 | 28/843 |
| Cam kết khi bóng còn cách | ~0,60s (chưa kịp nhìn) | 0,227s |
| Tỉ lệ cản phá | ~0% | 21,4% |

Hai lỗi đã sửa, cả hai đều là lỗi GHÉP TẦNG chứ không phải lỗi của một phase nào:

1. `SimpleKeeperController` so hạn cam kết với `timeToContact`, tức ngầm đòi thủ môn có mặt ở
   góc ngay lúc chân chạm bóng. Thực tế nó còn cả quãng bóng bay (~0,45s). Với bậc Thường và ô
   góc, hạn là 0,24 + 0,60 = 0,84s — dài hơn cả pha chạy đà, nên `outOfTime` đúng ngay khung
   hình đầu.
2. `BayesianKeeperBrain` trả `confidence` theo entropy trên 9 ô (thực đo 0,03–0,10) trong khi
   `SimpleKeeperController` đặt ngưỡng theo thang xác suất (0,20 / 0,45). Hai thang không gặp
   nhau nên nhánh "quá mù thì đứng giữa" nuốt trọn mọi quả.

Cả hai lớp đều xanh test khi đứng riêng. Chỉ lộ ra khi có
`KeeperReadsShotTests` dựng lại đúng bộ tín hiệu mà vòng lặp trận bơm vào.

### Giới hạn còn lại, đã đo

Mục tiêu T25 (cản phá 18% / 28% / 38%) **mâu thuẫn với mô hình tầm với**, không phải chỉ cần
chỉnh tham số. Ngân sách thời gian bậc Thường là 0,45s bóng bay + 0,11s cam kết sớm − 0,24s
phản xạ = **0,32s**, trong khi `ReachEnvelope` đòi 0,46–0,60s cho các ô biên. Thủ môn chỉ với
tới nổi ô 4 và ô 7. Muốn chạm 28% thì phải nới `reach`, mà `plan.md` cấm điều đó. Đây là một
quyết định thiết kế còn treo, ghi trong `DifficultyTests`.
