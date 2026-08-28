// GrassFieldRenderer.cs
// Bộ vẽ cỏ instanced dùng Graphics.DrawMeshInstanced.
// Phần dữ liệu (GrassField) đã xong — file này chỉ lo đưa nó lên màn hình.

using UnityEngine;
using Unity.Mathematics;
using Eleven.Core;

namespace Eleven.Presentation.Grass
{
    [DisallowMultipleComponent]
    public sealed class GrassFieldRenderer : MonoBehaviour
    {
        // ─── Serialized fields ────────────────────────────────────────────────

        [Header("Gió")]
        [SerializeField] private bool _windEnabled = true;

        [Header("Giới hạn vẽ")]
        [Tooltip("Số lá cỏ tối đa gửi xuống GPU mỗi khung. Giảm để đo chi phí từng dải.")]
        [SerializeField] private int _maxDrawInstances = 20000;

        [Tooltip("Bán kính đĩa cỏ (mét). Chỉ để hiển thị tham khảo; kích thước thật do GrassField quyết định.")]
        [SerializeField] private float _diskRadius = 20f;

        // ─── Hằng số ──────────────────────────────────────────────────────────

        // DrawMeshInstanced giới hạn cứng 1023 ma trận mỗi lô — Unity API contract.
        private const int BatchSize = 1023;

        // Màu cỏ sân bóng: gốc xanh nhạt (tint01=0) → ngọn xanh đậm (tint01=1).
        private static readonly Color ColorLow  = new Color(0.16f, 0.40f, 0.18f, 1f);
        private static readonly Color ColorHigh = new Color(0.26f, 0.62f, 0.30f, 1f);

        // Tần số dao động gió (rad/s). Không phải tuỳ chỉnh — phụ thuộc cảm quan.
        private const float WindFrequency = 2.0f * math.PI * 0.8f; // ~0.8 Hz

        // Biên độ nghiêng tối đa (radian) do gió. Cỏ sân bóng cắt ngắn, không cần to.
        private const float WindAmplitude = 0.18f;

        // ─── State nội bộ ─────────────────────────────────────────────────────

        private GrassField _field;

        // Mảng và block cấp phát một lần trong Awake, tái dùng mỗi khung — không GC.
        private Matrix4x4[]        _matrixBatch;
        private Vector4[]          _colorBatch;
        private MaterialPropertyBlock _mpb;

        private Mesh     _bladeMesh;
        private Material _bladeMaterial;

        // Tên property màu: thử _BaseColor trước (URP), fallback sang _Color.
        private int _colorPropertyId;

        private int _drawnInstanceCount;

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>GrassField đang dùng. Null trước Awake.</summary>
        public GrassField Field => _field;

        /// <summary>Số lá cỏ thực sự gửi đi vẽ ở khung gần nhất.</summary>
        public int DrawnInstanceCount => _drawnInstanceCount;

        // ─── Unity lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            // Chọn bậc từ DeviceTier nếu đã khởi tạo, không thì dùng bậc A làm mặc định.
            // Tránh rẽ nhánh theo nền tảng — bậc do năng lực phần cứng quyết định.
            var tier = DeviceTier.CurrentProfile != null
                ? DeviceTier.CurrentProfile.tier
                : QualityTier.A;

            BuildField(tier);
            BuildMesh();
            BuildMaterial();

            // Cấp phát buffer một lần — tránh GC trong Update.
            _matrixBatch = new Matrix4x4[BatchSize];
            _colorBatch  = new Vector4[BatchSize];
            _mpb         = new MaterialPropertyBlock();

            // Resolve property id một lần. _BaseColor là URP Unlit; _Color là legacy.
            _colorPropertyId = _bladeMaterial.HasProperty("_BaseColor")
                ? Shader.PropertyToID("_BaseColor")
                : Shader.PropertyToID("_Color");
        }

        private void Update()
        {
            if (_field == null || !_field.IsRendered)
            {
                // Bậc C hoặc field bị tắt — không vẽ gì, reset counter.
                _drawnInstanceCount = 0;
                return;
            }

            // Nhích đồng hồ gió. Field tự bỏ qua nếu cờ gió tắt hoặc bậc không hỗ trợ.
            if (_windEnabled)
                _field.Tick(Time.deltaTime);

            DrawField();
        }

        private void OnDestroy()
        {
            _field?.Dispose();
            _field = null;

            // Huỷ asset đã tạo bằng code để tránh rò bộ nhớ trong Editor.
            if (_bladeMesh != null)
            {
                Destroy(_bladeMesh);
                _bladeMesh = null;
            }

            if (_bladeMaterial != null)
            {
                Destroy(_bladeMaterial);
                _bladeMaterial = null;
            }
        }

        // ─── Public methods ───────────────────────────────────────────────────

