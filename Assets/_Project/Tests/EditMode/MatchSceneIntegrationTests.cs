// MatchSceneIntegrationTests.cs — Bộ test nghiệm thu scene Match.unity
// Chứng minh bằng máy rằng scene chơi thử có đủ hệ thống lõi của 7 phase
// và mọi tham chiếu SerializeField đã được nối. KHÔNG sửa, KHÔNG lưu scene.

using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine;
using Eleven.Ball;
using Eleven.Core;
using Eleven.Keeper;
using Eleven.Match;
using Eleven.Presentation;
using Eleven.Presentation.Crowd;
using Eleven.Presentation.Diagnostics;
using Eleven.Presentation.Grass;
using Eleven.Presentation.Net;
using Eleven.Shooter;
using Eleven.UI;

namespace Eleven.Tests.EditMode
{
    /// <summary>
    /// Nghiệm thu Match.unity: xác nhận mọi hệ thống lõi của 7 phase có mặt
    /// trong scene và mọi tham chiếu SerializeField đã được nối đúng.
    /// Mỗi test đỏ = một tính năng cụ thể bị mất trong bản chơi thử.
    /// </summary>
    [TestFixture]
    public class MatchSceneIntegrationTests
    {
        const string ScenePath = "Assets/_Project/Scenes/Match.unity";

        // ── Hằng hình học lấy từ GoalFrame — nguồn sự thật duy nhất ────────────
        const float PostX    = GoalFrame.PostCenterX;     // 3.72
        const float BarY     = GoalFrame.CrossbarCenterY; // 2.50
        const float GoalZ    = GoalFrame.PenaltyDistance; // 11.0
        const float BallR    = 0.11f;

        // ── Sai số chấp nhận được ────────────────────────────────────────────────
        const float PosEps   = 0.02f;  // ±2 cm cho hình học khung thành
        const float BallEps  = 0.01f;  // ±1 cm cho chấm phạt đền

        // ════════════════════════════════════════════════════════════════════════
        //  Setup — mở scene một lần cho toàn bộ fixture, KHÔNG lưu
        // ════════════════════════════════════════════════════════════════════════

