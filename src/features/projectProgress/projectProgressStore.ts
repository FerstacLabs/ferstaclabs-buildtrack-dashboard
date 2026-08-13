import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import type {
  AiAssistantMessage,
  ConstructionObject,
  Crew,
  DailyForemanReport,
  MaterialItem,
  ProjectProgressData,
  ProjectProgressMetrics,
  ProjectWorkStatus,
  WorkItem,
  WorkStage,
  WorkerAssignment,
} from '../../types/projectProgress'
import { ALL_PROJECTS_ID, useProjectSelectionStore } from '../../stores/projectSelectionStore'
import { createEmptyProjectProgressData, projectProgressSeed } from './projectProgressSeed'
import { projectProgressApi } from './projectProgressApi'

type ServerSyncStatus = 'idle' | 'loading' | 'ready' | 'saving' | 'fallback' | 'error'

interface ProjectProgressState extends ProjectProgressData {
  serverSyncStatus: ServerSyncStatus
  serverSyncError?: string
  serverPendingSave: boolean
  serverLastSavedAt?: string
  legacyLocalDataAvailable: boolean
  legacyLocalSummary?: string
  legacyLocalSnapshot?: ProjectProgressData
  prepareWorkspaceForTenant: (tenantId: string, tenantCode?: string, companyName?: string) => void
  loadFromBackend: () => Promise<boolean>
  saveToBackend: () => Promise<void>
  importLegacyLocalData: () => Promise<void>
  dismissLegacyLocalData: () => void
  applyBackendData: (data: Partial<ProjectProgressData>) => void
  syncTenantSites: (sites: Array<{ id: string; name: string; address?: string; createdAt?: string }>, mode?: 'replace' | 'merge') => void
  refreshSeedData: () => void
  resetDemoData: () => void
  addObject: (object: Omit<ConstructionObject, 'id' | 'projectId' | 'status'> & Partial<Pick<ConstructionObject, 'projectId' | 'status'>>) => string
  addStage: (stage: Omit<WorkStage, 'id' | 'order'>) => void
  updateStage: (stageId: string, patch: Partial<WorkStage>) => void
  deleteStage: (stageId: string) => void
  addWorkItem: (item: Omit<WorkItem, 'id'>) => string
  updateWorkItem: (itemId: string, patch: Partial<WorkItem>) => void
  deleteWorkItem: (itemId: string) => void
  addCrew: (crew: Omit<Crew, 'id'>) => void
  updateCrew: (crewId: string, patch: Partial<Crew>) => void
  deleteCrew: (crewId: string) => void
  addWorker: (worker: Omit<WorkerAssignment, 'id'>) => void
  updateWorker: (workerId: string, patch: Partial<WorkerAssignment>) => void
  deleteWorker: (workerId: string) => void
  addMaterial: (material: Omit<MaterialItem, 'id' | 'remainingQuantity'>) => void
  updateMaterial: (materialId: string, patch: Partial<MaterialItem>) => void
  deleteMaterial: (materialId: string) => void
  addDailyReport: (report: Omit<DailyForemanReport, 'id' | 'createdAt'>) => void
  updateDailyReport: (reportId: string, patch: Partial<DailyForemanReport>) => void
  deleteDailyReport: (reportId: string) => void
  addAssistantMessage: (message: Omit<AiAssistantMessage, 'id' | 'createdAt'>) => void
  clearAssistantMessages: () => void
}

const createId = (prefix: string) => `${prefix}-${Date.now()}-${Math.random().toString(16).slice(2, 8)}`

const todayKey = () => new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Baku' }).format(new Date())

const round1 = (value: number) => Math.round(value * 10) / 10

const clampProgress = (value: number) => Math.max(0, Math.min(100, Math.round(value * 10) / 10))

export const calculateWorkItemProgress = (item: WorkItem) => {
  if (item.quantity > 0 && typeof item.completedQuantity === 'number') {
    return clampProgress((item.completedQuantity / item.quantity) * 100)
  }
  if (item.plannedHours > 0) {
    return clampProgress((item.actualHours / item.plannedHours) * 100)
  }
  return clampProgress(item.progressPercent)
}

