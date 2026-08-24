# Eleven Metres — Backlog kỹ thuật v1

34 đầu việc code được, mỗi việc có hợp đồng API cố định và checklist nghiệm thu
kiểm chứng được bởi người không viết nó. Thiết kế để giao cho agent AI làm từng việc một.

**Dự án:** game sút luân lưu mobile (iOS + Android) · Unity `6000.3` LTS · URP Forward+
**Ngày:** 24/08/2026 · **Kế hoạch tổng thể:** [../plan.md](../plan.md)

> **Bắt đầu ở đâu:** [Quy tắc giao việc](#quy-tắc-giao-việc) → [Phase 0](phase-0-nen-tang.md) → T01.
> Mỗi task là một phiên làm việc riêng. Đừng gộp.

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

**Tổng 34 task · 198 mục nghiệm thu · ~47 ngày công code.**
Con số này chỉ tính phần agent làm được — xem [phần dưới](#những-việc-không-giao-cho-ai-được).

<details>
<summary><strong>Danh sách đầy đủ 34 task</strong> (ᴰ = phải tất định theo seed)</summary>

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

**Tổng ~47 ngày công code.** Con số này chỉ tính phần agent làm được. Nó *không* bao gồm:
dựng model, làm sạch mocap, chỉnh ánh sáng, tinh chỉnh cảm giác, thiết kế âm thanh,
và toàn bộ vòng lặp thẩm mỹ của M4 — vốn là phần chiếm nhiều thời gian nhất trong 36 tuần của plan.

> **Đọc kỹ trước khi giao việc hàng loạt.** Đừng chạy 34 task này liên tục rồi mới kiểm.
> Sau mỗi phase, tự tay chạy build lên điện thoại và chơi thử. Code qua hết checklist mà ghép lại
> không thành game là chuyện hoàn toàn có thể xảy ra — checklist bắt được lỗi kỹ thuật,
> không bắt được lỗi tích hợp.
