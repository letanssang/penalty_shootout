using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Eleven.Editor.Tools
{
    public static class KickerAnimatorControllerBuilder
    {
        private const string ControllerPath = "Assets/_Project/Art/Animations/KickerAnimator.controller";

        // Lớp điều khiển runtime dùng Animator.CrossFadeInFixedTime theo tên state để chuyển animation,
        // nên việc tạo transition trong Animator Controller không những thừa mà còn có thể gây xung đột
        // ngầm khi Animator tự động chuyển state theo logic transition mà ta không mong muốn.
        // Giữ đồ thị hoàn toàn tĩnh giúp runtime có toàn quyền kiểm soát luồng animation.

        private static readonly (string stateName, string fbxPath, bool mirror)[] StateDefinitions =
        {
            ("Idle",             "Assets/_Project/Art/Animations/Mixamo/Kicker/OffensiveIdle.fbx",    false),
            ("RunUp",            "Assets/_Project/Art/Animations/Mixamo/Kicker/JogForward.fbx",       false),
            ("StrikeInstep",     "Assets/_Project/Art/Animations/Mixamo/Kicker/PenaltyKick.fbx",      false),
            ("StrikeInsideFoot", "Assets/_Project/Art/Animations/Mixamo/Kicker/KickSoccerball_B.fbx", false),
            // KickSoccerball_A là clip sút chân TRÁI từ Mixamo, trong khi ba clip Strike còn lại đều sút
            // chân PHẢI. Bật mirror để nhân vật luôn sút chân phải, đảm bảo tính nhất quán hình ảnh
            // mà không cần yêu cầu nghệ sĩ export lại clip hoặc tạo clip riêng.
            ("StrikeChip",       "Assets/_Project/Art/Animations/Mixamo/Kicker/KickSoccerball_A.fbx", true),
            ("StrikeKnuckle",    "Assets/_Project/Art/Animations/Mixamo/Kicker/PenaltyKick.fbx",      false),
            // FollowThrough KHÔNG dùng lại PenaltyKick: state này chỉ bật ở pha Resolution,
            // tức sau khi bóng đã rời chân, mà clip PenaltyKick phát lại TỪ ĐẦU — người sút sẽ
            // chạy đà lần thứ hai trong lúc bóng đang bay. Gói Soccer Game Pack không có clip
            // "vung chân xong đứng lại" riêng, nên tư thế thật thà nhất là đứng nhìn theo bóng.
            // Giữ state riêng thay vì bỏ hẳn để sau này thả clip thật vào là xong, không sửa mã.
            ("FollowThrough",    "Assets/_Project/Art/Animations/Mixamo/Kicker/OffensiveIdle.fbx",   false),
            ("Celebrate",        "Assets/_Project/Art/Animations/Mixamo/Kicker/Celebrate.fbx",        false),
            ("Dejected",         "Assets/_Project/Art/Animations/Mixamo/Kicker/Dejected.fbx",         false),
        };

        [MenuItem("Eleven/Art/Build Kicker Animator Controller")]
        public static void Run()
        {
            // Thu thập clip trước khi chạm vào asset controller, tránh tạo file dở dang khi thiếu nguồn.
            var clips = new Dictionary<string, AnimationClip>();

            foreach (var (stateName, fbxPath, _) in StateDefinitions)
            {
                // Một FBX có thể dùng cho nhiều state (PenaltyKick dùng 3 lần), chỉ cần load một lần.
                if (clips.ContainsKey(fbxPath))
                    continue;

                AnimationClip clip = LoadFirstVisibleClip(fbxPath);
                if (clip == null)
                {
                    // Báo lỗi chi tiết để pipeline CI có thể grep log mà phát hiện ngay vấn đề.
                    Debug.LogError($"[KickerAnimatorControllerBuilder] Không tìm thấy AnimationClip hợp lệ tại: {fbxPath}");
                    return;
                }

                clips[fbxPath] = clip;
            }

            // Xoá controller cũ trước khi tạo mới để đảm bảo idempotent —
            // CreateAnimatorControllerAtPath sẽ ném lỗi nếu file đã tồn tại.
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            {
                AssetDatabase.DeleteAsset(ControllerPath);
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            // Lấy layer 0 vừa được tạo mặc định; không thêm layer mới vì toàn bộ logic nằm trong layer 0.
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

            // Xoá state "New State" mà Unity tự tạo kèm theo để giữ đồ thị sạch,
            // tránh state thừa gây nhầm lẫn khi đọc controller trong Editor.
            foreach (ChildAnimatorState existing in stateMachine.states.ToArray())
            {
                stateMachine.RemoveState(existing.state);
            }

            AnimatorState idleState = null;
            int createdCount = 0;

            // Dàn trải state theo chiều dọc trong Editor để dễ đọc khi mở Animator window.
            Vector3 position = new Vector3(300f, 0f, 0f);
            const float verticalSpacing = 70f;

            foreach (var (stateName, fbxPath, mirror) in StateDefinitions)
            {
                AnimatorState state = stateMachine.AddState(stateName, position);
                state.motion = clips[fbxPath];
                state.mirror = mirror;

                // Không tạo bất kỳ transition nào — xem comment đầu file.

                if (stateName == "Idle")
                    idleState = state;

                position.y += verticalSpacing;
                createdCount++;
            }

            // Đặt Idle làm defaultState để khi nhân vật được spawn, Animator phát Idle ngay lập tức
            // mà không cần runtime gửi lệnh CrossFade ban đầu.
            if (idleState != null)
                stateMachine.defaultState = idleState;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log($"[KickerAnimatorControllerBuilder] Hoàn tất. Đã tạo {createdCount} state trong {ControllerPath}");
        }

        /// <summary>
        /// Lấy AnimationClip đầu tiên hiển thị trong FBX. Clip ẩn (HideInHierarchy) là metadata nội bộ
        /// của Unity, không phải clip thực sự mà nghệ sĩ export — lọc bỏ để tránh gán nhầm.
        /// </summary>
        private static AnimationClip LoadFirstVisibleClip(string fbxPath)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            if (assets == null || assets.Length == 0)
                return null;

            foreach (Object asset in assets)
            {
                if (asset is AnimationClip clip)
                {
                    // HideInHierarchy đánh dấu các sub-asset nội bộ Unity không dành cho người dùng.
                    if ((clip.hideFlags & HideFlags.HideInHierarchy) == 0)
                        return clip;
                }
            }

            return null;
        }
    }
}
