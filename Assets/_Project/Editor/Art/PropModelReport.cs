using System;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Eleven.Editor.Tools
{
    /// <summary>
    /// Đo hai model đạo cụ (bóng, khung thành) NGAY SAU KHI Unity import, không đoán từ file
    /// FBX thô. Lý do: kích thước thật của một mesh chỉ biết được sau khi Unity đã áp
    /// UnitScaleFactor, xoay trục Z-up→Y-up và nhân transform của từng node — ba thứ mà đọc
    /// byte trong FBX không cho ra đúng.
    ///
    /// In ra: từng renderer con, số tam giác, và hộp bao trong KHÔNG GIAN THẾ GIỚI khi đặt
    /// prefab ở gốc toạ độ với scale 1. Có ba con số đó thì mới quyết được scale nào đưa
    /// model về đúng <c>GoalFrame</c>/<c>BallRadius</c>, và bộ phận nào nặng quá phải bỏ.
    ///
    /// Chạy: menu Eleven > Art > Đo Model Đạo Cụ, hoặc batch:
    ///   Unity -batchmode -quit -projectPath . -executeMethod Eleven.Editor.Tools.PropModelReport.Run
    /// </summary>
    public static class PropModelReport
    {
        const string BallPath = "Assets/_Project/Art/Models/Ball/Football.fbx";
        const string GoalPath = "Assets/_Project/Art/Models/Goal/FootballGoal.fbx";

        /// <summary>
        /// Model thủ môn. Đo cùng chỗ với bóng và khung thành vì cùng một câu hỏi: Unity nhập
        /// nó vào ra cao bao nhiêu mét thật. FBX này do Maya xuất, mà đơn vị Maya thì tuỳ file
        /// — không đo thì không biết phải co giãn bao nhiêu cho ra một người cao 1.85m.
        /// </summary>
        const string KeeperPath = "Assets/_Project/Art/Characters/Goalkeeper.fbx";

        [MenuItem("Eleven/Art/Đo Model Đạo Cụ")]
        public static void Run()
        {
            var sb = new StringBuilder();
            Report(sb, BallPath);
            Report(sb, GoalPath);
            Report(sb, KeeperPath);
            Debug.Log(sb.ToString());
            Console.WriteLine(sb.ToString());
        }

        /// <summary>
        /// Buộc nhập lại hai model rồi đo — dùng sau khi sửa <see cref="PropModelImport"/>,
        /// vì con số in ra chỉ có nghĩa khi asset trên đĩa đã theo luật import mới nhất.
        /// </summary>
        [MenuItem("Eleven/Art/Nhập Lại + Đo Model Đạo Cụ")]
        public static void ReimportAndRun()
        {
            AssetDatabase.ImportAsset(BallPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(GoalPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
            Run();
        }

        /// <summary>
        /// Đo khung thành SAU KHI đã đặt vào scene — tức đã xoay 180 độ và co giãn lệch trục.
        /// Đây mới là con số so được với <c>GoalFrame</c>: bản thân file FBX không biết gì về
        /// chỗ đứng của nó trên sân.
        /// </summary>
        [MenuItem("Eleven/Art/Đo Khung Thành Trong Scene")]
        public static void RunSceneGoal()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/Match.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            var goal = GameObject.Find("Environment/Goal");
            if (goal == null)
            {
                Debug.LogError("[PropModelReport] Không thấy Environment/Goal trong Match.unity");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("══ Khung thành trong Match.unity (toạ độ thế giới)");
            foreach (var r in goal.GetComponentsInChildren<MeshRenderer>(true))
            {
                Bounds b = r.bounds;
                sb.AppendLine($"   {r.name,-20} size={F(b.size)}  min={F(b.min)}  max={F(b.max)}  " +
                              $"mat={(r.sharedMaterial != null ? r.sharedMaterial.name : "null")}");
            }
            Debug.Log(sb.ToString());
            Console.WriteLine(sb.ToString());
        }

        static void Report(StringBuilder sb, string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                sb.AppendLine($"[PropModelReport] KHÔNG NẠP ĐƯỢC {path}");
                return;
            }

            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            sb.AppendLine($"══ {path}");
            sb.AppendLine($"   globalScale={importer?.globalScale.ToString("F4", CultureInfo.InvariantCulture)} " +
                          $"useFileScale={importer?.useFileScale} " +
                          $"materials={(importer != null ? importer.materialImportMode.ToString() : "?")}");

            // Dựng thật trong scene: bounds của Renderer chỉ đúng sau khi có transform thế giới.
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            // Lấy cả Renderer có xương: model nhân vật dùng SkinnedMeshRenderer, không phải
            // MeshRenderer — chỉ hỏi MeshRenderer thì bảng in ra rỗng và tưởng model hỏng.
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            int totalTris = 0;
            bool hasBounds = false;
            Bounds all = default;

            foreach (var r in renderers.OrderByDescending(TriCountOf))
            {
                int tris = TriCountOf(r);
                totalTris += tris;
                Bounds b = r.bounds;
                if (!hasBounds) { all = b; hasBounds = true; }
                else all.Encapsulate(b);

                var mats = string.Join(",", r.sharedMaterials.Select(m => m != null ? m.name : "null"));
                sb.AppendLine($"   {r.name,-20} tris={tris,8}  size={F(b.size)}  center={F(b.center)}  mat=[{mats}]");
            }

            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(path);
            if (avatar != null)
            {
                sb.AppendLine($"   Avatar: hợp lệ={avatar.isValid} humanoid={avatar.isHuman}");
            }

            sb.AppendLine($"   ── TỔNG tris={totalTris}  bao={F(all.size)}  tâm={F(all.center)}");
            UnityEngine.Object.DestroyImmediate(instance);
        }

        static int TriCountOf(Renderer r)
        {
            if (r is SkinnedMeshRenderer smr)
                return smr.sharedMesh != null ? smr.sharedMesh.triangles.Length / 3 : 0;

            var mf = r.GetComponent<MeshFilter>();
            var mesh = mf != null ? mf.sharedMesh : null;
            return mesh != null ? mesh.triangles.Length / 3 : 0;
        }

        static string F(Vector3 v) =>
            string.Format(CultureInfo.InvariantCulture, "({0:F3}, {1:F3}, {2:F3})", v.x, v.y, v.z);
    }
}
