using Eleven.Match;
using Eleven.Shooter;
using Unity.Mathematics;
using UnityEngine;

namespace Eleven.Presentation.Kicker
{
    /// <summary>
    /// Hiện thực <see cref="IKickerAnimator"/> bằng Mecanim trên rig Humanoid.
    ///
    /// Không có transition nào trong <c>KickerAnimator.controller</c> — lớp này lái thẳng
    /// bằng <c>CrossFadeInFixedTime</c> theo hash tên state. Lý do: đồ thị transition hỏng
    /// ngầm (một mũi tên đặt sai điều kiện là kẹt tư thế, không có lỗi biên dịch, không có
    /// log), còn ở đây thì <see cref="KickerClipSelector.Resolve"/> là hàm toàn phần và có
    /// kiểm thử phủ cả tám pha.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class MecanimKickerAnimator : MonoBehaviour, IKickerAnimator
    {
        // ── Số đo tài sản nghệ thuật ───────────────────────────────────────────────
        //
        // Bốn hằng dưới đây KHÔNG phải số ước lượng: chúng ra từ
        // Eleven/Art/Probe Strike Contact, ghi ở docs/data/strike-contact.tsv. Cột
        // contact_norm là khung mà bàn chân sút đạt tốc độ đỉnh TRONG SỐ những khung mà
        // mắt cá còn dưới 25 cm — tức lúc chân thật sự gặp quả bóng đặt trên đất, chứ
        // không phải lúc chân vung cao nhất.
        //
        // Đổi clip nguồn thì phải chạy lại probe và sửa ở đây. Awake() so lại với độ dài
        // thật của state và cảnh báo nếu lệch, nên sai sót không im lặng trôi qua.

        const float InstepLength = 1.500f, InstepContact = 0.4889f;   // PenaltyKick
        const float InsideLength = 0.500f, InsideContact = 0.8333f;   // KickSoccerball_B
        const float ChipLength   = 0.567f, ChipContact   = 0.9118f;   // KickSoccerball_A (mirror)

        // Knuckle CHƯA CÓ CLIP RIÊNG. Gói Soccer Game Pack chỉ có ba cú sút bóng-trên-đất
        // thật (soccer penalty kick, kick soccerball, kick soccerball (2)); file tên
        // "strike foward jog" hoá ra là chạy chậm dần rồi đứng lại, không có cú đá nào —
        // đã dựng hình xác nhận. Tạm dùng lại PenaltyKick. Đây là NỢ tài sản nghệ thuật,
        // không phải nợ mã: thay clip trong controller là xong, không phải sửa dòng nào ở đây
        // ngoài hai hằng dưới.
        const float KnuckleLength = InstepLength, KnuckleContact = InstepContact;

        /// <summary>Hash tên state, tính sẵn để <see cref="Tick"/> không cấp phát chuỗi.</summary>
        static readonly int[] k_StateHash = BuildStateHashes();

        static int[] BuildStateHashes()
        {
            var names = System.Enum.GetNames(typeof(KickerClip));
            var hashes = new int[names.Length];
            for (int i = 0; i < names.Length; i++) hashes[i] = Animator.StringToHash(names[i]);
            return hashes;
        }

        // ── Inspector ──────────────────────────────────────────────────────────────

        [Header("Nhịp")]
        [Tooltip("Thời lượng pha RunUp, phải khớp KickPhaseDurations.runUp của trận đấu.")]
        [SerializeField] float runUpDuration = 0.90f;

        [Tooltip("Thời gian hoà hình khi đổi clip (giây, thời gian thật).")]
        [SerializeField] float crossFadeSeconds = 0.12f;

        // ── Trạng thái ─────────────────────────────────────────────────────────────

        Animator _animator;
        KickerClipSelector _selector;
        KickPhase _phase = KickPhase.Complete;
        KickerClip _current = KickerClip.Idle;
        bool _started;

        Transform _hips, _leftFoot, _rightFoot, _head;

        // ── IKickerAnimator ────────────────────────────────────────────────────────

        public KickerClip CurrentClip => _current;

        public float NormalizedTime
            => _animator != null ? _animator.GetCurrentAnimatorStateInfo(0).normalizedTime : 0f;

        public float ContactNormalizedTime => ContactNormOf(_current);

        public Transform Root => transform;
        public Transform Hips => _hips;
        public Transform Head => _head;

        /// <summary>Chân trụ là chân TRÁI ở cả bốn clip sút: ba clip vốn thuận phải, còn
        /// StrikeChip đã bật cờ mirror trong controller nên cũng thành thuận phải.</summary>
        public Transform PlantFoot => _leftFoot;

        public Transform KickFoot => _rightFoot;

        public void SetRunUpDuration(float seconds) => runUpDuration = Mathf.Max(0.01f, seconds);

        public void SetAimYawDegrees(float yawDegrees)
            => transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);

