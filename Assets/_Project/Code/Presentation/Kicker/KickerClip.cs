namespace Eleven.Presentation.Kicker
{
    /// <summary>
    /// Chín tư thế mà người sút có thể đang ở. Đây là TÊN STATE trong
    /// <c>KickerAnimator.controller</c> — trùng từng ký tự, vì lớp điều khiển runtime
    /// gọi <c>Animator.CrossFadeInFixedTime(clip.ToString(), …)</c>.
    ///
    /// Cố ý KHÔNG trùng với <see cref="Eleven.Shooter.ShotType"/> dù bốn giá trị Strike*
    /// ánh xạ một-một với nó: ShotType là ý đồ chơi game, KickerClip là tài sản nghệ thuật.
    /// Ngày mai thêm một kiểu sút dùng chung clip cũ, hoặc một clip ăn mừng thứ hai, thì
    /// chỉ một trong hai enum phải đổi.
    /// </summary>
    public enum KickerClip : byte
    {
        Idle = 0,
        RunUp = 1,
        StrikeInstep = 2,
        StrikeInsideFoot = 3,
        StrikeChip = 4,
        StrikeKnuckle = 5,
        FollowThrough = 6,
        Celebrate = 7,
        Dejected = 8,
    }
}
