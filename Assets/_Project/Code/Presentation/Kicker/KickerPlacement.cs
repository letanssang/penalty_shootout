using Unity.Mathematics;

namespace Eleven.Presentation.Kicker
{
    /// <summary>
    /// Chỗ người sút đứng. Một nguồn duy nhất cho cả greybox lẫn model thật — hai bản sao
    /// của cùng bộ toạ độ là cách chắc chắn nhất để một hôm nào đó chân trụ của model thật
    /// đứng lệch nửa mét so với greybox mà không ai biết vì sao.
    ///
    /// Toạ độ trong hệ của trận đấu: bóng ở gốc, khung thành ở +Z.
    /// </summary>
    public static class KickerPlacement
    {
        /// <summary>Điểm xuất phát đà chạy — phía sau, chếch trái quả bóng.</summary>
        public static readonly float3 Start = new float3(-0.9f, 0f, -2.6f);

        /// <summary>Chỗ chân trụ đặt xuống cạnh bóng lúc sút.</summary>
        public static readonly float3 Plant = new float3(-0.35f, 0f, -0.15f);
    }
}
