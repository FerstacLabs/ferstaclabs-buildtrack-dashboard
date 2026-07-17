using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using BuildTrack.Domain.Dahua;
using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Data;
using BuildTrack.Infrastructure.Security;
using BuildTrack.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BuildTrack.Infrastructure.Dahua;

public sealed class DahuaNetSdkDiagnostics
{
    public bool SdkLoaded { get; set; }
    public bool SdkInitialized { get; set; }
    public int[] ListenerPorts { get; set; } = [];
    public bool AlarmCallbackConfigured { get; set; }
    public string ActiveRegisterServiceMode { get; set; } = "ListenServer";
    public bool ExperimentalStartServiceEnabled { get; set; }
    public bool ExperimentalStartServiceStarted { get; set; }
    public long? ExperimentalStartServiceHandle { get; set; }
    public int? ExperimentalStartServiceLastCommand { get; set; }
    public int ExperimentalStartServiceLastPayloadBytes { get; set; }
    public string? ExperimentalStartServiceLastDecodeStatus { get; set; }
    public int? ExperimentalStartServiceErrorSigned { get; set; }
    public string? ExperimentalStartServiceErrorHex { get; set; }
    public int? LastServiceCommand { get; set; }
    public string? LastServiceEventType { get; set; }
    public int LastServicePayloadBytes { get; set; }
    public string? LastServicePayloadFirst256Hex { get; set; }
    public string? LastRegisterDeviceId { get; set; }
    public int? LastParsedRegisterDeviceIdOffset { get; set; }
    public string? LastParsedRegisterDeviceId { get; set; }
    public int? LastParsedSerialOffset { get; set; }
    public string? LastParsedSerial { get; set; }
    public string? LastParsedRemoteIp { get; set; }
    public int? LastParsedRemotePort { get; set; }
    public string? LastPossibleSessionHandlesJson { get; set; }
    public string? LastPayloadStructLayout { get; set; }
    public bool ResponseDevRegCalled { get; set; }
    public bool? ResponseDevRegSuccess { get; set; }
    public int? ResponseDevRegErrorSigned { get; set; }
    public string? ResponseDevRegErrorHex { get; set; }
    public string? ResponseDevRegDevSerial { get; set; }
    public int? ResponseDevRegDevSerialLength { get; set; }
    public string? ResponseDevRegIp { get; set; }
    public int? ResponseDevRegPort { get; set; }
    public bool? ResponseDevRegAccept { get; set; }
    public string? ResponseDevRegCommandSource { get; set; }
    public long? LastServiceCallbackHandle { get; set; }
    public bool LastServiceCallbackHandleNonZero { get; set; }
    public bool ExperimentalServiceHandleSubscribeEnabled { get; set; }
    public string? LastExperimentalSubscribeJson { get; set; }
    public bool ActiveRegisterSessionHandleFound { get; set; }
    public bool ActiveRegisterSessionHandleValueNonZero { get; set; }
    public long? ActiveRegisterSessionHandleValue { get; set; }
    public string? ActiveRegisterSessionHandleSource { get; set; }
    public string? ActiveRegisterSessionHandleStrategyResult { get; set; }
    public string? LoginStrategy { get; set; }
    public long? LoginHandle { get; set; }
    public bool? LoginSucceeded { get; set; }
    public int? LoginErrorSigned { get; set; }
    public string? LoginErrorHex { get; set; }
    public int? LoginNativeErrorSigned { get; set; }
    public string? LoginNativeErrorHex { get; set; }
    public bool LoginPossibleMarshallingWarning { get; set; }
    public bool StartListenExCalled { get; set; }
    public bool? StartListenExSuccess { get; set; }
    public int? StartListenExErrorSigned { get; set; }
    public string? StartListenExErrorHex { get; set; }
    public int? LastAlarmCommand { get; set; }
    public string? LastAlarmCommandName { get; set; }
    public string? LastAlarmPayloadFirst256Hex { get; set; }
    public string? LastAlarmDecodeStatus { get; set; }
    public string? LastDecodedAlarmJson { get; set; }
    public bool NetSdkRecordQueryEnabled { get; set; }
    public bool NetSdkRecordQueryDiagnosticMode { get; set; }
    public DateTimeOffset? LastRecordQueryAt { get; set; }
    public bool? LastRecordQuerySuccess { get; set; }
    public string? LastRecordQueryError { get; set; }
    public int LastRecordQueryCount { get; set; }
    public long? LastRecordQueryLastRecNo { get; set; }
    public string? LastDecodeError { get; set; }
    public string NetSdkDecodeStatus { get; set; } = "MissingSdk";
}