export const calculateStageProgress = (stage: WorkStage, workItems: WorkItem[]) => {
  const items = workItems.filter((item) => item.stageId === stage.id)
  const weightedByCost = items.filter((item) => item.totalCost > 0)
  const totalCost = weightedByCost.reduce((sum, item) => sum + item.totalCost, 0)
  if (weightedByCost.length && totalCost > 0) {
    return round1(weightedByCost.reduce((sum, item) => sum + item.totalCost * calculateWorkItemProgress(item), 0) / totalCost)
  }

  const weightedByHours = items.filter((item) => item.plannedHours > 0)
  const totalHours = weightedByHours.reduce((sum, item) => sum + item.plannedHours, 0)
  if (weightedByHours.length && totalHours > 0) {
    return round1(weightedByHours.reduce((sum, item) => sum + item.plannedHours * calculateWorkItemProgress(item), 0) / totalHours)
  }

  return clampProgress(stage.progressPercent)
}

export const calculateProjectMetrics = (data: ProjectProgressData): ProjectProgressMetrics => {
  const totalCost = data.stages.reduce((sum, stage) => sum + stage.totalCost, 0)
  const weightedProgress = totalCost > 0
    ? data.stages.reduce((sum, stage) => sum + stage.totalCost * calculateStageProgress(stage, data.workItems), 0) / totalCost
    : 0

  const today = todayKey()
  const todayWorkerHours = data.workHourAllocations
    .filter((allocation) => allocation.date === today)
    .reduce((sum, allocation) => sum + allocation.hours, 0)
  const plannedHours = data.workItems.reduce((sum, item) => sum + item.plannedHours, 0)
  const allocatedHours = data.workHourAllocations.reduce((sum, allocation) => sum + allocation.hours, 0)
  const actualHours = allocatedHours || data.workItems.reduce((sum, item) => sum + item.actualHours, 0)

  return {
    weightedProgress: round1(weightedProgress),
    activeCrews: data.crews.filter((crew) => crew.activeWorkStageId || crew.activeWorkItemId).length,
    delayedStages: data.stages.filter((stage) => stage.status === 'Delayed').length,
    delayedWorkItems: data.workItems.filter((item) => item.status === 'Delayed').length,
    plannedHours,
    actualHours,
    remainingHours: Math.max(0, round1(plannedHours - actualHours)),
    todayWorkerHours: round1(todayWorkerHours),
    todayReports: data.dailyReports.filter((report) => report.date === today).length,
    materialWarnings: data.materials.filter((material) => material.quantity > 0 && material.remainingQuantity / material.quantity <= 0.15).length,
  }
}

const syncStageFromItems = (stage: WorkStage, workItems: WorkItem[]): WorkStage => {
  const items = workItems.filter((item) => item.stageId === stage.id)
  if (!items.length) return stage
  return {
    ...stage,
    progressPercent: calculateStageProgress(stage, workItems),
    actualHours: items.reduce((sum, item) => sum + item.actualHours, 0),
    plannedHours: items.reduce((sum, item) => sum + item.plannedHours, 0),
  }
}

const recalculateItemTotals = (item: WorkItem): WorkItem => {
  const laborTotal = Math.round(item.quantity * item.laborUnitPrice * 100) / 100
  const materialTotal = Math.round(item.materialQuantity * item.materialUnitPrice * 100) / 100
  const totalCost = Math.round((laborTotal + materialTotal) * 100) / 100
  const progressPercent = calculateWorkItemProgress({ ...item, laborTotal, materialTotal, totalCost })
  return {
    ...item,
    laborTotal,
    materialTotal,
    totalCost,
    progressPercent,
    remainingHours: Math.max(0, round1(item.plannedHours - item.actualHours)),
  }
}

