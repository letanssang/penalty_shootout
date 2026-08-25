using System;
using UnityEngine;

namespace Eleven.Core
{
    /// <summary>
    /// Phát hiện và áp bậc chất lượng lúc chạy.
    /// PHÂN BẬC THEO NĂNG LỰC, KHÔNG THEO HỆ ĐIỀU HÀNH — không có #if UNITY_IOS trong logic.
    /// Ép bậc khi test: PlayerPrefs int "tier.override" = 0/1/2, rồi gọi RefreshOverride().
    /// </summary>
    public static class DeviceTier
    {
        public const string OverrideKey = "tier.override";

        static QualityTier current;
        static bool initialized;
        static TierProfile[] profiles;

        /// <summary>Bắn đúng một lần cho mỗi lần đổi bậc.</summary>
        public static event Action<QualityTier> OnTierChanged;

        /// <summary>Profile của bậc đang chạy. Null cho tới khi Initialize() được gọi.</summary>
        public static TierProfile CurrentProfile { get; private set; }

        public static QualityTier Current => current;

        /// <summary>Gọi một lần lúc khởi động, truyền 3 profile A/B/C theo thứ tự.</summary>
        public static void Initialize(TierProfile[] profilesInABCOrder)
        {
            profiles = profilesInABCOrder;
            Apply(Detect());
            initialized = true;
        }

        /// <summary>Suy ra bậc từ thông tin phần cứng. Tất định trên cùng một máy.</summary>
        public static QualityTier Detect()
        {
            int forced = PlayerPrefs.GetInt(OverrideKey, -1);
            if (forced >= 0 && forced <= 2)
                return (QualityTier)forced;

            // 1) Bảng lớp máy Apple: model identifier có sẵn trên mọi nền tảng qua deviceModel,
            //    đây là dữ kiện phần cứng chứ không phải rẽ nhánh theo HĐH.
            var tier = DetectAppleByModel();
            if (tier.HasValue)
                return tier.Value;

            // 2) Mọi máy còn lại (Android/desktop): chấm điểm năng lực từ SystemInfo.
            return DetectByCapability();
        }

        static QualityTier? DetectAppleByModel()
        {
            string model = SystemInfo.deviceModel ?? string.Empty;
            if (!model.StartsWith("iPhone", StringComparison.OrdinalIgnoreCase))
                return null; // không phải iPhone → để nhánh năng lực xử lý

            // iPhone13,* = iPhone 12 · iPhone14,* = iPhone 13 · iPhone15,* = iPhone 14 ...
            var parts = model.Split(',');
            if (parts.Length != 2 || !int.TryParse(parts[0].Substring(6), out int gen))
                return QualityTier.C; // chuỗi lạ → thận trọng, xếp bậc thấp

            if (gen >= 14) return QualityTier.A;   // iPhone 13 trở lên
            if (gen >= 11) return QualityTier.B;   // XR (11,8) → iPhone 12
            return QualityTier.C;
        }

        static QualityTier DetectByCapability()
        {
            int ramMB = SystemInfo.systemMemorySize;      // tổng RAM
            int vramMB = SystemInfo.graphicsMemorySize;   // VRAM/bộ nhớ GPU
            int cores = SystemInfo.processorCount;

            // Ngưỡng tham chiếu SD 8 Gen 1+ / flagship gần đây: ~8GB RAM, GPU >= 2GB, 8 lõi.
            int score = 0;
            if (ramMB >= 7000) score += 2; else if (ramMB >= 4500) score += 1;
            if (vramMB >= 2000) score += 2; else if (vramMB >= 1200) score += 1;
            if (cores >= 8) score += 1;

            // Máy 3GB RAM hoặc ít hơn luôn bị ép về C — HĐH sẽ giết app nếu không.
            if (ramMB <= 3200) return QualityTier.C;

            if (score >= 4) return QualityTier.A;
            if (score >= 2) return QualityTier.B;
            return QualityTier.C;
        }

        /// <summary>Đổi bậc lúc đang chạy. Idempotent nếu tier không đổi.</summary>
        public static void Apply(QualityTier tier)
        {
            bool changed = !initialized || current != tier;
            initialized = true;   // nếu không đặt ở đây, mọi Apply đều coi là "đổi" và bắn sự kiện lặp
            current = tier;
            CurrentProfile = profiles != null && profiles.Length == 3 ? profiles[(int)tier] : null;

            // Mỗi quality level trỏ đúng một URP asset — đổi level là đổi pipeline asset.
            // Trước khi chạy Generate Tier Assets thì chưa có đủ 3 level; SetQualityLevel ngoài
            // phạm vi sẽ ném lỗi, nên bỏ qua và chỉ cảnh báo.
            if ((int)tier < QualitySettings.names.Length)
                QualitySettings.SetQualityLevel((int)tier, true);
            else
                Debug.LogWarning($"[DeviceTier] Chưa có quality level cho bậc {tier} — " +
                                 "chạy menu Eleven > Phase 0 > Generate Tier Assets.");
            if (CurrentProfile != null)
                Application.targetFrameRate = CurrentProfile.targetFrameRate;

            if (changed)
                OnTierChanged?.Invoke(tier); // đúng một lần cho mỗi lần đổi
        }

        /// <summary>Gọi lại sau khi sửa PlayerPrefs "tier.override".</summary>
        public static void RefreshOverride() => Apply(Detect());
    }
}
