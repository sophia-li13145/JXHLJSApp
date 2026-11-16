namespace JXHLJSApp.Models;

/// <summary>
/// 服务端版本校验结果。
/// </summary>
public sealed class AppVersionCheckResult
{
    /// <summary>是否需要更新。</summary>
    public bool needUpdate { get; set; }

    /// <summary>版本比较结果：equal / lower / higher。</summary>
    public string? compareResult { get; set; }

    /// <summary>服务端提示文案。</summary>
    public string? message { get; set; }

    /// <summary>企业可用版本号。</summary>
    public string? enterpriseVersionName { get; set; }

    /// <summary>更新内容说明。</summary>
    public string? updateNote { get; set; }

    /// <summary>更新包附件信息。</summary>
    public AppVersionFileInfo? fileInfo { get; set; }

    /// <summary>后端直接返回的可匿名访问下载地址（字段放在 result 顶层时使用）。</summary>
    public string? downloadUrl { get; set; }
}

/// <summary>
/// 更新包附件信息。
/// </summary>
public sealed class AppVersionFileInfo
{
    /// <summary>附件相对地址，需经附件预览接口换取带时效的下载地址。</summary>
    public string? attachmentUrl { get; set; }

    /// <summary>安装包文件名。</summary>
    public string? fileName { get; set; }

    /// <summary>后端直接返回的可匿名访问下载地址（字段放在 fileInfo 内时使用）。</summary>
    public string? downloadUrl { get; set; }
}