        /// <summary>
        /// Huỷ field cũ và dựng lại theo bậc mới.
        /// Dùng khi người chơi thay đổi cài đặt chất lượng lúc đang chạy.
        /// </summary>
        public void Rebuild(QualityTier tier)
        {
            _field?.Dispose();
            BuildField(tier);
            _drawnInstanceCount = 0;
        }

        /// <summary>Bật/tắt hiệu ứng gió mà không cần dựng lại field.</summary>
        public void SetWindEnabled(bool enabled)
        {
            _windEnabled = enabled;
        }

        // ─── Khởi tạo nội bộ ─────────────────────────────────────────────────

        private void BuildField(QualityTier tier)
        {
            // Đọc từ TierProfile nếu có, để mật độ cỏ nhất quán với các hệ thống khác.
            _field = DeviceTier.CurrentProfile != null
                ? new GrassField(GrassTierSettings.FromProfile(DeviceTier.CurrentProfile))
                : new GrassField(tier);
        }

        /// <summary>
        /// Tự sinh mesh lá cỏ bằng code — không dùng asset, không phụ thuộc pipeline cụ thể.
        /// Hình dạng: dải 3 đốt, 7 đỉnh, 5 tam giác. Gốc rộng ~1.6cm, thon dần về ngọn ở height=1.
        /// UV.v chạy 0→1 theo chiều cao để shader tô gradient gốc-ngọn.
        /// </summary>
        private void BuildMesh()
        {
            // Nửa chiều rộng mỗi đốt (mét). Gốc rộng nhất, ngọn chỉ còn một điểm.
            // 0.008 = 8mm bên trái/phải → tổng 16mm ở gốc — cỏ sân bóng cắt ngắn.
            const float w0 = 0.008f; // gốc
            const float w1 = 0.005f; // giữa
            const float w2 = 0.002f; // sát ngọn

            // Chiều cao tương đối từng đốt (0=gốc, 1=ngọn). Scale thật = height instance.
            const float h0 = 0.00f;
            const float h1 = 0.50f;
            const float h2 = 0.80f;
            const float h3 = 1.00f; // ngọn (điểm hội tụ)

            // 7 đỉnh: 3 cặp trái/phải + 1 điểm ngọn
            var vertices = new Vector3[]
            {
                new Vector3(-w0, h0, 0f), // 0: gốc trái
                new Vector3( w0, h0, 0f), // 1: gốc phải
                new Vector3(-w1, h1, 0f), // 2: giữa trái
                new Vector3( w1, h1, 0f), // 3: giữa phải
                new Vector3(-w2, h2, 0f), // 4: sát ngọn trái
                new Vector3( w2, h2, 0f), // 5: sát ngọn phải
                new Vector3( 0f, h3, 0f), // 6: ngọn (tip)
            };

            // UV.v = chiều cao chuẩn hoá để gradient màu gốc→ngọn.
            var uvs = new Vector2[]
            {
                new Vector2(0f, h0), new Vector2(1f, h0),
                new Vector2(0f, h1), new Vector2(1f, h1),
                new Vector2(0f, h2), new Vector2(1f, h2),
                new Vector2(0.5f, h3),
            };

            // 5 tam giác theo chiều CCW (mặt trước hướng +Z trong local space).
            // DrawMeshInstanced không dùng backface culling riêng nên cần cả hai mặt
            // → khai báo double-sided bằng cách lặp lại các triangle theo chiều ngược.
            var tris = new int[]
            {
                // mặt trước
                0, 2, 1,  1, 2, 3,  // đốt dưới
                2, 4, 3,  3, 4, 5,  // đốt giữa
                4, 6, 5,            // tam giác ngọn
                // mặt sau (flip winding)
                0, 1, 2,  1, 3, 2,
                2, 3, 4,  3, 5, 4,
                4, 5, 6,
            };

            _bladeMesh = new Mesh();
            _bladeMesh.name = "GrassBlade_Procedural";
            _bladeMesh.vertices  = vertices;
            _bladeMesh.uv        = uvs;
            _bladeMesh.triangles = tris;
            _bladeMesh.RecalculateNormals();
            _bladeMesh.RecalculateBounds();

            // Đánh dấu không thể đọc lại từ CPU — tiết kiệm RAM.
            _bladeMesh.UploadMeshData(markNoLongerReadable: true);
        }

