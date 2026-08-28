using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Eleven.Editor.Tools
{
    /// <summary>
    /// Tìm khung chạm bóng của các clip sút, và xác định chân nào là chân sút.
    ///
    /// Cách tìm: bóng rời chân ở lúc bàn chân đạt TỐC ĐỘ TIẾN LỚN NHẤT. Đó không phải quy ước
    /// tuỳ tiện — clip Mixamo không có bóng nên không thể đo va chạm, mà đỉnh vận tốc là mốc
    /// vật lý gần nhất với thời điểm truyền động lượng. Kết quả vẫn phải soi lại bằng ảnh
    /// (ClipFrameRenderer) trước khi đóng đinh vào ContactNormalizedTime.
    ///
    /// Chân sút = chân có tốc độ đỉnh lớn hơn. Chân trụ = chân còn lại.
    /// </summary>
    public static class StrikeContactProbe
    {
        const string OutputPath = "docs/data/strike-contact.tsv";

        static readonly string[] StrikeClips =
        {
            "PenaltyKick", "KickSoccerball_A", "KickSoccerball_B", "StrikeForwardJog",
        };

        [MenuItem("Eleven/Art/Probe Strike Contact")]
        public static void Run()
        {
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(MixamoModelImport.AvatarSourcePath);
            var actor = (GameObject)PrefabUtility.InstantiatePrefab(rig);
            var sb = new StringBuilder();
            try
            {
                var animator = actor.GetComponent<Animator>();
                var lFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                var rFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                var hips  = animator.GetBoneTransform(HumanBodyBones.Hips);

                sb.AppendLine("clip\tseconds\tkickFoot\tkickSpeed_m_s\tcontact_t_s\tcontact_norm\tframe\tankleY_cm\tplantSpeed_at_contact\tcanhbao");

                foreach (var name in StrikeClips)
                {
                    var path = AssetDatabase.FindAssets("t:Model", new[] { MixamoModelImport.Root.TrimEnd('/') })
                        .Select(AssetDatabase.GUIDToAssetPath)
                        .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == name);
                    if (path == null) { sb.AppendLine($"{name}\t<KHONG THAY>"); continue; }

                    var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                        .FirstOrDefault(c => (c.hideFlags & HideFlags.HideInHierarchy) == 0);
                    if (clip == null) { sb.AppendLine($"{name}\t<KHONG CO CLIP>"); continue; }

                    // Quét một lượt, lưu tốc độ CẢ HAI chân ở từng khung. Phải giữ cả mảng
                    // chứ không thể vừa quét vừa chốt: điều kiện "chân kia đang trụ" chỉ kiểm
                    // được khi đã biết tốc độ chân kia tại đúng khung đó.
                    var frames = Mathf.FloorToInt(clip.length * clip.frameRate) + 1;
                    var step = 1f / clip.frameRate;
                    var speedL = new float[frames];
                    var speedR = new float[frames];
                    var footYL = new float[frames];
                    var footYR = new float[frames];
                    Vector3 prevL = Vector3.zero, prevR = Vector3.zero;

                    for (int i = 0; i < frames; i++)
                    {
                        clip.SampleAnimation(actor, i * step);
                        var l = lFoot.position;
                        var r = rFoot.position;
                        var hipY = hips.position.y;
                        footYL[i] = l.y;      // ĐỘ CAO TUYỆT ĐỐI so với mặt sân, không phải so với hông:
                        footYR[i] = r.y;      // điều kiện chạm bóng là "sát đất", mà hông thì di động.
                        if (i > 0)
                        {
                            speedL[i] = (l - prevL).magnitude / step;
                            speedR[i] = (r - prevR).magnitude / step;
                        }
                        prevL = l; prevR = r;
                    }

                    // Chân sút = chân có đỉnh tốc độ lớn hơn.
                    var rightKicks = speedR.Max() >= speedL.Max();
                    var kick = rightKicks ? speedR : speedL;
                    var plant = rightKicks ? speedL : speedR;
                    var kickY = rightKicks ? footYR : footYL;

                    // Khung chạm = chân sút nhanh nhất TRONG SỐ những khung mà bàn chân còn SÁT ĐẤT.
                    //
                    // Điều kiện "chân kia đang trụ" đã thử và bỏ: khi chạy thì lúc nào cũng có
                    // đúng một chân trụ, nên nó không loại được sải chân nào. Ảnh chụp
                    // StrikeForwardJog ở mốc 0.345 cho thấy đúng lỗi đó — nhân vật đang nhấc gối
                    // giữa lúc chạy, chân cách đất gần 40 cm, mà phép đo vẫn gọi là chạm bóng.
                    //
                    // Mắt cá lúc sút bóng đặt trên sân nằm khoảng 10-20 cm; lấy ngưỡng 25 cm.
                    const float AnkleAtContact = 0.25f;   // mét
                    int best = -1;
                    for (int i = 1; i < frames; i++)
                        if (kickY[i] <= AnkleAtContact && (best < 0 || kick[i] > kick[best])) best = i;

                    var fallback = best < 0;
                    if (fallback)   // không khung nào có chân sát đất — clip này không sút bóng đặt
                        for (int i = 1; i < frames; i++)
                            if (best < 0 || kick[i] > kick[best]) best = i;

                    var tc = best * step;
                    sb.AppendLine(string.Join("\t",
                        name,
                        clip.length.ToString("F3"),
                        rightKicks ? "PHAI" : "TRAI",
                        kick[best].ToString("F2"),
                        tc.ToString("F3"),
                        (tc / clip.length).ToString("F4"),
                        best.ToString(),
                        (kickY[best] * 100f).ToString("F1"),
                        plant[best].ToString("F2"),
                        fallback ? "KHONG-CO-KHUNG-SAT-DAT" : "ok"));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(actor); }

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath)!);
            File.WriteAllText(OutputPath, sb.ToString());
            Debug.Log($"[StrikeContactProbe] → {OutputPath}\n{sb}");
        }
    }
}
