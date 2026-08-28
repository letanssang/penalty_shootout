// CrowdRenderer.cs — Bộ vẽ khán giả impostor cho Eleven Metres (URP Forward+).
// Nhiệm vụ DUY NHẤT của file này: lấy dữ liệu từ CrowdDirector rồi đẩy lên màn hình.
// Mọi logic "ai ngồi ở đâu", "đang cảm xúc gì" đã nằm trong lớp Director và các lớp
// dữ liệu đi kèm — Renderer không tính toán lại, chỉ đọc và vẽ.

using System;
using Unity.Mathematics;
using UnityEngine;
using Eleven.Core;
using Eleven.Match;

namespace Eleven.Presentation.Crowd
{
    /// <summary>
    /// MonoBehaviour vẽ toàn bộ khán đài bằng Graphics.DrawMeshInstanced.
    /// Đặt component này lên bất kỳ GameObject nào trong Scene — không phụ thuộc
    /// vào hierarchy, không cần con hay cha đặc biệt.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CrowdRenderer : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────────
        [Tooltip("Camera dùng để tính billboard. Để trống → dùng Camera.main mỗi khung.")]
        [SerializeField] private Camera _overrideCamera;

        // ─── Public API (hợp đồng bất biến) ─────────────────────────────────────

        /// <summary>Director điều phối đám đông; null trước khi Awake chạy xong.</summary>
        public CrowdDirector Director { get; private set; }

        /// <summary>Số instance thực sự được vẽ ở khung hình vừa rồi.</summary>
        public int DrawnInstanceCount { get; private set; }

        // ─── Hằng số nội bộ ──────────────────────────────────────────────────────

        // DrawMeshInstanced nhận tối đa 1023 instance mỗi lần gọi — giới hạn API Unity.
        private const int BatchSize = 1023;

        // Kích thước ô atlas sinh bằng code: 4 cột × 4 hàng, mỗi ô 64×64 pixel.
        // Chọn 4 cột vì CrowdAtlas.FramesPerMood = 8 nhưng atlas sinh bằng code chỉ
        // cần đủ để texture không trắng trơn — shader đọc UV từ Director.GetCellUv()
        // theo atlas thật (8 cột × 4 hàng). Atlas stub này chỉ để material không null.
        // Thực tế chúng ta tạo atlas đủ 8×4 (8 frames × 4 moods) để khớp CrowdAtlas.
        private const int AtlasCols = 8;
        private const int AtlasRows = 4;
        private const int CellPx = 64;      // điểm ảnh mỗi ô
        private const int AtlasW = AtlasCols * CellPx;   // 512
        private const int AtlasH = AtlasRows * CellPx;   // 256

        // Seed cố định cho màu áo trong atlas — đảm bảo tất định trên mọi máy.
        private const uint AtlasSeed = 0xA71A5A7Bu;

        // Biên độ nhún (mét) theo từng mood — Idle nhỏ, Tense gần 0, Celebration cao.
        private static readonly float[] BobAmplitude = new float[]
        {
            0.04f,  // Hushed       — im lặng, nhúc nhích nhẹ
            0.07f,  // Anticipation — nhấp nhổm, rõ hơn
            0.18f,  // Celebrate    — nhảy lên mừng
            0.02f,  // Despair      — gục xuống, gần như đứng yên
        };

        // ─── Tài nguyên Unity tạo trong Awake, huỷ trong OnDestroy ──────────────
        private Mesh _quadMesh;
        private Material _material;
        private Texture2D _atlasTexture;

        // ─── Mảng cấp phát MỘT LẦN trong Awake ──────────────────────────────────
        // Cấp phát sẵn batch 1023 phần tử; mỗi khung hình chỉ ghi đè, không new[].
        private Matrix4x4[] _matrices;
        private MaterialPropertyBlock _propBlock;

        // Giá trị yaw trước đó của từng instance — dùng khi camera thẳng đỉnh đầu
        // (xem CrowdBillboard.YawRadians: trường hợp suy biến giữ nguyên góc cũ).
        private float[] _prevYaw;

        // ─── Awake ───────────────────────────────────────────────────────────────

