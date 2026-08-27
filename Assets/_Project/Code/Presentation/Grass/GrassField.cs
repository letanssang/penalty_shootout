using System;
using Unity.Collections;
using Unity.Mathematics;
using Eleven.Core;

namespace Eleven.Presentation.Grass
{
    /// <summary>
    /// Rải cỏ một lần lúc dựng sân rồi giữ nguyên. Mỗi khung hình chỉ nhích một số thực
    /// (đồng hồ gió) — toàn bộ dao động do vertex shader tính từ pha riêng của từng túm.
    ///
    /// Cách rải: lưới lấy mẫu có nhiễu (jittered grid) kèm loại bỏ theo xác suất. Mỗi ô lưới
    /// nhận một điểm ngẫu nhiên bên trong nó, rồi giữ lại với xác suất bằng mật độ tại bán
    /// kính đó nhân diện tích ô. So với rải ngẫu nhiên đều, cách này không để lại mảng trống
    /// và cụm dày — mắt bắt hai thứ đó ngay trên mặt sân phẳng.
    ///
    /// TẤT ĐỊNH: cùng seed cho ra cùng một sân, tới từng bit. Dùng
    /// <see cref="Unity.Mathematics.Random"/>, không bao giờ dùng <c>UnityEngine.Random</c>.
    /// </summary>
    public sealed class GrassField : IDisposable
    {
        /// <summary>Cạnh ô lấy mẫu (mét). Diện tích ô nhân mật độ tối đa phải &lt;= 1.</summary>
        public const float SampleCellSize = 0.35f;

        public const float MinHeight = 0.06f;
        public const float MaxHeight = 0.11f;

        private NativeArray<GrassInstance> _instances;
        private NativeArray<GrassInstanceGpu> _gpuInstances;
        private int _count;
        private bool _isAllocated;

        private GrassTierSettings _settings;
        private float _windTime;

        /// <summary>
        /// Cờ TẮT RIÊNG cho cỏ, độc lập với bậc thiết bị — ô nghiệm thu T29 đòi
        /// "có cờ tắt riêng để đo đóng góp của riêng cỏ vào frame time".
        /// Bật/tắt cờ này KHÔNG rải lại cỏ, nên chênh lệch frame time đo được đúng bằng chi phí
        /// dựng hình của cỏ, không lẫn chi phí sinh dữ liệu.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>Số túm đã rải, bất kể có đang vẽ hay không.</summary>
        public int InstanceCount => _count;

        /// <summary>Số túm thật sự được vẽ khung hình này.</summary>
        public int VisibleInstanceCount => IsRendered ? _count : 0;

        /// <summary>Có vẽ cỏ không: cần cả bậc thiết bị cho phép LẪN cờ tắt riêng đang bật.</summary>
        public bool IsRendered => _settings.enabled && IsEnabled;

        public GrassTierSettings Settings => _settings;
        public QualityTier Tier => _settings.tier;
        public float WindTime => _windTime;
        public bool IsAllocated => _isAllocated;

        /// <summary>Chu kỳ bọc vòng của đồng hồ gió (giây).</summary>
        public const float WindCycleSeconds = 64.0f;

        public NativeArray<GrassInstance> Instances =>
            _isAllocated && _count > 0 ? _instances.GetSubArray(0, _count) : default;

        public NativeArray<GrassInstanceGpu> GpuInstances =>
            _isAllocated && _count > 0 ? _gpuInstances.GetSubArray(0, _count) : default;

        public GrassField(in GrassTierSettings settings, uint seed = 0x6A455u)
        {
            _settings = settings;

            int capacity = math.max(1, settings.maxInstances);
            _instances = new NativeArray<GrassInstance>(capacity, Allocator.Persistent);
            _gpuInstances = new NativeArray<GrassInstanceGpu>(capacity, Allocator.Persistent);
            _isAllocated = true;

            _count = settings.enabled ? Scatter(seed) : 0;
        }

        public GrassField(QualityTier tier, uint seed = 0x6A455u)
            : this(GrassTierSettings.ForTier(tier), seed)
        {
        }

