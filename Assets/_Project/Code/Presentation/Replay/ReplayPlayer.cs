using System;
using Unity.Mathematics;
using Eleven.Ball;
using Eleven.Shooter;
using Eleven.Match;

namespace Eleven.Presentation
{
    /// <summary>
    /// Trình điều khiển phát lại (Replay Player) tất định của lượt sút.
    /// Hỗ trợ phát lại ở nhiều tốc độ (0.25x, 0.5x, 1.0x) và kiểm chứng tính toàn vẹn quỹ đạo.
    /// </summary>
    public sealed class ReplayPlayer
    {
        public const float DefaultSimDt = 1f / 240f;

        public bool IsPlaying { get; private set; }
        public float PlaybackSpeed { get; private set; } = 1.0f;
        public float ElapsedSimTime { get; private set; }
        public BallState CurrentBallState { get; private set; }
        public bool HasCompleted { get; private set; }

        private ReplayKickData _currentData;
        private BallParams _ballParams;
        private float _accumulator;

        public ReplayPlayer()
        {
            _ballParams = BallParams.Default;
        }

        /// <summary>
        /// Nạp dữ liệu lượt sút và sẵn sàng phát lại.
        /// </summary>
        public void Load(in ReplayKickData data, BallParams? customParams = null)
        {
            _currentData = data;
            _ballParams = customParams ?? BallParams.Default;
            Reset();
        }

        public void SetPlaybackSpeed(float speed)
        {
            PlaybackSpeed = math.clamp(speed, 0.1f, 4.0f);
        }

        public void Play()
        {
            IsPlaying = true;
        }

        public void Pause()
        {
            IsPlaying = false;
        }

        public void Reset()
        {
            IsPlaying = false;
            HasCompleted = false;
            ElapsedSimTime = 0.0f;
            _accumulator = 0.0f;

            // Khởi tạo trạng thái ban đầu của quả bóng tại chấm 11m
            float3 origin = new float3(0f, _ballParams.radius, 0f);
            float3 target = _currentData.intent.aimPoint;
            float3 dir = math.normalize(target - origin);
            float3 initVelocity = dir * _currentData.intent.speed;

            CurrentBallState = new BallState
            {
                position = origin,
                velocity = initVelocity,
                spin = _currentData.intent.spin
            };
        }

        /// <summary>
        /// Bước phát lại theo thời gian thực dt.
        /// Tự động scale thời gian theo PlaybackSpeed và chia nhỏ thành các bước SimDt cố định để đảm bảo tất định.
        /// </summary>
        public void Tick(float realDt)
        {
            if (!IsPlaying || HasCompleted) return;

            float scaledDt = realDt * PlaybackSpeed;
            _accumulator += scaledDt;

            while (_accumulator >= DefaultSimDt)
            {
                _accumulator -= DefaultSimDt;
                ElapsedSimTime += DefaultSimDt;

                var state = CurrentBallState;
                CurrentBallState = BallSolver.Step(in state, in _ballParams, DefaultSimDt);

                // Kiểm tra điều kiện kết thúc: bóng chạm vạch vôi hoặc rơi quá thấp
                if (CurrentBallState.position.z >= GoalGeometry.PenaltyDistance || CurrentBallState.position.y < 0f)
                {
                    HasCompleted = true;
                    IsPlaying = false;
                    break;
                }
            }
        }

        /// <summary>
        /// Kiểm chứng tính tất định của lượt sút: chạy lại toàn bộ mô phỏng và so sánh
        /// từng thông số (crossing position, cell, outcome) với dữ liệu đã ghi lại.
        /// </summary>
        public static bool VerifyTrajectory(in ReplayKickData data, in BallParams ballParams,
                                           float tolerance, out string mismatchReport)
        {
            mismatchReport = null;

            float3 origin = new float3(0f, ballParams.radius, 0f);
            float3 target = data.intent.aimPoint;
            float3 dir = math.normalize(target - origin);
            float3 initVel = dir * data.intent.speed;

            var startState = new BallState
            {
                position = origin,
                velocity = initVel,
                spin = data.intent.spin
            };

            ShotOutcome outcome = GoalGeometry.Classify(in startState, in ballParams,
                                                      out float3 crossing, out int cell);

            if (outcome != data.expectedOutcome)
            {
                mismatchReport = $"Lệch kết quả: ghi nhận {data.expectedOutcome}, chạy lại ra {outcome}.";
                return false;
            }

            if (cell != data.expectedCell)
            {
                mismatchReport = $"Lệch ô khung thành: ghi nhận ô {data.expectedCell}, chạy lại ra ô {cell}.";
                return false;
            }

            float dist = math.distance(crossing, data.expectedCrossing);
            if (dist > tolerance)
            {
                mismatchReport = $"Lệch điểm giao cắt khung thành: ghi nhận {data.expectedCrossing}, " +
                                 $"chạy lại {crossing}, sai số {dist:E3} vượt quá dung sai {tolerance:E3}.";
                return false;
            }

            return true;
        }
    }
}
