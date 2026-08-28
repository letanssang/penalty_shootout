using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Eleven.Editor.Tools
{
    /// <summary>
    /// Import lại toàn bộ FBX Mixamo theo đúng thứ tự (Avatar gốc trước, phần còn lại sau)
    /// rồi in ra bảng số đo của từng clip: độ dài, fps, có root motion hay không, quãng dịch
    /// chuyển của root, và Avatar có hợp lệ không.
    ///
    /// Đây là bước biến câu hỏi "clip này dùng được không" thành số đọc được, thay vì mở
    /// Unity ra nhìn bằng mắt. Chạy: menu Eleven > Art > Report Mixamo Clips, hoặc batch:
    ///   Unity -batchmode -quit -projectPath . -executeMethod Eleven.Editor.Tools.MixamoAnimationReport.Run
    /// Kết quả ghi ra docs/data/mixamo-clip-report.tsv.
    /// </summary>
    public static class MixamoAnimationReport
    {
        const string OutputPath = "docs/data/mixamo-clip-report.tsv";

        [MenuItem("Eleven/Art/Report Mixamo Clips")]
        public static void Run()
        {
            var paths = AssetDatabase.FindAssets("t:Model", new[] { MixamoModelImport.Root.TrimEnd('/') })
                                     .Select(AssetDatabase.GUIDToAssetPath)
                                     .Where(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                                     .OrderBy(p => p, StringComparer.Ordinal)
                                     .ToList();

            // Avatar gốc phải tồn tại trước khi các clip khác CopyFromOther được nó.
            var source = MixamoModelImport.AvatarSourcePath;
            AssetDatabase.ImportAsset(source, ImportAssetOptions.ForceUpdate);
            foreach (var p in paths)
                if (p != source) AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);

            var sb = new StringBuilder();
            sb.AppendLine($"# Avatar source: {source}");
            sb.AppendLine("file\tclip\tseconds\tfps\tframes\thuman\trootCurves\tloop\tspeed_m_s\tdisplacement_m\tavatar");

            foreach (var path in paths)
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(path);

                // File dùng CopyFromOther KHÔNG sinh sub-asset Avatar của riêng nó — đó là
                // dấu hiệu thiết lập đã đúng, không phải lỗi. Ghi rõ để người đọc bảng này
                // khỏi tưởng 18/19 clip hỏng avatar.
                var avatarState = avatar != null
                    ? (avatar.isValid && avatar.isHuman ? "own:humanoid" : "own:INVALID")
                    : (importer != null && importer.avatarSetup == ModelImporterAvatarSetup.CopyFromOther
                        ? "copied"
                        : "THIEU");
                var clips = AssetDatabase.LoadAllAssetsAtPath(path)
                                         .OfType<AnimationClip>()
                                         .Where(c => (c.hideFlags & HideFlags.HideInHierarchy) == 0)
                                         .ToList();

                if (clips.Count == 0)
                {
                    sb.AppendLine($"{Path.GetFileName(path)}\t<KHONG CO CLIP>");
                    continue;
                }

                foreach (var clip in clips)
                {
                    var speed = clip.averageSpeed.magnitude;
                    sb.AppendLine(string.Join("\t",
                        Path.GetFileName(path),
                        clip.name,
                        clip.length.ToString("F3"),
                        clip.frameRate.ToString("F0"),
                        Mathf.RoundToInt(clip.length * clip.frameRate).ToString(),
                        clip.humanMotion,
                        clip.hasRootCurves,
                        clip.isLooping,
                        speed.ToString("F3"),
                        (speed * clip.length).ToString("F3"),
                        avatarState));
                }
            }

            sb.AppendLine();
            sb.AppendLine(DescribeSkeleton(source));

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath)!);
            File.WriteAllText(OutputPath, sb.ToString());
            Debug.Log($"[MixamoAnimationReport] {paths.Count} file → {OutputPath}\n{sb}");
        }

        /// <summary>
        /// Kiểm tra đúng ba xương mà <c>KickerBoneCueSource</c> đọc mỗi khung hình có lấy được
        /// qua Avatar không. Lấy được ở đây nghĩa là sau này gán bằng
        /// <c>animator.GetBoneTransform(HumanBodyBones.…)</c> chứ không phải dò tên xương.
        /// </summary>
        static string DescribeSkeleton(string modelPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (prefab == null) return "# Khong doc duoc model goc.";

            var sb = new StringBuilder();
            var all = prefab.GetComponentsInChildren<Transform>(true);
            sb.AppendLine($"# Skeleton ({Path.GetFileName(modelPath)}): {all.Length} transform");

            var skins = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var verts = skins.Sum(r => r.sharedMesh != null ? r.sharedMesh.vertexCount : 0);
            sb.AppendLine(skins.Length > 0
                ? $"# Skin\t{skins.Length} SkinnedMeshRenderer, {verts} đỉnh — có mesh"
                : "# Skin\tKHONG CO MESH — file nay chi la bo xuong");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                var animator = instance.GetComponent<Animator>();
                if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                {
                    sb.AppendLine("# CANH BAO: Avatar khong phai Humanoid — retarget se khong chay.");
                }
                else
                {
                    foreach (var bone in new[]
                             {
                                 HumanBodyBones.Hips, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot,
                                 HumanBodyBones.LeftToes, HumanBodyBones.RightToes,
                                 HumanBodyBones.LeftHand, HumanBodyBones.RightHand,
                                 HumanBodyBones.Head, HumanBodyBones.Spine
                             })
                    {
                        var t = animator.GetBoneTransform(bone);
                        sb.AppendLine($"# {bone}\t{(t != null ? t.name : "<NULL>")}");
                    }

                    sb.AppendLine(MeasureArmPose(animator));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Đo góc chúc xuống của cánh tay để biết file đang ở T-pose hay A-pose. Không phải
        /// bắt bẻ hình thức: Unity suy ánh xạ xương Humanoid từ chính tư thế này, T-pose thì
        /// auto-map đúng ngay, A-pose thì hay phải Enforce T-Pose và sửa tay vài xương.
        /// </summary>
        static string MeasureArmPose(Animator animator)
        {
            var upper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var hand  = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if (upper == null || hand == null) return "# Pose\tkhong do duoc (thieu xuong tay)";

            var dir = hand.position - upper.position;
            if (dir.sqrMagnitude < 1e-6f) return "# Pose\tkhong do duoc (tay dai 0)";

            // Góc so với mặt phẳng ngang: 0° = tay duỗi ngang = T-pose.
            var droop = -Mathf.Asin(Mathf.Clamp(dir.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            var verdict = Mathf.Abs(droop) <= 15f ? "T-pose"
                        : droop > 15f ? "A-pose (tay chuc xuong)"
                        : "tay giơ lên";
            return $"# Pose\ttay chuc {droop:F1}° so voi phuong ngang → {verdict}";
        }
    }
}