public sealed class DahuaNetSdkActiveRegisterService(
    IDahuaNativeLibraryProbe nativeLibraryProbe,
    IDahuaSdkHeaderProbe headerProbe,
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<DahuaNetSdkActiveRegisterService> logger) : IDahuaActiveRegisterSdk, IDisposable
{
    private readonly List<IntPtr> _listenHandles = [];
    private readonly List<IntPtr> _startServiceHandles = [];
    private readonly ConcurrentDictionary<IntPtr, int> _listenPortByHandle = new();
    private readonly ConcurrentDictionary<IntPtr, Guid> _deviceIdByLoginHandle = new();
    private readonly ConcurrentDictionary<Guid, IntPtr> _loginHandleByDeviceId = new();
    private readonly ConcurrentDictionary<Guid, byte> _subscriptionInProgress = new();
    private readonly ConcurrentDictionary<string, byte> _acceptedRegistrationKeys = new();
    private readonly ConcurrentDictionary<string, byte> _experimentalSubscribeAttemptKeys = new();
    private readonly ConcurrentDictionary<Guid, byte> _recordQueryLoops = new();
    private readonly CancellationTokenSource _recordQueryCancellation = new();
    private readonly object _diagnosticsLock = new();
    private long _diagnosticsPersistVersion;
    private readonly DahuaNetSdkDiagnostics _diagnostics = new()
    {
        SdkLoaded = nativeLibraryProbe.HasNativeSdk,
        NetSdkDecodeStatus = nativeLibraryProbe.HasNativeSdk ? "Initialized" : "MissingSdk",
    };
    private DahuaNetSdkNativeClient? _nativeClient;
    private DahuaNetSdkNativeClient.ServiceCallback? _serviceCallback;
    private DahuaNetSdkNativeClient.ServiceCallback? _experimentalStartServiceCallback;
    private DahuaNetSdkNativeClient.AlarmMessageCallback? _alarmCallback;
    private bool _disposed;
    private bool _singleDeviceFallbackEnabled;
    private string _decodeStatus = nativeLibraryProbe.HasNativeSdk ? "Initialized" : "MissingSdk";
    private string? _startupError;

    public bool IsRealSdkAvailable => nativeLibraryProbe.HasNativeSdk && _startupError is null;

    public bool IsSdkListenerActive { get; private set; }

    public string DecodeStatus => nativeLibraryProbe.HasNativeSdk ? _decodeStatus : "MissingSdk";

    public DahuaNetSdkDiagnostics Diagnostics
    {
        get
        {
            lock (_diagnosticsLock)
            {
                return new DahuaNetSdkDiagnostics
                {
                    SdkLoaded = _diagnostics.SdkLoaded,
                    SdkInitialized = _diagnostics.SdkInitialized,
                    ListenerPorts = _diagnostics.ListenerPorts.ToArray(),
                    AlarmCallbackConfigured = _diagnostics.AlarmCallbackConfigured,
                    ActiveRegisterServiceMode = _diagnostics.ActiveRegisterServiceMode,
                    ExperimentalStartServiceEnabled = _diagnostics.ExperimentalStartServiceEnabled,
                    ExperimentalStartServiceStarted = _diagnostics.ExperimentalStartServiceStarted,
                    ExperimentalStartServiceHandle = _diagnostics.ExperimentalStartServiceHandle,
                    ExperimentalStartServiceLastCommand = _diagnostics.ExperimentalStartServiceLastCommand,
                    ExperimentalStartServiceLastPayloadBytes = _diagnostics.ExperimentalStartServiceLastPayloadBytes,
                    ExperimentalStartServiceLastDecodeStatus = _diagnostics.ExperimentalStartServiceLastDecodeStatus,
                    ExperimentalStartServiceErrorSigned = _diagnostics.ExperimentalStartServiceErrorSigned,
                    ExperimentalStartServiceErrorHex = _diagnostics.ExperimentalStartServiceErrorHex,
                    LastServiceCommand = _diagnostics.LastServiceCommand,
                    LastServiceEventType = _diagnostics.LastServiceEventType,
                    LastServicePayloadBytes = _diagnostics.LastServicePayloadBytes,
                    LastServicePayloadFirst256Hex = _diagnostics.LastServicePayloadFirst256Hex,
                    LastRegisterDeviceId = _diagnostics.LastRegisterDeviceId,
                    LastParsedRegisterDeviceIdOffset = _diagnostics.LastParsedRegisterDeviceIdOffset,
                    LastParsedRegisterDeviceId = _diagnostics.LastParsedRegisterDeviceId,
                    LastParsedSerialOffset = _diagnostics.LastParsedSerialOffset,
                    LastParsedSerial = _diagnostics.LastParsedSerial,
                    LastParsedRemoteIp = _diagnostics.LastParsedRemoteIp,
                    LastParsedRemotePort = _diagnostics.LastParsedRemotePort,
                    LastPossibleSessionHandlesJson = _diagnostics.LastPossibleSessionHandlesJson,
                    LastPayloadStructLayout = _diagnostics.LastPayloadStructLayout,
                    ResponseDevRegCalled = _diagnostics.ResponseDevRegCalled,
                    ResponseDevRegSuccess = _diagnostics.ResponseDevRegSuccess,
                    ResponseDevRegErrorSigned = _diagnostics.ResponseDevRegErrorSigned,
                    ResponseDevRegErrorHex = _diagnostics.ResponseDevRegErrorHex,
                    ResponseDevRegDevSerial = _diagnostics.ResponseDevRegDevSerial,
                    ResponseDevRegDevSerialLength = _diagnostics.ResponseDevRegDevSerialLength,
                    ResponseDevRegIp = _diagnostics.ResponseDevRegIp,
                    ResponseDevRegPort = _diagnostics.ResponseDevRegPort,
                    ResponseDevRegAccept = _diagnostics.ResponseDevRegAccept,
                    ResponseDevRegCommandSource = _diagnostics.ResponseDevRegCommandSource,
                    LastServiceCallbackHandle = _diagnostics.LastServiceCallbackHandle,
                    LastServiceCallbackHandleNonZero = _diagnostics.LastServiceCallbackHandleNonZero,
                    ExperimentalServiceHandleSubscribeEnabled = _diagnostics.ExperimentalServiceHandleSubscribeEnabled,
                    LastExperimentalSubscribeJson = _diagnostics.LastExperimentalSubscribeJson,
                    ActiveRegisterSessionHandleFound = _diagnostics.ActiveRegisterSessionHandleFound,
                    ActiveRegisterSessionHandleValueNonZero = _diagnostics.ActiveRegisterSessionHandleValueNonZero,
                    ActiveRegisterSessionHandleValue = _diagnostics.ActiveRegisterSessionHandleValue,
                    ActiveRegisterSessionHandleSource = _diagnostics.ActiveRegisterSessionHandleSource,
                    ActiveRegisterSessionHandleStrategyResult = _diagnostics.ActiveRegisterSessionHandleStrategyResult,
                    LoginStrategy = _diagnostics.LoginStrategy,
                    LoginHandle = _diagnostics.LoginHandle,
                    LoginSucceeded = _diagnostics.LoginSucceeded,
                    LoginErrorSigned = _diagnostics.LoginErrorSigned,
                    LoginErrorHex = _diagnostics.LoginErrorHex,
                    LoginNativeErrorSigned = _diagnostics.LoginNativeErrorSigned,
                    LoginNativeErrorHex = _diagnostics.LoginNativeErrorHex,
                    LoginPossibleMarshallingWarning = _diagnostics.LoginPossibleMarshallingWarning,
                    StartListenExCalled = _diagnostics.StartListenExCalled,
                    StartListenExSuccess = _diagnostics.StartListenExSuccess,
                    StartListenExErrorSigned = _diagnostics.StartListenExErrorSigned,
                    StartListenExErrorHex = _diagnostics.StartListenExErrorHex,
                    LastAlarmCommand = _diagnostics.LastAlarmCommand,
                    LastAlarmCommandName = _diagnostics.LastAlarmCommandName,
                    LastAlarmPayloadFirst256Hex = _diagnostics.LastAlarmPayloadFirst256Hex,
                    LastAlarmDecodeStatus = _diagnostics.LastAlarmDecodeStatus,
                    LastDecodedAlarmJson = _diagnostics.LastDecodedAlarmJson,
                    NetSdkRecordQueryEnabled = _diagnostics.NetSdkRecordQueryEnabled,
                    NetSdkRecordQueryDiagnosticMode = _diagnostics.NetSdkRecordQueryDiagnosticMode,
                    LastRecordQueryAt = _diagnostics.LastRecordQueryAt,
                    LastRecordQuerySuccess = _diagnostics.LastRecordQuerySuccess,
                    LastRecordQueryError = _diagnostics.LastRecordQueryError,
                    LastRecordQueryCount = _diagnostics.LastRecordQueryCount,
                    LastRecordQueryLastRecNo = _diagnostics.LastRecordQueryLastRecNo,
                    LastDecodeError = _diagnostics.LastDecodeError,
                    NetSdkDecodeStatus = _diagnostics.NetSdkDecodeStatus,
                };
            }
        }
    }

    public string StartupWarning
    {
        get
        {
            if (!nativeLibraryProbe.HasNativeSdk)
            {
                return "Active Register TCP listener works, but real NetSDK event decode is disabled because native SDK is missing.";
            }

            if (!headerProbe.HasHeadersOrSamples) return headerProbe.MissingHeadersWarning;
            return string.IsNullOrWhiteSpace(_startupError) ? string.Empty : _startupError;
        }
    }

    public async Task<object> RunRecordQueryDiagnosticAsync(Guid deviceId, int maxRecords, CancellationToken cancellationToken)
    {
        if (_nativeClient is null)
        {
            return new { success = false, deviceId, error = "Dahua NetSDK native client is unavailable" };
        }

        if (!_loginHandleByDeviceId.TryGetValue(deviceId, out var loginHandle) || loginHandle == IntPtr.Zero)
        {
            return new
            {
                success = false,
                deviceId,
                error = "No active Dahua Active Register login handle exists for this device. Wait for the device to connect and login successfully.",
            };
        }

        var deviceTimeZone = ResolveTimeZone(configuration["DAHUA_ATTENDANCE_TIMEZONE"] ?? "Asia/Baku");
        return await QueryAccessControlRecordsOnceAsync(
            deviceId,
            loginHandle,
            Math.Clamp(maxRecords, 1, 200),
            deviceTimeZone,
            diagnosticMode: true,
            ingestRecords: false,
            cancellationToken);
    }

    public Task StartAsync(IEnumerable<int> ports, CancellationToken cancellationToken)
    {
        _singleDeviceFallbackEnabled = DahuaActiveRegisterFallbackMatcher.IsSingleDeviceFallbackEnabled(configuration["DAHUA_ACTIVE_REGISTER_ALLOW_SINGLE_DEVICE_FALLBACK"]);
        PersistDiagnostics();
        logger.LogInformation("Single-device fallback enabled: {Value}", _singleDeviceFallbackEnabled);

        if (!nativeLibraryProbe.HasNativeSdk)
        {
            SetStatus("MissingSdk");
            logger.LogWarning("{Warning}. Runtime folder: {RuntimeFolder}. Expected path: {ExpectedPath}", StartupWarning, nativeLibraryProbe.RuntimeFolder, nativeLibraryProbe.ExpectedPath);
            return Task.CompletedTask;
        }

        if (!nativeLibraryProbe.TryLoadNativeSdk(out var libraryHandle, out var loadError))
        {
            _startupError = loadError;
            SetStatus("Error");
            logger.LogError("{Error}. Set LD_LIBRARY_PATH=/app/vendor/dahua-netsdk/linux-x64:$LD_LIBRARY_PATH and verify all Dahua lib*.so dependencies exist.", loadError);
            return Task.CompletedTask;
        }

        logger.LogInformation("Dahua NetSDK native binaries loaded");

        try
        {
            _nativeClient = new DahuaNetSdkNativeClient(libraryHandle, logger);
            logger.LogInformation("Dahua NetSDK alarm subscription exports available: {Available}", _nativeClient.HasAlarmSubscriptionExports);

            if (!_nativeClient.Initialize())
            {
                _startupError = "Dahua NetSDK initialization returned false.";
                SetStatus("Error");
                logger.LogError("{Error}", _startupError);
                return Task.CompletedTask;
            }

            lock (_diagnosticsLock)
            {
                _diagnostics.SdkInitialized = true;
            }
            SetStatus("Initialized");
            logger.LogInformation("Dahua NetSDK initialized");
            if (!headerProbe.HasHeadersOrSamples)
            {
                logger.LogWarning("{Warning} SearchRoot={SearchRoot}", headerProbe.MissingHeadersWarning, headerProbe.SearchRoot);
            }

            _alarmCallback = OnAlarmMessageCallback;
            if (_nativeClient.TryConfigureAlarmCallback(_alarmCallback))
            {
                lock (_diagnosticsLock) _diagnostics.AlarmCallbackConfigured = true;
                PersistDiagnostics();
                logger.LogInformation("Dahua NetSDK alarm callback configured");
            }
            else
            {
                logger.LogWarning("CLIENT_SetDVRMessCallBack export is unavailable. Active Register listener can accept devices, but access events cannot be decoded.");
            }

            var distinctPorts = ports.Distinct().ToArray();
            var serviceMode = DahuaActiveRegisterServiceMode.Parse(configuration["DAHUA_ACTIVE_REGISTER_SERVICE_MODE"]);
            var experimentalEnabled = DahuaActiveRegisterServiceMode.IsExperimentalEnabled(configuration["DAHUA_EXPERIMENTAL_START_SERVICE_ENABLED"]);
            var experimentalPort = DahuaActiveRegisterServiceMode.ParseExperimentalPort(configuration["DAHUA_EXPERIMENTAL_START_SERVICE_PORT"]);
            lock (_diagnosticsLock)
            {
                _diagnostics.ActiveRegisterServiceMode = serviceMode;
                _diagnostics.ExperimentalStartServiceEnabled = experimentalEnabled;
                _diagnostics.ListenerPorts = distinctPorts;
            }
            PersistDiagnostics();

            if (serviceMode == DahuaActiveRegisterServiceMode.StartServiceExperimental)
            {
                if (!experimentalEnabled)
                {
                    logger.LogWarning("DAHUA_ACTIVE_REGISTER_SERVICE_MODE=StartServiceExperimental but DAHUA_EXPERIMENTAL_START_SERVICE_ENABLED is not true. Experimental StartService listener will not start.");
                    SetStatus("ExperimentalStartServiceDisabled");
                    return Task.CompletedTask;
                }

                if (DahuaActiveRegisterServiceMode.HasSamePortConflict(serviceMode, distinctPorts, experimentalPort))
                {
                    _startupError = $"StartServiceExperimental port {experimentalPort} conflicts with configured ListenServer ports. Use a separate port or disable ListenServer for that mode.";
                    lock (_diagnosticsLock)
                    {
                        _diagnostics.ExperimentalStartServiceErrorSigned = null;
                        _diagnostics.ExperimentalStartServiceErrorHex = "PORT_CONFLICT";
                        _diagnostics.ExperimentalStartServiceLastDecodeStatus = _startupError;
                    }
                    SetStatus("ExperimentalStartServicePortConflict");
                    logger.LogError("{Error}", _startupError);
                    return Task.CompletedTask;
                }

                _experimentalStartServiceCallback = OnExperimentalStartServiceCallback;
                var experimentalHandle = _nativeClient.StartService(experimentalPort, _experimentalStartServiceCallback);
                var startServiceError = _nativeClient.LastErrorCode;
                lock (_diagnosticsLock)
                {
                    _diagnostics.ExperimentalStartServiceStarted = experimentalHandle != IntPtr.Zero;
                    _diagnostics.ExperimentalStartServiceHandle = experimentalHandle != IntPtr.Zero ? experimentalHandle.ToInt64() : null;
                    _diagnostics.ExperimentalStartServiceErrorSigned = experimentalHandle == IntPtr.Zero ? startServiceError : null;
                    _diagnostics.ExperimentalStartServiceErrorHex = experimentalHandle == IntPtr.Zero ? ToHex(startServiceError) : null;
                }

                if (experimentalHandle == IntPtr.Zero)
                {
                    SetStatus("ExperimentalStartServiceFailed");
                    logger.LogError("Dahua experimental StartService listener failed to start on port {Port}. NetSDK last error {ErrorSigned}/{ErrorHex}", experimentalPort, startServiceError, ToHex(startServiceError));
                    return Task.CompletedTask;
                }

                _startServiceHandles.Add(experimentalHandle);
                IsSdkListenerActive = true;
                PersistDiagnostics();
                SetStatus("ExperimentalStartServiceActive");
                logger.LogWarning("Dahua experimental StartService listener started on port {Port}. Diagnostic-only mode; attendance will not be inserted from this callback.", experimentalPort);
                return Task.CompletedTask;
            }

            _serviceCallback = OnSdkServiceCallback;
            foreach (var port in distinctPorts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var handle = _nativeClient.ListenServer(port, _serviceCallback);
                if (handle == IntPtr.Zero)
                {
                    logger.LogError("Dahua Active Register SDK listener failed to start on port {Port}. NetSDK last error code {LastErrorCode}", port, _nativeClient.LastErrorCode);
                    continue;
                }

                _listenHandles.Add(handle);
                _listenPortByHandle[handle] = port;
                logger.LogInformation("Dahua Active Register SDK listener started on port {Port}", port);
            }

            IsSdkListenerActive = _listenHandles.Count > 0;
            PersistDiagnostics();
            SetStatus(IsSdkListenerActive ? "ListenerActive" : "Error");
            if (!IsSdkListenerActive)
            {
                _startupError = "Dahua NetSDK initialized, but no Active Register SDK listener could be started.";
            }        }
        catch (Exception ex)
        {
            _startupError = ex.Message;
            SetStatus("Error");
            logger.LogError(ex, "Dahua NetSDK startup failed. Raw TCP fallback will remain available if ports are free.");
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _recordQueryCancellation.Cancel();
        _disposed = true;

        if (_nativeClient is not null)
        {
            foreach (var loginHandle in _loginHandleByDeviceId.Values.Distinct())
            {
                _nativeClient.TryStopListen(loginHandle);
                _nativeClient.TryLogout(loginHandle);
            }

            foreach (var handle in _listenHandles)
            {
                _nativeClient.StopListenServer(handle);
            }

            foreach (var handle in _startServiceHandles)
            {
                _nativeClient.StopService(handle);
            }

            _nativeClient.Dispose();
        }
    }

    private int OnExperimentalStartServiceCallback(IntPtr serviceHandle, string deviceIp, ushort devicePort, int command, IntPtr param, uint paramLength, IntPtr userData)
    {
        var payload = CopyPayload(param, paramLength);
        var rawPayload = DahuaRawPayloadFormatter.CreateLogPayload(payload.Take(128).ToArray(), 0, deviceIp, devicePort);
        var diagnostic = DahuaExperimentalStartServiceDiagnostics.Inspect(command, payload);
        var eventType = GetCommandName(command);
        logger.LogInformation("Experimental StartService callback received. Command={Command}, EventType={EventType}, PayloadBytes={PayloadBytes}, Remote={RemoteIp}:{RemotePort}, Raw={RawPayload}", command, eventType, payload.Length, deviceIp, devicePort, JsonSerializer.Serialize(rawPayload));
        if (command == DahuaNetSdkAccessEventDecoder.AccessControlEventCommand)
        {
            logger.LogWarning("Experimental StartService received DH_ALARM_ACCESS_CTL_EVENT. DecodeStatus={DecodeStatus}. Diagnostic-only; attendance was not inserted.", diagnostic.DecodeStatus);
        }

        lock (_diagnosticsLock)
        {
            _diagnostics.ExperimentalStartServiceLastCommand = command;
            _diagnostics.ExperimentalStartServiceLastPayloadBytes = payload.Length;
            _diagnostics.ExperimentalStartServiceLastDecodeStatus = diagnostic.DecodeStatus;
        }
        PersistDiagnostics();
        return 0;
    }
    private int OnSdkServiceCallback(IntPtr listenHandle, string deviceIp, ushort devicePort, int command, IntPtr param, uint paramLength, IntPtr userData)
    {
        var payload = CopyPayload(param, paramLength);
        var listenerPort = _listenPortByHandle.GetValueOrDefault(listenHandle, devicePort);
        var eventType = GetCommandName(command);
        var payloadDiagnostics = DahuaActiveRegisterPayloadParser.Inspect(command, payload, deviceIp, devicePort, listenHandle);
        logger.LogInformation("Active Register service callback received. Command={Command}, EventType={EventType}, PayloadBytes={PayloadBytes}, First256Hex={First256Hex}", command, eventType, payload.Length, payloadDiagnostics.PayloadFirst256Hex);
        logger.LogInformation("Service callback lHandle={Handle}, Command={Command}, EventType={EventType}", listenHandle.ToInt64(), command, eventType);
        logger.LogInformation("Active Register payload parse diagnostics. StructLayout={StructLayout}, RegisterIdOffset={RegisterIdOffset}, RegisterId={RegisterId}, SerialOffset={SerialOffset}, Serial={Serial}, Remote={RemoteIp}:{RemotePort}, PossibleHandles={PossibleHandles}",
            payloadDiagnostics.StructLayout,
            payloadDiagnostics.RegisterDeviceIdOffset,
            payloadDiagnostics.RegisterDeviceId,
            payloadDiagnostics.SerialOffset,
            payloadDiagnostics.Serial,
            payloadDiagnostics.RemoteIp,
            payloadDiagnostics.RemotePort,
            JsonSerializer.Serialize(payloadDiagnostics.PossibleSessionHandles));
        lock (_diagnosticsLock)
        {
            _diagnostics.LastServiceCommand = command;
            _diagnostics.LastServiceEventType = eventType;
            _diagnostics.LastServicePayloadBytes = payload.Length;
            _diagnostics.LastServicePayloadFirst256Hex = payloadDiagnostics.PayloadFirst256Hex;
            _diagnostics.LastRegisterDeviceId = payloadDiagnostics.RegisterDeviceId;
            _diagnostics.LastParsedRegisterDeviceIdOffset = payloadDiagnostics.RegisterDeviceIdOffset;
            _diagnostics.LastParsedRegisterDeviceId = payloadDiagnostics.RegisterDeviceId;
            _diagnostics.LastParsedSerialOffset = payloadDiagnostics.SerialOffset;
            _diagnostics.LastParsedSerial = payloadDiagnostics.Serial;
            _diagnostics.LastParsedRemoteIp = payloadDiagnostics.RemoteIp;
            _diagnostics.LastParsedRemotePort = payloadDiagnostics.RemotePort;
            _diagnostics.LastPossibleSessionHandlesJson = JsonSerializer.Serialize(payloadDiagnostics.PossibleSessionHandles);
            _diagnostics.LastPayloadStructLayout = payloadDiagnostics.StructLayout;
            _diagnostics.ExperimentalServiceHandleSubscribeEnabled = IsEnabled(configuration["DAHUA_ACTIVE_REGISTER_EXPERIMENTAL_SERVICE_HANDLE_SUBSCRIBE"]);
            _diagnostics.LastServiceCallbackHandle = listenHandle.ToInt64();
            _diagnostics.LastServiceCallbackHandleNonZero = listenHandle != IntPtr.Zero;
        }

        PersistDiagnostics();
        _ = Task.Run(() => HandleServiceCallbackAsync(deviceIp, devicePort, listenerPort, command, payload, listenHandle));
        return 0;
    }
    private bool OnAlarmMessageCallback(int command, IntPtr loginHandle, IntPtr payload, uint payloadLength, string deviceIp, int devicePort, IntPtr userData)
    {
        var payloadCopy = CopyPayload(payload, payloadLength);
        var commandName = DahuaNetSdkAlarmCommandDiagnostics.ResolveCommandName(command);
        var first256Hex = Convert.ToHexString(payloadCopy.Take(256).ToArray());
        logger.LogInformation("NetSDK alarm callback received. Command=0x{Command:X}, CommandName={CommandName}, PayloadBytes={PayloadBytes}, First256Hex={First256Hex}", command, commandName, payloadLength, first256Hex);
        lock (_diagnosticsLock)
        {
            _diagnostics.LastAlarmCommand = command;
            _diagnostics.LastAlarmCommandName = commandName;
            _diagnostics.LastAlarmPayloadFirst256Hex = first256Hex;
        }
        PersistDiagnostics();
        _ = Task.Run(() => HandleAlarmCallbackAsync(command, loginHandle, payloadCopy, deviceIp, devicePort));
        return true;
    }
    private async Task HandleServiceCallbackAsync(string? remoteIp, int remotePort, int listenerPort, int command, byte[] payload, IntPtr serviceCallbackHandle)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<BuildTrackDbContext>();
            var connectionLogger = scope.ServiceProvider.GetRequiredService<IDeviceConnectionLogger>();
            var pipeline = scope.ServiceProvider.GetRequiredService<IDahuaAccessRecordIngestionPipeline>();

            var registration = DahuaActiveRegisterPayloadParser.Parse(command, payload);
            var payloadDiagnostics = DahuaActiveRegisterPayloadParser.Inspect(command, payload, remoteIp, remotePort, serviceCallbackHandle);
            var registerDeviceId = registration.RegisterDeviceId;
            var eventType = GetCommandName(command);
            var decodeResult = TryDecodeActiveRegisterPayload(command, payload);
            lock (_diagnosticsLock) _diagnostics.LastRegisterDeviceId = registerDeviceId;
            PersistDiagnostics();
            if (command == 5)
            {
                logger.LogInformation("Parsed DH_DVR_SERIAL_RETURN_EX. RegisterId={RegisterId}, Serial={Serial}, HasSessionHandle={HasSessionHandle}, SessionHandleNonZero={SessionHandleNonZero}",
                    registration.RegisterDeviceId,
                    registration.Serial,
                    registration.HasSessionHandle,
                    registration.SessionHandle != IntPtr.Zero);
            }

            if (command == -1)
            {
                await PersistActiveRegisterRawEventAsync(db, null, registerDeviceId, remoteIp, remotePort, listenerPort, command, eventType, payload, "DeviceDisconnected", decodeResult.DecodedJson, payloadDiagnostics, CancellationToken.None);
                SetStatus("DeviceDisconnected");
                await connectionLogger.LogAsync(null, registerDeviceId, remoteIp, remotePort, "netsdk_device_disconnected", "Dahua Active Register device disconnected during verification", new { command, registerDeviceId }, CancellationToken.None);
                return;
            }

            var device = await MatchDeviceAsync(db, registerDeviceId, listenerPort, CancellationToken.None);
            var rawPayload = DahuaRawPayloadFormatter.CreateLogPayload(payload, listenerPort, remoteIp, remotePort);
            var raw = new
            {
                command,
                commandName = GetCommandName(command),
                registration = new
                {
                    registration.RegisterDeviceId,
                    registration.Serial,
                    registration.SupportsRedirection,
                    registration.HasSessionHandle,
                    sessionHandleNonZero = registration.SessionHandle != IntPtr.Zero,
                    registration.Kind,
                },
                rawPayload,
            };

            if (device is null)
            {
                await PersistActiveRegisterRawEventAsync(db, null, registerDeviceId, remoteIp, remotePort, listenerPort, command, eventType, payload, decodeResult.DecodeStatus, decodeResult.DecodedJson, payloadDiagnostics, CancellationToken.None);
                await connectionLogger.LogAsync(null, registerDeviceId, remoteIp, remotePort, "netsdk_unmatched", "Dahua NetSDK Active Register service callback did not match a known device", raw, CancellationToken.None);
                logger.LogWarning("Dahua NetSDK callback did not match a known device. RegisterDeviceId {RegisterDeviceId}, remote {RemoteIp}:{RemotePort}", registerDeviceId, remoteIp, remotePort);
                return;
            }

            var rawDecodeStatus = decodeResult.DecodeStatus;
            if (decodeResult.Record is not null)
            {
                if (IsEnabled(configuration["DAHUA_ACTIVE_REGISTER_INGESTION_ENABLED"]))
                {
                    await pipeline.IngestAsync(device.Id, decodeResult.Record, DahuaEventSource.ActiveRegister, CancellationToken.None);
                    rawDecodeStatus = "Ingested";
                }
                else
                {
                    rawDecodeStatus = "DecodedIngestionDisabled";
                    logger.LogWarning("Active Register callback decoded as DahuaAccessRecord, but ingestion is disabled. Set DAHUA_ACTIVE_REGISTER_INGESTION_ENABLED=true to ingest it.");
                }
            }

            await PersistActiveRegisterRawEventAsync(db, device.Id, device.RegisterDeviceId, remoteIp, remotePort, listenerPort, command, eventType, payload, rawDecodeStatus, decodeResult.DecodedJson, payloadDiagnostics, CancellationToken.None);

            device.Status = DeviceStatus.Online;
            device.LastKnownIp = remoteIp;
            device.LastSeenAt = DateTimeOffset.UtcNow;
            device.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
            SetStatus("DeviceConnected");

            await connectionLogger.LogAsync(device.Id, device.RegisterDeviceId, remoteIp, remotePort, "netsdk_connected", "Dahua device connected via NetSDK", raw, CancellationToken.None);
            logger.LogInformation("Dahua device connected via NetSDK. DeviceId={DeviceId}, RegisterDeviceId={RegisterDeviceId}", device.Id, device.RegisterDeviceId);

            if (_nativeClient is null) return;

            var registrationKey = BuildRegistrationKey(device.RegisterDeviceId, remoteIp, remotePort, serviceCallbackHandle);
            if (!_acceptedRegistrationKeys.TryAdd(registrationKey, 0))
            {
                logger.LogInformation("Skipping duplicate Dahua Active Register accept/subscription. Key={RegistrationKey}", registrationKey);
                await connectionLogger.LogAsync(
                    device.Id,
                    device.RegisterDeviceId,
                    remoteIp,
                    remotePort,
                    "netsdk_duplicate_register_callback",
                    "Duplicate Active Register callback ignored for the same registerId, remote endpoint and service callback handle",
                    new { registrationKey, serviceCallbackHandle = serviceCallbackHandle.ToInt64(), command, commandName = GetCommandName(command) },
                    CancellationToken.None);
                return;
            }

            var responseDevRegPort = remotePort;
            logger.LogInformation("CLIENT_ResponseDevReg called. DevSerial={DevSerial}, DevSerialLength={DevSerialLength}, Ip={Ip}, Port={Port}, Accept={Accept}, CommandSource={CommandSource}",
                device.RegisterDeviceId,
                device.RegisterDeviceId.Length,
                remoteIp,
                responseDevRegPort,
                true,
                registration.Kind);
            var responseSuccess = _nativeClient.TryResponseDeviceRegister(device.RegisterDeviceId, remoteIp, responseDevRegPort);
            var responseError = _nativeClient.LastErrorCode;
            lock (_diagnosticsLock)
            {
                _diagnostics.ResponseDevRegCalled = true;
                _diagnostics.ResponseDevRegSuccess = responseSuccess;
                _diagnostics.ResponseDevRegErrorSigned = responseSuccess ? null : responseError;
                _diagnostics.ResponseDevRegErrorHex = responseSuccess ? null : ToHex(responseError);
                _diagnostics.ResponseDevRegDevSerial = device.RegisterDeviceId;
                _diagnostics.ResponseDevRegDevSerialLength = device.RegisterDeviceId.Length;
                _diagnostics.ResponseDevRegIp = remoteIp;
                _diagnostics.ResponseDevRegPort = responseDevRegPort;
                _diagnostics.ResponseDevRegAccept = true;
                _diagnostics.ResponseDevRegCommandSource = registration.Kind;
            }
            PersistDiagnostics();
            logger.LogInformation("CLIENT_ResponseDevReg result={Result}, ErrorSigned={ErrorSigned}, ErrorHex={ErrorHex}", responseSuccess, responseError, ToHex(responseError));
            if (!responseSuccess)
            {
                SetStatus("RegisterAcceptFailed");
                await connectionLogger.LogAsync(
                    device.Id,
                    device.RegisterDeviceId,
                    remoteIp,
                    remotePort,
                    "netsdk_register_accept_failed",
                    $"CLIENT_ResponseDevReg failed. ErrorSigned={responseError}, ErrorHex={ToHex(responseError)}",
                    new
                    {
                        errorSigned = responseError,
                        errorHex = ToHex(responseError),
                        devSerial = device.RegisterDeviceId,
                        devSerialLength = device.RegisterDeviceId.Length,
                        ip = remoteIp,
                        port = responseDevRegPort,
                        accept = true,
                        commandSource = registration.Kind,
                    },
                    CancellationToken.None);
                _acceptedRegistrationKeys.TryRemove(registrationKey, out _);
                return;
            }

            SetStatus("RegisterAccepted");
            await TryExperimentalServiceHandleSubscriptionsAsync(device, payloadDiagnostics, serviceCallbackHandle, remoteIp, remotePort, connectionLogger, CancellationToken.None);
            var passwordProtector = scope.ServiceProvider.GetRequiredService<IPasswordProtector>();
            var plainPassword = passwordProtector.Unprotect(device.EncryptedPassword);
            var subscribed = await EnsureSubscribedAsync(device, registration, serviceCallbackHandle, remoteIp, remotePort, connectionLogger, device.Username, plainPassword, CancellationToken.None);
            if (DahuaActiveRegisterLoginDiagnostics.ShouldReleaseRegistrationKeyAfterSubscription(subscribed))
            {
                _acceptedRegistrationKeys.TryRemove(registrationKey, out _);
            }
        }
        catch (Exception ex)
        {
            SetStatus("Error");
            logger.LogError(ex, "Dahua NetSDK service callback handling failed. Listener continues running.");
        }
    }

    private async Task TryExperimentalServiceHandleSubscriptionsAsync(Device device, DahuaActiveRegisterPayloadDiagnostics payloadDiagnostics, IntPtr serviceCallbackHandle, string? remoteIp, int remotePort, IDeviceConnectionLogger connectionLogger, CancellationToken cancellationToken)
    {
        var enabled = IsEnabled(configuration["DAHUA_ACTIVE_REGISTER_EXPERIMENTAL_SERVICE_HANDLE_SUBSCRIBE"]);
        lock (_diagnosticsLock) _diagnostics.ExperimentalServiceHandleSubscribeEnabled = enabled;
        if (!enabled || _nativeClient is null) return;

        var candidates = new List<(string Strategy, long Handle)>();
        if (serviceCallbackHandle != IntPtr.Zero) candidates.Add(("ServiceCallbackLHandle", serviceCallbackHandle.ToInt64()));
        foreach (var handle in payloadDiagnostics.PossibleSessionHandles.Where(x => x != 0 && x != serviceCallbackHandle.ToInt64()).Distinct())
        {
            candidates.Add(("PayloadCandidateHandle", handle));
        }

        if (candidates.Count == 0)
        {
            var emptyJson = JsonSerializer.Serialize(new { enabled, attempts = Array.Empty<object>(), reason = "No non-zero candidate handle found" });
            lock (_diagnosticsLock) _diagnostics.LastExperimentalSubscribeJson = emptyJson;
            return;
        }

        var attempts = new List<object>();
        foreach (var candidate in candidates)
        {
            var key = $"{device.Id}:{remoteIp}:{remotePort}:{candidate.Strategy}:{candidate.Handle}";
            if (!_experimentalSubscribeAttemptKeys.TryAdd(key, 0))
            {
                attempts.Add(new { candidate.Strategy, candidate.Handle, skipped = true, reason = "AlreadyAttemptedForThisSession" });
                continue;
            }

            var handle = new IntPtr(candidate.Handle);
            logger.LogWarning("Experimental CLIENT_StartListenEx attempt after ResponseDevReg. Strategy={Strategy}, Handle={Handle}. This is diagnostics only; success is not treated as validated until alarm callbacks arrive.", candidate.Strategy, candidate.Handle);
            var result = _nativeClient.TryStartListenEx(handle);
            var error = _nativeClient.LastErrorCode;
            logger.LogWarning("Experimental CLIENT_StartListenEx result. Strategy={Strategy}, Handle={Handle}, Result={Result}, ErrorSigned={ErrorSigned}, ErrorHex={ErrorHex}", candidate.Strategy, candidate.Handle, result, error, ToHex(error));

            if (result)
            {
                _deviceIdByLoginHandle[handle] = device.Id;
            }

            var attempt = new
            {
                candidate.Strategy,
                candidate.Handle,
                result,
                errorSigned = error,
                errorHex = ToHex(error),
                mappedForAlarmCallback = result,
                validatedByAlarmCallback = false,
            };
            attempts.Add(attempt);
            await connectionLogger.LogAsync(
                device.Id,
                device.RegisterDeviceId,
                remoteIp,
                remotePort,
                "netsdk_experimental_startlistenex_attempt",
                "Experimental CLIENT_StartListenEx attempt after ResponseDevReg; diagnostics only",
                attempt,
                cancellationToken);
        }

        var json = JsonSerializer.Serialize(new
        {
            enabled,
            remoteIp,
            remotePort,
            registerDeviceId = device.RegisterDeviceId,
            attempts,
        });
        lock (_diagnosticsLock) _diagnostics.LastExperimentalSubscribeJson = json;
        PersistDiagnostics();
    }
    private async Task<bool> EnsureSubscribedAsync(Device device, DahuaActiveRegisterRegistration registration, IntPtr serviceCallbackHandle, string? remoteIp, int remotePort, IDeviceConnectionLogger connectionLogger, string username, string password, CancellationToken cancellationToken)
    {
        if (_nativeClient is null) return false;
        if (_loginHandleByDeviceId.TryGetValue(device.Id, out var existingHandle) && existingHandle != IntPtr.Zero)
        {
            logger.LogDebug("Dahua access event subscription already exists for device {DeviceId}", device.Id);
            return true;
        }

        if (!_subscriptionInProgress.TryAdd(device.Id, 0))
        {
            logger.LogDebug("Dahua access event subscription already in progress for device {DeviceId}", device.Id);
            return false;
        }

        try
        {
            IntPtr loginHandle;
            string handleSource;
            if (registration.SessionHandle != IntPtr.Zero)
            {
                loginHandle = registration.SessionHandle;
                handleSource = "RegistrationPayload";
            }
            else if (IsEnabled(configuration["DAHUA_ACTIVE_REGISTER_INGESTION_ENABLED"]))
            {
                var passwordOverride = configuration["DAHUA_ACTIVE_REGISTER_PASSWORD_OVERRIDE"];
                var loginPassword = string.IsNullOrEmpty(passwordOverride) ? password : passwordOverride;
                var passwordSource = string.IsNullOrEmpty(passwordOverride) ? "DatabaseEncrypted" : "EnvironmentOverride";
                var attempts = _nativeClient.TryLoginActiveRegisterStrategies(device.RegisterDeviceId, remoteIp, remotePort, username, loginPassword);
                DahuaActiveRegisterLoginAttempt? successfulAttempt = null;
                foreach (var attempt in attempts)
                {
                    lock (_diagnosticsLock)
                    {
                        _diagnostics.LoginStrategy = attempt.Strategy;
                        _diagnostics.LoginHandle = attempt.LoginHandle;
                        _diagnostics.LoginSucceeded = attempt.Succeeded;
                        _diagnostics.LoginErrorSigned = attempt.LastErrorAfterCall;
                        _diagnostics.LoginErrorHex = ToHex(attempt.LastErrorAfterCall);
                        _diagnostics.LoginNativeErrorSigned = attempt.NativeErrorPointer;
                        _diagnostics.LoginNativeErrorHex = ToHex(attempt.NativeErrorPointer);
                        _diagnostics.LoginPossibleMarshallingWarning = attempt.PossibleMarshallingWarning;
                        _diagnostics.ActiveRegisterSessionHandleSource = attempt.Strategy;
                        _diagnostics.ActiveRegisterSessionHandleStrategyResult = attempt.Succeeded
                            ? DahuaNetSdkSubscriptionDiagnostics.StrategyResultSucceeded
                            : DahuaNetSdkSubscriptionDiagnostics.StrategyResultFailed;
                    }
                    PersistDiagnostics();

                    logger.LogInformation(
                        "Dahua Active Register server-connection login attempt. Strategy={Strategy}, LoginApi={LoginApi}, RegisterDeviceId={RegisterDeviceId}, UsernamePresent={UsernamePresent}, PasswordSource={PasswordSource}, PasswordLength={PasswordLength}, IpArgument={IpArgument}, PortArgument={PortArgument}, SpecCap={SpecCap}, CapParamKind={CapParamKind}, CapParamLength={CapParamLength}, LoginHandle={LoginHandle}, NativeErrorPointer={NativeErrorPointer}, NativeErrorHex={NativeErrorHex}, LastError={LastError}, LastErrorHex={LastErrorHex}",
                        attempt.Strategy,
                        attempt.LoginApi,
                        attempt.RegisterDeviceId,
                        attempt.UsernamePresent,
                        passwordSource,
                        attempt.PasswordLength,
                        string.IsNullOrEmpty(attempt.IpArgument) ? "<empty>" : attempt.IpArgument,
                        attempt.PortArgument,
                        attempt.SpecCap,
                        attempt.CapParamKind,
                        attempt.CapParamStringLength,
                        attempt.LoginHandle,
                        attempt.NativeErrorPointer,
                        ToHex(attempt.NativeErrorPointer),
                        attempt.LastErrorAfterCall,
                        ToHex(attempt.LastErrorAfterCall));

                    if (attempt.PossibleMarshallingWarning)
                    {
                        logger.LogWarning("Dahua Active Register login returned zero handle with zero SDK errors. This can indicate wrong P/Invoke signature or marshaling. Strategy={Strategy}", attempt.Strategy);
                    }

                    if (attempt.Succeeded)
                    {
                        successfulAttempt = attempt;
                        break;
                    }
                }

                if (successfulAttempt is null)
                {
                    var lastAttempt = attempts.LastOrDefault();
                    var lastError = lastAttempt?.LastErrorAfterCall ?? 0;
                    var nativeError = lastAttempt?.NativeErrorPointer ?? 0;
                    lock (_diagnosticsLock)
                    {
                        _diagnostics.ActiveRegisterSessionHandleFound = false;
                        _diagnostics.ActiveRegisterSessionHandleValueNonZero = false;
                        _diagnostics.ActiveRegisterSessionHandleValue = serviceCallbackHandle != IntPtr.Zero ? serviceCallbackHandle.ToInt64() : null;
                        _diagnostics.ActiveRegisterSessionHandleSource = lastAttempt?.Strategy ?? "LoginExServerConn";
                        _diagnostics.ActiveRegisterSessionHandleStrategyResult = DahuaNetSdkSubscriptionDiagnostics.StrategyResultFailed;
                        _diagnostics.StartListenExCalled = false;
                        _diagnostics.StartListenExSuccess = false;
                        _diagnostics.StartListenExErrorSigned = lastError;
                        _diagnostics.StartListenExErrorHex = ToHex(lastError);
                        _diagnostics.LastDecodeError = $"CLIENT_LoginEx/CLIENT_LoginEx2/CLIENT_LoginWithHighLevelSecurity active-register server connection login failed. LastStrategy={lastAttempt?.Strategy ?? "None"}, ErrorSigned={lastError}, ErrorHex={ToHex(lastError)}, NativeErrorSigned={nativeError}, NativeErrorHex={ToHex(nativeError)}";
                    }
                    SetStatus("ServerConnLoginFailed");
                    await connectionLogger.LogAsync(
                        device.Id,
                        device.RegisterDeviceId,
                        remoteIp,
                        remotePort,
                        "netsdk_server_conn_login_failed",
                        "CLIENT_LoginEx/CLIENT_LoginEx2/CLIENT_LoginWithHighLevelSecurity active-register server connection login failed",
                        new
                        {
                            attempts = attempts.Select(x => new
                            {
                                x.Strategy,
                                x.LoginApi,
                                x.RegisterDeviceId,
                                x.UsernamePresent,
                                passwordSource,
                                x.PasswordLength,
                                x.IpArgument,
                                x.PortArgument,
                                x.SpecCap,
                                x.CapParamKind,
                                x.CapParamStringLength,
                                x.LoginHandle,
                                x.NativeErrorPointer,
                                nativeErrorHex = ToHex(x.NativeErrorPointer),
                                x.LastErrorAfterCall,
                                lastErrorHex = ToHex(x.LastErrorAfterCall),
                                x.PossibleMarshallingWarning,
                            })
                        },
                        cancellationToken);
                    logger.LogError("Dahua active-register server connection login failed after {AttemptCount} strategies. LastErrorSigned={ErrorSigned}, LastErrorHex={ErrorHex}, NativeErrorSigned={NativeErrorSigned}, NativeErrorHex={NativeErrorHex}", attempts.Count, lastError, ToHex(lastError), nativeError, ToHex(nativeError));
                    return false;
                }

                loginHandle = new IntPtr(successfulAttempt.LoginHandle);
                handleSource = successfulAttempt.Strategy;
                logger.LogInformation("Dahua Active Register server-connection login succeeded. Strategy={Strategy}, RegisterDeviceId={RegisterDeviceId}, LoginHandle={LoginHandle}", successfulAttempt.Strategy, device.RegisterDeviceId, loginHandle.ToInt64());
            }
            else
            {
                lock (_diagnosticsLock)
                {
                    _diagnostics.ActiveRegisterSessionHandleFound = false;
                    _diagnostics.ActiveRegisterSessionHandleValueNonZero = false;
                    _diagnostics.ActiveRegisterSessionHandleValue = serviceCallbackHandle != IntPtr.Zero ? serviceCallbackHandle.ToInt64() : null;
                    _diagnostics.ActiveRegisterSessionHandleSource = serviceCallbackHandle != IntPtr.Zero ? "ServiceCallbackLHandle" : "None";
                    _diagnostics.ActiveRegisterSessionHandleStrategyResult = serviceCallbackHandle != IntPtr.Zero
                        ? DahuaNetSdkSubscriptionDiagnostics.StrategyResultFailed
                        : DahuaNetSdkSubscriptionDiagnostics.StrategyResultNotAttempted;
                    _diagnostics.StartListenExCalled = false;
                    _diagnostics.StartListenExSuccess = null;
                    _diagnostics.StartListenExErrorSigned = serviceCallbackHandle != IntPtr.Zero ? unchecked((int)0x80000004) : null;
                    _diagnostics.StartListenExErrorHex = serviceCallbackHandle != IntPtr.Zero ? "0x80000004" : null;
                    _diagnostics.LastDecodeError = serviceCallbackHandle != IntPtr.Zero
                        ? "ServiceCallbackLHandle strategy was tested with CLIENT_StartListenEx and failed with 0x80000004; not retrying for this Active Register session. Enable DAHUA_ACTIVE_REGISTER_INGESTION_ENABLED=true to test LoginEx/CLIENT_LoginEx2 server-connection login."
                        : "Active Register session handle not found; service callback lHandle is zero.";
                }
                SetStatus("SessionHandleMissing");
                var message = serviceCallbackHandle != IntPtr.Zero
                    ? "ServiceCallbackLHandle is not a valid CLIENT_StartListenEx login handle based on real 0x80000004 test; subscription retry skipped."
                    : "Active Register session handle not found; cannot call CLIENT_StartListenEx because service callback lHandle is zero.";
                await connectionLogger.LogAsync(device.Id, device.RegisterDeviceId, remoteIp, remotePort, "netsdk_session_handle_missing", message, new { serviceCallbackHandle = serviceCallbackHandle.ToInt64(), strategyResult = _diagnostics.ActiveRegisterSessionHandleStrategyResult }, cancellationToken);
                logger.LogWarning("{Message}", message);
                return false;
            }

            lock (_diagnosticsLock)
            {
                _diagnostics.ActiveRegisterSessionHandleFound = true;
                _diagnostics.ActiveRegisterSessionHandleValueNonZero = loginHandle != IntPtr.Zero;
                _diagnostics.ActiveRegisterSessionHandleValue = loginHandle.ToInt64();
                _diagnostics.ActiveRegisterSessionHandleSource = handleSource;
                _diagnostics.ActiveRegisterSessionHandleStrategyResult = DahuaNetSdkSubscriptionDiagnostics.StrategyResultAttempting;
                _diagnostics.StartListenExCalled = true;
                _diagnostics.StartListenExSuccess = null;
                _diagnostics.StartListenExErrorSigned = null;
                _diagnostics.StartListenExErrorHex = null;
            }

            SetStatus("Subscribing");
            logger.LogInformation("Starting Dahua access event subscription. Strategy={Strategy}, Handle={Handle}", handleSource, loginHandle.ToInt64());
            logger.LogInformation("CLIENT_StartListenEx called");
            var startListenResult = _nativeClient.TryStartListenEx(loginHandle);
            var startListenError = _nativeClient.LastErrorCode;
            logger.LogInformation("CLIENT_StartListenEx result={Result}, ErrorSigned={ErrorSigned}, ErrorHex={ErrorHex}", startListenResult, startListenError, ToHex(startListenError));

            if (!startListenResult)
            {
                lock (_diagnosticsLock)
                {
                    DahuaNetSdkSubscriptionDiagnostics.MarkStartListenExFailure(_diagnostics, startListenError, ToHex(startListenError));
                }
                SetStatus(DahuaNetSdkSubscriptionDiagnostics.StatusSubscriptionFailed);
                var message = $"Dahua access event subscription failed. ErrorSigned={startListenError}, ErrorHex={ToHex(startListenError)}";
                await connectionLogger.LogAsync(device.Id, device.RegisterDeviceId, remoteIp, remotePort, "netsdk_subscription_failed", message, new { errorSigned = startListenError, errorHex = ToHex(startListenError), handle = loginHandle.ToInt64(), handleSource }, cancellationToken);
                logger.LogError("{Message}", message);
                return false;
            }

            _deviceIdByLoginHandle[loginHandle] = device.Id;
            _loginHandleByDeviceId[device.Id] = loginHandle;
            StartRecordQueryLoopIfEnabled(device.Id, loginHandle);
            lock (_diagnosticsLock)
            {
                _diagnostics.StartListenExSuccess = true;
                _diagnostics.ActiveRegisterSessionHandleStrategyResult = DahuaNetSdkSubscriptionDiagnostics.StrategyResultSucceeded;
                _diagnostics.StartListenExErrorSigned = null;
                _diagnostics.StartListenExErrorHex = null;
                _diagnostics.LastDecodeError = null;
            }
            SetStatus("Subscribed");
            await connectionLogger.LogAsync(device.Id, device.RegisterDeviceId, remoteIp, remotePort, "netsdk_subscribed", "Subscribed to Dahua access events", new { loginHandle = loginHandle.ToInt64(), handleSource }, cancellationToken);
            logger.LogInformation("Subscribed to Dahua access events");
            return true;
        }
        finally
        {
            _subscriptionInProgress.TryRemove(device.Id, out _);
        }
    }

    private async Task HandleAlarmCallbackAsync(int command, IntPtr loginHandle, byte[] payload, string? remoteIp, int remotePort)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BuildTrackDbContext>();
        var connectionLogger = scope.ServiceProvider.GetRequiredService<IDeviceConnectionLogger>();
        var pipeline = scope.ServiceProvider.GetRequiredService<IDahuaAccessRecordIngestionPipeline>();

        Device? device = null;
        if (_deviceIdByLoginHandle.TryGetValue(loginHandle, out var mappedDeviceId))
        {
            device = await db.Devices.FirstOrDefaultAsync(x => x.Id == mappedDeviceId, CancellationToken.None);
        }

        var alarmDiagnostic = DahuaNetSdkAlarmCommandDiagnostics.Inspect(command, payload);
        var alarmDiagnosticJson = JsonSerializer.Serialize(alarmDiagnostic);
        lock (_diagnosticsLock)
        {
            _diagnostics.LastAlarmCommand = command;
            _diagnostics.LastAlarmCommandName = alarmDiagnostic.CommandName;
            _diagnostics.LastAlarmPayloadFirst256Hex = alarmDiagnostic.PayloadFirst256Hex;
            _diagnostics.LastAlarmDecodeStatus = alarmDiagnostic.DecodeStatus;
            _diagnostics.LastDecodedAlarmJson = alarmDiagnosticJson;
            _diagnostics.LastDecodeError = alarmDiagnostic.FailureReason;
        }
        PersistDiagnostics();

        var rawPayload = DahuaRawPayloadFormatter.CreateLogPayload(payload, 0, remoteIp, remotePort);
        var raw = new
        {
            command,
            commandHex = $"0x{command:X}",
            commandName = alarmDiagnostic.CommandName,
            structName = alarmDiagnostic.StructName,
            decodeStatus = alarmDiagnostic.DecodeStatus,
            decodeFailureReason = alarmDiagnostic.FailureReason,
            decodedFields = alarmDiagnostic.Fields,
            loginHandle = loginHandle.ToInt64(),
            rawPayload,
        };

        await PersistActiveRegisterRawEventAsync(db, device?.Id, device?.RegisterDeviceId, remoteIp, remotePort, 0, command, alarmDiagnostic.CommandName, payload, alarmDiagnostic.DecodeStatus, alarmDiagnosticJson, null, CancellationToken.None);

        logger.LogInformation("NetSDK alarm diagnostic. Command=0x{Command:X}, CommandName={CommandName}, StructName={StructName}, PayloadBytes={PayloadBytes}, DecodeStatus={DecodeStatus}, Reason={Reason}", command, alarmDiagnostic.CommandName, alarmDiagnostic.StructName, payload.Length, alarmDiagnostic.DecodeStatus, alarmDiagnostic.FailureReason);

        if (command != DahuaNetSdkAccessEventDecoder.AccessControlEventCommand)
        {
            await connectionLogger.LogAsync(device?.Id, device?.RegisterDeviceId, remoteIp, remotePort, "netsdk_event_skipped", "NetSDK alarm command is not an access-control attendance event", raw, CancellationToken.None);
            return;
        }
        var handle = GCHandle.Alloc(payload, GCHandleType.Pinned);
        try
        {
            if (!DahuaNetSdkAccessEventDecoder.TryDecodeAccessControlEvent(handle.AddrOfPinnedObject(), (uint)payload.Length, out var sdkEvent, out var skipReason))
            {
                var eventType = string.IsNullOrWhiteSpace(sdkEvent.UserId) ? "netsdk_stranger_event" : "netsdk_failed_event";
                lock (_diagnosticsLock) _diagnostics.LastDecodeError = skipReason;
                logger.LogInformation("Dahua access event skipped. Reason={Reason}", skipReason);
                await connectionLogger.LogAsync(device?.Id, device?.RegisterDeviceId, remoteIp, remotePort, eventType, skipReason ?? "Dahua access event skipped", raw, CancellationToken.None);
                return;
            }

            if (device is null && !string.IsNullOrWhiteSpace(sdkEvent.RegisterDeviceId))
            {
                device = await db.Devices.FirstOrDefaultAsync(x => x.RegisterDeviceId == sdkEvent.RegisterDeviceId, CancellationToken.None);
            }

            if (device is null)
            {
                var message = "Decoded Dahua event could not be matched to a device session";
                lock (_diagnosticsLock) _diagnostics.LastDecodeError = message;
                await connectionLogger.LogAsync(null, sdkEvent.RegisterDeviceId, remoteIp, remotePort, "netsdk_event_skipped", message, raw, CancellationToken.None);
                logger.LogWarning("Decoded Dahua access event could not be matched to a BuildTrack device. RegisterDeviceId={RegisterDeviceId}", sdkEvent.RegisterDeviceId);
                return;
            }

            DahuaSdkAccessEventNormalizer.TryNormalize(sdkEvent, out var record);
            logger.LogInformation("Decoded Dahua access event. UserID={UserID}, Name={Name}, Status={Status}, Method={Method}, Direction={Direction}, RecNo={RecNo}", record.UserId, record.CardName, record.NormalizedStatus, record.NormalizedMethod, record.NormalizedDirection, record.RecNo);
            SetStatus("DecodeActive");

            if (!DahuaSdkAccessEventNormalizer.ShouldInsertPayrollAttendance(record))
            {
                var eventType = string.IsNullOrWhiteSpace(record.UserId) ? "netsdk_stranger_event" : "netsdk_failed_event";
                await connectionLogger.LogAsync(device.Id, device.RegisterDeviceId, remoteIp, remotePort, eventType, "Decoded Dahua access event is not eligible for payroll attendance", raw, CancellationToken.None);
                return;
            }

            logger.LogInformation("Real Dahua face/access event received");
            var ingestionEnabled = IsEnabled(configuration["DAHUA_ACTIVE_REGISTER_INGESTION_ENABLED"]);
            if (!ingestionEnabled)
            {
                await connectionLogger.LogAsync(device.Id, device.RegisterDeviceId, remoteIp, remotePort, "netsdk_ingestion_disabled", "Decoded Dahua access event skipped because Active Register ingestion is disabled", raw, CancellationToken.None);
                logger.LogWarning("Decoded Dahua access event skipped because DAHUA_ACTIVE_REGISTER_INGESTION_ENABLED is false");
                return;
            }

            await pipeline.IngestAsync(device.Id, record, DahuaEventSource.ActiveRegister, CancellationToken.None);
            logger.LogInformation("Attendance event submitted to shared pipeline from Dahua NetSDK");
        }
        catch (Exception ex)
        {
            lock (_diagnosticsLock) _diagnostics.LastDecodeError = ex.Message;
            logger.LogError(ex, "Dahua NetSDK alarm callback handling failed. Listener continues running.");
        }
        finally
        {
            handle.Free();
        }
    }



    private void StartRecordQueryLoopIfEnabled(Guid deviceId, IntPtr loginHandle)
    {
        var enabled = IsEnabled(configuration["DAHUA_ACTIVE_REGISTER_NETSDK_RECORD_QUERY_ENABLED"]);
        var diagnosticMode = IsEnabled(configuration["DAHUA_ACTIVE_REGISTER_NETSDK_RECORD_QUERY_DIAGNOSTIC_MODE"]);
        lock (_diagnosticsLock)
        {
            _diagnostics.NetSdkRecordQueryEnabled = enabled;
            _diagnostics.NetSdkRecordQueryDiagnosticMode = diagnosticMode;
        }
        PersistDiagnostics();

        if (!enabled || _nativeClient is null || loginHandle == IntPtr.Zero)
        {
            return;
        }

        if (!_recordQueryLoops.TryAdd(deviceId, 0))
        {
            logger.LogDebug("Dahua NetSDK record query loop already running for device {DeviceId}", deviceId);
            return;
        }

        var intervalSeconds = ParsePositiveInt(configuration["DAHUA_ACTIVE_REGISTER_NETSDK_RECORD_QUERY_INTERVAL_SECONDS"], 30);
        var maxRecords = ParsePositiveInt(configuration["DAHUA_ACTIVE_REGISTER_NETSDK_RECORD_QUERY_MAX_RECORDS"], 20);
        var deviceTimeZone = ResolveTimeZone(configuration["DAHUA_ATTENDANCE_TIMEZONE"] ?? "Asia/Baku");
        var cancellationToken = _recordQueryCancellation.Token;
        logger.LogInformation("Dahua NetSDK Active Register record-query fallback enabled. Device {DeviceId}, IntervalSeconds {IntervalSeconds}, MaxRecords {MaxRecords}, TimeZone {TimeZone}, DiagnosticMode {DiagnosticMode}", deviceId, intervalSeconds, maxRecords, deviceTimeZone.Id, diagnosticMode);

        _ = Task.Run(() => RunRecordQueryLoopAsync(deviceId, loginHandle, intervalSeconds, maxRecords, deviceTimeZone, diagnosticMode, cancellationToken), CancellationToken.None);
    }

    private async Task RunRecordQueryLoopAsync(Guid deviceId, IntPtr loginHandle, int intervalSeconds, int maxRecords, TimeZoneInfo deviceTimeZone, bool diagnosticMode, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await QueryAccessControlRecordsOnceAsync(deviceId, loginHandle, maxRecords, deviceTimeZone, diagnosticMode, ingestRecords: true, cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown path.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Dahua NetSDK record query loop stopped unexpectedly for device {DeviceId}", deviceId);
        }
        finally
        {
            _recordQueryLoops.TryRemove(deviceId, out _);
        }
    }

    private async Task<object> QueryAccessControlRecordsOnceAsync(Guid deviceId, IntPtr loginHandle, int maxRecords, TimeZoneInfo deviceTimeZone, bool diagnosticMode, bool ingestRecords, CancellationToken cancellationToken)
    {
        if (_nativeClient is null) return new { success = false, error = "Dahua NetSDK native client is unavailable" };

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BuildTrackDbContext>();
        var pipeline = scope.ServiceProvider.GetRequiredService<IDahuaAccessRecordIngestionPipeline>();
        var device = await db.Devices.FirstOrDefaultAsync(x => x.Id == deviceId, cancellationToken);
        if (device is null)
        {
            logger.LogWarning("Dahua NetSDK record query skipped because device {DeviceId} no longer exists", deviceId);
            return new { success = false, error = $"Device {deviceId} was not found" };
        }

        var cursor = Math.Max(0, device.LastRecNo ?? 0);
        var queriedAt = DateTimeOffset.UtcNow;
        var result = _nativeClient.TryQueryAccessControlCardRecords(loginHandle, cursor, maxRecords, deviceTimeZone, diagnosticMode);
        var cursorResult = DahuaNetSdkRecordQueryCursor.Apply(result.Records, cursor);
        var records = cursorResult.CandidateRecords;
        var maxProcessedRecNo = cursor;
        var ingested = 0;

        if (result.Success && ingestRecords)
        {
            foreach (var record in records)
            {
                await pipeline.IngestAsync(device.Id, record, DahuaEventSource.ActiveRegister, cancellationToken);
                ingested++;
                if (record.RecNo is not null && record.RecNo.Value > maxProcessedRecNo) maxProcessedRecNo = record.RecNo.Value;
                logger.LogInformation("Submitted NetSDK record-query event to shared ingestion pipeline. Device {DeviceId}, WorkerExternalId {WorkerExternalId}, CardName {CardName}, Status {Status}, Method {Method}, RecNo {RecNo}, EventTime {EventTime}", device.Id, record.UserId, record.CardName, record.StatusRaw, record.MethodRaw, record.RecNo, record.CreateTime);
            }

            if (maxProcessedRecNo > cursor)
            {
                device.LastRecNo = maxProcessedRecNo;
                device.LastSeenAt = DateTimeOffset.UtcNow;
                device.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        var decodedPayload = new
        {
            source = DahuaEventSourceExtensions.ActiveRegisterSource,
            queryType = "NETSDK_RECORD_QUERY_ACCESSCTLCARDREC",
            diagnosticMode,
            ingestRecords,
            result.Success,
            result.Error,
            result.ErrorCode,
            cursor,
            findNextAttempts = result.FindNextAttempts,
            returnedRecords = result.Records.Count,
            candidateRecords = records.Count,
            ingested,
            lastRecNo = maxProcessedRecNo,
            records = result.Records.Take(10).Select(x => x.RawFields),
            attempts = result.StrategyAttempts.Select(x => new
            {
                x.QueryMode,
                x.RecordType,
                x.RecordTypeName,
                x.ConditionStructName,
                x.ConditionBytesLength,
                x.ConditionFirst256Hex,
                x.FindRecordReturnHandle,
                x.FindRecordReturnBool,
                x.FindRecordNativeErrorSigned,
                x.FindRecordNativeErrorHex,
                x.FindNextReturnBool,
                x.FindNextNativeErrorSigned,
                x.FindNextNativeErrorHex,
                x.FindNextCalls,
                outParamRetRecordNum = x.OutParamRetRecordNum,
                x.OutputBufferFirst256Hex,
                mappedRecords = x.MappedRecordDiagnostics,
                x.Error,
            }),
        };
        var decodedJson = JsonSerializer.Serialize(decodedPayload);

        await PersistActiveRegisterRawEventAsync(
            db,
            device.Id,
            device.RegisterDeviceId,
            device.LastKnownIp,
            null,
            0,
            DahuaNetSdkRecordQueryMapper.RecordTypeAccessControlCardRecEx,
            "NETSDK_RECORD_QUERY_ACCESSCTLCARDREC",
            [],
            result.Success ? "RecordQuerySucceeded" : "RecordQueryFailed",
            decodedJson,
            null,
            cancellationToken);

        lock (_diagnosticsLock)
        {
            _diagnostics.NetSdkRecordQueryEnabled = true;
            _diagnostics.NetSdkRecordQueryDiagnosticMode = diagnosticMode;
            _diagnostics.LastRecordQueryAt = queriedAt;
            _diagnostics.LastRecordQuerySuccess = result.Success;
            _diagnostics.LastRecordQueryError = result.Error;
            _diagnostics.LastRecordQueryCount = result.Records.Count;
            _diagnostics.LastRecordQueryLastRecNo = maxProcessedRecNo;
        }
        PersistDiagnostics();

        if (result.Success)
        {
            logger.LogInformation("Dahua NetSDK record query completed. Device {DeviceId}, Cursor {Cursor}, Returned {Returned}, Candidates {Candidates}, Ingested {Ingested}, LastRecNo {LastRecNo}", device.Id, cursor, result.Records.Count, records.Count, ingested, maxProcessedRecNo);
            if (result.Records.Count == 0)
            {
                logger.LogWarning("Dahua NetSDK record query returned 0 mapped records. Device {DeviceId}, Cursor {Cursor}, StrategyAttempts {StrategyAttempts}", device.Id, cursor, result.StrategyAttempts.Count);
            }
        }
        else
        {
            logger.LogWarning("Dahua NetSDK record query failed. Device {DeviceId}, Cursor {Cursor}, Error {Error}, ErrorCode {ErrorCode}", device.Id, cursor, result.Error, result.ErrorCode);
        }

        return decodedPayload;
    }
    private async Task PersistActiveRegisterRawEventAsync(
        BuildTrackDbContext db,
        Guid? deviceId,
        string? registerDeviceId,
        string? remoteIp,
        int? remotePort,
        int listenerPort,
        int command,
        string? commandName,
        byte[] payload,
        string decodeStatus,
        string? decodedJson,
        DahuaActiveRegisterPayloadDiagnostics? payloadDiagnostics,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled(configuration["DAHUA_ACTIVE_REGISTER_DIAGNOSTICS_ENABLED"], defaultValue: true)) return;

        try
        {
            db.DahuaActiveRegisterRawEvents.Add(new DahuaActiveRegisterRawEvent
            {
                DeviceId = deviceId,
                RegisterDeviceId = registerDeviceId,
                RemoteIp = remoteIp,
                RemotePort = remotePort,
                ListenerPort = listenerPort,
                CallbackCommand = command,
                CallbackCommandName = commandName,
                PayloadBytes = payload.Length,
                PayloadFirstBytesHex = Convert.ToHexString(payload.Take(256).ToArray()),
                PayloadBase64 = payload.Length == 0 ? null : Convert.ToBase64String(payload),
                DecodeStatus = decodeStatus,
                DecodedJson = BuildRawEventDecodedJson(decodedJson, payloadDiagnostics),
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist Dahua Active Register raw callback diagnostic. Listener continues running.");
        }
    }


    private static string? BuildRawEventDecodedJson(string? decodedJson, DahuaActiveRegisterPayloadDiagnostics? payloadDiagnostics)
    {
        if (decodedJson is null && payloadDiagnostics is null) return null;

        JsonElement? decoded = null;
        if (!string.IsNullOrWhiteSpace(decodedJson))
        {
            try
            {
                decoded = JsonSerializer.Deserialize<JsonElement>(decodedJson);
            }
            catch (JsonException)
            {
                decoded = JsonSerializer.SerializeToElement(new { raw = decodedJson });
            }
        }

        return JsonSerializer.Serialize(new
        {
            decoded,
            payloadDiagnostics,
        });
    }
    private ActiveRegisterPayloadDecodeResult TryDecodeActiveRegisterPayload(int command, byte[] payload)
    {
        if (payload.Length == 0) return new ActiveRegisterPayloadDecodeResult("EmptyPayload", null, null);

        if (command == DahuaNetSdkAccessEventDecoder.AccessControlEventCommand)
        {
            var handle = GCHandle.Alloc(payload, GCHandleType.Pinned);
            try
            {
                var decoded = DahuaNetSdkAccessEventDecoder.TryDecodeAccessControlEvent(handle.AddrOfPinnedObject(), (uint)payload.Length, out var sdkEvent, out var skipReason);
                if (DahuaSdkAccessEventNormalizer.TryNormalize(sdkEvent, out var record))
                {
                    var json = JsonSerializer.Serialize(new { sdkEvent, skipReason, decoded });
                    return new ActiveRegisterPayloadDecodeResult(decoded ? "DecodedAccessControlEvent" : "DecodedAccessControlEventSkipped", record, json);
                }

                return new ActiveRegisterPayloadDecodeResult("DecodeFailed", null, JsonSerializer.Serialize(new { skipReason }));
            }
            finally
            {
                handle.Free();
            }
        }

        if (DahuaSdkAccessEventNormalizer.TryParseAsciiKeyValuePayload(payload, out var asciiEvent)
            && (!string.IsNullOrWhiteSpace(asciiEvent.UserId)
                || !string.IsNullOrWhiteSpace(asciiEvent.CardName)
                || !string.IsNullOrWhiteSpace(asciiEvent.Status)
                || !string.IsNullOrWhiteSpace(asciiEvent.Method)))
        {
            DahuaSdkAccessEventNormalizer.TryNormalize(asciiEvent, out var record);
            return new ActiveRegisterPayloadDecodeResult("DecodedAsciiAccessRecord", record, JsonSerializer.Serialize(asciiEvent));
        }

        return new ActiveRegisterPayloadDecodeResult("ServiceCallbackOnly", null, null);
    }

    private static bool IsEnabled(string? value, bool defaultValue = false)
    {
        if (string.IsNullOrWhiteSpace(value)) return defaultValue;
        return value.Equals("true", StringComparison.OrdinalIgnoreCase)
               || value.Equals("1", StringComparison.OrdinalIgnoreCase)
               || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static int ParsePositiveInt(string? value, int defaultValue) =>
        int.TryParse(value, out var parsed) && parsed > 0 ? parsed : defaultValue;

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException) when (timeZoneId.Equals("Asia/Baku", StringComparison.OrdinalIgnoreCase))
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Azerbaijan Standard Time");
        }
        catch (InvalidTimeZoneException) when (timeZoneId.Equals("Asia/Baku", StringComparison.OrdinalIgnoreCase))
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Azerbaijan Standard Time");
        }
    }

    private sealed record ActiveRegisterPayloadDecodeResult(string DecodeStatus, DahuaAccessRecord? Record, string? DecodedJson);
    private async Task<Device?> MatchDeviceAsync(BuildTrackDbContext db, string? registerDeviceId, int listenerPort, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(registerDeviceId))
        {
            var matched = await db.Devices.FirstOrDefaultAsync(x => x.RegisterDeviceId == registerDeviceId, cancellationToken);
            if (matched is not null) return matched;
        }

        var candidates = await db.Devices.Where(x => x.Mode == DeviceMode.ActiveRegister && x.RegisterPort == listenerPort).OrderBy(x => x.CreatedAt).Take(2).ToListAsync(cancellationToken);
        logger.LogDebug("Active register fallback check: enabled={Enabled}, listenerPort={ListenerPort}, candidateCount={CandidateCount}", _singleDeviceFallbackEnabled, listenerPort, candidates.Count);

        var fallback = DahuaActiveRegisterFallbackMatcher.MatchSingleDeviceFallback(candidates, _singleDeviceFallbackEnabled);
        if (fallback is not null) logger.LogWarning("Matched Dahua connection by single-device fallback. Do not use in multi-device production.");
        else if (candidates.Count > 1) logger.LogWarning("Single-device fallback skipped because multiple active register devices exist on this port.");
        return fallback;
    }

    private void SetStatus(string status)
    {
        _decodeStatus = status;
        lock (_diagnosticsLock) _diagnostics.NetSdkDecodeStatus = status;
        PersistDiagnostics();
    }


    private void PersistDiagnostics()
    {
        var version = Interlocked.Increment(ref _diagnosticsPersistVersion);
        var snapshot = Diagnostics;

        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<BuildTrackDbContext>();
                var entity = await db.NetSdkRuntimeDiagnostics.FirstOrDefaultAsync(x => x.Id == "dahua-netsdk-runtime", CancellationToken.None);
                if (version < Interlocked.Read(ref _diagnosticsPersistVersion)) return;
                if (entity is null)
                {
                    entity = new NetSdkRuntimeDiagnostics { Id = "dahua-netsdk-runtime" };
                    db.NetSdkRuntimeDiagnostics.Add(entity);
                }

                entity.SdkLoaded = snapshot.SdkLoaded;
                entity.SdkInitialized = snapshot.SdkInitialized;
                entity.ListenerPortsJson = JsonSerializer.Serialize(snapshot.ListenerPorts ?? []);
                entity.AlarmCallbackConfigured = snapshot.AlarmCallbackConfigured;
                entity.ActiveRegisterServiceMode = snapshot.ActiveRegisterServiceMode;
                entity.ExperimentalStartServiceEnabled = snapshot.ExperimentalStartServiceEnabled;
                entity.ExperimentalStartServiceStarted = snapshot.ExperimentalStartServiceStarted;
                entity.ExperimentalStartServiceHandle = snapshot.ExperimentalStartServiceHandle;
                entity.ExperimentalStartServiceLastCommand = snapshot.ExperimentalStartServiceLastCommand;
                entity.ExperimentalStartServiceLastPayloadBytes = snapshot.ExperimentalStartServiceLastPayloadBytes;
                entity.ExperimentalStartServiceLastDecodeStatus = snapshot.ExperimentalStartServiceLastDecodeStatus;
                entity.ExperimentalStartServiceErrorSigned = snapshot.ExperimentalStartServiceErrorSigned;
                entity.ExperimentalStartServiceErrorHex = snapshot.ExperimentalStartServiceErrorHex;
                entity.LastServiceCommand = snapshot.LastServiceCommand;
                entity.LastServiceEventType = snapshot.LastServiceEventType;
                entity.LastServicePayloadBytes = snapshot.LastServicePayloadBytes;
                entity.LastServicePayloadFirst256Hex = snapshot.LastServicePayloadFirst256Hex;
                entity.LastRegisterDeviceId = snapshot.LastRegisterDeviceId;
                entity.LastParsedRegisterDeviceIdOffset = snapshot.LastParsedRegisterDeviceIdOffset;
                entity.LastParsedRegisterDeviceId = snapshot.LastParsedRegisterDeviceId;
                entity.LastParsedSerialOffset = snapshot.LastParsedSerialOffset;
                entity.LastParsedSerial = snapshot.LastParsedSerial;
                entity.LastParsedRemoteIp = snapshot.LastParsedRemoteIp;
                entity.LastParsedRemotePort = snapshot.LastParsedRemotePort;
                entity.LastPossibleSessionHandlesJson = snapshot.LastPossibleSessionHandlesJson;
                entity.LastPayloadStructLayout = snapshot.LastPayloadStructLayout;
                entity.ResponseDevRegCalled = snapshot.ResponseDevRegCalled;
                entity.ResponseDevRegSuccess = snapshot.ResponseDevRegSuccess;
                entity.ResponseDevRegErrorSigned = snapshot.ResponseDevRegErrorSigned;
                entity.ResponseDevRegErrorHex = snapshot.ResponseDevRegErrorHex;
                entity.ResponseDevRegDevSerial = snapshot.ResponseDevRegDevSerial;
                entity.ResponseDevRegDevSerialLength = snapshot.ResponseDevRegDevSerialLength;
                entity.ResponseDevRegIp = snapshot.ResponseDevRegIp;
                entity.ResponseDevRegPort = snapshot.ResponseDevRegPort;
                entity.ResponseDevRegAccept = snapshot.ResponseDevRegAccept;
                entity.ResponseDevRegCommandSource = snapshot.ResponseDevRegCommandSource;
                entity.LastServiceCallbackHandle = snapshot.LastServiceCallbackHandle;
                entity.LastServiceCallbackHandleNonZero = snapshot.LastServiceCallbackHandleNonZero;
                entity.ExperimentalServiceHandleSubscribeEnabled = snapshot.ExperimentalServiceHandleSubscribeEnabled;
                entity.LastExperimentalSubscribeJson = snapshot.LastExperimentalSubscribeJson;
                entity.ActiveRegisterSessionHandleFound = snapshot.ActiveRegisterSessionHandleFound;
                entity.ActiveRegisterSessionHandleValueNonZero = snapshot.ActiveRegisterSessionHandleValueNonZero;
                entity.ActiveRegisterSessionHandleValue = snapshot.ActiveRegisterSessionHandleValue;
                entity.ActiveRegisterSessionHandleSource = snapshot.ActiveRegisterSessionHandleSource;
                entity.ActiveRegisterSessionHandleStrategyResult = snapshot.ActiveRegisterSessionHandleStrategyResult;
                entity.LoginStrategy = snapshot.LoginStrategy;
                entity.LoginHandle = snapshot.LoginHandle;
                entity.LoginSucceeded = snapshot.LoginSucceeded;
                entity.LoginErrorSigned = snapshot.LoginErrorSigned;
                entity.LoginErrorHex = snapshot.LoginErrorHex;
                entity.LoginNativeErrorSigned = snapshot.LoginNativeErrorSigned;
                entity.LoginNativeErrorHex = snapshot.LoginNativeErrorHex;
                entity.LoginPossibleMarshallingWarning = snapshot.LoginPossibleMarshallingWarning;
                entity.StartListenExCalled = snapshot.StartListenExCalled;
                entity.StartListenExSuccess = snapshot.StartListenExSuccess;
                entity.StartListenExErrorSigned = snapshot.StartListenExErrorSigned;
                entity.StartListenExErrorHex = snapshot.StartListenExErrorHex;
                entity.LastAlarmCommand = snapshot.LastAlarmCommand;
                entity.LastAlarmCommandName = snapshot.LastAlarmCommandName;
                entity.LastAlarmPayloadFirst256Hex = snapshot.LastAlarmPayloadFirst256Hex;
                entity.LastAlarmDecodeStatus = snapshot.LastAlarmDecodeStatus;
                entity.LastDecodedAlarmJson = snapshot.LastDecodedAlarmJson;
                entity.NetSdkRecordQueryEnabled = snapshot.NetSdkRecordQueryEnabled;
                entity.NetSdkRecordQueryDiagnosticMode = snapshot.NetSdkRecordQueryDiagnosticMode;
                entity.LastRecordQueryAt = snapshot.LastRecordQueryAt;
                entity.LastRecordQuerySuccess = snapshot.LastRecordQuerySuccess;
                entity.LastRecordQueryError = snapshot.LastRecordQueryError;
                entity.LastRecordQueryCount = snapshot.LastRecordQueryCount;
                entity.LastRecordQueryLastRecNo = snapshot.LastRecordQueryLastRecNo;
                entity.LastDecodeError = snapshot.LastDecodeError;
                entity.NetSdkDecodeStatus = snapshot.NetSdkDecodeStatus;
                entity.UpdatedAt = DateTimeOffset.UtcNow;
                if (version < Interlocked.Read(ref _diagnosticsPersistVersion)) return;
                await db.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to persist Dahua NetSDK diagnostics");
            }
        });
    }
    private static string GetCommandName(int command) => DahuaNetSdkAlarmCommandDiagnostics.ResolveCommandName(command);

    private static string ToHex(int value) => $"0x{unchecked((uint)value):X8}";

    private static string BuildRegistrationKey(string registerDeviceId, string? remoteIp, int remotePort, IntPtr serviceCallbackHandle)
        => DahuaNetSdkSubscriptionDiagnostics.BuildRegistrationKey(registerDeviceId, remoteIp, remotePort, serviceCallbackHandle.ToInt64());

    private static byte[] CopyPayload(IntPtr param, uint paramLength)
    {
        if (param == IntPtr.Zero || paramLength == 0) return [];
        var length = (int)Math.Min(paramLength, 128 * 1024);
        var payload = new byte[length];
        Marshal.Copy(param, payload, 0, length);
        return payload;
    }
}

