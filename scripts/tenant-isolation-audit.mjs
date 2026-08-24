import { readFileSync } from 'node:fs'
import { join } from 'node:path'

const root = process.cwd()
const read = (path) => readFileSync(join(root, path), 'utf8')
const failures = []

const assertIncludes = (file, needle, description) => {
  if (!read(file).includes(needle)) failures.push(`${description}: missing "${needle}" in ${file}`)
}

const program = read('backend/src/BuildTrack.Api/Program.cs')
const fieldPortal = read('backend/src/BuildTrack.Api/FieldPortalEndpoints.cs')
const seeder = read('backend/src/BuildTrack.Infrastructure/Data/BakinityDemoSeeder.cs')
const authStore = read('src/features/auth/authStore.ts')

for (const [route, tenantNeedle] of [
  ['/api/sites', 'Where(x => x.TenantId == tenantId).OrderBy(x => x.Name)'],
  ['/api/workers', 'Where(x => x.TenantId == tenantId)'],
  ['/api/devices', 'Where(device => device.TenantId == tenantId)'],
  ['/api/attendance-events', 'var query = db.AttendanceEvents.AsNoTracking().Where(x => x.TenantId == tenantId)'],
  ['/api/sites/{siteId:guid}/security-events', 'Where(x => x.TenantId == tenantId && x.SiteId == siteId && x.EventDate == eventDate)'],
]) {
  if (!program.includes(tenantNeedle)) failures.push(`${route} is not explicitly tenant scoped`)
}

assertIncludes(
  'backend/src/BuildTrack.Api/FieldPortalEndpoints.cs',
  '.Where(x => x.TenantId == tenantId)',
  'management field reports must be tenant scoped',
)
assertIncludes(
  'backend/src/BuildTrack.Infrastructure/Data/BakinityDemoSeeder.cs',
  'already belongs to another tenant',
  'Bakinity seeder must not move users between tenants',
)
assertIncludes(
  'backend/src/BuildTrack.Infrastructure/Data/BakinityDemoSeeder.cs',
  'Eldar Qəmbərov',
  'Bakinity owner seed display name',
)
assertIncludes(
  'src/features/auth/authStore.ts',
  'resetTenantScopedBrowserState',
  'frontend login/logout tenant cache reset',
)

if (program.includes('FirstOrDefaultAsync(x => x.Id == request.WorkerId')) {
  failures.push('security event link-worker can resolve a worker without tenant ownership')
}

if (!fieldPortal.includes('var tenantId = RequireTenantId(tenantContext);')) {
  failures.push('management field reports must require tenant context')
}

if (seeder.includes('user.TenantId = tenantId;') && !seeder.includes('if (user.TenantId != tenantId)')) {
  failures.push('Bakinity seeder can still overwrite user tenant ownership')
}

if (!authStore.includes('previousTenantId') || !authStore.includes('nextTenantId')) {
  failures.push('auth store must compare previous and next tenant before applying tenant-scoped state')
}

if (failures.length > 0) {
  console.error('Tenant isolation audit failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('Tenant isolation audit passed.')
