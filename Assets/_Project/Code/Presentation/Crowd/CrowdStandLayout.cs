using Unity.Mathematics;
using Eleven.Keeper;

namespace Eleven.Presentation.Crowd
{
    /// <summary>
    /// Sinh chỗ ngồi cho ba khán đài quanh vùng sân 12m đã dựng (xem
    /// <see cref="CameraAuthoredBounds"/>): một khán đài chính sau khung thành và hai cánh
    /// hai bên. Toàn bộ ghế nằm NGOÀI hình hộp camera được phép đi vào, nên không có góc quay
    /// nào lọt vào giữa đám đông và lộ ra rằng họ chỉ là những tấm bảng phẳng.
    ///
    /// Tất định tuyệt đối theo seed: dùng <see cref="Unity.Mathematics.Random"/>, không bao giờ
    /// dùng <c>UnityEngine.Random</c>. Cùng seed → cùng khán đài trên mọi máy, nhờ vậy ảnh chụp
    /// so sánh giữa hai lần đo hiệu năng mới có nghĩa.
    /// </summary>
    public static class CrowdStandLayout
    {
        /// <summary>Khoảng cách giữa hai ghế cạnh nhau (m).</summary>
        public const float SeatSpacing = 0.62f;

        /// <summary>Mỗi hàng lùi ra sau bao nhiêu mét.</summary>
        public const float RowDepth = 0.85f;

        /// <summary>Mỗi hàng cao lên bao nhiêu mét.</summary>
        public const float RowRise = 0.42f;

        /// <summary>
        /// Hàng ghế đầu tiên của khán đài chính nằm ở z bao nhiêu mét. Phải lớn hơn 15.0 —
        /// biên xa nhất mà camera được phép tới (<c>CameraAuthoredBounds.MaxBounds.z</c>) —
        /// cộng thêm biên độ lệch ngẫu nhiên, nếu không hàng đầu sẽ nằm TRONG hộp camera.
        /// </summary>
        public const float MainStandFrontZ = 15.5f;

        /// <summary>Chiều cao mặt sàn hàng đầu tiên (m).</summary>
        public const float FirstRowHeight = 0.55f;

        public const int MainStandRows = 14;
        public const int MainStandSeatsPerRow = 46;

        public const int WingStandRows = 10;
        public const int WingStandSeatsPerRow = 30;

        /// <summary>Cánh trái/phải bắt đầu ở |x| bao nhiêu mét.</summary>
        public const float WingStandStartX = 10.5f;

        /// <summary>Ghế đầu tiên của cánh nằm ở z bao nhiêu mét.</summary>
        public const float WingStandFrontZ = -5.0f;

        /// <summary>Tổng số ghế mà bố cục này sinh ra khi buffer đủ chỗ.</summary>
        public const int TotalSeats =
            MainStandRows * MainStandSeatsPerRow + 2 * WingStandRows * WingStandSeatsPerRow;

        /// <summary>
        /// Đổ chỗ ngồi vào <paramref name="buffer"/> và trả về số ghế đã ghi.
        /// Không cấp phát gì: người gọi đưa mảng, hàm này chỉ ghi vào.
        /// Nếu buffer nhỏ hơn <see cref="TotalSeats"/> thì dừng khi đầy — không ném lỗi, vì
        /// đây là đường chạy lúc khởi tạo cảnh, không phải chỗ để làm sập game.
        /// </summary>
        public static int Generate(CrowdInstance[] buffer, uint seed)
        {
            if (buffer == null || buffer.Length == 0)
            {
                return 0;
            }

            var rng = new Random(seed == 0u ? 1u : seed);
            int written = 0;

            // 1. Khán đài chính: sau khung thành, nhìn thẳng vào lưới.
            for (int row = 0; row < MainStandRows && written < buffer.Length; row++)
            {
                float z = MainStandFrontZ + row * RowDepth;
                float y = FirstRowHeight + row * RowRise;
                float rowWidth = (MainStandSeatsPerRow - 1) * SeatSpacing;

                for (int seat = 0; seat < MainStandSeatsPerRow && written < buffer.Length; seat++)
                {
                    float x = -rowWidth * 0.5f + seat * SeatSpacing;
                    buffer[written++] = MakeSeat(ref rng, new float3(x, y, z));
                }
            }

            // 2. Hai cánh: song song với đường biên, đối xứng qua trục x = 0.
            for (int side = 0; side < 2 && written < buffer.Length; side++)
            {
                float sign = side == 0 ? -1f : 1f;

                for (int row = 0; row < WingStandRows && written < buffer.Length; row++)
                {
                    float x = sign * (WingStandStartX + row * RowDepth);
                    float y = FirstRowHeight + row * RowRise;

                    for (int seat = 0; seat < WingStandSeatsPerRow && written < buffer.Length; seat++)
                    {
                        float z = WingStandFrontZ + seat * SeatSpacing;
                        buffer[written++] = MakeSeat(ref rng, new float3(x, y, z));
                    }
                }
            }

            return written;
        }

        /// <summary>
        /// Một ghế: vị trí lệch nhẹ (người ngồi không thẳng hàng như bàn cờ), pha animation,
        /// nhịp và màu áo — tất cả rút từ cùng một chuỗi ngẫu nhiên có seed.
        /// </summary>
        private static CrowdInstance MakeSeat(ref Random rng, in float3 seatCenter)
        {
            float jitterX = rng.NextFloat(-0.10f, 0.10f);
            float jitterZ = rng.NextFloat(-0.08f, 0.08f);

            return new CrowdInstance
            {
                position = new float3(seatCenter.x + jitterX, seatCenter.y, seatCenter.z + jitterZ),
                phase01 = rng.NextFloat(0f, 1f),
                scale = rng.NextFloat(1.55f, 1.85f),   // chiều cao người, mét
                speedScale = rng.NextFloat(0.85f, 1.15f),
                colorIndex = (byte)rng.NextInt(0, CrowdPalette.ColorCount)
            };
        }

        /// <summary>
        /// Ghế có nằm ngoài vùng camera được phép đi vào không. Dùng cho test: nếu một ghế lọt
        /// vào trong hình hộp đó thì có góc quay sẽ chui vào giữa đám đông.
        /// </summary>
        public static bool IsOutsideCameraBox(in float3 position)
        {
            bool insideX = position.x >= -8.0f && position.x <= 8.0f;
            bool insideZ = position.z >= -5.0f && position.z <= 15.0f;
            return !(insideX && insideZ);
        }

        /// <summary>Khoảng cách ngang gần nhất từ một ghế tới mặt phẳng khung thành.</summary>
        public static float DistanceToGoalPlane(in float3 position)
        {
            return math.abs(position.z - GoalFrame.PenaltyDistance);
        }
    }
}
