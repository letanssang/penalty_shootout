using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Eleven.Ball
{
    /// <summary>Một điểm lấy mẫu trên quỹ đạo dự đoán.</summary>
    public struct TrajectorySample
    {
        public float3 position;
        public float time;
    }

    /// <summary>
    /// Chạy BallSolver tới trước để lấy quỹ đạo, không chạm vào MonoBehaviour hay Time.
    /// Ghi thẳng vào NativeArray truyền vào — không cấp phát — vì T16-T21 gọi hàm này
    /// mỗi khung để thủ môn dự đoán, và T33 gọi hàng nghìn lần trong test hồi quy.
    /// </summary>
    [BurstCompile]
    public static class TrajectoryPredictor
    {
        /// <summary>
        /// Giới hạn an toàn cho FirstCrossing khi bóng không bao giờ tới mặt phẳng
        /// (ví dụ bay ngược hướng, hoặc dt quá nhỏ). 30s bay là dư sức cho mọi tình
        /// huống penalty thật — không có quả sút nào bay quá vài giây.
        /// </summary>
        const float SafetyMaxTime = 30f;

        public static int Predict(in BallState start, in BallParams p,
                                  float dt, float maxTime,
                                  NativeArray<TrajectorySample> buffer)
        {
            if (buffer.Length == 0 || !(dt > 0f) || !(maxTime > 0f))
                return 0;

            int count = 0;
            buffer[count] = new TrajectorySample { position = start.position, time = 0f };
            count++;

            BallState cur = start;
            float t = 0f;

            while (count < buffer.Length && t + dt <= maxTime + 1e-4f)
            {
                cur = BallSolver.Step(cur, p, dt);
                t += dt;
                buffer[count] = new TrajectorySample { position = cur.position, time = t };
                count++;
            }

            return count;
        }

        /// <summary>Giao điểm đầu tiên với mặt phẳng z = planeZ, nội suy tuyến tính giữa hai bước.</summary>
        public static bool FirstCrossing(in BallState start, in BallParams p,
                                         float planeZ, float dt,
                                         out float3 point, out float time)
        {
            point = float3.zero;
            time = 0f;

            if (!(dt > 0f))
                return false;

            int maxSteps = (int)(SafetyMaxTime / dt) + 1;

            BallState prev = start;
            float tPrev = 0f;
            float zPrev = prev.position.z;

            for (int i = 0; i < maxSteps; i++)
            {
                BallState cur = BallSolver.Step(prev, p, dt);
                float tCur = tPrev + dt;
                float zCur = cur.position.z;

                // Đổi dấu so với mặt phẳng giữa hai bước liên tiếp = đã cắt qua.
                bool crossed = (zPrev < planeZ) != (zCur < planeZ);

                if (crossed)
                {
                    float denom = zCur - zPrev;
                    float frac = denom != 0f ? math.saturate((planeZ - zPrev) / denom) : 0f;
                    point = math.lerp(prev.position, cur.position, frac);
                    time = math.lerp(tPrev, tCur, frac);
                    return true;
                }

                prev = cur;
                tPrev = tCur;
                zPrev = zCur;
            }

            return false;
        }
    }
}
