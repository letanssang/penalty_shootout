using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Eleven.Core;
using Eleven.Keeper;

namespace Eleven.Presentation.Net
{
    /// <summary>
    /// Trình quản lý mô phỏng vật lý lưới Verlet hoàn chỉnh.
    /// Quản lý vòng đời NativeArray, lập lịch Job Burst và tích hợp cấu hình bật/tắt theo TierProfile.
    /// </summary>
    public sealed class NetSimulator : IDisposable
    {
        private NativeArray<NetParticle> _particles;
        private NativeArray<int2> _constraints;
        private NativeArray<float> _restLengths;
        private bool _isAllocated;

        public int ParticleCount => _isAllocated ? _particles.Length : 0;
        public int ConstraintCount => _isAllocated ? _constraints.Length : 0;
        public bool IsAllocated => _isAllocated;

        public NetSimulator(int cols = 17, int rows = 9, int depthSteps = 5)
        {
            var data = NetGridGenerator.GenerateBoxNet(cols, rows, depthSteps);

            _particles = new NativeArray<NetParticle>(data.particles, Allocator.Persistent);
            _constraints = new NativeArray<int2>(data.constraints, Allocator.Persistent);
            _restLengths = new NativeArray<float>(data.restLengths, Allocator.Persistent);
            _isAllocated = true;
        }

        public NativeArray<NetParticle> Particles => _particles;
        public NativeArray<int2> Constraints => _constraints;
        public NativeArray<float> RestLengths => _restLengths;

        /// <summary>
        /// Thực thi một bước mô phỏng vật lý lưới.
        /// </summary>
        public JobHandle ScheduleStep(float3 ballPosition, float3 ballVelocity, float ballRadius,
                                      float dt, float damping = 0.94f, int iterations = 6,
                                      bool isSimulationEnabled = true, JobHandle dependency = default)
        {
            if (!_isAllocated || !isSimulationEnabled || dt <= 0f)
            {
                return dependency;
            }

            var job = new NetStepJob
            {
                particles = _particles,
                constraints = _constraints,
                restLengths = _restLengths,
                ballPosition = ballPosition,
                ballVelocity = ballVelocity,
                ballRadius = ballRadius,
                dt = dt,
                damping = damping,
                iterations = iterations
            };

            return job.Schedule(dependency);
        }

        /// <summary>
        /// Thực thi bước mô phỏng và đợi hoàn tất ngay lập tức (tiện cho Unit Test hoặc main thread sync).
        /// </summary>
        public void StepSynchronous(float3 ballPosition, float3 ballVelocity, float ballRadius,
                                    float dt, float damping = 0.94f, int iterations = 6,
                                    bool isSimulationEnabled = true)
        {
            var handle = ScheduleStep(ballPosition, ballVelocity, ballRadius, dt, damping, iterations, isSimulationEnabled);
            handle.Complete();
        }

        /// <summary>
        /// Bước mô phỏng tương tác 2 chiều giữa quả bóng và lưới:
        /// Lưới căng làm tiêu hao động năng quả bóng và giữ bóng nằm gọn trong túi lưới.
        /// </summary>
        public void StepWithBall(ref float3 ballPosition, ref float3 ballVelocity, float ballRadius,
                                 float dt, float damping = 0.94f, int iterations = 6,
                                 bool isSimulationEnabled = true)
        {
            if (!isSimulationEnabled || dt <= 0f)
            {
                ballPosition += ballVelocity * dt;
                return;
            }

            // Bước vị trí bóng
            ballPosition += ballVelocity * dt;

            // Thực thi mô phỏng hạt lưới phản ứng với bóng
            StepSynchronous(ballPosition, ballVelocity, ballRadius, dt, damping, iterations, true);

            // Kiểm tra lực căng lưới cản bóng (khi bóng bay qua vạch vôi z >= 11.0m)
            if (ballPosition.z >= GoalFrame.PenaltyDistance)
            {
                float backNetZ = GoalFrame.PenaltyDistance + NetGridGenerator.TopDepth; // ~12.0m

                if (ballPosition.z + ballRadius > backNetZ)
                {
                    float penetration = (ballPosition.z + ballRadius) - backNetZ;
                    // Lực cản đàn hồi tăng dần khi lưới căng
                    float decel = 1800.0f * (penetration / 0.5f);
                    float newSpeedZ = math.max(-1.5f, ballVelocity.z - decel * dt);
                    ballVelocity.z = newSpeedZ;

                    // Giảm dần vận tốc ngang X và Y do ma sát bề mặt lưới
                    ballVelocity.x *= math.saturate(1.0f - 8.0f * dt);
                    ballVelocity.y *= math.saturate(1.0f - 8.0f * dt);
                }
            }
        }

        /// <summary>
        /// Tính toán vận tốc cực đại hiện tại giữa các hạt tự do để kiểm tra trạng thái đứng yên / ổn định.
        /// </summary>
        public float GetMaxParticleSpeed(float dt)
        {
            if (!_isAllocated || dt <= 0f) return 0f;

            float maxSpeed = 0f;
            for (int i = 0; i < _particles.Length; i++)
            {
                var p = _particles[i];
                if (p.pinned == 0)
                {
                    float speed = math.distance(p.position, p.prevPosition) / dt;
                    if (speed > maxSpeed) maxSpeed = speed;
                }
            }

            return maxSpeed;
        }

        public void Dispose()
        {
            if (_isAllocated)
            {
                if (_particles.IsCreated) _particles.Dispose();
                if (_constraints.IsCreated) _constraints.Dispose();
                if (_restLengths.IsCreated) _restLengths.Dispose();
                _isAllocated = false;
            }
        }
    }
}
