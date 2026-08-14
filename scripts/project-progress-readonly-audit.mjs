import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'

const root = process.cwd()
const read = (file) => readFileSync(resolve(root, file), 'utf8')

const readonlyRoutes = [
  ['Dashboard mount', 'src/features/dashboard/DashboardPage.tsx'],
  ['Workers mount', 'src/features/workers/WorkersPage.tsx'],
  ['Daily Reports mount', 'src/features/dailyReports/DailyReportsPage.tsx'],
  ['Materials mount', 'src/features/materials/MaterialsPage.tsx'],
  ['Warehouse mount', 'src/features/warehouse/WarehousePage.tsx'],
  ['Procurement mount', 'src/features/procurement/ProcurementPage.tsx'],
  ['Daily Attendance mount', 'src/features/dailyAttendance/DailyAttendancePage.tsx'],
  ['Delays/Permissions mount', 'src/features/delaysPermissions/DelaysPermissionsPage.tsx'],
  ['Payroll mount', 'src/features/payroll/PayrollPage.tsx'],
  ['Supervisors mount', 'src/features/supervisors/SupervisorsPage.tsx'],
  ['Supervisor Audit mount', 'src/features/supervisorAudit/SupervisorAuditPage.tsx'],
  ['Devices mount', 'src/features/devices/DevicesPage.tsx'],
  ['Attendance Live mount', 'src/features/attendanceLive/AttendanceLivePage.tsx'],
  ['Security Events mount', 'src/features/securityEvents/SecurityEventsPage.tsx'],
  ['Export mount', 'src/features/export/ExportPage.tsx'],
  ['Settings mount', 'src/features/settings/SettingsPage.tsx'],
  ['AI open/chat/TTS', 'src/features/aiAssistant/AiAssistant.tsx'],
]

const explicitBusinessActions = [
  'syncTenantSites',
  'applyBackendData',
  'refreshSeedData',
  'resetDemoData',
  'addObject',
  'addStage',
  'updateStage',
  'deleteStage',
  'addWorkItem',
  'updateWorkItem',
  'deleteWorkItem',
  'addCrew',
  'updateCrew',
  'deleteCrew',
  'addWorker',
  'updateWorker',
  'deleteWorker',
  'addMaterial',
  'updateMaterial',
  'deleteMaterial',
  'addDailyReport',
  'updateDailyReport',
  'deleteDailyReport',
]

const forbiddenReadOnlyHydrationActions = [
  'syncTenantSites',
  'applyBackendData',
  'hydrateTenantSitesFromBackend',
]

const knownBusinessMutationFiles = new Set([
  'src/features/projectProgress/ProjectEstimatePage.tsx',
  'src/features/projectProgress/ProjectCrewsPage.tsx',
  'src/features/projectProgress/ProjectTimelinePage.tsx',
  'src/features/workers/WorkersPage.tsx',
  'src/features/materials/MaterialsPage.tsx',
  'src/features/projectProgress/projectProgressStore.ts',
])

const findings = []

for (const [route, file] of readonlyRoutes) {
  let text = ''
  try {
    text = read(file)
  } catch {
    continue
  }

  if (file.endsWith('DevicesPage.tsx')) {
    if (/useProjectProgressStore|syncTenantSites|useProjectSelectionStore/.test(text)) {
      findings.push(`${route}: DevicesPage must not import ProjectProgress/project selection mutation paths.`)
    }
  }

  for (const action of forbiddenReadOnlyHydrationActions) {
    const pattern = new RegExp(`\\b${action}\\b`)
    if (pattern.test(text)) {
      findings.push(`${route}: ${file} references ProjectProgress hydration action "${action}" from a read-only route.`)
    }
  }

  for (const action of explicitBusinessActions) {
    const directGetStatePattern = new RegExp(`useProjectProgressStore\\.getState\\(\\)\\.${action}\\b`)
    if (directGetStatePattern.test(text) && !knownBusinessMutationFiles.has(file)) {
      findings.push(`${route}: ${file} directly invokes ProjectProgress business action "${action}" during a read-only route flow.`)
    }
  }
}

const appLayout = read('src/components/layout/AppLayout.tsx')
if (!appLayout.includes('hydrateTenantSitesFromBackend')) {
  findings.push('AppLayout fallback must use hydrateTenantSitesFromBackend for non-persisting site hydration.')
}
if (/syncTenantSites/.test(appLayout)) {
  findings.push('AppLayout must not use syncTenantSites for startup/fallback read-only hydration.')
}

const store = read('src/features/projectProgress/projectProgressStore.ts')
if (!store.includes('hydrateTenantSitesFromBackend')) {
  findings.push('ProjectProgress store must expose hydrateTenantSitesFromBackend.')
}
if (!store.includes('[ProjectProgress] SAVE QUEUED') || !store.includes('[ProjectProgress] NO SAVE')) {
  findings.push('ProjectProgress store must keep save/no-save development diagnostics.')
}

console.log('ProjectProgress read-only route audit')
if (findings.length) {
  findings.forEach((finding) => console.log(`FAIL ${finding}`))
  process.exit(1)
}

console.log('PASS read-only route mounts do not reference ProjectProgress mutating actions.')
console.log('PASS DevicesPage does not mutate ProjectProgress on load/reload/site fetch.')
console.log('PASS AppLayout fallback uses non-persisting tenant site hydration.')
