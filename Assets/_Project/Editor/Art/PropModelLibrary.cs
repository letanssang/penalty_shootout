using System.IO;
using UnityEditor;
using UnityEngine;

namespace Eleven.Editor.Tools
{
    /// <summary>
    /// Một chỗ duy nhất biết cách biến hai file FBX đạo cụ thành đồ dùng được trong scene:
    /// tỉ lệ, hướng quay, và vật liệu URP.
    ///
    /// Tách khỏi <see cref="Eleven.Editor.SceneSetup.MatchSceneGenerator"/> vì hai lý do:
    /// bộ dựng scene chỉ được phép nói "đặt khung thành ở đây", không phải nhớ hộ ba con số
    /// tỉ lệ lệch trục; và mấy con số ấy suy ra từ phép ĐO (xem <see cref="PropModelReport"/>)
    /// nên phải nằm cạnh phần giải thích cách đo.
    ///
    /// Vật liệu được tạo thành ASSET trên đĩa chứ không phải <c>new Material()</c> trong bộ
    /// nhớ như đồ greybox: chúng có texture, và material dựng lúc chạy sẽ bị nhúng thẳng vào
    /// file scene mỗi lần dựng lại — scene phình ra và không ai sửa được màu ngoài code.
    /// </summary>
    public static class PropModelLibrary
    {
        public const string BallFbx = PropModelImport.BallFbx;
        public const string GoalFbx = PropModelImport.GoalFbx;

        const string TextureDir = "Assets/_Project/Art/Models/Ball/Textures/";
        const string MaterialDir = "Assets/_Project/Art/Materials/";

        public const string BallMaterial = MaterialDir + "Football.mat";
        public const string GoalPostMaterial = MaterialDir + "GoalPost.mat";
        public const string GoalRopeMaterial = MaterialDir + "GoalRope.mat";
        public const string KeeperMaterial = MaterialDir + "Goalkeeper.mat";
        public const string PitchGrassMaterial = MaterialDir + "PitchGrass.mat";
        public const string PitchStripeMaterial = MaterialDir + "PitchGrassStripe.mat";

        /// <summary>
        /// Một ô texture cỏ phủ bao nhiêu mét sân. Ảnh gốc ambientCG Grass005 chụp một mảng
        /// cỏ 2m x 2m, nên lấy đúng 2 thì lá cỏ ra đúng cỡ lá cỏ.
        /// </summary>
        public const float GrassTileMeters = 2.0f;

        /// <summary>
        /// Cạnh mảng cỏ dựng trong scene (m) và bề dày một vệt cắt cỏ (m). Hai con số này
        /// nằm ở đây chứ không ở bộ dựng scene vì SỐ LẦN LÁT TEXTURE ĐƯỢC NƯỚNG THẲNG VÀO
        /// VẬT LIỆU: có vậy cả 13 vệt mới dùng chung đúng một material và gộp được vào một
        /// lệnh vẽ. Nếu để mỗi renderer tự chỉnh tiling bằng MaterialPropertyBlock thì SRP
        /// Batcher gãy, 13 vệt thành 13 lệnh vẽ — trả giá thật cho một thứ mắt không thấy.
        /// <see cref="MatchSceneGenerator"/> lấy chính hai hằng này ra dựng hình, nên hình
        /// và texture không thể lệch nhau.
        /// </summary>
        public const float PitchSpanMeters = 42.0f;
        public const float MowStripeDepthMeters = 1.3f;

        /// <summary>
        /// Tỉ lệ đưa khung thành model về đúng <c>GoalFrame</c> của dự án. CỐ Ý LỆCH TRỤC:
        ///
        ///   X 1.016667 — lòng khung model rộng 7.200 → 7.320 (luật FIFA, GoalFrame.Width)
        ///   Y 1.025210 — lòng khung model cao 2.380 → 2.440 (GoalFrame.Height)
        ///   Z 0.710339 — đẩy hai cột chống hậu từ z=2.534 về z=1.800, đúng mép dưới phía sau
        ///                của lưới Verlet (NetGridGenerator.BottomDepth), để khung và lưới
        ///                gặp nhau thay vì cắt nhau.
        ///
        /// Sai số còn lại ở mép NGOÀI cột: 7.564 vs 7.560 và 2.563 vs 2.560 — 4mm, nhỏ hơn
        /// một sợi cỏ. Chấp nhận, vì cái phải đúng tuyệt đối là mép TRONG: bộ phân loại
        /// vào/trượt (T10) đo theo đó.
        /// </summary>
        public static readonly Vector3 GoalScale = new Vector3(1.016667f, 1.025210f, 0.710339f);

        /// <summary>Model quay lưới về -Z của chính nó; sân này lưới phải hướng +Z.</summary>
        public const float GoalYawDegrees = 180f;

