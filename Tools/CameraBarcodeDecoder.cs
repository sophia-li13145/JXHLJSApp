using ZXing;
using ZXing.Common;

namespace JXHLJSApp.Tools;

internal sealed class CameraBarcodeDecoder
{
    private readonly BarcodeReaderGeneric _reader;
    private readonly bool _multiple;

    public CameraBarcodeDecoder(IList<BarcodeFormat> formats, bool autoRotate, bool tryHarder, bool tryInverted, bool multiple)
    {
        _multiple = multiple;
        _reader = new BarcodeReaderGeneric
        {
            AutoRotate = autoRotate,
            Options = new DecodingOptions
            {
                PossibleFormats = formats,
                TryHarder = tryHarder,
                TryInverted = tryInverted
            }
        };
    }

    public Result[]? Decode(byte[] luminance, int width, int height, int rotationDegrees)
    {
        // CameraX reports the clockwise rotation needed to align the sensor frame with the target.
        // Use a managed source so every rotation works on the same, tightly packed pixel data.
        if (rotationDegrees % 360 != 0)
            luminance = CameraLuminanceBuffer.RotateClockwise(luminance, width, height, rotationDegrees, out width, out height);
        var source = new RGBLuminanceSource(luminance, width, height, RGBLuminanceSource.BitmapFormat.Gray8);
        if (_multiple) return _reader.DecodeMultiple(source);
        var result = _reader.Decode(source);
        return result is null ? null : new[] { result };
    }
}
