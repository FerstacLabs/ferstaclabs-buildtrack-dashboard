using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Services;

namespace BuildTrack.Tests;

public sealed class SecuritySnapshotTests
{
    [Fact]
    public void Validate_RejectsHttpStatusTextResponse()
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes("HTTP/1.1 401 Unauthorized\r\nContent-Type: text/html\r\n").Concat(new byte[1200]).ToArray();

        var result = SecuritySnapshotValidator.Validate(bytes, "text/plain");

        Assert.False(result.IsValid);
        Assert.Contains("HTTP status", result.Error);
    }

    [Fact]
    public void Validate_RejectsHtmlResponse()
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes("<html><body>401</body></html>").Concat(new byte[1200]).ToArray();

        var result = SecuritySnapshotValidator.Validate(bytes, "text/html");

        Assert.False(result.IsValid);
        Assert.Contains("HTML", result.Error);
    }

    [Fact]
    public void Validate_RejectsSmallUnauthorizedResponse()
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes("Unauthorized");

        var result = SecuritySnapshotValidator.Validate(bytes, "text/plain");

        Assert.False(result.IsValid);
        Assert.Contains("too small", result.Error);
    }

    [Fact]
    public void Validate_AcceptsValidJpegNonBlackBytes()
    {
        var bytes = NonBlackJpegBytes();

        var result = SecuritySnapshotValidator.Validate(bytes, "application/octet-stream");

        Assert.True(result.IsValid);
        Assert.False(result.IsBlack);
    }

    [Fact]
    public void Validate_RejectsValidJpegAllBlackBytes()
    {
        var bytes = new byte[1400];
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;

        var result = SecuritySnapshotValidator.Validate(bytes, "image/jpeg");

        Assert.False(result.IsValid);
        Assert.True(result.IsBlack);
        Assert.Contains("black", result.Error);
    }

    [Fact]
    public void AttemptPlanner_TriesLiveChannelsAfterEventSnapshotCanBeRejectedAsBlack()
    {
        var attempts = SecuritySnapshotAttemptPlanner.BuildAttempts("192.168.31.174", "/mnt/appdata1/userpic/SnapShot/test.jpg");

        Assert.Equal("DahuaEventRpcLoadfile", attempts[0].Source);
        Assert.Equal("LiveSnapshotChannel0", attempts[1].Source);
        Assert.Equal("LiveSnapshotChannel1", attempts[2].Source);
        Assert.Equal("LiveSnapshotChannel2", attempts[3].Source);
    }

    [Fact]
    public async Task SnapshotFileReader_ReturnsLocalFileWhenStoredSnapshotPathExists()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"buildtrack-security-{Guid.NewGuid()}.jpg");
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xAA };
        await File.WriteAllBytesAsync(tempFile, bytes);
        try
        {
            var securityEvent = new SecurityEvent
            {
                StoredSnapshotPath = tempFile,
                StoredSnapshotContentType = "image/jpeg",
            };

            var result = await SecuritySnapshotFileReader.TryReadAsync(securityEvent);

            Assert.True(result.Exists);
            Assert.Equal("image/jpeg", result.ContentType);
            Assert.Equal(bytes, result.Bytes);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task SnapshotFileReader_ReturnsNotFoundWhenSnapshotIsNotStored()
    {
        var securityEvent = new SecurityEvent
        {
            StoredSnapshotPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid()}.jpg"),
        };

        var result = await SecuritySnapshotFileReader.TryReadAsync(securityEvent);

        Assert.False(result.Exists);
    }

    private static byte[] NonBlackJpegBytes()
    {
        var bytes = new byte[1400];
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;
        for (var index = 128; index < bytes.Length; index++)
        {
            bytes[index] = (byte)((index * 37) % 251 + 1);
        }

        return bytes;
    }
}
