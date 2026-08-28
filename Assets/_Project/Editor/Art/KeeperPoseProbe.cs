using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Eleven.Editor.Tools
{
    /// <summary>
    /// Đo TƯ THẾ THẬT của từng clip thủ môn trên chính bộ xương của model trong game.
    ///
    /// <see cref="KeeperClipProbe"/> chỉ đọc đường cong root motion, đủ để biết clip bay về
    /// bên nào nhưng không trả lời được hai câu quyết định thiết kế:
    ///
    ///  1. <b>Clip nào bắt đầu ở tư thế NẰM?</b> Muốn thủ môn bay xong biết đứng dậy thì phải
    ///     có một clip đi từ dưới đất lên. Trong thư mục không có file nào tên kiểu "đứng dậy",
    ///     nhưng tên file không phải bằng chứng — `KeeperMiss` hay `KeeperCelebrate` hoàn toàn
    ///     có thể vốn đã bắt đầu từ dưới đất. Đo chiều cao hông ở khung đầu và khung cuối thì
    ///     biết chắc, và biết luôn là có phải nhờ người tải thêm clip từ Mixamo hay không.
    ///
    ///  2. <b>Tay với xa được bao nhiêu mét?</b> <see cref="Eleven.Keeper.KeeperReach"/> đang
    ///     giả định tay tới được TÂM Ô, ô cao nhất cách chỗ đứng hơn 2 m. Nếu cánh tay thật
    ///     của rig ngắn hơn thế thì lúc ghim tay bằng IK, tay sẽ hụt trên hình trong khi máy
    ///     vẫn chấm là với tới — sai đúng chiều xấu nhất, mắt thấy hụt mà máy báo cản được.
    ///     Con số đo ở đây quyết định có phải kéo `ReachEnvelope` về khớp cơ thể thật hay không.
    ///
    /// Cách đo: dựng đúng model Goalkeeper.fbx, lấy pose từng khung bằng
    /// <c>SampleAnimation</c> rồi đọc vị trí THẾ GIỚI của xương hông, đầu và hai bàn tay.
    /// Không đoán từ tên file, không đoán từ độ dài clip.
    ///
    /// Chạy: menu Eleven > Art > Đo Tư Thế Clip Thủ Môn, hoặc batch:
    ///   Unity -batchmode -quit -projectPath . -executeMethod Eleven.Editor.Tools.KeeperPoseProbe.Run
    /// </summary>
    public static class KeeperPoseProbe
    {
        const string KeeperDir = "Assets/_Project/Art/Animations/Mixamo/Keeper";
        const string ModelPath = "Assets/_Project/Art/Characters/Goalkeeper.fbx";
        const string OutPath = "docs/data/keeper-clip-pose.tsv";

        /// <summary>Số lần lấy mẫu mỗi clip. 40 mẫu cho clip 3s là ~13 mẫu/giây — đủ để bắt
        /// đỉnh với tay mà không phải chờ lâu.</summary>
        const int Samples = 40;

        /// <summary>Dưới ngưỡng này (m) thì coi như hông đang sát đất, tức người đang NẰM.</summary>
        const float LyingHipHeight = 0.55f;

        [MenuItem("Eleven/Art/Đo Tư Thế Clip Thủ Môn")]
        public static void Run()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (fbx == null)
            {
                Debug.LogError($"[KeeperPoseProbe] Không thấy model tại {ModelPath}.");
                return;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;

            var animator = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
            animator.avatar = AssetDatabase.LoadAssetAtPath<Avatar>(ModelPath);

            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            Transform handL = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform handR = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform shoulderL = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);

            if (hips == null || head == null || handL == null || handR == null || shoulderL == null)
            {
                Debug.LogError("[KeeperPoseProbe] Avatar không trả về đủ xương — model không phải Humanoid?");
                UnityEngine.Object.DestroyImmediate(go);
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("# Đo trên " + ModelPath + ", " + Samples + " mẫu/clip, toạ độ THẾ GIỚI (m).");
            sb.AppendLine("# hongDau/hongCuoi = chiều cao xương hông ở khung đầu/cuối. Dưới "
                          + LyingHipHeight.ToString("F2", CultureInfo.InvariantCulture) + " coi như NẰM.");
            sb.AppendLine("# vuonXa = khoảng cách xa nhất từ vai tới bàn tay đạt được trong clip.");
            sb.AppendLine("# tayCaoNhat = độ cao lớn nhất một bàn tay với tới. tayXaNgang = |x| lớn nhất của bàn tay.");
            sb.AppendLine("clip\thongDau\thongCuoi\thongThapNhat\tdauDau\tdauCuoi\tvuonXa\ttayCaoNhat\ttayXaNgang\ttDinhVuon\tdang");

            var paths = AssetDatabase.FindAssets("t:Model", new[] { KeeperDir })
                                     .Select(AssetDatabase.GUIDToAssetPath)
                                     .Distinct()
                                     .OrderBy(p => p, StringComparer.Ordinal)
                                     .ToList();

            foreach (var path in paths)
            {
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (!(asset is AnimationClip clip)) continue;
                    if ((clip.hideFlags & HideFlags.HideInHierarchy) != 0) continue;

                    float hipFirst = 0f, hipLast = 0f, hipMin = float.MaxValue;
                    float headFirst = 0f, headLast = 0f;
                    float reachMax = 0f, handTop = 0f, handSide = 0f, tPeak = 0f;

                    for (int i = 0; i < Samples; i++)
                    {
                        float u = Samples == 1 ? 0f : i / (float)(Samples - 1);
                        clip.SampleAnimation(go, u * clip.length);

                        float hipY = hips.position.y;
                        if (i == 0) { hipFirst = hipY; headFirst = head.position.y; }
                        if (i == Samples - 1) { hipLast = hipY; headLast = head.position.y; }
                        if (hipY < hipMin) hipMin = hipY;

                        // Vươn xa đo TỪ VAI chứ không từ gốc: đó mới là giới hạn vật lý của
                        // cánh tay, thứ mà IK không thể kéo dài thêm.
                        float reach = Mathf.Max(
                            Vector3.Distance(shoulderL.position, handL.position),
                            Vector3.Distance(shoulderL.position, handR.position));
                        if (reach > reachMax) { reachMax = reach; tPeak = u; }

                        handTop = Mathf.Max(handTop, Mathf.Max(handL.position.y, handR.position.y));
                        handSide = Mathf.Max(handSide, Mathf.Max(Mathf.Abs(handL.position.x), Mathf.Abs(handR.position.x)));
                    }

                    string shape = Describe(hipFirst, hipLast);

                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "{0}\t{1:F2}\t{2:F2}\t{3:F2}\t{4:F2}\t{5:F2}\t{6:F2}\t{7:F2}\t{8:F2}\t{9:F2}\t{10}",
                        clip.name, hipFirst, hipLast, hipMin, headFirst, headLast,
                        reachMax, handTop, handSide, tPeak, shape));
                }
            }

            UnityEngine.Object.DestroyImmediate(go);

            Directory.CreateDirectory(Path.GetDirectoryName(OutPath));
            File.WriteAllText(OutPath, sb.ToString());

            Debug.Log(sb.ToString());
            Console.WriteLine(sb.ToString());
            Debug.Log($"[KeeperPoseProbe] Đã ghi {OutPath}.");
        }

        /// <summary>
        /// Gọi tên hình dáng clip theo hai đầu: đứng→nằm là clip đổ người, nằm→đứng chính là
        /// clip ĐỨNG DẬY đang cần tìm.
        /// </summary>
        static string Describe(float hipFirst, float hipLast)
        {
            bool startLying = hipFirst < LyingHipHeight;
            bool endLying = hipLast < LyingHipHeight;

            if (startLying && !endLying) return "NAM->DUNG (dung day)";
            if (!startLying && endLying) return "dung->nam (do nguoi)";
            if (startLying) return "nam->nam";
            return "dung->dung";
        }
    }
}
