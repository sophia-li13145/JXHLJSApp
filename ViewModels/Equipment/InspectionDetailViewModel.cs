using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IndustrialControlMAUI.Models;
using IndustrialControlMAUI.Pages;
using IndustrialControlMAUI.Services;
using System.Collections.ObjectModel;

namespace IndustrialControlMAUI.ViewModels
{
    /// <summary>
    /// 质检单详情页 VM
    /// </summary>
    public partial class InspectionDetailViewModel : ObservableObject, IQueryAttributable
    {
        private readonly IEquipmentApi _api;
        private readonly IAuthApi _authApi;
        private readonly IAttachmentApi _attachmentApi;
        private readonly CancellationTokenSource _cts = new();
        private const string FolderImage = "image";
        private const string FolderFile = "file";
        private const string LocationInspection = "processInspection";
        // ===== 上传限制 =====
        private const int MaxImageCount = 9;
        private const long MaxImageBytes = 2L * 1024 * 1024;   // 2MB
        private const long MaxFileBytes = 20L * 1024 * 1024;  // 20MB

        public ObservableCollection<OrderInspectionAttachmentItem> Attachments { get; } = new();
        public ObservableCollection<OrderInspectionAttachmentItem> ImageAttachments { get; } = new(); // 仅图片

        public ObservableCollection<WorkflowVmItem> WorkflowSteps { get; } = new();

        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private InspectionDetailDto? detail;
        [ObservableProperty]
        private bool isInspectorDropdownOpen = false; // 检验员下拉列表框默认关闭
        [ObservableProperty]
        private double inspectorDropdownOffset = 40; // Entry 高度 + 间距

        // 明细与附件集合（用于列表绑定）
        public ObservableCollection<InspectionItem> Items { get; } = new();

        // 检验结果下拉（合格 / 不合格）
        public ObservableCollection<StatusOption> InspectResultOptions { get; } = new();

        private StatusOption? _selectedInspectResult;
        public StatusOption? SelectedInspectResult
        {
            get => _selectedInspectResult;
            set
            {
                if (SetProperty(ref _selectedInspectResult, value))
                {
                    // 选中后回写到 Detail.inspectResult（不去改 total*，避免触发连锁）
                    if (Detail != null)
                        Detail.inspectResult = value?.Value ?? value?.Text;
                }
            }
        }

        // 可编辑开关（如需控制 Entry/Picker 的 IsEnabled）
        [ObservableProperty] private bool isEditing = true;

        // 导航入参
        private string? _id;
        public int Index { get; set; }
        public IReadOnlyList<string> InspectResultTextList { get; } = new[] { "合格", "不合格" };

        public InspectionDetailViewModel(IEquipmentApi api, IAuthApi authApi, IAttachmentApi attachmentApi)
        {
            _api = api;
            _authApi = authApi;

            // 默认选项（也可以从字典接口加载）
            InspectResultOptions.Add(new StatusOption { Text = "合格", Value = "合格" });
            InspectResultOptions.Add(new StatusOption { Text = "不合格", Value = "不合格" });

            InspectorSuggestions = new ObservableCollection<UserInfoDto>();
            AllUsers = new List<UserInfoDto>();
            _attachmentApi = attachmentApi;
        }

        public List<UserInfoDto> AllUsers { get; private set; }
        public ObservableCollection<UserInfoDto> InspectorSuggestions { get; }

        private string? inspectorText;
        public string? InspectorText
        {
            get => inspectorText;
            set
            {
                if (SetProperty(ref inspectorText, value))
                {
                    // 仅做显示文字，不直接写回 Detail.inspecter（等选中再写回更稳）
                    FilterInspectorSuggestions(value);
                    IsInspectorDropdownOpen =  InspectorSuggestions.Count > 0;
                }
            }
        }


