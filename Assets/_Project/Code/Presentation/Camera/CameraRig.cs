using Unity.Mathematics;
using UnityEngine;

namespace Eleven.Presentation
{
    /// <summary>
    /// Cầu nối giữa <see cref="CameraDirector"/> (logic thuần, không biết Transform) và
    /// camera thật trong scene. Director quyết định ĐANG Ở GÓC NÀO; rig này quyết định
    /// góc đó nằm ở đâu trong không gian và nội suy giữa hai góc.
    ///
    /// Vì sao tách: mọi góc quay phải nằm trong vùng 12m đã dựng (T26). Rig kẹp mọi vị trí
    /// bằng <see cref="CameraAuthoredBounds"/> ngay trước khi ghi vào Transform, nên không
    /// một đường blend hay cú rung máy nào có thể đẩy camera ra ngoài phần sân có đồ hoạ.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        /// <summary>Một góc quay dựng sẵn: chỗ đứng, chỗ nhìn, tiêu cự.</summary>
        public struct Pose
        {
            public float3 position;
            public float3 lookAt;
            public float fov;
        }

        [Header("Bám bóng")]
        [Tooltip("Tỉ lệ camera hướng theo bóng thay vì điểm nhìn dựng sẵn (0 = đứng yên, 1 = bám hoàn toàn).")]
        [SerializeField] private float ballTrackWeight = 0.35f;

        private UnityEngine.Camera _cam;
        private CameraDirector _director;

        private Pose _from;
        private Pose _to;

        private Transform _ballTransform;
        private bool _trackBall;

        // Rung máy: biên độ tắt dần theo thời gian, dao động bằng hàm lượng giác nên tất định.
        private float _shakeAmplitude;
        private float _shakeRemaining;
        private float _shakeDuration;

        // Quỹ đạo replay do MatchGameLoop điều khiển.
        private float _orbitYaw;
        private float _orbitPitch = 18f;
        private float _orbitDistance = 4.2f;
        private float3 _orbitCenter = new float3(0f, 1.22f, 11.0f);

        // Góc cận mặt người sút, CHỐT CỨNG tại khoảnh khắc cắt cảnh — xem SetKickerFace.
        // Mặc định nhắm vào chỗ chân trụ đặt xuống, để góc này vẫn dùng được khi chưa ai
        // gọi SetKickerFace (model chưa gán, hoặc xương đầu không lấy được).
        private float3 _faceEye = new float3(0.20f, 1.75f, 1.20f);
        private float3 _faceTarget = new float3(-0.35f, 1.65f, -0.15f);

        public CameraDirector Director => _director;
        public CameraShot CurrentShot => _director != null ? _director.CurrentShot : CameraShot.BehindShooter;

        private void Awake()
        {
            _cam = GetComponent<UnityEngine.Camera>();
            _director = new CameraDirector();
            _director.SetDefaultPhaseBindings();

            _to = PoseFor(CameraShot.BehindShooter);
            _from = _to;
            Apply(_to);
        }

        /// <summary>Gắn bóng để camera hơi bám theo trong pha bay — không bám thì cú sút trông chết cứng.</summary>
        public void BindBall(Transform ball)
        {
            _ballTransform = ball;
        }

        public void SetBallTracking(bool enabled)
        {
            _trackBall = enabled && _ballTransform != null;
        }

        /// <summary>Đổi góc quay. blendSeconds = 0 là cắt tức thì.</summary>
        public void SetShot(CameraShot shot, float blendSeconds)
        {
            if (_director == null) return;

            // Chốt pose hiện tại làm điểm xuất phát để blend không giật khi đang blend dở.
            _from = CurrentBlendedPose();
            _director.CutTo(shot, blendSeconds);
            _to = PoseFor(shot);

            if (blendSeconds <= 0f)
            {
                _from = _to;
                Apply(_to);
            }
        }

        /// <summary>Cập nhật thông số quỹ đạo cho góc replay. Giá trị được kẹp trong CameraAuthoredBounds.</summary>
        public void SetOrbit(float yawDeg, float pitchDeg, float distance, float3 center)
        {
            _orbitYaw = yawDeg;
            _orbitPitch = pitchDeg;
            _orbitDistance = distance;
            _orbitCenter = center;

            if (CurrentShot == CameraShot.ReplayOrbit)
            {
                _to = PoseFor(CameraShot.ReplayOrbit);
            }
        }