const applyReportProgress = (workItems: WorkItem[], report: DailyForemanReport) =>
  workItems.map((item) => {
    const completed = report.completedWorks.find((work) => work.workItemId === item.id)
    if (!completed) return item
    const completedQuantity = Math.min(item.quantity, (item.completedQuantity ?? 0) + completed.completedQuantity)
    return recalculateItemTotals({
      ...item,
      completedQuantity,
      status: completedQuantity >= item.quantity ? 'Completed' : item.status === 'NotStarted' ? 'InProgress' : item.status,
    })
  })

const removeDummyIlhamWorker = (workers: WorkerAssignment[]) =>
  workers.filter((worker) => !(
    worker.workerName.trim().toLocaleLowerCase('az-AZ') === 'ilham əliyev'
    && ['W-0001', 'W-01-0001'].includes(worker.workerExternalId)
  ))

const mapSiteToObject = (
  site: { id: string; name: string; address?: string; createdAt?: string },
  projectId: string,
  existing?: ConstructionObject,
): ConstructionObject => ({
  id: site.id,
  name: site.name,
  address: site.address ?? existing?.address,
  zone: site.address ?? existing?.zone ?? site.name,
  projectId,
  status: existing?.status ?? 'NotStarted',
  plannedStartDate: existing?.plannedStartDate ?? site.createdAt?.slice(0, 10),
  plannedEndDate: existing?.plannedEndDate,
  clientName: existing?.clientName,
  notes: existing?.notes,
})

const objectBelongsTo = (objectIds: Set<string>) => <T extends { objectId?: string }>(item: T) =>
  !item.objectId || objectIds.has(item.objectId)

const toProjectProgressData = (state: ProjectProgressData): ProjectProgressData => ({
  workspaceTenantId: state.workspaceTenantId,
  projects: state.projects,
  activeProjectId: state.activeProjectId,
  objects: state.objects,
  project: state.project,
  estimateVersions: state.estimateVersions,
  summary: state.summary,
  stages: state.stages,
  workItems: state.workItems,
  crews: state.crews,
  workerAssignments: state.workerAssignments,
  materials: state.materials,
  attendanceSessions: state.attendanceSessions,
  workHourAllocations: state.workHourAllocations,
  dailyReports: state.dailyReports,
  issues: state.issues,
  risks: state.risks,
  assistantMessages: state.assistantMessages,
})

const hasBusinessCollections = (data: Partial<ProjectProgressData>) =>
  Boolean(
    data.projects?.length
    || data.objects?.length
    || data.stages?.length
    || data.workItems?.length
    || data.crews?.length
    || data.workerAssignments?.length
    || data.materials?.length
    || data.dailyReports?.length,
  )

const legacySummary = (data: Partial<ProjectProgressData>) =>
  `Layihə: ${data.projects?.length ?? 0}, obyekt: ${data.objects?.length ?? 0}, smeta sətri: ${data.workItems?.length ?? 0}, briqada: ${data.crews?.length ?? 0}, işçi: ${data.workerAssignments?.length ?? 0}`

interface WorkspaceSaveJob {
  revision: number
  tenantId: string
  serialized: string
  workspace: ProjectProgressData
}

const PROJECT_PROGRESS_SAVE_DEBOUNCE_MS = 800
const PROJECT_PROGRESS_SAVE_RETRY_MS = 8000
const PROJECT_PROGRESS_SAVE_ERROR = 'Layihə dəyişiklikləri serverdə saxlanmadı. Bağlantını yoxlayın və yenidən cəhd edin.'

let lastObservedWorkspace = ''
let lastServerSavedWorkspace = ''
let saveRevision = 0
let saveTimer: ReturnType<typeof setTimeout> | undefined
let saveInFlight = false
let pendingSaveJob: WorkspaceSaveJob | undefined
let suppressWorkspacePersistence = false

const isPersistableWorkspaceTenant = (tenantId?: string): tenantId is string =>
  Boolean(tenantId && tenantId !== 'anonymous' && tenantId !== 'legacy-browser')

const serializeWorkspace = (workspace: ProjectProgressData) => JSON.stringify(workspace)

