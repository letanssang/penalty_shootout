using UnityEditor;
using UnityEngine;
using Eleven.Keeper;

namespace Eleven.Editor.Tools
{
    /// <summary>
    /// Sinh 3 asset KeeperProfile cho ba bậc độ khó (T25): menu Eleven > Phase 4 > Generate Keeper Profiles.
    ///
    /// Giá trị KHÔNG được gõ lại ở đây mà lấy thẳng từ các factory trong
    /// <see cref="KeeperProfile"/>. Nếu chép số ra hai nơi thì sớm muộn asset trên đĩa và
    /// hằng số trong code sẽ lệch nhau, và lúc đó test chạy bằng factory vẫn xanh trong khi
    /// game chạy bằng asset lại có độ khó khác — đúng kiểu lỗi không ai phát hiện ra.
    /// DifficultyTests kiểm tra chính điều này (asset phải khớp factory từng field).
    ///
    /// Chạy lại được: ghi đè asset đã có bằng giá trị chuẩn.
    /// </summary>
    public static class KeeperProfileAssetGenerator
    {
        const string SettingsDir = "Assets/_Project/Settings";

        [MenuItem("Eleven/Phase 4/Generate Keeper Profiles")]
        public static void Generate()
        {
            if (!AssetDatabase.IsValidFolder(SettingsDir))
                AssetDatabase.CreateFolder("Assets/_Project", "Settings");

            WriteProfile(DifficultyLevel.Easy, KeeperProfile.CreateEasy());
            WriteProfile(DifficultyLevel.Medium, KeeperProfile.CreateMedium());
            WriteProfile(DifficultyLevel.Hard, KeeperProfile.CreateHard());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[KeeperProfileAssetGenerator] Đã sinh 3 KeeperProfile (Easy/Medium/Hard) trong " + SettingsDir + ".");
        }

        static void WriteProfile(DifficultyLevel level, KeeperProfile source)
        {
            string path = $"{SettingsDir}/KeeperProfile-{level}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<KeeperProfile>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<KeeperProfile>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.readAccuracy = source.readAccuracy;
            asset.reactionMs = source.reactionMs;
            asset.commitOffsetMs = source.commitOffsetMs;
            asset.reachScale = source.reachScale;
            asset.parryChance = source.parryChance;
            asset.memoryWeight = source.memoryWeight;

            // CreateAsset ghi trạng thái LÚC TẠO ra đĩa; các gán ở trên chỉ nằm trong bộ nhớ.
            // Thiếu SetDirty thì SaveAssets() bỏ qua và cả ba file giữ nguyên giá trị mặc định
            // của field initializer — tức cả ba bậc đều ra thông số của bậc Thường.
            EditorUtility.SetDirty(asset);

            // source là instance tạm không nằm trên đĩa; không huỷ thì nó sống tới lần
            // domain reload kế tiếp và hiện ra trong bộ nhớ như một asset mồ côi.
            Object.DestroyImmediate(source);
        }
    }
}
