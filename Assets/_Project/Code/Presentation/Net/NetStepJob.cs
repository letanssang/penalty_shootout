using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Eleven.Presentation.Net
{
    /// <summary>
    /// Job tính toán một bước mô phỏng vật lý lưới Verlet và va chạm với quả bóng.
    /// Tối ưu hoá toàn diện cho Unity Burst, chạy trên worker thread không chặn main thread.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct NetStepJob : IJob
    {
        public NativeArray<NetParticle> particles;
        [ReadOnly] public NativeArray<int2> constraints;
        [ReadOnly] public NativeArray<float> restLengths;

        public float3 ballPosition;
        public float3 ballVelocity;
        public float ballRadius;

        /// <summary>
        /// Bán kính "với" tới hạt lưới, do <see cref="NetSimulator.InfluenceRadiusFor"/> tính
        /// từ cỡ ô lưới. Để 0 thì rơi về đúng bán kính quả bóng như trước.
        /// </summary>
        public float influenceRadius;

        public float dt;
        public float damping;
        public int iterations;

        public void Execute()
        {
            if (dt <= 0f || particles.Length == 0) return;

            float clampedDamping = math.clamp(damping, 0.80f, 0.95f);

            // 1. Tích phân Verlet cho từng hạt tự do
            for (int i = 0; i < particles.Length; i++)
            {
                NetParticle p = particles[i];
                if (p.pinned == 0)
                {
                    float3 vel = (p.position - p.prevPosition) * clampedDamping;
                    p.prevPosition = p.position;
                    p.position = p.position + vel;
                    particles[i] = p;
                }
            }

            // 2. Va chạm liên tục (CCD) giữa hạt và quỹ đạo quả bóng
            //
            // Bán kính dùng ở đây LỚN HƠN quả bóng thật, cố ý: lưới là mạng hạt thưa chứ
            // không phải mặt liền, giữa bốn hạt là một lỗ rộng bằng ô lưới. Lấy đúng 0.11m
            // thì quả bóng bay vào giữa ô chui lọt qua mà không hạt nào nhúc nhích — đúng
            // triệu chứng "lưới gần như không rung" báo ngày 2026-08-28. Xem
            // NetSimulator.InfluenceRadiusFor để biết con số ở đâu ra.
            float effectiveRadius = math.max(ballRadius + 0.02f, influenceRadius);
            float effectiveRadiusSq = effectiveRadius * effectiveRadius;
            float ballSpeed = math.length(ballVelocity);
            float3 ballDir = ballSpeed > 1e-4f ? ballVelocity / ballSpeed : new float3(0f, 0f, 1f);

            // Xung lực truyền cho hạt bị chạm, tính bằng m/s. Chặn trần vì cú sút 30 m/s mà
            // truyền thẳng tỉ lệ thì hạt lưới bắn đi cả mét trong một khung hình, trông như
            // lưới nổ chứ không phải lưới rung. Trần 3 m/s cho độ võng chừng 0.3m trong hai
            // phần mười giây — đúng tầm một pha bóng găm lưới thật.
            float kickSpeed = math.min(ballSpeed * 0.35f, 3.0f);

            float3 prevBallPos = ballPosition - ballVelocity * dt;
            float3 ballTraj = ballPosition - prevBallPos;
            float trajLenSq = math.dot(ballTraj, ballTraj);

            for (int i = 0; i < particles.Length; i++)
            {
                NetParticle p = particles[i];
                if (p.pinned == 0)
                {
                    float t = trajLenSq > 1e-6f
                        ? math.saturate(math.dot(p.position - prevBallPos, ballTraj) / trajLenSq)
                        : 1.0f;
                    float3 closestBallCenter = prevBallPos + t * ballTraj;
                    float3 toParticle = p.position - closestBallCenter;
                    float distSq = math.dot(toParticle, toParticle);

                    if (distSq < effectiveRadiusSq)
                    {
                        float dist = math.sqrt(distSq);
                        float3 normal = dist > 1e-4f
                            ? toParticle / dist
                            : (math.lengthsq(ballVelocity) > 1e-4f ? math.normalize(ballVelocity) : new float3(0f, 0f, 1f));

                        p.position = closestBallCenter + normal * effectiveRadius;

                        // Truyền quán tính để lưới bung căng tự nhiên. Hướng hất pha giữa
                        // pháp tuyến (lưới ôm quanh bóng) và hướng bay (lưới bị đẩy về sau);
                        // mạnh nhất ở tâm va chạm, tắt dần ra rìa tầm với, nhờ vậy vết lõm
                        // loang ra thành sóng thay vì một cái hố vuông thành sắc cạnh.
                        float falloff = 1.0f - dist / effectiveRadius;
                        float3 kickDir = math.normalizesafe(normal * 0.4f + ballDir * 0.6f, normal);
                        p.prevPosition = p.position - kickDir * (kickSpeed * falloff * dt);
                        particles[i] = p;
                    }
                }
            }

            // 3. Giải ràng buộc khoảng cách (Relaxation) kết hợp ép va chạm bóng
            int loopCount = math.clamp(iterations, 1, 8);

            for (int iter = 0; iter < loopCount; iter++)
            {
                for (int cIdx = 0; cIdx < constraints.Length; cIdx++)
                {
                    int2 c = constraints[cIdx];
                    float rest = restLengths[cIdx];

                    NetParticle p1 = particles[c.x];
                    NetParticle p2 = particles[c.y];

                    float3 delta = p2.position - p1.position;
                    float dist = math.length(delta);

                    if (dist > 1e-5f)
                    {
                        float diff = (dist - rest) / dist;

                        if (p1.pinned == 0 && p2.pinned == 0)
                        {
                            p1.position += delta * (0.5f * diff);
                            p2.position -= delta * (0.5f * diff);
                        }
                        else if (p1.pinned == 0)
                        {
                            p1.position += delta * diff;
                        }
                        else if (p2.pinned == 0)
                        {
                            p2.position -= delta * diff;
                        }
                    }

                    // Chống xuyên thấu trong lúc relaxation
                    if (p1.pinned == 0)
                    {
                        float3 toP1 = p1.position - ballPosition;
                        float d1Sq = math.dot(toP1, toP1);
                        if (d1Sq < effectiveRadiusSq && d1Sq > 1e-6f)
                        {
                            p1.position = ballPosition + (toP1 / math.sqrt(d1Sq)) * effectiveRadius;
                        }
                    }

                    if (p2.pinned == 0)
                    {
                        float3 toP2 = p2.position - ballPosition;
                        float d2Sq = math.dot(toP2, toP2);
                        if (d2Sq < effectiveRadiusSq && d2Sq > 1e-6f)
                        {
                            p2.position = ballPosition + (toP2 / math.sqrt(d2Sq)) * effectiveRadius;
                        }
                    }

                    particles[c.x] = p1;
                    particles[c.y] = p2;
                }
            }
        }
    }
}