        /// <summary>
        /// Tạo material bằng Shader.Find theo thứ tự ưu tiên.
        /// URP Unlit → legacy Unlit/Color → Sprites/Default (fallback tuyệt đối).
        /// GPU instancing BẮT BUỘC bật vì dùng DrawMeshInstanced.
        /// </summary>
        private void BuildMaterial()
        {
            // Thử các shader theo thứ tự ưu tiên — không crash nếu pipeline khác.
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Color")
                     ?? Shader.Find("Sprites/Default");

            if (sh == null)
            {
                // Trường hợp cực hiếm: không tìm được shader nào → dùng mặc định của Unity.
                Debug.LogWarning("[GrassFieldRenderer] Không tìm được URP/Unlit hay fallback shader.");
                sh = Shader.Find("Standard");
            }

            _bladeMaterial = new Material(sh);
            _bladeMaterial.name = "GrassBlade_Runtime";

            // Bật GPU instancing — DrawMeshInstanced yêu cầu cờ này để shader nhận SV_InstanceID.
            _bladeMaterial.enableInstancing = true;
        }

        // ─── Vẽ mỗi khung ────────────────────────────────────────────────────

        /// <summary>
        /// Duyệt toàn bộ Instances, dựng matrix TRS + màu, chia lô 1023 rồi gửi GPU.
        /// KHÔNG cấp phát bộ nhớ — dùng lại _matrixBatch và _colorBatch từ Awake.
        /// </summary>
        private void DrawField()
        {
            var instances = _field.Instances;
            if (!instances.IsCreated || instances.Length == 0)
            {
                _drawnInstanceCount = 0;
                return;
            }

            // Giới hạn số lá vẽ theo slider + số thực tế rải được.
            int total = math.min(instances.Length, _maxDrawInstances);
            float windTime = _field.WindTime;
            int drawn = 0;

            // Vị trí gốc của GameObject (thường là Vector3.zero) để offset vị trí instance.
            float3 origin = new float3(
                transform.position.x,
                transform.position.y,
                transform.position.z);

            int batchStart = 0;
            while (batchStart < total)
            {
                int batchCount = math.min(BatchSize, total - batchStart);

                for (int i = 0; i < batchCount; i++)
                {
                    var inst = instances[batchStart + i];

                    // ── Ma trận TRS ──────────────────────────────────────────
                    // 1. Scale: chỉ scale trục Y theo height thực của túm cỏ.
                    //    X/Z giữ 1 vì vertex mesh đã dùng đơn vị mét thật.
                    var scale = new float3(1f, inst.height, 1f);

                    // 2. Xoay: yaw quanh Y (hướng nhìn ngẫu nhiên) + nghiêng quanh X (gió/bend).
                    //    Góc nghiêng = bend * sin(windTime * freq + windPhase * 2π).
                    //    Nhân windPhase * 2π để pha trải đều từ 0 đến 2π thay vì 0..1.
                    float windAngle = _windEnabled
                        ? inst.bend * WindAmplitude * math.sin(windTime * WindFrequency + inst.windPhase * math.PI2)
                        : inst.bend * WindAmplitude * 0.3f; // dừng gió: giữ độ nghiêng tĩnh nhỏ

                    // quaternion.EulerXYZ: xoay X (nghiêng) rồi Y (hướng nhìn)
                    var rot = quaternion.EulerXYZ(windAngle, inst.yaw, 0f);

                    // 3. Vị trí: offset từ gốc GameObject.
                    var pos = origin + inst.position;

                    _matrixBatch[i] = Matrix4x4.TRS(
                        new Vector3(pos.x, pos.y, pos.z),
                        new Quaternion(rot.value.x, rot.value.y, rot.value.z, rot.value.w),
                        new Vector3(scale.x, scale.y, scale.z));

                    // ── Màu sắc ──────────────────────────────────────────────
                    // Lerp tuyến tính giữa hai màu xanh sân cỏ theo tint01.
                    // Dùng Vector4 thay Color vì SetVectorArray yêu cầu Vector4[].
                    float t = inst.tint01;
                    _colorBatch[i] = new Vector4(
                        math.lerp(ColorLow.r, ColorHigh.r, t),
                        math.lerp(ColorLow.g, ColorHigh.g, t),
                        math.lerp(ColorLow.b, ColorHigh.b, t),
                        1f);
                }

                // Ghi màu vào property block — tái dùng block, chỉ ghi đúng batchCount phần tử.
                // Unity đọc đúng batchCount phần tử đầu khi count khớp với số matrix.
                _mpb.SetVectorArray(_colorPropertyId, _colorBatch);

                // Gửi lô này xuống GPU. Layer 0, camera null = tất cả camera.
                Graphics.DrawMeshInstanced(
                    _bladeMesh,
                    submeshIndex: 0,
                    _bladeMaterial,
                    _matrixBatch,
                    count: batchCount,
                    _mpb,
                    UnityEngine.Rendering.ShadowCastingMode.Off,
                    receiveShadows: false,
                    layer: gameObject.layer,
                    camera: null);

                batchStart += batchCount;
                drawn      += batchCount;
            }

            _drawnInstanceCount = drawn;
        }
    }
}
