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
    /// Công cụ sinh Scene thi đấu chính Match.unity hoàn chỉnh chỉ bằng một lệnh.
    /// Tự động thiết lập: Khung thành FIFA, Lưới Verlet 3D, Quả bóng, Thủ môn AI, Camera và Bảng điểm.
    /// </summary>
    public static class MatchSceneGenerator
    {
        [MenuItem("Eleven/Gameplay/Dựng Scene Thi Đấu (Match.unity)")]
        public static void GenerateMatchScene()
        {
            // 1. Tạo Scene mới
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 2. Tạo Ánh sáng (Directional Light)
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.88f);
            light.intensity = 1.3f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            // 3. Tạo Mặt sân cỏ 12m (Pitch)
            var pitchGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            pitchGo.name = "StadiumPitch_12m";
            pitchGo.transform.position = new Vector3(0f, 0f, 5.5f);
            pitchGo.transform.localScale = new Vector3(2.5f, 1f, 2.5f); // 25m x 25m

            // 4. Tạo Khung thành chuẩn FIFA (Width = 7.32m, Height = 2.44m tại Z = 11.0m)
            var goalFrameGo = new GameObject("GoalFrame_FIFA");
            goalFrameGo.transform.position = new Vector3(0f, 0f, 11.0f);

            // Cột dọc trái
            var leftPost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leftPost.name = "LeftPost";
            leftPost.transform.parent = goalFrameGo.transform;
            leftPost.transform.localPosition = new Vector3(-3.66f, 1.22f, 0f);
            leftPost.transform.localScale = new Vector3(0.12f, 1.22f, 0.12f);

            // Cột dọc phải
            var rightPost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rightPost.name = "RightPost";
            rightPost.transform.parent = goalFrameGo.transform;
            rightPost.transform.localPosition = new Vector3(3.66f, 1.22f, 0f);
            rightPost.transform.localScale = new Vector3(0.12f, 1.22f, 0.12f);

            // Xà ngang
            var crossbar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            crossbar.name = "Crossbar";
            crossbar.transform.parent = goalFrameGo.transform;
            crossbar.transform.localPosition = new Vector3(0f, 2.44f, 0f);
            crossbar.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            crossbar.transform.localScale = new Vector3(0.12f, 3.66f, 0.12f);

            // Lưới Verlet 3D
            var netGo = new GameObject("GoalNet_Verlet3D");
            netGo.transform.parent = goalFrameGo.transform;
            netGo.transform.localPosition = Vector3.zero;
            var netView = netGo.AddComponent<GoalNetView>();

            // 5. Tạo Quả bóng (Ball) tại chấm phạt đền (0, 0.11, 0)
            var ballGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballGo.name = "MatchBall_Eleven";
            ballGo.transform.position = new Vector3(0f, 0.11f, 0f);
            ballGo.transform.localScale = Vector3.one * 0.22f; // Bán kính r = 11cm

            var trail = ballGo.AddComponent<TrailRenderer>();
            trail.time = 0.45f;
            trail.startWidth = 0.12f;
            trail.endWidth = 0.01f;
            trail.emitting = false;

            // 6. Tạo Nhân vật Thủ môn (Goalkeeper) trên vạch vôi
            var keeperGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            keeperGo.name = "Goalkeeper_AI";
            keeperGo.transform.position = new Vector3(0f, 0.95f, 11.0f);
            keeperGo.transform.localScale = new Vector3(0.55f, 0.95f, 0.45f);
            var keeperView = keeperGo.AddComponent<GoalkeeperView>();

            // 7. Tạo Camera chính
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            camGo.transform.position = new Vector3(0f, 1.8f, -4.5f);
            camGo.transform.rotation = Quaternion.Euler(10f, 0f, 0f);

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
    }
}
