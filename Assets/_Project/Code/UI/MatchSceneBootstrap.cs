using System;
using UnityEngine;
using Eleven.Ball;
using Eleven.Keeper;
using Eleven.Shooter;
using Eleven.Presentation.Net;

namespace Eleven.UI
{
    /// <summary>
    /// Component tự động khởi tạo toàn bộ không gian thi đấu Sân Vận Động chuẩn eFootball / EA Sports FC:
    /// - Góc quay camera telephoto 26.5° tạo chiều sâu truyền hình thể thao chân thực
    /// - Cầu thủ Ronaldo áo đỏ số 7 đứng chống nạnh chuẩn bị chạy đà
    /// - Khung thành FIFA chiếm trọn nửa trên màn hình
    /// - Thủ môn AI áo vàng dang tay thủ trên vạch vôi
    /// - Khán đài CĐV Bồ Đào Nha & Bảng LED Signal Iduna Park rực rỡ
    /// </summary>
    public sealed class MatchSceneBootstrap : MonoBehaviour
    {
        [Header("Tự động dựng Scene khi Start")]
        [SerializeField] private bool autoSetup = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitializeMatch()
        {
            if (FindFirstObjectByType<MatchGameLoop>() != null)
            {
                return;
            }

            var rootGo = new GameObject("Eleven.MatchController");
            var bootstrap = rootGo.AddComponent<MatchSceneBootstrap>();
            bootstrap.SetupMatchEnvironment();
        }

        private void Awake()
        {
            Screen.orientation = ScreenOrientation.LandscapeLeft;

            if (autoSetup && FindFirstObjectByType<MatchGameLoop>() == null)
            {
                SetupMatchEnvironment();
            }
        }

        public void SetupMatchEnvironment()
        {
            Screen.orientation = ScreenOrientation.LandscapeLeft;

            // 1. Ánh sáng môi trường
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.70f, 0.78f, 0.85f, 1.0f);

            var existingLight = FindFirstObjectByType<Light>();
            if (existingLight == null)
            {
                var lightGo = new GameObject("Stadium_MainDirectionalLight");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.98f, 0.94f);
                light.intensity = 1.4f;
                light.shadows = LightShadows.Soft;
                lightGo.transform.rotation = Quaternion.Euler(42f, -25f, 0f);
            }

            var stadiumParent = new GameObject("Stadium_Environment");

            // 2. Mặt sân cỏ xanh tươi sáng chuẩn FIFA (Pitch)
            var pitchGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            pitchGo.name = "StadiumPitch_Grass";
            pitchGo.transform.parent = stadiumParent.transform;
            pitchGo.transform.position = new Vector3(0f, -0.01f, 6.0f);
            pitchGo.transform.localScale = new Vector3(5.0f, 1.0f, 5.0f);

            Texture2D grassTex = GenerateGrassTexture();
            ApplyTexturedMaterial(pitchGo, grassTex, new Color(0.25f, 0.68f, 0.28f));

            // 3. Vạch kẻ sân bóng đá màu trắng chuẩn FIFA (Line Markings)
            var markingsParent = new GameObject("Pitch_Markings");
            markingsParent.transform.parent = stadiumParent.transform;

            // Vạch vôi khung thành (Goal line tại Z = 11.0m)
            CreateLine(markingsParent, "GoalLine", new Vector3(0f, 0.008f, 11.0f), new Vector3(16.0f, 0.015f, 0.12f));

            // Vạch 5m50 (Goal Box)
            CreateLine(markingsParent, "GoalBox_Front", new Vector3(0f, 0.008f, 11.0f - 5.5f), new Vector3(18.32f, 0.015f, 0.12f));
            CreateLine(markingsParent, "GoalBox_Left", new Vector3(-9.16f, 0.008f, 11.0f - 2.75f), new Vector3(0.12f, 0.015f, 5.5f));
            CreateLine(markingsParent, "GoalBox_Right", new Vector3(9.16f, 0.008f, 11.0f - 2.75f), new Vector3(0.12f, 0.015f, 5.5f));

            // Vạch 16m50 (Penalty Box line chạy ngang dưới chân cầu thủ sút tại Z = -3.2m)
            CreateLine(markingsParent, "PenaltyBox_Front", new Vector3(0f, 0.008f, -3.2f), new Vector3(36.0f, 0.015f, 0.12f));

