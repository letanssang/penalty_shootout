using Unity.Burst;
using Unity.Mathematics;

namespace Eleven.Ball
{
    /// <summary>
    /// Tích phân quỹ đạo bóng. HÀM THUẦN: không MonoBehaviour, không đọc Time, không đọc
    /// biến static nào. Cùng input luôn cho cùng output từng bit.
    ///
    /// Lý do phải thuần: thủ môn (T16–T21) chạy solver TRƯỚC để dự đoán bóng sẽ tới đâu,
    /// và replay (T27) chạy lại nó để dựng đúng pha bóng cũ. Chỉ cần đọc một biến ngoài
    /// là cả hai thứ đó hỏng.
    ///
    /// Tích phân RK4 cho hệ (vị trí, vận tốc). KHÔNG dùng Euler tiến: ở dt 1/120 với gia tốc
    /// cản ~9 m/s^2, Euler tích luỹ sai số bậc nhất đủ để lệch điểm chạm khung thành vài cm.
    /// </summary>
    [BurstCompile]
    public static class BallSolver
    {
        /// <summary>
        /// Hệ số cản theo tốc độ. Hằng cdLow dưới cdVLow, hằng cdHigh trên cdVHigh,
        /// nối bằng smoothstep ở giữa.
        ///
        /// Vì sao smoothstep chứ không lerp thẳng: đạo hàm của smoothstep bằng 0 ở cả hai
        /// đầu, khớp với đạo hàm 0 của hai đoạn hằng hai bên. Lerp thẳng làm đạo hàm nhảy
        /// bậc tại đúng hai ngưỡng, và cú sút nào đi qua vùng đó cũng bị giật gia tốc.
        /// </summary>
        [BurstCompile]
        public static float DragCoefficient(float speed, in BallParams p)
        {
            float span = p.cdVHigh - p.cdVLow;

            // Dải nội suy suy biến (bị fit về 0 hoặc âm): rơi về bậc thang, không chia cho 0.
            if (!(span > 0f))
                return speed < p.cdVLow ? p.cdLow : p.cdHigh;

            float t = math.saturate((speed - p.cdVLow) / span);
            t = t * t * (3f - 2f * t);
            return math.lerp(p.cdLow, p.cdHigh, t);
        }

        /// <summary>
        /// Gia tốc tại một vận tốc và một xoáy cho trước. Chỉ phụ thuộc vận tốc và xoáy,
        /// không phụ thuộc vị trí — nhờ đó RK4 bên dưới đúng mà không cần lấy mẫu vị trí.
        /// </summary>
        [BurstCompile]
        static float3 Acceleration(in float3 velocity, in float3 spin, in BallParams p)
        {
            // Tính thẳng từ field thay vì gọi p.CrossSectionArea: gọi property trên tham số 'in'
            // của struct không readonly khiến trình biên dịch chép thủ một bản BallParams mỗi lần.
            float area = math.PI * p.radius * p.radius;
            float speed = math.length(velocity);

            // Trọng lực.
            float3 a = new float3(0f, -p.gravity, 0f);

            // Cản: F = -0.5 * rho * Cd * A * |v| * v.
            // speed = 0 → hệ số bằng 0 và v bằng 0 → số hạng đúng bằng 0, không NaN.
            float cd = DragCoefficient(speed, p);
            a -= (0.5f * p.airDensity * cd * area * speed / p.mass) * velocity;

            // Magnus: F = 0.5 * rho * Cl * A * r * (omega x v).
            // Không chuẩn hoá spin ở đâu cả — nhờ vậy spin = 0 cho cross = 0 CHÍNH XÁC
            // chứ không phải 0/0.
            a += (0.5f * p.airDensity * p.liftCoefficient * area * p.radius / p.mass)
                 * math.cross(spin, velocity);

            return a;
        }

        /// <summary>
        /// Một bước RK4 độ dài dt. Xoáy coi như hằng trong bước rồi phân rã ở cuối bước:
        /// ở dt 1/120 với k ~ 0.045/s, sai khác do coi hằng là dưới 4e-4 — nhỏ hơn nhiều
        /// so với sai số của chính mô hình khí động.
        /// </summary>
        [BurstCompile]
        public static BallState Step(in BallState s, in BallParams p, float dt)
        {
            float3 x0 = s.position;
            float3 v0 = s.velocity;
            float3 w = s.spin;

            float3 k1v = Acceleration(v0, w, p);
            float3 k1x = v0;

            float3 v2 = v0 + (0.5f * dt) * k1v;
            float3 k2v = Acceleration(v2, w, p);
            float3 k2x = v2;

            float3 v3 = v0 + (0.5f * dt) * k2v;
            float3 k3v = Acceleration(v3, w, p);
            float3 k3x = v3;

            float3 v4 = v0 + dt * k3v;
            float3 k4v = Acceleration(v4, w, p);
            float3 k4x = v4;

            float sixth = dt / 6f;

            BallState r;
            r.position = x0 + sixth * (k1x + 2f * k2x + 2f * k3x + k4x);
            r.velocity = v0 + sixth * (k1v + 2f * k2v + 2f * k3v + k4v);
            // spinDecayPerSecond = 0 → exp(0) = 1 chính xác → xoáy giữ nguyên từng bit.
            r.spin = w * math.exp(-p.spinDecayPerSecond * dt);
            return r;
        }

        /// <summary>
        /// Chạy solver totalTime giây với bước dt.
        ///
        /// Số bước tính bằng số nguyên chứ không trừ dần một biến float: trừ dần tích luỹ
        /// sai số và đẻ ra một bước cuối dài vài nanô giây, khiến kết quả khác với việc gọi
        /// Step đúng N lần. Ở đây totalTime = N*dt cho ra ĐÚNG N bước, trùng từng bit.
        /// Phần dư (nếu totalTime không chia hết cho dt) đi thành một bước ngắn cuối cùng.
        /// </summary>
        [BurstCompile]
        public static BallState Integrate(in BallState s, in BallParams p, float totalTime, float dt)
        {
            BallState cur = s;

            if (!(dt > 0f) || !(totalTime > 0f))
                return cur;

            // +1e-4 hút các trường hợp 119.99998 về đúng 120.
            int steps = (int)math.floor(totalTime / dt + 1e-4f);
            for (int i = 0; i < steps; i++)
                cur = Step(cur, p, dt);

            float rest = totalTime - steps * dt;
            if (rest > 1e-6f)
                cur = Step(cur, p, rest);

            return cur;
        }
    }
}
