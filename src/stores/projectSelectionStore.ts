import { create } from 'zustand'

export const ALL_PROJECTS_ID = 'all'
export const PROJECT_SELECTION_STORAGE_KEY = 'buildtrack.selectedProjectId'
const legacyProjectSelectionStorageKey = PROJECT_SELECTION_STORAGE_KEY

export type SelectedProjectId = string

interface ProjectSelectionState {
  tenantScopeId?: string
  selectedProjectId: SelectedProjectId
  lastChangedAt: number
  setTenantScope: (tenantId?: string) => void
  setSelectedProjectId: (projectId: SelectedProjectId) => void
  ensureSelectedProjectId: (validProjectIds: string[]) => SelectedProjectId
  clearSelection: () => void
}

export const normalizeSelectedProjectId = (projectId?: string | null, validProjectIds?: string[]) => {
  const normalized = projectId?.trim() || ALL_PROJECTS_ID
  if (normalized === ALL_PROJECTS_ID) return ALL_PROJECTS_ID
  if (validProjectIds && !validProjectIds.includes(normalized)) return ALL_PROJECTS_ID
  return normalized
}

const scopedStorageKey = (tenantId?: string) =>
  tenantId ? `${PROJECT_SELECTION_STORAGE_KEY}.${tenantId}` : PROJECT_SELECTION_STORAGE_KEY

const readStoredProjectId = (tenantId?: string) => {
  if (typeof window === 'undefined') return ALL_PROJECTS_ID

  try {
    const scoped = window.localStorage.getItem(scopedStorageKey(tenantId))
    if (scoped) return normalizeSelectedProjectId(scoped)
    if (!tenantId) return normalizeSelectedProjectId(window.localStorage.getItem(legacyProjectSelectionStorageKey))
    return ALL_PROJECTS_ID
  } catch {
    return ALL_PROJECTS_ID
  }
}

const writeStoredProjectId = (projectId: SelectedProjectId, tenantId?: string) => {
  if (typeof window === 'undefined') return

  try {
    window.localStorage.setItem(scopedStorageKey(tenantId), projectId)
    window.localStorage.removeItem(legacyProjectSelectionStorageKey)
  } catch {
    // Persistence can be unavailable in private browsing; reactive state still updates.
  }
}

const clearStoredProjectSelection = (tenantId?: string) => {
  if (typeof window === 'undefined') return

  try {
    window.localStorage.removeItem(scopedStorageKey(tenantId))
    window.localStorage.removeItem(legacyProjectSelectionStorageKey)
  } catch {
    // Ignore unavailable storage; in-memory state still resets.
  }
}

const debugProjectSelection = (source: string, previous: SelectedProjectId, next: SelectedProjectId) => {
  if (!import.meta.env.DEV || previous === next) return
  console.debug('[ProjectStore] selectedProjectId', { source, previous, next })
}

export const useProjectSelectionStore = create<ProjectSelectionState>()((set, get) => ({
  tenantScopeId: undefined,
  selectedProjectId: readStoredProjectId(),
  lastChangedAt: Date.now(),
  setTenantScope: (tenantId) => {
    const previousTenant = get().tenantScopeId
    if (previousTenant === tenantId) return
    const next = readStoredProjectId(tenantId)
    set({ tenantScopeId: tenantId, selectedProjectId: next, lastChangedAt: Date.now() })
  },
  setSelectedProjectId: (projectId) => {
    const previous = get().selectedProjectId
    const next = normalizeSelectedProjectId(projectId)
    debugProjectSelection('setSelectedProjectId', previous, next)
    set({ selectedProjectId: next, lastChangedAt: Date.now() })
    writeStoredProjectId(next, get().tenantScopeId)
  },
  ensureSelectedProjectId: (validProjectIds) => {
    const previous = get().selectedProjectId
    const next = normalizeSelectedProjectId(previous, validProjectIds)

    if (next !== previous) {
      debugProjectSelection('ensureSelectedProjectId', previous, next)
      set({ selectedProjectId: next, lastChangedAt: Date.now() })
      writeStoredProjectId(next, get().tenantScopeId)
    }

    return next
  },
  clearSelection: () => {
    clearStoredProjectSelection(get().tenantScopeId)
    set({ selectedProjectId: ALL_PROJECTS_ID, lastChangedAt: Date.now() })
  },
}))