            // Chấm phạt đền 11m (Penalty Spot đĩa tròn trắng)
            var spot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            spot.name = "PenaltySpot_White";
            spot.transform.parent = markingsParent.transform;
            spot.transform.position = new Vector3(0f, 0.01f, 0f);
            spot.transform.localScale = new Vector3(0.24f, 0.01f, 0.24f);
            ApplySolidMaterial(spot, Color.white);

            // 4. Khung thành chuẩn FIFA (Rộng 7.32m, Cao 2.44m tại Z = 11.0m)
            var goalFrameGo = new GameObject("GoalFrame_FIFA");
            goalFrameGo.transform.parent = stadiumParent.transform;
            goalFrameGo.transform.position = new Vector3(0f, 0f, 11.0f);

            // Cột dọc trái
            var leftPost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leftPost.name = "LeftPost";
            leftPost.transform.parent = goalFrameGo.transform;
            leftPost.transform.localPosition = new Vector3(-3.66f, 1.22f, 0f);
            leftPost.transform.localScale = new Vector3(0.12f, 1.22f, 0.12f);
            ApplySolidMaterial(leftPost, Color.white);

            // Cột dọc phải
            var rightPost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rightPost.name = "RightPost";
            rightPost.transform.parent = goalFrameGo.transform;
            rightPost.transform.localPosition = new Vector3(3.66f, 1.22f, 0f);
            rightPost.transform.localScale = new Vector3(0.12f, 1.22f, 0.12f);
            ApplySolidMaterial(rightPost, Color.white);

            // Xà ngang
            var crossbar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            crossbar.name = "Crossbar";
            crossbar.transform.parent = goalFrameGo.transform;
            crossbar.transform.localPosition = new Vector3(0f, 2.44f, 0f);
            crossbar.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            crossbar.transform.localScale = new Vector3(0.12f, 3.66f, 0.12f);
            ApplySolidMaterial(crossbar, Color.white);

            // Khung sắt đỡ hậu phía sau
            var leftBackPost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leftBackPost.name = "LeftBackSupport";
            leftBackPost.transform.parent = goalFrameGo.transform;
            leftBackPost.transform.localPosition = new Vector3(-3.66f, 1.22f, 1.5f);
            leftBackPost.transform.localScale = new Vector3(0.08f, 1.22f, 0.08f);
            ApplySolidMaterial(leftBackPost, new Color(0.85f, 0.90f, 0.95f));

            var rightBackPost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rightBackPost.name = "RightBackSupport";
            rightBackPost.transform.parent = goalFrameGo.transform;
            rightBackPost.transform.localPosition = new Vector3(3.66f, 1.22f, 1.5f);
            rightBackPost.transform.localScale = new Vector3(0.08f, 1.22f, 0.08f);
            ApplySolidMaterial(rightBackPost, new Color(0.85f, 0.90f, 0.95f));

            // Lưới Verlet 3D
            var netGo = new GameObject("GoalNet_Verlet3D");
            netGo.transform.parent = goalFrameGo.transform;
            netGo.transform.localPosition = Vector3.zero;
            var netView = netGo.AddComponent<GoalNetView>();

            // 5. Bảng LED quảng cáo điện tử (BVB SIGNAL IDUNA PARK) sau gôn
            var adBoardsGo = new GameObject("Stadium_AdBoards");
            adBoardsGo.transform.parent = stadiumParent.transform;
            adBoardsGo.transform.position = new Vector3(0f, 0.55f, 13.6f);

            var adMain = GameObject.CreatePrimitive(PrimitiveType.Cube);
            adMain.name = "AdBoard_LED_Main";
            adMain.transform.parent = adBoardsGo.transform;
            adMain.transform.localPosition = Vector3.zero;
            adMain.transform.localScale = new Vector3(40.0f, 1.1f, 0.3f);

            Texture2D ledTex = GenerateLedAdTexture();
            ApplyTexturedMaterial(adMain, ledTex, Color.white);

            // 6. Khán đài khổng lồ đầy ắp CĐV Bồ Đào Nha
            var grandstandGo = new GameObject("Stadium_Grandstand");
            grandstandGo.transform.parent = stadiumParent.transform;
            grandstandGo.transform.position = new Vector3(0f, 4.5f, 19.0f);

            var crowdBackdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
            crowdBackdrop.name = "Crowd_Backdrop_Quad";
            crowdBackdrop.transform.parent = grandstandGo.transform;
            crowdBackdrop.transform.localPosition = new Vector3(0f, 3.5f, 0f);
            crowdBackdrop.transform.localScale = new Vector3(50.0f, 15.0f, 1.0f);

