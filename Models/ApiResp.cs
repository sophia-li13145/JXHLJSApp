namespace JXHLJSApp.Models;

public class ApiResp<T>
{
    public bool success { get; set; }
    public string? message { get; set; }
    public decimal? code { get; set; }
    public decimal? costTime { get; set; }
    public T? result { get; set; }
}
