namespace JXHLJSApp.Models;

public sealed class TransportOrderDto
{
    public string? currentMachineCode { get; set; }
    public string? currentMachineName { get; set; }
    public string? currentMachineNo { get; set; }
    public string? currentProcess { get; set; }
    public string? materialCode { get; set; }
    public string? materialName { get; set; }
    public string? nextMachineCode { get; set; }
    public string? nextMachineName { get; set; }
    public string? nextMachineNo { get; set; }
    public string? nextProcess { get; set; }
    public List<TransportOperationTraceDto>? operationTraceList { get; set; }
    public string? productionAddress { get; set; }
    public decimal? quantity { get; set; }
    public string? rawOrQuench { get; set; }
    public string? spec { get; set; }
    public string? steelGrade { get; set; }
    public decimal? totalQuantity { get; set; }
    public decimal? totalWeight { get; set; }
    public string? transportOrderNo { get; set; }
    public string? unit { get; set; }
    public decimal? weight { get; set; }
    public string? workOrderNo { get; set; }
}

public sealed class TransportOperationTraceDto
{
    public string? executionStatus { get; set; }
    public string? finishTime { get; set; }
    public string? machineCode { get; set; }
    public string? machineName { get; set; }
    public string? machineNo { get; set; }
    public string? operationName { get; set; }
    public decimal? operationSeq { get; set; }
    public string? operationTaskId { get; set; }
    public string? shiftCode { get; set; }
    public string? shiftName { get; set; }
    public string? startTime { get; set; }
    public string? workOrderNo { get; set; }
    public string? workOrderStatus { get; set; }
}
