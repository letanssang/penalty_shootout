using System;
using UnityEngine;
using Eleven.Ball;
using Eleven.Keeper;
using Eleven.Shooter;
using Eleven.Presentation.Net;

namespace Eleven.UI
{
    /// <summary>
    /// Component tự động khởi tạo toàn bộ không gian thi đấu (Sân, Khung thành, Quả bóng, Thủ môn, Camera, UI)
    /// khi Scene bắt đầu chạy, đảm bảo game hoạt động ngay lập tức 100% cả trong Editor lẫn trên bản build di động.
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
                return; // Đã có GameLoop trong Scene
            }

            var rootGo = new GameObject("Eleven.MatchController");
            var bootstrap = rootGo.AddComponent<MatchSceneBootstrap>();
            bootstrap.SetupMatchEnvironment();
        }

        private void Awake()
        {
            if (autoSetup && FindFirstObjectByType<MatchGameLoop>() == null)
            {
                SetupMatchEnvironment();
            }
        }

        public void SetupMatchEnvironment()
        {
            // 1. Ánh sáng mặt trời / Giàn đèn chính
            var existingLight = FindFirstObjectByType<Light>();
            if (existingLight == null)
            {
                var lightGo = new GameObject("Directional Light");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.96f, 0.90f);
                light.intensity = 1.4f;
                light.shadows = LightShadows.Soft;
                lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            }

            // 2. Mặt sân cỏ 12m
            var pitchGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            pitchGo.name = "StadiumPitch_12m";
            pitchGo.transform.position = new Vector3(0f, 0f, 6.0f);
            pitchGo.transform.localScale = new Vector3(3.0f, 1f, 3.0f); // 30m x 30m
            ApplyMaterial(pitchGo, new Color(0.12f, 0.42f, 0.18f), 0.2f); // Xanh cỏ mượt

            // Vạch vôi khung thành trắng (Goal Line)
            var goalLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
            goalLine.name = "GoalLine_White";
            goalLine.transform.position = new Vector3(0f, 0.005f, 11.0f);
            goalLine.transform.localScale = new Vector3(9.0f, 0.01f, 0.12f);
            ApplyMaterial(goalLine, Color.white, 0.8f);

            // Chấm phạt đền 11m (Penalty Spot)
            var spot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            spot.name = "PenaltySpot_White";
            spot.transform.position = new Vector3(0f, 0.005f, 0f);
            spot.transform.localScale = new Vector3(0.25f, 0.01f, 0.25f);
            ApplyMaterial(spot, Color.white, 0.8f);

            // 3. Khung thành chuẩn FIFA (Rộng 7.32m, Cao 2.44m tại Z = 11.0m)
            var goalFrameGo = new GameObject("GoalFrame_FIFA");
            goalFrameGo.transform.position = new Vector3(0f, 0f, 11.0f);

            // Cột dọc trái
            var leftPost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leftPost.name = "LeftPost";
            leftPost.transform.parent = goalFrameGo.transform;
            leftPost.transform.localPosition = new Vector3(-3.66f, 1.22f, 0f);
            leftPost.transform.localScale = new Vector3(0.12f, 1.22f, 0.12f);
            ApplyMaterial(leftPost, Color.white, 0.9f);

            // Cột dọc phải
            var rightPost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rightPost.name = "RightPost";
            rightPost.transform.parent = goalFrameGo.transform;
            rightPost.transform.localPosition = new Vector3(3.66f, 1.22f, 0f);
            rightPost.transform.localScale = new Vector3(0.12f, 1.22f, 0.12f);
            ApplyMaterial(rightPost, Color.white, 0.9f);

            // Xà ngang
            var crossbar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            crossbar.name = "Crossbar";
            crossbar.transform.parent = goalFrameGo.transform;
            crossbar.transform.localPosition = new Vector3(0f, 2.44f, 0f);
            crossbar.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            crossbar.transform.localScale = new Vector3(0.12f, 3.66f, 0.12f);
            ApplyMaterial(crossbar, Color.white, 0.9f);

            // Lưới Verlet 3D
            var netGo = new GameObject("GoalNet_Verlet3D");
            netGo.transform.parent = goalFrameGo.transform;
            netGo.transform.localPosition = Vector3.zero;
            var netView = netGo.AddComponent<GoalNetView>();

            // 4. Quả bóng tại chấm 11m (0, 0.11, 0)
            var ballGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballGo.name = "MatchBall_Eleven";
            ballGo.transform.position = new Vector3(0f, 0.11f, 0f);
            ballGo.transform.localScale = Vector3.one * 0.22f; // r = 11cm
            ApplyMaterial(ballGo, new Color(0.98f, 0.98f, 0.98f), 0.7f);

            var trail = ballGo.AddComponent<TrailRenderer>();
            trail.time = 0.40f;
            trail.startWidth = 0.14f;
            trail.endWidth = 0.02f;
            trail.emitting = false;
            trail.material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            trail.startColor = new Color(1f, 1f, 1f, 0.85f);
            trail.endColor = new Color(0.2f, 0.8f, 1f, 0f);

            // 5. Nhân vật Thủ môn AI trên vạch vôi
            var keeperGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            keeperGo.name = "Goalkeeper_AI";
            keeperGo.transform.position = new Vector3(0f, 0.95f, 11.0f);
            keeperGo.transform.localScale = new Vector3(0.55f, 0.95f, 0.45f);
            ApplyMaterial(keeperGo, new Color(1.0f, 0.75f, 0.10f), 0.5f); // Áo thủ môn màu vàng nổi bật
            var keeperView = keeperGo.AddComponent<GoalkeeperView>();

            // 6. Camera chính
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.07f, 0.11f, 1.0f); // Bầu trời đêm sân vận động
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.transform.position = new Vector3(0f, 1.8f, -4.5f);
            cam.transform.rotation = Quaternion.Euler(10f, 0f, 0f);

            // 7. Gắn Controller & MatchGameLoop
            var swipeReceiver = gameObject.GetComponent<TouchSwipeReceiver>() ?? gameObject.AddComponent<TouchSwipeReceiver>();
            var scoreboard = gameObject.GetComponent<ScoreboardUI>() ?? gameObject.AddComponent<ScoreboardUI>();
            var matchLoop = gameObject.GetComponent<MatchGameLoop>() ?? gameObject.AddComponent<MatchGameLoop>();

            // Thiết lập tham chiếu động
            var so = new ReflectionBinder(matchLoop);
            so.Set("ballTransform", ballGo.transform);
            so.Set("ballTrail", trail);
            so.Set("goalNet", netView);
            so.Set("goalkeeper", keeperView);
            so.Set("swipeReceiver", swipeReceiver);
            so.Set("scoreboard", scoreboard);
            so.Set("mainCamera", cam);

            Debug.Log("[MatchSceneBootstrap] ĐÃ KHỞI TẠO XONG KHÔNG GIAN THI ĐẤU PENALTY SHOOTOUT 11 METRES!");
        }

        private static void ApplyMaterial(GameObject go, Color color, float smoothness = 0.5f)
        {
            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null) return;

            var shader = Shader.Find("Universal Render Pipeline/Lit")
                      ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                      ?? Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Standard")
                      ?? Shader.Find("Diffuse");

            var mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);

            mr.material = mat;
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
