using Microsoft.EntityFrameworkCore;

using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Security;
using Microsoft.Extensions.Configuration;

namespace BuildTrack.Infrastructure.Data;

public static class DbInitializer
{
    public static readonly Guid DemoTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static async Task EnsureDatabaseAsync(BuildTrackDbContext db, IConfiguration? configuration = null, CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS tenants (
    "Id" uuid NOT NULL PRIMARY KEY,
    "CompanyName" character varying(180) NOT NULL,
    "Code" character varying(60) NOT NULL UNIQUE,
    "Status" character varying(40) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NULL
);
CREATE TABLE IF NOT EXISTS users (
    "Id" uuid NOT NULL PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id") ON DELETE CASCADE,
    "FullName" character varying(180) NOT NULL,
    "Email" character varying(180) NOT NULL UNIQUE,
    "Phone" character varying(60) NULL,
    "PasswordHash" character varying(500) NOT NULL,
    "Role" character varying(40) NOT NULL,
    "Status" character varying(40) NOT NULL,
    "LastLoginAt" timestamp with time zone NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NULL
);
ALTER TABLE users ADD COLUMN IF NOT EXISTS "Phone" character varying(60) NULL;
ALTER TABLE users ADD COLUMN IF NOT EXISTS "LastLoginAt" timestamp with time zone NULL;
CREATE TABLE IF NOT EXISTS licenses (
    "Id" uuid NOT NULL PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id") ON DELETE CASCADE,
    "LicenseKeyHash" character varying(128) NOT NULL UNIQUE,
    "Plan" character varying(40) NOT NULL,
    "Status" character varying(40) NOT NULL,
    "StartsAt" timestamp with time zone NOT NULL,
    "ExpiresAt" timestamp with time zone NULL,
    "MaxProjects" integer NULL,
    "MaxUsers" integer NULL,
    "MaxCameras" integer NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "ActivatedAt" timestamp with time zone NULL
);
INSERT INTO tenants ("Id", "CompanyName", "Code", "Status", "CreatedAt", "UpdatedAt")
VALUES ('11111111-1111-1111-1111-111111111111', 'FerstacLabs Demo', 'DEMO', 'Active', now(), now())
ON CONFLICT ("Code") DO UPDATE SET "CompanyName" = EXCLUDED."CompanyName", "Status" = 'Active', "UpdatedAt" = now();
INSERT INTO licenses ("Id", "TenantId", "LicenseKeyHash", "Plan", "Status", "StartsAt", "ExpiresAt", "MaxProjects", "MaxUsers", "MaxCameras", "CreatedAt", "ActivatedAt")
VALUES ('22222222-2222-2222-2222-222222222222', '11111111-1111-1111-1111-111111111111', '8247afc8bcc58ca96cb30987d1417cceccb8371f799498f30eaf69a83a7c1db0', 'Unlimited', 'Active', now(), NULL, NULL, NULL, NULL, now(), now())
ON CONFLICT ("LicenseKeyHash") DO UPDATE SET "TenantId" = EXCLUDED."TenantId", "Plan" = 'Unlimited', "Status" = 'Active', "ExpiresAt" = NULL, "ActivatedAt" = COALESCE(licenses."ActivatedAt", now());
ALTER TABLE sites ADD COLUMN IF NOT EXISTS "TenantId" uuid NULL;
ALTER TABLE workers ADD COLUMN IF NOT EXISTS "TenantId" uuid NULL;
ALTER TABLE workers ADD COLUMN IF NOT EXISTS "Brigade" character varying(120) NULL;
ALTER TABLE workers ADD COLUMN IF NOT EXISTS "Role" character varying(120) NULL;
ALTER TABLE workers ADD COLUMN IF NOT EXISTS "HourlyRate" numeric(18,2) NOT NULL DEFAULT 0;
ALTER TABLE workers ADD COLUMN IF NOT EXISTS "PlannedDailyHours" numeric(8,2) NOT NULL DEFAULT 8;
ALTER TABLE workers ADD COLUMN IF NOT EXISTS "AttendanceSource" character varying(40) NOT NULL DEFAULT 'Manual';
ALTER TABLE workers ADD COLUMN IF NOT EXISTS "RiskScore" integer NOT NULL DEFAULT 0;
ALTER TABLE workers ADD COLUMN IF NOT EXISTS "Notes" character varying(500) NULL;
ALTER TABLE workers ADD COLUMN IF NOT EXISTS "UpdatedAt" timestamp with time zone NULL;
ALTER TABLE devices ADD COLUMN IF NOT EXISTS "TenantId" uuid NULL;
ALTER TABLE attendance_events ADD COLUMN IF NOT EXISTS "TenantId" uuid NULL;
ALTER TABLE device_connection_logs ADD COLUMN IF NOT EXISTS "TenantId" uuid NULL;
UPDATE sites SET "TenantId" = '11111111-1111-1111-1111-111111111111' WHERE "TenantId" IS NULL;
UPDATE workers SET "TenantId" = COALESCE((SELECT s."TenantId" FROM sites s WHERE s."Id" = workers."SiteId"), '11111111-1111-1111-1111-111111111111') WHERE "TenantId" IS NULL;
UPDATE devices SET "TenantId" = COALESCE((SELECT s."TenantId" FROM sites s WHERE s."Id" = devices."SiteId"), '11111111-1111-1111-1111-111111111111') WHERE "TenantId" IS NULL;
UPDATE attendance_events SET "TenantId" = COALESCE((SELECT d."TenantId" FROM devices d WHERE d."Id" = attendance_events."DeviceId"), '11111111-1111-1111-1111-111111111111') WHERE "TenantId" IS NULL;
UPDATE device_connection_logs SET "TenantId" = (SELECT d."TenantId" FROM devices d WHERE d."Id" = device_connection_logs."DeviceId") WHERE "TenantId" IS NULL AND "DeviceId" IS NOT NULL;
ALTER TABLE sites ALTER COLUMN "TenantId" SET NOT NULL;
ALTER TABLE workers ALTER COLUMN "TenantId" SET NOT NULL;
ALTER TABLE devices ALTER COLUMN "TenantId" SET NOT NULL;
ALTER TABLE attendance_events ALTER COLUMN "TenantId" SET NOT NULL;
CREATE INDEX IF NOT EXISTS "IX_users_TenantId" ON users ("TenantId");
CREATE INDEX IF NOT EXISTS "IX_licenses_TenantId" ON licenses ("TenantId");
CREATE INDEX IF NOT EXISTS "IX_sites_TenantId" ON sites ("TenantId");
CREATE INDEX IF NOT EXISTS "IX_workers_TenantId" ON workers ("TenantId");
CREATE INDEX IF NOT EXISTS "IX_devices_TenantId" ON devices ("TenantId");
CREATE INDEX IF NOT EXISTS "IX_attendance_events_TenantId" ON attendance_events ("TenantId");
CREATE INDEX IF NOT EXISTS "IX_device_connection_logs_TenantId" ON device_connection_logs ("TenantId");
CREATE TABLE IF NOT EXISTS worker_camera_identities (
    "Id" uuid NOT NULL PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id") ON DELETE CASCADE,
    "WorkerId" uuid NOT NULL REFERENCES workers("Id") ON DELETE CASCADE,
    "DeviceId" uuid NULL REFERENCES devices("Id") ON DELETE SET NULL,
    "Vendor" character varying(60) NOT NULL DEFAULT 'Dahua',
    "ExternalUserId" character varying(80) NULL,
    "CardName" character varying(180) NULL,
    "NormalizedCardName" character varying(180) NULL,
    "IsPrimary" boolean NOT NULL DEFAULT true,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NULL
);
CREATE INDEX IF NOT EXISTS "IX_worker_camera_identities_TenantId" ON worker_camera_identities ("TenantId");
CREATE INDEX IF NOT EXISTS "IX_worker_camera_identities_WorkerId" ON worker_camera_identities ("WorkerId");
CREATE INDEX IF NOT EXISTS "IX_worker_camera_identities_DeviceId" ON worker_camera_identities ("DeviceId");
CREATE UNIQUE INDEX IF NOT EXISTS "UX_worker_camera_identities_cardname"
ON worker_camera_identities ("TenantId", COALESCE("DeviceId", '00000000-0000-0000-0000-000000000000'::uuid), "NormalizedCardName")
WHERE "NormalizedCardName" IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS "UX_worker_camera_identities_external_user"
ON worker_camera_identities ("TenantId", COALESCE("DeviceId", '00000000-0000-0000-0000-000000000000'::uuid), "ExternalUserId")
WHERE "ExternalUserId" IS NOT NULL;
CREATE TABLE IF NOT EXISTS worker_site_assignments (
    "Id" uuid NOT NULL PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id") ON DELETE CASCADE,
    "WorkerId" uuid NOT NULL REFERENCES workers("Id") ON DELETE CASCADE,
    "SiteId" uuid NOT NULL REFERENCES sites("Id") ON DELETE CASCADE,
    "IsPrimary" boolean NOT NULL DEFAULT false,
    "Status" character varying(40) NOT NULL DEFAULT 'Active',
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NULL
);
CREATE INDEX IF NOT EXISTS "IX_worker_site_assignments_TenantId" ON worker_site_assignments ("TenantId");
CREATE INDEX IF NOT EXISTS "IX_worker_site_assignments_WorkerId" ON worker_site_assignments ("WorkerId");
CREATE INDEX IF NOT EXISTS "IX_worker_site_assignments_SiteId" ON worker_site_assignments ("SiteId");
CREATE UNIQUE INDEX IF NOT EXISTS "UX_worker_site_assignments_active_site"
ON worker_site_assignments ("TenantId", "WorkerId", "SiteId", "Status")
WHERE "Status" = 'Active';
INSERT INTO worker_site_assignments ("Id", "TenantId", "WorkerId", "SiteId", "IsPrimary", "Status", "CreatedAt", "UpdatedAt")
SELECT w."Id", w."TenantId", w."Id", w."SiteId", true, 'Active', now(), now()
FROM workers w
WHERE NOT EXISTS (
    SELECT 1
    FROM worker_site_assignments a
    WHERE a."TenantId" = w."TenantId"
      AND a."WorkerId" = w."Id"
      AND a."SiteId" = w."SiteId"
      AND a."Status" = 'Active'
);
CREATE TABLE IF NOT EXISTS attendance_sessions (
    "Id" uuid NOT NULL PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id") ON DELETE CASCADE,
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
ALTER TABLE attendance_sessions ADD COLUMN IF NOT EXISTS "TenantId" uuid NULL;
ALTER TABLE attendance_sessions ADD COLUMN IF NOT EXISTS "LastSeenEventId" uuid NULL;
ALTER TABLE attendance_sessions ADD COLUMN IF NOT EXISTS "LastSeenTime" timestamp with time zone NULL;
ALTER TABLE attendance_sessions ADD COLUMN IF NOT EXISTS "CloseReason" character varying(50) NULL;
ALTER TABLE attendance_sessions ADD COLUMN IF NOT EXISTS "PresenceStatus" character varying(50) NULL;
UPDATE attendance_sessions SET "TenantId" = COALESCE((SELECT d."TenantId" FROM devices d WHERE d."Id" = attendance_sessions."DeviceId"), '11111111-1111-1111-1111-111111111111') WHERE "TenantId" IS NULL;
ALTER TABLE attendance_sessions ALTER COLUMN "TenantId" SET NOT NULL;
CREATE INDEX IF NOT EXISTS "IX_attendance_sessions_TenantId" ON attendance_sessions ("TenantId");
CREATE TABLE IF NOT EXISTS security_events (
    "Id" uuid NOT NULL PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id") ON DELETE CASCADE,
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
ALTER TABLE security_events ADD COLUMN IF NOT EXISTS "SnapshotSource" character varying(80) NULL;
ALTER TABLE security_events ADD COLUMN IF NOT EXISTS "TenantId" uuid NULL;
UPDATE security_events SET "TenantId" = COALESCE((SELECT d."TenantId" FROM devices d WHERE d."Id" = security_events."DeviceId"), '11111111-1111-1111-1111-111111111111') WHERE "TenantId" IS NULL;
ALTER TABLE security_events ALTER COLUMN "TenantId" SET NOT NULL;
CREATE INDEX IF NOT EXISTS "IX_security_events_TenantId" ON security_events ("TenantId");
CREATE TABLE IF NOT EXISTS dahua_active_register_raw_events (
    "Id" uuid NOT NULL PRIMARY KEY,
    "TenantId" uuid NULL REFERENCES tenants("Id") ON DELETE SET NULL,
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
ALTER TABLE dahua_active_register_raw_events ADD COLUMN IF NOT EXISTS "TenantId" uuid NULL;
UPDATE dahua_active_register_raw_events SET "TenantId" = (SELECT d."TenantId" FROM devices d WHERE d."Id" = dahua_active_register_raw_events."DeviceId") WHERE "TenantId" IS NULL AND "DeviceId" IS NOT NULL;
CREATE INDEX IF NOT EXISTS "IX_dahua_active_register_raw_events_TenantId" ON dahua_active_register_raw_events ("TenantId");
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
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "SmartEventEnabled" boolean NOT NULL DEFAULT false;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "SmartEventNeedPicture" boolean NOT NULL DEFAULT true;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "SmartEventChannel" integer NOT NULL DEFAULT -1;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "SmartEventSubscriptionAttempted" boolean NOT NULL DEFAULT false;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "SmartEventSubscriptionSuccess" boolean NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "SmartEventAttachHandle" bigint NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "SmartEventErrorSigned" integer NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "SmartEventErrorHex" character varying(40) NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "SmartEventSubscriptionGeneration" integer NOT NULL DEFAULT 0;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "SmartEventSubscribedAt" timestamp with time zone NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "SmartEventRemoteIp" character varying(80) NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "SmartEventRemotePort" integer NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastServiceCallbackAt" timestamp with time zone NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastSmartEventAt" timestamp with time zone NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastSmartEventResubscribeAt" timestamp with time zone NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastSmartEventResubscribeReason" character varying(160) NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastSmartEventResubscribeSuccess" boolean NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastSmartEventResubscribeError" character varying(500) NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "StaleSmartEventDetected" boolean NOT NULL DEFAULT false;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "SmartEventWatchdogEnabled" boolean NOT NULL DEFAULT false;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastSmartEventType" integer NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastSmartEventName" character varying(120) NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastSmartEventPayloadBytes" integer NOT NULL DEFAULT 0;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastSmartEventImageBytesLength" integer NOT NULL DEFAULT 0;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastSmartEventParseStatus" character varying(120) NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastSmartEventUserId" character varying(80) NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastSmartEventCardName" character varying(180) NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastSmartEventRecNo" bigint NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastSmartEventTime" timestamp with time zone NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastSmartEventRawStructSummaryJson" jsonb NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "NetSdkRecordQueryEnabled" boolean NOT NULL DEFAULT false;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "NetSdkRecordQueryDiagnosticMode" boolean NOT NULL DEFAULT false;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastRecordQueryAt" timestamp with time zone NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastRecordQuerySuccess" boolean NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastRecordQueryError" character varying(1000) NULL;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastRecordQueryCount" integer NOT NULL DEFAULT 0;
ALTER TABLE netsdk_runtime_diagnostics ADD COLUMN IF NOT EXISTS "LastRecordQueryLastRecNo" bigint NULL;
CREATE TABLE IF NOT EXISTS supervisor_site_assignments (
    "Id" uuid NOT NULL PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id") ON DELETE CASCADE,
    "SupervisorUserId" uuid NOT NULL REFERENCES users("Id") ON DELETE CASCADE,
    "ProjectId" uuid NULL,
    "SiteId" uuid NOT NULL REFERENCES sites("Id") ON DELETE CASCADE,
    "IsActive" boolean NOT NULL DEFAULT true,
    "Notes" character varying(500) NULL,
    "ValidFrom" timestamp with time zone NULL,
    "ValidUntil" timestamp with time zone NULL,
    "CreatedByUserId" uuid NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NULL
);
CREATE INDEX IF NOT EXISTS "IX_supervisor_site_assignments_TenantId" ON supervisor_site_assignments ("TenantId");
CREATE INDEX IF NOT EXISTS "IX_supervisor_site_assignments_SupervisorUserId" ON supervisor_site_assignments ("SupervisorUserId");
CREATE INDEX IF NOT EXISTS "IX_supervisor_site_assignments_SiteId" ON supervisor_site_assignments ("SiteId");
CREATE INDEX IF NOT EXISTS "IX_supervisor_site_assignments_Access" ON supervisor_site_assignments ("TenantId", "SupervisorUserId", "SiteId", "IsActive");
CREATE TABLE IF NOT EXISTS field_smeta_items (
    "Id" uuid NOT NULL PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id") ON DELETE CASCADE,
    "SiteId" uuid NOT NULL REFERENCES sites("Id") ON DELETE CASCADE,
    "StageName" character varying(180) NOT NULL,
    "WorkName" character varying(220) NOT NULL,
    "Unit" character varying(40) NOT NULL,
    "WorkCategory" character varying(100) NULL,
    "IsActive" boolean NOT NULL DEFAULT true,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NULL
);
CREATE INDEX IF NOT EXISTS "IX_field_smeta_items_TenantId" ON field_smeta_items ("TenantId");
CREATE INDEX IF NOT EXISTS "IX_field_smeta_items_SiteId" ON field_smeta_items ("SiteId");
CREATE UNIQUE INDEX IF NOT EXISTS "UX_field_smeta_items_site_work" ON field_smeta_items ("TenantId", "SiteId", "WorkName");
CREATE TABLE IF NOT EXISTS supervisor_daily_reports (
    "Id" uuid NOT NULL PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id") ON DELETE CASCADE,
    "ProjectId" uuid NULL,
    "SiteId" uuid NOT NULL REFERENCES sites("Id") ON DELETE CASCADE,
    "SupervisorUserId" uuid NOT NULL REFERENCES users("Id") ON DELETE CASCADE,
    "ReportDate" date NOT NULL,
    "Shift" character varying(80) NULL,
    "Status" character varying(40) NOT NULL,
    "GeneralNote" character varying(2000) NULL,
    "WeatherCondition" character varying(120) NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "SubmittedAt" timestamp with time zone NULL,
    "ReviewedAt" timestamp with time zone NULL,
    "ReviewedByUserId" uuid NULL,
    "ReviewNote" character varying(1000) NULL
);
CREATE INDEX IF NOT EXISTS "IX_supervisor_daily_reports_TenantId" ON supervisor_daily_reports ("TenantId");
CREATE UNIQUE INDEX IF NOT EXISTS "UX_supervisor_daily_reports_daily" ON supervisor_daily_reports ("TenantId", "SupervisorUserId", "SiteId", "ReportDate");
CREATE INDEX IF NOT EXISTS "IX_supervisor_daily_reports_SiteDate" ON supervisor_daily_reports ("SiteId", "ReportDate");
CREATE INDEX IF NOT EXISTS "IX_supervisor_daily_reports_Status" ON supervisor_daily_reports ("Status");
CREATE TABLE IF NOT EXISTS supervisor_daily_report_lines (
    "Id" uuid NOT NULL PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id") ON DELETE CASCADE,
    "ReportId" uuid NOT NULL REFERENCES supervisor_daily_reports("Id") ON DELETE CASCADE,
    "SmetaItemId" uuid NOT NULL REFERENCES field_smeta_items("Id") ON DELETE RESTRICT,
    "ReportedQuantity" numeric(18,3) NOT NULL,
    "Unit" character varying(40) NOT NULL,
    "Note" character varying(1000) NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);
CREATE INDEX IF NOT EXISTS "IX_supervisor_daily_report_lines_TenantId" ON supervisor_daily_report_lines ("TenantId");
CREATE INDEX IF NOT EXISTS "IX_supervisor_daily_report_lines_ReportId" ON supervisor_daily_report_lines ("ReportId");
CREATE INDEX IF NOT EXISTS "IX_supervisor_daily_report_lines_SmetaItemId" ON supervisor_daily_report_lines ("SmetaItemId");
CREATE TABLE IF NOT EXISTS supervisor_site_notes (
    "Id" uuid NOT NULL PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id") ON DELETE CASCADE,
    "ProjectId" uuid NULL,
    "SiteId" uuid NOT NULL REFERENCES sites("Id") ON DELETE CASCADE,
    "SupervisorUserId" uuid NOT NULL REFERENCES users("Id") ON DELETE CASCADE,
    "EventDateTime" timestamp with time zone NOT NULL,
    "Category" character varying(60) NOT NULL,
    "Text" character varying(2000) NOT NULL,
    "AttachmentPath" character varying(500) NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);
CREATE INDEX IF NOT EXISTS "IX_supervisor_site_notes_TenantId" ON supervisor_site_notes ("TenantId");
CREATE INDEX IF NOT EXISTS "IX_supervisor_site_notes_SiteDate" ON supervisor_site_notes ("SiteId", "EventDateTime");
CREATE INDEX IF NOT EXISTS "IX_supervisor_site_notes_SupervisorUserId" ON supervisor_site_notes ("SupervisorUserId");
CREATE TABLE IF NOT EXISTS supervisor_worker_events (
    "Id" uuid NOT NULL PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id") ON DELETE CASCADE,
    "ProjectId" uuid NULL,
    "SiteId" uuid NOT NULL REFERENCES sites("Id") ON DELETE CASCADE,
    "WorkerId" uuid NOT NULL REFERENCES workers("Id") ON DELETE CASCADE,
    "SupervisorUserId" uuid NOT NULL REFERENCES users("Id") ON DELETE CASCADE,
    "EventType" character varying(80) NOT NULL,
    "EventDateTime" timestamp with time zone NOT NULL,
    "Reason" character varying(1200) NOT NULL,
    "RiskDelta" integer NOT NULL,
    "Status" character varying(40) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "ReviewedAt" timestamp with time zone NULL,
    "ReviewedByUserId" uuid NULL
);
CREATE INDEX IF NOT EXISTS "IX_supervisor_worker_events_TenantId" ON supervisor_worker_events ("TenantId");
CREATE INDEX IF NOT EXISTS "IX_supervisor_worker_events_SiteDate" ON supervisor_worker_events ("SiteId", "EventDateTime");
CREATE INDEX IF NOT EXISTS "IX_supervisor_worker_events_WorkerId" ON supervisor_worker_events ("WorkerId");
CREATE INDEX IF NOT EXISTS "IX_supervisor_worker_events_SupervisorUserId" ON supervisor_worker_events ("SupervisorUserId");
CREATE TABLE IF NOT EXISTS field_warehouse_catalog_items (
    "Id" uuid NOT NULL PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id") ON DELETE CASCADE,
    "Name" character varying(180) NOT NULL,
    "Category" character varying(100) NOT NULL,
    "Unit" character varying(40) NOT NULL,
    "Code" character varying(80) NULL,
    "IsActive" boolean NOT NULL DEFAULT true,
    "CreatedAt" timestamp with time zone NOT NULL
);
CREATE INDEX IF NOT EXISTS "IX_field_warehouse_catalog_items_TenantId" ON field_warehouse_catalog_items ("TenantId");
CREATE UNIQUE INDEX IF NOT EXISTS "UX_field_warehouse_catalog_items_name" ON field_warehouse_catalog_items ("TenantId", "Name");
CREATE TABLE IF NOT EXISTS field_warehouse_requests (
    "Id" uuid NOT NULL PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id") ON DELETE CASCADE,
    "ProjectId" uuid NULL,
    "SiteId" uuid NOT NULL REFERENCES sites("Id") ON DELETE CASCADE,
    "SupervisorUserId" uuid NOT NULL REFERENCES users("Id") ON DELETE CASCADE,
    "CatalogItemId" uuid NOT NULL REFERENCES field_warehouse_catalog_items("Id") ON DELETE RESTRICT,
    "RequestedQuantity" numeric(18,3) NOT NULL,
    "Unit" character varying(40) NOT NULL,
    "NeededBy" date NULL,
    "Urgency" character varying(40) NOT NULL,
    "Reason" character varying(1200) NOT NULL,
    "Justification" character varying(1200) NULL,
    "ManagerComment" character varying(1200) NULL,
    "Status" character varying(60) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NULL,
    "ReviewedAt" timestamp with time zone NULL,
    "ReviewedByUserId" uuid NULL
);
CREATE INDEX IF NOT EXISTS "IX_field_warehouse_requests_TenantId" ON field_warehouse_requests ("TenantId");
CREATE INDEX IF NOT EXISTS "IX_field_warehouse_requests_SiteDate" ON field_warehouse_requests ("SiteId", "CreatedAt");
CREATE INDEX IF NOT EXISTS "IX_field_warehouse_requests_SupervisorUserId" ON field_warehouse_requests ("SupervisorUserId");
CREATE INDEX IF NOT EXISTS "IX_field_warehouse_requests_Status" ON field_warehouse_requests ("Status");
CREATE TABLE IF NOT EXISTS supervisor_audit_events (
    "Id" uuid NOT NULL PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id") ON DELETE CASCADE,
    "ProjectId" uuid NULL,
    "SiteId" uuid NULL,
    "SupervisorUserId" uuid NULL,
    "SupervisorNameSnapshot" character varying(180) NULL,
    "Action" character varying(120) NOT NULL,
    "EntityType" character varying(120) NOT NULL,
    "EntityId" uuid NULL,
    "Timestamp" timestamp with time zone NOT NULL,
    "RiskFlag" boolean NOT NULL DEFAULT false,
    "Description" character varying(1200) NOT NULL,
    "MetadataJson" jsonb NULL
);
CREATE INDEX IF NOT EXISTS "IX_supervisor_audit_events_TenantId" ON supervisor_audit_events ("TenantId");
CREATE INDEX IF NOT EXISTS "IX_supervisor_audit_events_SiteTime" ON supervisor_audit_events ("SiteId", "Timestamp");
CREATE INDEX IF NOT EXISTS "IX_supervisor_audit_events_SupervisorUserId" ON supervisor_audit_events ("SupervisorUserId");
CREATE INDEX IF NOT EXISTS "IX_supervisor_audit_events_Action" ON supervisor_audit_events ("Action");
""", cancellationToken);
        await SeedAdminUserAsync(db, configuration, cancellationToken);
        await SeedDemoFieldDataAsync(db, configuration, cancellationToken);
    }

    private static async Task SeedAdminUserAsync(BuildTrackDbContext db, IConfiguration? configuration, CancellationToken cancellationToken)
    {
        if (configuration is null) return;

        var email = configuration["SEED_ADMIN_EMAIL"];
        var password = configuration["SEED_ADMIN_PASSWORD"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;

        var resetPassword = ParseBool(configuration["SEED_ADMIN_RESET_PASSWORD"]);
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var fullName = string.IsNullOrWhiteSpace(configuration["SEED_ADMIN_FULL_NAME"])
            ? "BuildTrack Admin"
            : configuration["SEED_ADMIN_FULL_NAME"]!.Trim();
        var tenantName = string.IsNullOrWhiteSpace(configuration["SEED_ADMIN_TENANT_NAME"])
            ? "FerstacLabs Demo"
            : configuration["SEED_ADMIN_TENANT_NAME"]!.Trim();

        var tenant = await db.Tenants.FirstOrDefaultAsync(x => x.Id == DemoTenantId, cancellationToken);
        if (tenant is null)
        {
            tenant = new Tenant
            {
                Id = DemoTenantId,
                CompanyName = tenantName,
                Code = "DEMO",
                Status = TenantStatus.Active,
            };
            db.Tenants.Add(tenant);
        }
        else
        {
            tenant.CompanyName = tenantName;
            tenant.Status = TenantStatus.Active;
            tenant.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var user = await db.Users.FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);
        if (user is null)
        {
            db.Users.Add(new AppUser
            {
                TenantId = DemoTenantId,
                FullName = fullName,
                Email = normalizedEmail,
                PasswordHash = BuildTrackPasswordHasher.HashPassword(password),
                Role = BuildTrackUserRole.Owner,
                Status = BuildTrackUserStatus.Active,
            });
        }
        else
        {
            user.TenantId = DemoTenantId;
            user.FullName = fullName;
            user.Role = BuildTrackUserRole.Owner;
            user.Status = BuildTrackUserStatus.Active;
            user.UpdatedAt = DateTimeOffset.UtcNow;

            if (resetPassword)
            {
                user.PasswordHash = BuildTrackPasswordHasher.HashPassword(password);
                Console.WriteLine("Seed admin password hash updated from environment");
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedDemoFieldDataAsync(BuildTrackDbContext db, IConfiguration? configuration, CancellationToken cancellationToken)
    {
        var tenants = await db.Tenants.AsNoTracking().Select(x => x.Id).ToListAsync(cancellationToken);
        foreach (var tenantId in tenants)
        {
            if (!await db.FieldWarehouseCatalogItems.AnyAsync(x => x.TenantId == tenantId, cancellationToken))
            {
                db.FieldWarehouseCatalogItems.AddRange(
                    NewCatalogItem(tenantId, "Kaska", "PPE", "ədəd", "PPE-HELMET"),
                    NewCatalogItem(tenantId, "İş əlcəyi", "PPE", "cüt", "PPE-GLOVE"),
                    NewCatalogItem(tenantId, "Reflektor jilet", "PPE", "ədəd", "PPE-VEST"),
                    NewCatalogItem(tenantId, "Sverlo 12mm", "Alət", "ədəd", "TOOL-DRILL-12"),
                    NewCatalogItem(tenantId, "Kəsici disk", "Sərfiyyat", "ədəd", "CONS-CUT-DISC"),
                    NewCatalogItem(tenantId, "Sement M400", "Material", "kisə", "MAT-CEMENT-M400"));
            }
        }

        var sites = await db.Sites.AsNoTracking().Select(x => new { x.Id, x.TenantId }).ToListAsync(cancellationToken);
        foreach (var site in sites)
        {
            if (await db.FieldSmetaItems.AnyAsync(x => x.TenantId == site.TenantId && x.SiteId == site.Id, cancellationToken)) continue;
            db.FieldSmetaItems.AddRange(
                NewSmetaItem(site.TenantId, site.Id, "Torpaq işləri", "Torpaq qazıntısı", "m3", "Kaba işlər"),
                NewSmetaItem(site.TenantId, site.Id, "Bünövrə / Zirzəmi", "Armatur quraşdırılması", "ton", "Monolit"),
                NewSmetaItem(site.TenantId, site.Id, "Bünövrə / Zirzəmi", "Beton tökülməsi", "m3", "Monolit"),
                NewSmetaItem(site.TenantId, site.Id, "Hörgü işləri", "Kubik hörgü", "m2", "Hörgü"),
                NewSmetaItem(site.TenantId, site.Id, "Suvaq işləri", "Daxili suvaq", "m2", "Suvaq"),
                NewSmetaItem(site.TenantId, site.Id, "Dam örtüyü", "Dam konstruksiyası", "m2", "Dam"));
        }

        await SeedSupervisorUserAsync(db, configuration, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static FieldWarehouseCatalogItem NewCatalogItem(Guid tenantId, string name, string category, string unit, string code) => new()
    {
        TenantId = tenantId,
        Name = name,
        Category = category,
        Unit = unit,
        Code = code,
        IsActive = true,
    };

    private static FieldSmetaItem NewSmetaItem(Guid tenantId, Guid siteId, string stage, string work, string unit, string category) => new()
    {
        TenantId = tenantId,
        SiteId = siteId,
        StageName = stage,
        WorkName = work,
        Unit = unit,
        WorkCategory = category,
        IsActive = true,
    };

    private static async Task SeedSupervisorUserAsync(BuildTrackDbContext db, IConfiguration? configuration, CancellationToken cancellationToken)
    {
        if (configuration is null) return;

        var email = configuration["SEED_SUPERVISOR_EMAIL"];
        var password = configuration["SEED_SUPERVISOR_PASSWORD"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;

        var tenantId = DemoTenantId;
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var fullName = string.IsNullOrWhiteSpace(configuration["SEED_SUPERVISOR_FULL_NAME"])
            ? "Demo Prorab"
            : configuration["SEED_SUPERVISOR_FULL_NAME"]!.Trim();
        var phone = string.IsNullOrWhiteSpace(configuration["SEED_SUPERVISOR_PHONE"])
            ? null
            : configuration["SEED_SUPERVISOR_PHONE"]!.Trim();

        var user = await db.Users.FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);
        if (user is null)
        {
            user = new AppUser
            {
                TenantId = tenantId,
                FullName = fullName,
                Email = normalizedEmail,
                Phone = phone,
                PasswordHash = BuildTrackPasswordHasher.HashPassword(password),
                Role = BuildTrackUserRole.Supervisor,
                Status = BuildTrackUserStatus.Active,
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            user.TenantId = tenantId;
            user.FullName = fullName;
            user.Phone = phone;
            user.Role = BuildTrackUserRole.Supervisor;
            user.Status = BuildTrackUserStatus.Active;
            user.UpdatedAt = DateTimeOffset.UtcNow;
        }

        Guid? siteId = null;
        if (Guid.TryParse(configuration["SEED_SUPERVISOR_SITE_ID"], out var configuredSiteId))
        {
            siteId = configuredSiteId;
        }
        else
        {
            siteId = await db.Sites
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId)
                .OrderBy(x => x.Name)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (siteId is null) return;
        var assignmentExists = await db.SupervisorSiteAssignments.AnyAsync(
            x => x.TenantId == tenantId
                 && x.SupervisorUserId == user.Id
                 && x.SiteId == siteId.Value
                 && x.IsActive,
            cancellationToken);
        if (assignmentExists) return;

        db.SupervisorSiteAssignments.Add(new SupervisorSiteAssignment
        {
            TenantId = tenantId,
            SupervisorUserId = user.Id,
            SiteId = siteId.Value,
            IsActive = true,
            Notes = "Seed supervisor assignment",
        });
    }

    private static bool ParseBool(string? value)
    {
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }
}






















