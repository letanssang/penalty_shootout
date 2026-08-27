using Unity.Mathematics;

namespace Eleven.Presentation.Crowd
{
    /// <summary>
    /// Bố cục của ĐÚNG MỘT texture atlas dùng chung cho toàn bộ khán đài.
    ///
    /// Lưới ô: <see cref="Columns"/> cột × <see cref="Rows"/> hàng.
    ///   - HÀNG  = <see cref="CrowdMood"/> (4 trạng thái, đúng thứ tự giá trị enum).
    ///   - CỘT   = khung hình animation của trạng thái đó (8 khung, lặp vòng).
    ///
    /// Vì sao chỉ một atlas: ô nghiệm thu T30 đòi MỘT draw call cho toàn bộ khán giả. Mỗi
    /// texture thêm vào là một lần đổi trạng thái vật liệu, tức là thêm một draw call — nên
    /// sự đa dạng KHÔNG đến từ nhiều texture mà từ:
    ///   1. màu áo đổi theo từng instance (<see cref="CrowdPalette"/>), nhân vào trong shader;
    ///   2. pha animation lệch nhau (<see cref="CrowdInstance.phase01"/>);
    ///   3. tỉ lệ cao thấp lệch nhau (<see cref="CrowdInstance.scale"/>).
    ///
    /// Có <see cref="Padding"/> quanh mỗi ô: atlas có mipmap, không chừa viền thì ở mip cao
    /// hai ô cạnh nhau rỉ màu sang nhau và khán giả hàng sau viền màu áo của hàng trước.
    /// </summary>
    public static class CrowdAtlas
    {
        public const int Columns = 8;
        public const int Rows = 4;
        public const int CellCount = Columns * Rows;

        /// <summary>Số khung hình của mỗi trạng thái = số cột.</summary>
        public const int FramesPerMood = Columns;

        /// <summary>Kích thước cạnh atlas (điểm ảnh) — mỗi ô 256×256.</summary>
        public const int AtlasSize = 2048;
        public const int CellPixels = AtlasSize / Columns;   // 256

        /// <summary>Viền an toàn mỗi cạnh ô, tính theo toạ độ UV chuẩn hoá (4 điểm ảnh).</summary>
        public const float Padding = 4.0f / AtlasSize;

        /// <summary>
        /// Trả về hình chữ nhật UV của một ô: (uMin, vMin, uSize, vSize).
        /// Chỉ số ngoài dải bị bọc vòng chứ không ném exception — hàm này chạy mỗi khung hình
        /// cho hàng nghìn instance, không được phép có nhánh ném lỗi.
        /// </summary>
        public static float4 GetCellUv(CrowdMood mood, int frame)
        {
            int row = (int)mood;
            row = row - (row / Rows) * Rows;
            if (row < 0) row += Rows;

            int col = frame - (frame / Columns) * Columns;
            if (col < 0) col += Columns;

            float cellU = 1.0f / Columns;
            float cellV = 1.0f / Rows;

            return new float4(
                col * cellU + Padding,
                row * cellV + Padding,
                cellU - 2.0f * Padding,
                cellV - 2.0f * Padding);
        }

        /// <summary>Tổng số ô đang thật sự được dùng (4 trạng thái × 8 khung).</summary>
        public static int UsedCellCount => Rows * FramesPerMood;
    }

    /// <summary>
    /// Bảng màu áo khán giả. Tám màu, chọn theo băm chỉ số instance — đủ để khán đài không
    /// đơn sắc mà vẫn chỉ một texture. Giá trị là màu tuyến tính (linear), KHÔNG phải sRGB:
    /// shader nhân thẳng vào albedo đã giải mã, nhân màu sRGB vào đó sẽ ra khán đài chói gắt.
    /// </summary>
    public static class CrowdPalette
    {
        public const int ColorCount = 8;

        public static float3 GetColor(int index)
        {
            int i = index - (index / ColorCount) * ColorCount;
            if (i < 0) i += ColorCount;

            switch (i)
            {
                case 0: return new float3(0.72f, 0.14f, 0.14f);  // đỏ sân nhà
                case 1: return new float3(0.10f, 0.18f, 0.55f);  // xanh dương
                case 2: return new float3(0.88f, 0.86f, 0.80f);  // trắng ngà
                case 3: return new float3(0.16f, 0.16f, 0.18f);  // đen xám
                case 4: return new float3(0.82f, 0.62f, 0.10f);  // vàng
                case 5: return new float3(0.12f, 0.40f, 0.22f);  // xanh lá
                case 6: return new float3(0.45f, 0.30f, 0.22f);  // nâu áo khoác
                default: return new float3(0.55f, 0.55f, 0.58f); // ghi
            }
        }
    }
}
