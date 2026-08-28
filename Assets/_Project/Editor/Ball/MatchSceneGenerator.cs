using System.IO;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Eleven.Ball;
using Eleven.Core;
using Eleven.Keeper;
using Eleven.Match;
using Eleven.Presentation;
using Eleven.Presentation.Aim;
using Eleven.Presentation.Kicker;
using Eleven.Presentation.Audio;
using Eleven.Presentation.Crowd;
using Eleven.Presentation.Diagnostics;
using Eleven.Presentation.Grass;
using Eleven.Presentation.Net;
using Eleven.Shooter;
using Eleven.UI;
using Eleven.Editor.Tools;

namespace Eleven.Editor.SceneSetup
{
    /// <summary>
    /// Dựng lại toàn bộ Match.unity từ đầu, bằng code, chỉ dùng primitive — "bóng xám".
    ///
    /// Quy tắc của bản dựng này: KHÔNG có một hằng số hình học nào được gõ tay hai lần.
    /// Kích thước khung thành, bán kính cột, khoảng cách chấm phạt đền đều đọc từ
    /// <see cref="GoalFrame"/> — cùng nguồn mà bộ phân loại vào/trượt (T10) và bộ phân giải
    /// cản phá (T21) dùng. Đây là lý do cái nhìn thấy và cái tính ra không bao giờ lệch nhau.
    ///
    /// Mọi hệ thống của bảy phase đều được đặt vào scene ở đây; nếu một component biến mất
    /// khỏi hàm này thì tính năng tương ứng không tồn tại trong bản chơi thử.
    /// </summary>
    public static class MatchSceneGenerator
    {
        const string ScenePath = "Assets/_Project/Scenes/Match.unity";
        const string SettingsDir = "Assets/_Project/Settings";

        const float GoalZ = GoalFrame.PenaltyDistance;      // 11.0
        const float PostX = GoalFrame.PostCenterX;          // 3.72
        const float BarY = GoalFrame.CrossbarCenterY;       // 2.50
        const float PostR = GoalFrame.PostRadius;           // 0.06
        const float BallR = 0.11f;

        [MenuItem("Eleven/Gameplay/Dựng Scene Thi Đấu (Match.unity)")]
        public static void GenerateMatchScene()
        {
            // Vật liệu đạo cụ là ASSET; dựng trước để bóng và khung thành có cái mà gán,
            // kể cả trong lần dựng đầu tiên trên một máy vừa clone repo về.
            PropModelLibrary.EnsureMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildLighting();
            var env = new GameObject("Environment").transform;

            BuildPitch(env);
            BuildGoal(env, out GoalNetView netView);
            BuildStands(env);
            BuildFloodlights(env);

            GameObject ball = BuildBall(out TrailRenderer trail);
            GoalkeeperView keeper = BuildKeeper();
            BuildKicker(out var kickerModel, out var kickerGreybox);

            UnityEngine.Camera cam = BuildCamera(out CameraRig rig);
            BuildSystems(ball, trail, netView, keeper, kickerModel, kickerGreybox, rig, cam);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene("Assets/_Project/Scenes/Boot.unity", true)
            };

            Debug.Log($"[MatchSceneGenerator] Đã dựng xong {ScenePath} và đặt làm scene đầu tiên trong Build Settings.");
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Ánh sáng & không khí sân đêm
        // ═══════════════════════════════════════════════════════════════════════

        static void BuildLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.30f, 0.36f, 0.46f);
            RenderSettings.ambientEquatorColor = new Color(0.20f, 0.24f, 0.30f);
            RenderSettings.ambientGroundColor = new Color(0.10f, 0.13f, 0.12f);

            // Sương mù là thứ giấu đi mép của một sân vận động chỉ dựng 12 mét.
            //
            // ĐÃ KIỂM ngày 2026-08-28 khi camera lùi từ z=-4.85 ra z=-10.5: sương mù tuyến
            // tính đo theo khoảng cách TỚI CAMERA, nên lùi máy 5.65m về nguyên tắc đẩy mọi
            // thứ sâu thêm 5.65m vào màn sương. Tính ra thì không đáng kể: khung thành từ
            // 15.85m thành 21.5m, vẫn dưới mốc bắt đầu 26m nên KHÔNG dính sương chút nào;
            // khán đài (z=17) từ 21.9m thành 27.5m, tức mới chỉ 1.5/36 = 4% đậm đặc. Vì vậy
            // hai con số này giữ nguyên. Ghi lại để người sau khỏi "sửa" lại lần nữa.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.05f, 0.07f, 0.11f);
            RenderSettings.fogStartDistance = 26f;
            RenderSettings.fogEndDistance = 62f;

