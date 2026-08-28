← [Phase 8: Vòng lặp game và chế độ chơi](phase-8-vong-lap-game.md) · [Mục lục](README.md) · [Phase 10: Tối ưu, đánh bóng và phát hành](phase-10-toi-uu-phat-hanh.md) →

---

# PHASE 9 — Âm thanh và cảm giác

**5 task · tuần 27–29**

Phase này giải quyết nợ kiến trúc âm thanh tích lũy từ giai đoạn demo: `AudioDirector` hiện tại tổng hợp tất cả bằng code và phát qua bốn `AudioSource` trần, không có bus phân tách, không có ducking, không có đường để thay clip thật sau này. Rủi ro chính là âm thanh demo đang ship cùng cấu trúc giàn giáo — phần code đó phải được thay thế trước khi bước vào giai đoạn beta. Task T50 trả nợ `PostProcessTierConfig` chưa nối vào `URP Volume` nào. Task T51 định nghĩa cách đo khách quan để kiểm chứng tiêu chí thoát M6 ("bật/tắt tiếng thấy khác biệt rõ rệt") mà không phụ thuộc cảm nhận chủ quan.

> **Nhắc rõ về ranh giới giao việc:** thiết kế âm thanh — chọn lớp tiếng, trộn tỉ lệ, quyết định ngưỡng cảm xúc — là việc tai nghe, KHÔNG giao cho agent. Agent xây hệ thống; bạn chọn nội dung đổ vào.

---

## T47 — Kiến trúc AudioMixer theo lớp và bậc thiết bị

**Phụ thuộc:** T03 · **Ước lượng:** ~2 ngày

`AudioDirector` hiện tại (file `Assets/_Project/Code/Presentation/Audio/AudioDirector.cs`) phát tất cả âm thanh trực tiếp qua bốn `AudioSource` với `volume` điều chỉnh thủ công — không có bus nhóm, không có ducking tự động, không có đường để thay clip tổng hợp bằng file thu thật mà không sửa toàn bộ caller. Đây là giàn giáo demo, không phải kiến trúc có thể ship. Nợ cụ thể: khi thay một clip thủ tục bằng file `.wav` thật, phải sửa `ProceduralClips`, `BakeAllClips()` và mọi nơi gọi — không có điểm trừu tượng nào bảo vệ phần còn lại.

Task này thêm ba thứ mà **không bắt viết lại bất kỳ caller nào hiện có**:

1. Bốn bus `AudioMixerGroup` cố định: `SFX`, `Crowd`, `UI`, `Env`. `AudioDirector` gán từng `AudioSource` vào đúng bus của nó.
2. `IAudioClipProvider` — giao diện một hàm — để `AudioDirector` hỏi clip mà không biết clip đến từ đâu. `ProceduralClipProvider` là hiện thực mặc định; khi có file thu thật thì chỉ cần viết `AssetClipProvider` mà không chạm vào `AudioDirector`.
3. `AudioBudget` — struct chứa hằng số ngân sách CPU — để `T51` có điểm neo đo được.

Ngân sách CPU cho toàn bộ âm thanh: **NGÂN SÁCH CHƯA ĐO** — phải đo trên Pixel 7 thật bằng `PerfHud`/`BenchmarkRunner` sau khi task này xong, rồi ghi vào hằng số `AudioBudget.MaxCpuMs`.

