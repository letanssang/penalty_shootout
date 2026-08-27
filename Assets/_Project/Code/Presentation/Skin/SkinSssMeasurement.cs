using System;

namespace Eleven.Presentation.Skin
{
    /// <summary>
    /// Số đo GPU của shader da. Ngân sách T31 áp cho CẢ HAI nhân vật cùng lúc, không phải cho
    /// một — nên số đo phải ghi rõ lúc đo có mấy nhân vật trên màn hình.
    /// Quy tắc 7 của docs/backlog/README.md: ghi số đo, đừng ghi cảm nhận.
    /// </summary>
    public struct SkinGpuMeasurement
    {
        /// <summary>Thời gian GPU của riêng phần da (ms), đo bằng profiler trên máy thật.</summary>
        public float gpuMs;

        /// <summary>Số nhân vật da trên màn hình lúc đo. Ngân sách 0.5ms áp cho 2.</summary>
        public int characterCount;

        /// <summary>Bật hay tắt <c>_SKIN_SSS_ON</c> lúc đo.</summary>
        public bool sssEnabled;

        /// <summary>Tên máy. Ô nghiệm thu đòi "máy bậc B" — ghi rõ máy nào.</summary>
        public string deviceName;

        /// <summary>Bậc thiết bị lúc đo.</summary>
        public Eleven.Core.QualityTier tier;

        public bool IsRecorded =>
            gpuMs > 0.0f &&
            characterCount > 0 &&
            !string.IsNullOrEmpty(deviceName);

        /// <summary>Quy về ngân sách hai nhân vật để so với trần 0.5ms.</summary>
        public float NormalizedToTwoCharactersMs =>
            characterCount > 0 ? gpuMs * SkinBudget.CharacterCount / characterCount : 0.0f;
    }

    /// <summary>
    /// Kết luận ngân sách. Giống T29: không có giá trị nào nghĩa là "tự sửa giúp" —
    /// vượt trần thì báo cáo, quyết định cắt là của người.
    /// </summary>
    public enum SkinVerdict
    {
        ChuaDoDu = 0,
        Dat = 1,
        VuotNganSach_PhaiBaoCao = 2
    }

    /// <summary>
    /// Ô nghiệm thu "so sánh cạnh nhau: bật/tắt SSS, chụp cùng góc cùng ánh sáng".
    ///
    /// Kiểu này tồn tại để chặn đúng một kiểu gian lận vô tình: chụp hai ảnh ở hai góc camera
    /// khác nhau rồi kết luận SSS đẹp hơn. Hai ảnh phải cùng góc, cùng đèn, cùng bậc — nếu
    /// không thì cặp ảnh không chứng minh được gì.
    /// </summary>
    public struct SkinSideBySideComparison
    {
        public string sssOnImagePath;
        public string sssOffImagePath;

        /// <summary>Mô tả góc camera (T26 shot id, hoặc toạ độ). Phải giống nhau ở hai ảnh.</summary>
        public string cameraSetup;

        /// <summary>Mô tả cấu hình đèn. Phải giống nhau ở hai ảnh.</summary>
        public string lightingSetup;

        public string deviceName;

        public bool IsRecorded =>
            !string.IsNullOrEmpty(sssOnImagePath) &&
            !string.IsNullOrEmpty(sssOffImagePath) &&
            !string.IsNullOrEmpty(cameraSetup) &&
            !string.IsNullOrEmpty(lightingSetup) &&
            !string.IsNullOrEmpty(deviceName) &&
            !string.Equals(sssOnImagePath, sssOffImagePath, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ô nghiệm thu "thời gian biên dịch shader không làm màn hình đầu tiên delay quá 1 giây".
    /// Phải đo trên BUILD thật: trong Editor shader đã nằm sẵn trong cache.
    /// </summary>
    public struct SkinCompileMeasurement
    {
        /// <summary>Thời gian màn hình đầu tiên bị chặn vì biên dịch shader (ms).</summary>
        public float firstScreenCompileMs;

        /// <summary>Số biến thể thật sự được biên dịch lúc đó.</summary>
        public int compiledVariantCount;

        /// <summary>Có dùng warm-up (ShaderVariantCollection) không.</summary>
        public bool usedWarmup;

        /// <summary>Lần chạy đầu tiên sau khi cài đặt — cache shader còn trống.</summary>
        public bool coldStart;

        public string deviceName;

        public bool IsRecorded =>
            firstScreenCompileMs > 0.0f &&
            compiledVariantCount > 0 &&
            coldStart &&
            !string.IsNullOrEmpty(deviceName);

        public SkinVerdict Evaluate()
        {
            if (!IsRecorded) return SkinVerdict.ChuaDoDu;
            return firstScreenCompileMs <= SkinBudget.MaxFirstScreenCompileMs
                ? SkinVerdict.Dat
                : SkinVerdict.VuotNganSach_PhaiBaoCao;
        }
    }

    /// <summary>
    /// Kết quả kiểm biến thể trên BUILD thật: những biến thể nào còn sống sau bộ lọc.
    /// Đây là ô mà Editor không kiểm được — kiểu này chỉ là chỗ dán kết quả.
    /// </summary>
    public sealed class SkinVariantAudit
    {
        private readonly System.Collections.Generic.HashSet<SkinShaderVariant> _survived =
            new System.Collections.Generic.HashSet<SkinShaderVariant>();

        public string buildTarget;
        public string deviceName;

        public void RecordSurvivor(in SkinShaderVariant variant) => _survived.Add(variant);

        public int SurvivorCount => _survived.Count;

        public bool Survived(in SkinShaderVariant variant) => _survived.Contains(variant);

        /// <summary>Đã có đủ cả ba biến thể bắt buộc chưa.</summary>
        public bool AllRequiredSurvived()
        {
            foreach (SkinShaderVariant v in SkinVariantManifest.Required())
            {
                if (!_survived.Contains(v)) return false;
            }
            return true;
        }

        public bool IsRecorded =>
            !string.IsNullOrEmpty(buildTarget) &&
            !string.IsNullOrEmpty(deviceName) &&
            _survived.Count > 0;
    }

    /// <summary>Đánh giá ngân sách GPU. Hàm thuần, không sửa gì, không tự hạ chất lượng.</summary>
    public static class SkinBudgetCheck
    {
        public static SkinVerdict Evaluate(in SkinGpuMeasurement measurement,
                                           float budgetMs = SkinBudget.MaxGpuBudgetMs)
        {
            if (!measurement.IsRecorded) return SkinVerdict.ChuaDoDu;

            return measurement.NormalizedToTwoCharactersMs <= budgetMs
                ? SkinVerdict.Dat
                : SkinVerdict.VuotNganSach_PhaiBaoCao;
        }
    }
}