            var lightGo = new GameObject("KeyLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.97f, 0.90f);
            light.intensity = 1.5f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.75f;
            lightGo.transform.rotation = Quaternion.Euler(52f, 26f, 0f);

            var fillGo = new GameObject("FillLight");
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.55f, 0.68f, 0.95f);
            fill.intensity = 0.45f;
            fill.shadows = LightShadows.None;
            fillGo.transform.rotation = Quaternion.Euler(28f, -140f, 0f);
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Mặt sân và vạch vôi (số liệu IFAB)
        // ═══════════════════════════════════════════════════════════════════════

        static void BuildPitch(Transform parent)
        {
            // Mặt cỏ là ẢNH CHỤP CỎ THẬT lát đi lát lại, không còn là mảng màu xanh trơn
            // (đổi 2026-08-28). Số lần lát nằm sẵn trong vật liệu — xem PropModelLibrary.
            // Plane dựng sẵn của Unity rộng 10 đơn vị nên chia 10 ra localScale.
            const float span = PropModelLibrary.PitchSpanMeters;

            var pitch = Primitive(PrimitiveType.Plane, "PitchGround", parent);
            pitch.transform.position = new Vector3(0f, 0f, 6f);
            pitch.transform.localScale = new Vector3(span / 10f, 1f, span / 10f);
            PaintAsset(pitch, PropModelLibrary.PitchGrassMaterial);

            // Sọc cắt cỏ — thứ duy nhất khiến một mặt phẳng xanh trông ra sân bóng. Nay là
            // cùng tấm cỏ đó tô sáng hơn 1.18 lần, đúng như sân thật: máy cắt cỏ chỉ vuốt
            // lá cỏ nằm rạp về một phía chứ không đổi màu cỏ.
            var stripes = new GameObject("MowStripes").transform;
            stripes.SetParent(parent, false);
            for (int i = -6; i <= 6; i++)
            {
                var s = Primitive(PrimitiveType.Cube, $"Stripe_{i + 6}", stripes);
                s.transform.position = new Vector3(0f, 0.004f, 6f + i * 2.6f);
                s.transform.localScale =
                    new Vector3(span, 0.008f, PropModelLibrary.MowStripeDepthMeters);
                PaintAsset(s, PropModelLibrary.PitchStripeMaterial);
            }

            var lines = new GameObject("PitchLines").transform;
            lines.SetParent(parent, false);

            Line(lines, "GoalLine", new Vector3(0f, 0.010f, GoalZ), new Vector3(40.3f, 0.01f, 0.12f));

            // Vòng cấm 16m50: mép trước ở z = 11 - 16.5, rộng 40.32m.
            float boxFrontZ = GoalZ - 16.5f;
            Line(lines, "PenaltyBox_Front", new Vector3(0f, 0.010f, boxFrontZ), new Vector3(40.32f, 0.01f, 0.12f));
            Line(lines, "PenaltyBox_Left", new Vector3(-20.16f, 0.010f, (boxFrontZ + GoalZ) * 0.5f), new Vector3(0.12f, 0.01f, 16.5f));
            Line(lines, "PenaltyBox_Right", new Vector3(20.16f, 0.010f, (boxFrontZ + GoalZ) * 0.5f), new Vector3(0.12f, 0.01f, 16.5f));

            // Vòng 5m50.
            float sixFrontZ = GoalZ - 5.5f;
            Line(lines, "GoalBox_Front", new Vector3(0f, 0.010f, sixFrontZ), new Vector3(18.32f, 0.01f, 0.12f));
            Line(lines, "GoalBox_Left", new Vector3(-9.16f, 0.010f, (sixFrontZ + GoalZ) * 0.5f), new Vector3(0.12f, 0.01f, 5.5f));
            Line(lines, "GoalBox_Right", new Vector3(9.16f, 0.010f, (sixFrontZ + GoalZ) * 0.5f), new Vector3(0.12f, 0.01f, 5.5f));

            // Cung tròn 9m15 quanh chấm phạt đền, dựng bằng các đoạn ngắn.
            var arc = new GameObject("PenaltyArc").transform;
            arc.SetParent(lines, false);
            for (int i = 0; i <= 24; i++)
            {
                float a = math.radians(-52f + i * (104f / 24f));
                var seg = Primitive(PrimitiveType.Cube, $"Arc_{i}", arc);
                seg.transform.position = new Vector3(math.sin(a) * 9.15f, 0.010f, -math.cos(a) * 9.15f);
                seg.transform.rotation = Quaternion.Euler(0f, math.degrees(a), 0f);
                seg.transform.localScale = new Vector3(0.12f, 0.01f, 0.85f);
                Paint(seg, Color.white, lit: false);
            }

            var spot = Primitive(PrimitiveType.Cylinder, "PenaltySpot", lines);
            spot.transform.position = new Vector3(0f, 0.012f, 0f);
            spot.transform.localScale = new Vector3(0.22f, 0.005f, 0.22f);
            Paint(spot, Color.white, lit: false);
        }

