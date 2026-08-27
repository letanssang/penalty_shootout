using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using Eleven.Keeper;

namespace Eleven.Presentation.Net
{
    /// <summary>
    /// Bộ sinh cấu trúc lưới khung thành chuẩn cho môn bóng đá / luân lưu.
    /// Đảm bảo tổng số hạt &lt;= 600 và bố cục các điểm ghim cột/xà/đáy chính xác.
    /// </summary>
    public static class NetGridGenerator
    {
        public const float PostWidth = GoalFrame.Width; // 7.32m
        public const float PostHeight = GoalFrame.Height; // 2.44m
        public const float GoalLineZ = GoalFrame.PenaltyDistance; // 11.0m
        public const float TopDepth = 1.0f; // 11.0 + 1.0 = 12.0m
        public const float BottomDepth = 1.8f; // 11.0 + 1.8 = 12.8m

        public const int MaxAllowedParticles = 600;
        public const int DefaultIterations = 6;

        public struct NetData
        {
            public NetParticle[] particles;
            public int2[] constraints;
            public float[] restLengths;
        }

        /// <summary>
        /// Sinh mạng lưới hạt và các ràng buộc liên kết cho khung thành.
        /// </summary>
        public static NetData GenerateBoxNet(int cols = 17, int rows = 9, int depthSteps = 5)
        {
            var particleList = new List<NetParticle>();
            var constraintList = new List<int2>();

            float halfW = PostWidth * 0.5f;

            // 1. Mặt trên (Top face): z từ 11.0 đến (11.0 + TopDepth), y = PostHeight, x từ -halfW đến +halfW
            // Grid cols x depthSteps
            int[,] topGrid = new int[cols, depthSteps];
            for (int r = 0; r < depthSteps; r++)
            {
                float tZ = (float)r / (depthSteps - 1);
                float z = GoalLineZ + tZ * TopDepth;
                for (int c = 0; c < cols; c++)
                {
                    float tX = (float)c / (cols - 1);
                    float x = -halfW + tX * PostWidth;
                    float y = PostHeight;

                    // Mép trước (r == 0) gắn vào xà ngang -> pinned
                    // Hai bên mép ngoài (c == 0 hoặc c == cols - 1) ở r == 0 gắn vào góc chữ A -> pinned
                    byte pinned = (byte)(r == 0 ? 1 : 0);

                    int idx = particleList.Count;
                    topGrid[c, r] = idx;
                    particleList.Add(new NetParticle(new float3(x, y, z), pinned));
                }
            }

            // 2. Mặt sau (Back face): z từ (11.0 + TopDepth) tại y = PostHeight xuống (11.0 + BottomDepth) tại y = 0
            int[,] backGrid = new int[cols, rows];
            for (int r = 0; r < rows; r++)
            {
                float tY = (float)r / (rows - 1); // 0 = trên cùng, 1 = đáy đất
                float y = PostHeight * (1.0f - tY);
                float z = (GoalLineZ + TopDepth) + tY * (BottomDepth - TopDepth);

                for (int c = 0; c < cols; c++)
                {
                    if (r == 0)
                    {
                        // Hàng trên cùng của mặt sau chính là hàng cuối cùng của mặt trên
                        backGrid[c, 0] = topGrid[c, depthSteps - 1];
                    }
                    else
                    {
                        float tX = (float)c / (cols - 1);
                        float x = -halfW + tX * PostWidth;
                        // Hàng đáy dưới đất (r == rows - 1) ghim cố định
                        byte pinned = (byte)(r == rows - 1 ? 1 : 0);

                        int idx = particleList.Count;
                        backGrid[c, r] = idx;
                        particleList.Add(new NetParticle(new float3(x, y, z), pinned));
                    }
                }
            }

            // 3. Mặt hông trái và phải (Left / Right side faces)
            // Ghim viền cột dọc trước (z = 11.0)
            int[,] leftGrid = new int[depthSteps, rows];
            int[,] rightGrid = new int[depthSteps, rows];

            for (int d = 0; d < depthSteps; d++)
            {
                float tZ = (float)d / (depthSteps - 1);
                for (int r = 0; r < rows; r++)
                {
                    float tY = (float)r / (rows - 1);
                    float y = PostHeight * (1.0f - tY);
                    float zBack = (GoalLineZ + TopDepth) + tY * (BottomDepth - TopDepth);
                    float z = math.lerp(GoalLineZ, zBack, tZ);

                    // Trái
                    if (d == 0)
                    {
                        // Gắn vào cột dọc trái (x = -halfW, z = 11.0)
                        byte pinned = 1;
                        int idx = particleList.Count;
                        leftGrid[0, r] = idx;
                        particleList.Add(new NetParticle(new float3(-halfW, y, GoalLineZ), pinned));
                    }
                    else if (d == depthSteps - 1)
                    {
                        // Mép sau của mặt trái là mép trái của mặt sau
                        leftGrid[d, r] = backGrid[0, r];
                    }
                    else if (r == 0)
                    {
                        // Mép trên của mặt trái là mép trái của mặt trên
                        leftGrid[d, 0] = topGrid[0, d];
                    }
                    else
                    {
                        byte pinned = (byte)(r == rows - 1 ? 1 : 0);
                        int idx = particleList.Count;
                        leftGrid[d, r] = idx;
                        particleList.Add(new NetParticle(new float3(-halfW, y, z), pinned));
                    }

                    // Phải
                    if (d == 0)
                    {
                        // Gắn vào cột dọc phải (x = +halfW, z = 11.0)
                        byte pinned = 1;
                        int idx = particleList.Count;
                        rightGrid[0, r] = idx;
                        particleList.Add(new NetParticle(new float3(halfW, y, GoalLineZ), pinned));
                    }
                    else if (d == depthSteps - 1)
                    {
                        rightGrid[d, r] = backGrid[cols - 1, r];
                    }
                    else if (r == 0)
                    {
                        rightGrid[d, 0] = topGrid[cols - 1, d];
                    }
                    else
                    {
                        byte pinned = (byte)(r == rows - 1 ? 1 : 0);
                        int idx = particleList.Count;
                        rightGrid[d, r] = idx;
                        particleList.Add(new NetParticle(new float3(halfW, y, z), pinned));
                    }
                }
            }

            // 4. Tạo ràng buộc (Constraints) cho các lưới
            void AddEdge(int i1, int i2)
            {
                if (i1 != i2)
                {
                    constraintList.Add(new int2(i1, i2));
                }
            }

            void ConnectGrid(int[,] grid, int w, int h)
            {
                for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    if (x + 1 < w) AddEdge(grid[x, y], grid[x + 1, y]);
                    if (y + 1 < h) AddEdge(grid[x, y], grid[x, y + 1]);
                    // Ràng buộc chéo
                    if (x + 1 < w && y + 1 < h)
                    {
                        AddEdge(grid[x, y], grid[x + 1, y + 1]);
                        AddEdge(grid[x + 1, y], grid[x, y + 1]);
                    }
                }
            }

            ConnectGrid(topGrid, cols, depthSteps);
            ConnectGrid(backGrid, cols, rows);
            ConnectGrid(leftGrid, depthSteps, rows);
            ConnectGrid(rightGrid, depthSteps, rows);

            // Tính restLengths
            var particles = particleList.ToArray();
            var constraints = constraintList.ToArray();
            var restLengths = new float[constraints.Length];

            for (int i = 0; i < constraints.Length; i++)
            {
                int2 c = constraints[i];
                restLengths[i] = math.distance(particles[c.x].position, particles[c.y].position);
            }

            return new NetData
            {
                particles = particles,
                constraints = constraints,
                restLengths = restLengths
            };
        }
    }
}
