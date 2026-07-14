using System.Net;
using System.Net.Sockets;
using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Dahua;
using BuildTrack.Infrastructure.Data;
using BuildTrack.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BuildTrack.Worker.Dahua.Services;

public sealed class DahuaActiveRegisterHostedService(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    IDahuaActiveRegisterSdk dahuaSdk,
    ILogger<DahuaActiveRegisterHostedService> logger) : BackgroundService
{
    private readonly List<TcpListener> _listeners = [];
    private bool _singleDeviceFallbackEnabled;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var ports = ParsePorts(configuration["DAHUA_ACTIVE_REGISTER_PORTS"]);
        _singleDeviceFallbackEnabled = DahuaActiveRegisterFallbackMatcher.IsSingleDeviceFallbackEnabled(configuration["DAHUA_ACTIVE_REGISTER_ALLOW_SINGLE_DEVICE_FALLBACK"]);
        logger.LogInformation("Single-device fallback enabled: {Enabled}", _singleDeviceFallbackEnabled);
        await dahuaSdk.StartAsync(ports, stoppingToken);
        if (dahuaSdk.IsSdkListenerActive)
        {
            logger.LogInformation("Dahua NetSDK listener is active. Raw TCP fallback listener will not bind the same ports.");
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            return;
        }

        var tasks = ports.Select(port => RunListenerAsync(port, stoppingToken)).ToArray();
        await Task.WhenAll(tasks);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var listener in _listeners)
        {
            listener.Stop();
        }
        logger.LogInformation("Dahua Active Register listeners stopped");
        return base.StopAsync(cancellationToken);
    }

    private async Task RunListenerAsync(int port, CancellationToken stoppingToken)
    {
        var listener = new TcpListener(IPAddress.Any, port);
        _listeners.Add(listener);

        try
        {
            listener.Start();
            logger.LogInformation("Dahua Active Register listener started on TCP port {Port}", port);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start Dahua Active Register listener on TCP port {Port}", port);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var client = await listener.AcceptTcpClientAsync(stoppingToken);
                _ = Task.Run(() => HandleClientAsync(client, port, stoppingToken), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Dahua Active Register accept loop failed on port {Port}", port);
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, int listenerPort, CancellationToken stoppingToken)
    {
        using var _ = client;
        var endpoint = client.Client.RemoteEndPoint as IPEndPoint;
        var remoteIp = endpoint?.Address.ToString();
        var remotePort = endpoint?.Port;
        logger.LogInformation("Incoming Dahua register connection from {RemoteIp}:{RemotePort} on listener port {ListenerPort}", remoteIp, remotePort, listenerPort);

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<BuildTrackDbContext>();
            var connectionLogger = scope.ServiceProvider.GetRequiredService<IDeviceConnectionLogger>();

            var payload = await ReadInitialPayloadAsync(client, stoppingToken);
            var rawPayload = DahuaRawPayloadFormatter.CreateLogPayload(payload, listenerPort, remoteIp, remotePort);
            var registerDeviceId = TryExtractRegisterDeviceId(payload);
            Device? device = null;
            var matchedBySingleDeviceFallback = false;
            if (!string.IsNullOrWhiteSpace(registerDeviceId))
            {
                device = await db.Devices.FirstOrDefaultAsync(x => x.RegisterDeviceId == registerDeviceId, stoppingToken);
            }
            else
            {
                var candidates = await db.Devices
                    .Where(x => x.Mode == DeviceMode.ActiveRegister && x.RegisterPort == listenerPort)
                    .OrderBy(x => x.CreatedAt)
                    .Take(2)
                    .ToListAsync(stoppingToken);
                logger.LogDebug("Active register fallback check: enabled={Enabled}, listenerPort={ListenerPort}, candidateCount={CandidateCount}",
                    _singleDeviceFallbackEnabled,
                    listenerPort,
                    candidates.Count);
                device = DahuaActiveRegisterFallbackMatcher.MatchSingleDeviceFallback(candidates, _singleDeviceFallbackEnabled);
                matchedBySingleDeviceFallback = device is not null;

                if (device is null && candidates.Count > 1)
                {
                    logger.LogWarning("Single-device fallback skipped because multiple active register devices exist on this port.");
                }
            }

            if (device is null)
            {
                await connectionLogger.LogAsync(null, registerDeviceId, remoteIp, remotePort, "unmatched_register", "Incoming Active Register connection did not match a known device", rawPayload, stoppingToken);
                logger.LogInformation("Dahua raw Active Register payload saved as base64/hex");
                logger.LogWarning("Unmatched Dahua device register. RegisterDeviceId {RegisterDeviceId}, remote {RemoteIp}:{RemotePort}", registerDeviceId, remoteIp, remotePort);
                return;
            }

            device.Status = DeviceStatus.Online;
            device.LastKnownIp = remoteIp;
            device.LastSeenAt = DateTimeOffset.UtcNow;
            device.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(stoppingToken);
            var eventType = matchedBySingleDeviceFallback ? "matched_single_device_fallback" : "register";
            var message = matchedBySingleDeviceFallback
                ? "Matched Dahua Active Register connection by single-device fallback"
                : "Matched Dahua Active Register connection and marked device online";
            await connectionLogger.LogAsync(device.Id, device.RegisterDeviceId, remoteIp, remotePort, eventType, message, rawPayload, stoppingToken);
            logger.LogInformation("Dahua raw Active Register payload saved as base64/hex");
            if (matchedBySingleDeviceFallback)
            {
                logger.LogWarning("Matched Dahua connection by single-device fallback. Do not use in multi-device production.");
            }
            logger.LogInformation("Matched Dahua device {DeviceId} registerDeviceId {RegisterDeviceId}", device.Id, device.RegisterDeviceId);

            // Real-time event subscription must be done through Dahua NetSDK callbacks.
            // This TCP accept loop records connectivity and leaves the vendor protocol handling behind IDahuaActiveRegisterSdk.
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown path.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Dahua Active Register connection handling failed for {RemoteIp}:{RemotePort}. Listener continues running.", remoteIp, remotePort);
        }
    }

    private static async Task<byte[]> ReadInitialPayloadAsync(TcpClient client, CancellationToken stoppingToken)
    {
        if (!client.Connected) return [];

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        var buffer = new byte[1024];
        try
        {
            var stream = client.GetStream();
            if (!stream.CanRead) return [];
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), timeout.Token);
            return read <= 0 ? [] : buffer[..read];
        }
        catch (OperationCanceledException)
        {
            return [];
        }
        catch
        {
            return [];
        }
    }

    private static string? TryExtractRegisterDeviceId(byte[] payload)
    {
        if (payload.Length == 0) return null;
        var preview = DahuaRawPayloadFormatter.CreateAsciiPreview(payload);
        if (string.IsNullOrWhiteSpace(preview)) return null;

        var trimmed = preview.Trim('\r', '\n', ' ', '\t', '.');
        if (trimmed.Contains("DeviceID=", StringComparison.OrdinalIgnoreCase))
        {
            var parts = trimmed.Split(['\r', '\n', ';', '&'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var match = parts.FirstOrDefault(x => x.StartsWith("DeviceID=", StringComparison.OrdinalIgnoreCase) || x.StartsWith("DeviceId=", StringComparison.OrdinalIgnoreCase));
            return match?.Split('=', 2).ElementAtOrDefault(1);
        }

        if (trimmed.Length <= 160 && trimmed.All(ch => !char.IsControl(ch) && ch != '.')) return trimmed;
        return null;
    }

    private static int[] ParsePorts(string? raw) => (string.IsNullOrWhiteSpace(raw) ? "9500,7000" : raw)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(value => int.TryParse(value, out var port) ? port : 0)
        .Where(port => port > 0)
        .Distinct()
        .DefaultIfEmpty(9500)
        .ToArray();
}