        static void Line(Transform parent, string name, Vector3 pos, Vector3 scale)
        {
            var go = Primitive(PrimitiveType.Cube, name, parent);
            go.transform.position = pos;
            go.transform.localScale = scale;
            Paint(go, new Color(0.92f, 0.94f, 0.92f), lit: false);
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Khung thành + lưới Verlet (T28)
        // ═══════════════════════════════════════════════════════════════════════

        static void BuildGoal(Transform parent, out GoalNetView netView)
        {
            var goal = new GameObject("Goal").transform;
            goal.SetParent(parent, false);

            // Khung thành thật (model) nếu có; nếu thiếu file thì lùi về primitive để scene
            // vẫn dựng được trên máy chưa kéo asset về. Lưới thì KHÔNG lấy của model — nó
            // cứng đờ và nặng 221.820 tam giác; lưới của scene này là lưới Verlet ở dưới.
            if (PropModelLibrary.InstantiateGoal(goal, GoalZ) == null)
            {
                Debug.LogWarning($"[MatchSceneGenerator] Thiếu {PropModelLibrary.GoalFbx} — dựng khung thành greybox.");
                BuildGreyboxGoal(goal);
            }

            // Lưới: mesh do NetSimulator sinh mỗi khung, ở đây chỉ cần chỗ để vẽ.
            var netGo = new GameObject("GoalNet_Verlet");
            netGo.transform.SetParent(goal, false);
            netGo.AddComponent<MeshFilter>();
            var netRenderer = netGo.AddComponent<MeshRenderer>();
            netRenderer.sharedMaterial = MakeMaterial(new Color(0.92f, 0.95f, 1f, 0.85f), lit: false);
            netRenderer.shadowCastingMode = ShadowCastingMode.Off;
            netRenderer.receiveShadows = false;
            netView = netGo.AddComponent<GoalNetView>();

            // Bảng quảng cáo sau khung thành: chắn tầm nhìn ra phần sân chưa dựng.
            var boards = new GameObject("AdBoards").transform;
            boards.SetParent(parent, false);
            for (int i = 0; i < 9; i++)
            {
                var b = Primitive(PrimitiveType.Cube, $"Board_{i}", boards);
                b.transform.position = new Vector3(-16f + i * 4f, 0.5f, GoalZ + 3.6f);
                b.transform.localScale = new Vector3(3.8f, 1.0f, 0.12f);
                Paint(b, i % 2 == 0 ? new Color(0.08f, 0.12f, 0.30f) : new Color(0.55f, 0.10f, 0.14f), lit: true);
            }
        }

        /// <summary>
        /// Khung thành bằng primitive — bản dự phòng khi thiếu model. Kích thước vẫn đọc từ
        /// <see cref="GoalFrame"/> nên vào/trượt tính ra y hệt, chỉ khác cái nhìn thấy.
        /// </summary>
        static void BuildGreyboxGoal(Transform goal)
        {
            float postDiameter = PostR * 2f;

            var left = Primitive(PrimitiveType.Cylinder, "LeftPost", goal);
            left.transform.position = new Vector3(-PostX, BarY * 0.5f, GoalZ);
            left.transform.localScale = new Vector3(postDiameter, BarY * 0.5f, postDiameter);
            Paint(left, Color.white, lit: true);

            var right = Primitive(PrimitiveType.Cylinder, "RightPost", goal);
            right.transform.position = new Vector3(PostX, BarY * 0.5f, GoalZ);
            right.transform.localScale = new Vector3(postDiameter, BarY * 0.5f, postDiameter);
            Paint(right, Color.white, lit: true);

            var bar = Primitive(PrimitiveType.Cylinder, "Crossbar", goal);
            bar.transform.position = new Vector3(0f, BarY, GoalZ);
            bar.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            bar.transform.localScale = new Vector3(postDiameter, PostX, postDiameter);
            Paint(bar, Color.white, lit: true);

            foreach (int sign in new[] { -1, 1 })
            {
                var backPost = Primitive(PrimitiveType.Cylinder, sign < 0 ? "LeftBackSupport" : "RightBackSupport", goal);
                backPost.transform.position = new Vector3(sign * PostX, 0.85f, GoalZ + 1.75f);
                backPost.transform.localScale = new Vector3(0.08f, 0.85f, 0.08f);
                Paint(backPost, new Color(0.85f, 0.88f, 0.92f), lit: true);

                var brace = Primitive(PrimitiveType.Cylinder, sign < 0 ? "LeftBrace" : "RightBrace", goal);
                brace.transform.position = new Vector3(sign * PostX, 1.68f, GoalZ + 0.9f);
                brace.transform.rotation = Quaternion.Euler(46f, 0f, 0f);
                brace.transform.localScale = new Vector3(0.07f, 1.20f, 0.07f);
                Paint(brace, new Color(0.85f, 0.88f, 0.92f), lit: true);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Khán đài — hình khối để đám đông impostor (T30) có chỗ đứng
        // ═══════════════════════════════════════════════════════════════════════

        static void BuildStands(Transform parent)
        {
            var stands = new GameObject("Stands").transform;
            stands.SetParent(parent, false);

            // Khán đài chính: 14 hàng bắt đầu ở z = 15.5, mỗi hàng lùi 0.85m và cao thêm 0.42m.
            for (int r = 0; r < CrowdStandLayout.MainStandRows; r++)
            {
                var step = Primitive(PrimitiveType.Cube, $"MainStand_Row{r}", stands);
                float z = CrowdStandLayout.MainStandFrontZ + r * CrowdStandLayout.RowDepth;
                float h = CrowdStandLayout.FirstRowHeight + r * CrowdStandLayout.RowRise;
                step.transform.position = new Vector3(0f, h * 0.5f, z);
                step.transform.localScale = new Vector3(30f, h, CrowdStandLayout.RowDepth);
                Paint(step, new Color(0.09f, 0.10f, 0.14f), lit: true);
            }

            foreach (int sign in new[] { -1, 1 })
            {
                for (int r = 0; r < CrowdStandLayout.WingStandRows; r++)
                {
                    var step = Primitive(PrimitiveType.Cube, $"Wing{(sign < 0 ? "L" : "R")}_Row{r}", stands);
                    float x = sign * (CrowdStandLayout.WingStandStartX + r * CrowdStandLayout.RowDepth);
                    float h = CrowdStandLayout.FirstRowHeight + r * CrowdStandLayout.RowRise;
                    step.transform.position = new Vector3(x, h * 0.5f, 4f);
                    step.transform.localScale = new Vector3(CrowdStandLayout.RowDepth, h, 26f);
                    Paint(step, new Color(0.09f, 0.10f, 0.14f), lit: true);
                }
            }
        }

        static void BuildFloodlights(Transform parent)
        {
            var rig = new GameObject("Floodlights").transform;
            rig.SetParent(parent, false);

            var corners = new[]
            {
                new Vector3(-15f, 0f, 20f), new Vector3(15f, 0f, 20f),
                new Vector3(-15f, 0f, -6f), new Vector3(15f, 0f, -6f)
            };

            for (int i = 0; i < corners.Length; i++)
            {
                var pole = Primitive(PrimitiveType.Cylinder, $"Pole_{i}", rig);
                pole.transform.position = corners[i] + new Vector3(0f, 6f, 0f);
                pole.transform.localScale = new Vector3(0.35f, 6f, 0.35f);
                Paint(pole, new Color(0.16f, 0.17f, 0.20f), lit: true);

                var head = Primitive(PrimitiveType.Cube, $"Lamp_{i}", rig);
                head.transform.position = corners[i] + new Vector3(0f, 12.2f, 0f);
                head.transform.localScale = new Vector3(2.6f, 0.9f, 0.4f);
                head.transform.LookAt(new Vector3(0f, 0f, 6f));
                Paint(head, new Color(0.95f, 0.97f, 1f), lit: false);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Bóng, thủ môn, người sút
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Bóng = gốc điều khiển (scale 1) + phần nhìn thấy làm con.
        ///
        /// Tách hai tầng vì hai lý do, cả hai đều là lỗi đã từng xảy ra:
        ///  • <see cref="BallSpinView"/> xoay phần nhìn thấy; nếu nó xoay chính cái transform
        ///    mà <see cref="BallDriver"/> đang ghi vị trí thì hai bên tranh nhau một transform.
        ///  • <see cref="TrailRenderer"/> nhân bề rộng theo scale của transform nó nằm trên.
        ///    Hồi gốc bóng còn là quả cầu scale 0.22, hai con số 0.15/0.01 dưới đây thật ra
        ///    đang vẽ ra vệt rộng 0.033/0.0022m. Nay gốc scale 1 nên ghi thẳng số mét thật —
        ///    hình vẽ ra không đổi một pixel.
        /// </summary>
        static GameObject BuildBall(out TrailRenderer trail)
        {
            var ball = new GameObject("MatchBall");
            ball.transform.position = new Vector3(0f, BallR, 0f);

            GameObject visual = PropModelLibrary.InstantiateBallVisual(ball.transform);
            if (visual == null)
            {
                Debug.LogWarning($"[MatchSceneGenerator] Thiếu {PropModelLibrary.BallFbx} — dựng bóng greybox.");
                visual = Primitive(PrimitiveType.Sphere, "BallVisual", ball.transform);
                visual.transform.localScale = Vector3.one * (BallR * 2f);
                Paint(visual, new Color(0.97f, 0.97f, 0.98f), lit: true);
            }

            trail = ball.AddComponent<TrailRenderer>();
            trail.time = 0.45f;
            trail.startWidth = 0.033f;
            trail.endWidth = 0.0022f;
            trail.emitting = false;
            trail.numCapVertices = 2;
            trail.sharedMaterial = MakeMaterial(Color.white, lit: false);
            trail.shadowCastingMode = ShadowCastingMode.Off;

            var driver = ball.AddComponent<BallDriver>();

            var spinView = visual.AddComponent<BallSpinView>();
            var spinSo = new SerializedObject(spinView);
            spinSo.FindProperty("driver").objectReferenceValue = driver;
            spinSo.FindProperty("radius").floatValue = BallR;
            spinSo.ApplyModifiedPropertiesWithoutUndo();

            return ball;
        }

        const string KeeperModelPath = "Assets/_Project/Art/Characters/Goalkeeper.fbx";
        const string KeeperControllerPath = "Assets/_Project/Art/Animations/KeeperAnimator.controller";

        /// <summary>
        /// Dựng thủ môn. Ưu tiên model có xương; thiếu model, Avatar hay controller thì lùi về
        /// khối primitive để scene vẫn dựng được — cùng lối rẽ như <see cref="BuildKicker"/>.
        ///
        /// Hai nhánh KHÁC NHAU Ở GỐC TOẠ ĐỘ, và đó là toàn bộ chỗ khó: khối primitive lấy gốc
        /// ở giữa thân (y = 0.95), model Mixamo lấy gốc ở gót chân (y = 0). GoalkeeperView
        /// biết chuyện đó qua cờ <c>rootAtFeet</c>, xem lý do trong chính lớp ấy.
        /// </summary>
        static GoalkeeperView BuildKeeper()
        {
            var go = new GameObject("Goalkeeper");
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            Transform handL = null, handR = null;
            Animator animator = BuildKeeperModel(go.transform, ref handL, ref handR);
            bool hasModel = animator != null;

            if (!hasModel) BuildKeeperGreybox(go.transform);

            // Gốc toạ độ: gót chân với model, giữa thân với khối primitive.
            Vector3 home = new Vector3(0f, hasModel ? 0f : 0.95f, GoalZ);
            go.transform.position = home;

            // "Găng tay" mà GoalkeeperView vươn tới bóng là CHÍNH XƯƠNG BÀN TAY khi có model,
            // chứ không phải một quả cầu gắn thêm. Model này đã có găng thủ môn dựng và tô
            // texture sẵn (màu cam ở cổ tay), nên quả cầu chỉ làm hai việc, cả hai đều xấu:
            // nó là một hòn xám nằm ĐÚNG KHỚP CỔ TAY để bàn tay thật chọc xuyên qua — thấy rõ
            // trong khung hình chụp ngày 2026-08-28 — và nó tốn thêm hai lệnh vẽ.
            //
            // Với model, hai ô này chỉ còn để tra cứu và cho test soi: việc kéo bàn tay tới
            // điểm SaveResolver chấm đã chuyển sang KeeperHandIK gắn ngay dưới đây, vì
            // Animator KHÔNG còn bị tắt lúc bay người nữa (clip lo tư thế) — đặt thẳng
            // transform.position lên xương bây giờ sẽ bị chính Animator ghi đè ngay khung sau.
            // IK còn được cái lợi mà cách cũ không có: nó bẻ cả khuỷu, nên cánh tay vươn ra
            // chứ không giãn dai như sợi kẹo.
            Transform glL = handL, glR = handR;
            if (!hasModel)
            {
                glL = BuildGlove("LeftGlove", go.transform, new Vector3(-0.72f, 1.05f, GoalZ)).transform;
                glR = BuildGlove("RightGlove", go.transform, new Vector3(0.72f, 1.05f, GoalZ)).transform;
            }

            var view = go.AddComponent<GoalkeeperView>();
            var so = new SerializedObject(view);
            so.FindProperty("homePosition").vector3Value = home;
            so.FindProperty("leftGlove").objectReferenceValue = glL;
            so.FindProperty("rightGlove").objectReferenceValue = glR;
            so.FindProperty("rootAtFeet").boolValue = hasModel;
            so.FindProperty("animator").objectReferenceValue = animator;
            so.FindProperty("handIK").objectReferenceValue =
                animator != null ? animator.GetComponent<KeeperHandIK>() : null;
            so.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        /// <summary>
        /// Gắn model thủ môn vào <paramref name="parent"/>. Trả về Animator đã dựng xong, hoặc
        /// null nếu thiếu asset — chỗ gọi tự lùi về greybox.
        /// </summary>
        static Animator BuildKeeperModel(Transform parent, ref Transform handL, ref Transform handR)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(KeeperModelPath);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(KeeperControllerPath);
            var avatarAsset = AssetDatabase.LoadAssetAtPath<Avatar>(KeeperModelPath);

            if (fbx == null || controller == null || avatarAsset == null || !avatarAsset.isHuman)
            {
                Debug.LogWarning($"[MatchSceneGenerator] Thiếu {KeeperModelPath} hoặc {KeeperControllerPath} " +
                                 "(hoặc Avatar không phải Humanoid) — dựng thủ môn greybox thay thế. " +
                                 "Chạy Eleven > Art > Build Keeper Animator Controller rồi dựng lại scene.");
                return null;
            }

            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            model.name = "Body";
            model.transform.SetParent(parent, false);

            var animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
            animator.avatar = avatarAsset;
            animator.runtimeAnimatorController = controller;

            // Vị trí ngang của thủ môn do gameplay đặt (GoalkeeperView), không do clip đẩy.
            animator.applyRootMotion = false;
            // Batchmode và khung hình mà thủ môn nằm ngoài mép màn hình vẫn phải cập nhật:
            // tắt là pha bay người đứng hình đúng lúc người chơi đang nhìn nó.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Vật liệu do PropModelLibrary dựng — importer đặt materialImportMode = None nên
            // model về tay ta không có vật liệu nào, phải gán ở đây.
            var keeperMat = AssetDatabase.LoadAssetAtPath<Material>(PropModelLibrary.KeeperMaterial);
            if (keeperMat != null)
            {
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = keeperMat;
                    r.sharedMaterials = mats;
                }
            }

            // Bộ ghim bàn tay PHẢI ngồi cùng GameObject với Animator: Unity chỉ gọi
            // OnAnimatorIK trên component nằm cạnh Animator, gắn lên node cha là nó im lặng
            // không chạy — và im lặng ở đây nghĩa là bàn tay lệch khỏi điểm chấm điểm.
            if (model.GetComponent<KeeperHandIK>() == null) model.AddComponent<KeeperHandIK>();

            handL = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            handR = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (handL == null || handR == null)
            {
                Debug.LogWarning("[MatchSceneGenerator] Avatar thủ môn không trả về xương bàn tay — " +
                                 "găng tay gắn vào gốc thay vì vào tay.");
            }

            return animator;
        }

        /// <summary>Thủ môn khối hộp — bản dự phòng khi chưa có model, giữ nguyên như trước.</summary>
        static void BuildKeeperGreybox(Transform parent)
        {
            var torso = Primitive(PrimitiveType.Capsule, "Torso", parent);
            torso.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            torso.transform.localScale = new Vector3(0.52f, 0.62f, 0.34f);
            Paint(torso, new Color(0.98f, 0.80f, 0.08f), lit: true);

            var head = Primitive(PrimitiveType.Sphere, "Head", parent);
            head.transform.localPosition = new Vector3(0f, 0.78f, 0f);
            head.transform.localScale = Vector3.one * 0.22f;
            Paint(head, new Color(0.86f, 0.68f, 0.55f), lit: true);

            var legs = Primitive(PrimitiveType.Capsule, "Legs", parent);
            legs.transform.localPosition = new Vector3(0f, -0.55f, 0f);
            legs.transform.localScale = new Vector3(0.30f, 0.42f, 0.28f);
            Paint(legs, new Color(0.12f, 0.12f, 0.16f), lit: true);
        }

        /// <summary>
        /// Một chiếc găng cho THỦ MÔN KHỐI HỘP. Chỉ nhánh dự phòng mới gọi tới: có model thì
        /// bàn tay thật đóng vai này, xem lý do ở <see cref="BuildKeeper"/>. Quả cầu ở đây to
        /// vì nó CHÍNH LÀ bàn tay người chơi nhìn thấy.
        /// </summary>
        static GameObject BuildGlove(string name, Transform parent, Vector3 world)
        {
            var glove = Primitive(PrimitiveType.Sphere, name, parent);
            glove.transform.position = world;
            glove.transform.localScale = Vector3.one * 0.20f;
            Paint(glove, new Color(0.95f, 0.98f, 1f), lit: true);
            return glove;
        }

        /// <summary>
        /// Model người sút NHÌN THẤY trên sân. Cố ý KHÁC với
        /// <see cref="Eleven.Editor.Tools.MixamoModelImport"/>.KickerCharacter (vẫn là XBot.fbx):
        /// XBot là bộ xương gốc để retarget toàn bộ thư viện clip Mixamo — mọi số đo khung
        /// chạm bóng ở T35 tính trên nó, đổi đi là sai hết. Ch38 chỉ thay phần da thịt nhìn
        /// thấy: cả hai đều Humanoid nên Mecanim tự khớp lại xương lúc chạy.
        /// </summary>
        const string KickerModelPath = "Assets/_Project/Art/Characters/Kicker.fbx";
        const string KickerControllerPath = "Assets/_Project/Art/Animations/KickerAnimator.controller";

        /// <summary>
        /// Dựng người sút. Ưu tiên model Humanoid + Mecanim (T35); nếu thiếu model hoặc
        /// controller thì lùi về greybox primitive để scene vẫn dựng được.
        ///
        /// Cả hai đi chung <c>IKickerAnimator</c>, nên <c>MatchGameLoop</c> không biết mình
        /// đang lái cái nào — đó là lý do chỗ này được phép có hai nhánh mà chỗ khác thì không.
        /// </summary>
        static void BuildKicker(out MecanimKickerAnimator model, out KickerAvatar greybox)
        {
            model = null;
            greybox = null;

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(KickerModelPath);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(KickerControllerPath);
            var avatarAsset = AssetDatabase.LoadAssetAtPath<Avatar>(KickerModelPath);

            if (fbx != null && controller != null && avatarAsset != null && avatarAsset.isHuman)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
                go.name = "Kicker";

                var animator = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
                animator.avatar = avatarAsset;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;   // lý do: xem AdvanceRunUp trong MecanimKickerAnimator
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                model = go.AddComponent<MecanimKickerAnimator>();
                go.transform.position = (Vector3)(float3)KickerPlacement.Start;
                return;
            }

            Debug.LogWarning($"[MatchSceneGenerator] Thiếu {KickerModelPath} hoặc {KickerControllerPath} " +
                             "(hoặc Avatar không phải Humanoid) — dựng người sút greybox thay thế. " +
                             "Chạy Eleven ▸ Art ▸ Build Kicker Animator Controller rồi dựng lại scene.");

            var fallback = new GameObject("Kicker");
            greybox = fallback.AddComponent<KickerAvatar>();
            greybox.BuildGreybox();
            greybox.ResetToStart(new float3(0f, BallR, 0f));
        }

        static UnityEngine.Camera BuildCamera(out CameraRig rig)
        {
            var go = new GameObject("MainCamera");
            go.tag = "MainCamera";

            var cam = go.AddComponent<UnityEngine.Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.05f, 0.09f);
            cam.nearClipPlane = 0.08f;
            cam.farClipPlane = 90f;
            cam.fieldOfView = 36f;

            go.AddComponent<AudioListener>();
            rig = go.AddComponent<CameraRig>();

            go.transform.position = new Vector3(0f, 1.72f, -3.6f);
            go.transform.rotation = Quaternion.Euler(3.2f, 0f, 0f);
            return cam;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Hệ thống: bậc máy, âm thanh, cỏ, khán giả, điều khiển, vòng lặp trận
        // ═══════════════════════════════════════════════════════════════════════

        static void BuildSystems(GameObject ball, TrailRenderer trail, GoalNetView net,
                                 GoalkeeperView keeper,
                                 MecanimKickerAnimator kickerModel, KickerAvatar kickerGreybox,
                                 CameraRig rig, UnityEngine.Camera cam)
        {
            // Phase 0 — bậc thiết bị. Không có nó thì DeviceTier.CurrentProfile mãi null.
            var tierGo = new GameObject("TierBootstrap");
            var bootstrap = tierGo.AddComponent<TierBootstrap>();
            var tierSo = new SerializedObject(bootstrap);
            var arr = tierSo.FindProperty("profiles");
            arr.arraySize = 3;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = LoadAsset<TierProfile>($"{SettingsDir}/TierProfile-A.asset");
            arr.GetArrayElementAtIndex(1).objectReferenceValue = LoadAsset<TierProfile>($"{SettingsDir}/TierProfile-B.asset");
            arr.GetArrayElementAtIndex(2).objectReferenceValue = LoadAsset<TierProfile>($"{SettingsDir}/TierProfile-C.asset");
            tierSo.ApplyModifiedPropertiesWithoutUndo();

            // Phase 5 — cỏ và khán giả (dữ liệu đã có test, đây là phần vẽ).
            var grassGo = new GameObject("GrassField");
            grassGo.AddComponent<GrassFieldRenderer>();

            var crowdGo = new GameObject("Crowd");
            var crowd = crowdGo.AddComponent<CrowdRenderer>();
            var crowdSo = new SerializedObject(crowd);
            var camProp = crowdSo.FindProperty("_overrideCamera");
            if (camProp != null) camProp.objectReferenceValue = cam;
            crowdSo.ApplyModifiedPropertiesWithoutUndo();

            // Bộ điều khiển trận đấu: gom mọi thứ "không nhìn thấy" vào một chỗ.
            var ctrl = new GameObject("MatchController");
            var audio = ctrl.AddComponent<AudioDirector>();
            var swipe = ctrl.AddComponent<TouchSwipeReceiver>();
            var scoreboard = ctrl.AddComponent<ScoreboardUI>();
            var save = ctrl.AddComponent<MatchSaveLifecycle>();
            ctrl.AddComponent<DebugHotkeys>();

            var cues = ctrl.AddComponent<KickerBoneCueSource>();
            cues.ballPosition = new float3(0f, BallR, 0f);
            cues.runUpDuration = 1.05f;

            AimTrajectoryView aim = BuildAimTrajectory();

            var loop = ctrl.AddComponent<MatchGameLoop>();
            var so = new SerializedObject(loop);
            Set(so, "ballTransform", ball.transform);
            Set(so, "ballTrail", trail);
            Set(so, "goalNet", net);
            Set(so, "goalkeeper", keeper);
            Set(so, "kickerModel", kickerModel);
            Set(so, "kickerGreybox", kickerGreybox);
            Set(so, "cueSource", cues);
            Set(so, "aimTrajectory", aim);
            Set(so, "swipeReceiver", swipe);
            Set(so, "scoreboard", scoreboard);
            Set(so, "cameraRig", rig);
            Set(so, "saveLifecycle", save);
            Set(so, "audioDirector", audio);
            Set(so, "easyProfile", LoadAsset<KeeperProfile>($"{SettingsDir}/KeeperProfile-Easy.asset"));
            Set(so, "mediumProfile", LoadAsset<KeeperProfile>($"{SettingsDir}/KeeperProfile-Medium.asset"));
            Set(so, "hardProfile", LoadAsset<KeeperProfile>($"{SettingsDir}/KeeperProfile-Hard.asset"));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Đường bay dự kiến hiện ra sau khi người chơi vuốt xong (yêu cầu 2026-08-28).
        ///
        /// Hai LineRenderer chứ không phải một: đường bay là polyline mở, còn vòng tròn điểm
        /// chạm là đường khép kín ở mặt phẳng khung thành. Nhét chung một renderer thì phải
        /// nối chúng bằng một đoạn thẳng giả từ điểm cuối quỹ đạo về tâm vòng tròn — đúng
        /// chỗ mắt người chơi đang nhìn nhất.
        ///
        /// Không đổ bóng, không nhận bóng: đây là đồ hoạ chỉ dẫn, không phải vật thể trong sân.
        /// </summary>
        static AimTrajectoryView BuildAimTrajectory()
        {
            var go = new GameObject("AimTrajectory");

            var path = MakeAimLine(go, "Path", new Color(1f, 0.94f, 0.30f, 0.90f), 0.075f, 0.045f);
            var ring = MakeAimLine(go, "ImpactRing", new Color(1f, 0.45f, 0.20f, 0.95f), 0.05f, 0.05f);
            ring.loop = true;

            var view = go.AddComponent<AimTrajectoryView>();
            var so = new SerializedObject(view);
            Set(so, "path", path);
            Set(so, "impactRing", ring);
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        static LineRenderer MakeAimLine(GameObject parent, string name, Color color, float startW, float endW)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 0;
            lr.startWidth = startW;
            lr.endWidth = endW;
            lr.numCapVertices = 2;
            lr.alignment = LineAlignment.View;   // luôn quay mặt về camera, không bị mỏng đi khi nhìn nghiêng
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sharedMaterial = MakeMaterial(color, lit: false);
            lr.startColor = color;
            lr.endColor = color;
            lr.enabled = false;
            return lr;
        }

        static void Set(SerializedObject so, string field, Object value)
        {
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning($"[MatchSceneGenerator] Không tìm thấy trường '{field}' — tham chiếu này sẽ trống.");
                return;
            }
            prop.objectReferenceValue = value;
        }

        static T LoadAsset<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) Debug.LogWarning($"[MatchSceneGenerator] Thiếu asset {path}.");
            return asset;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Tiện ích dựng hình
        // ═══════════════════════════════════════════════════════════════════════

        static GameObject Primitive(PrimitiveType type, string name, Transform parent)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, false);

            // Không dùng collider của Unity ở bất cứ đâu: va chạm bóng là hình học giải tích,
            // để collider nằm đó chỉ tốn broadphase và mời gọi người sau viết vật lý thứ hai.
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            return go;
        }

        static void Paint(GameObject go, Color color, bool lit)
        {
            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null) return;
            mr.sharedMaterial = MakeMaterial(color, lit);
        }

        /// <summary>
        /// Gán một vật liệu ASSET (có texture) thay cho màu trơn dựng lúc chạy. Lùi về màu
        /// cỏ trơn nếu chưa dựng được vật liệu, để bộ sinh scene không gãy giữa chừng.
        /// </summary>
        static void PaintAsset(GameObject go, string materialPath)
        {
            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null) return;

            var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (mat == null)
            {
                Debug.LogWarning($"[MatchSceneGenerator] Thiếu vật liệu {materialPath} — " +
                                 "tô màu trơn tạm. Chạy Eleven > Art > Dựng Vật Liệu Đạo Cụ.");
                mr.sharedMaterial = MakeMaterial(new Color(0.14f, 0.34f, 0.16f), lit: true);
                return;
            }

            mr.sharedMaterial = mat;
        }

        static Material MakeMaterial(Color color, bool lit)
        {
            Shader shader = null;
            if (lit) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            var mat = new Material(shader) { name = $"GB_{ColorUtility.ToHtmlStringRGB(color)}" };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.18f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            return mat;
        }
    }
}
