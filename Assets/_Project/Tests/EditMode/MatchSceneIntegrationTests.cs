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
using Eleven.Presentation.Kicker;
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

        /// <summary>
        /// Phase3: thủ môn trong scene phải là MODEL CÓ XƯƠNG, và hai "găng tay" mà
        /// GoalkeeperView vươn tới bóng phải là CHÍNH XƯƠNG BÀN TAY của model đó.
        ///
        /// Bản trước gắn thêm hai quả cầu xám vào đúng khớp cổ tay: bàn tay thật của model
        /// chọc xuyên qua chúng, thấy rõ trong khung hình chụp ngày 2026-08-28. Nối thẳng
        /// vào xương thì lúc TickDive đặt vị trí thế giới lên "găng", bàn tay THẬT bay tới
        /// đúng điểm SaveResolver đem đi chấm — mắt thấy gì thì máy chấm nấy.
        ///
        /// Kèm theo là cờ rootAtFeet: model Mixamo lấy gốc ở gót chân, còn mấy con số bay
        /// người trong TickDive được chỉnh cho khối capsule gốc giữa thân. Sai cờ này là
        /// những pha bay thấp cho ra y âm — thủ môn chui xuống dưới mặt cỏ.
        /// </summary>
        [Test]
        public void Phase3_ThuMonLaModelCoXuongVaGangTayLaXuongBanTay()
        {
            var keeper = Object.FindFirstObjectByType<GoalkeeperView>();
            Assert.IsNotNull(keeper, "Thiếu GoalkeeperView — xem Phase3_CoGoalkeeperViewVaKickerBoneCueSource.");

            var animator = keeper.GetComponentInChildren<Animator>();
            Assert.IsNotNull(animator,
                "Thủ môn không có Animator — scene đang dùng nhánh greybox khối hộp. " +
                "Chạy Eleven > Art > Build Keeper Animator Controller rồi dựng lại scene.");
            Assert.IsNotNull(animator.avatar,
                "Animator thủ môn thiếu Avatar — không lấy được xương bàn tay.");
            Assert.IsTrue(animator.avatar.isHuman,
                "Avatar thủ môn không phải Humanoid — GetBoneTransform trả về null, " +
                "găng tay tụt về gốc và pha bay người vươn tay sai chỗ.");
            Assert.IsNotNull(animator.runtimeAnimatorController,
                "Animator thủ môn chưa gán controller — thủ môn đứng chết ở tư thế T-pose cả lượt chờ.");

            var so = new SerializedObject(keeper);
            Assert.IsTrue(so.FindProperty("rootAtFeet").boolValue,
                "rootAtFeet phải bật khi thủ môn là model Mixamo (gốc ở gót chân). " +
                "Tắt là những pha bay thấp cho ra y âm — thủ môn chui xuống dưới mặt cỏ.");

            var handL = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            var handR = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Assert.IsNotNull(handL, "Avatar thủ môn không trả về xương bàn tay trái.");
            Assert.IsNotNull(handR, "Avatar thủ môn không trả về xương bàn tay phải.");

            Assert.AreEqual(handL, so.FindProperty("leftGlove").objectReferenceValue,
                "leftGlove không phải xương bàn tay trái — nếu đây là quả cầu gắn thêm thì " +
                "bàn tay thật sẽ chọc xuyên qua nó, và người chơi thấy hai hòn xám ở cổ tay.");
            Assert.AreEqual(handR, so.FindProperty("rightGlove").objectReferenceValue,
                "rightGlove không phải xương bàn tay phải — xem lý do ở dòng trên.");
        }

        /// <summary>
        /// Phase3: thủ môn phải có ĐỦ CHÍN clip bay người — mỗi ô lưới khung thành một clip —
        /// và bộ ghim tay IK phải ngồi đúng chỗ.
        ///
        /// Trước 2026-08-28 controller chỉ có mỗi state "Idle" và pha bay người là code lái
        /// thân người trần trụi sau khi TẮT Animator: thủ môn trượt ngang như tủ lạnh rồi
        /// đứng hình ở tư thế cuối cho tới hết lượt, không bao giờ đứng dậy. Test này đỏ
        /// nghĩa là đã tụt lại về đúng trạng thái đó.
        ///
        /// Ba điều kiện, mất một là hỏng theo ba kiểu khác nhau:
        ///  • Thiếu state Dive{ô}: GoalkeeperView không phát được clip, thủ môn đứng chờ suốt
        ///    trong lúc thân người vẫn trượt đi — tệ hơn cả bản cũ.
        ///  • Thiếu IK Pass trên tầng 0: Unity KHÔNG gọi OnAnimatorIK, im lặng, không báo lỗi.
        ///    Bàn tay đi theo clip diễn viên chứ không theo KeeperReach, và người chơi thấy
        ///    tay chạm bóng trong khi máy báo thủng lưới.
        ///  • Thiếu KeeperHandIK cạnh Animator: y hệt trên, cũng im lặng.
        /// </summary>
        [Test]
        public void Phase3_ThuMonCoDuChinClipBayNguoiVaGhimTayBangIK()
        {
            var keeper = Object.FindFirstObjectByType<GoalkeeperView>();
            Assert.IsNotNull(keeper, "Thiếu GoalkeeperView — xem Phase3_CoGoalkeeperViewVaKickerBoneCueSource.");

            var animator = keeper.GetComponentInChildren<Animator>();
            Assert.IsNotNull(animator, "Thủ môn không có Animator — xem Phase3_ThuMonLaModelCoXuongVaGangTayLaXuongBanTay.");

            var controller = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
            Assert.IsNotNull(controller,
                "Controller thủ môn không phải AnimatorController dựng sẵn — " +
                "chạy Eleven > Art > Build Keeper Animator Controller.");

            Assert.IsTrue(controller.layers[0].iKPass,
                "Tầng 0 của controller thủ môn chưa bật IK Pass. Unity sẽ không gọi OnAnimatorIK " +
                "và không báo lỗi gì cả — bàn tay lặng lẽ đi theo clip thay vì theo KeeperReach.");

            var states = controller.layers[0].stateMachine.states;
            for (int cell = 0; cell < 9; cell++)
            {
                string want = "Dive" + cell;
                bool found = false;
                foreach (var st in states)
                {
                    if (st.state.name != want) continue;
                    found = true;
                    Assert.IsNotNull(st.state.motion,
                        $"State {want} tồn tại nhưng không gắn clip nào — thủ môn sẽ đứng hình ở ô {cell}.");
                    break;
                }
                Assert.IsTrue(found,
                    $"Controller thủ môn thiếu state {want} — không có clip bay người cho ô {cell}. " +
                    "GoalkeeperView gọi state theo tên này.");
            }

            var ik = animator.GetComponent<KeeperHandIK>();
            Assert.IsNotNull(ik,
                "KeeperHandIK không nằm CÙNG GameObject với Animator. Unity chỉ gọi OnAnimatorIK " +
                "trên component ngồi cạnh Animator; gắn lên node cha là nó không chạy, im lặng.");

            Assert.IsNotNull(keeper.CatchHand,
                "GoalkeeperView.CatchHand null — không có xương bàn tay nào để quả bóng bắt " +
                "dính bám vào, pha bắt bóng sẽ lùi về nhánh làm rơi bóng xuống cỏ.");

            var so2 = new SerializedObject(keeper);
            Assert.AreEqual(ik, so2.FindProperty("handIK").objectReferenceValue,
                "GoalkeeperView.handIK chưa nối tới KeeperHandIK của model — pha bay người sẽ " +
                "rơi về nhánh đặt thẳng transform lên xương, thứ bị Animator ghi đè ngay khung sau.");
        }

        /// <summary>
        /// Clip của mỗi ô phải ĐỔ NGƯỜI CÙNG BÊN với ô đó trên khung thành.
        ///
        /// Bản 2026-08-28 sai đúng chỗ này: thân thủ môn trượt sang phía bóng (code lo, theo
        /// KeeperReach) nhưng clip lại quăng người về bên kia, nên trên máy thật thấy thủ môn
        /// ngã ngửa, chân đi trước bóng. Máy vẫn chấm đúng vì tay được ghim bằng IK — nghĩa là
        /// KHÔNG một test nào khác bắt được lỗi này, chỉ có mắt người.
        ///
        /// Đo bằng cách lấy mẫu clip thật rồi đọc x của hông ở khung hông xuống thấp nhất,
        /// sau khi đã xoay model 180 độ đúng như thủ môn đứng trong scene. Ba ô giữa (1, 4, 7)
        /// bỏ qua: chúng vốn không nghiêng về bên nào.
        /// </summary>
        [Test]
        public void NgaDungBenVoiOLuoi()
        {
            var keeper = Object.FindFirstObjectByType<GoalkeeperView>();
            Assert.IsNotNull(keeper, "Thiếu GoalkeeperView — xem Phase3_CoGoalkeeperViewVaKickerBoneCueSource.");

            var animator = keeper.GetComponentInChildren<Animator>();
            var controller = animator != null
                ? animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController
                : null;
            Assert.IsNotNull(controller, "Không lấy được AnimatorController thủ môn.");

            // Bản sao rời để lấy mẫu: SampleAnimation ghi thẳng lên transform, đụng vào model
            // trong scene là hỏng scene đang mở.
            var probe = Object.Instantiate(animator.gameObject);
            probe.transform.position = Vector3.zero;
            probe.transform.rotation = Quaternion.Euler(0f, 180f, 0f);   // đúng tư thế đứng chờ

            var probeAnimator = probe.GetComponent<Animator>();
            Transform hips = probeAnimator.GetBoneTransform(HumanBodyBones.Hips);
            Assert.IsNotNull(hips, "Model thủ môn thiếu xương Hips.");

            var states = controller.layers[0].stateMachine.states;

            try
            {
                for (int cell = 0; cell < 9; cell++)
                {
                    if (cell % 3 == 1) continue;   // cột giữa: không có bên nào để so

                    AnimationClip clip = null;
                    foreach (var st in states)
                        if (st.state.name == "Dive" + cell) clip = st.state.motion as AnimationClip;

                    Assert.IsNotNull(clip, $"State Dive{cell} không gắn AnimationClip.");

                    float lowest = float.MaxValue, hipX = 0f;
                    for (int i = 0; i < 40; i++)
                    {
                        clip.SampleAnimation(probe, clip.length * i / 39f);
                        Vector3 h = hips.position;
                        if (h.y < lowest) { lowest = h.y; hipX = h.x; }
                    }

                    float wantSign = Mathf.Sign(GoalFrame.CellCenter(cell).x);
                    Assert.Greater(hipX * wantSign, 0f,
                        $"Ô {cell} nằm bên x={GoalFrame.CellCenter(cell).x:F2} nhưng clip " +
                        $"'{clip.name}' đổ người sang x={hipX:F2} — ngược bên. Thủ môn sẽ trượt " +
                        "theo bóng mà ngã về phía đối diện. Đo lại bằng Eleven ▸ Art ▸ " +
                        "Đo Bên Đổ Người Của Thủ Môn rồi sửa CellClips trong " +
                        "KeeperAnimatorControllerBuilder.");
                }
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
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

            AssertRef(so, "aimTrajectory",
                "MatchGameLoop.aimTrajectory null — vuốt xong không thấy đường bay dự kiến, " +
                "người chơi phải đoán mù xem cú vuốt vừa rồi sẽ đưa bóng đi đâu.");

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
        /// Kiểm tra khung thành NHÌN THẤY có đúng số liệu IFAB (từ GoalFrame).
        ///
        /// Trước đây test này dò ba GameObject tên "LeftPost"/"RightPost"/"Crossbar" — tên do
        /// MatchSceneGenerator tự đặt hồi khung thành còn là primitive. Từ khi khung thành là
        /// model thật, tên các node bên trong FBX thuộc về người dựng model, không thuộc về
        /// dự án này, và bám vào chúng là bám vào thứ mình không kiểm soát.
        ///
        /// Nên chỗ này đo HÌNH HỌC chứ không đọc tên: gộp hộp bao của mọi renderer nằm trên
        /// mặt phẳng vạch vôi z = GoalZ, rồi so mép ngoài với GoalFrame. Cách đo này đúng cho
        /// cả bản model lẫn bản greybox dự phòng, và nó chính là thứ cần bảo vệ: cái mắt thấy
        /// phải trùng cái GoalGeometry.Classify tính.
        /// </summary>
        [Test]
        public void KhungThanh_DungSoLieuIFAB()
        {
            var goal = GameObject.Find("Environment/Goal");
            Assert.IsNotNull(goal,
                "Không tìm thấy Environment/Goal — khung thành không được dựng, " +
                "GoalGeometry.Classify sẽ không có vật thể để phân loại vào/trượt.");

            bool found = false;
            Bounds frame = default;

            foreach (var r in goal.GetComponentsInChildren<MeshRenderer>(true))
            {
                // Lưới Verlet sinh mesh lúc chạy nên trong Editor nó rỗng ở gốc toạ độ;
                // gộp vào thì hộp bao kéo về (0,0,0) và mọi phép đo dưới đây sai hết.
                if (r.bounds.size == Vector3.zero) continue;

                // Chỉ lấy phần nằm trên vạch vôi: khung. Cột chống hậu (z ≈ 12.8) và dây néo
                // không phải khung thành, chúng không tham gia phán vào/trượt.
                if (r.bounds.min.z > GoalZ || r.bounds.max.z < GoalZ) continue;

                if (!found) { frame = r.bounds; found = true; }
                else frame.Encapsulate(r.bounds);
            }

            Assert.IsTrue(found,
                $"Không có renderer nào của Environment/Goal cắt mặt phẳng z = {GoalZ}m — " +
                "khung thành không nằm trên vạch vôi, hoặc chưa được dựng.");

            // Mép NGOÀI của cột: tâm cột ± bán kính. Đây là con số mắt nhìn thấy;
            // mép trong (±GoalFrame.Width/2) mới là con số bộ phân loại dùng.
            float outerX = PostX + GoalFrame.PostRadius;   // 3.78
            float outerY = BarY + GoalFrame.PostRadius;    // 2.56

            Assert.AreEqual(-outerX, frame.min.x, PosEps,
                $"Mép ngoài cột trái ở x = {frame.min.x:F3}m, cần {-outerX}m — " +
                "chiều rộng khung thành không đúng 7.32m IFAB.");
            Assert.AreEqual(outerX, frame.max.x, PosEps,
                $"Mép ngoài cột phải ở x = {frame.max.x:F3}m, cần {outerX}m — " +
                "chiều rộng khung thành không đúng 7.32m IFAB.");
            Assert.AreEqual(outerY, frame.max.y, PosEps,
                $"Mép trên xà ngang ở y = {frame.max.y:F3}m, cần {outerY}m — " +
                "cú sút dội xà vào/ra ngoài sẽ phán sai, không đúng chiều cao 2.44m IFAB.");
            Assert.AreEqual(GoalZ, frame.center.z, PosEps,
                $"Tâm khung thành ở z = {frame.center.z:F3}m, cần {GoalZ}m — " +
                "cầu môn lệch khỏi chấm phạt đền 11m.");
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

        /// <summary>
        /// Người sút trong scene phải là model CÓ DA CÓ THỊT: đủ xương Humanoid cho
        /// MecanimKickerAnimator, và MỌI vật liệu phải có ảnh nền.
        ///
        /// Vế thứ hai không thừa. Ngày 2026-08-28 model Ch38 nhập vào trông hoàn toàn bình
        /// thường trong mọi bảng kiểm — avatar hợp lệ, đủ xương, đúng chiều cao — nhưng ảnh
        /// nằm nhúng trong FBX với đường dẫn trỏ về máy dựng của Mixamo, nên Unity dựng vật
        /// liệu rỗng mà không hề báo lỗi. Ra màn hình là một người trắng bệch. Không có máy
        /// gác chỗ này thì lỗi đó chỉ lộ ra khi đã cài xong lên điện thoại.
        /// </summary>
        [Test]
        public void NguoiSutLaModelCoXuongVaCoTexture()
        {
            var kicker = Object.FindFirstObjectByType<MecanimKickerAnimator>();
            Assert.IsNotNull(kicker,
                "Không thấy MecanimKickerAnimator trong scene — người sút đã tụt về greybox, " +
                "nghĩa là thiếu model, thiếu controller, hoặc Avatar không phải Humanoid.");

            var animator = kicker.GetComponent<Animator>();
            Assert.IsNotNull(animator, "Người sút thiếu Animator.");
            Assert.IsTrue(animator.isHuman,
                "Avatar người sút không phải Humanoid — Mecanim không khớp lại được thư viện " +
                "clip Mixamo, mọi cú sút sẽ đứng hình ở tư thế T.");

            foreach (HumanBodyBones bone in new[]
                     {
                         HumanBodyBones.Hips, HumanBodyBones.LeftFoot,
                         HumanBodyBones.RightFoot, HumanBodyBones.Head,
                     })
            {
                Assert.IsNotNull(animator.GetBoneTransform(bone),
                    $"Avatar người sút thiếu xương {bone} — MecanimKickerAnimator đọc thẳng " +
                    "xương này để tính khung chạm bóng và góc máy cận mặt.");
            }

            var renderers = kicker.GetComponentsInChildren<Renderer>(true);
            Assert.Greater(renderers.Length, 0, "Người sút không có Renderer nào — không nhìn thấy gì.");

            foreach (Renderer r in renderers)
            {
                foreach (Material m in r.sharedMaterials)
                {
                    Assert.IsNotNull(m, $"Renderer '{r.name}' của người sút có ô vật liệu trống.");

                    Texture baseMap = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                    if (baseMap == null && m.HasProperty("_MainTex")) baseMap = m.GetTexture("_MainTex");

                    Assert.IsNotNull(baseMap,
                        $"Vật liệu '{m.name}' của người sút không có ảnh nền — model sẽ ra màn " +
                        "hình trắng trơn. Chạy Eleven ▸ Art ▸ Rút Texture Nhúng Của Nhân Vật " +
                        "rồi nhập lại FBX.");
                }
            }
        }
    }
}
