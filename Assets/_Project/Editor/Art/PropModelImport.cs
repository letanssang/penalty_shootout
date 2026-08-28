using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Eleven.Editor.Tools
{
    /// <summary>
    /// Áp thiết lập import cố định cho hai model đạo cụ tải về: quả bóng và khung thành.
    /// Cùng lý do tồn tại như <see cref="MixamoModelImport"/> — mấy con số này quyết định
    /// đúng-sai chứ không phải thẩm mỹ, và chúng nằm trong file .meta rất dễ bị bấm nhầm.
    ///
    /// HAI VIỆC KHÔNG ĐƯỢC BỎ:
    ///
    ///  • <b>Tỉ lệ quả bóng.</b> FBX gốc dựng bằng đơn vị inch của 3ds Max: Unity nhập vào
    ///    được quả cầu đường kính 2.841m. Bóng thi đấu số 5 là 0.22m — đúng hai lần
    ///    <c>BallDriver</c> đang dùng. Thu nhỏ ngay lúc import chứ không phải bằng
    ///    <c>localScale</c> ở scene, để mesh nằm sẵn trong bộ nhớ ở đúng cỡ thật và
    ///    <c>TrailRenderer</c> gắn cùng gốc không bị nhân theo.
    ///
    ///  • <b>Vứt cái lưới của model khung thành.</b> Lưới đó dựng bằng hình học dây thật:
    ///    221.820 tam giác, tức 99,5% toàn bộ model. Trên máy Android bậc C nguyên cái đó
    ///    một mình đã ăn hết ngân sách vẽ của cả khung hình, mà đổi lại vẫn là một tấm lưới
    ///    CỨNG ĐỜ — không phồng lên khi bóng găm vào. Dự án đã có lưới Verlet 287 hạt
    ///    (<c>NetSimulator</c>, T28) làm đúng việc phồng đó. Nên chỗ này giữ khung, bỏ lưới.
    ///    Xoá hẳn trong <see cref="OnPostprocessModel"/> chứ không chỉ tắt renderer: tắt thì
    ///    mesh vẫn nằm trong asset và vẫn ngốn dung lượng gói lẫn bộ nhớ lúc nạp.
    /// </summary>
    public sealed class PropModelImport : AssetPostprocessor
    {
        public const string Root = "Assets/_Project/Art/Models/";
        public const string BallFbx = Root + "Ball/Football.fbx";
        public const string GoalFbx = Root + "Goal/FootballGoal.fbx";

        /// <summary>
        /// Những node bị xoá khỏi FBX khung thành lúc import.
        ///   NGon002  — tấm lưới dựng bằng dây thật, 221.820 tam giác (lý do: xem trên).
        ///   Shape001 — mảng nền phẳng 7.38 x 1.53m dày 6cm nằm ngay dưới chân khung. Nó là
        ///              mặt đất của cảnh render gốc; ở đây đã có mặt cỏ thật tại y=0, để lại
        ///              thì hai mặt phẳng chồng nhau và nhấp nháy z-fighting.
        ///   Cylinder002, Line002
        ///            — cột chống hậu GIỮA và sợi dây néo của nó. Bỏ theo yêu cầu 2026-08-28:
        ///              chỉ giữ hai cột chống hai bên. Nó đứng ngay sau lưng thủ môn, giữa
        ///              đúng vùng mắt người chơi nhìn suốt cả lượt sút, nên có nó thì khung
        ///              hình rối chứ không "thật" hơn. Khung thành thi đấu thật cũng có loại
        ///              chỉ chống hai bên.
        /// </summary>
        public static readonly string[] GoalStrippedNodes =
            { "NGon002", "Shape001", "Cylinder002", "Line002" };

        /// <summary>Đường kính quả cầu khi nhập với globalScale = 1, đo bằng PropModelReport.</summary>
        const float BallImportedDiameter = 2.841f;

        /// <summary>Đường kính bóng thi đấu số 5 (m) — khớp <c>BallRadius</c> của vòng lặp trận.</summary>
        public const float BallDiameter = 0.22f;

        public const float BallImportScale = BallDiameter / BallImportedDiameter;

        /// <summary>
        /// Tăng số này mỗi lần sửa luật import ở dưới. Unity dùng nó để biết phải nhập lại
        /// các asset đã nhập theo luật cũ — không có nó thì sửa code xong asset vẫn giữ
        /// thiết lập cũ cho tới khi có người bấm Reimport bằng tay.
        /// </summary>
        public override uint GetVersion() => 5;

        // ── Model ──────────────────────────────────────────────────────────────────

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root, StringComparison.Ordinal)) return;

            var mi = (ModelImporter)assetImporter;

            // Vật liệu do PropModelLibrary dựng bằng URP Lit. Để importer tự sinh thì nó đẻ ra
            // material Standard của pipeline cũ, hiện màu hồng, rồi lần reimport sau lại đè.
            mi.materialImportMode = ModelImporterMaterialImportMode.None;

            mi.importCameras = false;
            mi.importLights = false;
            mi.importAnimation = false;
            mi.importBlendShapes = false;
            mi.importVisibility = false;
            mi.isReadable = false;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.CalculateMikk;   // cần cho normal map
            mi.meshOptimizationFlags = MeshOptimizationFlags.Everything;
            mi.generateSecondaryUV = false;                            // không bake lightmap

            if (assetPath == BallFbx)
            {
                mi.useFileScale = true;
                mi.globalScale = BallImportScale;
            }
            else if (assetPath == GoalFbx)
            {
                // Model đã dựng đúng mét. Phần tinh chỉnh cho khớp GoalFrame là tỉ lệ lệch
                // trục, nên nó nằm ở transform trong scene chứ không nhét được vào đây.
                mi.useFileScale = true;
                mi.globalScale = 1f;
            }
        }

        /// <summary>
        /// Hai cột hậu ngoài (model x = -4.205 và +4.008) không lệch nhau mà lệch CHUNG:
        /// tâm cặp cột nằm ở x = -0.0988 thay vì 0. Ban đầu tôi định bỏ qua — 0.1m ở rìa
        /// khung thành, cách máy 23m, chỉ chừng một phần tư độ cung. Nhưng khung hình render
        /// góc sau lưng người sút cho thấy hậu quả không phải là "lệch 0.1m": cột hậu TRÁI
        /// tình cờ nấp gọn sau cột chính trái, còn cột hậu PHẢI tách ra thành vạch thứ hai.
        /// Mắt không đo được 0.1m nhưng nhận ra ngay trái một vạch phải hai vạch.
        ///
        /// Dịch cả cụm (hai cột + hai dây néo đi kèm) đi đúng một lượng nên khớp nối
        /// dây-với-cột giữ nguyên. Đầu còn lại của dây néo là đầu tự do lơ lửng ở
        /// z ~ 12.03 — chỗ mép sau nóc lưới Verlet, không dính vào khung — nên dịch nó
        /// không hở ra chỗ nào.
        /// </summary>
        const float OuterBackOffsetX = 0.0988f;

        void OnPostprocessModel(GameObject root)
        {
            if (assetPath != GoalFbx) return;

            foreach (string nodeName in GoalStrippedNodes)
            {
                Transform node = FindChild(root.transform, nodeName);
                if (node == null)
                {
                    Debug.LogWarning($"[PropModelImport] {GoalFbx}: không thấy node '{nodeName}' để xoá — " +
                                     "model có thể đã đổi. Kiểm lại bằng Eleven > Art > Đo Model Đạo Cụ.", root);
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(node.gameObject);
            }

            Shift(root.transform, OuterBackOffsetX, "Cylinder001", "Cylinder003", "Line001", "Line003");
        }

        static void Shift(Transform root, float offsetX, params string[] nodeNames)
        {
            foreach (string nodeName in nodeNames)
            {
                Transform node = FindChild(root, nodeName);
                if (node == null) continue;
                node.localPosition += new Vector3(offsetX, 0f, 0f);
            }
        }

        static Transform FindChild(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform hit = FindChild(parent.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }

        // ── Texture ────────────────────────────────────────────────────────────────

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root, StringComparison.Ordinal)) return;

            var ti = (TextureImporter)assetImporter;
            string file = Path.GetFileName(assetPath);

            ti.mipmapEnabled = true;
            ti.wrapMode = TextureWrapMode.Clamp;

            switch (file)
            {
                // Bản gốc 2048² là cỡ để render tĩnh. Quả bóng chiếm nhiều nhất chừng một
                // phần tư chiều cao màn hình điện thoại, 1024 đã dư chi tiết.
                case "football_Base_Color.png":
                    ti.textureType = TextureImporterType.Default;
                    ti.sRGBTexture = true;
                    ti.maxTextureSize = 1024;
                    break;

                case "football_Normal_OpenGL.png":
                    ti.textureType = TextureImporterType.NormalMap;
                    ti.maxTextureSize = 1024;
                    break;

                // Bóng đổ ở đường chỉ khâu là chi tiết tần số thấp — 512 không nhìn ra khác.
                case "football_Mixed_AO.png":
                    ti.textureType = TextureImporterType.Default;
                    ti.sRGBTexture = false;
                    ti.maxTextureSize = 512;
                    break;
            }
        }
    }
}
