using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Eleven.Editor.Tools
{
    /// <summary>
    /// Dựng Animator Controller cho thủ môn: một trạng thái đứng chờ và CHÍN trạng thái bay
    /// người, mỗi ô lưới khung thành một clip.
    ///
    /// LỊCH SỬ — VÌ SAO TRƯỚC ĐÂY CHỈ CÓ MỘT TRẠNG THÁI:
    /// Bản đầu cố tình bỏ hết clip bay người, vì bất biến của
    /// <see cref="Eleven.Keeper.GoalkeeperView"/> đòi đường bay NHÌN THẤY phải trùng đúng công
    /// thức <c>KeeperReach</c> mà <c>SaveResolver</c> đem đi chấm, mà clip diễn viên thì bay
    /// theo quỹ đạo của nó. Cái giá phải trả là thủ môn trượt ngang như tủ lạnh rồi đứng hình.
    ///
    /// VÌ SAO GIỜ ĐƯA VÀO ĐƯỢC:
    /// Không phải vì đổi ý về bất biến — bất biến giữ nguyên. Mà vì chia lại việc: clip chỉ lo
    /// TƯ THẾ (nhào, chạm đất, chống tay đứng dậy), còn BÀN TAY vẫn bị ghim đúng vào điểm
    /// <c>KeeperReach.HandPositionAt</c> bằng IK (xem <c>KeeperHandIK</c>). Điểm mà
    /// <c>SaveResolver</c> đo không đổi một milimét, nên kết quả chấm cũng không đổi.
    ///
    /// Hai con số đo ngày 2026-08-28 (docs/data/keeper-clip-pose.tsv) cho phép làm việc này:
    ///
    ///  • Clip bay người ĐÃ CÓ SẴN PHA ĐỨNG DẬY: hông đi 0.94 m → 0.15 m → 0.92 m trong cùng
    ///    một clip. Không phải tải thêm clip "get up" nào từ Mixamo.
    ///
    ///  • Cánh tay thật dài 0.7 m, còn khoảng cách từ thân (chỗ code đẩy tới) đến tâm ô xa
    ///    nhất là 0.61 m. Vừa đủ, nên IK kéo tay tới đích được thật chứ không phải hy vọng.
    /// </summary>
    public static class KeeperAnimatorControllerBuilder
    {
        public const string ControllerPath = "Assets/_Project/Art/Animations/KeeperAnimator.controller";

        const string ClipDir = "Assets/_Project/Art/Animations/Mixamo/Keeper/";

        /// <summary>Tên trạng thái đứng chờ. <c>GoalkeeperView</c> gọi thẳng bằng tên này.</summary>
        public const string IdleState = "Idle";

        /// <summary>Tiền tố tên trạng thái bay người: "Dive0".."Dive8" theo chỉ số ô.</summary>
        public const string DiveStatePrefix = "Dive";

        /// <summary>
        /// KeeperIdle_B: 3.33s, 200 khung, root motion bằng 0 tuyệt đối và loopTime đã bật
        /// (docs/data/mixamo-clip-report.tsv) — nối vòng không trôi, không giật.
        /// </summary>
        const string IdleClipPath = ClipDir + "KeeperIdle_B.fbx";

        /// <summary>
        /// Clip cho từng ô, chỉ số 0..8 theo <c>GoalFrame.CellCenter</c>: ô 0 là GÓC CAO BÊN
        /// −x, ô 4 chính giữa, ô 8 là góc thấp bên +x.
        ///
        /// Chọn theo SỐ ĐO trong docs/data/keeper-clip-pose.tsv chứ không theo tên file:
        ///
        ///  • ô 0/2 — `DivingSave_B/_A`: bay ngang 3.68 m và 2.96 m, tay với cao 1.54 m và
        ///    1.28 m.
        ///  • ô 1 — `Catch_D`: tay với cao 2.18 m, cao nhất trong nhóm không đổ người; tâm ô
        ///    cao 2.03 m.
        ///  • ô 3/5 — `Sidestep_B/_A`: bước ngang 0.5 s, hông không chạm đất — đúng kiểu cản
        ///    bóng ngang tầm người, không cần nằm xuống.
        ///  • ô 4 — `Catch_B`: bắt trước ngực, tay 1.35 m, tâm ô 1.22 m.
        ///  • ô 6/8 — `BodyBlock_B/_A`: hông tụt xuống 0.16 m, đổ người sát cỏ.
        ///  • ô 7 — `Catch_A`: hạ người xuống 0.75 m, tay 0.94 m — bóng sệt chính giữa.
        ///
        /// CHIỀU TRÁI/PHẢI ĐO BẰNG <see cref="KeeperDiveSideProbe"/>, KHÔNG PHẢI KeeperClipProbe.
        /// Bản đầu lấy chiều từ KeeperClipProbe và cả chín ô đều bị lật gương — người chơi thấy
        /// thủ môn trượt về phía bóng nhưng đổ người ngược lại, chân đi trước. Lý do:
        /// KeeperClipProbe đọc hiệu RootT.x giữa khung cuối và khung đầu, mà mọi clip cản phá
        /// đều đổ người rồi TỰ ĐỨNG DẬY TẠI CHỖ, nên hiệu đó gần bằng không — nó không sai,
        /// nó chỉ không trả lời được câu hỏi này. Thêm một phép lật nữa: thủ môn trong scene
        /// xoay 180 độ để quay mặt về người sút, nên clip đổ về +x của model là đổ về −x của
        /// thế giới. Test NgaDungBenVoiOLuoi gác cả hai phép lật đó.
        /// </summary>
        static readonly string[] CellClips =
        {
            "KeeperDivingSave_B.fbx", "KeeperCatch_D.fbx",  "KeeperDivingSave_A.fbx",
            "KeeperSidestep_B.fbx",   "KeeperCatch_B.fbx",  "KeeperSidestep_A.fbx",
            "KeeperBodyBlock_B.fbx",  "KeeperCatch_A.fbx",  "KeeperBodyBlock_A.fbx",
        };

        [MenuItem("Eleven/Art/Build Keeper Animator Controller")]
        public static void Run()
        {
            AnimationClip idle = LoadFirstVisibleClip(IdleClipPath);
            if (idle == null)
            {
                Debug.LogError($"[KeeperAnimatorControllerBuilder] Không tìm thấy AnimationClip tại: {IdleClipPath}");
                return;
            }

            var diveClips = new AnimationClip[CellClips.Length];
            for (int cell = 0; cell < CellClips.Length; cell++)
            {
                diveClips[cell] = LoadFirstVisibleClip(ClipDir + CellClips[cell]);
                if (diveClips[cell] == null)
                {
                    Debug.LogError($"[KeeperAnimatorControllerBuilder] Thiếu clip cho ô {cell}: {ClipDir + CellClips[cell]}");
                    return;
                }
            }

            // Xoá rồi tạo lại để chạy bao nhiêu lần cũng ra một kết quả:
            // CreateAnimatorControllerAtPath ném lỗi nếu file đã có.
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            {
                AssetDatabase.DeleteAsset(ControllerPath);
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            // IK Pass phải BẬT, nếu không thì OnAnimatorIK không bao giờ được gọi và bàn tay
            // thả trôi theo clip — đúng cái sai mà cả thiết kế này sinh ra để tránh.
            AnimatorControllerLayer layer = controller.layers[0];
            layer.iKPass = true;
            controller.layers = new[] { layer };

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

            foreach (ChildAnimatorState existing in stateMachine.states.ToArray())
            {
                stateMachine.RemoveState(existing.state);
            }

            AnimatorState idleState = stateMachine.AddState(IdleState, new Vector3(260f, 0f, 0f));
            idleState.motion = idle;
            stateMachine.defaultState = idleState;

            // Không nối transition nào: GoalkeeperView gọi thẳng CrossFade theo tên trạng thái.
            // Máy trạng thái ngầm định bằng điều kiện sẽ là nguồn sự thật thứ hai bên cạnh
            // SimpleKeeperController, và hai nguồn ấy chắc chắn có ngày lệch nhau.
            for (int cell = 0; cell < diveClips.Length; cell++)
            {
                var pos = new Vector3(560f, (cell - 4) * 60f, 0f);
                AnimatorState state = stateMachine.AddState(DiveStatePrefix + cell, pos);
                state.motion = diveClips[cell];
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log($"[KeeperAnimatorControllerBuilder] Hoàn tất: {ControllerPath} " +
                      $"({diveClips.Length} trạng thái bay + đứng chờ, IK Pass bật).");
        }

        /// <summary>
        /// Clip đầu tiên hiển thị trong FBX. Clip ẩn (HideInHierarchy) là metadata nội bộ của
        /// Unity chứ không phải clip nghệ sĩ export — lọc bỏ để khỏi gán nhầm.
        /// </summary>
        static AnimationClip LoadFirstVisibleClip(string fbxPath)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (asset is AnimationClip clip && (clip.hideFlags & HideFlags.HideInHierarchy) == 0)
                    return clip;
            }
            return null;
        }
    }
}
