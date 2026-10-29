$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path (Split-Path $PSScriptRoot -Parent) 'Tools/CameraLuminanceBuffer.cs'
$regressionCases = @'

public static class CameraLuminanceRegression
{
    public static void Run()
    {
        Check("packed rows", new byte[] { 1, 2, 3, 4, 5, 6 }, 3, 2, 3, 1, new byte[] { 1, 2, 3, 4, 5, 6 });
        Check("row padding, truncated final padding", new byte[] { 1, 2, 3, 99, 99, 4, 5, 6 }, 3, 2, 5, 1, new byte[] { 1, 2, 3, 4, 5, 6 });
        Check("pixel and row padding", new byte[] { 1, 99, 2, 99, 3, 99, 99, 4, 99, 5, 99, 6 }, 3, 2, 7, 2, new byte[] { 1, 2, 3, 4, 5, 6 });
        Check("single pixel", new byte[] { 7 }, 1, 1, 8, 2, new byte[] { 7 });
        Reject("incomplete plane", new byte[] { 1, 2, 3 }, new byte[4], 2, 2, 2, 1);
        Reject("short destination", new byte[] { 1, 2, 3, 4 }, new byte[3], 2, 2, 2, 1);
        Reject("overlapping rows", new byte[] { 1, 2, 3, 4 }, new byte[4], 2, 2, 1, 1);
        Reject("invalid dimensions", new byte[] { 1 }, new byte[1], 0, 1, 1, 1);
        System.Console.WriteLine("PASS: 8 camera luminance regression cases");
    }

    private static void Check(string name, byte[] source, int width, int height, int rowStride, int pixelStride, byte[] expected)
    {
        var actual = new byte[width * height];
        CameraLuminanceBuffer.Copy(source, actual, width, height, rowStride, pixelStride);
        if (!System.MemoryExtensions.SequenceEqual<byte>(actual, expected))
            throw new System.Exception(name + " failed");
    }

    private static void Reject(string name, byte[] source, byte[] destination, int width, int height, int rowStride, int pixelStride)
    {
        try { CameraLuminanceBuffer.Copy(source, destination, width, height, rowStride, pixelStride); }
        catch (System.ArgumentException) { return; }
        throw new System.Exception(name + " should have failed");
    }
}
'@
# Compile the actual production helper and the cases into the same assembly; no NuGet restore is needed.
Add-Type -TypeDefinition ("using System;`n" + (Get-Content -LiteralPath $sourcePath -Raw) + $regressionCases)
[JXHLJSApp.Tools.CameraLuminanceRegression]::Run()