        [RelayCommand]
        public async Task LoadInspectorsAsync()
        {
            try
            {
                AllUsers = await _authApi.GetAllUsersAsync();

                if (!string.IsNullOrWhiteSpace(Detail?.inspecter))
                {
                    // 直接显示已有检验员名称
                    InspectorText = Detail.inspecter;
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("错误", $"加载用户列表失败：{ex.Message}", "OK");
            }
        }

        [RelayCommand]
        private void PickInspector(UserInfoDto? user)
        {
            if (user is null) return;

            // 存到后端字段
            if (Detail != null) Detail.inspecter = user.realname;
            // 输入框显示
            InspectorText = user.realname;

            IsInspectorDropdownOpen = false;
            InspectorSuggestions.Clear();
        }

        [RelayCommand]
        private void ClearInspector()
        {
            InspectorText = string.Empty;
            IsInspectorDropdownOpen = false;
        }

        private void FilterInspectorSuggestions(string? keyword)
        {
            InspectorSuggestions.Clear();
            if (string.IsNullOrWhiteSpace(keyword)) return;

            var k = keyword.Trim();

            foreach (var u in AllUsers.Where(u =>
                     (!string.IsNullOrWhiteSpace(u.realname) && u.realname.Contains(k, StringComparison.OrdinalIgnoreCase)) ||
                     (!string.IsNullOrWhiteSpace(u.username) && u.username.Contains(k, StringComparison.OrdinalIgnoreCase)) ||
                     (!string.IsNullOrWhiteSpace(u.phone) && u.phone.Contains(k, StringComparison.OrdinalIgnoreCase)) ||
                     (!string.IsNullOrWhiteSpace(u.email) && u.email.Contains(k, StringComparison.OrdinalIgnoreCase)))
                     .Take(50))
            {
                InspectorSuggestions.Add(u);
            }
        }

        /// <summary>
        /// Shell 路由入参，例如：.../InspectionDetailPage?id=xxxx
        /// </summary>
        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("id", out var v))
            {
                _id = v?.ToString();
                _ = LoadAsync();
            }
        }

        [RelayCommand]
        private async Task LoadAsync()
        {
            if (IsBusy || string.IsNullOrWhiteSpace(_id)) return;
            IsBusy = true;
            try
            {
                var resp = await _api.GetDetailAsync(_id!);
                if (resp?.result == null)
                {
                    await ShowTip("未获取到详情数据");
                    return;
                }

                Detail = resp.result;
                await LoadWorkflowAsync(_id!);

                await LoadInspectorsAsync();

                // ===== 明细 =====
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Items.Clear();
                    int i = 1;
                    foreach (var it in Detail.devInspectTaskDetailList ?? new())
                    {
                        it.index = i++; // 1,2,3...
                        Items.Add(it);
                    }
                });

                // ===== 附件 =====
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Attachments.Clear();

