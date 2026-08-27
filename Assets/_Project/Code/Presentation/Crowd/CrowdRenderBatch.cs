namespace Eleven.Presentation.Crowd
{
    /// <summary>
    /// Mô tả MỘT lần vẽ toàn bộ khán đài. Là struct trả về theo giá trị, không cấp phát —
    /// hàm dựng nó chạy mỗi khung hình.
    ///
    /// Không có mảng batch, không có danh sách: kiểu dữ liệu này cố tình chỉ mô tả được đúng
    /// một lần vẽ, để "vô tình tách thành hai draw call" là chuyện không viết ra được bằng
    /// API này chứ không phải chuyện phải nhớ mà tránh.
    /// </summary>
    public struct CrowdRenderBatch
    {
        /// <summary>Số instance nằm trong lần vẽ này — bằng đúng tổng số ghế.</summary>
        public int instanceCount;

        /// <summary>Chỉ số atlas. Luôn 0: cả khán đài dùng chung một texture.</summary>
        public int atlasId;

        /// <summary>Số draw call. Luôn 1.</summary>
        public int drawCallCount;

        /// <summary>Trạng thái cảm xúc đang phát.</summary>
        public CrowdMood mood;

        /// <summary>Animation có chạy không (false ở bậc C).</summary>
        public bool animated;

        /// <summary>Số byte đẩy lên GPU cho lần vẽ này.</summary>
        public int GpuBufferBytes => instanceCount * CrowdInstanceGpu.SizeInBytes;
    }
}
