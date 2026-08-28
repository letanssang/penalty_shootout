using System;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Eleven.Editor.Tools
{
    /// <summary>
    /// Đo MỌI model nhân vật trong <c>Art/Characters</c> ngay sau khi Unity import.
    ///
    /// Cùng lý do tồn tại như <see cref="PropModelReport"/>: kích thước và trạng thái rig thật
    /// chỉ biết được sau khi Unity đã áp UnitScaleFactor, xoay trục và dựng Avatar — đọc byte
    /// trong FBX không cho ra đúng. Khác ở chỗ nhân vật có thêm hai câu hỏi sống còn mà đạo cụ
    /// không có:
    ///
    ///  • <b>Avatar có phải Humanoid hợp lệ không.</b> Không thì mọi clip Mixamo retarget hỏng
    ///    và nhân vật đứng T-pose suốt trận — Unity chỉ ghi một dòng cảnh báo lúc import rồi
    ///    thôi.
    ///  • <b>Bốn xương mà code thật sự đọc có ánh xạ được không</b> (Hips / LeftFoot /
    ///    RightFoot / Head). <c>MecanimKickerAnimator</c> gọi <c>GetBoneTransform</c> cho đúng
    ///    bốn xương này; thiếu một cái là <c>KickerBoneCueSource</c> mất tín hiệu và thủ môn
    ///    đứng đoán mò cả trận.
    ///
    /// Chạy: menu Eleven > Art > Đo Model Nhân Vật, hoặc batch:
    ///   Unity -batchmode -quit -projectPath . -executeMethod Eleven.Editor.Tools.CharacterModelReport.Run
    /// </summary>
    public static class CharacterModelReport
    {
        const string Dir = "Assets/_Project/Art/Characters";

        /// <summary>Bốn xương code thật sự đọc — xem <c>MecanimKickerAnimator.CacheBones</c>.</summary>
        static readonly HumanBodyBones[] Required =
        {
            HumanBodyBones.Hips, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.Head,
        };

        [MenuItem("Eleven/Art/Đo Model Nhân Vật")]
        public static void Run()
        {
            var sb = new StringBuilder();

            var paths = AssetDatabase.FindAssets("t:Model", new[] { Dir })
                                     .Select(AssetDatabase.GUIDToAssetPath)
                                     .Distinct()
                                     .OrderBy(p => p, StringComparer.Ordinal);

            foreach (string path in paths) Report(sb, path);

            Debug.Log(sb.ToString());
            Console.WriteLine(sb.ToString());
        }

        static void Report(StringBuilder sb, string path)
        {
            sb.AppendLine("══ " + path);

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (fbx == null) { sb.AppendLine("   KHÔNG NẠP ĐƯỢC"); return; }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            try
            {
                go.transform.position = Vector3.zero;
                go.transform.rotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;

                // ── Hình học ────────────────────────────────────────────────────────────
                var renderers = go.GetComponentsInChildren<Renderer>(true);
                var bounds = new Bounds(Vector3.zero, Vector3.zero);
                bool first = true;
                int tris = 0, subMeshes = 0;

                foreach (var r in renderers)
                {
                    if (first) { bounds = r.bounds; first = false; }
                    else bounds.Encapsulate(r.bounds);

                    Mesh m = r is SkinnedMeshRenderer smr ? smr.sharedMesh
                           : r.GetComponent<MeshFilter>()?.sharedMesh;
                    if (m != null) { tris += m.triangles.Length / 3; subMeshes += m.subMeshCount; }

                    subMeshes += 0;
                }

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "   renderer {0} | tam giác {1} | cao {2:F3}m | hộp bao ({3:F3}, {4:F3}, {5:F3})m",
                    renderers.Length, tris, bounds.size.y,
                    bounds.size.x, bounds.size.y, bounds.size.z));

                // ── Vật liệu và texture ────────────────────────────────────────────────
                foreach (var r in renderers)
                {
                    foreach (var mat in r.sharedMaterials)
                    {
                        if (mat == null) { sb.AppendLine("   vật liệu: (rỗng)"); continue; }
                        var tex = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap")
                                : mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                        sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                            "   vật liệu: {0} | shader {1} | texture {2}",
                            mat.name, mat.shader != null ? mat.shader.name : "(rỗng)",
                            tex != null ? $"{tex.name} {((Texture2D)tex)?.width}x{((Texture2D)tex)?.height}" : "(không)"));
                    }
                }

                // ── Rig ────────────────────────────────────────────────────────────────
                var animator = go.GetComponent<Animator>();
                var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(path);

                sb.AppendLine(string.Format(
                    "   Avatar: {0} | hợp lệ {1} | Humanoid {2}",
                    avatar != null ? avatar.name : "(KHÔNG CÓ)",
                    avatar != null && avatar.isValid, avatar != null && avatar.isHuman));

                int bones = go.GetComponentsInChildren<Transform>(true).Length;
                sb.AppendLine($"   node trong hierarchy: {bones}");

                if (animator != null && avatar != null && avatar.isHuman)
                {
                    animator.avatar = avatar;
                    foreach (var b in Required)
                    {
                        Transform t = animator.GetBoneTransform(b);
                        sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                            "   xương {0}: {1}{2}", b,
                            t != null ? t.name : "THIẾU",
                            t != null ? $" @ y={t.position.y:F3}" : ""));
                    }
                }
                else
                {
                    sb.AppendLine("   BỎ QUA kiểm xương: Avatar không phải Humanoid hợp lệ.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
