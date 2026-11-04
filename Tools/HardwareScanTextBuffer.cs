namespace JXHLJSApp.Tools;

/// <summary>Coalesces scanner text injection, including Newland's default output without Enter.</summary>
internal sealed class HardwareScanTextBuffer
{
    // Low-end PDA keyboard wedges may pause while the UI/JIT processes the first key.
    // A 150 ms window could submit the first character as a complete machine number.
    internal const int SettleMilliseconds = 750;
    private string? _text;
    private int _generation;
    private long _readyAt;

    public void Update(string? text, int generation, long now)
    {
        _text = string.IsNullOrWhiteSpace(text) ? null : text;
        _generation = generation;
        _readyAt = now + (text?.EndsWith('\r') == true || text?.EndsWith('\n') == true ? 0 : SettleMilliseconds);
    }

    public bool TryTake(int generation, long now, out string? text)
    {
        text = null;
        if (generation != _generation)
        {
            Reset();
            return false;
        }
        if (_text is null || now < _readyAt) return false;
        text = _text;
        Reset();
        return true;
    }

    public void Reset() => _text = null;
}
