using BuildTrack.Domain.Dahua;
using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Dahua;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildTrack.Tests;

public sealed class DahuaSdkAccessEventNormalizerTests
{
    [Fact]
    public void TryNormalize_MapsSuccessfulFaceEventToDahuaAccessRecord()
    {
        var sdkEvent = new DahuaSdkAccessEvent
        {
            UserId = "1",
            CardName = "Ilham",
            Status = "success",
            Method = "face",
            Direction = "entry",
            RecNo = 123,
            EventTime = DateTimeOffset.Parse("2026-07-07T08:15:00Z"),
            SnapshotPath = "/snapshots/123.jpg",
        };

        var ok = DahuaSdkAccessEventNormalizer.TryNormalize(sdkEvent, out var record);

        Assert.True(ok);
        Assert.Equal("1", record.UserId);
        Assert.Equal("Ilham", record.CardName);
        Assert.Equal(AttendanceEventStatus.Ok, record.NormalizedStatus);
        Assert.Equal(AttendanceMethod.Face, record.NormalizedMethod);
        Assert.Equal(AttendanceDirection.Entry, record.NormalizedDirection);
        Assert.True(DahuaSdkAccessEventNormalizer.ShouldInsertPayrollAttendance(record));
    }

    [Fact]
    public void ShouldInsertPayrollAttendance_SkipsFailedAndStrangerEvents()
    {
        var failed = new DahuaSdkAccessEvent
        {
            UserId = "1",
            CardName = "Ilham",
            Status = "0",
            Method = "15",
            Direction = "Entry",
        };
        var stranger = new DahuaSdkAccessEvent
        {
            Status = "1",
            Method = "15",
            Direction = "Entry",
        };

        DahuaSdkAccessEventNormalizer.TryNormalize(failed, out var failedRecord);
        DahuaSdkAccessEventNormalizer.TryNormalize(stranger, out var strangerRecord);

        Assert.False(DahuaSdkAccessEventNormalizer.ShouldInsertPayrollAttendance(failedRecord));
        Assert.False(DahuaSdkAccessEventNormalizer.ShouldInsertPayrollAttendance(strangerRecord));
    }

    [Fact]
    public void TryParseAsciiKeyValuePayload_ParsesKnownDahuaFields()
    {
        var payload = "DeviceID=BT-API-TEST-001\r\nUserID=1\r\nCardName=Ilham\r\nStatus=1\r\nMethod=15\r\nType=Entry\r\nRecNo=987"u8.ToArray();

        var ok = DahuaSdkAccessEventNormalizer.TryParseAsciiKeyValuePayload(payload, out var sdkEvent);

        Assert.True(ok);
        Assert.Equal("BT-API-TEST-001", sdkEvent.RegisterDeviceId);
        Assert.Equal("1", sdkEvent.UserId);
        Assert.Equal("Ilham", sdkEvent.CardName);
        Assert.Equal("1", sdkEvent.Status);
        Assert.Equal("15", sdkEvent.Method);
        Assert.Equal("Entry", sdkEvent.Direction);
        Assert.Equal(987, sdkEvent.RecNo);
    }
}


public sealed class DahuaNetSdkActiveRegisterServiceTests
{
    [Fact]
    public async Task StartAsync_WithMissingSdk_KeepsWorkerAliveAndReportsWarning()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var configuration = new ConfigurationBuilder().Build();
        var service = new DahuaNetSdkActiveRegisterService(
            new MissingSdkProbe(),
            new HeaderProbe(hasHeaders: false),
            services.GetRequiredService<IServiceScopeFactory>(),
            configuration,
            NullLogger<DahuaNetSdkActiveRegisterService>.Instance);

        await service.StartAsync([7000], CancellationToken.None);

