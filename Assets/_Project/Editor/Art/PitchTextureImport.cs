using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Eleven.Editor.Tools
{
    /// <summary>
    /// Áp thiết lập import cho bộ texture mặt cỏ (ambientCG Grass005, bản 2K JPG).
    ///
    /// Cùng lý do tồn tại như <see cref="PropModelImport"/>: mấy con số này quyết định mặt
    /// sân trông ra sân hay ra tấm thảm, mà chúng nằm trong file .meta rất dễ bấm nhầm.
    ///
    /// BA THIẾT LẬP QUAN TRỌNG:
    ///
    ///  • <b>Repeat, không Clamp.</b> Một tấm 2m x 2m phải lát 21 lần mới phủ hết chiều
    ///    ngang 42m của mặt sân. Clamp thì rìa texture bị kéo dài thành vệt.
    ///
    ///  • <b>Lọc dị hướng.</b> Mặt sân là mặt phẳng nằm ngang mà máy quay nhìn gần như là
    ///    là — góc xiên nhất trong cả khung hình. Không có aniso thì texel xa nhoè thành
    ///    một mảng xám ngay từ khoảng giữa sân. Lấy 4 chứ không lấy 16: đây là điện thoại,
    ///    và 4 đã lấy lại gần hết phần chi tiết mà 1 làm mất.
    ///
    ///    Con số ở đây KHÔNG ĐỦ để aniso chạy. QualitySettings có cờ riêng
    ///    <c>anisotropicTextures</c> cho từng bậc chất lượng, và nó ĐÈ LÊN anisoLevel của
    ///    importer: bậc nào để 0 thì texture ở bậc đó lọc như aniso 1, dù .meta ghi 4.
    ///    Đo ngày 2026-08-28: hai bậc cao nhất của dự án đang để 0, nên mặt cỏ nhoè thành
    ///    vệt từ giữa sân trở đi trong khi .meta vẫn ghi aniso 4 — đã bật cả hai. Đổi số
    ///    dưới đây mà mặt cỏ không thấy khác thì kiểm cờ ấy trước tiên.
    ///
    ///  • <b>1024 chứ không 2048.</b> Ảnh gốc 2K là cỡ để render tĩnh cận cảnh. Ở đây mỗi ô
    ///    lát chiếm chừng một phần hai mươi chiều ngang màn hình, nên nửa số texel kia không
    ///    bao giờ hiện ra — chỉ tốn 3MB bộ nhớ mỗi bản đồ.
    /// </summary>
    public sealed class PitchTextureImport : AssetPostprocessor
    {
        public const string Root = "Assets/_Project/Art/Textures/Pitch/";

        public const string GrassColor = Root + "Grass_Color.jpg";
        public const string GrassNormal = Root + "Grass_Normal.jpg";

        /// <summary>Tăng mỗi lần sửa luật dưới đây — xem <see cref="PropModelImport.GetVersion"/>.</summary>
        public override uint GetVersion() => 1;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root, StringComparison.Ordinal)) return;

            var ti = (TextureImporter)assetImporter;

            ti.mipmapEnabled = true;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.filterMode = FilterMode.Trilinear;
            ti.anisoLevel = 4;
            ti.maxTextureSize = 1024;
            ti.textureCompression = TextureImporterCompression.Compressed;

            // Bản đồ pháp tuyến bản OpenGL (trục Y hướng lên) — đúng quy ước Unity dùng.
            // Bản DX trong gói tải về có Y ngược, dùng nhầm thì ánh sáng trên từng lá cỏ
            // hắt ngược chiều và mặt cỏ trông như bị dập nổi vào trong.
            if (Path.GetFileName(assetPath) == "Grass_Normal.jpg")
            {
                ti.textureType = TextureImporterType.NormalMap;
            }
            else
            {
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = true;
            }
        }
    }
}
