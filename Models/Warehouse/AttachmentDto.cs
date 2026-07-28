using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace JXHLJSApp.Models.Warehouse;

public sealed class AttachmentDto : INotifyPropertyChanged
{
    public string? attachmentExt { get; set; }
    public string? attachmentFolder { get; set; }
    public string? attachmentLocation { get; set; }
    public string? attachmentName { get; set; }
    public string? attachmentRealName { get; set; }
    public decimal? attachmentSize { get; set; }
    public string? attachmentUrl { get; set; }
    public string? createdTime { get; set; }
    public string? id { get; set; }
    public string? memo { get; set; }

    // 页面加载详情后通过附件预览接口填充，不参与后端 JSON 数据传输。
    private string? _previewUrl;

    [JsonIgnore]
    public string? previewUrl
    {
        get => _previewUrl;
        set => SetProperty(ref _previewUrl, value);
    }

    private ImageSource? _previewImage;

    [JsonIgnore]
    public ImageSource? previewImage
    {
        get => _previewImage;
        set
        {
            if (SetProperty(ref _previewImage, value))
            {
                OnPropertyChanged(nameof(hasPreviewImage));
            }
        }
    }

    private bool _isPreviewLoading;

    [JsonIgnore]
    public bool isPreviewLoading
    {
        get => _isPreviewLoading;
        set => SetProperty(ref _isPreviewLoading, value);
    }

    private bool _previewLoadFailed;

    [JsonIgnore]
    public bool previewLoadFailed
    {
        get => _previewLoadFailed;
        set => SetProperty(ref _previewLoadFailed, value);
    }

    [JsonIgnore]
    public bool hasPreviewImage => previewImage is not null;

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetProperty<T>(
        ref T storage,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