        // ═══════════════════════════════════════════════════════════════════════
        //  Vật liệu
        // ═══════════════════════════════════════════════════════════════════════

        [MenuItem("Eleven/Art/Dựng Vật Liệu Đạo Cụ")]
        public static void EnsureMaterials()
        {
            Directory.CreateDirectory(MaterialDir);

            var ball = GetOrCreate(BallMaterial);
            SetTexture(ball, "_BaseMap", TextureDir + "football_Base_Color.png");
            SetTexture(ball, "_BumpMap", TextureDir + "football_Normal_OpenGL.png", "_NORMALMAP");
            SetTexture(ball, "_OcclusionMap", TextureDir + "football_Mixed_AO.png", "_OCCLUSIONMAP");
            ball.SetColor("_BaseColor", Color.white);
            ball.SetFloat("_Metallic", 0f);
            // Da bóng thi đấu bóng vừa phải: đủ để đèn pha bắt được vệt sáng chạy trên bề mặt
            // lúc bay, chưa tới mức thành quả nhựa. Không nhập bản đồ roughness rời — với một
            // vật thể chiếm chưa tới một phần tư màn hình, một hằng số nhìn không khác.
            ball.SetFloat("_Smoothness", 0.32f);
            ball.SetFloat("_OcclusionStrength", 1f);
            EditorUtility.SetDirty(ball);

            var post = GetOrCreate(GoalPostMaterial);
            post.SetColor("_BaseColor", new Color(0.95f, 0.96f, 0.97f));
            post.SetFloat("_Metallic", 0f);
            post.SetFloat("_Smoothness", 0.45f);   // cột nhôm sơn trắng
            EditorUtility.SetDirty(post);

            var rope = GetOrCreate(GoalRopeMaterial);
            rope.SetColor("_BaseColor", new Color(0.88f, 0.89f, 0.86f));
            rope.SetFloat("_Metallic", 0f);
            rope.SetFloat("_Smoothness", 0.12f);   // dây thừng: gần như không phản chiếu
            EditorUtility.SetDirty(rope);

            // Mặt cỏ. Ảnh gốc là bãi cỏ tự nhiên, đo ra trung bình sRGB (102, 132, 44) —
            // ngả vàng, thiếu hẳn kênh xanh lam. Sân bóng cắt tỉa và tưới đủ nước thì xanh
            // sâu hơn, nên nhân màu để kéo về: hạ đỏ, hạ nhẹ lục, đẩy lam lên. Kết quả
            // khoảng sRGB (88, 126, 55) — vẫn là cỏ thật, chỉ thôi ngả vàng.
            //
            // Vệt cắt cỏ dùng CHUNG texture, chỉ sáng hơn 1.18 lần. Đó đúng là chuyện xảy ra
            // ngoài đời: cùng một thảm cỏ, lưỡi máy cắt gạt lá ngả về hai hướng ngược nhau
            // nên một dải hắt sáng nhiều hơn dải kia. Tô hai texture khác nhau mới là sai.
            MakeKeeper();

            float span = PitchSpanMeters / GrassTileMeters;

            // MÀU LÀ CỦA TẤM ẢNH, KHÔNG PHẢI CỦA TINT. Ảnh Grass005 đo ra trung bình
            // sRGB (101, 132, 44) — đã đúng màu cỏ sân bóng rồi. Nên tint để XÁM TRUNG
            // TÍNH, việc duy nhất của nó là chênh sáng giữa hai vệt cắt cỏ. Bản trước
            // dùng tint (0.72, 0.88, 1.50): xanh lam gấp đôi đỏ, kéo tấm cỏ về phía màu
            // bạc hà. Con số đó chỉnh theo khung hình render batchmode, mà batchmode thì
            // chính CameraFramingProbe đã ghi rõ là KHÔNG tin được về màu.
            MakeGrass(PitchGrassMaterial, new Color(0.85f, 0.85f, 0.85f),
                      new Vector2(span, span));
            // Vệt cắt cỏ là mặt trên của một khối hộp 42m x 1.3m: lát ngang y hệt mặt sân,
            // lát dọc thì ít hơn nhiều, để lá cỏ trong vệt cùng cỡ lá cỏ ngoài vệt. Sáng
            // hơn nền 1.18 lần — máy cắt cỏ vuốt lá cỏ rạp về một phía chứ không đổi màu cỏ.
            MakeGrass(PitchStripeMaterial, new Color(1.00f, 1.00f, 1.00f),
                      new Vector2(span, MowStripeDepthMeters / GrassTileMeters));

            AssetDatabase.SaveAssets();
            Debug.Log("[PropModelLibrary] Đã dựng xong vật liệu đạo cụ trong " + MaterialDir);
        }

