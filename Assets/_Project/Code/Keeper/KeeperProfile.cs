using System;
using UnityEngine;

namespace Eleven.Keeper
{
    /// <summary>
    /// Cấu hình hồ sơ năng lực của thủ môn (T16, T25).
    /// Chứa các thuộc tính vật lý, phản xạ và độ thông minh (đọc vị, trí nhớ).
    /// </summary>
    [CreateAssetMenu(fileName = "KeeperProfile", menuName = "Eleven/Keeper/KeeperProfile")]
    public class KeeperProfile : ScriptableObject
    {
        [Tooltip("Xác suất đọc vị đúng hướng (0..1)")]
        [Range(0f, 1f)]
        public float readAccuracy = 0.52f;

        [Tooltip("Độ trễ phản ứng thần kinh sau khi cam kết (ms)")]
        public float reactionMs = 240f;

        [Tooltip("Thời điểm cam kết so với lúc chạm bóng (ms). Âm = cam kết trước khi sút")]
        public float commitOffsetMs = -110f;

        [Tooltip("Hệ số nhân tầm với/tốc độ di chuyển chuẩn (khuyến nghị 0.92..1.06, kẹp cứng [0.85, 1.10])")]
        [Range(0.85f, 1.10f)]
        public float reachScale = 1.0f;

        [Tooltip("Xác suất đẩy bóng ra thay vì bắt dính khi chạm được bóng (0..1)")]
        [Range(0f, 1f)]
        public float parryChance = 0.45f;

        [Tooltip("Trọng số trí nhớ thói quen người sút (0..1, 0 = không nhớ)")]
        [Range(0f, 1f)]
        public float memoryWeight = 0.50f;

        /// <summary>
        /// Tạo profile mặc định (cấp độ Thường) dùng cho runtime hoặc testing mà không cần asset.
        /// </summary>
        public static KeeperProfile CreateDefault() => CreateMedium();

        public static KeeperProfile CreateEasy()
        {
            var p = CreateInstance<KeeperProfile>();
            p.readAccuracy = 0.30f;
            p.reactionMs = 320f;
            p.commitOffsetMs = -60f;
            p.reachScale = 0.92f;
            p.parryChance = 0.70f;
            p.memoryWeight = 0.20f;
            return p;
        }

        public static KeeperProfile CreateMedium()
        {
            var p = CreateInstance<KeeperProfile>();
            p.readAccuracy = 0.52f;
            p.reactionMs = 240f;
            p.commitOffsetMs = -110f;
            p.reachScale = 1.00f;
            p.parryChance = 0.45f;
            p.memoryWeight = 0.50f;
            return p;
        }

        public static KeeperProfile CreateHard()
        {
            var p = CreateInstance<KeeperProfile>();
            p.readAccuracy = 0.72f;
            p.reactionMs = 185f;
            p.commitOffsetMs = -150f;
            p.reachScale = 1.06f;
            p.parryChance = 0.28f;
            p.memoryWeight = 0.80f;
            return p;
        }
    }
}
