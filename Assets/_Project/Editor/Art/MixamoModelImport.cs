using System.IO;
using UnityEditor;
using UnityEngine;

namespace Eleven.Editor.Tools
{
    /// <summary>
    /// Áp thiết lập import cố định cho mọi FBX Mixamo nằm dưới <see cref="Root"/>.
    ///
    /// Lý do tồn tại: mấy thiết lập này quyết định đúng-sai của gameplay chứ không phải
    /// thẩm mỹ, và chúng nằm trong file .meta — thứ rất dễ bị một cú bấm nhầm trong
    /// Inspector làm hỏng mà không ai thấy. Đặt ở đây thì mọi lần reimport đều trả về
    /// đúng trạng thái, và thay đổi phải đi qua code review.
    ///
    /// Ba thiết lập KHÔNG được đổi nếu chưa đọc docs/backlog/phase-7-hoat-anh-ik.md:
    ///  • <c>animationCompression = Off</c> — T35 đòi sai số khung chạm bóng dưới 1 khung ở
    ///    60fps. Nén keyframe làm mượt đúng đoạn chân vung nhanh nhất, tức là làm sai chính
    ///    con số sắp đem đi đo.
    ///  • <c>optimizeGameObjects = false</c> — KickerBoneCueSource đọc transform của
    ///    root/plantFoot/hips mỗi khung hình. Bật optimize là Unity xoá hierarchy xương,
    ///    GetBoneTransform trả null, và thủ môn mất sạch tín hiệu đọc vị.
    ///  • <c>animationType = Human</c> — nhờ nó mà clip retarget được sang model cầu thủ
    ///    thật sau này mà không phải làm lại Animator.
    /// </summary>
    public sealed class MixamoModelImport : AssetPostprocessor
    {
        public const string Root = "Assets/_Project/Art/Animations/Mixamo/";

        /// <summary>Nhân vật T-pose tải từ tab Characters của Mixamo (X Bot / Y Bot đều được).</summary>
        public const string CharacterDir = "Assets/_Project/Art/Characters";

        /// <summary>Thư mục texture của nhân vật (base color / normal), do bên này áp luật import.</summary>
        public const string CharacterTextureDir = CharacterDir + "/Textures/";

        /// <summary>
        /// Model thủ môn (Maya, bộ xương đặt tên y hệt Mixamo nên Humanoid tự khớp được).
        /// Vật liệu do <c>PropModelLibrary</c> dựng chứ không để importer tự sinh — xem
        /// <see cref="OnPreprocessModel"/>.
        /// </summary>
        public const string KeeperCharacter = CharacterDir + "/Goalkeeper.fbx";

        /// <summary>
        /// Chiều cao model thủ môn khi nhập với globalScale = 1, đo bằng
        /// <see cref="PropModelReport"/> ngày 2026-08-28: hộp bao (0.142, 0.152, 0.030)m.
        /// FBX do Maya xuất, đơn vị trong file nhỏ hơn mét chừng 12 lần.
        /// </summary>
        const float KeeperImportedHeight = 0.152f;

        /// <summary>Chiều cao thủ môn muốn có (m). Thủ môn chuyên nghiệp cao 1.85–1.95m.</summary>
        public const float KeeperHeight = 1.85f;

        /// <summary>
        /// Co giãn NGAY LÚC IMPORT chứ không bằng localScale ở scene, cùng lý do như quả bóng:
        /// mesh nằm sẵn trong bộ nhớ ở đúng cỡ thật, và mọi thứ gắn vào nó về sau (găng tay,
        /// hiệu ứng) không bị nhân theo một hệ số vô hình.
        /// </summary>
        public const float KeeperImportScale = KeeperHeight / KeeperImportedHeight;

