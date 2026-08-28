using Unity.Mathematics;
using UnityEngine;
using Eleven.Ball;

namespace Eleven.Presentation
{
    /// <summary>
    /// Xoay phần NHÌN THẤY của quả bóng theo vận tốc góc mà solver đang giữ.
    ///
    /// Lý do component này phải tồn tại: <see cref="BallDriver"/> cố ý chỉ ghi
    /// <c>transform.position</c>, không bao giờ đụng tới <c>rotation</c> — xoáy trong
    /// <see cref="BallState.spin"/> là đại lượng VẬT LÝ, nó chỉ dùng để tính lực Magnus.
    /// Hồi bóng còn là quả cầu trắng trơn thì không ai thấy sự khác biệt. Từ lúc dán vân
    /// da có múi và đường chỉ khâu, một quả bóng không xoay sẽ TRƯỢT trên mặt cỏ như đồ
    /// chơi kéo dây, và cú sút xoáy — thứ cả phase 1 dựng nên — nhìn hệt cú sút thẳng.
    ///
    /// Chỉ xoay hình, không phản hồi ngược về solver: cái nhìn thấy đi theo cái tính ra,
    /// không bao giờ ngược lại.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BallSpinView : MonoBehaviour
    {
        [Tooltip("Bỏ trống thì tự tìm ngược lên cây cha.")]
        [SerializeField] BallDriver driver;

        [Tooltip("Bán kính bóng (m) — dùng để suy ra tốc độ lăn khi bóng đã chạm đất.")]
        [SerializeField] float radius = 0.11f;

        /// <summary>
        /// Dưới ngưỡng này coi như đứng yên, khỏi rung lắc vì sai số dấu phẩy động
        /// khi bóng đã nằm im chờ lượt sau.
        /// </summary>
        const float MinAngularSpeed = 0.01f;

        void Awake()
        {
            if (driver == null) driver = GetComponentInParent<BallDriver>();
        }

        void LateUpdate()
        {
            if (driver == null) return;

            float3 omega = AngularVelocity(driver.State);
            float speed = math.length(omega);
            if (speed < MinAngularSpeed) return;

            // Quay quanh chính trục của vector xoáy, quy tắc bàn tay phải — đúng quy ước
            // đã ghi ở BallState.spin. Nhân trái để phép quay diễn ra trong hệ thế giới,
            // vì spin là đại lượng thế giới chứ không phải cục bộ của quả bóng.
            var step = Quaternion.AngleAxis(math.degrees(speed) * Time.deltaTime,
                                            (Vector3)(omega / speed));
            transform.rotation = step * transform.rotation;
        }

        /// <summary>
        /// Xoáy bay + lăn mặt đất. Solver không mô phỏng ma sát lăn, nên khi bóng đã tiếp
        /// đất và chỉ còn trôi, <c>spin</c> quanh trục cũ không còn tả đúng cái mắt chờ
        /// thấy: một quả bóng lăn phải quay quanh trục vuông góc hướng đi, đúng một vòng
        /// mỗi chu vi. Chỗ này cộng thêm phần lăn đó thay vì sửa solver.
        /// </summary>
        float3 AngularVelocity(in BallState s)
        {
            float3 omega = s.spin;
            if (radius <= 0f) return omega;

            bool grounded = s.position.y <= radius * 1.05f;
            if (!grounded) return omega;

            float3 horizontal = new float3(s.velocity.x, 0f, s.velocity.z);
            float3 rolling = math.cross(new float3(0f, 1f, 0f), horizontal) / radius;

            // Giữ lại thành phần xoáy quanh trục đứng (bóng xoay tại chỗ) và thay phần
            // còn lại bằng chuyển động lăn thật.
            float3 vertical = new float3(0f, omega.y, 0f);
            return vertical + rolling;
        }
    }
}
