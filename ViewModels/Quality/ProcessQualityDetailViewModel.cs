using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IndustrialControlMAUI.Models;
using IndustrialControlMAUI.Services;
using System.Collections.ObjectModel;
using System.Net.Mail;
using Xamarin.Google.Crypto.Tink.Shaded.Protobuf;
using static Android.Telecom.Call;

namespace IndustrialControlMAUI.ViewModels
{
    /// <summary>
    /// 质检单详情页 VM
    /// </summary>
    public partial class ProcessQualityDetailViewModel : ObservableObject, IQueryAttributable
    {
        private readonly IQualityApi _api;
        private readonly IAuthApi _authApi;
        public ObservableCollection<OrderQualityAttachmentItem> Attachments { get; } = new();

        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private QualityDetailDto? detail;

        // 明细与附件集合（用于列表绑定）
        public ObservableCollection<QualityItem> Items { get; } = new();

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
                    // 选中后回写到 Detail.inspectResult
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

        public ProcessQualityDetailViewModel(IQualityApi api, IAuthApi authApi)
        {
            _api = api;

            // 默认选项（也可以从字典接口加载）
            InspectResultOptions.Add(new StatusOption { Text = "合格", Value = "合格" });
            InspectResultOptions.Add(new StatusOption { Text = "不合格", Value = "不合格" });
            InspectorSuggestions = new ObservableCollection<UserInfoDto>();
            AllUsers = new List<UserInfoDto>();
            _authApi = authApi;
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
                    IsInspectorDropdownOpen = !string.IsNullOrWhiteSpace(value) && InspectorSuggestions.Count > 0;
                }
            }
        }

        [ObservableProperty] private bool isInspectorDropdownOpen;
        [RelayCommand]
        public async Task LoadInspectorsAsync()
        {
            try
            {
                AllUsers = await _authApi.GetAllUsersAsync();

                // 如果已有 inspecter（账号）值，让显示框展示“姓名(账号)”
                if (!string.IsNullOrWhiteSpace(Detail?.inspecter))
                {
                    var hit = AllUsers.FirstOrDefault(u =>
                        string.Equals(u.username, Detail.inspecter, StringComparison.OrdinalIgnoreCase));
                    InspectorText = hit is null
                        ? Detail.inspecter
                        : BuildDisplay(hit);
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

            // 存账号到后端字段
            Detail.inspecter = user.username;
            // 输入框显示 姓名(账号)
            InspectorText = BuildDisplay(user);

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

            // 按 姓名/账号/手机/邮箱 模糊匹配
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

        private static string BuildDisplay(UserInfoDto u)
        {
            var name = string.IsNullOrWhiteSpace(u.realname) ? u.username : u.realname;
            var account = u.username;
            return string.IsNullOrWhiteSpace(name) ? (account ?? "") :
                   string.IsNullOrWhiteSpace(account) ? name! : $"{name} ({account})";
        }
        /// <summary>
        /// Shell 路由入参，例如：.../ProcessQualityDetailPage?id=xxxx
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

                // ===== 明细 =====
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Items.Clear();
                    int i = 1;
                    foreach (var it in Detail.orderQualityDetailList ?? new())
                    {
                        it.index = i++; // 1,2,3...
                        Items.Add(it);
                    }
                }); // 注意：删掉你原来第二个重复的 foreach

                // ===== 附件（把后端 DTO → 前端项）=====
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Attachments.Clear();

                    foreach (var at in (Detail.orderQualityAttachmentList ?? new List<QualityAttachment>()))
                    {
                        // 可按需过滤：没有URL的跳过
                        if (string.IsNullOrWhiteSpace(at.attachmentUrl))
                            continue;

                        Attachments.Add(new OrderQualityAttachmentItem
                        {
                            AttachmentExt = at.attachmentExt ?? "",
                            AttachmentFolder = at.attachmentFolder ?? "",
                            AttachmentLocation = at.attachmentLocation ?? "",
                            AttachmentName = at.attachmentName ?? "",
                            AttachmentRealName = at.attachmentRealName ?? "",
                            AttachmentSize = at.attachmentSize,
                            AttachmentUrl = at.attachmentUrl ?? "",
                            Id = at.id ?? "",
                            Memo = at.memo ?? "",
                            LocalPath = null,
                            IsUploaded = true
                        });
                    }
                });
                // 说明：XAML 的缩略图请绑定 Source="{Binding DisplaySource}"，
                // DisplaySource 会优先使用 LocalPath，否则用 attachmentUrl

                // ===== 下拉选中项：检验结果 =====
                SelectedInspectResult = InspectResultOptions
                    .FirstOrDefault(o =>
                        string.Equals(o.Value, Detail.inspectResult, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(o.Text, Detail.inspectResult, StringComparison.OrdinalIgnoreCase));

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

        /// <summary>
        /// 保存（示例：仅做本地校验与提示；如需调用后端保存接口，按你后端补齐）
        /// </summary>
        [RelayCommand]
        private async Task Save()
        {
            if (Detail is null)
            {
                await ShowTip("没有可保存的数据。");
                return;
            }

            // TODO: 在此处调用你的“保存详情”接口
            await ShowTip("已保存（示例）。");
        }

        /// <summary>
        /// 完成质检（示例：仅做提示；按你的后端接口补齐）
        /// </summary>
        [RelayCommand]
        private async Task Complete()
        {
            if (Detail is null)
            {
                await ShowTip("没有可提交的数据。");
                return;
            }

            // TODO: 在此处调用你的“完成质检/提交”接口
            await ShowTip("已完成质检（示例）。");
        }

        /// <summary>
        /// 预览附件（使用系统浏览器/查看器打开 URL）
        /// </summary>
        [RelayCommand]
        private async Task PreviewAttachment(QualityAttachment? att)
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

        // 多选（相册/文件）
        [RelayCommand]
        private async Task PickImagesAsync()
        {
            var pick = await FilePicker.PickMultipleAsync(new PickOptions
            {
                PickerTitle = "选择图片",
                FileTypes = FilePickerFileType.Images
            });
            if (pick == null) return;

            foreach (var f in pick)
                await AddLocalAttachmentAsync(f);
        }


        // 删除（前端集合中移除）
        [RelayCommand]
        private void RemoveAttachment(OrderQualityAttachmentItem? item)
        {
            if (item == null) return;
            try
            {
                if (!string.IsNullOrWhiteSpace(item.LocalPath) && File.Exists(item.LocalPath))
                    File.Delete(item.LocalPath);
            }
            catch { /* 忽略删除失败 */ }

            Attachments.Remove(item);
        }

        /// <summary>
        /// 把 FileResult 保存到本地临时文件，并加入 Attachments（UI 立即显示缩略图）
        /// </summary>
        private async Task AddLocalAttachmentAsync(FileResult file)
        {
            // 1) 基础校验（体积/格式）
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant(); // .jpg/.png/...
            var allow = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            if (!allow.Contains(ext))
            {
                await Application.Current.MainPage.DisplayAlert("提示", $"不支持的格式：{ext}", "OK");
                return;
            }

            using var s = await file.OpenReadAsync();
            if (s.Length > 20 * 1024 * 1024)
            {
                await Application.Current.MainPage.DisplayAlert("提示", $"单文件不能超过20MB：{file.FileName}", "OK");
                return;
            }

            // 2) 复制到缓存目录（防止临时流失效，也便于后续统一上传）
            var tempDir = FileSystem.CacheDirectory;
            var local = Path.Combine(tempDir, $"{Guid.NewGuid()}{ext}");
            using (var fs = File.Create(local))
                await s.CopyToAsync(fs);
            var size = new FileInfo(local).Length;

            // 3) 加入前端集合（UI 立即刷新并显示缩略图）
            Attachments.Add(new OrderQualityAttachmentItem
            {
                AttachmentExt = ext.TrimStart('.'),
                AttachmentName = Path.GetFileNameWithoutExtension(file.FileName),
                AttachmentRealName = file.FileName,
                AttachmentSize = size,
                LocalPath = local,
                IsUploaded = false       // 还未上传到服务端
            });
        }

        // --------- 工具方法 ----------

        private static Task ShowTip(string msg) =>
            Application.Current?.MainPage?.DisplayAlert("提示", msg, "OK") ?? Task.CompletedTask;

        public void BindExistingAttachments(IEnumerable<OrderQualityAttachmentDto>? list)
        {
            Attachments.Clear();
            if (list == null) return;

            foreach (var d in list)
            {
                // 可选：过滤无效/空URL
                if (string.IsNullOrWhiteSpace(d.attachmentUrl)) continue;

                Attachments.Add(new OrderQualityAttachmentItem
                {
                    // 后端字段
                    AttachmentExt = d.attachmentExt ?? "",
                    AttachmentFolder = d.attachmentFolder ?? "",
                    AttachmentLocation = d.attachmentLocation ?? "",
                    AttachmentName = d.attachmentName ?? "",
                    AttachmentRealName = d.attachmentRealName ?? "",
                    AttachmentSize = d.attachmentSize,
                    AttachmentUrl = d.attachmentUrl,
                    Id = d.id ?? "",
                    Memo = d.memo ?? "",

                    // 前端标记
                    LocalPath = null,     // 老数据没有本地文件
                    IsUploaded = true      // 已经在服务器
                });
            }
        }
        private bool ExistsByIdOrUrl(string? id, string? url) =>
    Attachments.Any(x =>
        (!string.IsNullOrEmpty(id) && string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase)) ||
        (!string.IsNullOrEmpty(url) && string.Equals(x.AttachmentUrl, url, StringComparison.OrdinalIgnoreCase)));

        public void MergeExistingAttachments(IEnumerable<OrderQualityAttachmentDto>? list)
        {
            if (list == null) return;
            foreach (var d in list)
            {
                if (string.IsNullOrWhiteSpace(d.attachmentUrl)) continue;
                if (ExistsByIdOrUrl(d.id, d.attachmentUrl)) continue;

                Attachments.Add(new OrderQualityAttachmentItem
                {
                    AttachmentExt = d.attachmentExt ?? "",
                    AttachmentFolder = d.attachmentFolder ?? "",
                    AttachmentLocation = d.attachmentLocation ?? "",
                    AttachmentName = d.attachmentName ?? "",
                    AttachmentRealName = d.attachmentRealName ?? "",
                    AttachmentSize = d.attachmentSize,
                    AttachmentUrl = d.attachmentUrl,
                    Id = d.id ?? "",
                    Memo = d.memo ?? "",
                    LocalPath = null,
                    IsUploaded = true
                });
            }
        }


        //[RelayCommand]
        //private async Task SaveAllAsync()
        //{
        //    // 1) 逐个上传未上传的图片，得到 attachmentUrl / id
        //    foreach (var a in Attachments.Where(x => !x.IsUploaded).ToList())
        //    {
        //        if (string.IsNullOrWhiteSpace(a.LocalPath) || !File.Exists(a.LocalPath)) continue;
        //        using var fs = File.OpenRead(a.LocalPath);
        //        var up = await _api.UploadAsync(fs, a.attachmentRealName, CancellationToken.None);
        //        if (up == null || string.IsNullOrWhiteSpace(up.Url))
        //        {
        //            await ShowTip($"上传失败：{a.attachmentRealName}");
        //            return;
        //        }
        //        a.attachmentUrl = up.Url;
        //        a.id = up.Id ?? "";
        //        a.IsUploaded = true;
        //    }

        //    // 2) 组装完整请求体（把页面上其它字段一并带上）
        //    var req = new OrderQualitySaveReq
        //    {
        //        arrivalQty = ArrivalQty,
        //        id = Id ?? "",
        //        inspectRemark = InspectRemark ?? "",
        //        inspectResult = InspectResult ?? "",
        //        inspectTime = string.IsNullOrWhiteSpace(InspectTime)
        //                        ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        //                        : InspectTime,
        //        inspecter = Inspecter ?? "",
        //        inspectionLossQty = InspectionLossQty,
        //        inspectionObject = InspectionObject ?? "",
        //        inspectionSchemeName = InspectionSchemeName ?? "",
        //        inspectionSchemeTypeName = InspectionSchemeTypeName ?? "",
        //        orderNumber = OrderNumber ?? "",

        //        orderQualityAttachmentList = Attachments.Select(a => new OrderQualityAttachmentDto
        //        {
        //            attachmentExt = a.attachmentExt,
        //            attachmentFolder = a.attachmentFolder,
        //            attachmentLocation = a.attachmentLocation,
        //            attachmentName = a.attachmentName,
        //            attachmentRealName = a.attachmentRealName,
        //            attachmentSize = a.attachmentSize,
        //            attachmentUrl = a.attachmentUrl,
        //            id = a.id,
        //            memo = a.memo
        //        }).ToList(),

        //        orderQualityDetailList = Details.Select(d => new OrderQualityDetailDto
        //        {
        //            badCause = d.badCause,
        //            badQty = d.badQty,
        //            badRate = d.badRate,
        //            defect = d.defect,
        //            id = d.id,
        //            inspectResult = d.inspectResult,
        //            inspectionAttributeName = d.inspectionAttributeName,
        //            inspectionCode = d.inspectionCode,
        //            inspectionMode = d.inspectionMode,
        //            inspectionName = d.inspectionName,
        //            inspectionStandard = d.inspectionStandard,
        //            inspectionTypeName = d.inspectionTypeName,
        //            lowerLimit = d.lowerLimit,
        //            sampleQty = d.sampleQty,
        //            standardValue = d.standardValue,
        //            unit = d.unit,
        //            upperLimit = d.upperLimit
        //        }).ToList(),

        //        passRate = PassRate,
        //        processCode = ProcessCode ?? "",
        //        processName = ProcessName ?? "",
        //        qualityType = QualityType ?? "",
        //        qualityTypeName = QualityTypeName ?? "",
        //        retainedSampleNumber = RetainedSampleNumber ?? "",
        //        retainedSampleQty = RetainedSampleQty,
        //        samplingDefectRate = SamplingDefectRate,
        //        supplierName = SupplierName ?? "",
        //        totalBad = TotalBad,
        //        totalQualified = TotalQualified,
        //        totalSampling = TotalSampling,
        //        totalUnqualified = TotalUnqualified
        //    };

        //    // 3) 提交
        //    var ok = await _api.SaveOrderQualityAsync(req);
        //    await ShowTip(ok ? "保存成功" : "保存失败");
        //}
    }
}
