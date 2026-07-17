using System.Runtime.InteropServices;
using BuildTrack.Domain.Dahua;
using Microsoft.Extensions.Logging;

namespace BuildTrack.Infrastructure.Dahua;

internal sealed class DahuaNetSdkNativeClient : IDisposable
{
    private const int LoginSpecCapServerConn = 2;

    private readonly ILogger _logger;
    private readonly IntPtr _libraryHandle;
    private readonly ClientInitDelegate _clientInit;
    private readonly ClientCleanupDelegate _clientCleanup;
    private readonly ClientListenServerDelegate _clientListenServer;
    private readonly ClientStopListenServerDelegate _clientStopListenServer;
    private readonly ClientStartServiceDelegate? _clientStartService;
    private readonly ClientStopServiceDelegate? _clientStopService;
    private readonly ClientGetLastErrorDelegate? _clientGetLastError;
    private readonly ClientSetDvrMessageCallbackDelegate? _clientSetDvrMessageCallback;
    private readonly ClientLoginExDelegate? _clientLoginEx;
    private readonly ClientLoginEx2Delegate? _clientLoginEx2;
    private readonly ClientLoginWithHighLevelSecurityDelegate? _clientLoginWithHighLevelSecurity;
    private readonly ClientLogoutDelegate? _clientLogout;
    private readonly ClientResponseDevRegDelegate? _clientResponseDevReg;
    private readonly ClientStartListenExDelegate? _clientStartListenEx;
    private readonly ClientStopListenDelegate? _clientStopListen;
    private readonly ClientFindRecordDelegate? _clientFindRecord;
    private readonly ClientFindNextRecordDelegate? _clientFindNextRecord;
    private readonly ClientFindRecordCloseDelegate? _clientFindRecordClose;
    private readonly DisconnectCallback _disconnectCallback;
    private readonly ReconnectCallback _reconnectCallback;
    private AlarmMessageCallback? _alarmMessageCallback;
    private bool _initialized;

    public DahuaNetSdkNativeClient(IntPtr libraryHandle, ILogger logger)
    {
        _libraryHandle = libraryHandle;
        _logger = logger;
        _clientInit = GetRequiredDelegate<ClientInitDelegate>("CLIENT_Init");
        _clientCleanup = GetRequiredDelegate<ClientCleanupDelegate>("CLIENT_Cleanup");
        _clientListenServer = GetRequiredDelegate<ClientListenServerDelegate>("CLIENT_ListenServer");
        _clientStopListenServer = GetRequiredDelegate<ClientStopListenServerDelegate>("CLIENT_StopListenServer");
        _clientStartService = TryGetDelegate<ClientStartServiceDelegate>("CLIENT_StartService");
        _clientStopService = TryGetDelegate<ClientStopServiceDelegate>("CLIENT_StopService");
        _clientGetLastError = TryGetDelegate<ClientGetLastErrorDelegate>("CLIENT_GetLastError");
        _clientSetDvrMessageCallback = TryGetDelegate<ClientSetDvrMessageCallbackDelegate>("CLIENT_SetDVRMessCallBack");
        _clientLoginEx = TryGetDelegate<ClientLoginExDelegate>("CLIENT_LoginEx");
        _clientLoginEx2 = TryGetDelegate<ClientLoginEx2Delegate>("CLIENT_LoginEx2");
        _clientLoginWithHighLevelSecurity = TryGetDelegate<ClientLoginWithHighLevelSecurityDelegate>("CLIENT_LoginWithHighLevelSecurity");
        _clientLogout = TryGetDelegate<ClientLogoutDelegate>("CLIENT_Logout");
        _clientResponseDevReg = TryGetDelegate<ClientResponseDevRegDelegate>("CLIENT_ResponseDevReg");
        _clientStartListenEx = TryGetDelegate<ClientStartListenExDelegate>("CLIENT_StartListenEx");
        _clientStopListen = TryGetDelegate<ClientStopListenDelegate>("CLIENT_StopListen");
        _clientFindRecord = TryGetDelegate<ClientFindRecordDelegate>("CLIENT_FindRecord");
        _clientFindNextRecord = TryGetDelegate<ClientFindNextRecordDelegate>("CLIENT_FindNextRecord");
        _clientFindRecordClose = TryGetDelegate<ClientFindRecordCloseDelegate>("CLIENT_FindRecordClose");
        _disconnectCallback = OnDisconnect;
        _reconnectCallback = OnReconnect;
    }

