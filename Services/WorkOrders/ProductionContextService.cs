namespace JXHLJSApp.Services.WorkOrders;

public sealed class ProductionContext
{
    public string WorkOrderId { get; init; } = string.Empty;

    public string WorkOrderNo { get; init; } = string.Empty;

    public string? ExecutionId { get; init; }

    public string? MachineCode { get; init; }

    public string? Status { get; init; }

    public DateTime StartedAt { get; init; }

    public Guid SessionId { get; init; } = Guid.NewGuid();
}

public interface IProductionContextService
{
    ProductionContext? Current { get; }

    bool HasActiveWorkOrder { get; }

    event EventHandler<ProductionContext?>? ContextChanged;

    void Set(ProductionContext context);

    void Clear();

    bool IsCurrent(Guid sessionId);
}

public sealed class ProductionContextService : IProductionContextService
{
    private readonly object _syncRoot = new();

    private ProductionContext? _current;

    public ProductionContext? Current
    {
        get
        {
            lock (_syncRoot)
            {
                return _current;
            }
        }
    }

    public bool HasActiveWorkOrder => Current is not null;

    public event EventHandler<ProductionContext?>? ContextChanged;

    public void Set(ProductionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        lock (_syncRoot)
        {
            _current = context;
        }

        ContextChanged?.Invoke(this, context);
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            _current = null;
        }

        ContextChanged?.Invoke(this, null);
    }

    public bool IsCurrent(Guid sessionId)
    {
        lock (_syncRoot)
        {
            return _current?.SessionId == sessionId;
        }
    }
}