        /// <summary>
        /// Nhân vật giữ vai Avatar gốc cho TOÀN BỘ clip Mixamo. Ghim cứng vào X Bot chứ không
        /// lấy "file .fbx đầu tiên trong thư mục" như trước: từ 2026-08-28 thư mục có thêm
        /// Goalkeeper.fbx, mà xếp theo bảng mã thì "G" đứng trước "X" — để nguyên luật cũ là
        /// cả bộ clip lặng lẽ đổi sang khớp xương thủ môn, và những khung chạm bóng đo được
        /// của T35 (sai số dưới một khung ở 60fps) không còn đúng nữa.
        ///
        /// Từ 2026-08-28 nó KHÔNG còn là model người sút nhìn thấy trên sân — chỗ đó là
        /// Kicker.fbx (Ch38), xem MatchSceneGenerator.KickerModelPath. Hai vai đã tách: X Bot
        /// giữ vai bộ xương chuẩn cho clip, Ch38 giữ vai phần nhìn.
        /// </summary>
        public const string KickerCharacter = CharacterDir + "/XBot.fbx";

        /// <summary>
        /// Nguồn dự phòng khi thư mục Characters còn trống: clip đứng yên, tư thế khung 0 gần
        /// trung tính nhất trong gói. Chỉ là chỗ đứng tạm cho tới khi có nhân vật thật.
        /// </summary>
        public const string FallbackAvatarSource = Root + "Kicker/OffensiveIdle.fbx";

        /// <summary>
        /// Đường dẫn FBX đang giữ vai trò Avatar gốc cho cả bộ: FBX đầu tiên trong
        /// <see cref="CharacterDir"/> nếu có, không thì clip dự phòng. Tự dò thay vì hằng số
        /// cứng để thả X Bot hay Y Bot vào cũng chạy, khỏi phải sửa code.
        /// </summary>
        public static string AvatarSourcePath
        {
            get
            {
                if (File.Exists(KickerCharacter)) return KickerCharacter;

                if (Directory.Exists(CharacterDir))
                {
                    var fbx = Directory.GetFiles(CharacterDir, "*.fbx", SearchOption.TopDirectoryOnly);
                    if (fbx.Length > 0)
                    {
                        System.Array.Sort(fbx, System.StringComparer.Ordinal);
                        return fbx[0].Replace('\\', '/');
                    }
                }
                return FallbackAvatarSource;
            }
        }

        /// <summary>
        /// Tăng số này mỗi lần sửa luật import ở dưới, để Unity nhập lại các asset đã nhập
        /// theo luật cũ. Không có nó thì sửa code xong asset vẫn giữ thiết lập cũ cho tới khi
        /// có người bấm Reimport bằng tay — và cái sai đó chỉ lộ ra trên máy người khác.
        /// </summary>
        public override uint GetVersion() => 2;

        static bool IsCharacter(string path) =>
            path.StartsWith(CharacterDir) && path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);

        void OnPreprocessModel()
        {
            if (IsCharacter(assetPath))
            {
                // Nhân vật thì ngược lại với clip: GIỮ mesh và vật liệu (đó là thứ nhìn thấy),
                // BỎ animation (tư thế T-pose không phải chuyển động cần lưu).
                var character = (ModelImporter)assetImporter;
                character.importAnimation = false;
                character.animationType   = ModelImporterAnimationType.Human;
                character.avatarSetup     = ModelImporterAvatarSetup.CreateFromThisModel;
                character.optimizeGameObjects = false;
                character.importCameras   = false;
                character.importLights    = false;

                if (assetPath == KeeperCharacter)
                {
                    // FBX thủ môn trỏ texture bằng đường dẫn tuyệt đối trên máy người dựng
                    // ("D:/Working file/..."), nên importer chỉ đẻ ra một material rỗng rồi
                    // đè lại mỗi lần reimport. Tự dựng vật liệu URP Lit ở PropModelLibrary
                    // và gán lúc dựng scene thì mới kiểm soát được.
                    character.materialImportMode = ModelImporterMaterialImportMode.None;
                    character.importAnimation = false;   // tư thế T-pose, clip lấy từ Mixamo
                    character.useFileScale = true;
                    character.globalScale = KeeperImportScale;
                }
                return;
            }

            if (!assetPath.StartsWith(Root)) return;
            var importer = (ModelImporter)assetImporter;

            // Không có mesh/vật liệu/camera trong bản tải "without skin"; tắt hẳn để khỏi
            // sinh sub-asset rác và để reimport nhanh.
            importer.importCameras     = false;
            importer.importLights      = false;
            importer.importVisibility  = false;
            importer.importBlendShapes = false;
            importer.importConstraints = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;

            importer.importAnimation   = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.resampleCurves    = true;
            importer.optimizeGameObjects = false;

            importer.animationType = ModelImporterAnimationType.Human;

            var source = AvatarSourcePath;
            if (assetPath == source)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                return;
            }

