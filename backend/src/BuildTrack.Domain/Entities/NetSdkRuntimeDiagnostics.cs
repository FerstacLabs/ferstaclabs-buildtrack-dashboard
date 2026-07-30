namespace BuildTrack.Domain.Entities;

public sealed class NetSdkRuntimeDiagnostics
{
    public string Id { get; set; } = "dahua-netsdk-runtime";
    public bool SdkLoaded { get; set; }
    public bool SdkInitialized { get; set; }
    public string ListenerPortsJson { get; set; } = "[]";
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
    public bool SmartEventEnabled { get; set; }
    public bool SmartEventNeedPicture { get; set; }
    public int SmartEventChannel { get; set; } = -1;
    public bool SmartEventSubscriptionAttempted { get; set; }
    public bool? SmartEventSubscriptionSuccess { get; set; }
    public long? SmartEventAttachHandle { get; set; }
    public int? SmartEventErrorSigned { get; set; }
    public string? SmartEventErrorHex { get; set; }
    public int SmartEventSubscriptionGeneration { get; set; }
    public DateTimeOffset? SmartEventSubscribedAt { get; set; }
    public string? SmartEventRemoteIp { get; set; }
    public int? SmartEventRemotePort { get; set; }
    public DateTimeOffset? LastServiceCallbackAt { get; set; }
    public DateTimeOffset? LastSmartEventAt { get; set; }
    public DateTimeOffset? LastSmartEventResubscribeAt { get; set; }
    public string? LastSmartEventResubscribeReason { get; set; }
    public bool? LastSmartEventResubscribeSuccess { get; set; }
    public string? LastSmartEventResubscribeError { get; set; }
    public bool StaleSmartEventDetected { get; set; }
    public bool SmartEventWatchdogEnabled { get; set; }
    public int? LastSmartEventType { get; set; }
    public string? LastSmartEventName { get; set; }
    public int LastSmartEventPayloadBytes { get; set; }
    public int LastSmartEventImageBytesLength { get; set; }
    public string? LastSmartEventParseStatus { get; set; }
    public string? LastSmartEventUserId { get; set; }
    public string? LastSmartEventCardName { get; set; }
    public long? LastSmartEventRecNo { get; set; }
    public DateTimeOffset? LastSmartEventTime { get; set; }
    public string? LastSmartEventRawStructSummaryJson { get; set; }
    public bool NetSdkRecordQueryEnabled { get; set; }
    public bool NetSdkRecordQueryDiagnosticMode { get; set; }
    public DateTimeOffset? LastRecordQueryAt { get; set; }
    public bool? LastRecordQuerySuccess { get; set; }
    public string? LastRecordQueryError { get; set; }
    public int LastRecordQueryCount { get; set; }
    public long? LastRecordQueryLastRecNo { get; set; }
    public string? LastDecodeError { get; set; }
    public string NetSdkDecodeStatus { get; set; } = "MissingSdk";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}