    public int LastErrorCode => _clientGetLastError?.Invoke() ?? 0;

    public bool HasAlarmSubscriptionExports =>
        _clientSetDvrMessageCallback is not null
        && (_clientLoginEx is not null || _clientLoginEx2 is not null || _clientLoginWithHighLevelSecurity is not null)
        && _clientStartListenEx is not null
        && _clientStopListen is not null;

    public bool HasRecordQueryExports => _clientFindRecord is not null && _clientFindNextRecord is not null && _clientFindRecordClose is not null;

    public bool Initialize()
    {
        _initialized = _clientInit(_disconnectCallback, IntPtr.Zero);
        if (_initialized)
        {
            TrySetAutoReconnect();
        }

        return _initialized;
    }

    public IntPtr ListenServer(int port, ServiceCallback callback)
    {
        if (!_initialized) return IntPtr.Zero;
        return _clientListenServer("0.0.0.0", (ushort)port, 1000, callback, IntPtr.Zero);
    }

    public IntPtr StartService(int port, ServiceCallback callback)
    {
        if (!_initialized || _clientStartService is null) return IntPtr.Zero;
        return _clientStartService((ushort)port, "0.0.0.0", callback, uint.MaxValue, IntPtr.Zero);
    }

    public bool TryConfigureAlarmCallback(AlarmMessageCallback callback)
    {
        _alarmMessageCallback = callback;
        if (_clientSetDvrMessageCallback is null) return false;

        _clientSetDvrMessageCallback(_alarmMessageCallback, IntPtr.Zero);
        return true;
    }

    public bool TryResponseDeviceRegister(string registerDeviceId, string? ip, int port)
    {
        if (_clientResponseDevReg is null || string.IsNullOrWhiteSpace(registerDeviceId) || string.IsNullOrWhiteSpace(ip)) return false;
        return _clientResponseDevReg(registerDeviceId, ip, (ushort)port, true);
    }

    public IReadOnlyList<DahuaActiveRegisterLoginAttempt> TryLoginActiveRegisterStrategies(string registerDeviceId, string? remoteIp, int remotePort, string username, string password)
    {
        var attempts = new List<DahuaActiveRegisterLoginAttempt>();
        if (string.IsNullOrWhiteSpace(registerDeviceId)) return attempts;

        var strategies = DahuaActiveRegisterLoginStrategyPlan.Build(
            registerDeviceId,
            remoteIp,
            remotePort,
            hasLoginEx: _clientLoginEx is not null,
            hasLoginEx2: _clientLoginEx2 is not null,
            hasHighLevelLogin: _clientLoginWithHighLevelSecurity is not null);

        foreach (var strategy in strategies)
        {
            var attempt = strategy.LoginApi switch
            {
                DahuaActiveRegisterLoginStrategyPlan.ApiLoginEx2 => TryLoginEx2(strategy, registerDeviceId, username, password),
                DahuaActiveRegisterLoginStrategyPlan.ApiHighLevel => TryLoginHighLevel(strategy, registerDeviceId, username, password),
                _ => TryLoginEx(strategy, registerDeviceId, username, password),
            };

            attempts.Add(attempt);
            if (attempt.Succeeded) return attempts;
        }

        return attempts;
    }

    private DahuaActiveRegisterLoginAttempt TryLoginEx(DahuaActiveRegisterLoginStrategy strategy, string registerDeviceId, string username, string password)
    {
        var errorPointer = 0;
        var capParamAllocation = CreateCapParam(registerDeviceId, strategy.CapParamKind);
        var deviceInfo = new NetDeviceInfo { SerialNumber = new byte[48] };
        try
        {
            var handle = _clientLoginEx!(strategy.IpArgument, (ushort)strategy.PortArgument, username, password, LoginSpecCapServerConn, capParamAllocation.Pointer, ref deviceInfo, ref errorPointer);
            var lastError = LastErrorCode;
            return DahuaActiveRegisterLoginAttempt.Create(strategy, LoginSpecCapServerConn, registerDeviceId, username, password, handle, errorPointer, lastError);
        }
        finally
        {
            capParamAllocation.Dispose();
        }
    }