            Texture2D crowdTex = GenerateCrowdTexture();
            ApplyTexturedMaterial(crowdBackdrop, crowdTex, Color.white);

            // 7. Cầu thủ sút bóng (Ronaldo áo đỏ số 7 đứng bên trái tiền cảnh)
            var shooterGo = new GameObject("Player_Shooter_Ronaldo");
            shooterGo.transform.parent = stadiumParent.transform;
            shooterGo.transform.position = new Vector3(-2.25f, 0.85f, -1.8f);
            shooterGo.transform.rotation = Quaternion.Euler(0f, 18f, 0f);
            shooterGo.transform.localScale = Vector3.one * 0.88f;

            // Thân người (Áo đỏ Bồ Đào Nha)
            var shooterTorso = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            shooterTorso.name = "Shooter_Torso_Red";
            shooterTorso.transform.parent = shooterGo.transform;
            shooterTorso.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            shooterTorso.transform.localScale = new Vector3(0.46f, 0.52f, 0.30f);
            ApplySolidMaterial(shooterTorso, new Color(0.85f, 0.12f, 0.12f));

            // Quần (Xanh lá đậm)
            var shooterShorts = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shooterShorts.name = "Shooter_Shorts_Green";
            shooterShorts.transform.parent = shooterGo.transform;
            shooterShorts.transform.localPosition = new Vector3(0f, -0.22f, 0f);
            shooterShorts.transform.localScale = new Vector3(0.42f, 0.30f, 0.28f);
            ApplySolidMaterial(shooterShorts, new Color(0.10f, 0.38f, 0.20f));

            // Chân & Giày hồng/đỏ
            var leftLeg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leftLeg.name = "Shooter_LeftLeg";
            leftLeg.transform.parent = shooterGo.transform;
            leftLeg.transform.localPosition = new Vector3(-0.12f, -0.55f, 0f);
            leftLeg.transform.localScale = new Vector3(0.12f, 0.32f, 0.12f);
            ApplySolidMaterial(leftLeg, new Color(0.85f, 0.12f, 0.12f));

            var rightLeg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rightLeg.name = "Shooter_RightLeg";
            rightLeg.transform.parent = shooterGo.transform;
            rightLeg.transform.localPosition = new Vector3(0.12f, -0.55f, 0f);
            rightLeg.transform.localScale = new Vector3(0.12f, 0.32f, 0.12f);
            ApplySolidMaterial(rightLeg, new Color(0.85f, 0.12f, 0.12f));

            // Đầu
            var shooterHead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shooterHead.name = "Shooter_Head";
            shooterHead.transform.parent = shooterGo.transform;
            shooterHead.transform.localPosition = new Vector3(0f, 0.60f, 0f);
            shooterHead.transform.localScale = new Vector3(0.26f, 0.28f, 0.26f);
            ApplySolidMaterial(shooterHead, new Color(0.92f, 0.74f, 0.60f));

            // 8. Trọng tài biên áo xanh nõn chuối bên phải
            var refGo = new GameObject("AssistantReferee");
            refGo.transform.parent = stadiumParent.transform;
            refGo.transform.position = new Vector3(9.5f, 0.85f, 11.5f);
            var refTorso = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            refTorso.transform.parent = refGo.transform;
            refTorso.transform.localScale = new Vector3(0.42f, 0.55f, 0.28f);
            ApplySolidMaterial(refTorso, new Color(0.20f, 0.95f, 0.45f));

            // 9. Quả bóng Penalty tại chấm 11m (0, 0.11, 0)
            var ballGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballGo.name = "MatchBall_Eleven";
            ballGo.transform.position = new Vector3(0f, 0.11f, 0f);
            ballGo.transform.localScale = Vector3.one * 0.22f; // r = 11cm
            ApplySolidMaterial(ballGo, new Color(0.98f, 0.98f, 0.98f));

            var ballPattern = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballPattern.name = "Ball_Pattern_Pink";
            ballPattern.transform.parent = ballGo.transform;
            ballPattern.transform.localPosition = new Vector3(0f, 0.04f, 0.04f);
            ballPattern.transform.localScale = Vector3.one * 0.5f;
            ApplySolidMaterial(ballPattern, new Color(0.85f, 0.20f, 0.65f));

            var trail = ballGo.AddComponent<TrailRenderer>();
            trail.time = 0.35f;
            trail.startWidth = 0.15f;
            trail.endWidth = 0.02f;
            trail.emitting = false;
            trail.material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            trail.startColor = new Color(1f, 1f, 1f, 0.9f);
            trail.endColor = new Color(0.1f, 0.8f, 1.0f, 0.0f);

