import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'

const root = process.cwd()

const read = (path) => readFileSync(resolve(root, path), 'utf8')

const checks = [
  {
    name: 'saveWorkspace uses strict apiRequest',
    pass: () => {
      const api = read('src/features/projectProgress/projectProgressApi.ts')
      return /saveWorkspace:\s*\([^)]*\)\s*=>\s*apiRequest/.test(api)
        && !/saveWorkspace:\s*\([^)]*\)\s*=>\s*tryApiRequest/.test(api)
    },
  },
  {
    name: 'central serialized save queue exists',
    pass: () => {
      const store = read('src/features/projectProgress/projectProgressStore.ts')
      return store.includes('flushProjectWorkspaceSaveQueue')
        && store.includes('saveInFlight')
        && store.includes('pendingSaveJob')
        && store.includes('queueServerSave(workspace)')
    },
  },
  {
    name: 'tenant switch/hydration suppresses saves',
    pass: () => {
      const store = read('src/features/projectProgress/projectProgressStore.ts')
      return store.includes('suppressWorkspacePersistence')
        && store.includes('runWithoutWorkspacePersistence')
        && store.includes('discardQueuedServerSaves()')
    },
  },
  {
    name: 'server write failures are visible and retryable',
    pass: () => {
      const store = read('src/features/projectProgress/projectProgressStore.ts')
      const layout = read('src/components/layout/AppLayout.tsx')
      return store.includes("serverSyncStatus: 'error'")
        && store.includes('serverPendingSave: true')
        && layout.includes('Layihə dəyişiklikləri serverdə saxlanmadı')
        && layout.includes('Yenidən saxla')
    },
  },
  {
    name: 'backend rejects cross-tenant workspace save',
    pass: () => {
      const endpoints = read('backend/src/BuildTrack.Api/ProjectProgressEndpoints.cs')
      return endpoints.includes('ValidateWorkspaceTenant')
        && endpoints.includes('Workspace tenant does not match authenticated tenant')
        && endpoints.includes('NormalizeWorkspaceJsonForTenant')
    },
  },
]

const failures = checks.filter((check) => !check.pass())

for (const check of checks) {
  console.log(`${failures.includes(check) ? 'FAIL' : 'PASS'} ${check.name}`)
}

if (failures.length) {
  process.exitCode = 1
}
