import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'

const root = process.cwd()

const read = (path) => readFileSync(resolve(root, path), 'utf8')

const checks = [
  {
    name: 'legacy saveWorkspace remains explicit strict API path',
    pass: () => {
      const api = read('src/features/projectProgress/projectProgressApi.ts')
      return /saveWorkspace:\s*\([^)]*\)\s*=>\s*apiRequest/.test(api)
        && !/saveWorkspace:\s*\([^)]*\)\s*=>\s*tryApiRequest/.test(api)
        && api.includes('Compatibility-only path')
    },
  },
  {
    name: 'normal runtime full-workspace autosave is disabled',
    pass: () => {
      const store = read('src/features/projectProgress/projectProgressStore.ts')
      const subscribeIndex = store.indexOf('useProjectProgressStore.subscribe')
      const subscribeTail = subscribeIndex >= 0 ? store.slice(subscribeIndex) : ''
      return store.includes('flushProjectWorkspaceSaveQueue')
        && store.includes('queueServerSave')
        && !subscribeTail.includes('queueServerSave(')
    },
  },
  {
    name: 'tenant switch/hydration suppresses save fingerprints',
    pass: () => {
      const store = read('src/features/projectProgress/projectProgressStore.ts')
      return store.includes('suppressWorkspacePersistence')
        && store.includes('runWithoutWorkspacePersistence')
        && store.includes('discardQueuedServerSaves()')
    },
  },
  {
    name: 'Smeta normal mutations use granular server APIs',
    pass: () => {
      const api = read('src/features/projectProgress/projectProgressApi.ts')
      const page = read('src/features/projectProgress/ProjectEstimatePage.tsx')
      return api.includes('/api/project-work-items/')
        && api.includes('/api/projects/${projectId}/work-items')
        && page.includes('projectProgressApi.createWorkItem')
        && page.includes('projectProgressApi.updateWorkItem')
        && page.includes('projectProgressApi.deleteWorkItem')
        && page.includes('await loadFromBackend()')
    },
  },
  {
    name: 'backend exposes canonical project-progress CRUD',
    pass: () => {
      const endpoints = read('backend/src/BuildTrack.Api/ProjectProgressEndpoints.cs')
      return endpoints.includes('/api/projects/{projectId}/work-items')
        && endpoints.includes('/api/project-work-items/{id}')
        && endpoints.includes('BuildWorkspaceFromCanonicalTablesAsync')
        && endpoints.includes('ImportWorkspaceJsonIntoCanonicalAsync')
        && endpoints.includes('UpsertFieldSmetaItemForWorkItemAsync')
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
