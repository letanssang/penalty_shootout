using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Eleven.Editor.Tools
{
    /// <summary>
    /// Đo hai thứ mà bảng MixamoAnimationReport không trả lời được, nhưng lại quyết định
    /// một clip có dùng làm vòng lặp màn hình chủ được hay không:
    ///
    ///   1. ĐƯỜNG NỐI VÒNG — khoảng lệch tư thế giữa khung đầu và khung cuối. Clip Mixamo
    ///      không được làm để loop; nếu khung cuối lệch khung đầu vài chục cm thì bật
    ///      loopTime lên chỉ tạo ra cú giật mỗi chu kỳ.
    ///   2. THỜI ĐIỂM CHẠM BÓNG — Mixamo KHÔNG kèm quả bóng, "soccerball" chỉ tả động tác.
    ///      Muốn dựng cảnh tâng bóng thì phải tự animate bóng, và việc đó cần biết bàn chân /
    ///      đầu gối / trán lên cao nhất ở giây thứ mấy. Đây chính là danh sách keyframe của bóng.
    ///
    /// Quét TOÀN BỘ cây Mixamo/ chứ không chỉ Menu/: cột seam chỉ đọc được khi có mẫu đối chứng.
    /// Clip một-lần như StrikeForwardJog phải cho seam lớn; nếu nó cũng ra 0 thì công cụ hỏng.
    /// Chạy: menu Eleven > Art > Probe Menu Clips. Kết quả ghi docs/data/menu-clip-probe.tsv.
    /// </summary>
    public static class MenuClipProbe
    {
        const string ScanDir = MixamoModelImport.Root;
        const string OutputPath = "docs/data/menu-clip-probe.tsv";

        static readonly HumanBodyBones[] Tracked =
        {
            HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot,
            HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg,
            HumanBodyBones.Head, HumanBodyBones.Hips,
        };

        [MenuItem("Eleven/Art/Probe Menu Clips")]
        public static void Run()
        {
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(MixamoModelImport.AvatarSourcePath);
            if (rig == null)
            {
                Debug.LogError($"[MenuClipProbe] Không nạp được nhân vật gốc: {MixamoModelImport.AvatarSourcePath}");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(rig);
            var sb = new StringBuilder();
            try
            {
                var animator = instance.GetComponent<Animator>();
                if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                {
                    Debug.LogError("[MenuClipProbe] Nhân vật gốc không có Avatar Humanoid.");
                    return;
                }

                var bones = Tracked.ToDictionary(b => b, b => animator.GetBoneTransform(b));
                if (bones.Values.Any(t => t == null))
                {
                    Debug.LogError("[MenuClipProbe] Thiếu xương trên Avatar — không đo được.");
                    return;
                }

                sb.AppendLine("file\tseconds\tseam_max_cm\tseam_bone\tfoot_peak_cm\tfoot_t_s\tfoot_norm\tknee_peak_cm\tknee_t_s\tknee_norm");

                var paths = AssetDatabase.FindAssets("t:Model", new[] { ScanDir.TrimEnd('/') })
                                         .Select(AssetDatabase.GUIDToAssetPath)
                                         .Where(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                                         .OrderBy(p => p, StringComparer.Ordinal);

                foreach (var path in paths)
                {
                    var clip = AssetDatabase.LoadAllAssetsAtPath(path)
                                            .OfType<AnimationClip>()
                                            .FirstOrDefault(c => (c.hideFlags & HideFlags.HideInHierarchy) == 0);
                    if (clip == null) continue;

                    // Khung đầu và khung cuối.
                    clip.SampleAnimation(instance, 0f);
                    var first = bones.ToDictionary(kv => kv.Key, kv => LocalToHips(kv.Value, bones));
                    clip.SampleAnimation(instance, clip.length);
                    var last = bones.ToDictionary(kv => kv.Key, kv => LocalToHips(kv.Value, bones));

                    var seamBone = Tracked[0];
                    var seamMax = 0f;
                    foreach (var b in Tracked)
                    {
                        if (b == HumanBodyBones.Hips) continue;   // gốc quy chiếu, luôn bằng 0
                        var d = Vector3.Distance(first[b], last[b]);
                        if (d > seamMax) { seamMax = d; seamBone = b; }
                    }

                    // Bàn chân và đầu gối lên cao nhất ở giây thứ mấy — đó là khung chạm bóng,
                    // tức là keyframe mà quả bóng (Mixamo không kèm) phải có mặt ở đó.
                    // KHÔNG đưa Head vào cuộc so sánh: so với hông thì đầu luôn cao nhất,
                    // để chung thì mọi clip đều ra "Head, 55cm" — một con số không nói gì.
                    var step = 1f / Mathf.Max(clip.frameRate, 1f);
                    var footTime = 0f; var footHeight = float.NegativeInfinity;
                    var kneeTime = 0f; var kneeHeight = float.NegativeInfinity;
                    for (var t = 0f; t <= clip.length; t += step)
                    {
                        clip.SampleAnimation(instance, t);
                        foreach (var b in new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot })
                        {
                            var y = LocalToHips(bones[b], bones).y;
                            if (y > footHeight) { footHeight = y; footTime = t; }
                        }
                        foreach (var b in new[] { HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg })
                        {
                            var y = LocalToHips(bones[b], bones).y;
                            if (y > kneeHeight) { kneeHeight = y; kneeTime = t; }
                        }
                    }

                    sb.AppendLine(string.Join("\t",
                        Path.GetFileName(path),
                        clip.length.ToString("F3"),
                        (seamMax * 100f).ToString("F1"),
                        seamBone,
                        (footHeight * 100f).ToString("F1"),
                        footTime.ToString("F3"),
                        (footTime / clip.length).ToString("F3"),
                        (kneeHeight * 100f).ToString("F1"),
                        kneeTime.ToString("F3"),
                        (kneeTime / clip.length).ToString("F3")));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath)!);
            File.WriteAllText(OutputPath, sb.ToString());
            Debug.Log($"[MenuClipProbe] → {OutputPath}\n{sb}");
        }

        /// <summary>
        /// Toạ độ xương quy về gốc hông, để phép đo không bị nhiễu bởi vị trí nhân vật
        /// trong thế giới (clip nào cũng có chút root motion còn sót).
        /// </summary>
        static Vector3 LocalToHips(Transform bone, System.Collections.Generic.Dictionary<HumanBodyBones, Transform> bones)
            => bone.position - bones[HumanBodyBones.Hips].position;
    }
}
