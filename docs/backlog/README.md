# Eleven Metres — Backlog kỹ thuật v2

58 đầu việc code được, mỗi việc có hợp đồng API cố định và checklist nghiệm thu
kiểm chứng được bởi người không viết nó. Thiết kế để giao cho agent AI làm từng việc một.

**Dự án:** game sút luân lưu mobile (iOS + Android) · Unity `6000.3` LTS · URP Forward+
**Ngày:** 24/08/2026 · **v2 27/08/2026** — thêm Phase 7–10 (T35–T58) sau khi bản demo bóng xám
chạy được trên máy thật · **Kế hoạch tổng thể:** [../plan.md](../plan.md)

> **Bắt đầu ở đâu:** [Quy tắc giao việc](#quy-tắc-giao-việc) → [Phase 0](phase-0-nen-tang.md) → T01.
> Mỗi task là một phiên làm việc riêng. Đừng gộp.

---

## Trạng thái 27/08/2026

**Phase 0–6 (T01–T34) đã code xong.** 534 test EditMode, 533 xanh, 0 đỏ, 1 skip có chủ đích.
Có một **bản demo bóng xám chơi được** đã build và cài lên Pixel 7 thật (`com.eleven.metres`):
vuốt để sút, thủ môn đọc vị và bay người, luật luân lưu 5 lượt + lượt chết, replay chậm 0.35×,
lưới Verlet, cỏ instanced, khán giả impostor, âm thanh tổng hợp bằng code.
Hướng dẫn chơi và bảng đối chiếu 7 phase: [../demo-choi-thu.md](../demo-choi-thu.md).

### Hai lỗi mà 534 test không bắt được

Đáng ghi lại vì nó xác nhận đúng câu cảnh báo ở cuối tài liệu này — *checklist bắt được lỗi
kỹ thuật, không bắt được lỗi tích hợp*. Cả hai đều là **lỗi ghép tầng**, và cả hai lớp liên
quan đều xanh test khi đứng riêng:

1. `SimpleKeeperController` (T19) so hạn cam kết với `timeToContact`, tức ngầm đòi thủ môn có
   mặt ở góc ngay lúc chân chạm bóng — trong khi nó còn cả quãng bóng bay ~0.45s. Với bậc
   Thường và ô góc, hạn là 0.24 + 0.60 = 0.84s, **dài hơn cả pha chạy đà**.
2. `BayesianKeeperBrain` (T18) trả `confidence` theo entropy trên 9 ô (đo thật: 0.03–0.10),
   còn T19 đặt ngưỡng theo thang xác suất (0.20 / 0.45). Hai thang không gặp nhau.

Hậu quả cộng dồn: thủ môn bị ép đứng giữa **843/843 quả**. Sau khi sửa: **28/843**, cản phá
21.4% ở bậc Khó. Bài học đã được đóng thành test thường trực trong `KeeperReadsShotTests` và
`MatchSceneIntegrationTests` — hai bộ test đo **tín hiệu thật mà vòng lặp trận bơm vào**,
chứ không đo từng lớp riêng.

### Nợ đã đo, và chỗ trả nợ

| Nợ | Số đo | Trả ở |
|---|---|---|
| `SkinSssLut` chưa gắn vào nhân vật nào | LUT 128×32 đã sinh, chưa có mesh người | [T40](phase-7-hoat-anh-ik.md) |
| `PostProcessTierConfig` chưa nối vào URP Volume | — | [T50](phase-9-am-thanh-cam-giac.md) |
| `ScoreboardUI` dùng IMGUI, cấp phát GC mỗi khung | 240/240 khung có cấp phát (đo trên Pixel 7) | [T54](phase-10-toi-uu-phat-hanh.md) |
| Cỏ và khán giả vẽ mà gần như không thấy | 13.901 lá + 1.244 người mỗi khung | [T55](phase-10-toi-uu-phat-hanh.md) |
| `SoakTestRunner` chưa có phím tắt | — | [T56](phase-10-toi-uu-phat-hanh.md) |
| Gói Sentis/AI làm build chậm và phình APK | phần lớn 8 phút build là biên dịch compute shader | [T53](phase-10-toi-uu-phat-hanh.md) |
| Chưa có menu, chưa có đường ra khỏi trận | — | [T41](phase-8-vong-lap-game.md) |
| Mục tiêu cản phá T25 (18/28/38%) mâu thuẫn với `ReachEnvelope` | đo được 1.9 / 9.7 / 14.5%; ngân sách bậc Thường 0.32s < 0.46–0.60s mà ô biên đòi | **quyết định thiết kế còn treo**, ghi trong `DifficultyTests` |

Dòng cuối bảng không phải việc tinh chỉnh tham số. Hoặc hạ mục tiêu xuống dải mà tầm với cho
phép, hoặc chấp nhận rằng cú sút đặt đúng góc là không thể cản — đúng như penalty thật.
Quyết định đó là của bạn, không giao được.

---

## Mục lục

| Phase | Task | Lịch | Ước lượng | Song song được? |
|---|---|---|---|---|
| **[Phase 0: Nền tảng](phase-0-nen-tang.md)** | `T01–T05` | tuần 1–2 | ~4 ngày | Tuần tự — không song song được |
| **[Phase 1: Vật lý bóng](phase-1-vat-ly-bong.md)** | `T06–T12` | tuần 3–5 | ~9 ngày | Sau T06: T08/T10/T11/T12 song song |
| **[Phase 2: Điều khiển và cú sút](phase-2-dieu-khien.md)** | `T13–T15` | tuần 6–7 | ~3 ngày | T13 trước, rồi T14/T15 |
| **[Phase 3: Thủ môn](phase-3-thu-mon.md)** | `T16–T21` | tuần 8–10 | ~8 ngày | T16/T17 song song, rồi tuần tự |
| **[Phase 4: Luật và trận đấu](phase-4-tran-dau.md)** | `T22–T25` | tuần 11–12 | ~4 ngày | T22/T24/T25 song song |
| **[Phase 5: Trình diễn](phase-5-trinh-dien.md)** | `T26–T32` | tuần 15–21 | ~16 ngày | Hầu hết song song được |
| **[Phase 6: Kiểm chứng tự động](phase-6-kiem-chung.md)** | `T33–T34` | tuần 30–32 | ~3 ngày | Tuần tự |
| **[Phase 7: Hoạt ảnh và IK](phase-7-hoat-anh-ik.md)** | `T35–T40` | tuần 11–14 | ~14 ngày | T36/T38 song song; T40 độc lập |
| **[Phase 8: Vòng lặp game và chế độ chơi](phase-8-vong-lap-game.md)** | `T41–T46` | tuần 23–26 | ~14 ngày | T41 trước, rồi T42/T44/T46 song song |
| **[Phase 9: Âm thanh và cảm giác](phase-9-am-thanh-cam-giac.md)** | `T47–T51` | tuần 27–29 | ~10 ngày | T47 trước, rồi phần lớn song song |
| **[Phase 10: Tối ưu, đánh bóng và phát hành](phase-10-toi-uu-phat-hanh.md)** | `T52–T58` | tuần 30–36 | ~13 ngày | Hầu hết song song được |

Lịch tuần bám theo mốc M0–M8 ở [plan.md](../plan.md) mục 08, nên số tuần **chồng nhau giữa vài
phase** — Phase 7 (M3) và Phase 4 (M2) cùng nằm ở tuần 11–14. Đó là chủ ý của lộ trình, không
phải lỗi đánh máy: những phase chồng nhau là những phase xen kẽ ngày code và ngày art.

**Tổng 58 task · 343 mục nghiệm thu · ~98 ngày công code.**
Con số này chỉ tính phần agent làm được — xem [phần dưới](#những-việc-không-giao-cho-ai-được).

<details>
<summary><strong>Danh sách đầy đủ 58 task</strong> (ᴰ = phải tất định theo seed)</summary>

**[Phase 0 — Nền tảng](phase-0-nen-tang.md)**

- `T01` [Khởi tạo repo và Git LFS](phase-0-nen-tang.md#t01-khởi-tạo-repo-và-git-lfs)
- `T02` [Cây thư mục và assembly definition](phase-0-nen-tang.md#t02-cây-thư-mục-và-assembly-definition)
- `T03` [Hệ thống bậc chất lượng A/B/C](phase-0-nen-tang.md#t03-hệ-thống-bậc-chất-lượng-abc)
- `T04` [HUD đo hiệu năng trên máy thật](phase-0-nen-tang.md#t04-hud-đo-hiệu-năng-trên-máy-thật)
- `T05` [Script build một lệnh](phase-0-nen-tang.md#t05-script-build-một-lệnh)

**[Phase 1 — Vật lý bóng](phase-1-vat-ly-bong.md)**

- `T06` [BallSolver, hàm thuần](phase-1-vat-ly-bong.md#t06-ballsolver-hàm-thuần) ᴰ
- `T07` [Bộ test tính tất định của solver](phase-1-vat-ly-bong.md#t07-bộ-test-tính-tất-định-của-solver) ᴰ
- `T08` [TrajectoryPredictor](phase-1-vat-ly-bong.md#t08-trajectorypredictor)
- `T09` [BallDriver, đồng hồ riêng và nội suy](phase-1-vat-ly-bong.md#t09-balldriver-đồng-hồ-riêng-và-nội-suy)
- `T10` [GoalGeometry và phân loại kết quả](phase-1-vat-ly-bong.md#t10-goalgeometry-và-phân-loại-kết-quả) ᴰ
- `T11` [Công cụ xem quỹ đạo trong Editor](phase-1-vat-ly-bong.md#t11-công-cụ-xem-quỹ-đạo-trong-editor)
- `T12` [Công cụ fit tham số từ video thật](phase-1-vat-ly-bong.md#t12-công-cụ-fit-tham-số-từ-video-thật)

**[Phase 2 — Điều khiển và cú sút](phase-2-dieu-khien.md)**

- `T13` [Thu và phân tích cử chỉ vuốt](phase-2-dieu-khien.md#t13-thu-và-phân-tích-cử-chỉ-vuốt)
- `T14` [Ánh xạ cử chỉ sang thông số cú sút](phase-2-dieu-khien.md#t14-ánh-xạ-cử-chỉ-sang-thông-số-cú-sút)
- `T15` [Cửa sổ thời điểm và bất ổn định knuckle](phase-2-dieu-khien.md#t15-cửa-sổ-thời-điểm-và-bất-ổn-định-knuckle) ᴰ

**[Phase 3 — Thủ môn](phase-3-thu-mon.md)**

- `T16` [Mô hình vùng với tới](phase-3-thu-mon.md#t16-mô-hình-vùng-với-tới)
- `T17` [Trích xuất tín hiệu đọc vị](phase-3-thu-mon.md#t17-trích-xuất-tín-hiệu-đọc-vị)
- `T18` [Suy luận đọc vị theo độ tin cậy](phase-3-thu-mon.md#t18-suy-luận-đọc-vị-theo-độ-tin-cậy) ᴰ
- `T19` [Máy trạng thái cam kết và bay người](phase-3-thu-mon.md#t19-máy-trạng-thái-cam-kết-và-bay-người) ᴰ
- `T20` [Trí nhớ thói quen người sút](phase-3-thu-mon.md#t20-trí-nhớ-thói-quen-người-sút) ᴰ
- `T21` [Phân giải pha cản phá](phase-3-thu-mon.md#t21-phân-giải-pha-cản-phá) ᴰ

**[Phase 4 — Luật và trận đấu](phase-4-tran-dau.md)**

- `T22` [Luật luân lưu, hàm thuần](phase-4-tran-dau.md#t22-luật-luân-lưu-hàm-thuần) ᴰ
- `T23` [Máy trạng thái lượt sút](phase-4-tran-dau.md#t23-máy-trạng-thái-lượt-sút)
- `T24` [Lưu tiến trình](phase-4-tran-dau.md#t24-lưu-tiến-trình)
- `T25` [Cấu hình độ khó](phase-4-tran-dau.md#t25-cấu-hình-độ-khó)

**[Phase 5 — Trình diễn](phase-5-trinh-dien.md)**

- `T26` [Đạo diễn camera](phase-5-trinh-dien.md#t26-đạo-diễn-camera)
- `T27` [Hệ thống replay](phase-5-trinh-dien.md#t27-hệ-thống-replay) ᴰ
- `T28` [Mô phỏng lưới Verlet](phase-5-trinh-dien.md#t28-mô-phỏng-lưới-verlet)
- `T29` [Hệ thống cỏ instanced](phase-5-trinh-dien.md#t29-hệ-thống-cỏ-instanced)
- `T30` [Khán giả impostor](phase-5-trinh-dien.md#t30-khán-giả-impostor)
- `T31` [Shader da tán xạ dưới bề mặt](phase-5-trinh-dien.md#t31-shader-da-tán-xạ-dưới-bề-mặt)
- `T32` [Cấu hình hậu kỳ theo bậc](phase-5-trinh-dien.md#t32-cấu-hình-hậu-kỳ-theo-bậc)

**[Phase 6 — Kiểm chứng tự động](phase-6-kiem-chung.md)**

- `T33` [Bộ đo hiệu năng hồi quy](phase-6-kiem-chung.md#t33-bộ-đo-hiệu-năng-hồi-quy)
- `T34` [Test ngâm và kiểm nhiệt](phase-6-kiem-chung.md#t34-test-ngâm-và-kiểm-nhiệt)

**[Phase 7 — Hoạt ảnh và IK](phase-7-hoat-anh-ik.md)**

- `T35` [Máy trạng thái hoạt ảnh người sút](phase-7-hoat-anh-ik.md)
- `T36` [IK chân sút gặp bóng](phase-7-hoat-anh-ik.md)
- `T37` [Đặt chân trụ và hiệu chuẩn lại tín hiệu đọc vị](phase-7-hoat-anh-ik.md) ᴰ
- `T38` [Hoạt ảnh và IK thủ môn](phase-7-hoat-anh-ik.md)
- `T39` [Bộ đo chất lượng hoạt ảnh](phase-7-hoat-anh-ik.md)
- `T40` [Gắn shader da tán xạ vào nhân vật thật](phase-7-hoat-anh-ik.md)

**[Phase 8 — Vòng lặp game và chế độ chơi](phase-8-vong-lap-game.md)**

- `T41` [Máy trạng thái ứng dụng và luồng màn hình](phase-8-vong-lap-game.md)
- `T42` [Chế độ Arcade: chuỗi trận và tiến trình](phase-8-vong-lap-game.md) ᴰ
- `T43` [Chế độ người chơi làm thủ môn](phase-8-vong-lap-game.md)
- `T44` [Cài đặt và hồ sơ người chơi](phase-8-vong-lap-game.md)
- `T45` [Thống kê cú sút và trí nhớ dài hạn của thủ môn](phase-8-vong-lap-game.md) ᴰ
- `T46` [Vòng đời ứng dụng trên máy thật](phase-8-vong-lap-game.md)

**[Phase 9 — Âm thanh và cảm giác](phase-9-am-thanh-cam-giac.md)**

- `T47` [Kiến trúc AudioMixer theo lớp và bậc thiết bị](phase-9-am-thanh-cam-giac.md)
- `T48` [Đám đông phản ứng theo kịch tính trận đấu](phase-9-am-thanh-cam-giac.md)
- `T49` [Rung (haptics) tách biệt theo sự kiện](phase-9-am-thanh-cam-giac.md)
- `T50` [Cảm giác va chạm: hit-stop, rung máy và hậu kỳ nhấn nhịp](phase-9-am-thanh-cam-giac.md)
- `T51` [Bộ đo nghiệm thu M6](phase-9-am-thanh-cam-giac.md)

**[Phase 10 — Tối ưu, đánh bóng và phát hành](phase-10-toi-uu-phat-hanh.md)**

- `T52` [Ngân sách bộ nhớ texture theo bậc và cổng build tự động](phase-10-toi-uu-phat-hanh.md)
- `T53` [Gỡ gói không dùng và danh sách shader variant được phép](phase-10-toi-uu-phat-hanh.md)
- `T54` [Thay ScoreboardUI từ IMGUI sang UGUI](phase-10-toi-uu-phat-hanh.md)
- `T55` [Mật độ và kích thước cỏ, khán giả](phase-10-toi-uu-phat-hanh.md)
- `T56` [Cổng ổn định nhiệt và pin](phase-10-toi-uu-phat-hanh.md)
- `T57` [Chuẩn bị phát hành: định danh, ký, quyền và tuân thủ cửa hàng](phase-10-toi-uu-phat-hanh.md)
- `T58` [Báo cáo lỗi sau phát hành và ranh giới thu thập dữ liệu](phase-10-toi-uu-phat-hanh.md)

</details>

---

## Quy tắc giao việc

Tài liệu này chỉ có giá trị nếu bạn tuân thủ đúng bảy quy tắc dưới đây.
Bỏ quy tắc 2 và 4 là cách nhanh nhất để có một đống code không ghép được với nhau.

1. **Một task cho một phiên làm việc.** Đừng gộp T06 và T08 vào cùng một prompt. Agent sẽ tự ý sửa hợp đồng API của cả hai để chúng "tiện" ghép, và bạn mất tính kiểm chứng độc lập.
2. **Dán nguyên văn khối API vào prompt.** Nói rõ: *không được đổi tên, thêm hoặc bớt tham số, đổi kiểu trả về.* Đây là thứ giữ cho 34 task ghép được với nhau khi làm rời rạc.
3. **Dán nguyên văn checklist nghiệm thu vào prompt ngay từ đầu**, không phải lúc review. Agent biết trước tiêu chí sẽ viết code hướng tới tiêu chí đó.
4. **Không nhận "đã xong" bằng lời.** Yêu cầu agent chạy test và dán output. Với task có test, "code biên dịch được" không phải là xong.
5. **Khoá danh sách file.** Mỗi task khai báo file nó được tạo và sửa. Nếu agent chạm vào file ngoài danh sách đó, từ chối và bắt làm lại — đây là cách duy nhất để chạy nhiều agent song song mà không giẫm chân nhau.
6. **Task có nhãn `TẤT ĐỊNH` phải qua test seed.** Chạy hai lần cùng input phải ra byte giống hệt nhau. Không có tính tất định thì không có replay, không có cân bằng độ khó, và không debug được.
7. **Ghi lại số đo, đừng ghi cảm nhận.** Mọi task hiệu năng phải trả về con số thật từ thiết bị thật. "Chạy mượt" không phải dữ liệu.

### Chuẩn chung cho mọi task C#

> Unity `6000.3` LTS · .NET Standard 2.1 · `Unity.Mathematics` thay cho `UnityEngine.Vector3`
> trong mọi code tính toán · không cấp phát bộ nhớ trong vòng lặp mỗi khung hình ·
> mọi struct dữ liệu là `struct` chứ không phải `class` · **không dùng `UnityEngine.Random`**
> ở bất cứ đâu trong logic gameplay, chỉ dùng `Unity.Mathematics.Random` có seed truyền vào.

---

## Những việc KHÔNG giao cho AI được

Quan trọng ngang danh sách việc code được. Giao nhầm những thứ này cho agent
là cách tiêu thời gian mà không ra kết quả dùng được.

| Việc | Vì sao không | Ai làm |
|---|---|---|
| Dựng model nhân vật | Topology, edge flow quanh khớp, UV — cần mắt và tay người | Bạn, hoặc thuê artist |
| Làm sạch mocap | Phán đoán chuyển động thật/giả từng khung hình | Bạn trong Blender |
| Quyết "trông đã thật chưa" | Đây chính là toàn bộ giá trị của dự án. Không uỷ quyền được. | Bạn + blind test |
| Tinh chỉnh cảm giác | Con số nào "đã tay" chỉ biết bằng cách cầm điện thoại lên chơi | Bạn |
| Thiết kế âm thanh | Chọn và trộn lớp tiếng là việc tai nghe | Bạn |
| Ảnh cửa hàng, trailer | Dựng cảnh và biên tập | Bạn |

AI làm rất tốt phần **hệ thống**: solver, state machine, tooling, test, shader có công thức rõ.
Nó làm rất tệ phần **thẩm mỹ**. Backlog này chia việc đúng theo ranh giới đó.

---

---

## Mẫu prompt giao việc

Dán nguyên khối này, thay phần trong ngoặc vuông. Đừng rút gọn —
mỗi câu trong đó đều chặn một kiểu hỏng cụ thể.

````
Bạn đang làm một task trong dự án Unity 6000.3 LTS tên Eleven Metres,
game sút luân lưu mobile (iOS + Android), render pipeline URP Forward+.

TASK: [T06 — BallSolver]

MỤC TIÊU
[dán đoạn mô tả từ backlog]

HỢP ĐỒNG API — KHÔNG ĐƯỢC THAY ĐỔI
[dán nguyên khối code từ backlog]

Bạn KHÔNG được đổi tên, thêm/bớt tham số, hay đổi kiểu trả về của bất kỳ
thành viên public nào ở trên. Nếu bạn tin rằng hợp đồng có vấn đề, hãy DỪNG
lại và nói ra, đừng tự sửa.

FILE ĐƯỢC PHÉP TẠO HOẶC SỬA
[dán danh sách file]

Không chạm vào bất kỳ file nào khác. Nếu task cần sửa file ngoài danh sách,
hãy dừng và báo.

RÀNG BUỘC
- .NET Standard 2.1, Unity.Mathematics thay cho UnityEngine.Vector3
- Không cấp phát bộ nhớ trong vòng lặp mỗi khung hình
- Không dùng UnityEngine.Random ở bất cứ đâu; chỉ Unity.Mathematics.Random
  với seed truyền vào
- [nếu task có nhãn TẤT ĐỊNH:] Code phải tất định: cùng input cho cùng
  output từng bit, giống nhau giữa Editor và build IL2CPP trên thiết bị

ĐỊNH NGHĨA HOÀN THÀNH
Bạn chỉ được nói "xong" sau khi đã tự chạy qua từng mục dưới đây và dán
bằng chứng cho mỗi mục:

[dán nguyên checklist nghiệm thu]

Với mỗi mục, ghi rõ: ĐẠT kèm bằng chứng (output test, số đo, ảnh chụp),
hoặc KHÔNG ĐẠT kèm lý do. Không được ghi ĐẠT mà không có bằng chứng.
Không được bỏ qua mục nào. Nếu một mục không kiểm được trong môi trường
của bạn, ghi CẦN NGƯỜI KIỂM và nói rõ cần làm gì.
````

> **Câu quan trọng nhất trong mẫu trên:** *"Nếu bạn tin rằng hợp đồng có vấn đề,
> hãy DỪNG lại và nói ra, đừng tự sửa."* — Không có câu này, agent sẽ lặng lẽ đổi
> chữ ký hàm cho hợp ý nó, và bạn phát hiện ra ở task thứ mười hai khi không có gì
> ghép được với nhau nữa.

---

---

## Tổng hợp

| Phase | Task | Ước lượng | Song song được? |
|---|---|---|---|
| 0 · Nền tảng | T01–T05 | ~4 ngày | Không — tuần tự |
| 1 · Vật lý bóng | T06–T12 | ~9 ngày | Sau T06: T08/T10/T11/T12 song song |
| 2 · Điều khiển | T13–T15 | ~3 ngày | T13 trước, rồi T14/T15 |
| 3 · Thủ môn | T16–T21 | ~8 ngày | T16/T17 song song, rồi tuần tự |
| 4 · Trận đấu | T22–T25 | ~4 ngày | T22/T24/T25 song song |
| 5 · Trình diễn | T26–T32 | ~16 ngày | Hầu hết song song được |
| 6 · Kiểm chứng | T33–T34 | ~3 ngày | Tuần tự |
| 7 · Hoạt ảnh và IK | T35–T40 | ~14 ngày | T36/T38 song song; T40 độc lập |
| 8 · Vòng lặp game | T41–T46 | ~14 ngày | T41 trước, rồi T42/T44/T46 song song |
| 9 · Âm thanh và cảm giác | T47–T51 | ~10 ngày | T47 trước, rồi phần lớn song song |
| 10 · Tối ưu và phát hành | T52–T58 | ~13 ngày | Hầu hết song song được |

**Tổng ~98 ngày công code.** Con số này chỉ tính phần agent làm được. Nó *không* bao gồm:
dựng model, làm sạch mocap, chỉnh ánh sáng, tinh chỉnh cảm giác, thiết kế âm thanh,
và toàn bộ vòng lặp thẩm mỹ của M4 — vốn là phần chiếm nhiều thời gian nhất trong 36 tuần của plan.

> **Đọc kỹ trước khi giao việc hàng loạt.** Đừng chạy 34 task này liên tục rồi mới kiểm.
> Sau mỗi phase, tự tay chạy build lên điện thoại và chơi thử. Code qua hết checklist mà ghép lại
> không thành game là chuyện hoàn toàn có thể xảy ra — checklist bắt được lỗi kỹ thuật,
> không bắt được lỗi tích hợp.