public sealed record DahuaActiveRegisterRegistration(
    string Kind,
    string? RegisterDeviceId,
    string? Serial,
    bool SupportsRedirection,
    bool HasSessionHandle,
    IntPtr SessionHandle);
public sealed record DahuaActiveRegisterPayloadDiagnostics(
    int Command,
    string CommandName,
    int PayloadBytes,
    string PayloadFirst256Hex,
    string? RegisterDeviceId,
    int? RegisterDeviceIdOffset,
    string? Serial,
    int? SerialOffset,
    string? RemoteIp,
    int RemotePort,
    long ServiceCallbackHandle,
    string StructLayout,
    IReadOnlyList<long> PossibleSessionHandles);

public static class DahuaActiveRegisterPayloadParser
{
    public static DahuaActiveRegisterPayloadDiagnostics Inspect(int command, byte[] payload, string? remoteIp, int remotePort, IntPtr serviceCallbackHandle)
    {
        var registration = Parse(command, payload);
        var first256Hex = Convert.ToHexString(payload.Take(256).ToArray());
        return command switch
        {
            1 => new DahuaActiveRegisterPayloadDiagnostics(
                command,
                registration.Kind,
                payload.Length,
                first256Hex,
                registration.RegisterDeviceId,
                registration.RegisterDeviceId is null ? null : 0,
                registration.Serial,
                registration.Serial is null ? null : 0,
                remoteIp,
                remotePort,
                serviceCallbackHandle.ToInt64(),
                "DH_DVR_SERIAL_RETURN: callback payload is char* szDevSerial; parser reads null-terminated ASCII at offset 0.",
                []),
            5 => new DahuaActiveRegisterPayloadDiagnostics(
                command,
                registration.Kind,
                payload.Length,
                first256Hex,
                registration.RegisterDeviceId,
                registration.RegisterDeviceId is null ? null : 0,
                registration.Serial,
                registration.Serial is null ? null : 0,
                remoteIp,
                remotePort,
                serviceCallbackHandle.ToInt64(),
                "NET_CB_SERIAL_RETURN_INFO: szDevSerial[64] offset 0, BOOL bSupportRedirection offset 64, szReserved[1020] offset 68, sizeof 1088. Header exposes no login/session handle field.",
                ScanPossibleHandles(payload, startOffset: 68)),
            4 => new DahuaActiveRegisterPayloadDiagnostics(
                command,
                registration.Kind,
                payload.Length,
                first256Hex,
                registration.RegisterDeviceId,
                registration.RegisterDeviceId is null ? null : 0,
                registration.Serial,
                registration.Serial is null ? null : 0,
                remoteIp,
                remotePort,
                serviceCallbackHandle.ToInt64(),
                "NET_CB_AUTOREGISTER_PRIMARY_BACKUP_INFO: szDevSerial[64] offset 0, nType offset 64, szReserved[1020] offset 68.",
                ScanPossibleHandles(payload, startOffset: 68)),
            _ => new DahuaActiveRegisterPayloadDiagnostics(
                command,
                registration.Kind,
                payload.Length,
                first256Hex,
                registration.RegisterDeviceId,
                null,
                registration.Serial,
                null,
                remoteIp,
                remotePort,
                serviceCallbackHandle.ToInt64(),
                "Unknown service callback command; no SDK struct layout is mapped.",
                ScanPossibleHandles(payload, startOffset: 0)),
        };
    }
    public static DahuaActiveRegisterRegistration Parse(int command, byte[] payload)
    {
        return command switch
        {
            1 => ParseSerialReturn(payload),
            5 => ParseSerialReturnEx(payload),
            4 => ParsePrimaryBackup(payload),
            _ => new DahuaActiveRegisterRegistration($"Command_{command}", TryParseAsciiRegisterId(payload), null, false, false, IntPtr.Zero),
        };
    }