const currentWorkspace = () => toProjectProgressData(useProjectProgressStore.getState())

const clearSaveTimer = () => {
  if (!saveTimer) return
  clearTimeout(saveTimer)
  saveTimer = undefined
}

const resetWorkspacePersistenceForCurrentState = () => {
  const serialized = serializeWorkspace(currentWorkspace())
  lastObservedWorkspace = serialized
  lastServerSavedWorkspace = serialized
}

const discardQueuedServerSaves = () => {
  clearSaveTimer()
  pendingSaveJob = undefined
}

const formatSaveError = (error: unknown) => {
  if (error instanceof Error && error.message.trim()) {
    return `${PROJECT_PROGRESS_SAVE_ERROR} Texniki məlumat: ${error.message.slice(0, 220)}`
  }
  return PROJECT_PROGRESS_SAVE_ERROR
}

const flushProjectWorkspaceSaveQueue = async () => {
  if (saveInFlight) return
  saveInFlight = true

  try {
    while (pendingSaveJob) {
      const job = pendingSaveJob
      pendingSaveJob = undefined

      if (currentWorkspace().workspaceTenantId !== job.tenantId) continue

      try {
        useProjectProgressStore.setState({
          serverSyncStatus: 'saving',
          serverSyncError: undefined,
          serverPendingSave: true,
        })
        await projectProgressApi.saveWorkspace(job.workspace)
      } catch (error) {
        const latest = currentWorkspace()
        if (latest.workspaceTenantId === job.tenantId) {
          const latestSerialized = serializeWorkspace(latest)
          pendingSaveJob = latestSerialized === job.serialized
            ? job
            : {
                revision: ++saveRevision,
                tenantId: job.tenantId,
                serialized: latestSerialized,
                workspace: latest,
              }

          useProjectProgressStore.setState({
            serverSyncStatus: 'error',
            serverSyncError: formatSaveError(error),
            serverPendingSave: true,
          })
          if (import.meta.env.DEV) console.warn('Project progress workspace save failed', error)
          if (typeof window !== 'undefined') {
            clearSaveTimer()
            saveTimer = window.setTimeout(() => {
              saveTimer = undefined
              void flushProjectWorkspaceSaveQueue()
            }, PROJECT_PROGRESS_SAVE_RETRY_MS)
          }
        }
        return
      }

      const latest = currentWorkspace()
      if (latest.workspaceTenantId !== job.tenantId) continue

      const latestSerialized = serializeWorkspace(latest)
      if (latestSerialized === job.serialized) {
        lastServerSavedWorkspace = job.serialized
        useProjectProgressStore.setState({
          serverSyncStatus: 'ready',
          serverSyncError: undefined,
          serverPendingSave: false,
          serverLastSavedAt: new Date().toISOString(),
        })
      } else {
        pendingSaveJob = {
          revision: ++saveRevision,
          tenantId: job.tenantId,
          serialized: latestSerialized,
          workspace: latest,
        }
      }
    }
  } finally {
    saveInFlight = false
  }
}

const queueServerSave = (workspace: ProjectProgressData, options?: { immediate?: boolean }) => {
  const tenantId = workspace.workspaceTenantId
  if (!isPersistableWorkspaceTenant(tenantId)) return

  const serialized = serializeWorkspace(workspace)
  if (serialized === lastServerSavedWorkspace && !pendingSaveJob) {
    useProjectProgressStore.setState({ serverSyncStatus: 'ready', serverSyncError: undefined, serverPendingSave: false })
    return
  }

  pendingSaveJob = {
    revision: ++saveRevision,
    tenantId,
    serialized,
    workspace,
  }

  useProjectProgressStore.setState({
    serverSyncStatus: 'saving',
    serverSyncError: undefined,
    serverPendingSave: true,
  })

  clearSaveTimer()
  if (options?.immediate) {
    return
  }

  if (typeof window === 'undefined') {
    void flushProjectWorkspaceSaveQueue()
    return
  }

  saveTimer = window.setTimeout(() => {
    saveTimer = undefined
    void flushProjectWorkspaceSaveQueue()
  }, PROJECT_PROGRESS_SAVE_DEBOUNCE_MS)
}