        public void ResetToStart(Unity.Mathematics.float3 ballPosition)
        {
            _selector.Reset();
            _phase = KickPhase.Complete;
            _started = false;
            transform.position = (Vector3)(float3)KickerPlacement.Start
                               + new Vector3(ballPosition.x, 0f, ballPosition.z);
            transform.rotation = Quaternion.identity;
            Apply(KickerClip.Idle);
        }

        public void PrepareFor(ShotType type) => _selector.PrepareFor(type);

        public void SetOutcome(KickResult result) => _selector.SetOutcome(result);

        public void OnPhaseChanged(KickPhase oldPhase, KickPhase newPhase)
        {
            // Quay lại Placing nghĩa là lượt mới (hoặc vừa Abort): xoá ý đồ và kết quả cũ,
            // nếu không thì cầu thủ ăn mừng bằng kết quả của lượt trước.
            if (newPhase == KickPhase.Placing) _selector.Reset();

            _phase = newPhase;
            Apply(Evaluate(0f));
        }

        public void Tick(float dt, float phaseProgress01)
        {
            Apply(Evaluate(phaseProgress01));
            AdvanceRunUp(phaseProgress01);
        }

        /// <summary>
        /// Đưa người sút từ vạch xuất phát tới chỗ chân trụ, bằng MÃ chứ không bằng root
        /// motion của clip.
        ///
        /// Root motion đúng hơn về dáng chạy — chân không trượt — nhưng nó quyết định người
        /// sút DỪNG Ở ĐÂU, mà chỗ đó phải là cạnh quả bóng, không phải chỗ mà 2.61 m dịch
        /// chuyển của PenaltyKick tình cờ đưa tới. Sai vài xen-ti-mét là bàn chân xuyên qua
        /// bóng hoặc đá vào không khí. Ở giai đoạn này ưu tiên chân gặp đúng bóng; T36 (IK)
        /// mới là chỗ khử trượt chân. Vì vậy prefab đặt applyRootMotion = false.
        /// </summary>
        void AdvanceRunUp(float phaseProgress01)
        {
            if (_phase != KickPhase.RunUp) return;

            var t = Mathf.Clamp01(phaseProgress01);
            // smoothstep: xuất phát và dừng đều mềm, giống người chạy đà hơn nội suy thẳng.
            var eased = t * t * (3f - 2f * t);
            var from = (Vector3)(float3)KickerPlacement.Start;
            var to   = (Vector3)(float3)KickerPlacement.Plant;

            // ĐẶT chứ không cộng dồn. MatchGameLoop cộng phần dạt ngang (tín hiệu T18) lên
            // trên vị trí này mỗi khung, và nó tính lại reveal từ đầu mỗi lần — nên hợp đồng
            // là: lớp hoạt ảnh đặt vị trí gốc, gọi hàm cộng thêm. Cộng dồn ở đây sẽ nhân đôi
            // phần dạt và người sút trôi khỏi quả bóng. Greybox theo đúng hợp đồng này.
            transform.position = Vector3.Lerp(from, to, eased);
        }

        // ── Nội bộ ─────────────────────────────────────────────────────────────────

        KickerClip Evaluate(float phaseProgress01)
        {
            var remaining = _phase == KickPhase.RunUp
                ? Mathf.Max(0f, (1f - Mathf.Clamp01(phaseProgress01)) * runUpDuration)
                : 0f;
            return _selector.Resolve(_phase, remaining, StrikeLeadSeconds(_selector.PendingShot));
        }

