using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Eleven.Ball;
using Eleven.Match;

namespace Eleven.Editor.Ball
{
    /// <summary>
    /// Vẽ quỹ đạo, khung thành và lưới 3x3 lên Scene view bằng Handles — không phải
    /// OnDrawGizmos, vì TrajectoryWindow là cửa sổ công cụ độc lập, không gắn vào
    /// GameObject nào trong scene để mà nhận callback OnDrawGizmos.
    /// </summary>
    static class TrajectoryGizmos
    {
        public static void DrawTrajectory(NativeArray<TrajectorySample> samples, int count, Color color)
        {
            if (count < 2)
                return;

            Handles.color = color;
            for (int i = 1; i < count; i++)
                Handles.DrawLine(samples[i - 1].position, samples[i].position, 2f);
        }

        /// <summary>Khung thành: hai cột + xà ngang, đường tâm theo đúng quy ước của GoalGeometry.</summary>
        public static void DrawGoalFrame()
        {
            float halfW = GoalGeometry.Width * 0.5f + GoalGeometry.PostRadius;
            float barY = GoalGeometry.Height + GoalGeometry.PostRadius;
            float z = GoalGeometry.PenaltyDistance;

            var leftBottom = new Vector3(-halfW, 0f, z);
            var leftTop = new Vector3(-halfW, barY, z);
            var rightTop = new Vector3(halfW, barY, z);
            var rightBottom = new Vector3(halfW, 0f, z);

            Handles.color = Color.white;
            Handles.DrawLine(leftBottom, leftTop, 3f);
            Handles.DrawLine(leftTop, rightTop, 3f);
            Handles.DrawLine(rightTop, rightBottom, 3f);
        }

        /// <summary>Lưới 3x3 bên trong khung thành, chỉ để tham chiếu mắt — không phải hàng rào va chạm.</summary>
        public static void DrawGrid()
        {
            float z = GoalGeometry.PenaltyDistance;
            float w = GoalGeometry.Width;
            float h = GoalGeometry.Height;

            Handles.color = new Color(1f, 1f, 1f, 0.25f);

            for (int i = 1; i < 3; i++)
            {
                float x = -w * 0.5f + i * (w / 3f);
                Handles.DrawLine(new Vector3(x, 0f, z), new Vector3(x, h, z));
            }
            for (int i = 1; i < 3; i++)
            {
                float y = i * (h / 3f);
                Handles.DrawLine(new Vector3(-w * 0.5f, y, z), new Vector3(w * 0.5f, y, z));
            }
        }

        public static void DrawCrossing(float3 point, Color color)
        {
            Handles.color = color;
            Handles.SphereHandleCap(0, point, Quaternion.identity, 0.15f, EventType.Repaint);
        }
    }
}
