import { readFileSync } from 'node:fs'
import { join } from 'node:path'

const root = process.cwd()
const read = (path) => readFileSync(join(root, path), 'utf8')
const findings = []

const assertContains = (path, needle, message) => {
  if (!read(path).includes(needle)) findings.push(`${path}: ${message}`)
}

const assertNotContains = (path, needle, message) => {
  if (read(path).includes(needle)) findings.push(`${path}: ${message}`)
}

const authToken = 'src/features/auth/authToken.ts'
const authStore = 'src/features/auth/authStore.ts'
const apiClient = 'src/shared/api/client.ts'
const backendApi = 'src/services/api/buildTrackBackendApi.ts'
const projectSelection = 'src/stores/projectSelectionStore.ts'
const aiStore = 'src/features/aiAssistant/aiAssistantStore.ts'

assertContains(authToken, 'window.sessionStorage.getItem(AUTH_TOKEN_STORAGE_KEY)', 'auth token must be read from current tab sessionStorage')
assertContains(authToken, 'window.sessionStorage.setItem(AUTH_TOKEN_STORAGE_KEY, token)', 'auth token must be written to current tab sessionStorage')
assertContains(authToken, 'window.sessionStorage.removeItem(AUTH_TOKEN_STORAGE_KEY)', 'logout must clear only current tab sessionStorage token')
assertContains(authToken, 'window.localStorage.removeItem(AUTH_TOKEN_STORAGE_KEY)', 'legacy shared localStorage auth token must be removed')
assertNotContains(authToken, 'window.localStorage.getItem(AUTH_TOKEN_STORAGE_KEY)', 'auth token must never be read from shared localStorage')
assertNotContains(authToken, 'window.localStorage.setItem(AUTH_TOKEN_STORAGE_KEY', 'auth token must never be written to shared localStorage')

assertContains(apiClient, 'Object.entries(authHeader()).forEach', 'shared API client must use authHeader')
assertContains(backendApi, 'Object.entries(authHeader()).forEach', 'backend API client must use authHeader')
assertNotContains(authStore, 'clearDataset', 'auth logout/tenant switch must not delete shared IndexedDB data used by other tabs')

assertContains(projectSelection, 'window.sessionStorage.getItem(scopedStorageKey(tenantId))', 'active project selection must be tab/session scoped')
assertContains(projectSelection, 'window.sessionStorage.setItem(scopedStorageKey(tenantId), projectId)', 'active project selection must write to tab/session storage')
assertNotContains(projectSelection, 'window.localStorage.getItem(scopedStorageKey(tenantId))', 'active project selection must not read shared localStorage')
assertNotContains(projectSelection, 'window.localStorage.setItem(scopedStorageKey(tenantId), projectId)', 'active project selection must not write shared localStorage')

assertContains(aiStore, 'createJSONStorage(() => window.sessionStorage)', 'AI chat/session state must be tab scoped')
assertNotContains(aiStore, 'window.localStorage.getItem', 'AI store must not read shared localStorage')
assertNotContains(aiStore, 'window.localStorage.setItem', 'AI store must not write shared localStorage')

if (findings.length) {
  console.error('Auth session audit failed:')
  findings.forEach((finding) => console.error(`- ${finding}`))
  process.exit(1)
}

console.log('Auth session audit passed: authentication credentials are current-tab session scoped.')