            // Dùng chung một Avatar cho cả bộ để muscle mapping đồng nhất — clip nào cũng
            // được quy về cùng một tỉ lệ xương, nếu không thì sai số retarget khác nhau
            // từng clip và T39 sẽ đo ra nhiễu.
            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(source);
            if (avatar != null)
            {
                importer.avatarSetup  = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = avatar;
            }
            else
            {
                // Avatar gốc chưa import xong (thứ tự import của Unity không đảm bảo).
                // Tự dựng tạm; MixamoAnimationReport.Run sẽ import lại đúng thứ tự.
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            }
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(CharacterTextureDir, System.StringComparison.Ordinal)) return;

            var ti = (TextureImporter)assetImporter;
            ti.mipmapEnabled = true;
            ti.wrapMode = TextureWrapMode.Clamp;   // UV nhân vật nằm gọn trong ô, không lát
            ti.filterMode = FilterMode.Bilinear;
            ti.textureCompression = TextureImporterCompression.Compressed;

            // Bản gốc 4096² là cỡ để render tĩnh. Thủ môn đứng cách máy 14m, cao chừng một
            // phần ba màn hình điện thoại — 1024 đã hơn đủ, mà tiết kiệm 15/16 bộ nhớ texture.
            ti.maxTextureSize = 1024;

            bool isNormal = Path.GetFileNameWithoutExtension(assetPath)
                                .EndsWith("_Normal", System.StringComparison.OrdinalIgnoreCase);
            if (isNormal)
            {
                ti.textureType = TextureImporterType.NormalMap;
            }
            else
            {
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = true;
            }
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(Root)) return;
            var importer = (ModelImporter)assetImporter;

            var clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;

            // Mixamo đặt tên MỌI take là "mixamo.com". Để nguyên thì Animator hiện 23 clip
            // trùng tên và không ai chọn đúng được clip nào.
            var fileName = Path.GetFileNameWithoutExtension(assetPath);
            // Chỉ bật loop cho clip ĐÃ ĐO là khép kín. Menu/Juggle* và Menu/Stall* có độ lệch
            // tư thế đầu-cuối 0.0 cm (docs/data/menu-clip-probe.tsv) nên nối vòng không giật;
            // JuggleKickUp lệch 20.5 cm nên là động tác vào, không nằm trong danh sách này.
            var loop = fileName.Contains("Idle")
                       || fileName == "JogForward"
                       || fileName.StartsWith("JuggleKnee")
                       || fileName.StartsWith("Stall");

            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].name = clips.Length == 1 ? fileName : $"{fileName}_{i}";
                clips[i].loopTime = loop;

                // Chia root motion làm ba đường, theo đúng ranh giới "cái gì gameplay quyết,
                // cái gì clip quyết":
                //  • XZ KHÔNG bake vào pose → nằm ở kênh root motion → Apply Root Motion tắt
                //    thì clip chạy tại chỗ, vị trí ngang hoàn toàn do gameplay đặt.
                //  • Y CÓ bake vào pose → cú bay người của thủ môn thấy được chiều cao thật
                //    của clip, mà root vẫn dính đất và không đẩy nhân vật đi đâu.
                //  • Rotation CÓ bake vào pose → thân người xoay theo clip, còn hướng mặt của
                //    nhân vật vẫn do gameplay đặt.
                clips[i].lockRootPositionXZ  = false;
                clips[i].lockRootHeightY     = true;
                clips[i].lockRootRotation    = true;
                clips[i].keepOriginalPositionXZ  = true;
                clips[i].keepOriginalPositionY   = true;
                clips[i].keepOriginalOrientation = true;
            }
            importer.clipAnimations = clips;
        }
    }
}
