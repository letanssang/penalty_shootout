using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Eleven.Ball;
using Eleven.Keeper;
using Eleven.Shooter;
using Eleven.Match;
using Eleven.Presentation.Net;
using Eleven.UI;

namespace Eleven.Editor.SceneSetup
{
    /// <summary>
    /// Công cụ dựng Scene thi đấu chính Match.unity chuẩn xác, tối giản và thanh lịch.
    /// Tự động thiết lập: Khung thành FIFA, Lưới Verlet 3D, Quả bóng tại chấm 11m, Thủ môn, Camera và Bảng điểm HUD.
    /// </summary>
    public static class MatchSceneGenerator
    {
        [MenuItem("Eleven/Gameplay/Dựng Scene Thi Đấu (Match.unity)")]
        public static void GenerateMatchScene()
        {
            // 1. Tạo Scene mới
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 2. Ánh sáng môi trường & Directional Light
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.65f, 0.72f, 0.80f, 1.0f);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.98f, 0.92f);
            light.intensity = 1.35f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            var environmentParent = new GameObject("Environment");

            // 3. Mặt sân cỏ 20m x 20m (Pitch)
            var pitchGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            pitchGo.name = "StadiumPitch";
            pitchGo.transform.parent = environmentParent.transform;
            pitchGo.transform.position = new Vector3(0f, 0f, 5.5f);
            pitchGo.transform.localScale = new Vector3(2.5f, 1f, 2.5f);
            ApplyColor(pitchGo, new Color(0.24f, 0.58f, 0.28f));

            // Vạch vôi khung thành (Goal line tại Z = 11.0m)
            CreateLine(environmentParent, "GoalLine", new Vector3(0f, 0.005f, 11.0f), new Vector3(12.0f, 0.01f, 0.12f));

            // Vạch 5m50
            CreateLine(environmentParent, "GoalBox_Front", new Vector3(0f, 0.005f, 5.5f), new Vector3(18.32f, 0.01f, 0.12f));
            CreateLine(environmentParent, "GoalBox_Left", new Vector3(-9.16f, 0.005f, 8.25f), new Vector3(0.12f, 0.01f, 5.5f));
            CreateLine(environmentParent, "GoalBox_Right", new Vector3(9.16f, 0.005f, 8.25f), new Vector3(0.12f, 0.01f, 5.5f));

            // Chấm phạt đền 11m (Penalty Spot tròn trắng)
            var spot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            spot.name = "PenaltySpot";
            spot.transform.parent = environmentParent.transform;
            spot.transform.position = new Vector3(0f, 0.008f, 0f);
            spot.transform.localScale = new Vector3(0.22f, 0.005f, 0.22f);
            ApplyColor(spot, Color.white);

            // 4. Khung thành chuẩn FIFA (Width = 7.32m, Height = 2.44m tại Z = 11.0m)
            var goalFrameGo = new GameObject("GoalFrame_FIFA");
            goalFrameGo.transform.parent = environmentParent.transform;
            goalFrameGo.transform.position = new Vector3(0f, 0f, 11.0f);

            // Cột dọc trái
            var leftPost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leftPost.name = "LeftPost";
            leftPost.transform.parent = goalFrameGo.transform;
            leftPost.transform.localPosition = new Vector3(-3.66f, 1.22f, 0f);
            leftPost.transform.localScale = new Vector3(0.12f, 1.22f, 0.12f);
            ApplyColor(leftPost, Color.white);

            // Cột dọc phải
            var rightPost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rightPost.name = "RightPost";
            rightPost.transform.parent = goalFrameGo.transform;
            rightPost.transform.localPosition = new Vector3(3.66f, 1.22f, 0f);
            rightPost.transform.localScale = new Vector3(0.12f, 1.22f, 0.12f);
            ApplyColor(rightPost, Color.white);

            // Xà ngang
            var crossbar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            crossbar.name = "Crossbar";
            crossbar.transform.parent = goalFrameGo.transform;
            crossbar.transform.localPosition = new Vector3(0f, 2.44f, 0f);
            crossbar.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            crossbar.transform.localScale = new Vector3(0.12f, 3.66f, 0.12f);
            ApplyColor(crossbar, Color.white);

            // Khung hậu
            var leftBackPost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leftBackPost.name = "LeftBackSupport";
            leftBackPost.transform.parent = goalFrameGo.transform;
            leftBackPost.transform.localPosition = new Vector3(-3.66f, 1.22f, 1.5f);
            leftBackPost.transform.localScale = new Vector3(0.08f, 1.22f, 0.08f);
            ApplyColor(leftBackPost, new Color(0.85f, 0.90f, 0.95f));

            var rightBackPost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rightBackPost.name = "RightBackSupport";
            rightBackPost.transform.parent = goalFrameGo.transform;
            rightBackPost.transform.localPosition = new Vector3(3.66f, 1.22f, 1.5f);
            rightBackPost.transform.localScale = new Vector3(0.08f, 1.22f, 0.08f);
            ApplyColor(rightBackPost, new Color(0.85f, 0.90f, 0.95f));

