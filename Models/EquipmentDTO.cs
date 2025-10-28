using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace IndustrialControlMAUI.Models;

    public class InspectionRecordDto
    {
    /// <summary>主键ID</summary>
    public string? id { get; set; }

    /// <summary>设备名称</summary>
    public string? devName { get; set; }

    public string? devCode { get; set; }

    /// <summary>工厂编码</summary>
    public string? factoryCode { get; set; }

    /// <summary>工厂名称</summary>
    public string? factoryName { get; set; }

    /// <summary>质检单号</summary>
    public string? inspectNo { get; set; }

    /// <summary>检验状态（0-新建；1-待检验；2-检验中；3-检验完成）</summary>
    public string? inspectStatus { get; set; }
    public string? inspectStatusText { get; set; }

    public string? inspectResult { get; set; }

    public string? inspecter { get; set; }

    /// <summary>检验数量</summary>
    public decimal? inspectNum { get; set; }

    /// <summary>合格总数</summary>
    public decimal? qualifiedNum { get; set; }

    /// <summary>不合格总数</summary>
    public decimal? unqualifiedNum { get; set; }

    /// <summary>计划编码</summary>
    public string? planCode { get; set; }

    /// <summary>计划名称</summary>
    public string? planName { get; set; }

    /// <summary>计划检验日期</summary>
    public string? planInspectTime { get; set; }

    /// <summary>实际检验时间</summary>
    public string? inspectTime { get; set; }

    /// <summary>备注</summary>
    public string? memo { get; set; }

    /// <summary>创建时间</summary>
    public string? createdTime { get; set; }

    /// <summary>修改时间</summary>
    public string? modifiedTime { get; set; }
}

/// <summary>
/// 设备
/// </summary>
public class InspectionOrderItem
{
    /// <summary>主键ID</summary>
    public string? Id { get; set; }

    /// <summary>设备名称</summary>
    public string? DevName { get; set; }

    /// <summary>设备编码</summary>
    public string? DevCode { get; set; }

    /// <summary>工厂编码</summary>
    public string? FactoryCode { get; set; }

    /// <summary>工厂名称</summary>
    public string? FactoryName { get; set; }

    /// <summary>质检单号</summary>
    public string? InspectNo { get; set; }

    /// <summary>检验状态（0-新建；1-待检验；2-检验中；3-检验完成）</summary>
    public string? InspectStatus { get; set; }

    /// <summary>检验状态名称（映射显示用）</summary>
    public string? InspectStatusText { get; set; }

    public string? InspectResult { get; set; }

    /// <summary>检验员</summary>
    public string? Inspecter { get; set; }

    /// <summary>检验数量</summary>
    public decimal? InspectNum { get; set; }

    /// <summary>合格总数</summary>
    public decimal? QualifiedNum { get; set; }

    /// <summary>不合格总数</summary>
    public decimal? UnqualifiedNum { get; set; }

    /// <summary>计划编码</summary>
    public string? PlanCode { get; set; }

    /// <summary>计划名称</summary>
    public string? PlanName { get; set; }

    /// <summary>计划检验日期</summary>
    public DateTime? PlanInspectTime { get; set; }

    /// <summary>实际检验时间</summary>
    public DateTime? InspectTime { get; set; }

    /// <summary>备注</summary>
    public string? Memo { get; set; }

    /// <summary>创建时间</summary>
    public DateTime? CreatedTime { get; set; }

    /// <summary>修改时间</summary>
    public DateTime? ModifiedTime { get; set; }
}

public class DictInspection
{
    public List<DictItem> InspectStatus { get; set; } = new();
}


public class InspectionDetailDto : ObservableObject
{
    public string? id { get; set; }
    /// <summary>设备编码</summary>
    public string? devCode { get; set; }

    /// <summary>工厂编码</summary>
    public string? factoryCode { get; set; }

    /// <summary>工厂名称</summary>
    public string? factoryName { get; set; }

    /// <summary>质检单号</summary>
    public string? inspectNo { get; set; }

    /// <summary>检验状态（0-新建；1-待检验；2-检验中；3-检验完成）</summary>
    public string? inspectStatus { get; set; }

    /// <summary>检验状态名称（映射显示用）</summary>
    public string? inspectStatusText { get; set; }

    public string? inspectResult { get; set; }

    /// <summary>检验员</summary>
    public string? inspecter { get; set; }

    /// <summary>检验数量</summary>
    public decimal? inspectNum { get; set; }

