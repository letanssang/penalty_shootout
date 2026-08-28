using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Eleven.Editor.Tools
{
    /// <summary>
    /// Rút texture NHÚNG TRONG FBX ra thành file rời dưới <c>Art/Characters/Textures</c>.
    ///
    /// Vì sao phải có bước này: Mixamo xuất bản "nonPBR" với ảnh nhúng thẳng vào FBX, và
    /// đường dẫn trong file trỏ về máy dựng của họ
    /// (<c>/home/app/mixamo-mini/tmp/skins_…fbm/</c>). Unity nhập vào thì dựng được vật liệu
    /// nhưng KHÔNG móc được ảnh — model ra màn hình trắng trơn, mà log không báo gì.
    ///
    /// Rút ra file rời còn được hai thứ nữa: luật import ở
    /// <see cref="MixamoModelImport.OnPreprocessTexture"/> mới với tới được (ép về 1024, nén,
    /// nhận diện normal map), và ảnh nằm trong version control dưới dạng file xem được thay vì
    /// chôn trong 52MB nhị phân.
    ///
    /// Chạy: menu Eleven > Art > Rút Texture Nhúng Của Nhân Vật.
    /// </summary>
    public static class CharacterTextureExtract
    {
        const string Dir = "Assets/_Project/Art/Characters";
        const string TextureDir = Dir + "/Textures";

        [MenuItem("Eleven/Art/Rút Texture Nhúng Của Nhân Vật")]
        public static void Run()
        {
            if (!Directory.Exists(TextureDir)) Directory.CreateDirectory(TextureDir);

            foreach (string path in Directory.GetFiles(Dir, "*.fbx", SearchOption.TopDirectoryOnly))
            {
                string assetPath = path.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (importer == null) continue;

                bool any = importer.ExtractTextures(TextureDir);
                Debug.Log($"[CharacterTextureExtract] {assetPath}: {(any ? "đã rút texture" : "không có texture nhúng")}");
            }

            AssetDatabase.Refresh();
            Console.WriteLine("[CharacterTextureExtract] xong");
        }
    }
}
