using Unity.Mathematics;

namespace Eleven.Presentation.Skin
{
    /// <summary>
    /// LUT tán xạ tính trước (pre-integrated SSS, Penner).
    ///
    /// Ý tưởng: thay vì mô phỏng ánh sáng lan trong da lúc chạy, tích phân TRƯỚC kết quả cho mọi
    /// cặp (góc chiếu, độ cong bề mặt) rồi nhét vào một texture 128×32. Lúc chạy, shader chỉ làm
    /// đúng một việc: lấy mẫu texture đó. Một lần fetch, không pass nào thêm.
    ///
    /// Trục U = NdotL ánh xạ từ [-1, 1] về [0, 1]. Phải dùng cả nửa ÂM: toàn bộ hiệu ứng nằm ở
    /// vùng NdotL hơi nhỏ hơn 0 — chỗ Lambert đã tắt hẳn mà da vẫn còn ửng đỏ.
    /// Trục V = độ cong (1/bán kính, đơn vị 1/mm). Chỗ cong gắt (cánh mũi, vành tai) ánh sáng
    /// vòng qua được nhiều hơn hẳn chỗ phẳng (trán, má).
    ///
    /// Hàm ở đây là hàm THUẦN và TẤT ĐỊNH: cùng đầu vào cho ra cùng từng bit, nên EditMode kiểm
    /// được đúng những con số mà GPU sẽ đọc.
    /// </summary>
    public static class SkinSssLut
    {
        /// <summary>Số cột — độ phân giải theo góc chiếu.</summary>
        public const int Width = 128;

        /// <summary>Số hàng — độ phân giải theo độ cong. 32 hàng là quá đủ: độ cong đổi rất chậm trên mặt người.</summary>
        public const int Height = 32;

        /// <summary>Bán kính cong nhỏ nhất (mm) — vành tai, cánh mũi.</summary>
        public const float MinRadiusMm = 6.0f;

        /// <summary>Bán kính cong lớn nhất (mm) — trán, gò má, coi như phẳng.</summary>
        public const float MaxRadiusMm = 200.0f;

        /// <summary>Độ cong nhỏ nhất trong LUT (1/mm).</summary>
        public const float MinCurvature = 1.0f / MaxRadiusMm;

        /// <summary>Độ cong lớn nhất trong LUT (1/mm).</summary>
        public const float MaxCurvature = 1.0f / MinRadiusMm;

        /// <summary>Bước tích phân theo góc (radian). Nhỏ hơn thì chậm hơn mà kết quả không đổi thấy được.</summary>
        public const float IntegrationStepRadians = 0.02f;

        /// <summary>
        /// Tích phân độ sáng khuếch tán quanh một vòng bán kính <paramref name="radiusMm"/>.
        ///
        /// Với mỗi góc lệch <c>a</c> quanh điểm đang xét: điểm trên vòng nhận được
        /// <c>saturate(cos(theta + a))</c> ánh sáng, và phần ánh sáng đó lan tới điểm đang xét
        /// theo hồ sơ khuếch tán ở khoảng cách dây cung <c>2·r·sin(a/2)</c>.
        /// Chia cho tổng trọng số để giữ bảo toàn năng lượng: bề mặt phẳng phải ra đúng Lambert.
        /// </summary>
        public static float3 Integrate(float ndotl, float radiusMm)
        {
            float cosTheta = math.clamp(ndotl, -1.0f, 1.0f);
            float theta = math.acos(cosTheta);

            float3 totalLight = float3.zero;
            float3 totalWeight = float3.zero;

            for (float a = -math.PI * 0.5f; a <= math.PI * 0.5f; a += IntegrationStepRadians)
            {
                float diffuse = math.saturate(math.cos(theta + a));

                // Khoảng cách dây cung trên mặt cầu bán kính r, ứng với góc lệch a.
                float chordMm = math.abs(2.0f * radiusMm * math.sin(a * 0.5f));

                float3 weight = SkinDiffusionProfile.Scatter(chordMm);

                totalLight += diffuse * weight;
                totalWeight += weight;
            }

            return totalLight / math.max(totalWeight, new float3(1e-12f));
        }

        /// <summary>Bán kính cong (mm) của hàng thứ <paramref name="y"/>. Chia đều theo ĐỘ CONG, không theo bán kính.</summary>
        public static float RadiusForRow(int y)
        {
            return 1.0f / CurvatureForRow(y);
        }

        /// <summary>Độ cong (1/mm) của hàng thứ <paramref name="y"/>.</summary>
        public static float CurvatureForRow(int y)
        {
            float v = Height > 1 ? y / (float)(Height - 1) : 0.0f;
            return math.lerp(MinCurvature, MaxCurvature, v);
        }

        /// <summary>NdotL của cột thứ <paramref name="x"/>, trong [-1, 1].</summary>
        public static float NdotLForColumn(int x)
        {
            float u = Width > 1 ? x / (float)(Width - 1) : 0.0f;
            return u * 2.0f - 1.0f;
        }

        /// <summary>
        /// Toạ độ lấy mẫu LUT. PHẢI khớp từng dòng với <c>SkinSssUv</c> trong Skin.shader —
        /// lệch một phép ánh xạ là toàn bộ sắc da lệch theo mà không có lỗi nào báo ra.
        /// </summary>
        public static float2 Uv(float ndotl, float curvature)
        {
            float u = math.saturate(ndotl * 0.5f + 0.5f);
            float v = math.saturate((curvature - MinCurvature) / (MaxCurvature - MinCurvature));
            return new float2(u, v);
        }

        /// <summary>
        /// Nướng toàn bộ LUT vào mảng do người gọi cấp phát, dài <see cref="Width"/> ×
        /// <see cref="Height"/>, thứ tự hàng trước. Chạy MỘT LẦN lúc dựng asset, không bao giờ
        /// lúc chạy — nên hàm này được phép chậm, và cố tình không cấp phát gì thêm.
        /// </summary>
        public static void Bake(float3[] destination)
        {
            if (destination == null || destination.Length < Width * Height)
            {
                throw new System.ArgumentException(
                    $"Cần mảng dài ít nhất {Width * Height} phần tử để nướng LUT.", nameof(destination));
            }

            for (int y = 0; y < Height; y++)
            {
                float radiusMm = RadiusForRow(y);

                for (int x = 0; x < Width; x++)
                {
                    destination[y * Width + x] = Integrate(NdotLForColumn(x), radiusMm);
                }
            }
        }

        /// <summary>
        /// Bản byte RGB24 để ghi thẳng thành texture. 128 × 32 × 3 = 12 KB — nhỏ hơn ngân sách
        /// texture của bậc C hàng nghìn lần, nên LUT này không bao giờ là thứ phải cắt.
        /// </summary>
        public const int TextureBytes = Width * Height * 3;

        /// <summary>Đóng gói một ô LUT thành ba byte. Giá trị đã nằm trong [0,1] nên không cần tonemap.</summary>
        public static void EncodeRgb24(in float3 value, byte[] destination, int offset)
        {
            destination[offset + 0] = (byte)math.round(math.saturate(value.x) * 255.0f);
            destination[offset + 1] = (byte)math.round(math.saturate(value.y) * 255.0f);
            destination[offset + 2] = (byte)math.round(math.saturate(value.z) * 255.0f);
        }
    }
}
