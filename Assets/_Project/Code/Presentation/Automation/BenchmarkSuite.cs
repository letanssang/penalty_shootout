using System;
using System.Collections.Generic;
using Unity.Mathematics;
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

            for (uint i = 0; i < StandardReplayCount; i++)
            {
                uint seed = 20260827u + i * 101u;
                var rng = new Unity.Mathematics.Random(seed);

                // Điểm ngắm và vận tốc phân bổ đều khắp khung thành
                float targetX = rng.NextFloat(-3.2f, 3.2f);
                float targetY = rng.NextFloat(0.4f, 2.2f);
                float speed = rng.NextFloat(22.0f, 32.0f);

                float3 launchVel = new float3(
                    targetX * 0.8f,
                    targetY * 0.9f,
                    speed
                );

                // Xoáy (spin) từ sút thẳng (knuckle ~0) đến xoáy mạnh 8-10 vòng/giây
                float3 spin = (i % 4 == 0)
                    ? float3.zero // Knuckleball
                    : new float3(rng.NextFloat(-2f, 2f), rng.NextFloat(-8f, 8f), rng.NextFloat(-2f, 2f));

                var kick = new ReplayKickData(
                    seed: seed,
                    shooterId: (int)(i % 5),
                    keeperId: 1,
                    strikeType: (int)(i % 4),
                    launchPosition: new float3(0f, 0.11f, 0f),
                    launchVelocity: launchVel,
                    spin: spin,
                    flightDuration: rng.NextFloat(0.40f, 0.55f),
                    result: (i % 3 == 0) ? (byte)2 : (byte)1 // Ghi bàn / Cản phá
                );

                list.Add(kick);
            }

            return list;
        }
    }
}
