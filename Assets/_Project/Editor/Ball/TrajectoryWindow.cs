using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Eleven.Ball;
using Eleven.Match;

namespace Eleven.Editor.Ball
{
    /// <summary>
    /// Cửa sổ Editor để chỉnh tham số cú sút bằng thanh trượt và thấy quỹ đạo đổi ngay
    /// trong Scene view. Nhiều quỹ đạo so sánh được cùng lúc, mỗi cái một màu.
    ///
    /// GHI CHÚ: các mục nghiệm thu cần NHÌN THẤY cửa sổ thật (thanh trượt phản hồi, quỹ đạo
    /// vẽ đúng trong Scene view, kích thước build không phình) không kiểm được bằng
    /// -batchmode — CẦN NGƯỜI KIỂM mở Unity, vào menu Eleven/Ball/Trajectory Window.
    /// </summary>
    public class TrajectoryWindow : EditorWindow
    {
        [Serializable]
        class Overlay
        {
            public string label = "A";
            public float speed = 25f;
            public float horizontalAngleDeg;
            public float verticalAngleDeg = 10f;
            public float spinX, spinY, spinZ;
            public Color color = Color.yellow;

            [NonSerialized] public NativeArray<TrajectorySample> samples;
            [NonSerialized] public int sampleCount;
            [NonSerialized] public ShotOutcome outcome;
            [NonSerialized] public float3 crossing;
            [NonSerialized] public int cell;
        }

        const int MaxSamples = 512;
        const float SimDt = 1f / 120f;
        const float MaxTime = 2f;

        [SerializeField] List<Overlay> overlays = new List<Overlay>();
        [SerializeField] BallParams ballParams = BallParams.Default;
        Vector2 scroll;

        [MenuItem("Eleven/Ball/Trajectory Window")]
        public static void Open() => GetWindow<TrajectoryWindow>("Trajectory");

        void OnEnable()
        {
            if (overlays.Count == 0)
                overlays.Add(new Overlay());

            foreach (var o in overlays)
                o.samples = new NativeArray<TrajectorySample>(MaxSamples, Allocator.Persistent);

            SceneView.duringSceneGui += OnSceneGUI;
            RecomputeAll();
        }

        void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            foreach (var o in overlays)
                if (o.samples.IsCreated)
                    o.samples.Dispose();
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("Tham số khí động (BallParams)", EditorStyles.boldLabel);
            ballParams.mass = EditorGUILayout.FloatField("Mass", ballParams.mass);
            ballParams.radius = EditorGUILayout.FloatField("Radius", ballParams.radius);
            ballParams.airDensity = EditorGUILayout.FloatField("Air Density", ballParams.airDensity);
            ballParams.gravity = EditorGUILayout.FloatField("Gravity", ballParams.gravity);
            ballParams.cdLow = EditorGUILayout.FloatField("Cd Low", ballParams.cdLow);
            ballParams.cdHigh = EditorGUILayout.FloatField("Cd High", ballParams.cdHigh);
            ballParams.cdVLow = EditorGUILayout.FloatField("Cd V Low", ballParams.cdVLow);
            ballParams.cdVHigh = EditorGUILayout.FloatField("Cd V High", ballParams.cdVHigh);
            ballParams.liftCoefficient = EditorGUILayout.FloatField("Lift Coefficient", ballParams.liftCoefficient);
            ballParams.spinDecayPerSecond = EditorGUILayout.FloatField("Spin Decay/s", ballParams.spinDecayPerSecond);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Quỹ đạo so sánh", EditorStyles.boldLabel);

            for (int i = 0; i < overlays.Count; i++)
            {
                var o = overlays[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                o.label = EditorGUILayout.TextField(o.label, GUILayout.Width(80));
                o.color = EditorGUILayout.ColorField(o.color, GUILayout.Width(50));
                bool remove = GUILayout.Button("X", GUILayout.Width(20)) && overlays.Count > 1;
                EditorGUILayout.EndHorizontal();

                o.speed = EditorGUILayout.Slider("Tốc độ (m/s)", o.speed, 1f, 40f);
                o.horizontalAngleDeg = EditorGUILayout.Slider("Góc ngang (°)", o.horizontalAngleDeg, -45f, 45f);
                o.verticalAngleDeg = EditorGUILayout.Slider("Góc dọc (°)", o.verticalAngleDeg, -10f, 45f);
                o.spinX = EditorGUILayout.Slider("Xoáy trục X (rad/s)", o.spinX, -100f, 100f);
                o.spinY = EditorGUILayout.Slider("Xoáy trục Y (rad/s)", o.spinY, -100f, 100f);
                o.spinZ = EditorGUILayout.Slider("Xoáy trục Z (rad/s)", o.spinZ, -100f, 100f);

                EditorGUILayout.LabelField("Kết quả", $"{o.outcome} — ô {o.cell} — cắt tại ({o.crossing.x:F2}, {o.crossing.y:F2}, {o.crossing.z:F2})");

                EditorGUILayout.EndVertical();

                if (remove)
                {
                    if (o.samples.IsCreated) o.samples.Dispose();
                    overlays.RemoveAt(i);
                    i--;
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                RecomputeAll();
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("+ Thêm quỹ đạo so sánh"))
            {
                char nextLabel = (char)('A' + overlays.Count);
                var fresh = new Overlay { label = nextLabel.ToString(), color = UnityEngine.Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.8f, 1f) };
                fresh.samples = new NativeArray<TrajectorySample>(MaxSamples, Allocator.Persistent);
                overlays.Add(fresh);
                RecomputeAll();
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Preset", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Lưu preset..."))
                SavePreset();
            if (GUILayout.Button("Tải preset..."))
                LoadPreset();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndScrollView();
        }

