namespace JXHLJSApp.Services.Quality;

public sealed class QualityMaterialAlreadyInspectedException : InvalidOperationException
{
    public QualityMaterialAlreadyInspectedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    // 后端暂未提供独立错误码，只识别已确认的业务提示，不吞掉其他扫码错误。
    public static bool IsAlreadyInspectedMessage(string message) =>
        message.Contains("该二维码已提交质检结果", StringComparison.Ordinal) &&
        message.Contains("请勿重复扫码", StringComparison.Ordinal);
}