        private void Awake()
        {
            // 1. Chọn bậc chất lượng từ DeviceTier.
            //    Nếu DeviceTier chưa được Initialize() (chạy trong test/scene standalone),
            //    fallback về QualityTier.A để vẫn thấy khán giả thay vì crash.
            QualityTier tier = QualityTier.A;
            try
            {
                // DeviceTier.Current trả về giá trị tĩnh, không ném lỗi thông thường;
                // nhưng nếu assembly chưa load hoặc test environment bất thường thì bọc try.
                tier = DeviceTier.Current;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CrowdRenderer] DeviceTier.Current lỗi, dùng QualityTier.A: {e.Message}");
            }

            // 2. Dựng Director — sinh toàn bộ khán đài một lần.
            Director = new CrowdDirector(tier);

            // 3. Tạo mesh quad đứng, gốc ở chân, cao 1m, rộng QuadAspect m.
            //    Dùng QuadAspect từ CrowdBillboard để khớp công thức TransformVertex.
            _quadMesh = BuildQuadMesh();

            // 4. Tạo atlas texture bằng code rồi gán lên material.
            _atlasTexture = BuildAtlasTexture();

            // 5. Tạo material với shader URP Unlit — ưu tiên theo thứ tự.
            _material = BuildMaterial(_atlasTexture);

            // 6. Cấp phát mảng một lần — không bao giờ new[] trong Update/LateUpdate.
            int cap = Director.InstanceCount;
            _matrices = new Matrix4x4[math.min(cap, BatchSize)];
            _propBlock = new MaterialPropertyBlock();
            _prevYaw = new float[cap];