```csharp
namespace Eleven.Presentation.Audio {

  /// <summary>
  /// Nhãn bus âm thanh. Thứ tự enum = thứ tự khai báo trong AudioMixer asset.
  /// Không đổi thứ tự sau khi đã build — Unity serialize theo index.
  /// </summary>
  public enum AudioBus { SFX = 0, Crowd = 1, UI = 2, Env = 3 }

  /// <summary>
  /// Hằng số ngân sách. Ghi vào đây sau khi đo thật; không bao giờ để NaN hay 0.
  /// </summary>
  public static class AudioBudget {
    /// <summary>Ngân sách CPU tổng cho toàn bộ hệ thống âm thanh (ms/frame). Đo trên Pixel 7.</summary>
    public const float MaxCpuMs       = 1.0f; // NGÂN SÁCH CHƯA ĐO — cập nhật sau khi đo
    /// <summary>Số AudioSource tối đa đang phát đồng thời. Vượt trần → cắt âm ưu tiên thấp nhất.</summary>
    public const int   MaxActiveSources = 8;
    /// <summary>Trễ tối đa từ sự kiện vật lý đến khi AudioSource.Play() được gọi (giây).</summary>
    public const float MaxTriggerLatencyS = 0.020f; // 20ms — xem T51
  }

  /// <summary>
  /// Điểm trừu tượng duy nhất giữa AudioDirector và nguồn cung clip.
  /// Hiện thực mặc định: ProceduralClipProvider (bake bằng code).
  /// Hiện thực thay thế: AssetClipProvider (đọc file thu thật).
  /// AudioDirector không biết và không được biết sự khác biệt này.
  /// </summary>
  public interface IAudioClipProvider {
    AudioClip GetClip(AudioEvent ev);
  }

  /// <summary>
  /// Danh sách sự kiện âm thanh có thể yêu cầu clip. Mỗi enum value ánh xạ 1-1 với
  /// một clip — không phải nhiều biến thể (biến thể là trách nhiệm của provider).
  /// </summary>
  public enum AudioEvent {
    Whistle, KickLight, KickMedium, KickHeavy,
    BallNet, BallPost, GloveSave, GloveParry,
    CrowdRoar, CrowdGroan, CrowdTense, CrowdAmbient,
    UiConfirm, UiCancel
  }

  /// <summary>
  /// Cấu hình âm thanh theo bậc thiết bị.
  /// Bậc C: chỉ SFX + UI, tắt Crowd và Env để giảm CPU.
  /// </summary>
  public struct AudioTierConfig {
    public bool crowdEnabled;
    public bool envEnabled;
    public int  maxSimultaneousSfx;  // số kênh SFX đồng thời tối đa
    public static AudioTierConfig ForTier(QualityTier t);
  }

  /// <summary>
  /// Provider dùng ProceduralClips để tổng hợp clip — hiện thực hiện tại.
  /// Bake một lần trong constructor, trả về cached reference mỗi lần được hỏi.
  /// </summary>
  public sealed class ProceduralClipProvider : IAudioClipProvider {
    public ProceduralClipProvider();
    public AudioClip GetClip(AudioEvent ev);
    public void Dispose(); // giải phóng tất cả AudioClip đã bake
  }

  public sealed class AudioDirector : MonoBehaviour {
    public static AudioDirector Instance { get; }
    public float MasterVolume { get; set; }      // [0..1], áp dụng ngay
    public bool  MuteAll      { get; set; }      // tắt hoàn toàn, không xoá clip
    public void  ApplyTier(QualityTier tier);    // đổi bậc lúc chạy
    public void  SetProvider(IAudioClipProvider provider); // hoán đổi nguồn clip

    // API phát âm thanh — giữ nguyên tên để caller hiện tại không phải sửa
    public void PlayWhistle();
    public void PlayKick(float power01);          // power01 chọn KickLight/Medium/Heavy
    public void PlayNet();
    public void PlayPost();
    public void PlayGloveSave();
    public void PlayGloveParry();
    public void PlayCrowdRoar();
    public void PlayCrowdGroan();
    public void PlayUiClick();
    public void SetCrowdTension(float t01);

    // Đo độ trễ phát — dùng bởi T51
    public float LastTriggerLatencyS { get; }    // giây từ Play*() đến DSP clock
  }
}
```

