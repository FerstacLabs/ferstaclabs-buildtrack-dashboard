using System.Reflection;

namespace BuildTrack.Infrastructure.Services;

public sealed record SecuritySnapshotValidationResult(bool IsValid, string? Error = null, string FirstBytesHex = "", double? AverageBrightness = null, bool IsBlack = false);

public static class SecuritySnapshotValidator
{
    public const int MinimumJpegBytes = 1000;
    private const double BlackBrightnessThreshold = 8.0;

    public static SecuritySnapshotValidationResult Validate(byte[] bytes, string? contentType)
    {
        var firstBytesHex = FirstBytesHex(bytes);
        if (bytes.Length <= MinimumJpegBytes)
        {
            return new SecuritySnapshotValidationResult(false, $"image response too small ({bytes.Length} bytes)", firstBytesHex);
        }

        if (StartsWithAscii(bytes, "HTTP/1.1") || StartsWithAscii(bytes, "HTTP/1.0"))
        {
            return new SecuritySnapshotValidationResult(false, "response body starts with HTTP status text", firstBytesHex);
        }

        if (StartsWithAscii(bytes, "<html") || StartsWithAscii(bytes, "<!doctype") || StartsWithAscii(bytes, "<HTML"))
        {
            return new SecuritySnapshotValidationResult(false, "response body is HTML, not JPEG", firstBytesHex);
        }

        var hasJpegMagic = bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
        var hasJpegContentType = contentType?.Contains("image/jpeg", StringComparison.OrdinalIgnoreCase) == true
            || contentType?.Contains("image/jpg", StringComparison.OrdinalIgnoreCase) == true;

        if (!hasJpegMagic && !hasJpegContentType)
        {
            return new SecuritySnapshotValidationResult(false, $"response is not JPEG. ContentType={contentType ?? "unknown"}", firstBytesHex);
        }

        if (!hasJpegMagic)
        {
            return new SecuritySnapshotValidationResult(false, "response content-type says JPEG but bytes do not start with FF D8 FF", firstBytesHex);
        }

        var brightness = TryCalculateAverageBrightness(bytes) ?? EstimateCompressedBrightness(bytes);
        var isBlack = brightness < BlackBrightnessThreshold;
        if (isBlack)
        {
            return new SecuritySnapshotValidationResult(false, $"snapshot appears black. AverageBrightness={brightness:F2}", firstBytesHex, brightness, true);
        }

        return new SecuritySnapshotValidationResult(true, FirstBytesHex: firstBytesHex, AverageBrightness: brightness, IsBlack: false);
    }

    public static string FirstBytesHex(byte[] bytes, int count = 8) => string.Join(" ", bytes.Take(count).Select(value => value.ToString("X2")));

    private static double? TryCalculateAverageBrightness(byte[] bytes)
    {
        try
        {
            var drawingAssembly = Assembly.Load("System.Drawing.Common");
            var imageType = drawingAssembly.GetType("System.Drawing.Image");
            var bitmapType = drawingAssembly.GetType("System.Drawing.Bitmap");
            if (imageType is null || bitmapType is null) return null;

            using var stream = new MemoryStream(bytes);
            var fromStream = imageType.GetMethod("FromStream", [typeof(Stream)]);
            var image = fromStream?.Invoke(null, [stream]);
            if (image is null) return null;
            using var imageDisposable = image as IDisposable;
            var bitmap = Activator.CreateInstance(bitmapType, image);
            if (bitmap is null) return null;
            using var bitmapDisposable = bitmap as IDisposable;

            var width = (int)(bitmapType.GetProperty("Width")?.GetValue(bitmap) ?? 0);
            var height = (int)(bitmapType.GetProperty("Height")?.GetValue(bitmap) ?? 0);
            var getPixel = bitmapType.GetMethod("GetPixel", [typeof(int), typeof(int)]);
            if (width <= 0 || height <= 0 || getPixel is null) return null;

            var stepX = Math.Max(1, width / 32);
            var stepY = Math.Max(1, height / 32);
            double total = 0;
            var count = 0;
            for (var y = 0; y < height; y += stepY)
            {
                for (var x = 0; x < width; x += stepX)
                {
                    var color = getPixel.Invoke(bitmap, [x, y]);
                    if (color is null) continue;
                    var colorType = color.GetType();
                    var r = (byte)(colorType.GetProperty("R")?.GetValue(color) ?? (byte)0);
                    var g = (byte)(colorType.GetProperty("G")?.GetValue(color) ?? (byte)0);
                    var b = (byte)(colorType.GetProperty("B")?.GetValue(color) ?? (byte)0);
                    total += (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
                    count++;
                }
            }

            return count == 0 ? null : total / count;
        }
        catch
        {
            return null;
        }
    }

    private static double EstimateCompressedBrightness(byte[] bytes)
    {
        var scanData = ExtractScanData(bytes);
        if (scanData.Length == 0) return 255;

        var sample = scanData.Length > 2048 ? scanData.Take(2048).ToArray() : scanData;
        var lowValueRatio = sample.Count(value => value <= 0x10 || value == 0xFF || value == 0x00) / (double)sample.Length;
        var uniqueRatio = sample.Distinct().Count() / (double)sample.Length;
        var mean = sample.Average(value => (double)value);

        if (lowValueRatio > 0.88 || uniqueRatio < 0.035) return 0;
        return Math.Clamp(mean, 0, 255);
    }

    private static byte[] ExtractScanData(byte[] bytes)
    {
        for (var index = 0; index < bytes.Length - 1; index++)
        {
            if (bytes[index] == 0xFF && bytes[index + 1] == 0xDA)
            {
                if (index + 4 >= bytes.Length) return [];
                var segmentLength = (bytes[index + 2] << 8) + bytes[index + 3];
                var start = index + 2 + segmentLength;
                if (start >= bytes.Length) return [];
                var end = bytes.Length - 2;
                for (var scan = start; scan < bytes.Length - 1; scan++)
                {
                    if (bytes[scan] == 0xFF && bytes[scan + 1] == 0xD9)
                    {
                        end = scan;
                        break;
                    }
                }

                return bytes[start..end];
            }
        }

        return bytes.Skip(Math.Min(128, bytes.Length)).ToArray();
    }

    private static bool StartsWithAscii(byte[] bytes, string value)
    {
        if (bytes.Length < value.Length) return false;
        for (var index = 0; index < value.Length; index++)
        {
            var expected = (byte)value[index];
            var actual = bytes[index];
            if (actual == expected) continue;
            if (char.IsLetter(value[index]) && actual == (byte)char.ToLowerInvariant(value[index])) continue;
            return false;
        }

        return true;
    }
}
