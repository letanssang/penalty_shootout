using System;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Eleven.Editor.Tools
{
    /// <summary>
    /// Đo THỦ MÔN ĐỔ NGƯỜI VỀ BÊN NÀO, có dấu.
    ///
    /// Vì sao cần thêm một phép đo nữa dù đã có <see cref="KeeperClipProbe"/> và bảng
    /// docs/data/keeper-clip-pose.tsv: cả hai đều không trả lời được câu này.
    /// KeeperClipProbe lấy hiệu RootT.x giữa khung cuối và khung đầu, mà mọi clip cản phá đều
    /// "đứng → đứng" (đổ người rồi tự đứng dậy tại chỗ), nên hiệu đó gần bằng không dù cú bay
    /// dài hai mét. Bảng pose thì chỉ ghi |x| lớn nhất — bỏ mất đúng cái dấu đang cần.
    ///
    /// Ở đây lấy mẫu tư thế thật rồi đọc toạ độ CÓ DẤU của hông và hai bàn tay ở khung mà
    /// hông xuống thấp nhất, tức đúng đỉnh pha đổ người.
    ///
    /// Đọc kết quả: số đo trong hệ toạ độ CỦA MODEL. Thủ môn đứng trong scene xoay 180 độ
    /// quanh trục Y (quay mặt về người sút), nên +x của model là -x của thế giới. Cột
    /// "theGioi" đã quy đổi sẵn, so thẳng được với dấu của GoalFrame.CellCenter().x.
    ///
    /// Chạy: menu Eleven > Art > Đo Bên Đổ Người Của Thủ Môn.
    /// </summary>
    public static class KeeperDiveSideProbe
    {
        const string KeeperDir = "Assets/_Project/Art/Animations/Mixamo/Keeper";
        const string ModelPath = "Assets/_Project/Art/Characters/Goalkeeper.fbx";
        const int Samples = 60;

        [MenuItem("Eleven/Art/Đo Bên Đổ Người Của Thủ Môn")]
        public static void Run()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (prefab == null) { Debug.LogError($"[KeeperDiveSideProbe] Không nạp được {ModelPath}"); return; }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;

            var animator = go.GetComponent<Animator>();
            Transform hips = animator != null ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
            Transform handL = animator != null ? animator.GetBoneTransform(HumanBodyBones.LeftHand) : null;
            Transform handR = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (hips == null) { Debug.LogError("[KeeperDiveSideProbe] Model thiếu xương Hips."); return; }

            var sb = new StringBuilder();
            sb.AppendLine("clip\thongX\thongY\ttayXaNhatX\tmodel\ttheGioi");

            var paths = AssetDatabase.FindAssets("t:Model", new[] { KeeperDir })
                                     .Select(AssetDatabase.GUIDToAssetPath)
                                     .Distinct()
                                     .OrderBy(p => p, StringComparer.Ordinal);

            foreach (var path in paths)
            {
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (!(asset is AnimationClip clip)) continue;
                    if ((clip.hideFlags & HideFlags.HideInHierarchy) != 0) continue;

                    float lowestY = float.MaxValue;
                    float hipXAtLowest = 0f;
                    float handXFarthest = 0f;

                    for (int i = 0; i < Samples; i++)
                    {
                        float t = clip.length * i / (Samples - 1f);
                        clip.SampleAnimation(go, t);

                        Vector3 h = hips.position;
                        if (h.y < lowestY) { lowestY = h.y; hipXAtLowest = h.x; }

                        foreach (Transform hand in new[] { handL, handR })
                        {
                            if (hand == null) continue;
                            if (Mathf.Abs(hand.position.x) > Mathf.Abs(handXFarthest))
                                handXFarthest = hand.position.x;
                        }
                    }

                    // Lấy hông làm chuẩn: đó là khối lượng thân người, thứ mắt nhìn thấy "đổ
                    // về bên nào". Tay chỉ dùng để đối chiếu khi hông gần như không lệch.
                    float side = Mathf.Abs(hipXAtLowest) >= 0.25f ? hipXAtLowest : handXFarthest;
                    string model = Mathf.Abs(side) < 0.25f ? "giua" : side > 0f ? "+x" : "-x";
                    string world = model == "giua" ? "giua" : model == "+x" ? "-x (col 0, trai)" : "+x (col 2, phai)";

                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "{0}\t{1:F2}\t{2:F2}\t{3:F2}\t{4}\t{5}",
                        clip.name, hipXAtLowest, lowestY, handXFarthest, model, world));
                }
            }

            UnityEngine.Object.DestroyImmediate(go);
            Debug.Log(sb.ToString());
            Console.WriteLine(sb.ToString());
        }
    }
}