        void Apply(KickerClip clip)
        {
            if (_started && clip == _current) return;
            _current = clip;
            _started = true;
            if (clip >= KickerClip.StrikeInstep && clip <= KickerClip.StrikeKnuckle)
                _selector.LockStrike();
            // Không có controller thì CrossFade chỉ ném cảnh báo rồi thôi. Bỏ qua để lớp này
            // vẫn chạy được trong kiểm thử EditMode dựng Animator trần, và để một prefab
            // gán thiếu không làm ngập log mỗi khung hình.
            if (_animator == null || _animator.runtimeAnimatorController == null) return;

            // CrossFadeInFixedTime chứ không CrossFade: bản thường tính thời gian hoà hình
            // theo TỈ LỆ của clip đích, nên cùng một tham số cho ra 0.10 s khi vào
            // KickSoccerball_B (0.5 s) và 1.71 s khi vào Celebrate (8.5 s).
            _animator.CrossFadeInFixedTime(k_StateHash[(int)clip], crossFadeSeconds, 0);
        }

        static float ContactNormOf(KickerClip clip)
        {
            switch (clip)
            {
                case KickerClip.StrikeInstep:     return InstepContact;
                case KickerClip.StrikeInsideFoot: return InsideContact;
                case KickerClip.StrikeChip:       return ChipContact;
                case KickerClip.StrikeKnuckle:    return KnuckleContact;
                default:                          return 0f;
            }
        }

        static float LengthOf(KickerClip clip)
        {
            switch (clip)
            {
                case KickerClip.StrikeInstep:     return InstepLength;
                case KickerClip.StrikeInsideFoot: return InsideLength;
                case KickerClip.StrikeChip:       return ChipLength;
                case KickerClip.StrikeKnuckle:    return KnuckleLength;
                default:                          return 0f;
            }
        }

        /// <summary>
        /// Clip sút phải bật sớm bấy nhiêu giây trước lúc gameplay bắn bóng, để khung chạm
        /// của hoạt ảnh rơi đúng vào thời điểm đó.
        /// </summary>
        public static float StrikeLeadSeconds(ShotType type)
        {
            var clip = KickerClipSelector.StrikeFor(type);
            return LengthOf(clip) * ContactNormOf(clip);
        }

        void Awake()
        {
            _animator = GetComponent<Animator>();
            _selector.Reset();

            if (_animator.avatar == null || !_animator.avatar.isHuman)
            {
                Debug.LogError($"[MecanimKickerAnimator] {name}: Avatar không phải Humanoid — " +
                               "không lấy được xương và retarget sẽ không chạy.", this);
                return;
            }

            _hips      = _animator.GetBoneTransform(HumanBodyBones.Hips);
            _leftFoot  = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            _rightFoot = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
            _head      = _animator.GetBoneTransform(HumanBodyBones.Head);

            if (_hips == null || _leftFoot == null || _rightFoot == null)
                Debug.LogError($"[MecanimKickerAnimator] {name}: thiếu xương Hips/LeftFoot/RightFoot — " +
                               "KickerBoneCueSource sẽ đọc ra số 0.", this);

            if (_head == null)
                Debug.LogWarning($"[MecanimKickerAnimator] {name}: không có xương Head — góc cận mặt " +
                                 "ở pha phản ứng sẽ rơi về chỗ đặt máy mặc định, không bám người sút.", this);

            // Nếu ai đó thay clip trong controller mà quên chạy lại probe, các hằng ở đầu file
            // thành số ma. Bắt lỗi đó ngay lúc khởi động thay vì để nó biểu hiện thành
            // "chân vung lệch nhịp" mà không ai truy được nguyên nhân.
            VerifyClipLengths();
        }

        void VerifyClipLengths()
        {
            var rac = _animator.runtimeAnimatorController;
            if (rac == null)
            {
                Debug.LogError($"[MecanimKickerAnimator] {name}: chưa gán Animator Controller.", this);
                return;
            }

            var clips = rac.animationClips;
            for (var c = KickerClip.StrikeInstep; c <= KickerClip.StrikeKnuckle; c++)
            {
                var expected = LengthOf(c);
                var found = false;
                for (int i = 0; i < clips.Length; i++)
                {
                    if (clips[i] == null) continue;
                    if (Mathf.Abs(clips[i].length - expected) > 0.02f) continue;
                    found = true;
                    break;
                }
                if (!found)
                    Debug.LogWarning($"[MecanimKickerAnimator] {name}: không thấy clip nào dài " +
                                     $"{expected:F3}s cho {c}. Chạy lại Eleven/Art/Probe Strike Contact " +
                                     "và cập nhật hằng số ở đầu MecanimKickerAnimator.cs.", this);
            }
        }
    }
}
