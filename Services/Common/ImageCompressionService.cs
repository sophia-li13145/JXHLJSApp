using Serilog;
using SkiaSharp;

namespace JXHLJSApp.Services.Common;

public interface IImageCompressionService
{
    Task<CompressedImageResult> CompressForOcrAsync(
        FileResult source,
        CancellationToken cancellationToken = default);
}

public sealed class CompressedImageResult
{
    public required FileResult File { get; init; }
    public bool IsTemporary { get; init; }
    public long OriginalBytes { get; init; }
    public long CompressedBytes { get; init; }
    public int OriginalWidth { get; init; }
    public int OriginalHeight { get; init; }
    public int OutputWidth { get; init; }
    public int OutputHeight { get; init; }
}

public sealed class ImageCompressionService : IImageCompressionService
{
    private const int MaxLongSide = 2000;
    private const int DecodeMaxLongSide = 3000;
    private const int JpegQuality = 85;
    private const long SkipBelowBytes = 1_500_000;

    public async Task<CompressedImageResult> CompressForOcrAsync(
        FileResult source,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await Task.Run(
                () => CompressForOcrInternal(source, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Warning(ex, "OCR图片压缩失败，使用原图上传。FileName={FileName}", source.FileName);
            return await CreateOriginalResultAsync(source).ConfigureAwait(false);
        }
    }

    private static CompressedImageResult CompressForOcrInternal(
        FileResult source,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var input = OpenSourceReadStream(source);
        using var codec = SKCodec.Create(input)
            ?? throw new InvalidOperationException("无法读取票签图片。 ");

        var originalWidth = codec.Info.Width;
        var originalHeight = codec.Info.Height;
        var orientedSize = GetOrientedSize(originalWidth, originalHeight, codec.EncodedOrigin);
        var originalBytes = GetFileSize(source);
        var shouldNormalizeToJpeg = !IsJpeg(source.FileName);

        if (!shouldNormalizeToJpeg &&
            originalBytes > 0 &&
            originalBytes <= SkipBelowBytes &&
            Math.Max(orientedSize.Width, orientedSize.Height) <= MaxLongSide)
        {
            return new CompressedImageResult
            {
                File = source,
                IsTemporary = false,
                OriginalBytes = originalBytes,
                CompressedBytes = originalBytes,
                OriginalWidth = orientedSize.Width,
                OriginalHeight = orientedSize.Height,
                OutputWidth = orientedSize.Width,
                OutputHeight = orientedSize.Height
            };
        }

        var decodeSampleSize = CalculateSampleSize(
            Math.Max(originalWidth, originalHeight),
            DecodeMaxLongSide);
        var decodeWidth = Math.Max(1, originalWidth / decodeSampleSize);
        var decodeHeight = Math.Max(1, originalHeight / decodeSampleSize);
        var decodeInfo = new SKImageInfo(
            decodeWidth,
            decodeHeight,
            SKColorType.Rgba8888,
            SKAlphaType.Premul);

        using var decoded = new SKBitmap(decodeInfo);
        var result = codec.GetPixels(
            decodeInfo,
            decoded.GetPixels(),
            new SKCodecOptions(decodeSampleSize));

        if (result is not SKCodecResult.Success and not SKCodecResult.IncompleteInput)
        {
            throw new InvalidOperationException($"票签图片解码失败：{result}");
        }

        cancellationToken.ThrowIfCancellationRequested();

        using var oriented = ApplyOrientation(decoded, codec.EncodedOrigin);
        var outputSize = CalculateOutputSize(oriented.Width, oriented.Height, MaxLongSide);
        using var resized = ResizeIfNeeded(oriented, outputSize.Width, outputSize.Height);
        using var image = SKImage.FromBitmap(resized);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality)
            ?? throw new InvalidOperationException("票签图片JPEG压缩失败。");

        var tempPath = Path.Combine(
            FileSystem.CacheDirectory,
            $"ocr-ticket-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.jpg");

        using (var output = File.OpenWrite(tempPath))
        {
            data.SaveTo(output);
        }

        var compressedBytes = new FileInfo(tempPath).Length;
        if (originalBytes > 0 && compressedBytes >= originalBytes)
        {
            TryDeleteFile(tempPath);
            return new CompressedImageResult
            {
                File = source,
                IsTemporary = false,
                OriginalBytes = originalBytes,
                CompressedBytes = originalBytes,
                OriginalWidth = orientedSize.Width,
                OriginalHeight = orientedSize.Height,
                OutputWidth = orientedSize.Width,
                OutputHeight = orientedSize.Height
            };
        }

        return new CompressedImageResult
        {
            File = new FileResult(tempPath, "image/jpeg"),
            IsTemporary = true,
            OriginalBytes = originalBytes,
            CompressedBytes = compressedBytes,
            OriginalWidth = orientedSize.Width,
            OriginalHeight = orientedSize.Height,
            OutputWidth = resized.Width,
            OutputHeight = resized.Height
        };
    }

