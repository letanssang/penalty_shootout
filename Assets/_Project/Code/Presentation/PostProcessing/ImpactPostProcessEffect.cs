using System;
using Unity.Mathematics;

namespace Eleven.Presentation
{
    /// <summary>
    /// Bộ điều khiển hiệu ứng sai lệch màu tức thời (Transient Chromatic Aberration) khi chạm bóng.
    /// Thời lượng kích hoạt luôn được đảm bảo &lt; 200 ms và không cấp phát GC.
    /// </summary>
    public sealed class ImpactPostProcessEffect
    {
        public const float MaxAllowedDurationSeconds = 0.20f; // Bắt buộc dưới 200ms

        public float CurrentIntensity { get; private set; }
        public float RemainingDuration { get; private set; }
        public float TotalDuration { get; private set; }
        public bool IsActive => RemainingDuration > 0f && CurrentIntensity > 0f;

        private float _peakIntensity;

        public void TriggerImpact(float intensity = 0.6f, float duration = 0.12f)
        {
            float clampedDuration = math.clamp(duration, 0.01f, MaxAllowedDurationSeconds);
            float clampedIntensity = math.clamp(intensity, 0.0f, 1.0f);

            _peakIntensity = clampedIntensity;
            TotalDuration = clampedDuration;
            RemainingDuration = clampedDuration;
            CurrentIntensity = clampedIntensity;
        }

        public void Reset()
        {
            CurrentIntensity = 0.0f;
            RemainingDuration = 0.0f;
            TotalDuration = 0.0f;
            _peakIntensity = 0.0f;
        }

        /// <summary>
        /// Cập nhật hiệu ứng theo thời gian delta time, giảm dần cường độ về 0.
        /// </summary>
        public void Tick(float dt)
        {
            if (!IsActive) return;

            RemainingDuration = math.max(0.0f, RemainingDuration - dt);

            if (RemainingDuration <= 0.0f)
            {
                CurrentIntensity = 0.0f;
            }
            else
            {
                // Giảm dần tuyến tính theo thời gian còn lại
                float progress = RemainingDuration / TotalDuration;
                CurrentIntensity = _peakIntensity * progress;
            }
        }
    }
}