            // Khởi tạo yaw ban đầu = 0 (mặt về phía +Z, tức nhìn về phía sân).
            // Sẽ được tính lại đúng ngay khung đầu tiên khi camera đã sẵn sàng.
            for (int i = 0; i < cap; i++) _prevYaw[i] = 0f;
        }

        // ─── Rebuild (đổi bậc lúc đang chạy) ────────────────────────────────────

        /// <summary>
        /// Đổi bậc chất lượng lúc đang chạy mà không sinh lại khán đài.
        /// Gọi khi người dùng hạ/tăng cấu hình hoặc khi máy nóng lên.
        /// </summary>
        public void Rebuild(QualityTier tier)
        {
            // Không sinh lại ghế — cùng seed, cùng khán đài — chỉ animation đổi.
            Director?.ApplyTier(tier);
        }

        // ─── Chuyển tiếp sự kiện cho Director ────────────────────────────────────

        /// <summary>Chuyển tiếp thay đổi pha lượt sút cho Director.</summary>
        public void OnPhaseChanged(KickPhase phase)
        {
            Director?.OnKickPhaseChanged(phase);
        }

        /// <summary>Chuyển tiếp kết quả cú sút cho Director.</summary>
        public void OnOutcome(ShotOutcome outcome)
        {
            Director?.OnOutcomeResolved(outcome);
        }

        // ─── Update / LateUpdate ─────────────────────────────────────────────────

        private void LateUpdate()
        {
            // Director chưa sẵn sàng (lạ — Awake phải chạy trước LateUpdate) thì bỏ qua.
            if (Director == null) return;

            // Nhích đồng hồ animation của Director — đây là DUY NHẤT nơi Time.deltaTime
            // được dùng trong Renderer. Mọi tính toán khung hình từ _time.
            Director.Tick(Time.deltaTime);

            DrawCrowd();
        }

        // ─── Lõi vẽ ─────────────────────────────────────────────────────────────

        private void DrawCrowd()
        {
            // Không vẽ nếu không có instance — tránh gọi DrawMeshInstanced với count=0
            // vì Unity có thể log warning.
            int total = Director.InstanceCount;
            if (total == 0 || _quadMesh == null || _material == null)
            {
                DrawnInstanceCount = 0;
                return;
            }

            // Lấy camera để tính billboard. Ưu tiên camera gán ở Inspector.
            Camera cam = _overrideCamera != null ? _overrideCamera : Camera.main;
            if (cam == null)
            {
                DrawnInstanceCount = 0;
                return;
            }

            float3 camPos = cam.transform.position;

            CrowdInstance[] instances = Director.Instances;
            CrowdMood mood = Director.Mood;

            // Biên độ nhún theo mood hiện tại — đọc bảng tĩnh, không tính toán.
            float bobAmp = BobAmplitude[(int)mood];

            // Tốc độ nhún: nhanh hơn khi Celebrate, chậm khi Hushed/Despair.
            // Dùng AnimationTime của Director để đồng bộ với đồng hồ animation.
            float animTime = Director.AnimationTime;

            int drawn = 0;

            // Vẽ theo lô 1023 — giới hạn cứng của Graphics.DrawMeshInstanced.
            for (int batchStart = 0; batchStart < total; batchStart += BatchSize)
            {
                int batchCount = math.min(BatchSize, total - batchStart);

                for (int b = 0; b < batchCount; b++)
                {
                    int i = batchStart + b;
                    ref readonly CrowdInstance inst = ref instances[i];

                    // Billboard: tính góc yaw để mặt người luôn hướng về camera.
                    // Chỉ xoay quanh Y — không nghiêng, không lật khi camera đi ngang.
                    float yaw = CrowdBillboard.YawRadians(inst.position, camPos, _prevYaw[i]);
                    _prevYaw[i] = yaw;

                    // Chuyển động nhún: offset Y theo sine, biên độ theo mood.
                    // phase01 lệch mỗi người → không đồng loạt như robot.
                    // speedScale lệch nhịp nhún riêng từng người.
                    float bobPhase = animTime * inst.speedScale * math.PI * 2f
                                     + inst.phase01 * math.PI * 2f;
                    // Dùng math.sin (Unity.Mathematics) — KHÔNG dùng Mathf.Sin.
                    float bobY = math.sin(bobPhase) * bobAmp;

                    // Vị trí chân có dịch chuyển nhún; scale chiều cao người (mét).
                    float3 pos = inst.position + new float3(0f, bobY, 0f);
                    float s = inst.scale;

                    // Dựng ma trận TRS từ các vector billboard.
                    // Right và Up tính từ yaw — Normal không cần vì quad phẳng.
                    float3 right = CrowdBillboard.Right(yaw) * (s * CrowdBillboard.QuadAspect);
                    float3 up = CrowdBillboard.Up * s;
                    float3 fwd = CrowdBillboard.Normal(yaw);   // pháp tuyến quad, không ảnh hưởng scale

                    // Matrix4x4 từ cột — Unity dùng column-major.
                    // Cột 0 = right (đã nhân scale ngang), cột 1 = up (đã nhân scale cao),
                    // cột 2 = forward (chỉ hướng, không scale — quad không có chiều sâu),
                    // cột 3 = position.
                    _matrices[b] = new Matrix4x4(
                        new Vector4(right.x,  right.y,  right.z,  0f),
                        new Vector4(up.x,     up.y,     up.z,     0f),
                        new Vector4(fwd.x,    fwd.y,    fwd.z,    0f),
                        new Vector4(pos.x,    pos.y,    pos.z,    1f)
                    );
                }

                // MaterialPropertyBlock được tái sử dụng — clear rồi set lại mỗi lô.
                // Không cấp phát: _propBlock cấp phát trong Awake.
                _propBlock.Clear();

                // Gọi DrawMeshInstanced: một lô, một draw call.
                // submeshIndex=0 — quad chỉ có một submesh.
                // layer=0 — khán giả không cần layer riêng.
                // camera=null — Unity tự chọn camera render phù hợp.
                Graphics.DrawMeshInstanced(
                    _quadMesh,
                    0,
                    _material,
                    _matrices,
                    batchCount,
                    _propBlock,
                    UnityEngine.Rendering.ShadowCastingMode.Off,
                    false,  // receiveShadows — khán giả impostor không cần nhận bóng
                    0,      // layer
                    null    // camera — null = vẽ cho tất cả camera
                );

                drawn += batchCount;
            }

            DrawnInstanceCount = drawn;
        }

        // ─── OnDestroy ───────────────────────────────────────────────────────────

        private void OnDestroy()
        {
            // Huỷ tài nguyên đã tạo bằng code — không huỷ asset có sẵn trong Project.
            // Kiểm tra null vì OnDestroy có thể gọi kể cả khi Awake chưa hoàn thành.
            if (_quadMesh != null)
            {
                Destroy(_quadMesh);
                _quadMesh = null;
            }

            if (_material != null)
            {
                Destroy(_material);
                _material = null;
            }

            if (_atlasTexture != null)
            {
                Destroy(_atlasTexture);
                _atlasTexture = null;
            }
        }

        // ─── Xây dựng tài nguyên ─────────────────────────────────────────────────

        /// <summary>
        /// Tạo mesh quad đứng: 2 tam giác, gốc ở chân (y=0..1), rộng [-0.5..0.5] theo X cục bộ.
        /// Mesh này là đơn vị — scale thực đến từ ma trận TRS trong DrawMeshInstanced.
        /// Chiều rộng cục bộ là 1; QuadAspect được nhân vào cột Right của ma trận khi vẽ.
        /// </summary>
        private static Mesh BuildQuadMesh()
        {
            // Quy ước đỉnh theo CrowdBillboard.TransformVertex:
            //   quadVertex.x ∈ [-0.5, 0.5], quadVertex.y ∈ [0, 1]
            // Gốc toạ độ cục bộ ở chân → y=0 là chân, y=1 là đỉnh đầu.
            var mesh = new Mesh { name = "CrowdImpostorQuad" };

            mesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f, 0f, 0f),  // 0: chân trái
                new Vector3( 0.5f, 0f, 0f),  // 1: chân phải
                new Vector3( 0.5f, 1f, 0f),  // 2: đầu phải
                new Vector3(-0.5f, 1f, 0f),  // 3: đầu trái
            };

            // UV: (0,0) chân trái → (1,1) đỉnh đầu phải — shader sẽ nhân vào cellUv.
            mesh.uv = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
            };

            // Hai tam giác, mặt trước (normal về -Z trong không gian cục bộ).
            // CrowdBillboard.Normal là hướng camera → Normal = +Z sau khi xoay → cần
            // mặt trước mesh nhìn về -Z cục bộ để sau khi nhân ma trận = nhìn về camera.
            mesh.triangles = new int[] { 0, 3, 1, 1, 3, 2 };

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            // MarkDynamic: mesh này không thay đổi nhưng DrawMeshInstanced không yêu cầu
            // static mesh — để mặc định (không đánh dấu dynamic).

            return mesh;
        }

        /// <summary>
        /// Sinh texture atlas bằng code: AtlasCols×AtlasRows ô, mỗi ô CellPx×CellPx pixel.
        /// Mỗi ô vẽ hình người đơn giản: đầu tròn + thân hình thang, màu áo khác nhau.
        /// Nền TRONG SUỐT (alpha=0). Dùng Unity.Mathematics.Random với seed cố định.
        /// filterMode = Point, không mipmap.
        ///
        /// Atlas sinh bằng code này khớp với bố cục CrowdAtlas (8 cột × 4 hàng):
        ///   Hàng = mood (0=Hushed, 1=Anticipation, 2=Celebrate, 3=Despair)
        ///   Cột  = khung hình animation (0..7)
        /// </summary>
        private static Texture2D BuildAtlasTexture()
        {
            // Không mipmap: khán giả ở xa sẽ blur nếu có mipmap và viền ô rỉ màu.
            // filterMode Point: giữ pixel cứng, phù hợp với sprite-style impostor.
            var tex = new Texture2D(AtlasW, AtlasH, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode   = TextureWrapMode.Clamp,
                name       = "CrowdAtlasGenerated"
            };

            // Khởi tạo toàn bộ texture trong suốt trước.
            var pixels = new Color32[AtlasW * AtlasH];
            for (int p = 0; p < pixels.Length; p++)
                pixels[p] = new Color32(0, 0, 0, 0);

            // RNG tất định — seed cố định, KHÔNG dùng UnityEngine.Random.
            var rng = new Unity.Mathematics.Random(AtlasSeed);

            for (int row = 0; row < AtlasRows; row++)
            {
                // mood xác định dáng dựa trên giá trị enum CrowdMood.
                var mood = (CrowdMood)row;

                for (int col = 0; col < AtlasCols; col++)
                {
                    // Góc trên-trái của ô theo toạ độ pixel (y đếm từ dưới lên trong Unity).
                    int ox = col * CellPx;
                    int oy = row * CellPx;

                    // Màu áo: mỗi ô một màu ngẫu nhiên trong bảng CrowdPalette.
                    int colorIdx = rng.NextInt(0, CrowdPalette.ColorCount);
                    float3 shirColor3 = CrowdPalette.GetColor(colorIdx);
                    // Chuyển từ linear sang gamma để hiển thị đúng trên texture sRGB.
                    // Texture2D mặc định là sRGB; CrowdPalette lưu giá trị linear.
                    var shirtColor = new Color32(
                        (byte)(math.pow(shirColor3.x, 1f / 2.2f) * 255f),
                        (byte)(math.pow(shirColor3.y, 1f / 2.2f) * 255f),
                        (byte)(math.pow(shirColor3.z, 1f / 2.2f) * 255f),
                        255
                    );
                    var skinColor = new Color32(230, 195, 170, 255);  // màu da

                    // Dáng người thay đổi theo mood và frame (col):
                    //   Celebrate: tay giơ cao, frame lẻ = nhảy lên, frame chẵn = xuống.
                    //   Despair:   vai cúi xuống, gục đầu.
                    //   Anticipation: ngả người về trước nhẹ.
                    //   Hushed:    đứng yên thẳng.
                    DrawFigure(pixels, AtlasW, AtlasH, ox, oy, CellPx, mood, col, shirtColor, skinColor);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);  // false,false: không gen mipmap, không giải phóng CPU data
            return tex;
        }

        /// <summary>
        /// Vẽ hình người đơn giản vào mảng pixels tại offset (ox, oy).
        /// Tất cả toạ độ tính theo pixel trong ô CellPx×CellPx.
        /// </summary>
        private static void DrawFigure(
            Color32[] pixels, int texW, int texH,
            int ox, int oy, int cellSize,
            CrowdMood mood, int frame,
            Color32 shirtColor, Color32 skinColor)
        {
            // Tỉ lệ ô: đầu chiếm 20% chiều cao, thân 50%, chân 30%.
            int headR   = cellSize / 9;          // bán kính đầu
            int headCx  = cellSize / 2;          // tâm X đầu (giữa ô)
            int headCy  = cellSize - headR - 2;  // tâm Y đầu (gần trên)

            // Điều chỉnh vị trí đầu theo mood/frame:
            //   Celebrate + frame lẻ: nhảy lên → dịch đầu lên
            //   Despair: gục đầu → dịch đầu xuống
            int headOffsetY = 0;
            float shoulderDropF = 0f;  // vai cúi bao nhiêu (0..1)

            switch (mood)
            {
                case CrowdMood.Celebrate:
                    // Nhảy nhấp nhô: khung lẻ nhảy lên, khung chẵn xuống.
                    headOffsetY = (frame % 2 == 1) ? -(cellSize / 10) : 0;
                    break;
                case CrowdMood.Despair:
                    // Gục xuống: đầu thấp hơn và vai cúi.
                    headOffsetY = cellSize / 8;
                    shoulderDropF = 0.35f;
                    break;
                case CrowdMood.Anticipation:
                    // Ngả nhẹ về trước — nhích đầu lên trên một chút.
                    headOffsetY = -(cellSize / 18);
                    break;
                case CrowdMood.Hushed:
                default:
                    headOffsetY = 0;
                    break;
            }

            headCy += headOffsetY;

            // Toạ độ thân: từ cổ xuống eo.
            int neckY = headCy - headR;           // Y đỉnh thân (cổ)
            int waistY = cellSize / 3;             // Y đáy thân (eo)
            // Thân hình thang: vai rộng hơn eo.
            int shoulderHalfW = cellSize / 4;
            int waistHalfW    = cellSize / 6;

            // Drop vai theo mood.
            int shoulderDrop = (int)(shoulderHalfW * shoulderDropF);

            // Vẽ đầu (hình tròn đặc màu da).
            DrawCircleFilled(pixels, texW, texH, ox + headCx, oy + headCy, headR, skinColor);

            // Vẽ thân hình thang màu áo — từ neckY xuống waistY.
            // Y trong Unity Texture2D tính từ dưới lên, nên oy là đáy ô.
            DrawTrapezoid(pixels, texW, texH,
                ox, oy, cellSize,
                headCx, neckY, shoulderHalfW - shoulderDrop, waistHalfW, waistY,
                shirtColor);
        }

        /// <summary>
        /// Vẽ hình tròn đặc trong mảng pixels. Dùng kiểm tra khoảng cách bình phương —
        /// không sqrt, không cấp phát.
        /// </summary>
        private static void DrawCircleFilled(
            Color32[] pixels, int texW, int texH,
            int cx, int cy, int radius, Color32 color)
        {
            int r2 = radius * radius;
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dy * dy > r2) continue;

                    int px = cx + dx;
                    int py = cy + dy;
                    if (px < 0 || px >= texW || py < 0 || py >= texH) continue;

                    pixels[py * texW + px] = color;
                }
            }
        }

        /// <summary>
        /// Vẽ hình thang đặc: rộng topHalfW tại Y=topY, rộng botHalfW tại Y=botY.
        /// Y tăng từ dưới lên (quy ước Unity Texture2D).
        /// Toạ độ đầu vào tính trong không gian ô (origin ox, oy).
        /// </summary>
        private static void DrawTrapezoid(
            Color32[] pixels, int texW, int texH,
            int ox, int oy, int cellSize,
            int cx, int topY, int topHalfW, int botHalfW, int botY,
            Color32 color)
        {
            // topY > botY vì Y đếm từ dưới lên mà cổ nằm trên eo.
            // Duyệt từ botY đến topY, nội suy rộng theo t.
            if (topY <= botY) return;   // không vẽ nếu đảo chiều

            for (int y = botY; y <= topY; y++)
            {
                float t = (topY == botY) ? 1f : (float)(y - botY) / (topY - botY);
                // t=0 ↔ eo (rộng botHalfW), t=1 ↔ cổ (rộng topHalfW)
                int halfW = (int)math.lerp(botHalfW, topHalfW, t);

                for (int x = cx - halfW; x <= cx + halfW; x++)
                {
                    int px = ox + x;
                    int py = oy + y;
                    if (px < 0 || px >= texW || py < 0 || py >= texH) continue;

                    pixels[py * texW + px] = color;
                }
            }
        }

        /// <summary>
        /// Tạo material Unlit với alpha transparency.
        /// Thứ tự ưu tiên shader: URP Unlit → Unlit/Transparent → Sprites/Default.
        /// Bật GPU instancing; set _Surface=1 và _AlphaClip=1 nếu shader hỗ trợ.
        /// </summary>
        private static Material BuildMaterial(Texture2D atlas)
        {
            // Tìm shader theo thứ tự ưu tiên — URP Forward+ cần URP Unlit để hoạt động đúng.
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                // Fallback: pipeline cũ hoặc project chưa set URP asset.
                shader = Shader.Find("Unlit/Transparent");
                Debug.LogWarning("[CrowdRenderer] 'Universal Render Pipeline/Unlit' không tìm thấy, dùng 'Unlit/Transparent'.");
            }
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
                Debug.LogWarning("[CrowdRenderer] 'Unlit/Transparent' không tìm thấy, dùng 'Sprites/Default'.");
            }
            if (shader == null)
            {
                // Không còn lựa chọn nào — trả về material màu hồng mặc định của Unity.
                Debug.LogError("[CrowdRenderer] Không tìm thấy shader nào phù hợp. Khán giả sẽ không hiển thị đúng.");
                return new Material(Shader.Find("Hidden/InternalErrorShader") ?? Shader.Find("Standard"))
                {
                    enableInstancing = true
                };
            }

            var mat = new Material(shader) { name = "CrowdImpostorMat" };

            // Gán texture atlas.
            mat.mainTexture = atlas;

            // Bật GPU instancing — bắt buộc để DrawMeshInstanced hoạt động hiệu quả.
            mat.enableInstancing = true;

            // _Surface = 1 → chế độ Transparent (URP Unlit: 0=Opaque, 1=Transparent).
            // Bọc trong HasProperty để không ném lỗi khi shader không có property này.
            if (mat.HasProperty("_Surface"))
                mat.SetFloat("_Surface", 1f);

            // _AlphaClip = 1 → bật alpha cutout (loại bỏ pixel trong suốt hoàn toàn).
            // Với impostor khán giả, alpha clip sắc nét hơn alpha blend.
            if (mat.HasProperty("_AlphaClip"))
                mat.SetFloat("_AlphaClip", 1f);

            // _Cutoff / _AlphaCutoff: ngưỡng cắt alpha — 0.1 để không cắt quá thô.
            if (mat.HasProperty("_Cutoff"))
                mat.SetFloat("_Cutoff", 0.1f);
            else if (mat.HasProperty("_AlphaCutoff"))
                mat.SetFloat("_AlphaCutoff", 0.1f);

            // Đảm bảo blend mode đúng cho Transparent kể cả khi shader không tự xử lý.
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);

            // Tắt culling: khán giả là billboard, camera có thể nhìn từ sau — không muốn
            // mất mesh chỉ vì pháp tuyến quay sai chiều ở một vài góc nhìn.
            mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);

            // Thiết lập keyword Transparent cho URP nếu có.
            if (mat.HasProperty("_Surface"))
            {
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            return mat;
        }
    }
}
