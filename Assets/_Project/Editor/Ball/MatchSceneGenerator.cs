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
using Eleven.Presentation.Kicker;
using Eleven.Presentation.Audio;
using Eleven.Presentation.Crowd;
using Eleven.Presentation.Diagnostics;
using Eleven.Presentation.Grass;
using Eleven.Presentation.Net;
using Eleven.Shooter;
using Eleven.UI;

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
            var pitch = Primitive(PrimitiveType.Plane, "PitchGround", parent);
            pitch.transform.position = new Vector3(0f, 0f, 6f);
            pitch.transform.localScale = new Vector3(4.2f, 1f, 4.2f);
            Paint(pitch, new Color(0.14f, 0.34f, 0.16f), lit: true);

            // Sọc cắt cỏ — thứ duy nhất khiến một mặt phẳng xanh trông ra sân bóng.
            var stripes = new GameObject("MowStripes").transform;
            stripes.SetParent(parent, false);
            for (int i = -6; i <= 6; i++)
            {
                var s = Primitive(PrimitiveType.Cube, $"Stripe_{i + 6}", stripes);
                s.transform.position = new Vector3(0f, 0.004f, 6f + i * 2.6f);
                s.transform.localScale = new Vector3(42f, 0.008f, 1.3f);
                Paint(s, new Color(0.17f, 0.40f, 0.19f), lit: true);
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

            // Khung hậu đỡ lưới (chỉ để mắt tin rằng cái lưới có chỗ bám).
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

        static GameObject BuildBall(out TrailRenderer trail)
        {
            var ball = Primitive(PrimitiveType.Sphere, "MatchBall", null);
            ball.transform.position = new Vector3(0f, BallR, 0f);
            ball.transform.localScale = Vector3.one * (BallR * 2f);
            Paint(ball, new Color(0.97f, 0.97f, 0.98f), lit: true);

            trail = ball.AddComponent<TrailRenderer>();
            trail.time = 0.45f;
            trail.startWidth = 0.15f;
            trail.endWidth = 0.01f;
            trail.emitting = false;
            trail.numCapVertices = 2;
            trail.sharedMaterial = MakeMaterial(Color.white, lit: false);
            trail.shadowCastingMode = ShadowCastingMode.Off;

            ball.AddComponent<BallDriver>();
            return ball;
        }

        static GoalkeeperView BuildKeeper()
        {
            var go = new GameObject("Goalkeeper");
            go.transform.position = new Vector3(0f, 0.95f, GoalZ);
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var torso = Primitive(PrimitiveType.Capsule, "Torso", go.transform);
            torso.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            torso.transform.localScale = new Vector3(0.52f, 0.62f, 0.34f);
            Paint(torso, new Color(0.98f, 0.80f, 0.08f), lit: true);

            var head = Primitive(PrimitiveType.Sphere, "Head", go.transform);
            head.transform.localPosition = new Vector3(0f, 0.78f, 0f);
            head.transform.localScale = Vector3.one * 0.22f;
            Paint(head, new Color(0.86f, 0.68f, 0.55f), lit: true);

            var legs = Primitive(PrimitiveType.Capsule, "Legs", go.transform);
            legs.transform.localPosition = new Vector3(0f, -0.55f, 0f);
            legs.transform.localScale = new Vector3(0.30f, 0.42f, 0.28f);
            Paint(legs, new Color(0.12f, 0.12f, 0.16f), lit: true);

            var glL = Primitive(PrimitiveType.Sphere, "LeftGlove", go.transform);
            glL.transform.position = new Vector3(-0.72f, 1.05f, GoalZ);
            glL.transform.localScale = Vector3.one * 0.20f;
            Paint(glL, new Color(0.95f, 0.98f, 1f), lit: true);

            var glR = Primitive(PrimitiveType.Sphere, "RightGlove", go.transform);
            glR.transform.position = new Vector3(0.72f, 1.05f, GoalZ);
            glR.transform.localScale = Vector3.one * 0.20f;
            Paint(glR, new Color(0.95f, 0.98f, 1f), lit: true);

            var view = go.AddComponent<GoalkeeperView>();
            var so = new SerializedObject(view);
            so.FindProperty("homePosition").vector3Value = new Vector3(0f, 0.95f, GoalZ);
            so.FindProperty("leftGlove").objectReferenceValue = glL.transform;
            so.FindProperty("rightGlove").objectReferenceValue = glR.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        const string KickerModelPath = "Assets/_Project/Art/Characters/XBot.fbx";
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

            var loop = ctrl.AddComponent<MatchGameLoop>();
            var so = new SerializedObject(loop);
            Set(so, "ballTransform", ball.transform);
            Set(so, "ballTrail", trail);
            Set(so, "goalNet", net);
            Set(so, "goalkeeper", keeper);
            Set(so, "kickerModel", kickerModel);
            Set(so, "kickerGreybox", kickerGreybox);
            Set(so, "cueSource", cues);
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