const runWithoutWorkspacePersistence = (operation: () => void) => {
  suppressWorkspacePersistence = true
  try {
    operation()
    resetWorkspacePersistenceForCurrentState()
  } finally {
    suppressWorkspacePersistence = false
  }
}

const normalizeLegacySnapshot = (saved: Partial<ProjectProgressData>): ProjectProgressData => {
  const empty = createEmptyProjectProgressData(saved.workspaceTenantId ?? 'legacy-browser', saved.project?.clientName ?? saved.project?.name)
  return {
    ...empty,
    ...saved,
    projects: saved.projects?.length ? saved.projects : [saved.project ?? empty.project],
    activeProjectId: saved.activeProjectId ?? saved.project?.id ?? empty.activeProjectId,
    objects: saved.objects ?? [],
    estimateVersions: saved.estimateVersions ?? [],
    summary: saved.summary ?? empty.summary,
    stages: saved.stages ?? [],
    workItems: saved.workItems ?? [],
    crews: saved.crews ?? [],
    workerAssignments: saved.workerAssignments ?? [],
    materials: saved.materials ?? [],
    attendanceSessions: saved.attendanceSessions ?? [],
    workHourAllocations: saved.workHourAllocations ?? [],
    dailyReports: saved.dailyReports ?? [],
    issues: saved.issues ?? [],
    risks: saved.risks ?? [],
    assistantMessages: saved.assistantMessages ?? [],
  }
}

export const statusLabel: Record<ProjectWorkStatus, string> = {
  NotStarted: 'Başlamayıb',
  InProgress: 'İcradadır',
  Paused: 'Dayandırılıb',
  Completed: 'Tamamlanıb',
  Delayed: 'Gecikir',
}

export const statusColor: Record<ProjectWorkStatus, string> = {
  NotStarted: 'default',
  InProgress: 'blue',
  Paused: 'orange',
  Completed: 'green',
  Delayed: 'red',
}