    private static async Task<CompressedImageResult> CreateOriginalResultAsync(FileResult source)
    {
        var bytes = GetFileSize(source);
        var width = 0;
        var height = 0;

        try
        {
            await using var stream = await source.OpenReadAsync().ConfigureAwait(false);
            using var codec = SKCodec.Create(stream);
            if (codec is not null)
            {
                var orientedSize = GetOrientedSize(codec.Info.Width, codec.Info.Height, codec.EncodedOrigin);
                width = orientedSize.Width;
                height = orientedSize.Height;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "读取原始OCR图片信息失败。FileName={FileName}", source.FileName);
        }

        return new CompressedImageResult
        {
            File = source,
            IsTemporary = false,
            OriginalBytes = bytes,
            CompressedBytes = bytes,
            OriginalWidth = width,
            OriginalHeight = height,
            OutputWidth = width,
            OutputHeight = height
        };
    }

    private static Stream OpenSourceReadStream(FileResult source)
    {
        if (!string.IsNullOrWhiteSpace(source.FullPath) && File.Exists(source.FullPath))
        {
            return File.OpenRead(source.FullPath);
        }

        return source.OpenReadAsync().GetAwaiter().GetResult();
    }

    private static long GetFileSize(FileResult source)
    {
        if (!string.IsNullOrWhiteSpace(source.FullPath) && File.Exists(source.FullPath))
        {
            return new FileInfo(source.FullPath).Length;
        }

        try
        {
            using var stream = source.OpenReadAsync().GetAwaiter().GetResult();
            return stream.CanSeek ? stream.Length : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static int CalculateSampleSize(int originalLongSide, int decodeMaxLongSide)
    {
        var sampleSize = 1;
        while (originalLongSide / sampleSize > decodeMaxLongSide)
        {
            sampleSize *= 2;
        }

        return sampleSize;
    }

    private static (int Width, int Height) CalculateOutputSize(
        int width,
        int height,
        int maxLongSide)
    {
        var longSide = Math.Max(width, height);
        if (longSide <= maxLongSide)
        {
            return (width, height);
        }

        var scale = maxLongSide / (double)longSide;
        return (
            Math.Max(1, (int)Math.Round(width * scale)),
            Math.Max(1, (int)Math.Round(height * scale)));
    }

    private static SKBitmap ResizeIfNeeded(SKBitmap source, int width, int height)
    {
        if (source.Width == width && source.Height == height)
        {
            return source.Copy();
        }

        return source.Resize(
                   new SKImageInfo(width, height, source.ColorType, source.AlphaType),
                   SKFilterQuality.Medium)
               ?? throw new InvalidOperationException("票签图片缩放失败。");
    }

    private static SKBitmap ApplyOrientation(SKBitmap source, SKEncodedOrigin origin)
    {
        var swap = origin is SKEncodedOrigin.LeftTop
            or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom
            or SKEncodedOrigin.LeftBottom;
        var destination = new SKBitmap(
            swap ? source.Height : source.Width,
            swap ? source.Width : source.Height,
            source.ColorType,
            source.AlphaType);

        using var canvas = new SKCanvas(destination);
        switch (origin)
        {
            case SKEncodedOrigin.TopRight:
                canvas.Scale(-1, 1);
                canvas.Translate(-source.Width, 0);
                break;
            case SKEncodedOrigin.BottomRight:
                canvas.RotateDegrees(180, source.Width / 2f, source.Height / 2f);
                break;
            case SKEncodedOrigin.BottomLeft:
                canvas.Scale(1, -1);
                canvas.Translate(0, -source.Height);
                break;
            case SKEncodedOrigin.LeftTop:
                canvas.RotateDegrees(90);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.RightTop:
                canvas.Translate(destination.Width, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightBottom:
                canvas.Scale(-1, 1);
                canvas.Translate(-destination.Width, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.LeftBottom:
                canvas.Translate(0, destination.Height);
                canvas.RotateDegrees(270);
                break;
        }

        canvas.DrawBitmap(source, 0, 0);
        canvas.Flush();
        return destination;
    }

    private static (int Width, int Height) GetOrientedSize(
        int width,
        int height,
        SKEncodedOrigin origin) =>
        origin is SKEncodedOrigin.LeftTop
            or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom
            or SKEncodedOrigin.LeftBottom
            ? (height, width)
            : (width, height);

    private static bool IsJpeg(string? fileName) =>
        Path.GetExtension(fileName ?? string.Empty).Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
        Path.GetExtension(fileName ?? string.Empty).Equals(".jpeg", StringComparison.OrdinalIgnoreCase);

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "删除OCR临时压缩图片失败。Path={Path}", path);
        }
    }
}