    private DahuaActiveRegisterLoginAttempt TryLoginEx2(DahuaActiveRegisterLoginStrategy strategy, string registerDeviceId, string username, string password)
    {
        var errorPointer = 0;
        var capParamAllocation = CreateCapParam(registerDeviceId, strategy.CapParamKind);
        var deviceInfo = NetDeviceInfoEx.Create();
        try
        {
            var handle = _clientLoginEx2!(strategy.IpArgument, (ushort)strategy.PortArgument, username, password, LoginSpecCapServerConn, capParamAllocation.Pointer, ref deviceInfo, ref errorPointer);
            var lastError = LastErrorCode;
            return DahuaActiveRegisterLoginAttempt.Create(strategy, LoginSpecCapServerConn, registerDeviceId, username, password, handle, errorPointer, lastError);
        }
        finally
        {
            capParamAllocation.Dispose();
        }
    }

    private DahuaActiveRegisterLoginAttempt TryLoginHighLevel(DahuaActiveRegisterLoginStrategy strategy, string registerDeviceId, string username, string password)
    {
        var capParamAllocation = CreateCapParam(registerDeviceId, strategy.CapParamKind);
        var input = NetInLoginWithHighLevelSecurity.Create(strategy.IpArgument, strategy.PortArgument, username, password, LoginSpecCapServerConn, capParamAllocation.Pointer);
        var output = NetOutLoginWithHighLevelSecurity.Create();
        try
        {
            var handle = _clientLoginWithHighLevelSecurity!(ref input, ref output);
            var lastError = LastErrorCode;
            return DahuaActiveRegisterLoginAttempt.Create(strategy, LoginSpecCapServerConn, registerDeviceId, username, password, handle, output.Error, lastError);
        }
        finally
        {
            capParamAllocation.Dispose();
        }
    }

    public bool TryStartListenEx(IntPtr loginHandle)
    {
        if (loginHandle == IntPtr.Zero || _clientStartListenEx is null) return false;
        return _clientStartListenEx(loginHandle);
    }

    public bool TryStopListen(IntPtr loginHandle)
    {
        if (loginHandle == IntPtr.Zero || _clientStopListen is null) return false;
        return _clientStopListen(loginHandle);
    }

    public bool TryLogout(IntPtr loginHandle)
    {
        if (loginHandle == IntPtr.Zero || _clientLogout is null) return false;
        return _clientLogout(loginHandle);
    }

