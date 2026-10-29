$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path (Split-Path $PSScriptRoot -Parent) 'Tools/ScannerRecoveryPolicy.cs'
$productionSource = (Get-Content -LiteralPath $sourcePath -Raw).Replace(
    'namespace JXHLJSApp.Tools;', 'namespace JXHLJSApp.ScannerRecoveryTests;')
$regressionCases = @'

public static class ScannerRecoveryRegression
{
    private static int _assertions;
    private static readonly TimeSpan Zero = TimeSpan.Zero;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(6);

    public static void Run()
    {
        _assertions = 0;
        CheckHealthyAndReadOnlyProbes();
        CheckEachFailureSignal();
        CheckInactiveStates();
        CheckBudgetAndReset();
        Console.WriteLine("PASS: " + _assertions + " scanner recovery assertions");
    }

    private static void CheckHealthyAndReadOnlyProbes()
    {
        var policy = new ScannerRecoveryPolicy();
        Check(policy.Attempts == 0, "Initial budget");
        var justBelowTimeout = TimeSpan.FromTicks(Timeout.Ticks - 1);
        for (var index = 0; index < 1000; index++)
        {
            Evaluate(policy, true, true, Zero, Zero, 0, ScannerRecoveryDecision.None, 0, "Healthy poll");
            Check(policy.Attempts == 0, "Reading Attempts must not consume budget");
        }
        Evaluate(policy, true, true, justBelowTimeout, justBelowTimeout, 9,
            ScannerRecoveryDecision.None, 0, "Every signal below threshold");
        Evaluate(policy, true, true, TimeSpan.FromSeconds(-1), TimeSpan.FromSeconds(-1), -1,
            ScannerRecoveryDecision.None, 0, "Negative ages are not a stall");
        Console.WriteLine("PASS: healthy polls, read-only probes and exact threshold boundaries");
    }

    private static void CheckEachFailureSignal()
    {
        var noFrames = new ScannerRecoveryPolicy();
        Evaluate(noFrames, true, true, Timeout, Zero, 0, ScannerRecoveryDecision.Recover, 1, "No new frame for six seconds");

        var stalledProcessing = new ScannerRecoveryPolicy();
        Evaluate(stalledProcessing, true, true, Zero, Timeout, 0, ScannerRecoveryDecision.Recover, 1, "Frames arrive but processing stalls");

        var continuousErrors = new ScannerRecoveryPolicy();
        Evaluate(continuousErrors, true, true, Zero, Zero, 9, ScannerRecoveryDecision.None, 0, "Nine consecutive errors");
        Evaluate(continuousErrors, true, true, Zero, Zero, 10, ScannerRecoveryDecision.Recover, 1, "Ten consecutive errors");

        var combined = new ScannerRecoveryPolicy();
        Evaluate(combined, true, true, TimeSpan.FromHours(1), TimeSpan.FromHours(1), 100,
            ScannerRecoveryDecision.Recover, 1, "Combined failures consume one attempt");
        Console.WriteLine("PASS: no-frame stall, processing stall, persistent errors and combined signals");
    }

    private static void CheckInactiveStates()
    {
        var policy = new ScannerRecoveryPolicy();
        var stale = TimeSpan.FromDays(1);
        for (var index = 0; index < 100; index++)
        {
            Evaluate(policy, false, true, stale, stale, 100, ScannerRecoveryDecision.None, 0, "Background timeout");
            Evaluate(policy, true, false, stale, stale, 100, ScannerRecoveryDecision.None, 0, "Idle timeout");
            Evaluate(policy, false, false, stale, stale, 100, ScannerRecoveryDecision.None, 0, "Background and Idle");
        }
        Evaluate(policy, true, true, stale, Zero, 0, ScannerRecoveryDecision.Recover, 1, "Foreground recovery after inactive polls");
        Evaluate(policy, false, true, stale, stale, 100, ScannerRecoveryDecision.None, 1, "Background preserves used budget");
        Evaluate(policy, true, false, stale, stale, 100, ScannerRecoveryDecision.None, 1, "Idle preserves used budget");
        Console.WriteLine("PASS: background and Idle timeouts never consume recovery budget");
    }

    private static void CheckBudgetAndReset()
    {
        var policy = new ScannerRecoveryPolicy();
        Evaluate(policy, true, true, Timeout, Zero, 0, ScannerRecoveryDecision.Recover, 1, "First recovery");
        // A successful automatic rebuild can resume healthy frames, but must retain the session budget.
        Evaluate(policy, true, true, Zero, Zero, 0, ScannerRecoveryDecision.None, 1, "Healthy after first rebuild");
        Evaluate(policy, true, true, Zero, Timeout, 0, ScannerRecoveryDecision.Recover, 2, "Second recovery");
        Evaluate(policy, true, true, Zero, Zero, 0, ScannerRecoveryDecision.None, 2, "Healthy after second rebuild");
        for (var index = 0; index < 100; index++)
            Evaluate(policy, true, true, Timeout, Timeout, 10, ScannerRecoveryDecision.LimitReached, 2, "Persistent failure after limit");
        Evaluate(policy, false, true, Timeout, Timeout, 10, ScannerRecoveryDecision.None, 2, "Background after limit");
        Evaluate(policy, true, false, Timeout, Timeout, 10, ScannerRecoveryDecision.None, 2, "Idle after limit");
        policy.Reset();
        Check(policy.Attempts == 0, "User exits scanner: reset budget");
        Evaluate(policy, true, true, Zero, Zero, 0, ScannerRecoveryDecision.None, 0, "Healthy new scan session");
        Evaluate(policy, true, true, Timeout, Zero, 0, ScannerRecoveryDecision.Recover, 1, "New session receives full budget");
        policy.Reset();
        policy.Reset();
        Check(policy.Attempts == 0, "Reset is idempotent");
        Console.WriteLine("PASS: two-attempt limit, retained rebuild budget and user-exit reset");
    }

    private static void Evaluate(ScannerRecoveryPolicy policy, bool foreground, bool streaming,
        TimeSpan frameAge, TimeSpan completionAge, int consecutiveErrors,
        ScannerRecoveryDecision expected, int expectedAttempts, string scenario)
    {
        var decision = policy.Evaluate(foreground, streaming, frameAge, completionAge, consecutiveErrors);
        Check(decision == expected && policy.Attempts == expectedAttempts,
            scenario + ": decision=" + decision + ", attempts=" + policy.Attempts);
    }

    private static void Check(bool condition, string scenario)
    {
        _assertions++;
        if (!condition) throw new Exception(scenario);
    }
}
'@
# Use the actual production policy in an isolated namespace so other Add-Type regressions can coexist.
$testSource = "#nullable enable`nusing System;`n" + $productionSource + $regressionCases
Add-Type -TypeDefinition $testSource
[JXHLJSApp.ScannerRecoveryTests.ScannerRecoveryRegression]::Run()
