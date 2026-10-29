namespace JXHLJSApp.Tools;

internal static class CameraLuminanceBuffer
{
    public static byte[] RotateClockwise(ReadOnlySpan<byte> source, int width, int height,
        int rotationDegrees, out int rotatedWidth, out int rotatedHeight)
    {
        if (width <= 0 || height <= 0 || source.Length < checked(width * height))
            throw new ArgumentException("摄像头亮度缓冲区尺寸不匹配。");
        var rotation = ((rotationDegrees % 360) + 360) % 360;
        if (rotation % 90 != 0)
            throw new ArgumentOutOfRangeException(nameof(rotationDegrees), "相机方向必须为 90 度的整数倍。");
        rotatedWidth = rotation is 90 or 270 ? height : width;
        rotatedHeight = rotation is 90 or 270 ? width : height;
        var output = new byte[checked(width * height)];
        if (rotation == 0)
        {
            source[..output.Length].CopyTo(output);
            return output;
        }
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var destination = rotation switch
                {
                    90 => x * height + height - 1 - y,
                    180 => output.Length - 1 - (y * width + x),
                    _ => (width - 1 - x) * height + y
                };
                output[destination] = source[y * width + x];
            }
        }
        return output;
    }

    // CameraX Y planes may have padding between rows or pixels; ZXing requires width * height bytes.
    public static void Copy(ReadOnlySpan<byte> source, Span<byte> destination,
        int width, int height, int rowStride, int pixelStride)
    {
        if (width <= 0 || height <= 0 || pixelStride <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "图像尺寸及像素步长必须为正数。");
        var rowLength = checked((width - 1) * pixelStride + 1);
        var requiredLength = checked((height - 1) * rowStride + rowLength);
        if (rowStride < rowLength || source.Length < requiredLength || destination.Length < checked(width * height))
            throw new ArgumentException("摄像头亮度缓冲区尺寸不匹配。");

        for (var y = 0; y < height; y++)
        {
            var row = source.Slice(y * rowStride, rowLength);
            var output = destination.Slice(y * width, width);
            if (pixelStride == 1) row.CopyTo(output);
            else
                for (var x = 0; x < width; x++) output[x] = row[x * pixelStride];
        }
    }
}
