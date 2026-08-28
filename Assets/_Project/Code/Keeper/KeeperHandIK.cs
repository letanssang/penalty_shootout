using UnityEngine;

namespace Eleven.Keeper
{
    /// <summary>
    /// Ghim BÀN TAY thủ môn vào một điểm trong thế giới, trong khi clip hoạt ảnh vẫn lo phần
    /// còn lại của thân người.
    ///
    /// VÌ SAO CẦN NÓ:
    /// <see cref="GoalkeeperView"/> có một bất biến không được phép nhượng bộ — điểm mà người
    /// chơi NHÌN THẤY bàn tay chạm tới phải là đúng điểm mà <see cref="SaveResolver"/> đem đi
    /// chấm, tức <see cref="KeeperReach.HandPositionAt"/>. Trước ngày 2026-08-28 cách giữ bất
    /// biến ấy là tắt hẳn Animator lúc chạm bóng rồi tự lái thân người bằng code: đúng tuyệt
    /// đối, và trông như cái tủ lạnh trượt ngang rồi đứng hình.
    ///
    /// IK cho phép giữ cả hai. Clip diễn tư thế (nhào, chạm đất, chống tay đứng dậy); lớp này
    /// kéo riêng bàn tay về đúng toạ độ gameplay. Cánh tay là thứ duy nhất bị bẻ khỏi clip,
    /// và nó chỉ bị bẻ trong khoảng một phần hai giây bóng bay.
    ///
    /// PHẢI NẰM CÙNG GameObject VỚI ANIMATOR: Unity chỉ gọi <c>OnAnimatorIK</c> trên các
    /// component ngồi cạnh Animator, và chỉ khi tầng hoạt ảnh đã bật IK Pass — xem
    /// <c>KeeperAnimatorControllerBuilder</c>.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class KeeperHandIK : MonoBehaviour
    {
        private Animator _animator;

        private AvatarIKGoal _goal = AvatarIKGoal.RightHand;
        private Vector3 _target;
        private float _weight;

        private void Awake() => _animator = GetComponent<Animator>();

        /// <summary>
        /// Bàn tay nào, kéo tới đâu, kéo mạnh bao nhiêu (0 = mặc clip, 1 = ghim cứng).
        /// Gọi mỗi khung hình: Animator xoá cấu hình IK sau mỗi lần đánh giá.
        /// </summary>
        public void Pin(bool rightHand, Vector3 worldPosition, float weight)
        {
            _goal = rightHand ? AvatarIKGoal.RightHand : AvatarIKGoal.LeftHand;
            _target = worldPosition;
            _weight = Mathf.Clamp01(weight);
        }

        /// <summary>Trả bàn tay về cho clip.</summary>
        public void Release() => _weight = 0f;

        /// <summary>
        /// Vị trí bàn tay đang bị ghim, để chỗ khác đọc lại mà không phải tự suy ra.
        /// Trả về false khi không ghim tay nào.
        /// </summary>
        public bool TryGetPinned(out Vector3 worldPosition)
        {
            worldPosition = _target;
            return _weight > 0f;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0 || _animator == null) return;

            // Đặt cả trọng số 0 chứ không bỏ qua: bỏ qua thì Animator giữ nguyên trọng số của
            // khung trước và bàn tay dính lại ở điểm cũ sau khi đã thả.
            _animator.SetIKPositionWeight(_goal, _weight);
            if (_weight > 0f) _animator.SetIKPosition(_goal, _target);
        }
    }
}
