// AudioDirector.cs — Singleton quản lý phát âm thanh thủ tục.
// Mọi AudioClip được tạo một lần trong Awake (zero GC sau đó).
// Không có Inspector field nào — tự cấu hình hoàn toàn trong code.

using Unity.Mathematics;
using UnityEngine;

namespace Eleven.Presentation.Audio
{
    /// <summary>
    /// Điểm duy nhất trong game gọi để phát âm thanh.
    /// Sử dụng pattern Singleton đơn giản: Instance gán trong Awake,
    /// huỷ bỏ gracefully khi có instance trùng (không throw).
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        // ─── SINGLETON ────────────────────────────────────────────────────
        public static AudioDirector Instance { get; private set; }

        // ─── PUBLIC PROPERTY ──────────────────────────────────────────────
        /// <summary>Volume tổng [0..1]. Thay đổi áp dụng ngay lên tất cả nguồn.</summary>
        public float MasterVolume
        {
            get => _masterVolume;
            set
            {
                _masterVolume = math.saturate(value);
                ApplyMasterVolume();
            }
        }

        // ─── FIELDS (AudioSource) ─────────────────────────────────────────
        // Mỗi nguồn có vai trò cố định → không cần Inspector, không cần tên đặc biệt.
        private AudioSource _ambientSource;      // loop vô hạn, SetCrowdTension điều chỉnh
        private AudioSource _crowdOneShotSource; // roar/groan one-shot
        private AudioSource _sfxA;              // SFX kênh A (round-robin)
        private AudioSource _sfxB;              // SFX kênh B (round-robin)
        private bool        _sfxRoundRobin;     // false = A, true = B

        // ─── FIELDS (AudioClip đã baked) ─────────────────────────────────
        private AudioClip _clipWhistle;
        private AudioClip _clipKick;    // kick baked ở power 0.5 làm base
        private AudioClip _clipNet;
        private AudioClip _clipPost;
        private AudioClip _clipGlove;
        private AudioClip _clipCrowdRoar;
        private AudioClip _clipCrowdGroan;
        private AudioClip _clipCrowdAmbient;
        private AudioClip _clipUiClick;

        // ─── FIELDS (trạng thái nội bộ) ──────────────────────────────────
        private float _masterVolume   = 1f;
        private float _crowdTension   = 0f; // giữ để tính lại khi MasterVolume thay đổi

        // Biên volume ambient: t=0 → 0.12, t=1 → 0.42
        private const float AmbientVolMin   = 0.12f;
        private const float AmbientVolMax   = 0.42f;
        // Biên pitch ambient: t=0 → 0.92, t=1 → 1.06
        private const float AmbientPitchMin = 0.92f;
        private const float AmbientPitchMax = 1.06f;

        // ═════════════════════════════════════════════════════════════════
        // UNITY LIFECYCLE
        // ═════════════════════════════════════════════════════════════════

        private void Awake()
        {
            // Singleton guard: nếu đã có instance thì tự huỷ
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject); // sống qua scene transition

            // Tạo tất cả clip một lần — sau đây không có cấp phát nào trong hot path
            BakeAllClips();

            // Tạo và cấu hình AudioSource cho từng kênh
            _ambientSource      = CreateSource(loop: true,  volume: AmbientVolMin * _masterVolume, pitch: AmbientPitchMin);
            _crowdOneShotSource = CreateSource(loop: false, volume: _masterVolume, pitch: 1f);
            _sfxA               = CreateSource(loop: false, volume: _masterVolume, pitch: 1f);
            _sfxB               = CreateSource(loop: false, volume: _masterVolume, pitch: 1f);