        private int Scatter(uint seed)
        {
            var rng = new Unity.Mathematics.Random(seed == 0u ? 1u : seed);

            float extent = GrassDensityField.FadeEndRadius;
            int cellsPerSide = (int)math.ceil((extent * 2.0f) / SampleCellSize);
            float cellArea = SampleCellSize * SampleCellSize;
            float radiusSqLimit = extent * extent;

            int capacity = _instances.Length;
            int written = 0;

            for (int iz = 0; iz < cellsPerSide; iz++)
            {
                float cellZ = -extent + iz * SampleCellSize;

                for (int ix = 0; ix < cellsPerSide; ix++)
                {
                    if (written >= capacity)
                    {
                        // Chạm trần bộ đệm. Không im lặng bỏ qua: trả về đúng số đã ghi để
                        // test bắt được, thay vì âm thầm rải thiếu một góc sân.
                        return written;
                    }

                    float cellX = -extent + ix * SampleCellSize;

                    float x = cellX + rng.NextFloat(0.0f, SampleCellSize);
                    float z = cellZ + rng.NextFloat(0.0f, SampleCellSize);

                    float distSq = x * x + z * z;
                    if (distSq > radiusSqLimit)
                    {
                        continue;
                    }

                    float radius = math.sqrt(distSq);
                    float accept = GrassDensityField.AcceptProbability(radius, _settings.densityScale, cellArea);

                    if (rng.NextFloat() >= accept)
                    {
                        continue;
                    }

                    var instance = new GrassInstance
                    {
                        position = new float3(x, 0.0f, z),
                        yaw = rng.NextFloat(0.0f, 2.0f * math.PI),
                        height = rng.NextFloat(MinHeight, MaxHeight),
                        bend = rng.NextFloat(0.05f, 0.35f),
                        windPhase = rng.NextFloat(0.0f, 1.0f),
                        tint01 = rng.NextFloat(0.0f, 1.0f)
                    };

                    _instances[written] = instance;
                    _gpuInstances[written] = GrassInstanceExtensions.ToGpu(in instance);
                    written++;
                }
            }

            return written;
        }

        /// <summary>
        /// Nhích đồng hồ gió. Toàn bộ phần chạy mỗi khung hình của CPU là phép cộng này.
        /// Bọc vòng theo <see cref="WindCycleSeconds"/> vì float32 mất hết phần lẻ sau vài giờ
        /// chơi liên tục, và cỏ sẽ giật từng nấc.
        /// </summary>
        public void Tick(float dt)
        {
            if (!IsRendered || !_settings.render.wind || dt <= 0.0f)
            {
                return;
            }

            _windTime += dt;
            if (_windTime >= WindCycleSeconds)
            {
                _windTime -= math.floor(_windTime / WindCycleSeconds) * WindCycleSeconds;
            }
        }

        /// <summary>Mô tả lần vẽ với cấu hình dựng hình mặc định của bậc.</summary>
        public GrassRenderBatch BuildBatch() => BuildBatch(_settings.render);

        /// <summary>
        /// Mô tả lần vẽ với một biến thể cụ thể — dùng khi chạy tám dòng của bảng đo.
        /// Trả về struct theo giá trị, không cấp phát.
        /// </summary>
        public GrassRenderBatch BuildBatch(in GrassRenderSettings render)
        {
            int visible = VisibleInstanceCount;

            return new GrassRenderBatch
            {
                instanceCount = visible,
                drawCallCount = visible > 0 ? GrassBudget.MaxDrawCalls : 0,
                variantIndex = render.VariantIndex,
                usesGroundTexture = _settings.useGroundTexture || !IsEnabled
            };
        }

        public void Dispose()
        {
            if (!_isAllocated)
            {
                return;
            }

            if (_instances.IsCreated) _instances.Dispose();
            if (_gpuInstances.IsCreated) _gpuInstances.Dispose();
            _isAllocated = false;
            _count = 0;
        }
    }
}
