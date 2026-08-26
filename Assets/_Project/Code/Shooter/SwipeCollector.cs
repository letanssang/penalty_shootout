// Sinh boi mo hinh duoc giao viec (9router), da qua ra soat tinh cua Claude 2026-08-26.
// CHUA CHAY TEST SONG trong Unity -> chua tick muc nghiem thu nao trong backlog.
using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Eleven.Shooter {
    public enum SwipeEndReason {
        Completed,
        TooFewSamples,
        Discarded,
        BufferOverflow
    }

    public struct SwipeResult {
        public bool valid;
        public SwipeFeatures features;
        public SwipeEndReason reason;
        public int sampleCount;
    }

    /// <summary>
    /// Lớp thu thập cử chỉ chạm thô từ màn hình, chuẩn hoá DPI sang đơn vị cm
    /// và cung cấp dữ liệu cho SwipeAnalyzer.
    /// Hoàn toàn là C# thuần, không phụ thuộc MonoBehaviour, không sinh rác GC trong quá trình thu thập.
    /// </summary>
    public sealed class SwipeCollector : IDisposable {
        private NativeArray<SwipeSample> _samples;
        private readonly int _capacity;
        private int _count;
        private float _k; // Hệ số chuyển đổi pixel -> cm chốt lúc Begin: 2.54 / dpi
        private float _lastTime;
        private bool _isCollecting;
        private bool _hasOverflowed;
        private bool _isDisposed;

        public bool IsCollecting => _isCollecting && !_isDisposed;
        public int Capacity => _capacity;
        public int SampleCount => _count;

        public SwipeCollector(int capacity = 256) {
            if (capacity < 4) {
                capacity = 4;
            }

            _capacity = capacity;
            _samples = new NativeArray<SwipeSample>(capacity, Allocator.Persistent);
            _count = 0;
            _isCollecting = false;
            _hasOverflowed = false;
            _isDisposed = false;
        }

        /// <summary>
        /// Bắt đầu thu thập cú vuốt mới. Hệ số quy đổi k = 2.54 / dpi được chốt tại đây.
        /// HỢP ĐỒNG GỐC TOẠ ĐỘ: mọi <paramref name="pixelPosition"/> bơm vào một cú vuốt phải
        /// cùng MỘT gốc toạ độ. Collector không tự đoán được hệ input nào đang gọi nó; lớp trên
        /// tự quy về một mối (xem <see cref="PhysicalUnits.FlipYToBottomLeft"/>).
        /// Nếu đang trong một cú vuốt dở dang, cú vuốt cũ sẽ bị huỷ êm để bắt đầu cú vuốt mới.
        /// </summary>
        public void Begin(float2 pixelPosition, float time, float dpi) {
            if (_isDisposed) return;

            _k = PhysicalUnits.GetPixelsToCmFactor(dpi);
            _count = 0;
            _hasOverflowed = false;
            _isCollecting = true;
            _lastTime = time;

            _samples[0] = new SwipeSample {
                position = pixelPosition * _k,
                time = time
            };
            _count = 1;
        }

        /// <summary>
        /// Nhận điểm di chuyển tiếp theo của ngón tay.
        /// Tự động bỏ qua các điểm có mốc thời gian không tăng (tránh chia cho 0).
        /// </summary>
        public void Move(float2 pixelPosition, float time) {
            if (_isDisposed || !_isCollecting) return;

            // Bỏ qua mẫu có thời gian không tiến triển
            if (time <= _lastTime) return;

            AppendSample(pixelPosition, time);
        }

        /// <summary>
        /// Kết thúc cú vuốt và chạy phân tích cử chỉ sang SwipeFeatures (đơn vị cm).
        /// </summary>
        public SwipeResult End(float2 pixelPosition, float time) {
            if (_isDisposed || !_isCollecting) {
                return new SwipeResult {
                    valid = false,
                    features = default,
                    reason = SwipeEndReason.Discarded,
                    sampleCount = 0
                };
            }

            // Ghi nhận điểm nhấc tay nếu thời gian hợp lệ
            if (time > _lastTime) {
                AppendSample(pixelPosition, time);
            }

            _isCollecting = false;

            if (_count < 3) {
                SwipeResult tooFew = new SwipeResult {
                    valid = false,
                    features = default,
                    reason = SwipeEndReason.TooFewSamples,
                    sampleCount = _count
                };
                _count = 0;
                return tooFew;
            }

            // Gọi phân tích trên mảng mẫu đã thu thập
            NativeSlice<SwipeSample> slice = new NativeSlice<SwipeSample>(_samples, 0, _count);
            SwipeFeatures analyzedFeatures = SwipeAnalyzer.Analyze(slice);

            SwipeResult result = new SwipeResult {
                valid = true,
                features = analyzedFeatures,
                reason = _hasOverflowed ? SwipeEndReason.BufferOverflow : SwipeEndReason.Completed,
                sampleCount = _count
            };

            _count = 0;
            return result;
        }

        /// <summary>
        /// Huỷ bỏ cú vuốt hiện tại khi xảy ra xoay màn hình, mất focus hoặc huỷ touch từ OS.
        /// </summary>
        public void Discard() {
            _isCollecting = false;
            _count = 0;
            _hasOverflowed = false;
        }

        /// <summary>
        /// CHIẾN LƯỢC XỬ LÝ TRÀN BỘ ĐỆM (Buffer Overflow):
        /// - Khi số mẫu vượt quá sức chứa tối đa, ta KHÔNG cấp phát thêm vùng nhớ mới (tránh GC/realloc).
        /// - Ta giữ nguyên toàn bộ quỹ đạo ban đầu [0 .. capacity - 2] và liên tục ghi đè mẫu mới nhất
        ///   vào vị trí cuối cùng [capacity - 1].
        /// - Lý do: Chiến lược này bảo toàn được điểm bắt đầu, hình dáng đường cong chính của cú vuốt,
        ///   đồng thời giữ đúng điểm kết thúc thực tế và thời gian kết thúc mà không làm sập bộ đệm.
        /// </summary>
        private void AppendSample(float2 pixelPosition, float time) {
            if (_count < _capacity) {
                _samples[_count] = new SwipeSample {
                    position = pixelPosition * _k,
                    time = time
                };
                _count++;
            } else {
                _hasOverflowed = true;
                _samples[_capacity - 1] = new SwipeSample {
                    position = pixelPosition * _k,
                    time = time
                };
            }
            _lastTime = time;
        }

        public void Dispose() {
            if (_isDisposed) return;

            if (_samples.IsCreated) {
                _samples.Dispose();
            }

            _isDisposed = true;
            _isCollecting = false;
            _count = 0;
        }
    }
}
