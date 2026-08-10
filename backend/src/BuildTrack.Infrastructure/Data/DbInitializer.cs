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
        await EnsureSupplyChainSchemaAsync(db, cancellationToken);
        await SeedAdminUserAsync(db, configuration, cancellationToken);
        await SeedDemoFieldDataAsync(db, configuration, cancellationToken);
        await SeedSupplyChainDataAsync(db, cancellationToken);
    }

    private static async Task EnsureSupplyChainSchemaAsync(BuildTrackDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
CREATE EXTENSION IF NOT EXISTS pgcrypto;
ALTER TABLE field_warehouse_catalog_items ADD COLUMN IF NOT EXISTS "NameAz" character varying(180) NULL;
ALTER TABLE field_warehouse_catalog_items ADD COLUMN IF NOT EXISTS "NameRu" character varying(180) NULL;
ALTER TABLE field_warehouse_catalog_items ADD COLUMN IF NOT EXISTS "NameEn" character varying(180) NULL;
ALTER TABLE field_warehouse_catalog_items ADD COLUMN IF NOT EXISTS "Subcategory" character varying(120) NULL;
ALTER TABLE field_warehouse_catalog_items ADD COLUMN IF NOT EXISTS "ItemType" character varying(60) NOT NULL DEFAULT 'Other';
ALTER TABLE field_warehouse_catalog_items ADD COLUMN IF NOT EXISTS "Description" character varying(1000) NULL;
ALTER TABLE field_warehouse_catalog_items ADD COLUMN IF NOT EXISTS "SearchAliases" character varying(1000) NULL;
ALTER TABLE field_warehouse_catalog_items ADD COLUMN IF NOT EXISTS "SpecificationSchemaJson" jsonb NULL;
ALTER TABLE field_warehouse_catalog_items ADD COLUMN IF NOT EXISTS "IsCustom" boolean NOT NULL DEFAULT false;
ALTER TABLE field_warehouse_catalog_items ADD COLUMN IF NOT EXISTS "UpdatedAt" timestamp with time zone NULL;
UPDATE field_warehouse_catalog_items SET "NameAz" = COALESCE("NameAz", "Name") WHERE "NameAz" IS NULL;
CREATE INDEX IF NOT EXISTS "IX_field_warehouse_catalog_items_Code" ON field_warehouse_catalog_items ("Code");
CREATE INDEX IF NOT EXISTS "IX_field_warehouse_catalog_items_Tenant_Category_Subcategory" ON field_warehouse_catalog_items ("TenantId", "Category", "Subcategory");

ALTER TABLE field_warehouse_requests ADD COLUMN IF NOT EXISTS "Code" character varying(40) NULL;
ALTER TABLE field_warehouse_requests ADD COLUMN IF NOT EXISTS "ApprovedQuantity" numeric(18,3) NOT NULL DEFAULT 0;
ALTER TABLE field_warehouse_requests ADD COLUMN IF NOT EXISTS "ReservedQuantity" numeric(18,3) NOT NULL DEFAULT 0;
ALTER TABLE field_warehouse_requests ADD COLUMN IF NOT EXISTS "IssuedQuantity" numeric(18,3) NOT NULL DEFAULT 0;
ALTER TABLE field_warehouse_requests ADD COLUMN IF NOT EXISTS "GeneralNote" character varying(1200) NULL;
ALTER TABLE field_warehouse_requests ADD COLUMN IF NOT EXISTS "AbnormalRequest" boolean NOT NULL DEFAULT false;
ALTER TABLE field_warehouse_requests ADD COLUMN IF NOT EXISTS "SubmittedAt" timestamp with time zone NULL;
UPDATE field_warehouse_requests
SET "Code" = COALESCE(NULLIF("Code", ''), 'FR-' || substr(replace("Id"::text, '-', ''), 1, 10))
WHERE "Code" IS NULL OR "Code" = '';
ALTER TABLE field_warehouse_requests ALTER COLUMN "Code" SET NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS "UX_field_warehouse_requests_Code" ON field_warehouse_requests ("Code");
CREATE INDEX IF NOT EXISTS "IX_field_warehouse_requests_Tenant_Status" ON field_warehouse_requests ("TenantId", "Status");

CREATE TABLE IF NOT EXISTS supply_units (
    "Id" uuid NOT NULL PRIMARY KEY,
    "Code" character varying(40) NOT NULL UNIQUE,
    "NameAz" character varying(80) NOT NULL,
    "NameEn" character varying(80) NOT NULL,
    "NameRu" character varying(80) NOT NULL,
    "IsActive" boolean NOT NULL DEFAULT true,
    "CreatedAt" timestamp with time zone NOT NULL
);
CREATE TABLE IF NOT EXISTS warehouses (
    "Id" uuid NOT NULL PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id") ON DELETE CASCADE,
    "Name" character varying(180) NOT NULL,
    "Address" character varying(300) NULL,
    "IsDefault" boolean NOT NULL DEFAULT true,
    "IsActive" boolean NOT NULL DEFAULT true,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NULL
);
CREATE INDEX IF NOT EXISTS "IX_warehouses_TenantId" ON warehouses ("TenantId");
CREATE INDEX IF NOT EXISTS "IX_warehouses_Tenant_Default" ON warehouses ("TenantId", "IsDefault");

CREATE TABLE IF NOT EXISTS field_warehouse_request_lines (
    "Id" uuid NOT NULL PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id") ON DELETE CASCADE,
    "RequestId" uuid NOT NULL REFERENCES field_warehouse_requests("Id") ON DELETE CASCADE,
    "CatalogItemId" uuid NOT NULL REFERENCES field_warehouse_catalog_items("Id") ON DELETE RESTRICT,
    "RequestedQuantity" numeric(18,3) NOT NULL,
    "ApprovedQuantity" numeric(18,3) NOT NULL DEFAULT 0,
    "ReservedQuantity" numeric(18,3) NOT NULL DEFAULT 0,
    "IssuedQuantity" numeric(18,3) NOT NULL DEFAULT 0,
    "Unit" character varying(40) NOT NULL,
    "Reason" character varying(1200) NULL,
    "SpecificationJson" jsonb NULL,
    "Status" character varying(60) NOT NULL DEFAULT 'Pending',
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NULL
);
CREATE INDEX IF NOT EXISTS "IX_field_warehouse_request_lines_TenantId" ON field_warehouse_request_lines ("TenantId");
CREATE INDEX IF NOT EXISTS "IX_field_warehouse_request_lines_RequestId" ON field_warehouse_request_lines ("RequestId");
CREATE INDEX IF NOT EXISTS "IX_field_warehouse_request_lines_CatalogItemId" ON field_warehouse_request_lines ("CatalogItemId");
INSERT INTO field_warehouse_request_lines ("Id", "TenantId", "RequestId", "CatalogItemId", "RequestedQuantity", "ApprovedQuantity", "ReservedQuantity", "IssuedQuantity", "Unit", "Reason", "Status", "CreatedAt", "UpdatedAt")
SELECT gen_random_uuid(), r."TenantId", r."Id", r."CatalogItemId", r."RequestedQuantity", r."ApprovedQuantity", r."ReservedQuantity", r."IssuedQuantity", r."Unit", r."Reason", 'Pending', r."CreatedAt", r."UpdatedAt"
FROM field_warehouse_requests r
WHERE NOT EXISTS (SELECT 1 FROM field_warehouse_request_lines l WHERE l."RequestId" = r."Id");

CREATE TABLE IF NOT EXISTS warehouse_reservations (
    "Id" uuid NOT NULL PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id") ON DELETE CASCADE,
    "WarehouseId" uuid NOT NULL REFERENCES warehouses("Id") ON DELETE CASCADE,
    "CatalogItemId" uuid NOT NULL REFERENCES field_warehouse_catalog_items("Id") ON DELETE RESTRICT,
    "RequestLineId" uuid NOT NULL REFERENCES field_warehouse_request_lines("Id") ON DELETE CASCADE,
    "Quantity" numeric(18,3) NOT NULL,
    "Status" character varying(40) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "ReleasedAt" timestamp with time zone NULL,
    "ConsumedAt" timestamp with time zone NULL
);
CREATE INDEX IF NOT EXISTS "IX_warehouse_reservations_Tenant_Warehouse_Item_Status" ON warehouse_reservations ("TenantId", "WarehouseId", "CatalogItemId", "Status");
CREATE INDEX IF NOT EXISTS "IX_warehouse_reservations_RequestLineId" ON warehouse_reservations ("RequestLineId");

CREATE TABLE IF NOT EXISTS warehouse_stock_movements (
    "Id" uuid NOT NULL PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id") ON DELETE CASCADE,
    "WarehouseId" uuid NOT NULL REFERENCES warehouses("Id") ON DELETE CASCADE,
    "CatalogItemId" uuid NOT NULL REFERENCES field_warehouse_catalog_items("Id") ON DELETE RESTRICT,
    "MovementType" character varying(60) NOT NULL,
    "Quantity" numeric(18,3) NOT NULL,
    "ReferenceType" character varying(80) NOT NULL,
    "ReferenceId" uuid NULL,
    "PerformedByUserId" uuid NULL,
    "OccurredAt" timestamp with time zone NOT NULL,
    "Note" character varying(1000) NULL
);
CREATE INDEX IF NOT EXISTS "IX_warehouse_stock_movements_Tenant_Warehouse_Item" ON warehouse_stock_movements ("TenantId", "WarehouseId", "CatalogItemId");
CREATE UNIQUE INDEX IF NOT EXISTS "UX_warehouse_stock_movements_reference" ON warehouse_stock_movements ("TenantId", "ReferenceType", "ReferenceId", "CatalogItemId", "MovementType") WHERE "ReferenceId" IS NOT NULL;

CREATE TABLE IF NOT EXISTS warehouse_usage_policies (
    "Id" uuid NOT NULL PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id") ON DELETE CASCADE,
    "CatalogItemId" uuid NULL,
    "Category" character varying(100) NULL,
    "DefaultMaximumPerRequest" numeric(18,3) NULL,
    "DefaultMaximumPerWorker" numeric(18,3) NULL,
    "DefaultMaximumPerSitePeriod" numeric(18,3) NULL,
    "PeriodDays" integer NOT NULL DEFAULT 30,
    "RequireJustificationAboveThreshold" boolean NOT NULL DEFAULT true,
    "RiskWeight" integer NOT NULL DEFAULT 1,
    "CreatedAt" timestamp with time zone NOT NULL
);
CREATE INDEX IF NOT EXISTS "IX_warehouse_usage_policies_Tenant_Item" ON warehouse_usage_policies ("TenantId", "CatalogItemId");
CREATE INDEX IF NOT EXISTS "IX_warehouse_usage_policies_Tenant_Category" ON warehouse_usage_policies ("TenantId", "Category");

CREATE TABLE IF NOT EXISTS procurement_needs (
    "Id" uuid NOT NULL PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id") ON DELETE CASCADE,
    "ProjectId" uuid NULL,
    "SiteId" uuid NOT NULL REFERENCES sites("Id") ON DELETE CASCADE,
    "WarehouseId" uuid NOT NULL REFERENCES warehouses("Id") ON DELETE CASCADE,
    "SourceRequestId" uuid NOT NULL REFERENCES field_warehouse_requests("Id") ON DELETE CASCADE,
    "SourceRequestLineId" uuid NOT NULL REFERENCES field_warehouse_request_lines("Id") ON DELETE CASCADE,
    "CatalogItemId" uuid NOT NULL REFERENCES field_warehouse_catalog_items("Id") ON DELETE RESTRICT,
    "RequiredQuantity" numeric(18,3) NOT NULL,
    "AlreadyAvailableQuantity" numeric(18,3) NOT NULL DEFAULT 0,
    "ShortfallQuantity" numeric(18,3) NOT NULL,
    "PurchasedQuantity" numeric(18,3) NOT NULL DEFAULT 0,
    "ReceivedQuantity" numeric(18,3) NOT NULL DEFAULT 0,
    "Unit" character varying(40) NOT NULL,
    "Priority" character varying(40) NOT NULL,
    "RequiredBy" date NULL,
    "Reason" character varying(1200) NOT NULL,
    "Status" character varying(60) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "CreatedByUserId" uuid NULL,
    "UpdatedAt" timestamp with time zone NULL
);
CREATE INDEX IF NOT EXISTS "IX_procurement_needs_Tenant_Status" ON procurement_needs ("TenantId", "Status");
CREATE INDEX IF NOT EXISTS "IX_procurement_needs_Tenant_Item" ON procurement_needs ("TenantId", "CatalogItemId");
CREATE INDEX IF NOT EXISTS "IX_procurement_needs_SourceRequestLineId" ON procurement_needs ("SourceRequestLineId");

CREATE TABLE IF NOT EXISTS procurement_tasks (
    "Id" uuid NOT NULL PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id") ON DELETE CASCADE,
    "Code" character varying(40) NOT NULL UNIQUE,
    "AssignedProcurementUserId" uuid NULL REFERENCES users("Id") ON DELETE SET NULL,
    "Status" character varying(60) NOT NULL,
    "Priority" character varying(40) NOT NULL,
    "RequiredBy" date NULL,
    "ManagerInstruction" character varying(1200) NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "AssignedAt" timestamp with time zone NULL,
    "StartedAt" timestamp with time zone NULL,
    "SubmittedAt" timestamp with time zone NULL,
    "VerifiedAt" timestamp with time zone NULL,
    "VerifiedByUserId" uuid NULL,
    "VerificationNote" character varying(1200) NULL
);
CREATE INDEX IF NOT EXISTS "IX_procurement_tasks_Tenant_Assignee_Status" ON procurement_tasks ("TenantId", "AssignedProcurementUserId", "Status");

CREATE TABLE IF NOT EXISTS procurement_task_lines (
    "Id" uuid NOT NULL PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id") ON DELETE CASCADE,
    "TaskId" uuid NOT NULL REFERENCES procurement_tasks("Id") ON DELETE CASCADE,
    "ProcurementNeedId" uuid NOT NULL REFERENCES procurement_needs("Id") ON DELETE CASCADE,
    "CatalogItemId" uuid NOT NULL REFERENCES field_warehouse_catalog_items("Id") ON DELETE RESTRICT,
    "RequestedQuantity" numeric(18,3) NOT NULL,
    "PurchasedQuantity" numeric(18,3) NOT NULL DEFAULT 0,
    "AcceptedQuantity" numeric(18,3) NOT NULL DEFAULT 0,
    "Unit" character varying(40) NOT NULL,
    "SpecificationJson" jsonb NULL,
    "Status" character varying(60) NOT NULL,
    "Note" character varying(1200) NULL,
    "UnitPrice" numeric(18,4) NULL,
    "SupplierId" uuid NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NULL
);
CREATE INDEX IF NOT EXISTS "IX_procurement_task_lines_TaskId" ON procurement_task_lines ("TaskId");
CREATE INDEX IF NOT EXISTS "IX_procurement_task_lines_NeedId" ON procurement_task_lines ("ProcurementNeedId");

CREATE TABLE IF NOT EXISTS suppliers (
    "Id" uuid NOT NULL PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id") ON DELETE CASCADE,
    "Name" character varying(180) NOT NULL,
    "TaxId" character varying(60) NULL,
    "Phone" character varying(80) NULL,
    "Email" character varying(180) NULL,
    "Address" character varying(300) NULL,
    "ContactPerson" character varying(180) NULL,
    "Categories" character varying(500) NULL,
    "Status" character varying(40) NOT NULL,
    "Notes" character varying(1000) NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "UX_suppliers_Tenant_Name" ON suppliers ("TenantId", "Name");

CREATE TABLE IF NOT EXISTS procurement_attachments (
    "Id" uuid NOT NULL PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id") ON DELETE CASCADE,
    "TaskId" uuid NOT NULL REFERENCES procurement_tasks("Id") ON DELETE CASCADE,
    "TaskLineId" uuid NULL REFERENCES procurement_task_lines("Id") ON DELETE CASCADE,
    "AttachmentType" character varying(60) NOT NULL,
    "StoragePath" character varying(700) NOT NULL,
    "OriginalFileName" character varying(260) NOT NULL,
    "MimeType" character varying(120) NOT NULL,
    "Size" bigint NOT NULL,
    "UploadedByUserId" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);
CREATE INDEX IF NOT EXISTS "IX_procurement_attachments_TaskId" ON procurement_attachments ("TaskId");
CREATE INDEX IF NOT EXISTS "IX_procurement_attachments_TaskLineId" ON procurement_attachments ("TaskLineId");

CREATE TABLE IF NOT EXISTS procurement_receipts (
    "Id" uuid NOT NULL PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id") ON DELETE CASCADE,
    "TaskId" uuid NOT NULL REFERENCES procurement_tasks("Id") ON DELETE CASCADE,
    "SupplierId" uuid NULL REFERENCES suppliers("Id") ON DELETE SET NULL,
    "ReceiptNumber" character varying(120) NULL,
    "ReceiptDate" date NOT NULL,
    "TotalAmount" numeric(18,2) NOT NULL DEFAULT 0,
    "Currency" character varying(10) NOT NULL,
    "TaxAmount" numeric(18,2) NULL,
    "StorageAttachmentId" uuid NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "CreatedByUserId" uuid NOT NULL
);
CREATE TABLE IF NOT EXISTS procurement_receipt_lines (
    "Id" uuid NOT NULL PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id") ON DELETE CASCADE,
    "ReceiptId" uuid NOT NULL REFERENCES procurement_receipts("Id") ON DELETE CASCADE,
    "TaskLineId" uuid NOT NULL REFERENCES procurement_task_lines("Id") ON DELETE CASCADE,
    "Quantity" numeric(18,3) NOT NULL,
    "Amount" numeric(18,2) NOT NULL
);
CREATE TABLE IF NOT EXISTS catalog_item_purchase_prices (
    "Id" uuid NOT NULL PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id") ON DELETE CASCADE,
    "CatalogItemId" uuid NOT NULL REFERENCES field_warehouse_catalog_items("Id") ON DELETE RESTRICT,
    "SupplierId" uuid NULL REFERENCES suppliers("Id") ON DELETE SET NULL,
    "UnitPrice" numeric(18,4) NOT NULL,
    "Currency" character varying(10) NOT NULL,
    "Quantity" numeric(18,3) NOT NULL,
    "PurchasedAt" timestamp with time zone NOT NULL,
    "ProcurementTaskId" uuid NOT NULL
);
CREATE TABLE IF NOT EXISTS warehouse_goods_receipts (
    "Id" uuid NOT NULL PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id") ON DELETE CASCADE,
    "WarehouseId" uuid NOT NULL REFERENCES warehouses("Id") ON DELETE CASCADE,
    "ProcurementTaskId" uuid NOT NULL REFERENCES procurement_tasks("Id") ON DELETE CASCADE,
    "ReceivedByUserId" uuid NOT NULL,
    "ReceivedAt" timestamp with time zone NOT NULL,
    "Note" character varying(1200) NULL,
    "Status" character varying(40) NOT NULL
);
CREATE INDEX IF NOT EXISTS "IX_warehouse_goods_receipts_TaskId" ON warehouse_goods_receipts ("ProcurementTaskId");
CREATE TABLE IF NOT EXISTS warehouse_goods_receipt_lines (
    "Id" uuid NOT NULL PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id") ON DELETE CASCADE,
    "ReceiptId" uuid NOT NULL REFERENCES warehouse_goods_receipts("Id") ON DELETE CASCADE,
    "ProcurementTaskLineId" uuid NOT NULL REFERENCES procurement_task_lines("Id") ON DELETE CASCADE,
    "CatalogItemId" uuid NOT NULL REFERENCES field_warehouse_catalog_items("Id") ON DELETE RESTRICT,
    "ExpectedQuantity" numeric(18,3) NOT NULL,
    "ReceivedQuantity" numeric(18,3) NOT NULL,
    "RejectedQuantity" numeric(18,3) NOT NULL DEFAULT 0,
    "Unit" character varying(40) NOT NULL,
    "Condition" character varying(40) NOT NULL,
    "Note" character varying(1200) NULL
);
CREATE TABLE IF NOT EXISTS warehouse_issues (
    "Id" uuid NOT NULL PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id") ON DELETE CASCADE,
    "WarehouseId" uuid NOT NULL REFERENCES warehouses("Id") ON DELETE CASCADE,
    "ProjectId" uuid NULL,
    "SiteId" uuid NOT NULL,
    "FieldRequestId" uuid NOT NULL REFERENCES field_warehouse_requests("Id") ON DELETE CASCADE,
    "IssuedByUserId" uuid NOT NULL,
    "ReceivedBySupervisorUserId" uuid NOT NULL,
    "IssuedAt" timestamp with time zone NOT NULL,
    "Status" character varying(40) NOT NULL,
    "RecipientName" character varying(180) NULL,
    "HandoverNote" character varying(1200) NULL,
    "HandoverAttachmentPath" character varying(700) NULL
);
CREATE INDEX IF NOT EXISTS "IX_warehouse_issues_FieldRequestId" ON warehouse_issues ("FieldRequestId");
CREATE TABLE IF NOT EXISTS warehouse_issue_lines (
    "Id" uuid NOT NULL PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id") ON DELETE CASCADE,
    "IssueId" uuid NOT NULL REFERENCES warehouse_issues("Id") ON DELETE CASCADE,
    "CatalogItemId" uuid NOT NULL REFERENCES field_warehouse_catalog_items("Id") ON DELETE RESTRICT,
    "Quantity" numeric(18,3) NOT NULL,
    "Unit" character varying(40) NOT NULL,
    "ReservationId" uuid NOT NULL REFERENCES warehouse_reservations("Id") ON DELETE RESTRICT
);
CREATE TABLE IF NOT EXISTS supply_notifications (
    "Id" uuid NOT NULL PRIMARY KEY,
    "TenantId" uuid NOT NULL REFERENCES tenants("Id") ON DELETE CASCADE,
    "UserId" uuid NULL,
    "SiteId" uuid NULL,
    "Audience" character varying(40) NOT NULL,
    "Title" character varying(180) NOT NULL,
    "Message" character varying(1000) NOT NULL,
    "ReferenceType" character varying(80) NULL,
    "ReferenceId" uuid NULL,
    "Status" character varying(40) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "ReadAt" timestamp with time zone NULL
);
CREATE INDEX IF NOT EXISTS "IX_supply_notifications_Tenant_User_Status" ON supply_notifications ("TenantId", "UserId", "Status");
CREATE INDEX IF NOT EXISTS "IX_supply_notifications_Tenant_Audience_Status" ON supply_notifications ("TenantId", "Audience", "Status");
""", cancellationToken);
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

    private static async Task SeedSupplyChainDataAsync(BuildTrackDbContext db, CancellationToken cancellationToken)
    {
        var units = new[]
        {
            ("eded", "ədəd", "piece", "штука"),
            ("cut", "cüt", "pair", "пара"),
            ("kise", "kisə", "bag", "мешок"),
            ("kg", "kq", "kg", "кг"),
            ("ton", "ton", "ton", "тонна"),
            ("m", "m", "m", "м"),
            ("m2", "m2", "m2", "м2"),
            ("m3", "m3", "m3", "м3"),
            ("rulon", "rulon", "roll", "рулон"),
            ("litr", "litr", "liter", "литр"),
        };

        foreach (var (code, az, en, ru) in units)
        {
            if (await db.SupplyUnits.AnyAsync(x => x.Code == code, cancellationToken)) continue;
            db.SupplyUnits.Add(new SupplyUnit { Code = code, NameAz = az, NameEn = en, NameRu = ru });
        }

        var tenants = await db.Tenants.AsNoTracking().Select(x => x.Id).ToListAsync(cancellationToken);
        foreach (var tenantId in tenants)
        {
            await UpsertCatalogAsync(db, tenantId, "PPE-HELMET", "Kaska", "Каска", "Safety helmet", "PPE", "Baş qoruyucu", "ədəd", SupplyItemType.PPE, "helmet hardhat safety kaska каска");
            await UpsertCatalogAsync(db, tenantId, "PPE-GLOVE", "İş əlcəyi", "Рабочие перчатки", "Work gloves", "PPE", "Əl qoruyucu", "cüt", SupplyItemType.PPE, "elcek glove перчатки əlcək əlcəyi");
            await UpsertCatalogAsync(db, tenantId, "PPE-VEST", "Reflektor jilet", "Сигнальный жилет", "Reflective vest", "PPE", "Görünürlük", "ədəd", SupplyItemType.PPE, "jilet vest reflective");
            await UpsertCatalogAsync(db, tenantId, "PPE-GLASSES", "Qoruyucu eynək", "Защитные очки", "Safety glasses", "PPE", "Göz qoruyucu", "ədəd", SupplyItemType.PPE, "eynek glasses goggles");
            await UpsertCatalogAsync(db, tenantId, "TOOL-DRILL-12", "Sverlo 12mm", "Сверло 12мм", "Drill bit 12mm", "Alət", "Sverlo", "ədəd", SupplyItemType.Tool, "sverlo drill bit сверло");
            await UpsertCatalogAsync(db, tenantId, "TOOL-DRILL-8", "Sverlo 8mm", "Сверло 8мм", "Drill bit 8mm", "Alət", "Sverlo", "ədəd", SupplyItemType.Tool, "sverlo drill bit сверло");
            await UpsertCatalogAsync(db, tenantId, "CONS-CUT-DISC", "Kəsici disk", "Отрезной диск", "Cutting disc", "Sərfiyyat", "Disk", "ədəd", SupplyItemType.Consumable, "disk cutting grinder");
            await UpsertCatalogAsync(db, tenantId, "CONS-WELD-ELECTRODE", "Qaynaq elektrodu", "Сварочный электрод", "Welding electrode", "Sərfiyyat", "Qaynaq", "kq", SupplyItemType.Consumable, "qaynaq electrode welding");
            await UpsertCatalogAsync(db, tenantId, "MAT-CEMENT-M400", "Sement M400", "Цемент M400", "Cement M400", "Material", "Sement", "kisə", SupplyItemType.ConstructionMaterial, "sement cement цемент");
            await UpsertCatalogAsync(db, tenantId, "MAT-WATERPROOF-ROLL", "Hidroizolyasiya rulonu", "Гидроизоляционный рулон", "Waterproofing roll", "Material", "İzolyasiya", "rulon", SupplyItemType.Chemical, "hidroizolyasiya waterproofing");
            await UpsertCatalogAsync(db, tenantId, "MAT-REBAR-A3", "Armatur A3", "Арматура A3", "Rebar A3", "Material", "Armatur", "ton", SupplyItemType.Steel, "armatur rebar steel");
            await UpsertCatalogAsync(db, tenantId, "MAT-CONCRETE-B25", "Beton B25", "Бетон B25", "Concrete B25", "Material", "Beton", "m3", SupplyItemType.Concrete, "beton concrete");
            await UpsertCatalogAsync(db, tenantId, "ELEC-EXT-CABLE-30", "Uzatma kabeli 30m", "Удлинитель 30м", "Extension cable 30m", "Elektrik", "Kabel", "ədəd", SupplyItemType.Electrical, "kabel cable extension");

            var warehouse = await db.Warehouses.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.IsDefault, cancellationToken);
            if (warehouse is null)
            {
                warehouse = new Warehouse
                {
                    TenantId = tenantId,
                    Name = "Mərkəzi anbar",
                    Address = "BuildTrack demo anbarı",
                    IsDefault = true,
                    IsActive = true,
                };
                db.Warehouses.Add(warehouse);
                await db.SaveChangesAsync(cancellationToken);
            }

            await SeedOpeningBalanceAsync(db, tenantId, warehouse.Id, "PPE-HELMET", 86, cancellationToken);
            await SeedOpeningBalanceAsync(db, tenantId, warehouse.Id, "PPE-GLOVE", 35, cancellationToken);
            await SeedOpeningBalanceAsync(db, tenantId, warehouse.Id, "PPE-VEST", 48, cancellationToken);
            await SeedOpeningBalanceAsync(db, tenantId, warehouse.Id, "TOOL-DRILL-12", 0, cancellationToken);
            await SeedOpeningBalanceAsync(db, tenantId, warehouse.Id, "TOOL-DRILL-8", 64, cancellationToken);
            await SeedOpeningBalanceAsync(db, tenantId, warehouse.Id, "CONS-CUT-DISC", 120, cancellationToken);
            await SeedOpeningBalanceAsync(db, tenantId, warehouse.Id, "CONS-WELD-ELECTRODE", 42, cancellationToken);
            await SeedOpeningBalanceAsync(db, tenantId, warehouse.Id, "MAT-CEMENT-M400", 210, cancellationToken);
            await SeedOpeningBalanceAsync(db, tenantId, warehouse.Id, "MAT-WATERPROOF-ROLL", 9, cancellationToken);
            await SeedOpeningBalanceAsync(db, tenantId, warehouse.Id, "ELEC-EXT-CABLE-30", 14, cancellationToken);

            await SeedUsagePolicyAsync(db, tenantId, "PPE", 50, cancellationToken);
            await SeedUsagePolicyAsync(db, tenantId, "Alət", 10, cancellationToken);
            await SeedUsagePolicyAsync(db, tenantId, "Sərfiyyat", 75, cancellationToken);
            await SeedUsagePolicyAsync(db, tenantId, "Material", 250, cancellationToken);

            if (!await db.Suppliers.AnyAsync(x => x.TenantId == tenantId, cancellationToken))
            {
                db.Suppliers.AddRange(
                    new Supplier { TenantId = tenantId, Name = "SafetyPro", Phone = "+994 50 000 10 10", Categories = "PPE", Status = SupplierStatus.Active },
                    new Supplier { TenantId = tenantId, Name = "ToolMarket", Phone = "+994 50 000 20 20", Categories = "Alət,Sərfiyyat", Status = SupplierStatus.Active },
                    new Supplier { TenantId = tenantId, Name = "Karvan Beton", Phone = "+994 50 000 30 30", Categories = "Material", Status = SupplierStatus.Active });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task UpsertCatalogAsync(
        BuildTrackDbContext db,
        Guid tenantId,
        string code,
        string nameAz,
        string nameRu,
        string nameEn,
        string category,
        string subcategory,
        string unit,
        SupplyItemType itemType,
        string aliases)
    {
        var item = await db.FieldWarehouseCatalogItems.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Code == code);
        if (item is null)
        {
            db.FieldWarehouseCatalogItems.Add(new FieldWarehouseCatalogItem
            {
                TenantId = tenantId,
                Code = code,
                Name = nameAz,
                NameAz = nameAz,
                NameRu = nameRu,
                NameEn = nameEn,
                Category = category,
                Subcategory = subcategory,
                Unit = unit,
                ItemType = itemType,
                SearchAliases = aliases,
                IsActive = true,
            });
            return;
        }

        item.Name = nameAz;
        item.NameAz = nameAz;
        item.NameRu = nameRu;
        item.NameEn = nameEn;
        item.Category = category;
        item.Subcategory = subcategory;
        item.Unit = unit;
        item.ItemType = itemType;
        item.SearchAliases = aliases;
        item.IsActive = true;
        item.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static async Task SeedOpeningBalanceAsync(BuildTrackDbContext db, Guid tenantId, Guid warehouseId, string itemCode, decimal quantity, CancellationToken cancellationToken)
    {
        var item = await db.FieldWarehouseCatalogItems.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Code == itemCode, cancellationToken);
        if (item is null) return;
        if (await db.WarehouseStockMovements.AnyAsync(x => x.TenantId == tenantId && x.WarehouseId == warehouseId && x.CatalogItemId == item.Id && x.ReferenceType == "SeedOpeningBalance", cancellationToken)) return;

        db.WarehouseStockMovements.Add(new WarehouseStockMovement
        {
            TenantId = tenantId,
            WarehouseId = warehouseId,
            CatalogItemId = item.Id,
            MovementType = WarehouseStockMovementType.OpeningBalance,
            Quantity = quantity,
            ReferenceType = "SeedOpeningBalance",
            ReferenceId = item.Id,
            Note = "Demo başlanğıc qalığı",
        });
    }

    private static async Task SeedUsagePolicyAsync(BuildTrackDbContext db, Guid tenantId, string category, decimal maxPerRequest, CancellationToken cancellationToken)
    {
        if (await db.WarehouseUsagePolicies.AnyAsync(x => x.TenantId == tenantId && x.Category == category && x.CatalogItemId == null, cancellationToken)) return;
        db.WarehouseUsagePolicies.Add(new WarehouseUsagePolicy
        {
            TenantId = tenantId,
            Category = category,
            DefaultMaximumPerRequest = maxPerRequest,
            DefaultMaximumPerSitePeriod = maxPerRequest * 4,
            PeriodDays = 30,
            RequireJustificationAboveThreshold = true,
            RiskWeight = category == "PPE" ? 2 : 1,
        });
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






