    public bool TryQueryAccessControlCardRecords(IntPtr loginHandle, int maxRecords, TimeZoneInfo deviceTimeZone, out IReadOnlyList<DahuaAccessRecord> records, out string? error)
    {
        var result = TryQueryAccessControlCardRecords(loginHandle, 0, maxRecords, deviceTimeZone);
        records = result.Records;
        error = result.Error;
        return result.Success;
    }
    public DahuaNetSdkRecordQueryResult TryQueryAccessControlCardRecords(IntPtr loginHandle, long lastRecNo, int maxRecords, TimeZoneInfo deviceTimeZone)
    {
        if (loginHandle == IntPtr.Zero)
        {
            return DahuaNetSdkRecordQueryResult.Failure("Login handle is zero", LastErrorCode);
        }

        if (!HasRecordQueryExports)
        {
            return DahuaNetSdkRecordQueryResult.Failure("CLIENT_FindRecord/CLIENT_FindNextRecord/CLIENT_FindRecordClose exports are unavailable", LastErrorCode);
        }

        var records = new List<DahuaAccessRecord>();
        var diagnostics = new List<Dictionary<string, string?>>();
        var condition = FindRecordAccessCtlCardRecConditionEx.Create();
        var conditionPtr = Marshal.AllocHGlobal(Marshal.SizeOf<FindRecordAccessCtlCardRecConditionEx>());
        var recordBuffer = Marshal.AllocHGlobal(DahuaNetSdkRecordQueryMapper.DefaultRecordBufferBytes);
        IntPtr findHandle = IntPtr.Zero;

        try
        {
            Marshal.StructureToPtr(condition, conditionPtr, false);
            var input = NetInFindRecordParam.Create(DahuaNetSdkRecordQueryMapper.RecordTypeAccessControlCardRecEx, conditionPtr);
            var output = NetOutFindRecordParam.Create();
            var findResult = _clientFindRecord!(loginHandle, ref input, ref output, 3000);
            var findError = LastErrorCode;
            if (!findResult || output.FindHandle == IntPtr.Zero)
            {
                return DahuaNetSdkRecordQueryResult.Failure($"CLIENT_FindRecord failed. Result={findResult}, Handle={output.FindHandle.ToInt64()}", findError);
            }

            findHandle = output.FindHandle;
            var attempts = 0;
            while (attempts < Math.Max(1, maxRecords))
            {
                attempts++;
                var zero = new byte[DahuaNetSdkRecordQueryMapper.DefaultRecordBufferBytes];
                Marshal.Copy(zero, 0, recordBuffer, zero.Length);

                var nextInput = NetInFindNextRecordParam.Create(findHandle, 1);
                var nextOutput = NetOutFindNextRecordParam.Create(recordBuffer, 1);
                var nextResult = _clientFindNextRecord!(ref nextInput, ref nextOutput, 3000);
                var nextError = LastErrorCode;
                if (!nextResult)
                {
                    return new DahuaNetSdkRecordQueryResult(false, $"CLIENT_FindNextRecord failed after {attempts} attempts", nextError, records, diagnostics, attempts, records.Count);
                }

                if (nextOutput.ReturnRecordNum <= 0) break;

                var payload = new byte[DahuaNetSdkRecordQueryMapper.DefaultRecordBufferBytes];
                Marshal.Copy(recordBuffer, payload, 0, payload.Length);
                if (DahuaNetSdkRecordQueryMapper.TryMapAccessControlCardRecord(payload, deviceTimeZone, out var record, out var parseError))
                {
                    diagnostics.Add(new Dictionary<string, string?>(record.RawFields, StringComparer.OrdinalIgnoreCase));
                    if (record.RecNo is not null && record.RecNo > lastRecNo)
                    {
                        records.Add(record);
                    }
                }
                else
                {
                    diagnostics.Add(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["ParseError"] = parseError,
                        ["PayloadFirstBytesHex"] = Convert.ToHexString(payload.Take(128).ToArray()),
                    });
                }
            }

            return new DahuaNetSdkRecordQueryResult(true, null, 0, records, diagnostics, attempts, records.Count);
        }
        catch (Exception ex)
        {
            return DahuaNetSdkRecordQueryResult.Failure(ex.Message, LastErrorCode);
        }
        finally
        {
            if (findHandle != IntPtr.Zero)
            {
                try { _clientFindRecordClose!(findHandle); } catch { }
            }

            Marshal.FreeHGlobal(conditionPtr);
            Marshal.FreeHGlobal(recordBuffer);
        }
    }


    public void StopListenServer(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return;

        try
        {
            _clientStopListenServer(handle);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to stop Dahua NetSDK listen server handle {Handle}", handle);
        }
    }

    public void StopService(IntPtr handle)
    {
        if (handle == IntPtr.Zero || _clientStopService is null) return;

        try
        {
            _clientStopService(handle);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to stop Dahua NetSDK StartService handle {Handle}", handle);
        }
    }

    public void Dispose()
    {
        try
        {
            if (_initialized)
            {
                _clientCleanup();
                _logger.LogInformation("Dahua NetSDK cleanup complete");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dahua NetSDK cleanup failed");
        }

        if (_libraryHandle != IntPtr.Zero)
        {
            NativeLibrary.Free(_libraryHandle);
        }
    }

    private void TrySetAutoReconnect()
    {
        try
        {
            var setAutoReconnect = TryGetDelegate<ClientSetAutoReconnectDelegate>("CLIENT_SetAutoReconnect");
            setAutoReconnect?.Invoke(_reconnectCallback, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dahua NetSDK auto reconnect callback could not be configured");
        }
    }

    private TDelegate GetRequiredDelegate<TDelegate>(string exportName) where TDelegate : Delegate
    {
        var result = TryGetDelegate<TDelegate>(exportName);
        if (result is null)
        {
            throw new MissingMethodException($"Dahua NetSDK export '{exportName}' was not found in native library.");
        }

        return result;
    }

    private TDelegate? TryGetDelegate<TDelegate>(string exportName) where TDelegate : Delegate
    {
        return NativeLibrary.TryGetExport(_libraryHandle, exportName, out var export)
            ? Marshal.GetDelegateForFunctionPointer<TDelegate>(export)
            : null;
    }

    private void OnDisconnect(IntPtr loginId, string deviceIp, int devicePort, IntPtr userData) =>
        _logger.LogWarning("Dahua device disconnected. LoginHandle {LoginHandle}, Remote {RemoteIp}:{RemotePort}", loginId, deviceIp, devicePort);

    private void OnReconnect(IntPtr loginId, string deviceIp, int devicePort, IntPtr userData) =>
        _logger.LogInformation("Dahua device reconnected. LoginHandle {LoginHandle}, Remote {RemoteIp}:{RemotePort}", loginId, deviceIp, devicePort);

    [StructLayout(LayoutKind.Sequential)]
    private struct FindRecordAccessCtlCardRecOrder
    {
        public int Field;
        public int OrderType;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] Reserved;

        public static FindRecordAccessCtlCardRecOrder Empty() => new() { Reserved = new byte[64] };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FindRecordAccessCtlCardRecConditionEx
    {
        public uint Size;
        [MarshalAs(UnmanagedType.Bool)] public bool CardNoEnabled;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] CardNo;
        [MarshalAs(UnmanagedType.Bool)] public bool TimeEnabled;
        public DahuaNetSdkAccessEventDecoder.NetTime StartTime;
        public DahuaNetSdkAccessEventDecoder.NetTime EndTime;
        public int OrderCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public FindRecordAccessCtlCardRecOrder[] Orders;
        [MarshalAs(UnmanagedType.Bool)] public bool RealUtcTimeEnabled;
        public uint StartRealUtcTime;
        public uint EndRealUtcTime;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 40)] public byte[] Reserved;

        public static FindRecordAccessCtlCardRecConditionEx Create() => new()
        {
            Size = (uint)Marshal.SizeOf<FindRecordAccessCtlCardRecConditionEx>(),
            CardNo = new byte[32],
            TimeEnabled = false,
            OrderCount = 0,
            Orders = Enumerable.Range(0, 6).Select(_ => FindRecordAccessCtlCardRecOrder.Empty()).ToArray(),
            Reserved = new byte[40],
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NetInFindRecordParam
    {
        public uint Size;
        public int Type;
        public IntPtr QueryCondition;

        public static NetInFindRecordParam Create(int type, IntPtr queryCondition) => new()
        {
            Size = (uint)Marshal.SizeOf<NetInFindRecordParam>(),
            Type = type,
            QueryCondition = queryCondition,
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NetOutFindRecordParam
    {
        public uint Size;
        public IntPtr FindHandle;

        public static NetOutFindRecordParam Create() => new()
        {
            Size = (uint)Marshal.SizeOf<NetOutFindRecordParam>(),
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NetInFindNextRecordParam
    {
        public uint Size;
        public IntPtr FindHandle;
        public int FileCount;

        public static NetInFindNextRecordParam Create(IntPtr findHandle, int fileCount) => new()
        {
            Size = (uint)Marshal.SizeOf<NetInFindNextRecordParam>(),
            FindHandle = findHandle,
            FileCount = fileCount,
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NetOutFindNextRecordParam
    {
        public uint Size;
        public IntPtr RecordList;
        public int MaxRecordNum;
        public int ReturnRecordNum;

        public static NetOutFindNextRecordParam Create(IntPtr recordList, int maxRecordNum) => new()
        {
            Size = (uint)Marshal.SizeOf<NetOutFindNextRecordParam>(),
            RecordList = recordList,
            MaxRecordNum = maxRecordNum,
        };
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NetDeviceInfo
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 48)] public byte[] SerialNumber;
        public byte AlarmInPortNum;
        public byte AlarmOutPortNum;
        public byte DiskNum;
        public byte DvrType;
        public byte ChanNum;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NetDeviceInfoEx
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 48)] public byte[] SerialNumber;
        public int AlarmInPortNum;
        public int AlarmOutPortNum;
        public int DiskNum;
        public int DvrType;
        public int ChanNum;
        public byte LimitLoginTime;
        public byte LeftLogTimes;
        public ushort ReservedAlignment;
        public int LockLeftTime;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public byte[] Reserved;
        public int NTlsPort;
        public int KeyFrameEncrypt;
        public int Algorithm;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public byte[] Reserved2;

        public static NetDeviceInfoEx Create() => new()
        {
            SerialNumber = new byte[48],
            Reserved = new byte[4],
            Reserved2 = new byte[8],
        };
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct NetInLoginWithHighLevelSecurity
    {
        public uint Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Ip;
        public int Port;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string UserName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Password;
        public int SpecCap;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public byte[] Reserved;
        public IntPtr CapParam;
        public int TlsCap;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string LocalIp;
        public int ClientType;

        public static NetInLoginWithHighLevelSecurity Create(string ip, int port, string username, string password, int specCap, IntPtr capParam) => new()
        {
            Size = (uint)Marshal.SizeOf<NetInLoginWithHighLevelSecurity>(),
            Ip = Truncate(ip, 63),
            Port = port,
            UserName = Truncate(username, 63),
            Password = Truncate(password, 63),
            SpecCap = specCap,
            Reserved = new byte[4],
            CapParam = capParam,
            TlsCap = 0,
            LocalIp = string.Empty,
            ClientType = 0,
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NetOutLoginWithHighLevelSecurity
    {
        public uint Size;
        public NetDeviceInfoEx DeviceInfo;
        public int Error;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 132)] public byte[] Reserved;

        public static NetOutLoginWithHighLevelSecurity Create() => new()
        {
            Size = (uint)Marshal.SizeOf<NetOutLoginWithHighLevelSecurity>(),
            DeviceInfo = NetDeviceInfoEx.Create(),
            Reserved = new byte[132],
        };
    }

    private sealed class CapParamAllocation : IDisposable
    {
        public IntPtr Pointer { get; init; }
        public int Length { get; init; }

        public void Dispose()
        {
            if (Pointer != IntPtr.Zero) Marshal.FreeHGlobal(Pointer);
        }
    }

    private static CapParamAllocation CreateCapParam(string registerDeviceId, string capParamKind)
    {
        if (capParamKind == DahuaActiveRegisterLoginStrategyPlan.CapNull)
        {
            return new CapParamAllocation { Pointer = IntPtr.Zero, Length = 0 };
        }

        var bytes = System.Text.Encoding.ASCII.GetBytes(registerDeviceId);
        var includeNull = capParamKind == DahuaActiveRegisterLoginStrategyPlan.CapNullTerminatedRegisterId;
        var length = bytes.Length + (includeNull ? 1 : 0);
        var pointer = Marshal.AllocHGlobal(length);
        Marshal.Copy(bytes, 0, pointer, bytes.Length);
        if (includeNull) Marshal.WriteByte(pointer, bytes.Length, 0);
        return new CapParamAllocation { Pointer = pointer, Length = length };
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int ServiceCallback(IntPtr listenHandle, string deviceIp, ushort devicePort, int command, IntPtr param, uint paramLength, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool AlarmMessageCallback(int command, IntPtr loginHandle, IntPtr payload, uint payloadLength, string deviceIp, int devicePort, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool ClientInitDelegate(DisconnectCallback disconnectCallback, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ClientCleanupDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr ClientListenServerDelegate(string ip, ushort port, int timeout, ServiceCallback callback, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool ClientStopListenServerDelegate(IntPtr listenHandle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr ClientStartServiceDelegate(ushort port, [MarshalAs(UnmanagedType.LPStr)] string? ip, ServiceCallback callback, uint timeout, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool ClientStopServiceDelegate(IntPtr handle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ClientGetLastErrorDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ClientSetAutoReconnectDelegate(ReconnectCallback reconnectCallback, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ClientSetDvrMessageCallbackDelegate(AlarmMessageCallback callback, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr ClientLoginExDelegate(
        [MarshalAs(UnmanagedType.LPStr)] string? ip,
        ushort port,
        [MarshalAs(UnmanagedType.LPStr)] string username,
        [MarshalAs(UnmanagedType.LPStr)] string password,
        int specCap,
        IntPtr capParam,
        ref NetDeviceInfo deviceInfo,
        ref int error);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr ClientLoginEx2Delegate(
        [MarshalAs(UnmanagedType.LPStr)] string? ip,
        ushort port,
        [MarshalAs(UnmanagedType.LPStr)] string username,
        [MarshalAs(UnmanagedType.LPStr)] string password,
        int specCap,
        IntPtr capParam,
        ref NetDeviceInfoEx deviceInfo,
        ref int error);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr ClientLoginWithHighLevelSecurityDelegate(
        ref NetInLoginWithHighLevelSecurity input,
        ref NetOutLoginWithHighLevelSecurity output);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool ClientLogoutDelegate(IntPtr loginHandle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool ClientResponseDevRegDelegate(
        [MarshalAs(UnmanagedType.LPStr)] string deviceSerial,
        [MarshalAs(UnmanagedType.LPStr)] string ip,
        ushort port,
        [MarshalAs(UnmanagedType.Bool)] bool accept);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool ClientStartListenExDelegate(IntPtr loginHandle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool ClientStopListenDelegate(IntPtr loginHandle);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool ClientFindRecordDelegate(IntPtr loginHandle, ref NetInFindRecordParam input, ref NetOutFindRecordParam output, int waitTime);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool ClientFindNextRecordDelegate(ref NetInFindNextRecordParam input, ref NetOutFindNextRecordParam output, int waitTime);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool ClientFindRecordCloseDelegate(IntPtr findHandle);


    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void DisconnectCallback(IntPtr loginId, string deviceIp, int devicePort, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ReconnectCallback(IntPtr loginId, string deviceIp, int devicePort, IntPtr userData);
}

internal sealed record DahuaActiveRegisterLoginAttempt(
    string Strategy,
    string LoginApi,
    string IpArgument,
    int PortArgument,
    int SpecCap,
    string CapParamKind,
    string RegisterDeviceId,
    bool UsernamePresent,
    bool PasswordPresent,
    int PasswordLength,
    int CapParamStringLength,
    long LoginHandle,
    int NativeErrorPointer,
    int LastErrorAfterCall,
    bool UsesLoginEx2,
    bool PossibleMarshallingWarning)
{
    public bool Succeeded => LoginHandle != 0;

    public static DahuaActiveRegisterLoginAttempt Create(
        DahuaActiveRegisterLoginStrategy strategy,
        int specCap,
        string registerDeviceId,
        string? username,
        string? password,
        IntPtr loginHandle,
        int nativeErrorPointer,
        int lastErrorAfterCall)
    {
        var handleValue = loginHandle.ToInt64();
        var possibleMarshallingWarning = DahuaActiveRegisterLoginDiagnostics.IsPossibleMarshallingWarning(handleValue, nativeErrorPointer, lastErrorAfterCall);
        return new DahuaActiveRegisterLoginAttempt(
            strategy.Name,
            strategy.LoginApi,
            strategy.IpArgument,
            strategy.PortArgument,
            specCap,
            strategy.CapParamKind,
            registerDeviceId,
            !string.IsNullOrWhiteSpace(username),
            !string.IsNullOrEmpty(password),
            password?.Length ?? 0,
            strategy.CapParamKind == DahuaActiveRegisterLoginStrategyPlan.CapNull ? 0 : registerDeviceId.Length + (strategy.CapParamKind == DahuaActiveRegisterLoginStrategyPlan.CapNullTerminatedRegisterId ? 1 : 0),
            handleValue,
            nativeErrorPointer,
            lastErrorAfterCall,
            strategy.UsesLoginEx2,
            possibleMarshallingWarning);
    }
}

internal sealed record DahuaNetSdkRecordQueryResult(
    bool Success,
    string? Error,
    int ErrorCode,
    IReadOnlyList<DahuaAccessRecord> Records,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Diagnostics,
    int Attempts,
    int CandidateCount)
{
    public static DahuaNetSdkRecordQueryResult Failure(string error, int errorCode) => new(false, error, errorCode, [], [], 0, 0);
}