        /// <summary>
        /// Vật liệu thủ môn: base color + normal của bộ model tải về. CỐ TÌNH BỎ hai bản đồ
        /// metallic và roughness đi kèm. Metallic đo ra gần như toàn 0 (đỉnh 68/255 ở một
        /// chi tiết nhỏ) — người mặc áo đấu thì đúng là không có kim loại. Roughness thì phải
        /// đóng gói vào kênh alpha của map khác mới dùng được trong URP, tốn một bước xử lý
        /// ảnh cho một khác biệt không thấy được ở cỡ thủ môn hiện trên màn hình điện thoại.
        /// </summary>
        static void MakeKeeper()
        {
            var mat = GetOrCreate(KeeperMaterial);
            SetTexture(mat, "_BaseMap", MixamoModelImport.CharacterTextureDir + "Goalkeeper_BaseColor.png");
            SetTexture(mat, "_BumpMap", MixamoModelImport.CharacterTextureDir + "Goalkeeper_Normal.png",
                       "_NORMALMAP");
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Metallic", 0f);
            // Áo đấu là vải lưới: bóng hơn cỏ một chút, còn xa mới tới mức da hay nhựa.
            mat.SetFloat("_Smoothness", 0.22f);
            EditorUtility.SetDirty(mat);
        }

        static void MakeGrass(string path, Color tint, Vector2 tiling)
        {
            var mat = GetOrCreate(path);
            SetTexture(mat, "_BaseMap", PitchTextureImport.GrassColor);
            mat.SetTextureScale("_BaseMap", tiling);
            mat.SetTextureScale("_BumpMap", tiling);
            SetTexture(mat, "_BumpMap", PitchTextureImport.GrassNormal, "_NORMALMAP");
            mat.SetColor("_BaseColor", tint);
            mat.SetFloat("_Metallic", 0f);
            // Cỏ khô gần như không phản chiếu; để bóng lên là ra mặt sân nhựa.
            mat.SetFloat("_Smoothness", 0.08f);
            // Gờ lá cỏ trong bản đồ pháp tuyến rất nhỏ, nhấn lên mới thấy ở khoảng cách
            // máy quay đứng — nhưng quá tay thì mặt sân lấp lánh nhiễu lúc máy di chuyển.
            if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", 1.3f);
            EditorUtility.SetDirty(mat);
        }

        static Material GetOrCreate(string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void SetTexture(Material mat, string property, string texturePath, string keyword = null)
        {
            if (!mat.HasProperty(property)) return;

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (tex == null)
            {
                Debug.LogWarning($"[PropModelLibrary] Thiếu texture {texturePath} — bỏ qua {property}.");
                return;
            }

            mat.SetTexture(property, tex);
            if (keyword != null) mat.EnableKeyword(keyword);
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Dựng vào scene
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Sinh phần NHÌN THẤY của quả bóng: một bản model đã đúng cỡ 0.22m sẵn từ importer,
        /// nên chỉ việc đặt vào và gán vật liệu. Trả về null nếu chưa có model — nơi gọi tự
        /// lùi về quả cầu primitive.
        /// </summary>
        public static GameObject InstantiateBallVisual(Transform parent)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(BallFbx);
            if (fbx == null) return null;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx, parent);
            go.name = "BallVisual";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var ballMat = AssetDatabase.LoadAssetAtPath<Material>(BallMaterial);
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                r.sharedMaterial = ballMat;

            return go;
        }

        /// <summary>
        /// Sinh khung thành từ model, đã xoay và co giãn về đúng <c>GoalFrame</c>.
        /// Lưới dây của model đã bị importer vứt (xem <see cref="PropModelImport"/>);
        /// phần lưới trong scene vẫn là lưới Verlet của T28.
        /// </summary>
        public static GameObject InstantiateGoal(Transform parent, float goalZ)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(GoalFbx);
            if (fbx == null) return null;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx, parent);
            go.name = "GoalFrameModel";
            go.transform.localPosition = new Vector3(0f, 0f, goalZ);
            go.transform.localRotation = Quaternion.Euler(0f, GoalYawDegrees, 0f);
            go.transform.localScale = GoalScale;

            var post = AssetDatabase.LoadAssetAtPath<Material>(GoalPostMaterial);
            var rope = AssetDatabase.LoadAssetAtPath<Material>(GoalRopeMaterial);

            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                // Trong FBX: Rectangle001 là khung, Cylinder00x là hai cột chống hậu, Line00x
                // là dây néo từ xà ngang ra sau. Chỉ mấy sợi dây dùng vật liệu thừng; còn lại
                // là kim loại sơn trắng.
                bool isRope = r.name.StartsWith("Line");
                r.sharedMaterial = isRope ? rope : post;
            }

            return go;
        }
    }
}