        /// <summary>
        /// Dựng góc cận mặt người sút từ vị trí ĐẦU THẬT và hướng người đang quay.
        ///
        /// Chốt một lần rồi đứng yên, KHÔNG bám đầu mỗi khung hình. Máy bám theo một cái đầu
        /// đang ăn mừng thì khung hình rung theo hoạt ảnh — trông như quay bằng tay chứ không
        /// như một cú cắt cảnh. Đứng yên thì đầu người tự chạy trong khung, đúng kiểu máy cận
        /// đặt sẵn trên sân.
        ///
        /// Máy đặt TRƯỚC MẶT (phía khung thành) nên nhìn ngược về phía người sút; chếch sang
        /// bên 0.55m để thành góc 3/4 chứ không phải ảnh thẻ, và nhích lên 0.08m cho ngang
        /// tầm mắt. Cách 1.46m với fov 26° cho khung cao 0.67m — vừa đủ đầu và vai.
        /// </summary>
        public void SetKickerFace(float3 headWorldPosition, float3 facingDirection)
        {
            float3 fwd = new float3(facingDirection.x, 0f, facingDirection.z);
            fwd = math.lengthsq(fwd) > 1e-6f ? math.normalize(fwd) : new float3(0f, 0f, 1f);
            float3 right = math.cross(new float3(0f, 1f, 0f), fwd);

            _faceTarget = headWorldPosition;
            _faceEye = headWorldPosition + fwd * 1.35f + right * 0.55f + new float3(0f, 0.08f, 0f);

            // Gọi giữa lúc đang ở góc này thì áp ngay, nếu không tư thế mới phải đợi cú cắt sau.
            if (CurrentShot == CameraShot.KickerFace)
            {
                _to = PoseFor(CameraShot.KickerFace);
                _from = _to;
                Apply(_to);
            }
        }

        /// <summary>Rung máy khi bóng nổ vào lưới / đập cột. Tất định: chỉ dùng hàm lượng giác theo thời gian còn lại.</summary>
        public void Shake(float amplitude, float duration)
        {
            _shakeAmplitude = math.max(_shakeAmplitude, amplitude);
            _shakeDuration = math.max(0.01f, duration);
            _shakeRemaining = _shakeDuration;
        }

        /// <summary>Gọi mỗi khung hình từ vòng lặp trận đấu (sau khi bóng đã cập nhật vị trí).</summary>
        public void Tick(float dt)
        {
            if (_director == null) return;

            _director.Tick(dt);
            if (_shakeRemaining > 0f) _shakeRemaining = math.max(0f, _shakeRemaining - dt);

            // Góc replay tính lại mỗi khung vì tâm quỹ đạo có thể đang chạy theo bóng.
            if (CurrentShot == CameraShot.ReplayOrbit) _to = PoseFor(CameraShot.ReplayOrbit);

            Apply(CurrentBlendedPose());
        }

        private Pose CurrentBlendedPose()
        {
            float t = 1f;
            if (_director.BlendDuration > 0f)
            {
                t = math.saturate(_director.BlendElapsed / _director.BlendDuration);
                t = t * t * (3f - 2f * t); // smoothstep: vào/ra mượt, không giật ở hai đầu
            }

            Pose p;
            p.position = math.lerp(_from.position, _to.position, t);
            p.lookAt = math.lerp(_from.lookAt, _to.lookAt, t);
            p.fov = math.lerp(_from.fov, _to.fov, t);
            return p;
        }

