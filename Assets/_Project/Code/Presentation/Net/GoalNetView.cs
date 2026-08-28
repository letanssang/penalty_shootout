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

            // Khởi tạo NetSimulator (378 hạt Verlet, z = 11.0m đến 12.8m).
            // Cỡ lưới để mặc định — NetGridGenerator.GenerateBoxNet giữ lý do chọn 24 x 9 x 5.
            simulator = new NetSimulator();

            var particles = simulator.Particles;
            int count = particles.Length;

            meshVertices = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                meshVertices[i] = (Vector3)(float3)particles[i].position;
            }

            // Sinh topology hình học cho lưới.
            //
            // CHỈ VẼ CẠNH KHUNG Ô, không vẽ ràng buộc chéo. Ràng buộc chéo có thật trong mô
            // phỏng (nó chống cho lưới khỏi xô lệch thành hình thoi) nhưng nó KHÔNG phải sợi
            // lưới — vẽ luôn cả nó thì mỗi ô mọc thêm hai vạch chéo, ra tấm lưới toàn tam
            // giác và chữ X. Lưới bóng đá thật là ô vuông, nên chỉ lấy khúc đầu của mảng
            // ràng buộc (NetGridGenerator xếp cạnh khung ô lên trước đúng vì việc này).
            var constraints = simulator.Constraints;
            int numDrawn = simulator.StructuralConstraintCount;
            var indicesList = new List<int>(numDrawn * 2);

            for (int i = 0; i < numDrawn; i++)
            {
                var c = constraints[i];
                indicesList.Add(c.x);
                indicesList.Add(c.y);
            }

            meshTriangles = indicesList.ToArray();

            netMesh = new Mesh
            {
                name = "GoalNet_ProceduralMesh"
            };
            netMesh.MarkDynamic();
            netMesh.vertices = meshVertices;
            netMesh.SetIndices(meshTriangles, MeshTopology.Lines, 0);

            var mf = GetComponent<MeshFilter>();
            mf.sharedMesh = netMesh;

            var mr = GetComponent<MeshRenderer>();
            var netShader = Shader.Find("Universal Render Pipeline/Unlit") 
                         ?? Shader.Find("Sprites/Default") 
                         ?? Shader.Find("Unlit/Color");
            var netMat = new Material(netShader);
            netMat.color = new Color(0.92f, 0.96f, 1.0f, 0.95f);
            if (netMat.HasProperty("_BaseColor")) netMat.SetColor("_BaseColor", netMat.color);
            if (netMat.HasProperty("_Color")) netMat.SetColor("_Color", netMat.color);
            mr.material = netMat;

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

            // Chạy bước tính toán Verlet
            simulator.StepSynchronous(ballPos, ballVel, ballRadius, dt);

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