        static BallState BuildInitialState(Overlay o)
        {
            float yaw = math.radians(o.horizontalAngleDeg);
            float pitch = math.radians(o.verticalAngleDeg);

            float3 dir = new float3(
                math.sin(yaw) * math.cos(pitch),
                math.sin(pitch),
                math.cos(yaw) * math.cos(pitch));

            return new BallState
            {
                position = float3.zero,
                velocity = dir * o.speed,
                spin = new float3(o.spinX, o.spinY, o.spinZ)
            };
        }

        void RecomputeAll()
        {
            foreach (var o in overlays)
            {
                if (!o.samples.IsCreated)
                    o.samples = new NativeArray<TrajectorySample>(MaxSamples, Allocator.Persistent);

                var start = BuildInitialState(o);
                o.sampleCount = TrajectoryPredictor.Predict(start, ballParams, SimDt, MaxTime, o.samples);
                o.outcome = GoalGeometry.Classify(start, ballParams, out o.crossing, out o.cell);
            }
        }

        void OnSceneGUI(SceneView view)
        {
            TrajectoryGizmos.DrawGoalFrame();
            TrajectoryGizmos.DrawGrid();

            foreach (var o in overlays)
            {
                TrajectoryGizmos.DrawTrajectory(o.samples, o.sampleCount, o.color);
                if (o.outcome != ShotOutcome.Short)
                    TrajectoryGizmos.DrawCrossing(o.crossing, o.color);
            }
        }

        void SavePreset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Lưu preset quỹ đạo", "TrajectoryPreset", "asset", "Chọn nơi lưu preset");
            if (string.IsNullOrEmpty(path))
                return;

            var preset = ScriptableObject.CreateInstance<TrajectoryPreset>();
            preset.ballParams = ballParams;
            preset.overlays = new List<TrajectoryPreset.OverlayData>();
            foreach (var o in overlays)
            {
                preset.overlays.Add(new TrajectoryPreset.OverlayData
                {
                    label = o.label,
                    speed = o.speed,
                    horizontalAngleDeg = o.horizontalAngleDeg,
                    verticalAngleDeg = o.verticalAngleDeg,
                    spinX = o.spinX,
                    spinY = o.spinY,
                    spinZ = o.spinZ,
                    color = o.color
                });
            }

            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();
        }

        void LoadPreset()
        {
            string absolutePath = EditorUtility.OpenFilePanel("Tải preset quỹ đạo", "Assets", "asset");
            if (string.IsNullOrEmpty(absolutePath) || !absolutePath.StartsWith(Application.dataPath))
                return;

            string assetPath = "Assets" + absolutePath.Substring(Application.dataPath.Length);
            var preset = AssetDatabase.LoadAssetAtPath<TrajectoryPreset>(assetPath);
            if (preset == null)
                return;

            ballParams = preset.ballParams;

            foreach (var o in overlays)
                if (o.samples.IsCreated)
                    o.samples.Dispose();
            overlays.Clear();

            foreach (var od in preset.overlays)
            {
                var fresh = new Overlay
                {
                    label = od.label,
                    speed = od.speed,
                    horizontalAngleDeg = od.horizontalAngleDeg,
                    verticalAngleDeg = od.verticalAngleDeg,
                    spinX = od.spinX,
                    spinY = od.spinY,
                    spinZ = od.spinZ,
                    color = od.color
                };
                fresh.samples = new NativeArray<TrajectorySample>(MaxSamples, Allocator.Persistent);
                overlays.Add(fresh);
            }

            if (overlays.Count == 0)
                overlays.Add(new Overlay { samples = new NativeArray<TrajectorySample>(MaxSamples, Allocator.Persistent) });

            RecomputeAll();
            SceneView.RepaintAll();
        }
    }

    /// <summary>
    /// Lưu trạng thái TrajectoryWindow thành asset để tải lại sau. Nằm trong cùng file
    /// TrajectoryWindow.cs (không phải file riêng) để không vượt quá danh sách file được
    /// phép của T11.
    /// </summary>
    public class TrajectoryPreset : ScriptableObject
    {
        [Serializable]
        public class OverlayData
        {
            public string label;
            public float speed;
            public float horizontalAngleDeg;
            public float verticalAngleDeg;
            public float spinX, spinY, spinZ;
            public Color color;
        }

        public BallParams ballParams;
        public List<OverlayData> overlays = new List<OverlayData>();
    }
}
