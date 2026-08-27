using Unity.Mathematics;
using Eleven.Match;

namespace Eleven.Presentation
{
    /// <summary>
    /// Giao diện điều phối góc máy máy ảnh theo tiến trình lượt sút.
    /// </summary>
    public interface ICameraDirector
    {
        /// <summary>
        /// Chuyển đổi góc quay camera hiện tại sang <paramref name="shot"/>.
        /// </summary>
        /// <param name="shot">Góc máy đích.</param>
        /// <param name="blendSeconds">Thời gian pha trộn (blend) góc máy tính bằng giây (0 = cắt tức thì).</param>
        void CutTo(CameraShot shot, float blendSeconds);

        /// <summary>
        /// Gán một góc máy cố định sẽ tự động kích hoạt khi lượt sút chuyển sang pha <paramref name="phase"/>.
        /// </summary>
        /// <param name="phase">Pha của lượt sút.</param>
        /// <param name="shot">Góc máy tương ứng.</param>
        void BindToPhase(KickPhase phase, CameraShot shot);

        /// <summary>
        /// Kiểm tra toạ độ <paramref name="position"/> có nằm hoàn toàn trong vùng không gian 12m đã được dựng hay không.
        /// </summary>
        /// <param name="position">Toạ độ không gian thế giới cần kiểm tra.</param>
        /// <returns>True nếu vị trí nằm trong ranh giới an toàn đã dựng, False nếu nằm ngoài.</returns>
        bool IsWithinAuthoredBounds(in float3 position);
    }
}
