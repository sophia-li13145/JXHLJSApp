$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$zxingPath = Join-Path $env:USERPROFILE '.nuget/packages/zxing.net/0.16.9/lib/netstandard2.0/zxing.dll'
if (-not (Test-Path -LiteralPath $zxingPath)) { throw '请先还原项目的 ZXing.Net 0.16.9 依赖。' }
Add-Type -Path $zxingPath
$productionSources = @('Tools/CameraLuminanceBuffer.cs', 'Tools/CameraBarcodeDecoder.cs') | ForEach-Object {
    $source = Get-Content -LiteralPath (Join-Path $repoRoot $_) -Raw
    $source -replace '(?m)^using [^\r\n]+;\r?$', '' -replace 'namespace JXHLJSApp.Tools;', ''
}
$regressionCases = @'
public static class CameraBarcodeRotationRegression
{
    private const int Width = 640;
    private const int Height = 480;
    private const string QrText = "MACHINE-QR-ANY-ANGLE-20261027";
    private const string BarcodeText = "MACHINE-001";

    public static void Run()
    {
        CheckPixelRotations();
        var formats = new List<BarcodeFormat>();
        foreach (var format in System.Enum.GetValues<BarcodeFormat>())
            if (format != BarcodeFormat.All_1D) formats.Add(format);
        var decoder = new CameraBarcodeDecoder(formats, true, true, false, false);
        var qr = new ZXing.QrCode.QRCodeWriter().encode(QrText, BarcodeFormat.QR_CODE, 240, 240);
        var count = 0;
        for (var angle = 0; angle < 360; angle++)
        {
            var image = Render(qr, angle);
            for (var metadata = 0; metadata < 360; metadata += 90)
            {
                AssertDecoded(decoder, image, metadata, QrText, "QR angle=" + angle + ", metadata=" + metadata);
                count++;
            }
        }
        System.Console.WriteLine("PASS: " + count + " QR angle / CameraX orientation combinations (0-359 degrees)");

        var barcode = new ZXing.OneD.Code128Writer().encode(BarcodeText, BarcodeFormat.CODE_128, 400, 100);
        for (var angle = 0; angle < 360; angle += 90)
            for (var metadata = 0; metadata < 360; metadata += 90)
                AssertDecoded(decoder, Render(barcode, angle), metadata, BarcodeText, "Code128 angle=" + angle + ", metadata=" + metadata);
        System.Console.WriteLine("PASS: 16 Code128 direction / CameraX orientation combinations");

        var padded = new byte[(Width + 13) * Height];
        var tilted = Render(qr, 37);
        for (var y = 0; y < Height; y++) System.Array.Copy(tilted, y * Width, padded, y * (Width + 13), Width);
        var packed = new byte[Width * Height];
        CameraLuminanceBuffer.Copy(padded, packed, Width, Height, Width + 13, 1);
        for (var metadata = 0; metadata < 360; metadata += 90)
            AssertDecoded(decoder, packed, metadata, QrText, "padded QR metadata=" + metadata);
        System.Console.WriteLine("PASS: 4 padded tilted QR frames with CameraX orientation");

        var blank = new byte[Width * Height];
        System.Array.Fill(blank, (byte)255);
        if (decoder.Decode(blank, Width, Height, 90) != null) throw new System.Exception("Blank frame produced a result");
        // A failed frame must not poison the reader's cached decoder state.
        AssertDecoded(decoder, Render(qr, 213), 270, QrText, "QR after blank frame");
        System.Console.WriteLine("PASS: blank-frame rejection and subsequent rotated QR");
    }

    private static void CheckPixelRotations()
    {
        var pixels = new byte[] { 1, 2, 3, 4, 5, 6 };
        var expected = new byte[][] {
            new byte[] { 1, 2, 3, 4, 5, 6 }, new byte[] { 4, 1, 5, 2, 6, 3 },
            new byte[] { 6, 5, 4, 3, 2, 1 }, new byte[] { 3, 6, 2, 5, 1, 4 }
        };
        for (var quarter = 0; quarter < 4; quarter++)
        {
            var rotated = CameraLuminanceBuffer.RotateClockwise(pixels, 3, 2, quarter * 90, out var width, out var height);
            if (width != (quarter % 2 == 0 ? 3 : 2) || height != (quarter % 2 == 0 ? 2 : 3) ||
                !System.MemoryExtensions.SequenceEqual<byte>(rotated, expected[quarter]))
                throw new System.Exception("Non-square pixel rotation failed: " + quarter * 90);
        }
        System.Console.WriteLine("PASS: 4 non-square pixel rotations with exact dimension and pixel checks");
    }

    private static byte[] Render(BitMatrix symbol, int angle)
    {
        var output = new byte[Width * Height];
        System.Array.Fill(output, (byte)255);
        var radians = angle * System.Math.PI / 180;
        var cos = System.Math.Cos(radians);
        var sin = System.Math.Sin(radians);
        for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
            {
                var dx = x - (Width - 1) / 2.0;
                var dy = y - (Height - 1) / 2.0;
                var sx = (int)System.Math.Round(cos * dx + sin * dy + (symbol.Width - 1) / 2.0);
                var sy = (int)System.Math.Round(-sin * dx + cos * dy + (symbol.Height - 1) / 2.0);
                if (sx >= 0 && sx < symbol.Width && sy >= 0 && sy < symbol.Height && symbol[sx, sy])
                    output[y * Width + x] = 0;
            }
        return output;
    }

    private static void AssertDecoded(CameraBarcodeDecoder decoder, byte[] pixels, int metadata, string expected, string scenario)
    {
        var result = decoder.Decode(pixels, Width, Height, metadata);
        if (result == null || result.Length != 1 || result[0].Text != expected)
            throw new System.Exception(scenario + " failed: " + (result == null ? "no result" : result[0].Text));
    }
}
'@
# Keep this harness isolated so both regression scripts can run in the same PowerShell session.
$testSource = "#nullable enable`nusing System;`nusing System.Collections.Generic;`nusing ZXing;`nusing ZXing.Common;`nnamespace JXHLJSApp.ScannerRotationTests {`n" + ($productionSources -join "`n") + $regressionCases + "`n}"
$frameworkReferences = @(Get-ChildItem -LiteralPath (Join-Path $PSHOME ref) -Filter '*.dll' | ForEach-Object FullName)
Add-Type -TypeDefinition $testSource -ReferencedAssemblies (@($zxingPath) + $frameworkReferences)
[JXHLJSApp.ScannerRotationTests.CameraBarcodeRotationRegression]::Run()
