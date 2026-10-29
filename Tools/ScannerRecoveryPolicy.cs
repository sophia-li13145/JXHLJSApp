namespace JXHLJSApp.Tools;

internal enum ScannerRecoveryDecision
{
    None,
    Recover,
    LimitReached
}

// Keep this policy for the entire user scan session, including automatic camera rebuilds.
internal sealed class ScannerRecoveryPolicy
{
    private static readonly TimeSpan StallThreshold = TimeSpan.FromSeconds(6);
    private const int ConsecutiveErrorThreshold = 10;
    private const int MaximumAttempts = 2;

    public int Attempts { get; private set; }

    public ScannerRecoveryDecision Evaluate(bool foreground, bool streaming,
        TimeSpan frameAge, TimeSpan completionAge, int consecutiveErrors)
    {
        if (!foreground || !streaming) return ScannerRecoveryDecision.None;

        var unhealthy = frameAge >= StallThreshold || completionAge >= StallThreshold ||
            consecutiveErrors >= ConsecutiveErrorThreshold;
        if (!unhealthy) return ScannerRecoveryDecision.None;
        if (Attempts >= MaximumAttempts) return ScannerRecoveryDecision.LimitReached;

        Attempts++;
        return ScannerRecoveryDecision.Recover;
    }

    // Only a user-ended scan session should reset the recovery budget.
    public void Reset() => Attempts = 0;
}
