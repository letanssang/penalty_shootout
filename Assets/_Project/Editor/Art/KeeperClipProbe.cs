using System;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Eleven.Editor.Tools
{
    /// <summary>
    /// Đo XEM MỖI CLIP THỦ MÔN BAY VỀ BÊN NÀO. Bảng số đo chung
    /// (docs/data/mixamo-clip-report.tsv) chỉ ghi quãng dịch chuyển là một con số DƯƠNG, nên
    /// nhìn vào đó không biết KeeperDivingSave_A bay trái hay bay phải — đúng câu hỏi còn bỏ
    /// ngỏ ở docs/art-hoat-anh-mixamo.md dòng 259.
    ///
    /// Đoán sai chiều thì thủ môn bay ngược hướng bóng trong khi máy vẫn chấm là cản được:
    /// người chơi thấy ngay là game nói dối. Nên chỗ này đo chứ không đoán.
    ///
    /// Cách đo: đọc thẳng đường cong root motion (RootT.x) của clip Humanoid, lấy giá trị
    /// khung cuối trừ khung đầu. Dương = dịch sang +x. Trục +x của Mixamo hướng sang TRÁI
    /// của người xem khi nhân vật quay mặt về +z, nên bảng in ra cả hai cách đọc.
    ///
    /// Chạy: menu Eleven > Art > Đo Chiều Bay Thủ Môn, hoặc batch:
    ///   Unity -batchmode -quit -projectPath . -executeMethod Eleven.Editor.Tools.KeeperClipProbe.Run
    /// </summary>
    public static class KeeperClipProbe
    {
        const string KeeperDir = "Assets/_Project/Art/Animations/Mixamo/Keeper";

        [MenuItem("Eleven/Art/Đo Chiều Bay Thủ Môn")]
        public static void Run()
        {
            var paths = AssetDatabase.FindAssets("t:Model", new[] { KeeperDir })
                                     .Select(AssetDatabase.GUIDToAssetPath)
                                     .Distinct()
                                     .OrderBy(p => p, StringComparer.Ordinal)
                                     .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("clip\tdx_m\tdy_m\tdz_m\tchieu");

            foreach (var path in paths)
            {
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (!(asset is AnimationClip clip)) continue;
                    if ((clip.hideFlags & HideFlags.HideInHierarchy) != 0) continue;

                    float dx = Delta(clip, "RootT.x");
                    float dy = Delta(clip, "RootT.y");
                    float dz = Delta(clip, "RootT.z");

                    string side = Mathf.Abs(dx) < 0.15f ? "giua"
                                : dx > 0f ? "+x" : "-x";

                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "{0}\t{1:F3}\t{2:F3}\t{3:F3}\t{4}", clip.name, dx, dy, dz, side));
                }
            }

            Debug.Log(sb.ToString());
            Console.WriteLine(sb.ToString());
        }

        /// <summary>
        /// Chênh lệch giữa khung cuối và khung đầu của một đường cong root motion. Trả 0 khi
        /// clip không có đường cong đó — clip đứng yên tại chỗ thì đúng là không có.
        /// </summary>
        static float Delta(AnimationClip clip, string property)
        {
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.propertyName != property) continue;
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null || curve.length < 2) return 0f;
                return curve.keys[curve.length - 1].value - curve.keys[0].value;
            }
            return 0f;
        }
    }
}