        [OneTimeSetUp]
        public void MoScene()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Phase 0 — Bậc thiết bị
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Phase0: TierBootstrap phải tồn tại trong scene để DeviceTier.CurrentProfile
        /// không mãi null và bộ chọn chất lượng đồ hoạ có dữ liệu để hoạt động.
        /// DebugHotkeys cũng được kiểm tra ở đây (xem thêm Phase6).
        /// </summary>
        [Test]
        public void Phase0_CoTierBootstrapVaDebugHotkeys()
        {
            var bootstrap = Object.FindFirstObjectByType<TierBootstrap>();
            Assert.IsNotNull(bootstrap,
                "Thiếu TierBootstrap — DeviceTier.CurrentProfile sẽ mãi null, " +
                "không bậc chất lượng nào được áp, cỏ/khán giả luôn chạy ở mức thấp nhất.");

            var hotkeys = Object.FindFirstObjectByType<DebugHotkeys>();
            Assert.IsNotNull(hotkeys,
                "Thiếu DebugHotkeys — Phase 6 (F1/F2 benchmark, 3-finger PerfHud) " +
                "hoàn toàn không hoạt động, không thể đo hiệu năng trực tiếp trên máy.");
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Phase 1 — Vật lý bóng
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Phase1: BallDriver phải có mặt trên quả bóng và quả bóng phải nằm
        /// đúng tại chấm phạt đền (x≈0, z≈0) ở độ cao bằng bán kính (y≈0.11m).
        /// </summary>
        [Test]
        public void Phase1_CoBallDriverTrenQuaBong()
        {
            var driver = Object.FindFirstObjectByType<BallDriver>();
            Assert.IsNotNull(driver,
                "Thiếu BallDriver — quả bóng không có bộ giải vật lý 120Hz, " +
                "sẽ không bay, không xoáy, không bật cột — toàn bộ Phase 1 tê liệt.");

            Vector3 pos = driver.transform.position;

            Assert.AreEqual(0f, pos.x, BallEps,
                $"Quả bóng lệch ngang {pos.x:F3}m so với chấm phạt đền — " +
                "aimPoint của mọi cú sút sẽ bị lệch hệ thống, kết quả hình học sai.");

            Assert.AreEqual(0f, pos.z, BallEps,
                $"Quả bóng lệch sâu {pos.z:F3}m so với chấm phạt đền — " +
                "khoảng cách tới khung thành sẽ không phải đúng 11m, vật lý bay sai.");

            Assert.AreEqual(BallR, pos.y, BallEps,
                $"Quả bóng ở y={pos.y:F3}m, cần y≈{BallR}m — " +
                "bóng ngập dưới đất hoặc lơ lửng trên không, cú sút đầu tiên sẽ lỗi.");
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Phase 2 — Điều khiển cảm ứng
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Phase2: TouchSwipeReceiver phải tồn tại để nhận ngón tay người chơi,
        /// phân tích vuốt và xây dựng ShotIntent — thiếu nó, game không thể chơi.
        /// </summary>
        [Test]
        public void Phase2_CoTouchSwipeReceiver()
        {
            var swipe = Object.FindFirstObjectByType<TouchSwipeReceiver>();
            Assert.IsNotNull(swipe,
                "Thiếu TouchSwipeReceiver — mọi thao tác chạm/vuốt của người chơi " +
                "sẽ bị nuốt hoàn toàn, không có cú sút nào được kích hoạt trong Phase 2.");
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Phase 3 — Thủ môn và xương tín hiệu
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Phase3: GoalkeeperView phải tồn tại và có tham chiếu leftGlove/rightGlove,
        /// đồng thời KickerBoneCueSource phải có mặt để thủ môn đọc tín hiệu xương.
        /// </summary>
        [Test]
        public void Phase3_CoGoalkeeperViewVaKickerBoneCueSource()
        {
            var keeper = Object.FindFirstObjectByType<GoalkeeperView>();
            Assert.IsNotNull(keeper,
                "Thiếu GoalkeeperView — thủ môn không tồn tại, " +
                "mọi cú sút đều ghi bàn không cần phán xét, Phase 3 không chạy.");

            // Kiểm tra leftGlove và rightGlove bằng SerializedObject
            var so = new SerializedObject(keeper);

            var leftGlove = so.FindProperty("leftGlove");
            Assert.IsNotNull(leftGlove,
                "Không tìm thấy trường 'leftGlove' trong GoalkeeperView — " +
                "cần kiểm tra tên trường trong source code.");
            Assert.IsNotNull(leftGlove.objectReferenceValue,
                "GoalkeeperView.leftGlove chưa được nối — tay trái thủ môn sẽ không di chuyển, " +
                "SaveResolver không tính được khoảng cách tay-bóng, mọi cú sút đều trượt qua.");

            var rightGlove = so.FindProperty("rightGlove");
            Assert.IsNotNull(rightGlove,
                "Không tìm thấy trường 'rightGlove' trong GoalkeeperView — " +
                "cần kiểm tra tên trường trong source code.");
            Assert.IsNotNull(rightGlove.objectReferenceValue,
                "GoalkeeperView.rightGlove chưa được nối — tay phải thủ môn sẽ không di chuyển, " +
                "mọi cú sút về phía phải đều bị bỏ lọt không phán được.");

            var cueSource = Object.FindFirstObjectByType<KickerBoneCueSource>();
            Assert.IsNotNull(cueSource,
                "Thiếu KickerBoneCueSource — thủ môn không nhận được tín hiệu xương người sút, " +
                "BayesianKeeperBrain mất dữ liệu đầu vào, thủ môn sẽ đứng im không phản ứng.");
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Phase 4 — Luật luân lưu và hồ sơ độ khó
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Phase4: MatchGameLoop phải có đủ ba hồ sơ độ khó (easy/medium/hard),
        /// thiếu một trong ba thì DifficultySelector không xây được, chỉ chạy
        /// ở chế độ mặc định cứng — người chơi không thể chọn độ khó.
        /// </summary>
        [Test]
        public void Phase4_MatchGameLoopCoDuBaHoSoDoKho()
        {
            var loop = Object.FindFirstObjectByType<MatchGameLoop>();
            Assert.IsNotNull(loop,
                "Thiếu MatchGameLoop — nhạc trưởng của trận đấu không có mặt, " +
                "không có phase nào trong 7 phase được điều phối, game không chạy.");

            var so = new SerializedObject(loop);

            var easyProp = so.FindProperty("easyProfile");
            Assert.IsNotNull(easyProp,
                "Không tìm thấy trường 'easyProfile' trong MatchGameLoop — " +
                "kiểm tra lại tên trường trong source code.");
            Assert.IsNotNull(easyProp.objectReferenceValue,
                "MatchGameLoop.easyProfile chưa được nối — DifficultySelector không khởi tạo được, " +
                "nút chọn 'DỄ' sẽ không có hồ sơ thủ môn tương ứng.");

            var mediumProp = so.FindProperty("mediumProfile");
            Assert.IsNotNull(mediumProp,
                "Không tìm thấy trường 'mediumProfile' trong MatchGameLoop.");
            Assert.IsNotNull(mediumProp.objectReferenceValue,
                "MatchGameLoop.mediumProfile chưa được nối — DifficultySelector không khởi tạo được, " +
                "nút chọn 'TRUNG BÌNH' sẽ không có hồ sơ thủ môn tương ứng.");

            var hardProp = so.FindProperty("hardProfile");
            Assert.IsNotNull(hardProp,
                "Không tìm thấy trường 'hardProfile' trong MatchGameLoop.");
            Assert.IsNotNull(hardProp.objectReferenceValue,
                "MatchGameLoop.hardProfile chưa được nối — DifficultySelector không khởi tạo được, " +
                "nút chọn 'KHÓ' sẽ không có hồ sơ thủ môn tương ứng.");
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Phase 5 — Trình bày (camera, lưới, cỏ, khán giả)
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Phase5: CameraRig, GoalNetView, GrassFieldRenderer và CrowdRenderer
        /// đều phải tồn tại trong scene để phần trình bày hình ảnh hoạt động.
        /// </summary>
        [Test]
        public void Phase5_CoCameraRigGoalNetViewGrassRendererCrowdRenderer()
        {
            var rig = Object.FindFirstObjectByType<CameraRig>();
            Assert.IsNotNull(rig,
                "Thiếu CameraRig — không có góc quay nào hoạt động, " +
                "người chơi chỉ thấy màn hình đen hoặc góc quay mặc định của Unity không có logic.");

            var netView = Object.FindFirstObjectByType<GoalNetView>();
            Assert.IsNotNull(netView,
                "Thiếu GoalNetView — lưới Verlet không được vẽ, " +
                "bóng vào lưới sẽ không có hiệu ứng rung lưới, cảm giác ghi bàn mất đi.");

            var grass = Object.FindFirstObjectByType<GrassFieldRenderer>();
            Assert.IsNotNull(grass,
                "Thiếu GrassFieldRenderer — mặt sân sẽ không có lá cỏ instanced, " +
                "chỉ còn plane xanh lá, visual bị tụt xuống mức bản thảo.");

            var crowd = Object.FindFirstObjectByType<CrowdRenderer>();
            Assert.IsNotNull(crowd,
                "Thiếu CrowdRenderer — khán đài trống không, không có khán giả impostor, " +
                "mọi hiệu ứng hò reo / hụ còi đều thiếu hình ảnh đám đông tương ứng.");
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Phase 6 — Benchmark và PerfHud
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Phase6: DebugHotkeys phải tồn tại để F1/F2 và cử chỉ 3 ngón hoạt động.
        /// Assert riêng biệt để khi test đỏ, thông điệp nói đúng Phase 6.
        /// </summary>
        [Test]
        public void Phase6_DebugHotkeysTonTai()
        {
            var hotkeys = Object.FindFirstObjectByType<DebugHotkeys>();
            Assert.IsNotNull(hotkeys,
                "Thiếu DebugHotkeys (Phase 6) — BenchmarkRunner.RunStandardSuite không có " +
                "đầu vào kích hoạt, không thể đo hiệu năng 20 kịch bản trên máy thật, " +
                "báo cáo CSV sẽ không bao giờ được ghi ra.");
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Toàn bộ SerializeField của MatchGameLoop
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Kiểm tra TẤT CẢ các trường [SerializeField] kiểu tham chiếu của MatchGameLoop
        /// đều đã được nối. Danh sách lấy từ source code MatchGameLoop.cs.
        /// Mỗi tham chiếu null = một hệ thống tương ứng sẽ bị vô hiệu hoá lặng lẽ.
        /// </summary>
        [Test]
        public void MatchGameLoop_MoiThamChieuSceneDeuDuocNoi()
        {
            var loop = Object.FindFirstObjectByType<MatchGameLoop>();
            Assert.IsNotNull(loop,
                "Thiếu MatchGameLoop — không thể kiểm tra tham chiếu scene.");

            var so = new SerializedObject(loop);

            // Danh sách đầy đủ từ MatchGameLoop.cs (dòng 41-56):
            // ballTransform, ballTrail, goalNet, goalkeeper, kicker,
            // cueSource, swipeReceiver, scoreboard, cameraRig,
            // saveLifecycle, audioDirector, easyProfile, mediumProfile, hardProfile

            AssertRef(so, "ballTransform",
                "MatchGameLoop.ballTransform null — BallDriver không được gắn vào vòng lặp, " +
                "bóng sẽ đứng yên suốt trận, không có cú sút nào bay được.");

            AssertRef(so, "ballTrail",
                "MatchGameLoop.ballTrail null — đường đuôi màu sắc theo loại cú sút không hiển thị, " +
                "người chơi không phân biệt được InsideFoot/Knuckle/Chip bằng mắt.");

            AssertRef(so, "goalNet",
                "MatchGameLoop.goalNet null — GoalNetView.Initialize() không được gọi, " +
                "lưới Verlet không khởi tạo, bóng đi xuyên qua lưới không rung.");

            AssertRef(so, "goalkeeper",
                "MatchGameLoop.goalkeeper null — thủ môn không được gọi TickRead/TickDive, " +
                "đứng im như tượng, mọi cú sút đều ghi bàn không cần kỹ thuật.");

            // Hai ô người sút: model Humanoid (T35) và greybox dự phòng. MatchGameLoop chọn
            // model nếu có. Trống CẢ HAI mới là hỏng — nên không dùng AssertRef cho từng ô.
            Assert.That(so.FindProperty("kickerModel"), Is.Not.Null,
                "Không tìm thấy trường 'kickerModel' trong MatchGameLoop.");
            Assert.That(so.FindProperty("kickerGreybox"), Is.Not.Null,
                "Không tìm thấy trường 'kickerGreybox' trong MatchGameLoop.");
            Assert.That(so.FindProperty("kickerModel").objectReferenceValue != null
                     || so.FindProperty("kickerGreybox").objectReferenceValue != null, Is.True,
                "Cả kickerModel lẫn kickerGreybox đều null — hoạt ảnh chạy đà/đặt chân/đá bóng " +
                "không chạy, người sút biến mất hoàn toàn khỏi màn hình.");

            AssertRef(so, "cueSource",
                "MatchGameLoop.cueSource null — KickerBoneCueSource.Sample() không được gọi, " +
                "thủ môn mất hoàn toàn nguồn tín hiệu xương, luôn đứng giữa cầu môn.");

            AssertRef(so, "swipeReceiver",
                "MatchGameLoop.swipeReceiver null — OnSwipeReleased không được đăng ký, " +
                "người chơi vuốt nhưng không có ShotIntent nào được tạo ra.");

            AssertRef(so, "scoreboard",
                "MatchGameLoop.scoreboard null — bảng tỷ số, banner kết quả, " +
                "thanh thời điểm, nhãn độ khó đều không hiển thị.");

            AssertRef(so, "cameraRig",
                "MatchGameLoop.cameraRig null — không có góc quay nào được chuyển đổi " +
                "theo pha luân lưu, rung máy và bám bóng đều mất.");

            AssertRef(so, "saveLifecycle",
                "MatchGameLoop.saveLifecycle null — kết quả trận không được lưu, " +
                "khởi động lại app sẽ mất toàn bộ tiến trình, không thể chơi tiếp trận dở.");

            AssertRef(so, "audioDirector",
                "MatchGameLoop.audioDirector null — không có âm thanh còi, đá bóng, " +
                "reo hò, ghi bàn — toàn bộ trải nghiệm âm thanh tắt.");
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Hình học khung thành IFAB
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Kiểm tra cột dọc và xà ngang có đúng số liệu IFAB (từ GoalFrame).
        /// MatchSceneGenerator đặt tên: "LeftPost", "RightPost", "Crossbar".
        /// Sai số ±2cm — đủ nghiêm ngặt để phát hiện gõ nhầm số.
        /// </summary>
        [Test]
        public void KhungThanh_DungSoLieuIFAB()
        {
            // Tìm theo tên — MatchSceneGenerator.BuildGoal đặt tên cố định
            GameObject leftPost  = GameObject.Find("LeftPost");
            GameObject rightPost = GameObject.Find("RightPost");
            GameObject crossbar  = GameObject.Find("Crossbar");

            Assert.IsNotNull(leftPost,
                "Không tìm thấy GameObject 'LeftPost' — khung thành không được dựng, " +
                "GoalGeometry.Classify sẽ không có vật thể để phân loại vào/trượt.");
            Assert.IsNotNull(rightPost,
                "Không tìm thấy GameObject 'RightPost' — cột phải bị thiếu, " +
                "cú sút cột phải sẽ phán sai kết quả hình học.");
            Assert.IsNotNull(crossbar,
                "Không tìm thấy GameObject 'Crossbar' — xà ngang bị thiếu, " +
                "cú sút dội xà sẽ không có vật thể để bật lại.");

            Vector3 lp = leftPost.transform.position;
            Vector3 rp = rightPost.transform.position;
            Vector3 cb = crossbar.transform.position;

            // Cột trái: x = -PostX, z = GoalZ
            Assert.AreEqual(-PostX, lp.x, PosEps,
                $"LeftPost.x = {lp.x:F3}m, cần {-PostX}m — cột trái sai vị trí ngang, " +
                "chiều rộng khung thành không đúng 7.32m IFAB.");
            Assert.AreEqual(GoalZ, lp.z, PosEps,
                $"LeftPost.z = {lp.z:F3}m, cần {GoalZ}m — cột trái không nằm trên vạch vôi, " +
                "vị trí cầu môn lệch khỏi chấm phạt đền 11m.");

            // Cột phải: x = +PostX, z = GoalZ
            Assert.AreEqual(PostX, rp.x, PosEps,
                $"RightPost.x = {rp.x:F3}m, cần {PostX}m — cột phải sai vị trí ngang, " +
                "chiều rộng khung thành không đúng 7.32m IFAB.");
            Assert.AreEqual(GoalZ, rp.z, PosEps,
                $"RightPost.z = {rp.z:F3}m, cần {GoalZ}m — cột phải không nằm trên vạch vôi.");

            // Xà ngang: y = BarY, z = GoalZ
            Assert.AreEqual(BarY, cb.y, PosEps,
                $"Crossbar.y = {cb.y:F3}m, cần {BarY}m — xà ngang sai độ cao, " +
                "cú sút dội xà vào/ra ngoài sẽ phán sai, không đúng chiều cao 2.44m IFAB.");
            Assert.AreEqual(GoalZ, cb.z, PosEps,
                $"Crossbar.z = {cb.z:F3}m, cần {GoalZ}m — xà ngang không nằm trên vạch vôi, " +
                "toàn bộ hình học khung thành bị lệch theo chiều sâu.");
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Vùng hợp lệ của camera
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Kiểm tra camera thật trong scene nằm trong CameraAuthoredBounds.
        /// Nằm ngoài = người chơi thấy mép sân chưa được dựng.
        /// </summary>
        [Test]
        public void Camera_NamTrongVungDaDung()
        {
            var rig = Object.FindFirstObjectByType<CameraRig>();
            Assert.IsNotNull(rig,
                "Thiếu CameraRig — không thể kiểm tra vị trí camera.");

            float3 pos = (float3)(Vector3)rig.transform.position;
            bool within = CameraAuthoredBounds.IsWithin(in pos);

            Assert.IsTrue(within,
                $"Camera ở ({pos.x:F2}, {pos.y:F2}, {pos.z:F2}) nằm ngoài vùng đã dựng " +
                $"[{CameraAuthoredBounds.MinBounds.x},{CameraAuthoredBounds.MaxBounds.x}] × " +
                $"[{CameraAuthoredBounds.MinBounds.y},{CameraAuthoredBounds.MaxBounds.y}] × " +
                $"[{CameraAuthoredBounds.MinBounds.z},{CameraAuthoredBounds.MaxBounds.z}] — " +
                "người chơi sẽ thấy mép sân bị cắt bỏ hoặc khoảng trống đen.");
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Tiện ích nội bộ
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Kiểm tra một trường SerializeField kiểu tham chiếu đã được nối.
        /// Dùng SerializedObject để truy cập trường private — API chính thức của Unity Editor.
        /// </summary>
        static void AssertRef(SerializedObject so, string fieldName, string messageIfNull)
        {
            var prop = so.FindProperty(fieldName);
            Assert.IsNotNull(prop,
                $"Không tìm thấy trường '{fieldName}' trong {so.targetObject.GetType().Name} — " +
                "có thể tên trường đã đổi, cần cập nhật test.");
            Assert.IsNotNull(prop.objectReferenceValue, messageIfNull);
        }
    }
}