            // 10. Nhân vật Thủ môn AI áo vàng dang tay thủ giữa gôn
            var keeperGo = new GameObject("Goalkeeper_AI");
            keeperGo.transform.position = new Vector3(0f, 0.95f, 11.0f);

            // Thân người (Áo Vàng tươi)
            var torso = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            torso.name = "Torso_Jersey_Yellow";
            torso.transform.parent = keeperGo.transform;
            torso.transform.localPosition = Vector3.zero;
            torso.transform.localScale = new Vector3(0.55f, 0.65f, 0.35f);
            ApplySolidMaterial(torso, new Color(0.98f, 0.82f, 0.10f));

            // Quần thủ môn (Vàng)
            var shorts = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shorts.name = "Shorts_Yellow";
            shorts.transform.parent = keeperGo.transform;
            shorts.transform.localPosition = new Vector3(0f, -0.42f, 0f);
            shorts.transform.localScale = new Vector3(0.50f, 0.35f, 0.32f);
            ApplySolidMaterial(shorts, new Color(0.98f, 0.82f, 0.10f));

            // Đầu
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.parent = keeperGo.transform;
            head.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            head.transform.localScale = new Vector3(0.32f, 0.35f, 0.32f);
            ApplySolidMaterial(head, new Color(0.92f, 0.74f, 0.60f));

            // Đôi tay dang rộng thế thủ (Goalie Ready Stance)
            var leftArm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leftArm.name = "LeftArm";
            leftArm.transform.parent = keeperGo.transform;
            leftArm.transform.localPosition = new Vector3(-0.50f, 0.15f, 0f);
            leftArm.transform.localRotation = Quaternion.Euler(0f, 0f, 40f);
            leftArm.transform.localScale = new Vector3(0.12f, 0.40f, 0.12f);
            ApplySolidMaterial(leftArm, new Color(0.98f, 0.82f, 0.10f));

            var rightArm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rightArm.name = "RightArm";
            rightArm.transform.parent = keeperGo.transform;
            rightArm.transform.localPosition = new Vector3(0.50f, 0.15f, 0f);
            rightArm.transform.localRotation = Quaternion.Euler(0f, 0f, -40f);
            rightArm.transform.localScale = new Vector3(0.12f, 0.40f, 0.12f);
            ApplySolidMaterial(rightArm, new Color(0.98f, 0.82f, 0.10f));

            // Đôi găng tay trắng
            var leftGlove = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leftGlove.name = "LeftGlove_White";
            leftGlove.transform.parent = keeperGo.transform;
            leftGlove.transform.localPosition = new Vector3(-0.85f, 0.25f, 0f);
            leftGlove.transform.localScale = new Vector3(0.24f, 0.24f, 0.24f);
            ApplySolidMaterial(leftGlove, Color.white);

            var rightGlove = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rightGlove.name = "RightGlove_White";
            rightGlove.transform.parent = keeperGo.transform;
            rightGlove.transform.localPosition = new Vector3(0.85f, 0.25f, 0f);
            rightGlove.transform.localScale = new Vector3(0.24f, 0.24f, 0.24f);
            ApplySolidMaterial(rightGlove, Color.white);

            var keeperView = keeperGo.AddComponent<GoalkeeperView>();

