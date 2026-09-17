namespace JXHLJSApp.Models.Warehouse;

public sealed class PrintFileResult
{
    public byte[] Bytes { get; set; } = Array.Empty<byte>();

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/octet-stream";
}