**Checklist nghiệm thu**
- [ ] Bốn bus `AudioMixerGroup` tồn tại trong một `AudioMixer` asset duy nhất; mỗi `AudioSource` của `AudioDirector` nằm đúng bus của nó — kiểm bằng `AudioSource.outputAudioMixerGroup != null` và `group.name` khớp `AudioBus` tương ứng.
- [ ] `SetProvider(new ProceduralClipProvider())` rồi `SetProvider(mockProvider)` không crash và không rò clip cũ — test `DoiProvider_KhongRo_KhongCrash` cấp phát một provider mock, hoán đổi, rồi kiểm tra `Dispose()` của provider cũ được gọi đúng một lần.
- [ ] `MuteAll = true` dừng toàn bộ âm thanh; `MuteAll = false` phát lại ambient ngay lập tức — không cấp phát GC trong cả hai chiều.
- [ ] Bậc C tắt `Crowd` và `Env`, giữ `SFX` và `UI` — `AudioTierConfig.ForTier(C).crowdEnabled == false` và `envEnabled == false`; test `BacC_TatCrowd_VaEnv_GiuSfxVaUi`.
- [ ] Không cấp phát GC trong hot path sau `Awake`: gọi liên tiếp 100 lần `PlayKick(0.5f)` + `SetCrowdTension(0.7f)` → 0 byte GC — test `HotPath_KhongCapPhatGC`.
- [ ] Đo `LastTriggerLatencyS` trên Pixel 7 thật bằng `PerfHud`, ghi số đo và tên máy vào báo cáo — trần là `AudioBudget.MaxTriggerLatencyS` = 20ms.

---

## T48 — Đám đông phản ứng theo kịch tính trận đấu

**Phụ thuộc:** T47, T30, T22 · **Ước lượng:** ~2 ngày · `TẤT ĐỊNH`

`CrowdDirector` đã có `CrowdMood` và `OnKickPhaseChanged/OnOutcomeResolved` (file `Assets/_Project/Code/Presentation/Crowd/CrowdDirector.cs`). `AudioDirector` đã có `SetCrowdTension(float)`. Nhưng hai thứ đó chưa được nối với nhau — cũng chưa có logic nào biến kịch tính trận đấu (`ShootoutState`: tỉ số, lượt còn lại, lượt quyết định) thành một đường tension có hình. Tiếng đám đông hiện tại là một lớp ambient một tone, không đi theo nhịp trận.

Task này thêm `CrowdAudioBridge` — một lớp nối duy nhất nhận `ShootoutState` + `KickPhase` + `ShotOutcome` và tính ra tham số âm thanh đám đông mà `AudioDirector` cần. Logic tính tension phải tất định theo seed để hai lần đo cùng kịch bản cho cùng đường tension, đủ để so sánh độ trễ và biên độ.

Phần thiết kế âm thanh — ngưỡng `tension` ứng với lượt quyết định bằng bao nhiêu, im bặt lúc chạy đà kéo dài bao lâu — là **việc của bạn, không giao được**. Task này chỉ làm hệ thống nhận tham số đó và thực thi đúng.

```csharp
namespace Eleven.Presentation.Audio {

  /// <summary>
  /// Trạng thái đầu vào để CrowdAudioBridge tính tension.
  /// Tách khỏi ShootoutState để test không cần dựng toàn bộ match.
  /// </summary>
  public struct CrowdAudioInput {
    public int  teamScore;           // tổng bàn thắng đội mình
    public int  opponentScore;       // tổng bàn thắng đối phương
    public int  kicksRemaining;      // số lượt sút còn lại trong loạt
    public bool isDecidingKick;      // true nếu lượt này quyết định kết quả
    public KickPhase currentPhase;
    public bool hasOutcome;
    public ShotOutcome lastOutcome;
    public uint seed;                // bắt buộc — đảm bảo tất định
  }

  /// <summary>
  /// Kết quả tính của bridge: tham số đủ để AudioDirector phát đám đông đúng nhịp.
  /// Struct thuần — không cấp phát, trả theo giá trị mỗi khung hình.
  /// </summary>
  public struct CrowdAudioOutput {
    public float tension;            // [0..1] → AudioDirector.SetCrowdTension
    public bool  shouldRoar;         // phát CrowdRoar ngay khung này
    public bool  shouldGroan;        // phát CrowdGroan ngay khung này
    public bool  silenceDuringRunUp; // RunUp: im bặt ambient để nhấn căng thẳng
  }

  /// <summary>
  /// Cầu nối kịch tính → âm thanh đám đông. Hàm thuần, không trạng thái — test kiểm được
  /// toàn bộ bảng đầu ra mà không cần dựng scene. Caller giữ trạng thái lần trước để
  /// phát hiện cạnh sườn (roar/groan chỉ phát một lần, không phát mỗi khung).
  /// </summary>
  public static class CrowdAudioBridge {
    /// <summary>
    /// Tính tham số âm thanh đám đông từ trạng thái trận đấu.
    /// Tất định theo seed: cùng input → cùng output từng bit.
    /// </summary>
    public static CrowdAudioOutput Evaluate(in CrowdAudioInput input);

    /// <summary>
    /// Tension cơ bản từ tỉ số và số lượt còn lại, không phụ thuộc pha.
    /// Tách ra để test riêng phần logic tính điểm.
    /// </summary>
    public static float BaseTension(int scoreDiff, int kicksRemaining, bool isDeciding, uint seed);
  }
}
```

