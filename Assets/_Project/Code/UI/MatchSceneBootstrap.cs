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
                light.intensity = 1.35f;
                light.shadows = LightShadows.Soft;
                lightGo.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            }

            // 2. Mặt sân cỏ 12m
            var pitchGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            pitchGo.name = "StadiumPitch_12m";
            pitchGo.transform.position = new Vector3(0f, 0f, 5.5f);
            pitchGo.transform.localScale = new Vector3(2.5f, 1f, 2.5f); // 25m x 25m
            var pitchRenderer = pitchGo.GetComponent<MeshRenderer>();
            if (pitchRenderer != null)
            {
                pitchRenderer.material.color = new Color(0.18f, 0.48f, 0.22f); // Xanh cỏ mượt
            }

            // 3. Khung thành chuẩn FIFA (Rộng 7.32m, Cao 2.44m tại Z = 11.0m)
            var goalFrameGo = new GameObject("GoalFrame_FIFA");
            goalFrameGo.transform.position = new Vector3(0f, 0f, 11.0f);

            // Cột dọc trái
            var leftPost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leftPost.name = "LeftPost";
            leftPost.transform.parent = goalFrameGo.transform;
            leftPost.transform.localPosition = new Vector3(-3.66f, 1.22f, 0f);
            leftPost.transform.localScale = new Vector3(0.12f, 1.22f, 0.12f);
            SetWhiteColor(leftPost);

            // Cột dọc phải
            var rightPost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rightPost.name = "RightPost";
            rightPost.transform.parent = goalFrameGo.transform;
            rightPost.transform.localPosition = new Vector3(3.66f, 1.22f, 0f);
            rightPost.transform.localScale = new Vector3(0.12f, 1.22f, 0.12f);
            SetWhiteColor(rightPost);

            // Xà ngang
            var crossbar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            crossbar.name = "Crossbar";
            crossbar.transform.parent = goalFrameGo.transform;
            crossbar.transform.localPosition = new Vector3(0f, 2.44f, 0f);
            crossbar.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            crossbar.transform.localScale = new Vector3(0.12f, 3.66f, 0.12f);
            SetWhiteColor(crossbar);

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
            SetWhiteColor(ballGo);

            var trail = ballGo.AddComponent<TrailRenderer>();
            trail.time = 0.40f;
            trail.startWidth = 0.14f;
            trail.endWidth = 0.02f;
            trail.emitting = false;
            trail.startColor = new Color(1f, 1f, 1f, 0.8f);
            trail.endColor = new Color(0.2f, 0.8f, 1f, 0f);

            // 5. Nhân vật Thủ môn AI trên vạch vôi
            var keeperGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            keeperGo.name = "Goalkeeper_AI";
            keeperGo.transform.position = new Vector3(0f, 0.95f, 11.0f);
            keeperGo.transform.localScale = new Vector3(0.55f, 0.95f, 0.45f);
            var keeperRenderer = keeperGo.GetComponent<MeshRenderer>();
            if (keeperRenderer != null)
            {
                keeperRenderer.material.color = new Color(0.95f, 0.75f, 0.15f); // Áo thủ môn màu vàng nổi bật
            }
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

        private void SetWhiteColor(GameObject go)
        {
            var r = go.GetComponent<MeshRenderer>();
            if (r != null)
            {
                r.material.color = Color.white;
            }
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
