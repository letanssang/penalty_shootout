using System;
using Unity.Mathematics;
using Eleven.Match;

namespace Eleven.Presentation
{
    /// <summary>
    /// Bộ điều phối góc máy máy ảnh (Camera Director) cho trận đấu luân lưu.
    /// Quản lý chuyển đổi góc quay, gắn pha lượt sút với góc máy và đảm bảo giới hạn vùng dựng.
    /// </summary>
    public sealed class CameraDirector : ICameraDirector
    {
        private readonly CameraShot[] _phaseBindings = new CameraShot[8];

        public CameraShot CurrentShot { get; private set; } = CameraShot.BehindShooter;
        public CameraShot PreviousShot { get; private set; } = CameraShot.BehindShooter;

        public float BlendDuration { get; private set; }
        public float BlendElapsed { get; private set; }
        public bool IsBlending => BlendElapsed < BlendDuration;

        /// <summary>
        /// Sự kiện phát ra khi góc máy thay đổi.
        /// Tham số: (Góc cũ, Góc mới, Thời gian blend).
        /// </summary>
        public event Action<CameraShot, CameraShot, float> OnShotChanged;

        public CameraDirector()
        {
            SetDefaultPhaseBindings();
        }

        /// <summary>
        /// Thiết lập cấu hình góc máy mặc định cho các pha của lượt sút:
        /// - Pha ngắm và sút (Placing, Aiming, RunUp, Contact, Flight): BehindShooter (góc tĩnh chuẩn).
        /// - Pha giải quyết (Resolution): Broadcast.
        /// - Pha phản ứng (Reaction): KickerFace — cận mặt người sút.
        /// - Pha kết thúc (Complete): BehindShooter (chuẩn bị lượt tiếp theo).
        /// </summary>
        public void SetDefaultPhaseBindings()
        {
            _phaseBindings[(int)KickPhase.Placing] = CameraShot.BehindShooter;
            _phaseBindings[(int)KickPhase.Aiming] = CameraShot.BehindShooter;
            _phaseBindings[(int)KickPhase.RunUp] = CameraShot.BehindShooter;
            _phaseBindings[(int)KickPhase.Contact] = CameraShot.BehindShooter;
            _phaseBindings[(int)KickPhase.Flight] = CameraShot.BehindShooter;
            _phaseBindings[(int)KickPhase.Resolution] = CameraShot.Broadcast;
            _phaseBindings[(int)KickPhase.Reaction] = CameraShot.KickerFace;
            _phaseBindings[(int)KickPhase.Complete] = CameraShot.BehindShooter;
        }

        public void BindToPhase(KickPhase phase, CameraShot shot)
        {
            int idx = (int)phase;
            if (idx >= 0 && idx < _phaseBindings.Length)
            {
                _phaseBindings[idx] = shot;
            }
        }

        public CameraShot GetShotForPhase(KickPhase phase)
        {
            int idx = (int)phase;
            if (idx >= 0 && idx < _phaseBindings.Length)
            {
                return _phaseBindings[idx];
            }
            return CameraShot.BehindShooter;
        }

        /// <summary>
        /// Lắng nghe sự kiện chuyển pha từ IKickSequencer để tự động đổi góc quay theo cấu hình đã gắn.
        /// </summary>
        public void OnKickPhaseChanged(KickPhase oldPhase, KickPhase newPhase)
        {
            CameraShot targetShot = GetShotForPhase(newPhase);
            if (targetShot != CurrentShot)
            {
                // Mặc định blend 0.3s khi đổi sang góc Reaction/Resolution, cắt tức thì khi về đầu lượt
                float blend = (newPhase == KickPhase.Placing || newPhase == KickPhase.Complete) ? 0.0f : 0.3f;
                CutTo(targetShot, blend);
            }
        }

        public void CutTo(CameraShot shot, float blendSeconds)
        {
            CameraShot old = CurrentShot;
            PreviousShot = old;
            CurrentShot = shot;
            BlendDuration = math.max(0.0f, blendSeconds);
            BlendElapsed = 0.0f;

            OnShotChanged?.Invoke(old, shot, BlendDuration);
        }

        /// <summary>
        /// Cập nhật tiến trình blend theo delta time.
        /// </summary>
        public void Tick(float dt)
        {
            if (IsBlending)
            {
                BlendElapsed = math.min(BlendDuration, BlendElapsed + dt);
            }
        }

        public bool IsWithinAuthoredBounds(in float3 position)
        {
            return CameraAuthoredBounds.IsWithin(position);
        }
    }
}
