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
    public string? LastRegisterDeviceId { get; set; }
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
                    LastRegisterDeviceId = _diagnostics.LastRegisterDeviceId,
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
        logger.LogInformation("Active Register service callback received. Command={Command}, EventType={EventType}, PayloadBytes={PayloadBytes}", command, eventType, payload.Length);
        logger.LogInformation("Service callback lHandle={Handle}, Command={Command}, EventType={EventType}", listenHandle.ToInt64(), command, eventType);
        lock (_diagnosticsLock)
        {
            _diagnostics.LastServiceCommand = command;
            _diagnostics.LastServiceEventType = eventType;
            _diagnostics.LastServicePayloadBytes = payload.Length;
            _diagnostics.LastServiceCallbackHandle = listenHandle.ToInt64();
            _diagnostics.LastServiceCallbackHandleNonZero = listenHandle != IntPtr.Zero;
        }

        PersistDiagnostics();
        _ = Task.Run(() => HandleServiceCallbackAsync(deviceIp, devicePort, listenerPort, command, payload, listenHandle));
        return 0;
    }

    private bool OnAlarmMessageCallback(int command, IntPtr loginHandle, IntPtr payload, uint payloadLength, string deviceIp, int devicePort, IntPtr userData)
    {
        logger.LogInformation("NetSDK alarm callback received. Command=0x{Command:X}, PayloadBytes={PayloadBytes}", command, payloadLength);
        lock (_diagnosticsLock) _diagnostics.LastAlarmCommand = command;
        PersistDiagnostics();
        var payloadCopy = CopyPayload(payload, payloadLength);
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
                await PersistActiveRegisterRawEventAsync(db, null, registerDeviceId, remoteIp, remotePort, listenerPort, command, eventType, payload, "DeviceDisconnected", decodeResult.DecodedJson, CancellationToken.None);
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
                await PersistActiveRegisterRawEventAsync(db, null, registerDeviceId, remoteIp, remotePort, listenerPort, command, eventType, payload, decodeResult.DecodeStatus, decodeResult.DecodedJson, CancellationToken.None);
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

            await PersistActiveRegisterRawEventAsync(db, device.Id, device.RegisterDeviceId, remoteIp, remotePort, listenerPort, command, eventType, payload, rawDecodeStatus, decodeResult.DecodedJson, CancellationToken.None);

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
                var attempts = _nativeClient.TryLoginActiveRegisterStrategies(device.RegisterDeviceId, remoteIp, remotePort, username, password);
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
                        "Dahua Active Register server-connection login attempt. Strategy={Strategy}, RegisterDeviceId={RegisterDeviceId}, UsernamePresent={UsernamePresent}, PasswordPresent={PasswordPresent}, IpArgument={IpArgument}, PortArgument={PortArgument}, SpecCap={SpecCap}, CapParamLength={CapParamLength}, LoginHandle={LoginHandle}, NativeErrorPointer={NativeErrorPointer}, NativeErrorHex={NativeErrorHex}, LastError={LastError}, LastErrorHex={LastErrorHex}, UsesLoginEx2={UsesLoginEx2}",
                        attempt.Strategy,
                        attempt.RegisterDeviceId,
                        attempt.UsernamePresent,
                        attempt.PasswordPresent,
                        string.IsNullOrEmpty(attempt.IpArgument) ? "<empty>" : attempt.IpArgument,
                        attempt.PortArgument,
                        attempt.SpecCap,
                        attempt.CapParamStringLength,
                        attempt.LoginHandle,
                        attempt.NativeErrorPointer,
                        ToHex(attempt.NativeErrorPointer),
                        attempt.LastErrorAfterCall,
                        ToHex(attempt.LastErrorAfterCall),
                        attempt.UsesLoginEx2);

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
                        _diagnostics.LastDecodeError = $"CLIENT_LoginEx/CLIENT_LoginEx2 active-register server connection login failed. LastStrategy={lastAttempt?.Strategy ?? "None"}, ErrorSigned={lastError}, ErrorHex={ToHex(lastError)}, NativeErrorSigned={nativeError}, NativeErrorHex={ToHex(nativeError)}";
                    }
                    SetStatus("ServerConnLoginFailed");
                    await connectionLogger.LogAsync(
                        device.Id,
                        device.RegisterDeviceId,
                        remoteIp,
                        remotePort,
                        "netsdk_server_conn_login_failed",
                        "CLIENT_LoginEx/CLIENT_LoginEx2 active-register server connection login failed",
                        new
                        {
                            attempts = attempts.Select(x => new
                            {
                                x.Strategy,
                                x.RegisterDeviceId,
                                x.UsernamePresent,
                                x.PasswordPresent,
                                x.IpArgument,
                                x.PortArgument,
                                x.SpecCap,
                                x.CapParamStringLength,
                                x.LoginHandle,
                                x.NativeErrorPointer,
                                nativeErrorHex = ToHex(x.NativeErrorPointer),
                                x.LastErrorAfterCall,
                                lastErrorHex = ToHex(x.LastErrorAfterCall),
                                x.UsesLoginEx2,
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

        var rawPayload = DahuaRawPayloadFormatter.CreateLogPayload(payload, 0, remoteIp, remotePort);
        var raw = new
        {
            command,
            commandHex = $"0x{command:X}",
            commandName = GetCommandName(command),
            loginHandle = loginHandle.ToInt64(),
            rawPayload,
        };

        if (command != DahuaNetSdkAccessEventDecoder.AccessControlEventCommand)
        {
            logger.LogDebug("NetSDK alarm callback skipped. Command=0x{Command:X}, PayloadBytes={PayloadBytes}", command, payload.Length);
            await connectionLogger.LogAsync(device?.Id, device?.RegisterDeviceId, remoteIp, remotePort, "netsdk_event_skipped", "Unknown or unsupported NetSDK alarm command skipped", raw, CancellationToken.None);
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
                PayloadFirstBytesHex = Convert.ToHexString(payload.Take(128).ToArray()),
                PayloadBase64 = payload.Length == 0 ? null : Convert.ToBase64String(payload),
                DecodeStatus = decodeStatus,
                DecodedJson = decodedJson,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist Dahua Active Register raw callback diagnostic. Listener continues running.");
        }
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
                entity.LastRegisterDeviceId = snapshot.LastRegisterDeviceId;
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
    private static string GetCommandName(int command) => command switch
    {
        -1 => "DH_DVR_DISCONNECT",
        1 => "DH_DVR_SERIAL_RETURN",
        2 => "NET_DEV_AUTOREGISTER_RETURN",
        3 => "NET_DEV_NOTIFY_IP_RETURN",
        4 => "NET_DEV_AUTOREGISTER_PRIMARY_BACKUP",
        5 => "DH_DVR_SERIAL_RETURN_EX",
        DahuaNetSdkAccessEventDecoder.AccessControlEventCommand => "DH_ALARM_ACCESS_CTL_EVENT",
        _ => $"UnknownCommand_{command}"
    };

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

public static class DahuaActiveRegisterPayloadParser
{
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


























