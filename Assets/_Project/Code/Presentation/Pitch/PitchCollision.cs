using Unity.Mathematics;
using Eleven.Ball;
using Eleven.Keeper;
using Eleven.Presentation.Crowd;

namespace Eleven.Presentation
{
    /// <summary>
    /// Bóng va vào cái gì sau khi cú sút đã được chấm điểm.
    ///
    /// VÌ SAO CÓ LỚP NÀY. BallSolver chỉ biết khí động — trọng lực, cản, Magnus. Nó không
    /// biết mặt cỏ, không biết lưới, không biết khán đài, và nó KHÔNG NÊN biết: giữ solver
    /// thuần là điều luật của dự án. Nhưng phải có ai đó biết, nếu không quả bóng cứ thế
    /// rơi xuyên qua sân. Người đó là lớp này.
    ///
    /// Để static và thuần (không MonoBehaviour, không Time.deltaTime) để test được bằng
    /// EditMode: hành vi "bóng dừng ở đâu" là thứ người chơi nhìn thấy rõ nhất, nên nó
    /// đáng được chốt bằng test chứ không phải bằng mắt.
    ///
    /// Đây là mô hình THÔ, cố ý. Khán đài coi như bậc thang đặc; lưới coi như một hộp hãm
    /// vận tốc. Không có va chạm lồi lõm, không có ma sát xoáy. Đủ để quả bóng dừng lại ở
    /// chỗ trông hợp lý và không biến mất — quá mức đó là công sức đổ vào thứ người chơi
    /// chỉ nhìn nửa giây sau khi đã biết bàn thắng.
    /// </summary>
    public static class PitchCollision
    {
        /// <summary>Nảy còn 45% vận tốc dọc — cỏ ướt, bóng thi đấu.</summary>
        public const float Restitution = 0.45f;

        /// <summary>Ma sát trượt mỗi lần chạm đất: giữ lại 80% vận tốc ngang.</summary>
        public const float GroundFriction = 0.80f;

        /// <summary>Hệ số hãm trong lòng lưới (1/s). Chính nó làm cú sút "găm" chứ không xuyên.</summary>
        public const float NetDamping = 7.5f;

        /// <summary>Lưới sâu bao nhiêu mét sau vạch vôi.</summary>
        public const float NetDepth = 1.8f;

        /// <summary>Dưới tốc độ này và đã chạm đất thì coi như bóng chết hẳn.</summary>
        public const float RestSpeed = 0.35f;

        // Mép đồ hoạ thật: PitchGround là Plane scale 4.2 tại z = 6, tức x ∈ [-21, 21],
        // z ∈ [-15, 27]. Lấy vào trong một chút cho chắc.
        public const float PitchHalfWidth = 20.0f;
        public const float PitchMinZ = -14.0f;
        public const float PitchMaxZ = 26.0f;

        /// <summary>
        /// Cao độ của mặt cứng tại (x, z): 0 trên cỏ, còn trên khán đài thì là mặt bậc.
        /// Khán đài trong scene là những khối hộp đặc từ đất lên, nên "mặt" của nó chính
        /// là nóc hộp — bóng đá lên khán đài sẽ nằm trên bậc chứ không lọt xuống chân tường.
        /// </summary>
        public static float SurfaceHeight(float x, float z)
        {
            float h = 0f;

            // Khán đài chính phía sau khung thành, bề ngang 30m (x ∈ [-15, 15]).
            if (z >= CrowdStandLayout.MainStandFrontZ && math.abs(x) <= 15f)
            {
                int row = (int)math.floor((z - CrowdStandLayout.MainStandFrontZ) / CrowdStandLayout.RowDepth);
                row = math.clamp(row, 0, CrowdStandLayout.MainStandRows - 1);
                h = math.max(h, CrowdStandLayout.FirstRowHeight + row * CrowdStandLayout.RowRise);
            }

            // Hai khán đài cánh, trải z ∈ [-9, 17].
            float ax = math.abs(x);
            if (ax >= CrowdStandLayout.WingStandStartX && z >= -9f && z <= 17f)
            {
                int row = (int)math.floor((ax - CrowdStandLayout.WingStandStartX) / CrowdStandLayout.RowDepth);
                row = math.clamp(row, 0, CrowdStandLayout.WingStandRows - 1);
                h = math.max(h, CrowdStandLayout.FirstRowHeight + row * CrowdStandLayout.RowRise);
            }

            return h;
        }

        /// <summary>Bóng có đang nằm trong lòng lưới không.</summary>
        public static bool IsInsideNet(float3 pos)
        {
            return pos.z >= GoalFrame.PenaltyDistance &&
                   pos.z <= GoalFrame.PenaltyDistance + NetDepth &&
                   math.abs(pos.x) <= GoalFrame.Width * 0.5f &&
                   pos.y <= GoalFrame.Height;
        }

        /// <summary>Bóng đã ra khỏi vùng có đồ hoạ chưa — ra rồi thì đóng băng, không ai nhìn nữa.</summary>
        public static bool IsOutOfWorld(float3 pos)
        {
            return math.abs(pos.x) > PitchHalfWidth || pos.z < PitchMinZ || pos.z > PitchMaxZ;
        }

        /// <summary>
        /// Áp lưới, mặt cứng và điều kiện dừng cho MỘT bước solver.
        /// Trả về true nếu <paramref name="result"/> khác trạng thái vào — khi đó phải ghi đè
        /// lại vào BallDriver. <paramref name="atRest"/> bật khi bóng đã chết hẳn và vòng lặp
        /// nên Freeze driver lại: để nó chạy tiếp là đốt CPU cho một quả bóng đứng yên.
        /// </summary>
        public static bool Resolve(in BallState s, float dt, float ballRadius,
                                   out BallState result, out bool atRest)
        {
            float3 pos = s.position;
            float3 vel = s.velocity;
            float3 spin = s.spin;
            bool changed = false;
            atRest = false;

            if (IsOutOfWorld(pos))
            {
                result = s;
                atRest = true;
                return false;
            }

            if (IsInsideNet(pos))
            {
                vel *= 1f - NetDamping * dt;
                changed = true;
            }

            float floor = SurfaceHeight(pos.x, pos.z) + ballRadius;
            if (pos.y < floor)
            {
                pos.y = floor;
                if (vel.y < 0f) vel.y = -vel.y * Restitution;
                vel.x *= GroundFriction;
                vel.z *= GroundFriction;
                spin *= GroundFriction;
                changed = true;

                // Chạm đất mà đã chậm thì thôi, đừng nảy lăn tăn mãi.
                if (math.lengthsq(vel) < RestSpeed * RestSpeed)
                {
                    vel = float3.zero;
                    spin = float3.zero;
                    atRest = true;
                }
            }

            result = new BallState(pos, vel, spin);
            return changed;
        }
    }
}