    public string? inspectMemo { get; set; }

    /// <summary>合格总数</summary>
    public decimal? qualifiedNum { get; set; }

    /// <summary>不合格总数</summary>
    public decimal? unqualifiedNum { get; set; }

    /// <summary>计划编码</summary>
    public string? planCode { get; set; }

    /// <summary>计划名称</summary>
    public string? planName { get; set; }

    /// <summary>计划检验日期</summary>
    public string? planInspectTime { get; set; }

    /// <summary>实际检验时间</summary>
    public string? inspectTime { get; set; }

    /// <summary>备注</summary>
    public string? memo { get; set; }

    /// <summary>创建时间</summary>
    public string? createdTime { get; set; }

    /// <summary>修改时间</summary>
    public string? modifiedTime { get; set; }


    public List<InspectionItem>? devInspectTaskDetailList { get; set; } = new();
    public List<InspectionAttachment>? devInspectTaskAttachmentList { get; set; } = new();


}



public class InspectionMaterial
{
    public string? materialCode { get; set; }
    public string? materialName { get; set; }
    public string? model { get; set; }
    public string? spec { get; set; }
    public decimal? qty { get; set; }               // 生产/到货数量
    public string? unit { get; set; }
}

public partial class InspectionItem : ObservableObject
{
    public int? index { get; set; }
    public string? id { get; set; }
    public string? itemName { get; set; }
    public string? itemCode { get; set; }
    public string? inspectionStandard { get; set; }
    public string? inspectionMode { get; set; }
    public string? inspectionAttributeName { get; set; }
    public string? inspectionAttribute { get; set; }
    public string? inspectValue { get; set; }
    public string? inspectResult { get; set; }
    public string? inspectNo { get; set; }

    public string? memo { get; set; }
    public string? upperLimit { get; set; }
    public string? lowerLimit { get; set; }
    public string? standardValue { get; set; }
    public string? unit { get; set; }
}

public class InspectionAttachment
{
    public string? id { get; set; }
    public string? attachmentName { get; set; }
    public string? attachmentRealName { get; set; }
    public string? attachmentUrl { get; set; }
    public string? attachmentExt { get; set; }
    public string? attachmentLocation { get; set; } // main/table
    public decimal? attachmentSize { get; set; }     // KB
    public string? attachmentFolder { get; set; }
    public string? createdTime { get; set; }
}

public partial class OrderInspectionAttachmentItem : ObservableObject
{
    // 只保留这一个：使用 MVVM Toolkit 自动生成 Public LocalPath
    [ObservableProperty]
    [JsonIgnore]                 // 不序列化给后端
    private string? localPath;

    public bool IsImage { get; set; }   // 只要它为 true 才进缩略图

    // 统一用 PascalCase 命名，别和小写混用
    public string? AttachmentUrl { get; set; }
    public string? AttachmentName { get; set; }
    public string? AttachmentRealName { get; set; }
    public string? AttachmentExt { get; set; }
    public string? AttachmentFolder { get; set; }
    public string? AttachmentLocation { get; set; }
    public long AttachmentSize { get; set; }
    public string? Id { get; set; }
    public string? CreatedTime { get; set; }
    public string? Memo { get; set; }
    public bool IsUploaded { get; set; }
    // 预览接口返回的直连 URL（短期有效）
    private string? _previewUrl;
    public string? PreviewUrl { get => _previewUrl; set => SetProperty(ref _previewUrl, value); }

    // 供 XAML 绑定：优先显示 Preview → Local → 原地址
    public string? DisplaySource => PreviewUrl ?? LocalPath ?? AttachmentUrl;

    // 通知 UI 刷新 DisplaySource
    public void RefreshDisplay() => OnPropertyChanged(nameof(DisplaySource));
}

public class InspectWorkflowNode
{
    public string? statusValue { get; set; }  // "0" | "1" | "2"
    public string? statusName { get; set; }  // "新建"、"待检验" 等（仅展示用）
    public string? statusTime { get; set; }  // "2025-01-02 12:34:56"
}
public class WorkflowVmItem
{
    public string StatusValue { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Time { get; set; }

    // 计算/标注用
    public int StepNo { get; set; }           // 1,2,3
    public bool IsCompleted { get; set; }     // 已完成（在当前之前且有时间）
    public bool IsCurrent { get; set; }       // 当前节点（最后一个有时间的）
    public bool IsActive => IsCompleted || IsCurrent; // 蓝色描边/连线的条件
    public bool IsLast { get; set; }
}