**Checklist nghiệm thu**
- [ ] Lượt quyết định (`isDecidingKick = true`) cho `tension` cao hơn lượt thường cùng tỉ số ít nhất 0.15 — test `LuotQuyetDinh_TensionCaoHon_0_15` duyệt 50 bộ tỉ số ngẫu nhiên.
- [ ] Pha `RunUp` cho `silenceDuringRunUp = true` — test `RunUp_SilenceAmbient_LuonTrue`.
- [ ] `shouldRoar` chỉ bật đúng khung `hasOutcome` chuyển từ `false` sang `true` với kết quả `Goal`/`PostIn` — test `Roar_ChiPhatMotLan_KhiOutcomeVuaChuyen` kiểm rằng gọi `Evaluate` hai lần với cùng `hasOutcome = true` thì lần hai `shouldRoar = false`.
- [ ] Cùng seed, cùng input → cùng output từng bit — test `TatDinh_CungSeed_CungInput_CungOutput` duyệt 1000 bộ ngẫu nhiên.
- [ ] `tension` luôn nằm trong `[0, 1]`, không sinh NaN với bất kỳ tổ hợp đầu vào hợp lệ nào — test `Tension_LuonTrong01_VaKhongNaN` (100 bộ cực đoan).
- [ ] Đo `tension` trên Pixel 7 thật qua `PerfHud` ở năm pha khác nhau: `Placing`, `RunUp`, `Contact`, kết quả Goal, kết quả Saved — ghi số đo cụ thể (không phải "mượt"), tên máy, phiên bản build.

---

## T49 — Rung (haptics) tách biệt theo sự kiện

**Phụ thuộc:** T47, T23 · **Ước lượng:** ~2 ngày

Bốn sự kiện cần rung khác nhau: chạm bóng, bóng đập cột/xà, thủ môn đấm bóng, bóng găm lưới. Hiện tại không có haptics nào. Trên iOS 13+, `Core Haptics` cho phép rung theo pattern tuỳ chỉnh. Trên Android 12+, `VibratorManager` với `VibrationEffect.createPredefined` cho ba pattern chuẩn. Unity 6 không có API haptics cross-platform cấp cao bao hàm cả hai — `Handheld.Vibrate()` chỉ có một pattern duy nhất và không kiểm soát được cường độ.

Hướng giải quyết thực tế: viết `IHapticDriver` với ba hiện thực: `NullHapticDriver` (tắt), `AndroidHapticDriver` (gọi JNI tới `VibrationEffect`), và `IosHapticDriver` (gọi `UIImpactFeedbackGenerator` qua UnityFramework). Cả hai native driver đều là **việc của bạn nếu muốn rung tốt** — task này viết scaffold và `NullHapticDriver` đầy đủ, khai báo API mà hai driver native phải tuân theo. Nếu plugin native chưa có thì `NullHapticDriver` là fallback an toàn, game không crash.

> **Lưu ý quan trọng về plugin native:** `AndroidHapticDriver` cần JNI call vào `android.os.VibrationEffect` — đây không phải code Unity thuần; cần `AndroidJavaClass`/`AndroidJavaObject`. `IosHapticDriver` cần `DllImport` vào framework iOS. Task này KHÔNG viết hai driver đó — chỉ khai báo interface và kiểm tra `NullHapticDriver`. Việc viết hai driver native là của bạn, hoặc dùng plugin từ Asset Store và đặt nó implement `IHapticDriver`.