            // Lưới Verlet 3D
            var netGo = new GameObject("GoalNet_Verlet3D");
            netGo.transform.parent = goalFrameGo.transform;
            netGo.transform.localPosition = Vector3.zero;
            var netView = netGo.AddComponent<GoalNetView>();

            // 5. Quả bóng (Ball) tại chấm phạt đền (0, 0.11, 0)
            var ballGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballGo.name = "MatchBall_Eleven";
            ballGo.transform.position = new Vector3(0f, 0.11f, 0f);
            ballGo.transform.localScale = Vector3.one * 0.22f; // Bán kính r = 11cm
            ApplyColor(ballGo, new Color(0.96f, 0.96f, 0.96f));

            var trail = ballGo.AddComponent<TrailRenderer>();
            trail.time = 0.40f;
            trail.startWidth = 0.14f;
            trail.endWidth = 0.01f;
            trail.emitting = false;
            var trailShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            trail.material = new Material(trailShader);
            trail.startColor = new Color(1f, 1f, 1f, 0.95f);
            trail.endColor = new Color(0.1f, 0.8f, 1.0f, 0f);

            var ballDriver = ballGo.AddComponent<BallDriver>();

            // 6. Thủ môn (Goalkeeper) trên vạch vôi
            var keeperGo = new GameObject("Goalkeeper_AI");
            keeperGo.transform.position = new Vector3(0f, 0.95f, 11.0f);

            var torso = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            torso.name = "Keeper_Body";
            torso.transform.parent = keeperGo.transform;
            torso.transform.localPosition = Vector3.zero;
            torso.transform.localScale = new Vector3(0.55f, 0.70f, 0.35f);
            ApplyColor(torso, new Color(0.98f, 0.82f, 0.10f));

            var leftGlove = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leftGlove.name = "LeftGlove";
            leftGlove.transform.parent = keeperGo.transform;
            leftGlove.transform.localPosition = new Vector3(-0.75f, 0.20f, 0f);
            leftGlove.transform.localScale = new Vector3(0.22f, 0.22f, 0.22f);
            ApplyColor(leftGlove, Color.white);

            var rightGlove = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rightGlove.name = "RightGlove";
            rightGlove.transform.parent = keeperGo.transform;
            rightGlove.transform.localPosition = new Vector3(0.75f, 0.20f, 0f);
            rightGlove.transform.localScale = new Vector3(0.22f, 0.22f, 0.22f);
            ApplyColor(rightGlove, Color.white);

            var keeperView = keeperGo.AddComponent<GoalkeeperView>();

            // 7. Camera chính
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.07f, 0.10f, 1.0f);
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 120f;
            camGo.transform.position = new Vector3(0f, 1.80f, -4.5f);
            camGo.transform.rotation = Quaternion.Euler(9.5f, 0f, 0f);

            // 8. Tạo Hệ thống Điều khiển & Gameplay Loop
            var controllerGo = new GameObject("MatchController");
            var swipeReceiver = controllerGo.AddComponent<TouchSwipeReceiver>();
            var scoreboard = controllerGo.AddComponent<ScoreboardUI>();
            var matchLoop = controllerGo.AddComponent<MatchGameLoop>();

            // Gán Serialized Fields cho MatchGameLoop qua SerializedObject
            var so = new SerializedObject(matchLoop);
            so.FindProperty("ballTransform").objectReferenceValue = ballGo.transform;
            so.FindProperty("ballTrail").objectReferenceValue = trail;
            so.FindProperty("goalNet").objectReferenceValue = netView;
            so.FindProperty("goalkeeper").objectReferenceValue = keeperView;
            so.FindProperty("swipeReceiver").objectReferenceValue = swipeReceiver;
            so.FindProperty("scoreboard").objectReferenceValue = scoreboard;
            so.FindProperty("mainCamera").objectReferenceValue = cam;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 9. Lưu Scene vào Assets/_Project/Scenes/Match.unity
            string scenePath = "Assets/_Project/Scenes/Match.unity";
            Directory.CreateDirectory("Assets/_Project/Scenes");
            EditorSceneManager.SaveScene(scene, scenePath);

            // 10. Cập nhật Build Settings
            var buildScenes = new[]
            {
                new EditorBuildSettingsScene(scenePath, true),
                new EditorBuildSettingsScene("Assets/_Project/Scenes/Boot.unity", true)
            };
            EditorBuildSettings.scenes = buildScenes;

            Debug.Log($"[MatchSceneGenerator] ĐÃ DỰNG THÀNH CÔNG SCENE: {scenePath} VÀ THÊM VÀO BUILD SETTINGS!");
        }

        private static void CreateLine(GameObject parent, string name, Vector3 pos, Vector3 scale)
        {
            var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = name;
            line.transform.parent = parent.transform;
            line.transform.position = pos;
            line.transform.localScale = scale;
            ApplyColor(line, Color.white);
        }

        private static void ApplyColor(GameObject go, Color color)
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
    }
}
