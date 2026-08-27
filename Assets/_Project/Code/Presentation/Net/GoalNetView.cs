using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Eleven.Presentation.Net;

namespace Eleven.Presentation.Net
{
    /// <summary>
    /// Component trực quan hóa và mô phỏng mạng lưới khung thành 3D bằng Verlet (T28).
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class GoalNetView : MonoBehaviour, IDisposable
    {
        private NetSimulator simulator;
        private Mesh netMesh;
        private Vector3[] meshVertices;
        private int[] meshTriangles;
        private bool isInitialized;

        public void Initialize()
        {
            if (isInitialized) return;

            // Khởi tạo NetSimulator (287 hạt Verlet)
            simulator = new NetSimulator(enableSimulation: true);

            var particles = simulator.Particles;
            int count = particles.Length;

            meshVertices = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                meshVertices[i] = (Vector3)(float3)particles[i].position;
            }

            // Sinh topology hình học cho lưới (Wireframe/Quads/Triangles)
            var constraints = simulator.Constraints;
            int numConstraints = constraints.Length;
            var trianglesList = new List<int>(numConstraints * 2);

            for (int i = 0; i < numConstraints; i++)
            {
                var c = constraints[i];
                // Vẽ đường liên kết lưới dưới dạng đường dây hoặc tam giác mảnh
                trianglesList.Add(c.indexA);
                trianglesList.Add(c.indexB);
                trianglesList.Add(c.indexA);
            }

            meshTriangles = trianglesList.ToArray();

            netMesh = new Mesh
            {
                name = "GoalNet_ProceduralMesh"
            };
            netMesh.MarkDynamic();
            netMesh.vertices = meshVertices;
            netMesh.SetIndices(meshTriangles, MeshTopology.Lines, 0);

            var mf = GetComponent<MeshFilter>();
            mf.sharedMesh = netMesh;

            isInitialized = true;
        }

        private void Awake()
        {
            Initialize();
        }

        private void OnDestroy()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (simulator != null)
            {
                simulator.Dispose();
                simulator = null;
            }
            if (netMesh != null)
            {
                Destroy(netMesh);
                netMesh = null;
            }
            isInitialized = false;
        }

        /// <summary>
        /// Cập nhật mô phỏng lưới tương tác với vị trí và vận tốc quả bóng mỗi khung hình.
        /// </summary>
        public void UpdateSimulation(float dt, float3 ballPos, float3 ballVel, float ballRadius = 0.11f)
        {
            if (!isInitialized || simulator == null) return;

            // Chạy bước tính toán Verlet với CCD chống xuyên bóng
            simulator.StepWithBall(dt, ballPos, ballVel, ballRadius);

            // Cập nhật đỉnh Mesh
            var particles = simulator.Particles;
            int count = particles.Length;
            for (int i = 0; i < count; i++)
            {
                meshVertices[i] = (Vector3)(float3)particles[i].position;
            }

            netMesh.vertices = meshVertices;
            netMesh.RecalculateBounds();
        }
    }
}
