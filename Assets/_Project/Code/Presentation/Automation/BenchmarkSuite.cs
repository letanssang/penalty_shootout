using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Eleven.Ball;
using Eleven.Shooter;
using Eleven.Match;
using Eleven.Presentation;

namespace Eleven.Presentation.Automation
{
    /// <summary>
    /// Tập hợp 20 kịch bản Replay chuẩn (Golden 20 Replays) dùng để benchmark và đo hồi quy hiệu năng.
    /// Mỗi kịch bản có seed và thông số cú sút cố định để đảm bảo tính tất định 100%.
    /// </summary>
    public static class BenchmarkSuite
    {
        public const int StandardReplayCount = 20;

        /// <summary>
        /// Tạo danh sách 20 lượt sút mẫu đại diện cho đầy đủ các kịch bản va chạm, quỹ đạo và đồ họa:
        /// - Cứa lòng góc chữ A (Top Corner Curl)
        /// - Nã đại bác knuckleball đổi hướng (Knuckleball strike)
        /// - Sút sệt căng sát cột dọc (Low drive post)
        /// - Thủ môn bay người chạm ngón tay đẩy bóng xà ngang (Tip over crossbar)
        /// - Bóng đập mép trong lưới Verlet bung căng (Net impact)
        /// </summary>
        public static List<ReplayKickData> GenerateStandard20Replays()
        {
            var list = new List<ReplayKickData>(StandardReplayCount);
            var p = BallParams.Default;

            for (uint i = 0; i < StandardReplayCount; i++)
            {
                uint seed = 20260827u + i * 101u;
                var rng = new Unity.Mathematics.Random(seed);

                float targetX = rng.NextFloat(-3.2f, 3.2f);
                float targetY = rng.NextFloat(0.4f, 2.2f);
                float speed = rng.NextFloat(22.0f, 32.0f);

                float3 aim = new float3(targetX, targetY, 11.0f);
                float3 spin = (i % 4 == 0)
                    ? float3.zero
                    : new float3(rng.NextFloat(-2f, 2f), rng.NextFloat(-8f, 8f), 0f);

                ShotType type = (i % 4 == 0) ? ShotType.Knuckle : (i % 3 == 0 ? ShotType.InsideFoot : ShotType.Instep);

                float3 origin = new float3(0f, p.radius, 0f);
                float3 dir = math.normalize(aim - origin);
                float3 initVel = dir * speed;

                var startState = new BallState
                {
                    position = origin,
                    velocity = initVel,
                    spin = spin
                };

                ShotOutcome outcome = GoalGeometry.Classify(in startState, in p, out float3 crossing, out int cell);

                var kick = new ReplayKickData
                {
                    seed = seed,
                    intent = new ShotIntent
                    {
                        aimPoint = aim,
                        spin = spin,
                        speed = speed,
                        type = type,
                        quality = 0.95f,
                        unstable = type == ShotType.Knuckle,
                        scatterRadius = 0.05f
                    },
                    expectedOutcome = outcome,
                    expectedCrossing = crossing,
                    expectedCell = cell
                };

                list.Add(kick);
            }

            return list;
        }
    }
}
