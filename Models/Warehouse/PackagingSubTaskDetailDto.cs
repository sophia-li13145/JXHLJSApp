namespace JXHLJSApp.Models.Warehouse;

public sealed class PackagingSubTaskDetailDto
{
    public string? id { get; set; }
    public string? materialCode { get; set; }
    public string? materialName { get; set; }
    public string? materialProperty { get; set; }
    public string? memo { get; set; }
    public bool? needPackagingCloth { get; set; }
    public bool? needPalletizing { get; set; }
    public string? operationCode { get; set; }
    public string? operationName { get; set; }
    public string? operationTaskNo { get; set; }
    public string? otherRequirement { get; set; }
    public string? packageMethod { get; set; }
    public string? packageWeight { get; set; }
    public string? packagingClothColor { get; set; }
    public string? workOrderId { get; set; }
    public string? workOrderNo { get; set; }
    public string? workOrderStatus { get; set; }

    public string taskNoDisplay => Display(operationTaskNo);
    public string materialNameDisplay => Display(materialName);
    public string materialCodeDisplay => Display(materialCode);
    public string materialPropertyDisplay => Display(materialProperty);
    public string memoDisplay => Display(memo);
    public string needPackagingClothDisplay => needPackagingCloth == true ? "是" : "否";
    public string needPalletizingDisplay => needPalletizing == true ? "打托" : "不打托";
    public string packageMethodDisplay => Display(packageMethod);
    public string packageWeightDisplay => Display(packageWeight);
    public string packagingClothColorDisplay => Display(packagingClothColor);
    public string otherRequirementDisplay => Display(otherRequirement);
    public string workOrderNoDisplay => Display(workOrderNo);

    private static string Display(string? value) => string.IsNullOrWhiteSpace(value) ? "无" : value!;
}