```csharp
namespace Eleven.Presentation {

  /// <summary>
  /// Loại phản hồi xúc giác. Mỗi giá trị ánh xạ một sự kiện game cụ thể —
  /// cường độ và thời lượng là quyết định thiết kế, KHÔNG giao cho agent.
  /// </summary>
  public enum HapticEvent {
    BallKick,       // chân chạm bóng — ngắn, trung bình
    BallPost,       // bóng đập cột/xà — ngắn, sắc, cao tần
    KeeperPunch,    // thủ môn đấm bóng — trung bình
    BallNet         // bóng găm lưới — dài hơn, trầm
  }

  /// <summary>
  /// Giao diện haptics. Mọi driver phải triển khai đầy đủ bốn hàm.
  /// Nếu platform không hỗ trợ, dùng NullHapticDriver — không throw.
  /// </summary>
  public interface IHapticDriver {
    bool IsSupported { get; }
    void Trigger(HapticEvent ev);
    void Cancel();              // dừng rung đang chạy nếu có
    void Dispose();
  }

  /// <summary>
  /// Driver rỗng: IsSupported = false, mọi hàm đều no-op.
  /// Dùng khi platform không hỗ trợ hoặc người chơi tắt rung.
  /// Không cấp phát, không throw.
  /// </summary>
  public sealed class NullHapticDriver : IHapticDriver {
    public bool IsSupported => false;
    public void Trigger(HapticEvent ev) { }
    public void Cancel() { }
    public void Dispose() { }
  }

  /// <summary>
  /// Điểm điều phối duy nhất trong game. Nhận driver từ ngoài vào (DI) —
  /// không tự tạo driver, không biết platform cụ thể.
  /// </summary>
  public sealed class HapticController {
    public HapticController(IHapticDriver driver);
    public bool Enabled { get; set; }          // người chơi tắt rung
    public void OnEvent(HapticEvent ev);       // kiểm Enabled trước khi gọi driver
    public IHapticDriver Driver { get; }       // để test kiểm tra driver đang dùng
  }
}
```

**Checklist nghiệm thu**
- [ ] `NullHapticDriver.Trigger(ev)` gọi được cho mọi `HapticEvent` mà không throw, không log, không cấp phát — test `NullDriver_MoiEvent_KhongThrow_KhongCapPhat` (duyệt enum đầy đủ).
- [ ] `HapticController(new NullHapticDriver())` hoạt động đúng với `Enabled = false`: `OnEvent(ev)` không gọi `driver.Trigger` — test `KhiTat_OnEvent_KhongGoiDriver` kiểm bằng mock driver đếm lần gọi.
- [ ] `HapticController(new NullHapticDriver())` hoạt động đúng với `Enabled = true`: `OnEvent(ev)` gọi `driver.Trigger` đúng một lần — test `KhiBat_OnEvent_GoiDriverDungMotLan`.
- [ ] `HapticController.OnEvent` không cấp phát GC — test `OnEvent_KhongCapPhatGC` (1000 lần gọi).
- [ ] Tài liệu trong code (XML doc hoặc comment) ghi rõ với `AndroidHapticDriver`: cần `AndroidJavaClass("android.os.VibrationEffect")` và permission `VIBRATE`; với `IosHapticDriver`: cần `UIImpactFeedbackGenerator` qua `DllImport`. Kiểm bằng cách đọc file: comment phải có từ "AndroidJavaClass" và "UIImpactFeedbackGenerator" — test `TaiLieu_MotaRoNativeAPI_ChoHaiNen`.
- [ ] Trên Pixel 7 thật với `AndroidHapticDriver` (nếu đã viết): bốn `HapticEvent` phân biệt rõ bằng tay — đây là **việc của bạn, không đo được bằng test tự động**. Ghi kết quả vào báo cáo.

---

## T50 — Cảm giác va chạm: hit-stop, rung máy và hậu kỳ nhấn nhịp

**Phụ thuộc:** T32, T26 · **Ước lượng:** ~2 ngày

Ba thứ đã tồn tại nhưng chưa nối với nhau:
- `ImpactPostProcessEffect` (file `Assets/_Project/Code/Presentation/PostProcessing/ImpactPostProcessEffect.cs`): bộ điều khiển hiệu ứng sai lệch màu với trần 200ms — **chưa nối vào `URP Volume` nào**.
- `CameraRig.Shake(amplitude, duration)` (file `Assets/_Project/Code/Presentation/Camera/CameraRig.cs`): rung máy tất định — đã có nhưng chưa được gọi tự động từ sự kiện trận đấu.
- `PostProcessTierConfig` (file `Assets/_Project/Code/Presentation/PostProcessing/PostProcessTierConfig.cs`): bảng số theo bậc — **chưa nối vào `URP Volume` nào**.

