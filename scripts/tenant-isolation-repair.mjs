import { spawnSync } from 'node:child_process'

const apply = process.argv.includes('--apply')
const databaseUrl = process.env.POSTGRES_CONNECTION_STRING ?? process.env.DATABASE_URL

const sql = String.raw`
\set ON_ERROR_STOP on
BEGIN;

CREATE TEMP TABLE tenant_repair_summary(entity text, rows_changed integer) ON COMMIT DROP;

WITH changed AS (
  UPDATE devices child
  SET "TenantId" = parent."TenantId"
  FROM sites parent
  WHERE child."SiteId" = parent."Id" AND child."TenantId" <> parent."TenantId"
  RETURNING child."Id"
) INSERT INTO tenant_repair_summary SELECT 'devices.site_tenant_mismatch', count(*) FROM changed;

WITH changed AS (
  UPDATE workers child
  SET "TenantId" = parent."TenantId"
  FROM sites parent
  WHERE child."SiteId" = parent."Id" AND child."TenantId" <> parent."TenantId"
  RETURNING child."Id"
) INSERT INTO tenant_repair_summary SELECT 'workers.site_tenant_mismatch', count(*) FROM changed;

WITH changed AS (
  UPDATE worker_camera_identities child
  SET "TenantId" = parent."TenantId"
  FROM workers parent
  WHERE child."WorkerId" = parent."Id" AND child."TenantId" <> parent."TenantId"
  RETURNING child."Id"
) INSERT INTO tenant_repair_summary SELECT 'worker_camera_identities.worker_tenant_mismatch', count(*) FROM changed;

WITH changed AS (
  UPDATE worker_site_assignments child
  SET "TenantId" = parent."TenantId"
  FROM workers parent
  WHERE child."WorkerId" = parent."Id" AND child."TenantId" <> parent."TenantId"
  RETURNING child."Id"
) INSERT INTO tenant_repair_summary SELECT 'worker_site_assignments.worker_tenant_mismatch', count(*) FROM changed;

WITH changed AS (
  UPDATE attendance_events child
  SET "TenantId" = parent."TenantId"
  FROM devices parent
  WHERE child."DeviceId" = parent."Id" AND child."TenantId" <> parent."TenantId"
  RETURNING child."Id"
) INSERT INTO tenant_repair_summary SELECT 'attendance_events.device_tenant_mismatch', count(*) FROM changed;

WITH changed AS (
  UPDATE attendance_sessions child
  SET "TenantId" = parent."TenantId"
  FROM devices parent
  WHERE child."DeviceId" = parent."Id" AND child."TenantId" <> parent."TenantId"
  RETURNING child."Id"
) INSERT INTO tenant_repair_summary SELECT 'attendance_sessions.device_tenant_mismatch', count(*) FROM changed;

WITH changed AS (
  UPDATE security_events child
  SET "TenantId" = parent."TenantId"
  FROM devices parent
  WHERE child."DeviceId" = parent."Id" AND child."TenantId" <> parent."TenantId"
  RETURNING child."Id"
) INSERT INTO tenant_repair_summary SELECT 'security_events.device_tenant_mismatch', count(*) FROM changed;

WITH changed AS (
  UPDATE supervisor_daily_reports child
  SET "TenantId" = parent."TenantId"
  FROM sites parent
  WHERE child."SiteId" = parent."Id" AND child."TenantId" <> parent."TenantId"
  RETURNING child."Id"
) INSERT INTO tenant_repair_summary SELECT 'supervisor_daily_reports.site_tenant_mismatch', count(*) FROM changed;

WITH changed AS (
  UPDATE supervisor_daily_report_lines child
  SET "TenantId" = parent."TenantId"
  FROM supervisor_daily_reports parent
  WHERE child."ReportId" = parent."Id" AND child."TenantId" <> parent."TenantId"
  RETURNING child."Id"
) INSERT INTO tenant_repair_summary SELECT 'supervisor_daily_report_lines.report_tenant_mismatch', count(*) FROM changed;

SELECT * FROM tenant_repair_summary ORDER BY entity;

${apply ? 'COMMIT;' : 'ROLLBACK;'}
`

if (!apply) {
  console.log('Tenant isolation repair dry-run SQL. Review it before running with --apply.')
  console.log(sql)
  process.exit(0)
}

if (!databaseUrl) {
  console.error('POSTGRES_CONNECTION_STRING or DATABASE_URL is required for --apply.')
  process.exit(1)
}

const result = spawnSync('psql', [databaseUrl], { input: sql, encoding: 'utf8', stdio: ['pipe', 'inherit', 'inherit'] })
process.exit(result.status ?? 1)
