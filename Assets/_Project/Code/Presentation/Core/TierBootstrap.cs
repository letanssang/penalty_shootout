using UnityEngine;

namespace Eleven.Core
{
    /// <summary>
    /// Đặt một cái duy nhất trong scene Boot. Nhiệm vụ: gọi DeviceTier.Initialize đúng một lần
    /// với 3 profile theo thứ tự A/B/C. Không có nó, DeviceTier.CurrentProfile mãi là null
    /// và targetFrameRate của bậc không bao giờ được áp.
    /// Sinh tự động bởi Eleven > Phase 0 > Generate Boot Scene.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class TierBootstrap : MonoBehaviour
    {
        [Tooltip("Đúng 3 phần tử, thứ tự A, B, C.")]
        [SerializeField] TierProfile[] profiles = new TierProfile[3];

        void Awake()
        {
            DontDestroyOnLoad(gameObject);

            if (profiles == null || profiles.Length != 3 || profiles[0] == null || profiles[1] == null || profiles[2] == null)
            {
                Debug.LogError("[TierBootstrap] Thiếu TierProfile A/B/C — chạy menu Eleven > Phase 0 > Generate Tier Assets rồi gán lại.");
                return;
            }

            DeviceTier.Initialize(profiles);
            Debug.Log($"[TierBootstrap] Bậc phát hiện được: {DeviceTier.Current} " +
                      $"(model {SystemInfo.deviceModel}, RAM {SystemInfo.systemMemorySize}MB, {SystemInfo.processorCount} lõi)");
        }
    }
}
