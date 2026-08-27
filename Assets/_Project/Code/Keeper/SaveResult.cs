namespace Eleven.Keeper
{
    /// <summary>
    /// Kết cục pha cản phá (T21). Thứ tự giá trị là hợp đồng lưu trữ: T24 ghi kết quả lượt
    /// sút xuống save file dưới dạng byte, nên KHÔNG được chèn giá trị mới vào giữa.
    ///
    /// Missed    — thủ môn không chạm được bóng. Bóng đi tiếp không đổi hướng.
    /// Caught    — bắt dính. Bóng chết trong tay, deflectVelocity = 0.
    /// Parried   — đẩy bóng ra dứt khoát bằng lòng bàn tay: bóng đổi hướng mạnh, bật ra xa.
    /// Deflected — chạm nhẹ đầu ngón tay: bóng chỉ lệch hướng, phần lớn động năng còn giữ nguyên.
    /// OntoPost  — chạm được bóng nhưng bóng bật vào khung (cột dọc hoặc xà ngang).
    /// </summary>
    public enum SaveResult : byte
    {
        Missed = 0,
        Caught = 1,
        Parried = 2,
        Deflected = 3,
        OntoPost = 4
    }
}
