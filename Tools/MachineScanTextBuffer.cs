namespace JXHLJSApp.Tools;

/// <summary>Waits for a complete machine code from PDA text output, including output without Enter.</summary>
internal sealed class MachineScanTextBuffer
{
    public const int SettleMilliseconds = 750;
    private string? _text;
    private int _revision;
    private long _readyAt;

    public void Update(string? text, int revision, long now)
    {
        _text = string.IsNullOrWhiteSpace(text) ? null : text;
        _revision = revision;
        _readyAt = now + SettleMilliseconds;
    }

    public bool TryTake(int revision, long now, out string? code)
    {
        code = null;
        if (revision != _revision) { Reset(); return false; }
        if (_text is null || now < _readyAt) return false;
        code = _text;
        Reset();
        return true;
    }

    public void Reset() => _text = null;
}