                    foreach (var at in (Detail.devInspectTaskAttachmentList ?? new List<InspectionAttachment>()))
                    {
                        if (string.IsNullOrWhiteSpace(at.attachmentUrl)) continue;

                        var item = new OrderInspectionAttachmentItem
                        {
                            AttachmentExt = at.attachmentExt ?? "",
                            AttachmentFolder = at.attachmentFolder ?? "",
                            AttachmentLocation = at.attachmentLocation ?? "",
                            AttachmentName = at.attachmentName ?? "",
                            AttachmentRealName = at.attachmentRealName ?? "",
                            AttachmentSize = (long)at.attachmentSize,
                            AttachmentUrl = at.attachmentUrl ?? "",
                            Id = at.id ?? "",
                            CreatedTime = at.createdTime ?? "",
                            LocalPath = null,
                            IsUploaded = true
                        };

                        // === 关键：只要是图片，才入缩略图集合 ===
                        item.IsImage = IsImageExt(item.AttachmentExt)
                                       || IsImageExt(Path.GetExtension(item.AttachmentUrl));

                        Attachments.Add(item);
                        if (item.IsImage) ImageAttachments.Add(item);
                    }

                    
                });
                await LoadPreviewThumbnailsAsync();

                // ===== 下拉选中项：检验结果 =====
                SelectedInspectResult = InspectResultOptions
                    .FirstOrDefault(o =>
                        string.Equals(o.Value, Detail.inspectResult, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(o.Text, Detail.inspectResult, StringComparison.OrdinalIgnoreCase));
               
                IsInspectorDropdownOpen = false;
            }
            catch (Exception ex)
            {
                await ShowTip($"加载失败：{ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }
        private async Task LoadPreviewThumbnailsAsync()
        {
            // 只处理“图片且当前没有 PreviewUrl，但有 AttachmentUrl 的项”
            var list = Attachments
                .Where(a => (a.IsImage || IsImageExt(a.AttachmentExt))
                            && string.IsNullOrWhiteSpace(a.PreviewUrl)                            && !string.IsNullOrWhiteSpace(a.AttachmentUrl))
                .ToList();
            if (list.Count == 0) return;

            // 并发控制：最多 4 条并发
            var options = new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = _cts.Token };

            await Task.Run(() =>
                Parallel.ForEach(list, options, item =>
                {
                    try
                    {
                        // 预签名有效期：例如 10 分钟
                        var resp = _attachmentApi.GetPreviewUrlAsync(item.AttachmentUrl!, 600, options.CancellationToken).GetAwaiter().GetResult();
                        if (resp?.success == true && !string.IsNullOrWhiteSpace(resp.result))
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                item.PreviewUrl = resp.result;
                                item.LocalPath = null;  // 有了直连地址就不再用本地
                                item.RefreshDisplay();
                            });
                        }
                    }
                    catch
                    {
                        // 忽略单条失败，必要时写日志
                    }
                })
            );
        }
       


        /// <summary>
        /// 预览附件
        /// </summary>
        [RelayCommand]
        private async Task PreviewAttachment(InspectionAttachment? att)
        {
            if (att is null || string.IsNullOrWhiteSpace(att.attachmentUrl))
            {
                await ShowTip("无效的附件。");
                return;
            }

            try
            {
                await Launcher.Default.OpenAsync(new Uri(att.attachmentUrl));
            }
            catch (Exception ex)
            {
                await ShowTip($"无法打开附件：{ex.Message}");
            }
        }

        [RelayCommand]
        public async Task PickImagesAsync() => await PickAndUploadAsync(isImage: true);

        // 新增：附件上传命令（给“上传附件”文字）
        [RelayCommand]
        public async Task PickFilesAsync() => await PickAndUploadAsync(isImage: false);

        private async Task PickAndUploadAsync(bool isImage)
        {
            try
            {
                var pick = await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    var opt = new PickOptions
                    {
                        PickerTitle = isImage ? "选择图片" : "选择附件",
                        FileTypes = isImage
                            ? FilePickerFileType.Images
                            : new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                            {
                        { DevicePlatform.Android,    new[] { "*/*" } },
                        { DevicePlatform.iOS,        new[] { "public.data" } },
                        { DevicePlatform.MacCatalyst,new[] { "public.data" } },
                        { DevicePlatform.WinUI,      new[] { "*" } },
                            })
                    };
                    return await FilePicker.PickMultipleAsync(opt);
                });
                if (pick == null) return;

                foreach (var f in pick)
                {
                    var ext = Path.GetExtension(f.FileName)?.TrimStart('.').ToLowerInvariant();
                    var isImg = IsImageExt(ext);

                    // === 1) 统一做格式校验（文件入口也要限制到白名单）
                    if (!isImg && !IsAllowedFile(ext))
                    {
                        await ShowTip($"不支持的文件格式：{f.FileName}\n允许：PDF、Word、Excel、Txt、JPG、PNG、RAR");
                        continue;
                    }

                    // === 2) 读尺寸（同时返回临时文件路径以便计算大小）
                    using var s = await f.OpenReadAsync();
                    var (tmpPath, len) = await s.CopyToTempAndLenAsync();

                    // === 3) 数量/大小限制
                    if (isImg)
                    {
                        if (ImageAttachments.Count >= MaxImageCount)
                        {
                            await ShowTip($"最多上传 {MaxImageCount} 张图片。");
                            continue;
                        }
                        if (len > MaxImageBytes)
                        {
                            await ShowTip($"图片过大：{f.FileName}\n单张不超过 2M。");
                            continue;
                        }
                    }
                    else
                    {
                        if (len > MaxFileBytes)
                        {
                            await ShowTip($"附件过大：{f.FileName}\n单个附件不超过 20M。");
                            continue;
                        }
                    }

                    // === 4) 构造本地项（先让 UI 有反馈）
                    var localItem = new OrderInspectionAttachmentItem
                    {
                        AttachmentName = f.FileName,
                        AttachmentExt = ext,
                        AttachmentSize = len,
                        LocalPath = f.FullPath,      // 有些设备是 content:// 也没事；缩略图走 LocalPath 时已兼容
                        CreatedTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        IsImage = isImg            // 决定是否进缩略图
                    };

                    // 全部列表
                    Attachments.Insert(0, localItem);
                    // 仅图片列表
                    if (isImg) ImageAttachments.Insert(0, localItem);

                    // 6) 真正上传文件（关键：multipart/form-data + file）
                    var folder = isImg ? FolderImage : FolderFile;
                    var contentType = DetectContentType(ext);     // 见下方辅助函数

                    // 用临时文件重新打开流，避免上面 using 的 src 已被释放
                    await using var fs = File.OpenRead(tmpPath);
                    var resp = await _attachmentApi.UploadAttachmentAsync(
                                attachmentFolder: folder,
                                attachmentLocation: LocationInspection,
                                fileStream: fs,
                                fileName: f.FileName,
                                contentType: contentType,
                                attachmentName: f.FileName,
                                attachmentExt: ext,
                                attachmentSize: len
                            );


                    if (resp?.success == true && resp.result != null)
                    {
                        localItem.AttachmentUrl = string.IsNullOrWhiteSpace(resp.result.attachmentUrl)
                                                        ? localItem.AttachmentUrl
                                                        : resp.result.attachmentUrl;
                        localItem.AttachmentRealName = resp.result.attachmentRealName ?? localItem.AttachmentRealName;
                        localItem.AttachmentFolder = resp.result.attachmentFolder ?? folder;
                        localItem.AttachmentLocation = resp.result.attachmentLocation ?? LocationInspection;
                        localItem.AttachmentExt = resp.result.attachmentExt ?? ext;

                        // 如果服务端给了 URL，就让图片改走网络地址展示；本地临时可清掉
                        if (!string.IsNullOrWhiteSpace(resp.result.attachmentUrl))
                            localItem.LocalPath = null;

                        // 如果最终被认定是图片而你本地没归到图片，就补一次
                        var nowIsImg = localItem.IsImage || IsImageExt(localItem.AttachmentExt)
                                       || (!string.IsNullOrWhiteSpace(localItem.AttachmentUrl)
                                           && IsImageExt(Path.GetExtension(localItem.AttachmentUrl)?.TrimStart('.')));
                        if (nowIsImg && !localItem.IsImage)
                        {
                            localItem.IsImage = true;
                            if (ImageAttachments.Count < MaxImageCount)
                                ImageAttachments.Insert(0, localItem);
                        }
                    }
                    else
                    {
                        await ShowTip($"上传失败：{resp?.message ?? "未知错误"}\n（已仅本地显示）");
                    }
                }
            }
            catch (Exception ex)
            {
                await ShowTip($"选择/上传异常：{ex.Message}");
            }
        }

        private static string? DetectContentType(string? ext)
        {
            switch (ext?.ToLowerInvariant())
            {
                case "jpg":
                case "jpeg": return "image/jpeg";
                case "png": return "image/png";
                case "gif": return "image/gif";
                case "bmp": return "image/bmp";
                case "webp": return "image/webp";
                case "pdf": return "application/pdf";
                case "doc": return "application/msword";
                case "docx": return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                case "xls": return "application/vnd.ms-excel";
                case "xlsx": return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                case "txt": return "text/plain";
                case "rar": return "application/x-rar-compressed";
                case "zip": return "application/zip";
                default: return null; // 让 HttpClient 自行处理
            }
        }

        private static bool IsImageExt(string? ext)
            => ext is "jpg" or "jpeg" or "png" or "gif" or "bmp" or "webp";

        private static bool IsAllowedFile(string? ext)
            => IsImageExt(ext) || ext is "pdf" or "doc" or "docx" or "xls" or "xlsx" or "txt" or "rar" or "zip";


        /// <summary>
        /// 将前端的 Attachments/Items 回填到 Detail，用于提交
        /// </summary>
        private void PreparePayloadFromUi()
        {
            if (Detail is null) return;

            // 1) 附件：把 UI 集合映射回服务器字段
            Detail.devInspectTaskAttachmentList = Attachments.Select(a => new InspectionAttachment
            {
                attachmentExt = a.AttachmentExt,
                attachmentFolder = a.AttachmentFolder,
                attachmentLocation = a.AttachmentLocation,
                attachmentName = a.AttachmentName,
                attachmentRealName = a.AttachmentRealName,
                attachmentSize = a.AttachmentSize,
                attachmentUrl = a.AttachmentUrl,
                createdTime = a.CreatedTime,
                id = a.Id
            }).ToList();

            // 2) 明细：Items 已经是服务器的明细模型（你加载时就是 Detail.orderInspectionDetailList → Items）
            //    如果后端要求传回列表字段名叫 orderInspectionDetailList，则回填：
            Detail.devInspectTaskDetailList = Items?.ToList() ?? new List<InspectionItem>();

        }

        [RelayCommand]
        private async Task DownloadAttachment(OrderInspectionAttachmentItem? item)
        {
            if (item is null)
            {
                await ShowTip("无效的附件。");
                return;
            }

            try
            {
                // 1) 有线上链接：交给系统处理（浏览器/下载管理器）
                if (!string.IsNullOrWhiteSpace(item.AttachmentUrl))
                {
                    // 如果你的 URL 需要带 token，可以在这里拼接
                    await Launcher.Default.OpenAsync(new Uri(item.AttachmentUrl));
                    return;
                }

                // 2) 没有 URL，但本地有路径（刚选完未上传）→ 直接打开
                if (!string.IsNullOrWhiteSpace(item.LocalPath) && File.Exists(item.LocalPath))
                {
                    await Launcher.Default.OpenAsync(new OpenFileRequest
                    {
                        File = new ReadOnlyFile(item.LocalPath)
                    });
                    return;
                }

                await ShowTip("该附件没有可用的下载地址。");
            }
            catch (Exception ex)
            {
                await ShowTip($"下载/打开失败：{ex.Message}");
            }
        }

        [RelayCommand]
        private async Task DeleteAttachmentAsync(OrderInspectionAttachmentItem? item)
        {
            if (item is null) return;

            // 未上传（没有 Id）的本地占位，直接本地删除
            if (string.IsNullOrWhiteSpace(item.Id))
            {
                Attachments.Remove(item);
                if (item.IsImage) ImageAttachments.Remove(item);
                await ShowTip("已从本地移除（未上传到服务器）");
                return;
            }

            bool confirm = await Application.Current.MainPage.DisplayAlert(
                "确认删除",
                $"确定删除附件：{item.AttachmentName}？",
                "删除", "取消");
            if (!confirm) return;

            try
            {
                var resp = await _api.DeleteAttachmentAsync(item.Id, _cts?.Token ?? CancellationToken.None);
                if (resp?.success == true && resp.result)
                {
                    Attachments.Remove(item);
                    if (item.IsImage) ImageAttachments.Remove(item);
                    await ShowTip("删除成功");
                }
                else
                {
                    await ShowTip($"删除失败：{resp?.message ?? "未知错误"}");
                }
            }
            catch (Exception ex)
            {
                await ShowTip($"删除异常：{ex.Message}");
            }
        }



       


        private void RemoveFromCollections(OrderInspectionAttachmentItem item)
        {
            Attachments.Remove(item);
            if (item.IsImage && ImageAttachments.Contains(item))
                ImageAttachments.Remove(item);
        }
        // --------- 工具方法 ----------
        private static Task ShowTip(string msg) =>
            Application.Current?.MainPage?.DisplayAlert("提示", msg, "OK") ?? Task.CompletedTask;

        private async Task LoadWorkflowAsync(string id)
        {
            try
            {
                var baseSteps = new List<WorkflowVmItem>
        {
            new() { StatusValue = "0", Title = "待执行" },
            new() { StatusValue = "1", Title = "执行中" },
            new() { StatusValue = "2", Title = "已完成" },
        };

                var resp = await _api.GetWorkflowAsync(id, _cts.Token);
                var list = resp?.result ?? new List<InspectWorkflowNode>();

                // 回填时间 & 找“当前”
                int currentIndex = -1;
                for (int i = 0; i < baseSteps.Count; i++)
                {
                    var s = baseSteps[i];
                    s.StepNo = i + 1;
                    var hit = list.FirstOrDefault(x => string.Equals(x.statusValue, s.StatusValue, StringComparison.OrdinalIgnoreCase));
                    if (hit != null && !string.IsNullOrWhiteSpace(hit.statusTime))
                    {
                        s.Time = hit.statusTime.Split(' ')[0];
                        currentIndex = i; // 最后一个有时间的就是“当前”
                    }
                }

                // 标注 Completed/Current
                for (int i = 0; i < baseSteps.Count; i++)
                {
                    baseSteps[i].IsCurrent = (i == currentIndex);
                    baseSteps[i].IsCompleted = (i < currentIndex) && !string.IsNullOrWhiteSpace(baseSteps[i].Time);
                    baseSteps[i].IsLast = (i == baseSteps.Count - 1);
                }

                WorkflowSteps.Clear();
                foreach (var s in baseSteps) WorkflowSteps.Add(s);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("LoadWorkflowAsync error: " + ex.Message);
            }
        }


    }

}
