$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path (Split-Path $PSScriptRoot -Parent) 'Tools/ScannerOrientationTracker.cs'
$productionSource = (Get-Content -LiteralPath $sourcePath -Raw).Replace(
    'namespace JXHLJSApp.Tools;', 'namespace JXHLJSApp.ScannerOrientationTests;')
$regressionCases = @'

public static class ScannerOrientationRegression
{
    private static int _assertions;

    public static void Run()
    {
        _assertions = 0;
        CheckInitialRotations();
        CheckCardinalsAndRapidReversals();
        CheckFullCircles();
        CheckBoundaryHysteresis();
        CheckUnknownAndWrapAround();
        Console.WriteLine("PASS: " + _assertions + " scanner orientation assertions");
    }

    private static void CheckInitialRotations()
    {
        for (var surface = 0; surface < 4; surface++)
        {
            var tracker = new ScannerOrientationTracker(surface);
            Check(tracker.SurfaceRotation == surface, "Initial SurfaceRotation " + surface);
            var center = (360 - surface * 90) % 360;
            Update(tracker, center, false, surface, "Initial physical orientation " + center);
            Update(tracker, Wrap(center + 25), false, surface, "Same quadrant clockwise");
            Update(tracker, Wrap(center - 25), false, surface, "Same quadrant counterclockwise");
        }
        RejectInitial(-1);
        RejectInitial(4);
        Console.WriteLine("PASS: all initial display rotations and same-quadrant readings");
    }

    private static void CheckCardinalsAndRapidReversals()
    {
        var tracker = new ScannerOrientationTracker(0);
        Update(tracker, 90, true, 3, "Landscape clockwise");
        Update(tracker, 180, true, 2, "Upside down without display rotation");
        Update(tracker, 270, true, 1, "Landscape counterclockwise");
        Update(tracker, 0, true, 0, "Portrait");
        var readings = new[] { 180, 0, 270, 90, 270, 0, 180 };
        var expected = new[] { 2, 0, 1, 3, 1, 0, 2 };
        for (var index = 0; index < readings.Length; index++)
            Update(tracker, readings[index], true, expected[index], "Rapid reversal " + readings[index]);
        Console.WriteLine("PASS: four directions, portrait inversion and rapid reversals");
    }

    private static void CheckFullCircles()
    {
        var tracker = new ScannerOrientationTracker(0);
        var expected = 0;
        for (var degrees = 0; degrees < 360; degrees++)
        {
            var changed = degrees is 56 or 146 or 236 or 326;
            if (changed) expected = degrees switch { 56 => 3, 146 => 2, 236 => 1, _ => 0 };
            Update(tracker, degrees, changed, expected, "Clockwise circle " + degrees);
        }
        Update(tracker, 0, false, 0, "Clockwise wrap to zero");
        for (var degrees = 359; degrees >= 0; degrees--)
        {
            var changed = degrees is 304 or 214 or 124 or 34;
            if (changed) expected = degrees switch { 304 => 1, 214 => 2, 124 => 3, _ => 0 };
            Update(tracker, degrees, changed, expected, "Counterclockwise circle " + degrees);
        }
        Console.WriteLine("PASS: complete circles in both directions with exact transition points");
    }

    private static void CheckBoundaryHysteresis()
    {
        for (var center = 0; center < 360; center += 90)
        {
            foreach (var direction in new[] { -1, 1 })
            {
                var initial = ToSurface(center);
                var next = ToSurface(Wrap(center + direction * 90));
                var tracker = new ScannerOrientationTracker(initial);
                foreach (var offset in new[] { 44, 46, 45, 54, 55, 48, 53 })
                    Update(tracker, Wrap(center + direction * offset), false, initial, "Boundary jitter before switch");
                Update(tracker, Wrap(center + direction * 56), true, next, "Cross switch threshold");
                foreach (var offset in new[] { 55, 46, 44, 54, 45, 35 })
                    Update(tracker, Wrap(center + direction * offset), false, next, "Boundary jitter after switch");
                Update(tracker, Wrap(center + direction * 34), true, initial, "Cross reverse threshold");
            }
        }
        Console.WriteLine("PASS: ten-degree hysteresis on every boundary in both directions");
    }

    private static void CheckUnknownAndWrapAround()
    {
        var tracker = new ScannerOrientationTracker(0);
        foreach (var degrees in new[] { 359, 0, 1, 358, 2 })
            Update(tracker, degrees, false, 0, "Zero-degree wrap jitter");
        Update(tracker, 180, true, 2, "Physical portrait inversion");
        foreach (var invalid in new[] { -1, -2, 360, 720 })
            Update(tracker, invalid, false, 2, "Unknown or invalid reading");
        Update(tracker, 0, true, 0, "Recover from unknown reading");
        Console.WriteLine("PASS: unknown readings, wrap-around and recovery");
    }

    private static int Wrap(int degrees) => (degrees % 360 + 360) % 360;
    private static int ToSurface(int degrees) => (4 - degrees / 90) % 4;

    private static void Update(ScannerOrientationTracker tracker, int degrees, bool expectedChanged, int expectedSurface, string scenario)
    {
        var changed = tracker.TryUpdate(degrees, out var surface);
        Check(changed == expectedChanged && surface == expectedSurface && tracker.SurfaceRotation == expectedSurface,
            scenario + ": degrees=" + degrees + ", changed=" + changed + ", surface=" + surface);
    }

    private static void RejectInitial(int surface)
    {
        try { _ = new ScannerOrientationTracker(surface); }
        catch (ArgumentOutOfRangeException) { Check(true, "Invalid initial rotation rejected"); return; }
        Check(false, "Invalid initial rotation accepted: " + surface);
    }

    private static void Check(bool condition, string scenario)
    {
        _assertions++;
        if (!condition) throw new Exception(scenario);
    }
}
'@
# Compile the actual pure-managed tracker and its cases together; no Android SDK or NuGet is needed.
$testSource = "#nullable enable`nusing System;`n" + $productionSource + $regressionCases
Add-Type -TypeDefinition $testSource
[JXHLJSApp.ScannerOrientationTests.ScannerOrientationRegression]::Run()