Nợ này làm `PostProcessTierConfig` trở thành dead code: bảng số tồn tại nhưng không có gì đọc và áp dụng nó lên render thật.

Task này:
1. Nối `ImpactPostProcessEffect.CurrentIntensity` vào `URP Volume` thông qua `VolumeProfile` và `ChromaticAberration` override — cập nhật mỗi khung hình từ `Tick()`.
2. Nối `PostProcessTierConfig` vào `URP Volume` tại thời điểm khởi tạo và khi đổi bậc.
3. Tạo `GameFeelDirector` — điểm duy nhất nhận sự kiện trận đấu và ra lệnh cho cả ba hệ thống (hit-stop, rung máy, hậu kỳ) cùng lúc, với cường độ tuỳ `HitStopProfile`.

Ràng buộc bất di bất dịch: **hiệu ứng chỉ là trình bày**. Vật lý (`BallSolver`, `BallDriver`) là nguồn sự thật duy nhất. Hit-stop dừng `Time.timeScale` — điều này phải được tách khỏi `BallDriver` (chạy đồng hồ riêng 120Hz) để quỹ đạo không bị ảnh hưởng. Giải pháp: `BallDriver` dùng `Time.unscaledDeltaTime` nên hit-stop không ảnh hưởng.

Tinh chỉnh cường độ hit-stop, biên độ rung, thời lượng chromatic aberration là **việc của bạn, không giao được** — đây là cảm giác tay, chỉ biết khi cầm điện thoại lên chơi.

```csharp
namespace Eleven.Presentation {

  /// <summary>
  /// Tham số game feel cho một loại sự kiện va chạm.
  /// Dùng ScriptableObject để tinh chỉnh trong Inspector mà không cần recompile.
  /// </summary>
  [CreateAssetMenu(menuName = "Eleven/HitStopProfile")]
  public class HitStopProfile : ScriptableObject {
    [Range(0f, 0.1f)]  public float hitStopDuration;    // giây dừng Time.timeScale
    [Range(0f, 1f)]    public float hitStopTimeScale;   // timeScale trong lúc hit-stop (0 = dừng hoàn toàn)
    [Range(0f, 0.05f)] public float cameraShakeAmplitude;
    [Range(0f, 0.5f)]  public float cameraShakeDuration;
    [Range(0f, 1f)]    public float chromaticIntensity;
    [Range(0f, 0.2f)]  public float chromaticDuration;
  }

  /// <summary>
  /// Cầu nối sự kiện trận đấu → cảm giác va chạm.
  /// Điểm duy nhất gọi: CameraRig.Shake, ImpactPostProcessEffect.TriggerImpact,
  /// và Time.timeScale. Không biết chi tiết vật lý.
  /// </summary>
  public sealed class GameFeelDirector {
    public GameFeelDirector(
      CameraRig cameraRig,
      ImpactPostProcessEffect postProcess,
      PostProcessTierBinder tierBinder);

    /// <summary>Kích hoạt game feel theo profile. Gọi từ MatchGameLoop khi có sự kiện.</summary>
    public void TriggerImpact(in HitStopProfile profile);

    /// <summary>Reset tức thì, không chờ timer — dùng khi chuyển scene hoặc replay.</summary>
    public void Reset();

    /// <summary>Cập nhật hit-stop timer mỗi khung hình (dùng Time.unscaledDeltaTime).</summary>
    public void Tick(float unscaledDt);

    public bool IsHitStopActive { get; }
    public float HitStopRemaining { get; }
  }

  /// <summary>
  /// Nối PostProcessTierConfig vào URP Volume. Tách riêng để test không cần URP.
  /// Apply() đọc config và ghi vào VolumeProfile override.
  /// </summary>
  public sealed class PostProcessTierBinder {
    public PostProcessTierBinder(UnityEngine.Rendering.Volume volume);
    public void Apply(QualityTier tier);
    public void ApplyImpactIntensity(float intensity01); // gọi mỗi khung từ GameFeelDirector
  }
}
```