        private void Apply(in Pose pose)
        {
            float3 pos = pose.position;
            float3 look = pose.lookAt;

            // Bám bóng: kéo điểm nhìn về phía bóng theo trọng số, không kéo vị trí máy.
            if (_trackBall && _ballTransform != null)
            {
                float3 ball = (float3)(Vector3)_ballTransform.position;
                look = math.lerp(look, ball, math.saturate(ballTrackWeight));
            }

            if (_shakeRemaining > 0f && _shakeDuration > 0f)
            {
                float k = _shakeRemaining / _shakeDuration;
                float decay = k * k;
                float tt = (_shakeDuration - _shakeRemaining) * 47f;
                float3 offset = new float3(math.sin(tt), math.sin(tt * 1.37f + 1.1f), math.sin(tt * 0.83f + 2.3f));
                pos += offset * (_shakeAmplitude * decay);
                if (_shakeRemaining <= 0f) _shakeAmplitude = 0f;
            }

            // Kẹp cuối cùng: bất kể blend hay rung máy, camera không bao giờ rời vùng đã dựng.
            pos = math.clamp(pos, CameraAuthoredBounds.MinBounds, CameraAuthoredBounds.MaxBounds);

            transform.position = (Vector3)pos;
            Vector3 dir = (Vector3)(look - pos);
            if (dir.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

            if (_cam != null) _cam.fieldOfView = pose.fov;
        }

        /// <summary>
        /// Bảng góc quay dựng sẵn. Tiêu cự hẹp ở góc sau lưng người sút là chủ ý: ống kính
        /// tele nén phối cảnh, khung thành trông gần và to như trên sóng truyền hình.
        /// </summary>
        public Pose PoseFor(CameraShot shot)
        {
            Pose p;
            switch (shot)
            {
                case CameraShot.Broadcast:
                    p.position = new float3(5.2f, 3.4f, 5.2f);
                    p.lookAt = new float3(0f, 1.25f, 10.6f);
                    p.fov = 40f;
                    break;

                case CameraShot.KeeperPOV:
                    p.position = new float3(0f, 1.62f, 10.4f);
                    p.lookAt = new float3(0f, 0.5f, 0f);
                    p.fov = 62f;
                    break;

                case CameraShot.LowAngle:
                    p.position = new float3(-2.0f, 0.35f, -0.5f);
                    p.lookAt = new float3(0f, 1.4f, 11.0f);
                    p.fov = 46f;
                    break;

                case CameraShot.NetCam:
                    p.position = new float3(0f, 1.45f, 12.1f);
                    p.lookAt = new float3(0f, 0.9f, 2.0f);
                    p.fov = 52f;
                    break;

                case CameraShot.ReplayOrbit:
                    p.position = CameraAuthoredBounds.ComputeOrbitPosition(_orbitYaw, _orbitPitch, _orbitDistance, _orbitCenter);
                    p.lookAt = _orbitCenter;
                    p.fov = 44f;
                    break;

                case CameraShot.KickerFace:
                    p.position = _faceEye;
                    p.lookAt = _faceTarget;
                    p.fov = 26f;
                    break;

                case CameraShot.BehindShooter:
                default:
                    // Khung hình này được tính chứ không chỉnh mò. Ràng buộc:
                    //   (a) thấy CẢ NGƯỜI sút, từ đỉnh đầu tới bàn chân, ở chỗ đứng đầu đà
                    //       KickerPlacement.Start = (-0.9, 0, -2.6);
                    //   (b) thấy quả bóng ở chấm phạt đền;
                    //   (c) khung thành đủ to để ngắm bằng ngón tay.
                    //
                    // Bản cũ (0, 1.38, -4.85) fov 30 vi phạm (a) và không thể không vi phạm:
                    // người sút đứng cách máy 2.25m, bàn chân nằm dưới mép dưới khung hình
                    // 2.2 lần nửa chiều cao. Không có tiêu cự nào sửa được — phải LÙI MÁY.
                    //
                    // Bản (0, 3.4, -10.5) nhìn về (0, 0, 3) thấy được cả người, nhưng CHÚI
                    // XUỐNG MẶT CỎ: trục ống kính hạ 14.1° so với phương ngang, mà nửa góc mở
                    // chỉ 13°, nên đường chân trời nằm NGOÀI khung hình phía trên — toàn bộ
                    // màn hình là cỏ với khán đài, không có lấy một mảng trời. Người chơi thử
                    // trên Pixel 7 ngày 2026-08-28 gọi đúng tên nó: "hơi hướng nhiều xuống mặt
                    // sân, cần ngẩng cao hơn".
                    //
                    // Sửa bằng cách HẠ MÁY 3.4 → 2.9 rồi NGẨNG trục lên 14.1° → 9.46°.
                    // Hạ máy trước vì nó nới khoảng góc giữa bàn chân và xà ngang (20.8° → 18.9°),
                    // tức là mua thêm chỗ để ngẩng mà không đội bàn chân ra khỏi mép dưới.
                    //
                    // Vì sao dừng đúng ở 9.46° chứ không ngẩng nữa: ngẩng thêm thì mọi thứ tụt
                    // xuống trong khung, mà đầu người sút (gần) tụt CHẬM hơn vạch vôi (xa). Tại
                    // 9.46° hai cái vừa chạm nhau; ngẩng quá nữa là đầu người bắt đầu che miệng
                    // khung thành — chỗ người chơi phải ngắm. Đó là trần thật của góc này.
                    //
                    // Toạ độ dọc trong khung (NDC, -1 đáy … +1 đỉnh), tỉ lệ 20:9, fov 26 dọc:
                    //   bàn chân -0.82 · đỉnh đầu +0.12 (người chiếm 47% chiều cao khung)
                    //   quả bóng  -0.41 · vạch vôi +0.13 · xà ngang +0.63
                    //   đường chân trời +0.72 — nay đã NẰM TRONG khung, 14% trên cùng là trời.
                    // Bàn chân còn cách mép dưới 9% chiều cao (~98px trên 1080), đủ an toàn;
                    // và đây là thế xấu nhất, vì chạy đà làm người sút LÙI XA máy nên chân
                    // nhích lên chứ không tụt xuống.
                    //
                    // Đã kiểm khán đài không hở: mép trên khung hình ngóc 3.5°, tia nhìn dâng
                    // 0.061m/m còn bậc khán đài dâng 0.494m/m, nên hàng 11 (trong 14 hàng, cao
                    // 5.17m) chặn kín — dư 3 hàng.
                    p.position = new float3(0f, 2.9f, -10.5f);
                    p.lookAt = new float3(0f, 0.65f, 3.0f);
                    p.fov = 26f;
                    break;
            }

            p.position = math.clamp(p.position, CameraAuthoredBounds.MinBounds, CameraAuthoredBounds.MaxBounds);
            return p;
        }
    }
}