export const useProjectProgressStore = create<ProjectProgressState>()(
  persist(
    (set) => ({
        ...createEmptyProjectProgressData('anonymous'),
        serverSyncStatus: 'idle',
        serverPendingSave: false,
        legacyLocalDataAvailable: false,
        prepareWorkspaceForTenant: (tenantId, tenantCode, companyName) => {
        const normalizedTenantCode = tenantCode?.trim().toUpperCase()
        const targetWorkspaceId = normalizedTenantCode === 'DEMO' ? 'DEMO' : tenantId
        if (useProjectProgressStore.getState().workspaceTenantId === targetWorkspaceId) return

        discardQueuedServerSaves()
        runWithoutWorkspacePersistence(() => {
          set((state) => {
            const nextData = createEmptyProjectProgressData(targetWorkspaceId, companyName)

            useProjectSelectionStore.getState().setSelectedProjectId(ALL_PROJECTS_ID)
            return {
              ...nextData,
              serverSyncStatus: 'idle',
              serverSyncError: undefined,
              serverPendingSave: false,
              serverLastSavedAt: undefined,
              legacyLocalDataAvailable: state.legacyLocalDataAvailable,
              legacyLocalSummary: state.legacyLocalSummary,
              legacyLocalSnapshot: state.legacyLocalSnapshot,
            }
          })
        })
      },
        loadFromBackend: async () => {
        set({ serverSyncStatus: 'loading', serverSyncError: undefined, serverPendingSave: false })
        const data = await projectProgressApi.getWorkspace()
        if (!data) {
          set({ serverSyncStatus: 'fallback', serverSyncError: 'Backend əlçatan deyil, lokal/demo məlumat göstərilir.' })
          return false
        }

        discardQueuedServerSaves()
        runWithoutWorkspacePersistence(() => {
          set((state) => ({
            ...state,
            ...data,
            serverSyncStatus: 'ready',
            serverSyncError: undefined,
            serverPendingSave: false,
            serverLastSavedAt: new Date().toISOString(),
            workspaceTenantId: data.workspaceTenantId ?? state.workspaceTenantId,
            assistantMessages: state.assistantMessages.length ? state.assistantMessages : data.assistantMessages ?? [],
          }))
        })
        return true
      },
        saveToBackend: async () => {
        const current = toProjectProgressData(useProjectProgressStore.getState())
        queueServerSave(current, { immediate: true })
        await flushProjectWorkspaceSaveQueue()
      },
        importLegacyLocalData: async () => {
        const snapshot = useProjectProgressStore.getState().legacyLocalSnapshot
        if (!snapshot) return
        set({ serverSyncStatus: 'saving', serverSyncError: undefined, serverPendingSave: true })
        try {
          await projectProgressApi.importLegacyWorkspace(snapshot)
          const imported = await projectProgressApi.getWorkspace()
          discardQueuedServerSaves()
          runWithoutWorkspacePersistence(() => {
            set((state) => ({
              ...state,
              ...(imported ?? snapshot),
              legacyLocalDataAvailable: false,
              legacyLocalSummary: undefined,
              legacyLocalSnapshot: undefined,
              serverSyncStatus: 'ready',
              serverSyncError: undefined,
              serverPendingSave: false,
              serverLastSavedAt: new Date().toISOString(),
            }))
          })
        } catch (error) {
          set({
            serverSyncStatus: 'error',
            serverSyncError: formatSaveError(error),
            serverPendingSave: true,
          })
        }
      },
        dismissLegacyLocalData: () => set({ legacyLocalDataAvailable: false, legacyLocalSummary: undefined, legacyLocalSnapshot: undefined }),
        applyBackendData: (data) => set((state) => {
        const objects = data.objects?.length ? data.objects : state.objects
        return {
          ...state,
          project: data.project ?? state.project,
          projects: data.projects?.length ? data.projects : state.projects,
          activeProjectId: data.activeProjectId ?? state.activeProjectId,
          objects,
          estimateVersions: data.estimateVersions?.length ? data.estimateVersions : state.estimateVersions,
          summary: data.summary ?? state.summary,
          stages: data.stages?.length ? data.stages : state.stages,
          workItems: data.workItems?.length ? data.workItems : state.workItems,
          crews: data.crews?.length ? data.crews : state.crews,
          workerAssignments: data.workerAssignments?.length ? data.workerAssignments : state.workerAssignments,
          materials: data.materials?.length ? data.materials : state.materials,
          attendanceSessions: data.attendanceSessions?.length ? data.attendanceSessions : state.attendanceSessions,
          workHourAllocations: data.workHourAllocations?.length ? data.workHourAllocations : state.workHourAllocations,
          dailyReports: data.dailyReports?.length ? data.dailyReports : state.dailyReports,
          issues: data.issues?.length ? data.issues : state.issues,
          risks: data.risks?.length ? data.risks : state.risks,
        }
      }),
      syncTenantSites: (sites, mode = 'replace') => set((state) => {
        const existingById = new Map(state.objects.map((object) => [object.id, object]))
        const siteObjects = sites.map((site) => mapSiteToObject(site, state.activeProjectId, existingById.get(site.id)))
        const objects = mode === 'merge'
          ? [
              ...state.objects.filter((object) => !sites.some((site) => site.id === object.id)),
              ...siteObjects,
            ]
          : siteObjects
        const objectIds = new Set(objects.map((object) => object.id))
        useProjectSelectionStore.getState().ensureSelectedProjectId([...objectIds])

        if (mode === 'merge') return { objects }

        return {
          objects,
          stages: state.stages.filter(objectBelongsTo(objectIds)),
          workItems: state.workItems.filter(objectBelongsTo(objectIds)),
          crews: state.crews.filter(objectBelongsTo(objectIds)),
          workerAssignments: state.workerAssignments.filter(objectBelongsTo(objectIds)),
          materials: state.materials.filter(objectBelongsTo(objectIds)),
          attendanceSessions: state.attendanceSessions.filter(objectBelongsTo(objectIds)),
          workHourAllocations: state.workHourAllocations.filter(objectBelongsTo(objectIds)),
          dailyReports: state.dailyReports.filter(objectBelongsTo(objectIds)),
          issues: state.issues.filter(objectBelongsTo(objectIds)),
          risks: state.risks.filter(objectBelongsTo(objectIds)),
        }
      }),
      refreshSeedData: () => set((state) => ({ ...projectProgressSeed, workspaceTenantId: state.workspaceTenantId })),
      resetDemoData: () => set((state) => ({ ...projectProgressSeed, workspaceTenantId: state.workspaceTenantId })),
      addObject: (object) => {
        const objectId = createId('object')
        set((state) => ({
          objects: [
            ...state.objects,
            {
              ...object,
              id: objectId,
              projectId: object.projectId ?? state.activeProjectId,
              status: object.status ?? 'NotStarted',
            },
          ],
        }))
        useProjectSelectionStore.getState().setSelectedProjectId(objectId)
        return objectId
      },
      addStage: (stage) => set((state) => ({
        stages: [
          ...state.stages,
          { ...stage, id: createId('stage'), order: state.stages.length + 1 },
        ],
      })),
      updateStage: (stageId, patch) => set((state) => ({
        stages: state.stages.map((stage) => (stage.id === stageId ? { ...stage, ...patch } : stage)),
      })),
      deleteStage: (stageId) => set((state) => ({
        stages: state.stages.filter((stage) => stage.id !== stageId),
        workItems: state.workItems.filter((item) => item.stageId !== stageId),
      })),
      addWorkItem: (item) => {
        const itemId = createId('item')
        set((state) => {
          const workItem = recalculateItemTotals({ ...item, id: itemId })
          const workItems = [...state.workItems, workItem]
          return { workItems, stages: state.stages.map((stage) => syncStageFromItems(stage, workItems)) }
        })
        return itemId
      },
      updateWorkItem: (itemId, patch) => set((state) => {
        const workItems = state.workItems.map((item) => (item.id === itemId ? recalculateItemTotals({ ...item, ...patch }) : item))
        return { workItems, stages: state.stages.map((stage) => syncStageFromItems(stage, workItems)) }
      }),
      deleteWorkItem: (itemId) => set((state) => {
        const workItems = state.workItems.filter((item) => item.id !== itemId)
        return { workItems, stages: state.stages.map((stage) => syncStageFromItems(stage, workItems)) }
      }),
      addCrew: (crew) => set((state) => ({
        crews: [...state.crews, { ...crew, id: createId('crew') }],
      })),
      updateCrew: (crewId, patch) => set((state) => ({
        crews: state.crews.map((crew) => (crew.id === crewId ? { ...crew, ...patch } : crew)),
        workerAssignments: state.workerAssignments.map((worker) => {
          if (worker.crewId !== crewId) return worker
          const activeItem = patch.activeWorkItemId ? state.workItems.find((item) => item.id === patch.activeWorkItemId) : undefined
          return {
            ...worker,
            activeWorkItemId: patch.activeWorkItemId ?? worker.activeWorkItemId,
            activeStageId: activeItem?.stageId ?? patch.activeWorkStageId ?? worker.activeStageId,
          }
        }),
      })),
      deleteCrew: (crewId) => set((state) => ({
        crews: state.crews.filter((crew) => crew.id !== crewId),
        workerAssignments: state.workerAssignments.map((worker) => (worker.crewId === crewId ? { ...worker, crewId: '' } : worker)),
      })),
      addWorker: (worker) => set((state) => ({
        workerAssignments: [...state.workerAssignments, { ...worker, id: createId('worker') }],
      })),
      updateWorker: (workerId, patch) => set((state) => ({
        workerAssignments: state.workerAssignments.map((worker) => (worker.id === workerId ? { ...worker, ...patch } : worker)),
      })),
      deleteWorker: (workerId) => set((state) => ({
        workerAssignments: state.workerAssignments.filter((worker) => worker.id !== workerId),
      })),
      addMaterial: (material) => set((state) => ({
        materials: [
          ...state.materials,
          {
            ...material,
            id: createId('mat'),
            remainingQuantity: Math.max(0, material.quantity - material.usedQuantity),
          },
        ],
      })),
      updateMaterial: (materialId, patch) => set((state) => ({
        materials: state.materials.map((material) => {
          if (material.id !== materialId) return material
          const next = { ...material, ...patch }
          return { ...next, remainingQuantity: Math.max(0, next.quantity - next.usedQuantity) }
        }),
      })),
      deleteMaterial: (materialId) => set((state) => ({
        materials: state.materials.filter((material) => material.id !== materialId),
      })),
      addDailyReport: (report) => set((state) => {
        const dailyReport: DailyForemanReport = {
          ...report,
          id: createId('report'),
          createdAt: new Date().toISOString(),
        }
        const workItems = applyReportProgress(state.workItems, dailyReport)
        return {
          dailyReports: [dailyReport, ...state.dailyReports],
          workItems,
          stages: state.stages.map((stage) => syncStageFromItems(stage, workItems)),
        }
      }),
      updateDailyReport: (reportId, patch) => set((state) => ({
        dailyReports: state.dailyReports.map((report) => (report.id === reportId ? { ...report, ...patch } : report)),
      })),
      deleteDailyReport: (reportId) => set((state) => ({
        dailyReports: state.dailyReports.filter((report) => report.id !== reportId),
      })),
      addAssistantMessage: (message) => set((state) => ({
        assistantMessages: [
          ...state.assistantMessages,
          { ...message, id: createId('msg'), createdAt: new Date().toISOString() },
        ],
      })),
        clearAssistantMessages: () => set({ assistantMessages: [] }),
    }),
    {
      name: 'buildtrack-project-progress',
      partialize: (state) => ({
        workspaceTenantId: state.workspaceTenantId,
        activeProjectId: state.activeProjectId,
        assistantMessages: state.assistantMessages,
        legacyLocalDataAvailable: state.legacyLocalDataAvailable,
        legacyLocalSummary: state.legacyLocalSummary,
      }),
      version: 9,
      migrate: (persisted) => {
        const saved = persisted as Partial<ProjectProgressData>
        const empty = createEmptyProjectProgressData(saved.workspaceTenantId ?? 'anonymous', saved.project?.clientName ?? saved.project?.name)
        const legacySnapshot = hasBusinessCollections(saved)
          ? normalizeLegacySnapshot({ ...saved, workerAssignments: saved.workerAssignments ? removeDummyIlhamWorker(saved.workerAssignments) : [] })
          : undefined

        return {
          ...empty,
          workspaceTenantId: saved.workspaceTenantId ?? empty.workspaceTenantId,
          activeProjectId: saved.activeProjectId ?? ALL_PROJECTS_ID,
          assistantMessages: saved.assistantMessages ?? [],
          serverSyncStatus: 'idle',
          serverPendingSave: false,
          legacyLocalDataAvailable: Boolean(legacySnapshot),
          legacyLocalSummary: legacySnapshot ? legacySummary(legacySnapshot) : undefined,
          legacyLocalSnapshot: legacySnapshot,
        }
      },
    },
  ),
)

useProjectProgressStore.subscribe((state) => {
  const workspace = toProjectProgressData(state)
  const serialized = serializeWorkspace(workspace)
  if (serialized === lastObservedWorkspace) return
  lastObservedWorkspace = serialized

  const tenantId = workspace.workspaceTenantId
  if (suppressWorkspacePersistence) {
    lastServerSavedWorkspace = serialized
    discardQueuedServerSaves()
    return
  }

  if (!isPersistableWorkspaceTenant(tenantId)) return
  if (state.serverSyncStatus === 'idle' || state.serverSyncStatus === 'loading' || state.serverSyncStatus === 'fallback') return

  queueServerSave(workspace)
})