        Assert.False(service.IsSdkListenerActive);
        Assert.False(service.IsRealSdkAvailable);
        Assert.Equal("MissingSdk", service.DecodeStatus);
        Assert.Contains("real NetSDK event decode is disabled", service.StartupWarning);
    }

    [Fact]
    public void MissingHeadersProbe_ReportsClearWarning()
    {
        var probe = new HeaderProbe(hasHeaders: false);

        Assert.False(probe.HasHeadersOrSamples);
        Assert.Contains("Native binaries are present, but Dahua SDK headers/samples are missing", probe.MissingHeadersWarning);
    }

    private sealed class MissingSdkProbe : IDahuaNativeLibraryProbe
    {
        public bool HasNativeSdk => false;
        public string RuntimeFolder => "linux-x64";
        public string ExpectedPath => "/app/vendor/dahua-netsdk/linux-x64";
        public string? NativeLibraryPath => null;
        public string? LastLoadError => null;

        public bool TryLoadNativeSdk(out IntPtr libraryHandle, out string? error)
        {
            libraryHandle = IntPtr.Zero;
            error = "missing";
            return false;
        }
    }

    private sealed class HeaderProbe(bool hasHeaders) : IDahuaSdkHeaderProbe
    {
        public bool HasHeadersOrSamples => hasHeaders;
        public string SearchRoot => "/app/vendor/dahua-netsdk";
        public IReadOnlyList<string> MatchedFiles => HasHeadersOrSamples ? ["NetSDK.h"] : [];
        public string MissingHeadersWarning => "Native binaries are present, but Dahua SDK headers/samples are missing. Exact access-event struct binding cannot be completed safely.";
    }
}

public sealed class DahuaNetSdkSubscriptionDiagnosticsTests
{
    [Fact]
    public void MarkStartListenExFailure_SetsSubscriptionFailedDiagnostics()
    {
        var diagnostics = new DahuaNetSdkDiagnostics
        {
            NetSdkDecodeStatus = "Subscribing",
            ActiveRegisterSessionHandleSource = "ServiceCallbackLHandle",
            ActiveRegisterSessionHandleStrategyResult = DahuaNetSdkSubscriptionDiagnostics.StrategyResultAttempting,
            StartListenExCalled = true,
        };

        DahuaNetSdkSubscriptionDiagnostics.MarkStartListenExFailure(diagnostics, -2147483644, "0x80000004");

        Assert.True(diagnostics.StartListenExCalled);
        Assert.False(diagnostics.StartListenExSuccess);
        Assert.Equal(-2147483644, diagnostics.StartListenExErrorSigned);
        Assert.Equal("0x80000004", diagnostics.StartListenExErrorHex);
        Assert.Equal("SubscriptionFailed", diagnostics.NetSdkDecodeStatus);
        Assert.Equal("Failed", diagnostics.ActiveRegisterSessionHandleStrategyResult);
        Assert.Contains("CLIENT_StartListenEx failed", diagnostics.LastDecodeError);
    }
}

public sealed class DahuaSmartEventSubscriptionHealthTests
{
    [Fact]
    public void EndpointChange_WithActiveSessionAndNewRemotePort_TriggersResubscribe()
    {
        var changed = DahuaSmartEventSubscriptionEndpoint.HasChanged(
            previousIp: "185.146.112.123",
            previousPort: 60062,
            currentIp: "185.146.112.123",
            currentPort: 60099,
            hasActiveSession: true);

        Assert.True(changed);
    }

    [Fact]
    public void EndpointChange_WithoutActiveSession_DoesNotTriggerResubscribe()
    {
        var changed = DahuaSmartEventSubscriptionEndpoint.HasChanged(
            previousIp: "185.146.112.123",
            previousPort: 60062,
            currentIp: "185.146.112.123",
            currentPort: 60099,
            hasActiveSession: false);

        Assert.False(changed);
    }