**Checklist nghiệm thu**
- [ ] `PostProcessTierConfig` không còn là dead code: `PostProcessTierBinder.Apply(tier)` đọc config và ghi vào `VolumeProfile` — test `TierBinder_Apply_GhiVaoVolumeProfile` kiểm `volume.profile` có override `ChromaticAberration` với weight > 0 sau khi `Apply(QualityTier.A)`.
- [ ] Hit-stop không ảnh hưởng đến `BallDriver`: `BallDriver` dùng `Time.unscaledDeltaTime` — kiểm bằng cách gọi `GameFeelDirector.TriggerImpact(profile)` rồi đọc `BallDriver.IsRunning`: vẫn `true` và đồng hồ không dừng — test `HitStop_KhongAnhHuongBallDriver`.
- [ ] `TriggerImpact` không cấp phát GC — test `TriggerImpact_KhongCapPhatGC` (100 lần gọi liên tiếp).
- [ ] Mọi hiệu ứng tắt được: `GameFeelDirector.Reset()` trả `IsHitStopActive = false`, `Time.timeScale = 1f`, `ImpactPostProcessEffect.IsActive = false` — test `Reset_TatHetHieuUng`.
- [ ] `HitStopDuration` bị kẹp trong `[0, 0.1f]` ngay trong code, không chỉ trong Inspector — test `HitStopDuration_KepCung_TrongKhoang` (truyền 1.0f → kẹp về 0.1f).
- [ ] Trên Pixel 7 thật: bật/tắt `GameFeelDirector` không làm thay đổi tỉ số khung hình hơn 2ms/frame — **đo bằng `PerfHud`/`BenchmarkRunner`, ghi số đo và tên máy**.

---

## T51 — Bộ đo nghiệm thu M6

**Phụ thuộc:** T47, T48, T49, T50 · **Ước lượng:** ~2 ngày

Tiêu chí thoát M6 là "bật/tắt tiếng phải thấy khác biệt rõ rệt". Tiêu chí này không đo được nếu chỉ nói "nghe hay hơn". Task này xây bộ đo khách quan tự động hóa được — và tách rõ phần nào test được, phần nào phải người đánh giá.

Bốn thứ đo được tự động:
1. **Độ trễ phát tiếng**: từ khoảnh khắc `KickPhase.Contact` (vật lý) đến khi `AudioDirector.LastTriggerLatencyS` được cập nhật. Trần: `AudioBudget.MaxTriggerLatencyS` = 20ms.
2. **Đếm lớp tiếng theo pha**: tại mỗi `KickPhase`, `AudioLayerProbe` đếm số `AudioSource` đang phát và bus nào. Pha `Placing` phải có chính xác 1 lớp (ambient), pha `Flight` phải có ít nhất 2.
3. **Phát hiện cắt tiếng**: một tiếng không được cắt cụt trước khi clip kết thúc tự nhiên, trừ khi bị `Cancel()` có chủ đích. `AudioLayerProbe` theo dõi thời điểm `AudioSource.isPlaying` chuyển từ `true` sang `false` và đối chiếu với `clip.length`.
4. **Tất định của tension**: cùng seed + cùng `ShootoutState` + cùng chuỗi `KickPhase` → cùng đường `tension` từng mili-giây — đo bằng `CrowdAudioBridge` thuần (không cần scene).

Phần không đo được tự động và phải người đánh giá:
- **Blind test so sánh A/B**: phiên `MuteAll = true` vs phiên bình thường. Người đánh giá không biết trước phiên nào là phiên nào. Kết quả ghi vào báo cáo. Thủ tục phải có quy trình rõ ràng (ai đánh giá, bao nhiêu lần, ngưỡng đạt). Điều này là **việc của bạn, không giao được**.

