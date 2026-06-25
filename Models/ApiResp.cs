namespace JXHLJSApp.Models;

public class ApiResp<T>
{
    public bool success { get; set; }
    public string? message { get; set; }
    public int? code { get; set; }
    public T? result { get; set; }
}