    [Fact]
    public void Watchdog_RecentServiceCallbackAndStaleSmartEvent_RequestsResubscribe()
    {
        var now = DateTimeOffset.Parse("2026-07-30T10:00:00Z");
        var snapshot = NewSnapshot(
            lastServiceCallbackAt: now.AddMinutes(-1),
            lastSmartEventAt: now.AddMinutes(-20),
            subscribedAt: now.AddHours(-1));
        var options = new DahuaSmartEventWatchdogOptions(
            Enabled: true,
            StaleThreshold: TimeSpan.FromMinutes(10),
            PeriodicResubscribeInterval: TimeSpan.FromHours(6),
            ResubscribeCooldown: TimeSpan.FromSeconds(60));

        var decision = DahuaSmartEventWatchdogPolicy.Evaluate(snapshot, options, now);

        Assert.True(decision.ShouldResubscribe);
        Assert.True(decision.StaleSmartEventDetected);
        Assert.Equal("StaleSmartEventSubscription", decision.Reason);
    }

    [Fact]
    public void Watchdog_CooldownSuppressesDuplicateStaleResubscribe()
    {
        var now = DateTimeOffset.Parse("2026-07-30T10:00:00Z");
        var snapshot = NewSnapshot(
            lastServiceCallbackAt: now.AddMinutes(-1),
            lastSmartEventAt: now.AddMinutes(-20),
            subscribedAt: now.AddHours(-1),
            lastResubscribeAt: now.AddSeconds(-30));
        var options = new DahuaSmartEventWatchdogOptions(
            Enabled: true,
            StaleThreshold: TimeSpan.FromMinutes(10),
            PeriodicResubscribeInterval: TimeSpan.FromHours(6),
            ResubscribeCooldown: TimeSpan.FromSeconds(60));

        var decision = DahuaSmartEventWatchdogPolicy.Evaluate(snapshot, options, now);

        Assert.False(decision.ShouldResubscribe);
        Assert.True(decision.StaleSmartEventDetected);
        Assert.True(decision.CooldownActive);
    }

    [Fact]
    public void Watchdog_PeriodicSubscriptionAge_RequestsResubscribe()
    {
        var now = DateTimeOffset.Parse("2026-07-30T10:00:00Z");
        var snapshot = NewSnapshot(
            lastServiceCallbackAt: now.AddMinutes(-30),
            lastSmartEventAt: now.AddMinutes(-30),
            subscribedAt: now.AddHours(-7));
        var options = new DahuaSmartEventWatchdogOptions(
            Enabled: true,
            StaleThreshold: TimeSpan.FromMinutes(10),
            PeriodicResubscribeInterval: TimeSpan.FromHours(6),
            ResubscribeCooldown: TimeSpan.FromSeconds(60));

        var decision = DahuaSmartEventWatchdogPolicy.Evaluate(snapshot, options, now);

        Assert.True(decision.ShouldResubscribe);
        Assert.False(decision.StaleSmartEventDetected);
        Assert.Equal("PeriodicSmartEventResubscribe", decision.Reason);
    }

    private static DahuaSmartEventSubscriptionSnapshot NewSnapshot(
        DateTimeOffset? lastServiceCallbackAt,
        DateTimeOffset? lastSmartEventAt,
        DateTimeOffset? subscribedAt,
        DateTimeOffset? lastResubscribeAt = null) =>
        new(
            DeviceId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            RegisterDeviceId: "BT-API-TEST-001",
            LoginHandle: 123,
            SmartEventAttachHandle: 456,
            RemoteIp: "185.146.112.123",
            RemotePort: 60062,
            SubscribedAt: subscribedAt,
            LastSmartEventAt: lastSmartEventAt,
            LastServiceCallbackAt: lastServiceCallbackAt,
            SubscriptionGeneration: 1,
            LastResubscribeAt: lastResubscribeAt,
            LastResubscribeReason: null,
            LastResubscribeSuccess: null,
            LastResubscribeError: null,
            SmartEventEnabled: true,
            SmartEventSubscriptionSuccess: true);
}

public sealed class DahuaNetSdkRegistrationAttemptTests
{
    [Fact]
    public void BuildRegistrationKey_DeduplicatesSameRegisterEndpointAndHandle()
    {
        var first = DahuaNetSdkSubscriptionDiagnostics.BuildRegistrationKey("BT-API-TEST-001", "172.21.0.1", 52610, 140004122793824);
        var second = DahuaNetSdkSubscriptionDiagnostics.BuildRegistrationKey("BT-API-TEST-001", "172.21.0.1", 52610, 140004122793824);
        var differentHandle = DahuaNetSdkSubscriptionDiagnostics.BuildRegistrationKey("BT-API-TEST-001", "172.21.0.1", 52610, 140004122793825);

        Assert.Equal(first, second);
        Assert.NotEqual(first, differentHandle);
    }
}

