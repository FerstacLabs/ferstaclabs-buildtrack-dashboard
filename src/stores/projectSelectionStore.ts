import { create } from 'zustand'

export const ALL_PROJECTS_ID = 'all'
export const PROJECT_SELECTION_STORAGE_KEY = 'buildtrack.selectedProjectId'

export type SelectedProjectId = string

interface ProjectSelectionState {
  selectedProjectId: SelectedProjectId
  setSelectedProjectId: (projectId: SelectedProjectId) => void
  ensureSelectedProjectId: (validProjectIds: string[]) => SelectedProjectId
}

export const normalizeSelectedProjectId = (projectId?: string | null, validProjectIds?: string[]) => {
  const normalized = projectId?.trim() || ALL_PROJECTS_ID
  if (normalized === ALL_PROJECTS_ID) return ALL_PROJECTS_ID
  if (validProjectIds && !validProjectIds.includes(normalized)) return ALL_PROJECTS_ID
  return normalized
}

const readStoredProjectId = () => {
  if (typeof window === 'undefined') return ALL_PROJECTS_ID

  try {
    return normalizeSelectedProjectId(window.localStorage.getItem(PROJECT_SELECTION_STORAGE_KEY))
  } catch {
    return ALL_PROJECTS_ID
  }
}

const writeStoredProjectId = (projectId: SelectedProjectId) => {
  if (typeof window === 'undefined') return

  try {
    window.localStorage.setItem(PROJECT_SELECTION_STORAGE_KEY, projectId)
  } catch {
    // Persistence can be unavailable in private browsing; reactive state still updates.
  }
}

const debugProjectSelection = (source: string, previous: SelectedProjectId, next: SelectedProjectId) => {
  if (!import.meta.env.DEV || previous === next) return
  console.debug('[ProjectStore] selectedProjectId', { source, previous, next })
}

export const useProjectSelectionStore = create<ProjectSelectionState>()((set, get) => ({
  selectedProjectId: readStoredProjectId(),
  setSelectedProjectId: (projectId) => {
    const previous = get().selectedProjectId
    const next = normalizeSelectedProjectId(projectId)
    writeStoredProjectId(next)
    debugProjectSelection('setSelectedProjectId', previous, next)
    set({ selectedProjectId: next })
  },
  ensureSelectedProjectId: (validProjectIds) => {
    const previous = get().selectedProjectId
    const next = normalizeSelectedProjectId(previous, validProjectIds)

    if (next !== previous) {
      writeStoredProjectId(next)
      debugProjectSelection('ensureSelectedProjectId', previous, next)
      set({ selectedProjectId: next })
    }

    return next
  },
}))
