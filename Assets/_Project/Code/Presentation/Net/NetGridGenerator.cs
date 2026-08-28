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

            /// <summary>
            /// Số ràng buộc ĐẦU MẢNG thuộc loại cạnh khung ô (dọc + ngang). Phần còn lại là
            /// ràng buộc chéo. Hai loại xếp tách bạch để bên vẽ lấy đúng khúc đầu mà dựng
            /// lưới ô vuông, còn bên mô phỏng vẫn giải hết cả mảng.
            /// </summary>
            public int structuralCount;

            /// <summary>Đường chéo một ô lưới ở mặt sau (m) — xem <see cref="BackCellDiagonal"/>.</summary>
            public float backCellDiagonal;
        }

        /// <summary>
        /// Đường chéo của một ô lưới trên MẶT SAU. Đây là con số quyết định bán kính ảnh
        /// hưởng của quả bóng lên lưới: hạt nằm xa nhất so với một điểm bất kỳ trên mặt lưới
        /// là góc ô, cách nửa đường chéo. Bán kính đẩy nhỏ hơn nửa đường chéo thì bóng chui
        /// lọt giữa bốn hạt mà không hạt nào nhúc nhích — đúng triệu chứng "lưới gần như
        /// không rung" báo ngày 2026-08-28.
        ///
        /// Lấy mặt sau làm chuẩn vì cú luân lưu vào lưới thì găm vào mặt sau, và đây cũng là
        /// mặt người chơi nhìn thẳng suốt cả lượt.
        /// </summary>
        public static float BackCellDiagonal(int cols, int rows)
        {
            float dx = PostWidth / math.max(1, cols - 1);
            // Mặt sau nghiêng: mỗi hàng vừa tụt xuống vừa lùi ra sau.
            float dy = PostHeight / math.max(1, rows - 1);
            float dz = (BottomDepth - TopDepth) / math.max(1, rows - 1);
            float down = math.sqrt(dy * dy + dz * dz);
            return math.sqrt(dx * dx + down * down);
        }

        /// <summary>
        /// Sinh mạng lưới hạt và các ràng buộc liên kết cho khung thành.
        ///
        /// MẶC ĐỊNH 24 x 9 x 5 (378 hạt). Con số cột đổi từ 17 lên 24 ngày 2026-08-28 để ô
        /// lưới VUÔNG: bước ngang 7.32/23 = 0.318m, bước dọc mặt sau 2.568/8 = 0.321m — lệch
        /// 0.8%, mắt không thấy. Với 17 cột thì ô là 0.458 x 0.321, dẹt rõ ràng theo chiều
        /// ngang. Mặt hông cũng gần vuông (0.35 x 0.321); riêng mặt nóc là 0.25 x 0.318 nhưng
        /// nóc chỉ hiện ra ở góc rất xiên nên không đáng đánh đổi thêm hạt.
        /// </summary>
        public static NetData GenerateBoxNet(int cols = 24, int rows = 9, int depthSteps = 5)
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

            // Cạnh khung ô (dọc + ngang) và cạnh chéo phải nằm TÁCH BẠCH trong mảng:
            // khung ô đứng trước, chéo đứng sau. Bên vẽ chỉ lấy khúc đầu nên ra lưới ô vuông;
            // bên mô phỏng vẫn giải cả mảng nên lưới vẫn không bị trượt xô (shear) thành
            // hình thoi. Trước đây vẽ cả mảng, thành ra mỗi ô có thêm hai vạch chéo — chính
            // là những hình tam giác / chữ X người chơi thấy thay vì ô vuông.
            void ConnectStructural(int[,] grid, int w, int h)
            {
                for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    if (x + 1 < w) AddEdge(grid[x, y], grid[x + 1, y]);
                    if (y + 1 < h) AddEdge(grid[x, y], grid[x, y + 1]);
                }
            }

            void ConnectShear(int[,] grid, int w, int h)
            {
                for (int x = 0; x + 1 < w; x++)
                for (int y = 0; y + 1 < h; y++)
                {
                    AddEdge(grid[x, y], grid[x + 1, y + 1]);
                    AddEdge(grid[x + 1, y], grid[x, y + 1]);
                }
            }

            ConnectStructural(topGrid, cols, depthSteps);
            ConnectStructural(backGrid, cols, rows);
            ConnectStructural(leftGrid, depthSteps, rows);
            ConnectStructural(rightGrid, depthSteps, rows);
            int structuralCount = constraintList.Count;

            ConnectShear(topGrid, cols, depthSteps);
            ConnectShear(backGrid, cols, rows);
            ConnectShear(leftGrid, depthSteps, rows);
            ConnectShear(rightGrid, depthSteps, rows);

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
                restLengths = restLengths,
                structuralCount = structuralCount,
                backCellDiagonal = BackCellDiagonal(cols, rows)
            };
        }
    }
}