public sealed class DahuaActiveRegisterServiceModeTests
{
    [Fact]
    public void Defaults_ToListenServer_AndExperimentalDisabled()
    {
        Assert.Equal("ListenServer", DahuaActiveRegisterServiceMode.Parse(null));
        Assert.False(DahuaActiveRegisterServiceMode.IsExperimentalEnabled(null));
    }

    [Fact]
    public void StartServiceExperimental_DetectsSamePortConflict()
    {
        var mode = DahuaActiveRegisterServiceMode.Parse("StartServiceExperimental");

        Assert.True(DahuaActiveRegisterServiceMode.HasSamePortConflict(mode, [9500, 7000], 7000));
        Assert.False(DahuaActiveRegisterServiceMode.HasSamePortConflict(mode, [9500, 7000], 7001));
    }
}

public sealed class DahuaExperimentalStartServiceDiagnosticsTests
{
    [Fact]
    public void Inspect_NonAccessCommand_IsDiagnosticOnlySkipped()
    {
        var diagnostic = DahuaExperimentalStartServiceDiagnostics.Inspect(0x400C, [1, 2, 3]);

        Assert.Equal(0x400C, diagnostic.Command);
        Assert.Equal(3, diagnostic.PayloadBytes);
        Assert.Null(diagnostic.AccessEvent);
        Assert.Contains("diagnostic-only", diagnostic.DecodeStatus);
    }

    [Fact]
    public void Inspect_AccessControlEvent_DecodesDiagnosticOnlyEvent()
    {
        var payload = StructToBytes(SuccessfulInfo());

        var diagnostic = DahuaExperimentalStartServiceDiagnostics.Inspect(DahuaNetSdkAccessEventDecoder.AccessControlEventCommand, payload);

        Assert.NotNull(diagnostic.AccessEvent);
        Assert.Equal("1", diagnostic.AccessEvent.UserId);
        Assert.Equal("Ilham", diagnostic.AccessEvent.CardName);
        Assert.Contains("diagnostic-only", diagnostic.DecodeStatus);
    }

    private static DahuaNetSdkAccessEventDecoder.AlarmAccessControlEventInfo SuccessfulInfo() => new()
    {
        DwSize = 1,
        BStatus = true,
        EmOpenMethod = 16,
        EmEventType = 1,
        NPunchingRecNo = 12345,
        StuTime = new DahuaNetSdkAccessEventDecoder.NetTime
        {
            DwYear = 2026,
            DwMonth = 7,
            DwDay = 7,
            DwHour = 8,
            DwMinute = 15,
            DwSecond = 0,
        },
        SzUserID = Bytes(64, "1"),
        SzCardName = Bytes(64, "Ilham"),
        SzCitizenName = Bytes(256, "Citizen Ilham"),
        SzSnapURL = Bytes(256, "/snapshots/12345.jpg"),
        SzDeviceID = Bytes(128, "BT-API-TEST-001"),
        SzUserUniqueID = Bytes(128, "person-1"),
        NScore = 95,
        NSimilarity = 96,
        NAliveFlag = 1,
    };

    private static byte[] StructToBytes<T>(T value) where T : struct
    {
        var size = System.Runtime.InteropServices.Marshal.SizeOf<T>();
        var bytes = new byte[size];
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(bytes, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            System.Runtime.InteropServices.Marshal.StructureToPtr(value, handle.AddrOfPinnedObject(), false);
            return bytes;
        }
        finally
        {
            handle.Free();
        }
    }

    private static byte[] Bytes(int size, string value)
    {
        var bytes = new byte[size];
        var source = System.Text.Encoding.UTF8.GetBytes(value);
        Array.Copy(source, bytes, Math.Min(source.Length, size));
        return bytes;
    }
}