            // 11. Camera chính góc nhìn chuẩn Broadcast Telephoto (FOV 26.5°, Z = -9.2m)
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 1.0f);
            cam.fieldOfView = 26.5f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 150f;
            cam.transform.position = new Vector3(0f, 1.82f, -9.0f);
            cam.transform.rotation = Quaternion.Euler(6.0f, 0f, 0f);

            // 12. Gắn Controller & MatchGameLoop
            var swipeReceiver = gameObject.GetComponent<TouchSwipeReceiver>() ?? gameObject.AddComponent<TouchSwipeReceiver>();
            var scoreboard = gameObject.GetComponent<ScoreboardUI>() ?? gameObject.AddComponent<ScoreboardUI>();
            var matchLoop = gameObject.GetComponent<MatchGameLoop>() ?? gameObject.AddComponent<MatchGameLoop>();

            // Gán tham chiếu
            var so = new ReflectionBinder(matchLoop);
            so.Set("ballTransform", ballGo.transform);
            so.Set("ballTrail", trail);
            so.Set("goalNet", netView);
            so.Set("goalkeeper", keeperView);
            so.Set("swipeReceiver", swipeReceiver);
            so.Set("scoreboard", scoreboard);
            so.Set("mainCamera", cam);

            Debug.Log("[MatchSceneBootstrap] ĐÃ HOÀN THIỆN SÂN VẬN ĐỘNG CHUẨN eFOOTBALL 1:1!");
        }

        private static void CreateLine(GameObject parent, string name, Vector3 pos, Vector3 scale)
        {
            var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = name;
            line.transform.parent = parent.transform;
            line.transform.position = pos;
            line.transform.localScale = scale;
            ApplySolidMaterial(line, Color.white);
        }

        private static void ApplySolidMaterial(GameObject go, Color color)
        {
            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null) return;

            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Unlit/Color")
                      ?? Shader.Find("Sprites/Default");

            var mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

            mr.material = mat;
        }

        private static void ApplyTexturedMaterial(GameObject go, Texture2D texture, Color tint)
        {
            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null) return;

            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Unlit/Texture")
                      ?? Shader.Find("Sprites/Default");

            var mat = new Material(shader);
            mat.mainTexture = texture;
            mat.color = tint;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", texture);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);

            mr.material = mat;
        }

        private static Texture2D GenerateGrassTexture()
        {
            int w = 256, h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            Color[] cols = new Color[w * h];

            Color c1 = new Color(0.24f, 0.65f, 0.28f);
            Color c2 = new Color(0.28f, 0.72f, 0.32f);

            for (int y = 0; y < h; y++)
            {
                bool stripe = (y / 32) % 2 == 0;
                Color baseC = stripe ? c1 : c2;

                for (int x = 0; x < w; x++)
                {
                    float noise = Mathf.PerlinNoise(x * 0.1f, y * 0.1f) * 0.1f;
                    cols[y * w + x] = baseC + new Color(noise, noise * 1.2f, noise * 0.5f);
                }
            }

            tex.SetPixels(cols);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.Apply();
            return tex;
        }

        private static Texture2D GenerateLedAdTexture()
        {
            int w = 512, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            Color[] cols = new Color[w * h];

            Color black = new Color(0.04f, 0.05f, 0.08f);
            Color yellow = new Color(0.98f, 0.85f, 0.05f);
            Color cyan = new Color(0.10f, 0.85f, 1.0f);
            Color red = new Color(0.90f, 0.15f, 0.15f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (y < 4 || y > h - 5)
                    {
                        cols[y * w + x] = yellow;
                    }
                    else
                    {
                        int block = (x / 64) % 4;
                        if ((x % 32 < 20) && (y > 15 && y < 50))
                        {
                            cols[y * w + x] = (block == 0 || block == 2) ? yellow : (block == 1 ? cyan : red);
                        }
                        else
                        {
                            cols[y * w + x] = black;
                        }
                    }
                }
            }

            tex.SetPixels(cols);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.Apply();
            return tex;
        }

        private static Texture2D GenerateCrowdTexture()
        {
            int w = 512, h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            Color[] cols = new Color[w * h];

            Color red = new Color(0.85f, 0.12f, 0.12f);
            Color green = new Color(0.12f, 0.45f, 0.20f);
            Color yellow = new Color(0.95f, 0.80f, 0.10f);
            Color dark = new Color(0.10f, 0.12f, 0.18f);
            Color white = Color.white;

            var rng = new System.Random(12345);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (y % 16 < 2)
                    {
                        cols[y * w + x] = new Color(0.2f, 0.22f, 0.28f);
                        continue;
                    }

                    if (x > 180 && x < 330 && y > 140 && y < 240)
                    {
                        cols[y * w + x] = (x < 240) ? green : red;
                        if (Mathf.Abs(x - 240) < 12 && Mathf.Abs(y - 190) < 12)
                        {
                            cols[y * w + x] = yellow;
                        }
                        continue;
                    }

                    int r = rng.Next(100);
                    if (r < 55) cols[y * w + x] = red;
                    else if (r < 75) cols[y * w + x] = green;
                    else if (r < 88) cols[y * w + x] = yellow;
                    else if (r < 95) cols[y * w + x] = white;
                    else cols[y * w + x] = dark;
                }
            }

            tex.SetPixels(cols);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            return tex;
        }

        private class ReflectionBinder
        {
            private object target;
            private Type type;

            public ReflectionBinder(object target)
            {
                this.target = target;
                this.type = target.GetType();
            }

            public void Set(string fieldName, object value)
            {
                var field = type.GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                if (field != null)
                {
                    field.SetValue(target, value);
                }
            }
        }
    }
}