    private static DahuaActiveRegisterRegistration ParseSerialReturn(byte[] payload)
    {
        var serial = ReadNullTerminated(payload, 0, Math.Min(payload.Length, 160));
        return new DahuaActiveRegisterRegistration("DH_DVR_SERIAL_RETURN", EmptyToNull(serial), EmptyToNull(serial), false, false, IntPtr.Zero);
    }

    private static DahuaActiveRegisterRegistration ParseSerialReturnEx(byte[] payload)
    {
        var serial = ReadNullTerminated(payload, 0, Math.Min(payload.Length, 64));
        var supportsRedirection = payload.Length >= 68 && BitConverter.ToInt32(payload, 64) != 0;
        return new DahuaActiveRegisterRegistration("DH_DVR_SERIAL_RETURN_EX", EmptyToNull(serial), EmptyToNull(serial), supportsRedirection, false, IntPtr.Zero);
    }

    private static DahuaActiveRegisterRegistration ParsePrimaryBackup(byte[] payload)
    {
        var serial = ReadNullTerminated(payload, 0, Math.Min(payload.Length, 64));
        return new DahuaActiveRegisterRegistration("NET_DEV_AUTOREGISTER_PRIMARY_BACKUP", EmptyToNull(serial), EmptyToNull(serial), false, false, IntPtr.Zero);
    }