            // Bắt đầu phát ambient ngay lập tức
            _ambientSource.clip = _clipCrowdAmbient;
            _ambientSource.Play();
        }

        private void OnDestroy()
        {
            // Giải phóng bộ nhớ âm thanh đã cấp phát trong Awake
            DestroyClipSafe(ref _clipWhistle);
            DestroyClipSafe(ref _clipKick);
            DestroyClipSafe(ref _clipNet);
            DestroyClipSafe(ref _clipPost);
            DestroyClipSafe(ref _clipGlove);
            DestroyClipSafe(ref _clipCrowdRoar);
            DestroyClipSafe(ref _clipCrowdGroan);
            DestroyClipSafe(ref _clipCrowdAmbient);
            DestroyClipSafe(ref _clipUiClick);

            // Xoá singleton reference khi object bị destroy
            if (Instance == this)
                Instance = null;
        }

        // ═════════════════════════════════════════════════════════════════
        // PUBLIC API — PHÁT ÂM THANH
        // ═════════════════════════════════════════════════════════════════

        /// <summary>Còi trọng tài ~0.5s.</summary>
        public void PlayWhistle() => PlayOneSfx(_clipWhistle, pitch: 1f);

        /// <summary>
        /// Tiếng sút bóng. power01 [0..1] điều chỉnh pitch tương đương
        /// việc dùng clip đã baked ở power khác nhau — giải pháp zero-GC.
        /// </summary>
        public void PlayKick(float power01)
        {
            // Thay vì tạo clip mới mỗi lần (GC), ta điều chỉnh pitch của AudioSource.
            // Pitch 0.9 (nhẹ) → 1.2 (mạnh): khớp cảm quan với sự thay đổi tần số trong clip.
            float pitch = math.lerp(0.90f, 1.20f, math.saturate(power01));
            PlayOneSfx(_clipKick, pitch);
        }

        /// <summary>Bóng găm lưới: "phập" trầm + xào xạc.</summary>
        public void PlayNet() => PlayOneSfx(_clipNet, pitch: 1f);

        /// <summary>Bóng đập cột/xà: "coong" kim loại.</summary>
        public void PlayPost() => PlayOneSfx(_clipPost, pitch: 1f);

        /// <summary>Tiếng găng tay bắt bóng.</summary>
        public void PlayGloveSave() => PlayOneSfx(_clipGlove, pitch: 1f);

        /// <summary>Khán đài bùng nổ khi vào bàn (~2.5s).</summary>
        public void PlayCrowdRoar()
        {
            _crowdOneShotSource.pitch = 1f;
            _crowdOneShotSource.PlayOneShot(_clipCrowdRoar, _masterVolume);
        }

        /// <summary>Khán đài thở dài khi hỏng ăn (~2.0s).</summary>
        public void PlayCrowdGroan()
        {
            _crowdOneShotSource.pitch = 1f;
            _crowdOneShotSource.PlayOneShot(_clipCrowdGroan, _masterVolume);
        }

        /// <summary>Tiếng click nút UI — nhẹ và nhanh.</summary>
        public void PlayUiClick() => PlayOneSfx(_clipUiClick, pitch: 1f);

        /// <summary>
        /// Điều biến âm lượng và pitch của lớp ambient theo căng thẳng trận đấu.
        /// t01=0 → yên tĩnh; t01=1 → căng thẳng tối đa.
        /// Kẹp biên để caller không cần lo về giá trị ngoài [0..1].
        /// </summary>
        public void SetCrowdTension(float t01)
        {
            _crowdTension = math.saturate(t01);

            float vol   = math.lerp(AmbientVolMin, AmbientVolMax, _crowdTension) * _masterVolume;
            float pitch = math.lerp(AmbientPitchMin, AmbientPitchMax, _crowdTension);

            _ambientSource.volume = vol;
            _ambientSource.pitch  = pitch;
        }

        // ═════════════════════════════════════════════════════════════════
        // PHƯƠNG THỨC TRỢ GIÚP NỘI BỘ
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Tạo tất cả AudioClip trước khi game chạy.
        /// Gom vào một hàm riêng để Awake() dễ đọc.
        /// </summary>
        private void BakeAllClips()
        {
            _clipWhistle     = ProceduralClips.Whistle();
            _clipKick        = ProceduralClips.Kick();          // base clip, pitch adjust khi play
            _clipNet         = ProceduralClips.Net();
            _clipPost        = ProceduralClips.Post();
            _clipGlove       = ProceduralClips.Glove();
            _clipCrowdRoar   = ProceduralClips.CrowdRoar();
            _clipCrowdGroan  = ProceduralClips.CrowdGroan();
            _clipCrowdAmbient= ProceduralClips.CrowdAmbientLoop();
            _clipUiClick     = ProceduralClips.UiClick();
        }

        /// <summary>
        /// Thêm AudioSource vào GameObject này với cấu hình chuẩn.
        /// playOnAwake=false để tránh phát âm thanh rác trước khi clip được gán.
        /// </summary>
        private AudioSource CreateSource(bool loop, float volume, float pitch)
        {
            var src            = gameObject.AddComponent<AudioSource>();
            src.loop           = loop;
            src.volume         = volume;
            src.pitch          = pitch;
            src.playOnAwake    = false;
            src.spatialBlend   = 0f;  // 2D hoàn toàn — game mobile không cần 3D audio
            return src;
        }

        /// <summary>
        /// Phát SFX theo round-robin giữa _sfxA và _sfxB.
        /// PlayOneShot cho phép hai tiếng chồng lên nhau mà không cắt nhau.
        /// </summary>
        private void PlayOneSfx(AudioClip clip, float pitch)
        {
            if (clip == null) return;

            // Chọn nguồn theo round-robin
            AudioSource src = _sfxRoundRobin ? _sfxB : _sfxA;
            _sfxRoundRobin  = !_sfxRoundRobin;

            src.pitch = pitch;
            src.PlayOneShot(clip, _masterVolume);
        }

        /// <summary>
        /// Áp dụng MasterVolume lên tất cả nguồn khi property thay đổi.
        /// Ambient dùng volume tương đối theo tension để giữ tỉ lệ đúng.
        /// </summary>
        private void ApplyMasterVolume()
        {
            if (_ambientSource != null)
            {
                float ambVol = math.lerp(AmbientVolMin, AmbientVolMax, _crowdTension) * _masterVolume;
                _ambientSource.volume = ambVol;
            }
            if (_crowdOneShotSource != null) _crowdOneShotSource.volume = _masterVolume;
            if (_sfxA != null)               _sfxA.volume               = _masterVolume;
            if (_sfxB != null)               _sfxB.volume               = _masterVolume;
        }

        /// <summary>
        /// Huỷ clip an toàn: kiểm tra null trước, xoá ref sau để tránh double-free.
        /// </summary>
        private static void DestroyClipSafe(ref AudioClip clip)
        {
            if (clip != null)
            {
                Object.Destroy(clip);
                clip = null;
            }
        }
    }
}
