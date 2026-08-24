using UnityEngine;

namespace Eleven.Core
{
    /// <summary>
    /// Ngân sách tính năng của một bậc. Giá trị khớp bảng "Ba bậc thiết bị" trong docs/plan.md:
    /// renderScale 1.0/0.80/0.65 · targetFrameRate 60/60/30 · grassDensity 1.0/0.4/0.0 ·
    /// textureMemoryBudgetMB 400/250/140.
    /// Mọi hệ thống đồ hoạ PHẢI hỏi qua profile này chứ không tự quyết.
    /// </summary>
    [CreateAssetMenu(fileName = "TierProfile", menuName = "Eleven/Tier Profile")]
    public class TierProfile : ScriptableObject
    {
        public QualityTier tier;
        public float renderScale = 1.0f;             // 1.0 / 0.80 / 0.65
        public int targetFrameRate = 60;             // 60  / 60   / 30
        public float grassDensity = 1.0f;            // 1.0 / 0.4  / 0.0
        public bool netSimulation = true;            // lưới Verlet: tắt ở bậc C (lưới tĩnh)
        public bool subsurfaceScattering = true;     // tắt ở bậc C
        public bool lightShafts = true;              // tắt ở bậc B và C
        public int textureMemoryBudgetMB = 400;      // 400 / 250 / 140
    }
}