```csharp
namespace Eleven.Presentation.Audio {

  /// <summary>
  /// Snapshot trạng thái âm thanh tại một thời điểm — dùng để kiểm tra ràng buộc.
  /// Struct thuần, không cấp phát.
  /// </summary>
  public struct AudioLayerSnapshot {
    public int   activeSourceCount;    // số AudioSource đang phát
    public int   sfxCount;             // trong đó: bus SFX
    public int   crowdCount;           // trong đó: bus Crowd
    public int   uiCount;              // trong đó: bus UI
    public int   envCount;             // trong đó: bus Env
    public float dominantTension;      // tension của frame này
    public bool  hasClipCutoff;        // có tiếng nào bị cắt cụt không
  }

  /// <summary>
  /// Probe đo trạng thái hệ thống âm thanh mỗi khung hình.
  /// Không can thiệp vào âm thanh, chỉ đọc và ghi log.
  /// </summary>
  public sealed class AudioLayerProbe {
    public AudioLayerProbe(AudioDirector director);
    public AudioLayerSnapshot Sample();                    // đọc trạng thái ngay lập tức
    public void BeginRecord(int maxFrames);               // bắt đầu ghi log
    public void RecordFrame(AudioLayerSnapshot snapshot); // ghi một frame
    public AudioLayerReport EndRecord();                  // kết thúc và trả báo cáo
  }

  /// <summary>
  /// Báo cáo đo đầy đủ một phiên. Dùng để so sánh hai lần đo.
  /// </summary>
  public struct AudioLayerReport {
    public int   totalFrames;
    public float maxTriggerLatencyS;      // đỉnh độ trễ phát trong phiên
    public float avgTriggerLatencyS;      // trung bình độ trễ phát
    public int   cutoffEventCount;        // số lần tiếng bị cắt cụt
    public int   minLayersInFlight;       // số lớp tối thiểu trong pha Flight
    public bool  PassesTriggerLatency     // maxTriggerLatencyS <= AudioBudget.MaxTriggerLatencyS
      => maxTriggerLatencyS <= AudioBudget.MaxTriggerLatencyS;
    public bool  PassesNoCutoff           // không có tiếng nào bị cắt cụt
      => cutoffEventCount == 0;
    public bool  PassesMinLayers          // pha Flight có đủ lớp
      => minLayersInFlight >= 2;
  }

  /// <summary>
  /// Kiểm tra tất định của CrowdAudioBridge trên một chuỗi sự kiện.
  /// Hàm thuần — chạy ngoài scene, không cần AudioDirector.
  /// </summary>
  public static class AudioDeterminismChecker {
    /// <summary>
    /// Chạy chuỗi sự kiện hai lần với cùng seed, so sánh đầu ra từng bước.
    /// Trả true nếu mọi CrowdAudioOutput khớp bit-exact.
    /// </summary>
    public static bool CheckDeterminism(CrowdAudioInput[] events, uint seed);
  }
}
```

**Checklist nghiệm thu**
- [ ] `AudioLayerProbe.Sample()` không cấp phát GC — test `Probe_Sample_KhongCapPhatGC` (1000 lần liên tiếp, 0 byte GC).
- [ ] Pha `Placing` luôn có đúng 1 lớp đang phát (ambient), pha `Flight` có ít nhất 2 — test `PhaPlacing_DungMotLop_VaFlight_ItnhatHaiLop` giả lập hai pha với mock `AudioDirector`.
- [ ] `AudioDeterminismChecker.CheckDeterminism` trả `true` với chuỗi 50 sự kiện ngẫu nhiên, hai lần chạy với cùng seed — test `Determinism_50SuKienNgauNhien_KhopBitExact`.
- [ ] `AudioLayerReport.PassesTriggerLatency` trả `true` khi `maxTriggerLatencyS` ≤ 20ms và `false` khi vượt — test `Report_TriggerLatency_DungNguong`.
- [ ] Đo `maxTriggerLatencyS` và `avgTriggerLatencyS` trên Pixel 7 thật, 10 lượt sút liên tiếp — ghi số đo cụ thể và tên máy vào báo cáo. Trần kiểm chứng: `maxTriggerLatencyS` ≤ `AudioBudget.MaxTriggerLatencyS` = 20ms.
- [ ] Tổng CPU của toàn bộ hệ thống âm thanh (T47–T51) đo trên Pixel 7 thật bằng `BenchmarkRunner`: ghi số ms/frame ở bậc A, B, C riêng biệt. Đây là số đo điền vào `AudioBudget.MaxCpuMs` sau này — **không được để trống**.

---

[Mục lục](README.md) · [Phase 8: Vòng lặp game và chế độ chơi](phase-8-vong-lap-game.md)
