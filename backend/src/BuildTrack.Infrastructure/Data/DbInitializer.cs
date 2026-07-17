using Microsoft.EntityFrameworkCore;

namespace BuildTrack.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task EnsureDatabaseAsync(BuildTrackDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS attendance_sessions (
    "Id" uuid NOT NULL PRIMARY KEY,
    "SiteId" uuid NOT NULL REFERENCES sites("Id") ON DELETE CASCADE,
    "DeviceId" uuid NOT NULL REFERENCES devices("Id") ON DELETE CASCADE,
    "WorkerId" uuid NULL REFERENCES workers("Id") ON DELETE SET NULL,
    "WorkerExternalId" character varying(80) NOT NULL,
    "WorkerName" character varying(180) NULL,
    "WorkDate" date NOT NULL,
    "CheckInEventId" uuid NOT NULL REFERENCES attendance_events("Id") ON DELETE RESTRICT,
    "CheckInTime" timestamp with time zone NOT NULL,
    "CheckOutEventId" uuid NULL REFERENCES attendance_events("Id") ON DELETE RESTRICT,
    "CheckOutTime" timestamp with time zone NULL,
    "LastSeenEventId" uuid NULL REFERENCES attendance_events("Id") ON DELETE RESTRICT,
    "LastSeenTime" timestamp with time zone NULL,
    "CloseReason" character varying(50) NULL,
    "PresenceStatus" character varying(50) NULL,
    "Status" character varying(30) NOT NULL,
    "Source" character varying(80) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NULL
);
CREATE INDEX IF NOT EXISTS "IX_attendance_sessions_SiteId_WorkDate" ON attendance_sessions ("SiteId", "WorkDate");
CREATE INDEX IF NOT EXISTS "IX_attendance_sessions_DeviceId_WorkerExternalId_WorkDate" ON attendance_sessions ("DeviceId", "WorkerExternalId", "WorkDate");
CREATE INDEX IF NOT EXISTS "IX_attendance_sessions_WorkerExternalId_WorkDate" ON attendance_sessions ("WorkerExternalId", "WorkDate");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_attendance_sessions_Open_Device_Worker_Date" ON attendance_sessions ("DeviceId", "WorkerExternalId", "WorkDate", "Status") WHERE "Status" = 'Open';
DO $$
BEGIN
    CREATE UNIQUE INDEX IF NOT EXISTS "IX_AttendanceSessions_DailyUnique"
    ON attendance_sessions ("SiteId", "DeviceId", "WorkerExternalId", "WorkDate");
EXCEPTION WHEN unique_violation THEN
    RAISE NOTICE 'Skipped IX_AttendanceSessions_DailyUnique because duplicate historical sessions exist.';
END $$;
ALTER TABLE devices ADD COLUMN IF NOT EXISTS "CgiLastRecNo" bigint NULL;
ALTER TABLE attendance_sessions ADD COLUMN IF NOT EXISTS "LastSeenEventId" uuid NULL;
ALTER TABLE attendance_sessions ADD COLUMN IF NOT EXISTS "LastSeenTime" timestamp with time zone NULL;
ALTER TABLE attendance_sessions ADD COLUMN IF NOT EXISTS "CloseReason" character varying(50) NULL;
ALTER TABLE attendance_sessions ADD COLUMN IF NOT EXISTS "PresenceStatus" character varying(50) NULL;
CREATE TABLE IF NOT EXISTS security_events (
    "Id" uuid NOT NULL PRIMARY KEY,
    "SiteId" uuid NOT NULL REFERENCES sites("Id") ON DELETE CASCADE,
    "DeviceId" uuid NOT NULL REFERENCES devices("Id") ON DELETE CASCADE,
    "EventTime" timestamp with time zone NOT NULL,
    "EventDate" date NOT NULL,
    "EventType" character varying(50) NOT NULL,
    "Severity" character varying(30) NOT NULL,
    "Status" character varying(30) NOT NULL,
    "RawRecNo" bigint NULL,
    "Method" character varying(40) NULL,
    "Direction" character varying(30) NULL,
    "SnapshotPath" character varying(500) NULL,
    "SnapshotUrl" character varying(1000) NULL,
    "StoredSnapshotPath" character varying(500) NULL,
    "StoredSnapshotContentType" character varying(80) NULL,
    "SnapshotDownloadStatus" character varying(40) NULL,
    "SnapshotDownloadError" character varying(500) NULL,
    "SnapshotSource" character varying(80) NULL,
    "ErrorCode" character varying(50) NULL,
    "Message" character varying(300) NULL,
    "Source" character varying(80) NOT NULL,
    "RawPayloadJson" jsonb NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "ReviewedAt" timestamp with time zone NULL,
    "ReviewNote" character varying(500) NULL
);
CREATE INDEX IF NOT EXISTS "IX_security_events_SiteId_EventDate" ON security_events ("SiteId", "EventDate");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_security_events_DeviceId_RawRecNo" ON security_events ("DeviceId", "RawRecNo") WHERE "RawRecNo" IS NOT NULL;
CREATE INDEX IF NOT EXISTS "IX_security_events_Status" ON security_events ("Status");
CREATE INDEX IF NOT EXISTS "IX_security_events_EventType" ON security_events ("EventType");
ALTER TABLE security_events ADD COLUMN IF NOT EXISTS "StoredSnapshotPath" character varying(500) NULL;
ALTER TABLE security_events ADD COLUMN IF NOT EXISTS "StoredSnapshotContentType" character varying(80) NULL;
ALTER TABLE security_events ADD COLUMN IF NOT EXISTS "SnapshotDownloadStatus" character varying(40) NULL;
ALTER TABLE security_events ADD COLUMN IF NOT EXISTS "SnapshotDownloadError" character varying(500) NULL;
ALTER TABLE security_events ADD COLUMN IF NOT EXISTS "SnapshotSource" character varying(80) NULL;CREATE TABLE IF NOT EXISTS dahua_active_register_raw_events (
    "Id" uuid NOT NULL PRIMARY KEY,
    "DeviceId" uuid NULL REFERENCES devices("Id") ON DELETE SET NULL,
    "RegisterDeviceId" character varying(160) NULL,
    "RemoteIp" character varying(80) NULL,
    "RemotePort" integer NULL,
    "ListenerPort" integer NOT NULL,
    "CallbackCommand" integer NOT NULL,
    "CallbackCommandName" character varying(120) NULL,
    "PayloadBytes" integer NOT NULL,
    "PayloadFirstBytesHex" character varying(512) NULL,
    "PayloadBase64" text NULL,
    "DecodeStatus" character varying(80) NOT NULL,
    "DecodedJson" jsonb NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);
CREATE INDEX IF NOT EXISTS "IX_dahua_active_register_raw_events_CreatedAt" ON dahua_active_register_raw_events ("CreatedAt");
CREATE INDEX IF NOT EXISTS "IX_dahua_active_register_raw_events_CallbackCommand" ON dahua_active_register_raw_events ("CallbackCommand");
CREATE INDEX IF NOT EXISTS "IX_dahua_active_register_raw_events_DecodeStatus" ON dahua_active_register_raw_events ("DecodeStatus");
CREATE TABLE IF NOT EXISTS netsdk_runtime_diagnostics (
    "Id" character varying(80) NOT NULL PRIMARY KEY,
    "SdkLoaded" boolean NOT NULL DEFAULT false,
    "SdkInitialized" boolean NOT NULL DEFAULT false,
    "ListenerPortsJson" jsonb NOT NULL DEFAULT '[]'::jsonb,
    "AlarmCallbackConfigured" boolean NOT NULL DEFAULT false,
    "ActiveRegisterServiceMode" character varying(80) NOT NULL DEFAULT 'ListenServer',
    "ExperimentalStartServiceEnabled" boolean NOT NULL DEFAULT false,
    "ExperimentalStartServiceStarted" boolean NOT NULL DEFAULT false,
    "ExperimentalStartServiceHandle" bigint NULL,
    "ExperimentalStartServiceLastCommand" integer NULL,
    "ExperimentalStartServiceLastPayloadBytes" integer NOT NULL DEFAULT 0,
    "ExperimentalStartServiceLastDecodeStatus" character varying(1000) NULL,
    "ExperimentalStartServiceErrorSigned" integer NULL,
    "ExperimentalStartServiceErrorHex" character varying(40) NULL,
    "LastServiceCommand" integer NULL,
    "LastServiceEventType" character varying(120) NULL,
    "LastServicePayloadBytes" integer NOT NULL DEFAULT 0,
    "LastRegisterDeviceId" character varying(160) NULL,
    "ResponseDevRegCalled" boolean NOT NULL DEFAULT false,
    "ResponseDevRegSuccess" boolean NULL,
    "ResponseDevRegErrorSigned" integer NULL,
    "ResponseDevRegErrorHex" character varying(40) NULL,
    "ResponseDevRegDevSerial" character varying(160) NULL,
    "ResponseDevRegDevSerialLength" integer NULL,
    "ResponseDevRegIp" character varying(80) NULL,
    "ResponseDevRegPort" integer NULL,
    "ResponseDevRegAccept" boolean NULL,
    "ResponseDevRegCommandSource" character varying(120) NULL,
    "LastServiceCallbackHandle" bigint NULL,
    "LastServiceCallbackHandleNonZero" boolean NOT NULL DEFAULT false,
    "ActiveRegisterSessionHandleFound" boolean NOT NULL DEFAULT false,
    "ActiveRegisterSessionHandleValueNonZero" boolean NOT NULL DEFAULT false,
    "ActiveRegisterSessionHandleValue" bigint NULL,
    "ActiveRegisterSessionHandleSource" character varying(120) NULL,
    "ActiveRegisterSessionHandleStrategyResult" character varying(80) NULL,
    "LoginStrategy" character varying(120) NULL,
    "LoginHandle" bigint NULL,
    "LoginSucceeded" boolean NULL,
    "LoginErrorSigned" integer NULL,
    "LoginErrorHex" character varying(40) NULL,
    "LoginNativeErrorSigned" integer NULL,
    "LoginNativeErrorHex" character varying(40) NULL,
    "LoginPossibleMarshallingWarning" boolean NOT NULL DEFAULT false,
    "StartListenExCalled" boolean NOT NULL DEFAULT false,
    "StartListenExSuccess" boolean NULL,
    "StartListenExErrorSigned" integer NULL,
    "StartListenExErrorHex" character varying(40) NULL,
    "LastAlarmCommand" integer NULL,
    "LastDecodeError" character varying(1000) NULL,
    "NetSdkDecodeStatus" character varying(80) NOT NULL DEFAULT 'MissingSdk',
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT now()
);
ALTER TABLE dahua_active_register_raw_events ALTER COLUMN "PayloadFirstBytesHex" TYPE character varying(512);
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastServicePayloadFirst256Hex" character varying(512) NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastParsedRegisterDeviceIdOffset" integer NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastParsedRegisterDeviceId" character varying(160) NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastParsedSerialOffset" integer NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastParsedSerial" character varying(160) NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastParsedRemoteIp" character varying(80) NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastParsedRemotePort" integer NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastPossibleSessionHandlesJson" jsonb NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastPayloadStructLayout" character varying(1000) NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "ExperimentalServiceHandleSubscribeEnabled" boolean NOT NULL DEFAULT false;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastExperimentalSubscribeJson" jsonb NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "ResponseDevRegDevSerial" character varying(160) NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "ResponseDevRegDevSerialLength" integer NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "ResponseDevRegIp" character varying(80) NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "ResponseDevRegPort" integer NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "ResponseDevRegAccept" boolean NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "ResponseDevRegCommandSource" character varying(120) NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastServiceCallbackHandle" bigint NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastServiceCallbackHandleNonZero" boolean NOT NULL DEFAULT false;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "ActiveRegisterSessionHandleSource" character varying(120) NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "ActiveRegisterSessionHandleStrategyResult" character varying(80) NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LoginStrategy" character varying(120) NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LoginHandle" bigint NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LoginSucceeded" boolean NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LoginErrorSigned" integer NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LoginErrorHex" character varying(40) NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LoginNativeErrorSigned" integer NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LoginNativeErrorHex" character varying(40) NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LoginPossibleMarshallingWarning" boolean NOT NULL DEFAULT false;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "ActiveRegisterServiceMode" character varying(80) NOT NULL DEFAULT 'ListenServer';
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "ExperimentalStartServiceEnabled" boolean NOT NULL DEFAULT false;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "ExperimentalStartServiceStarted" boolean NOT NULL DEFAULT false;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "ExperimentalStartServiceHandle" bigint NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "ExperimentalStartServiceLastCommand" integer NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "ExperimentalStartServiceLastPayloadBytes" integer NOT NULL DEFAULT 0;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "ExperimentalStartServiceLastDecodeStatus" character varying(1000) NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "ExperimentalStartServiceErrorSigned" integer NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "ExperimentalStartServiceErrorHex" character varying(40) NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastAlarmCommandName" character varying(120) NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastAlarmPayloadFirst256Hex" character varying(512) NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastAlarmDecodeStatus" character varying(120) NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastDecodedAlarmJson" jsonb NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "NetSdkRecordQueryEnabled" boolean NOT NULL DEFAULT false;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastRecordQueryAt" timestamp with time zone NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastRecordQuerySuccess" boolean NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastRecordQueryError" character varying(1000) NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastRecordQueryCount" integer NOT NULL DEFAULT 0;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastRecordQueryLastRecNo" bigint NULL;
""", cancellationToken);
    }
}






















