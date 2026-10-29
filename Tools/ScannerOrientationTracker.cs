namespace JXHLJSApp.Tools;

// Tracks physical device orientation even when Android keeps the display in portrait.
internal sealed class ScannerOrientationTracker
{
    private const int SwitchThresholdDegrees = 45 + 10;

    public ScannerOrientationTracker(int initialSurfaceRotation)
    {
        if (initialSurfaceRotation is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(initialSurfaceRotation));
        SurfaceRotation = initialSurfaceRotation;
    }

    public int SurfaceRotation { get; private set; }

    public bool TryUpdate(int orientationDegrees, out int surfaceRotation)
    {
        surfaceRotation = SurfaceRotation;
        // ORIENTATION_UNKNOWN (-1), and any invalid vendor reading, must preserve the last direction.
        if (orientationDegrees is < 0 or >= 360) return false;

        // Surface rotation runs in the opposite direction to OrientationEventListener degrees.
        var currentOrientation = (360 - SurfaceRotation * 90) % 360;
        var delta = (orientationDegrees - currentOrientation + 540) % 360 - 180;
        // Keep the current quadrant until the device passes its boundary by ten degrees.
        if (Math.Abs(delta) <= SwitchThresholdDegrees) return false;

        var quadrant = ((orientationDegrees + 45) / 90) % 4;
        var nextSurfaceRotation = (4 - quadrant) % 4;
        if (nextSurfaceRotation == SurfaceRotation) return false;

        SurfaceRotation = nextSurfaceRotation;
        surfaceRotation = nextSurfaceRotation;
        return true;
    }
}