    private static IReadOnlyList<long> ScanPossibleHandles(byte[] payload, int startOffset)
    {
        var handles = new List<long>();
        if (payload.Length < startOffset + 8) return handles;

        var maxOffset = Math.Min(payload.Length - 8, startOffset + 256);
        for (var offset = Math.Max(0, startOffset); offset <= maxOffset; offset += 4)
        {
            var value = BitConverter.ToInt64(payload, offset);
            if (value > 4096 && value < 0x00007FFFFFFFFFFF && !handles.Contains(value))
            {
                handles.Add(value);
                if (handles.Count >= 4) break;
            }
        }

        return handles;
    }
    private static string? TryParseAsciiRegisterId(byte[] payload)
    {
        if (DahuaSdkAccessEventNormalizer.TryParseAsciiKeyValuePayload(payload, out var sdkEvent)) return sdkEvent.RegisterDeviceId;
        return null;
    }

    private static string ReadNullTerminated(byte[] payload, int offset, int maxLength)
    {
        if (payload.Length <= offset || maxLength <= 0) return string.Empty;
        var length = 0;
        while (length < maxLength && offset + length < payload.Length && payload[offset + length] != 0) length++;
        if (length == 0) return string.Empty;
        return Encoding.ASCII.GetString(payload, offset, length).Trim('\0', ' ', '\r', '\n', '\t');
    }

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}














































