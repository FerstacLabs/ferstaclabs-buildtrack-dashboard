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
import { projectProgressSeed, villaEstimateSummary } from './projectProgressSeed'

interface ProjectProgressState extends ProjectProgressData {
  applyBackendData: (data: Partial<ProjectProgressData>) => void
  refreshSeedData: () => void
  resetDemoData: () => void
  setSelectedObjectId: (objectId: string) => void
  setSelectedObjectForPage: (pageKey: string, objectId: string) => void
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

const allObjectsId = 'all'

const objectFilterPageKeys = [
  'dashboard',
  'estimate',
  'crews',
  'workers',
  'timeline',
  'dailyReports',
  'materials',
  'attendance',
  'siteHours',
  'riskWorkers',
  'dailyAttendance',
  'delays',
  'payroll',
  'audit',
  'supervisorAudit',
  'export',
  'devices',
  'attendanceLive',
  'securityEvents',
]

const normalizeSelectedObjectId = (objectId: string | undefined, objects: { id: string }[]) =>
  objectId && (objectId === allObjectsId || objects.some((object) => object.id === objectId)) ? objectId : allObjectsId

const createSyncedObjectSelections = (objectId: string, current: Record<string, string> = {}) => ({
  ...current,
  ...Object.fromEntries(objectFilterPageKeys.map((key) => [key, objectId])),
})

const firstSavedObjectSelection = (selections?: Record<string, string>) =>
  selections ? Object.values(selections).find((value) => value && value !== allObjectsId) : undefined

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
      ...projectProgressSeed,
      applyBackendData: (data) => set((state) => {
        const objects = data.objects?.length ? data.objects : state.objects
        const selectedObjectId = normalizeSelectedObjectId(
          data.selectedObjectId ?? data.selectedObjectIdByPage?.dashboard ?? firstSavedObjectSelection(data.selectedObjectIdByPage) ?? state.selectedObjectId,
          objects,
        )
        return {
          ...state,
          project: data.project ?? state.project,
          projects: data.projects?.length ? data.projects : state.projects,
          activeProjectId: data.activeProjectId ?? state.activeProjectId,
          objects,
          selectedObjectId,
          selectedObjectIdByPage: createSyncedObjectSelections(selectedObjectId, data.selectedObjectIdByPage ?? state.selectedObjectIdByPage),
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
      refreshSeedData: () => set(projectProgressSeed),
      resetDemoData: () => set(projectProgressSeed),
      setSelectedObjectId: (objectId) => set((state) => {
        const selectedObjectId = normalizeSelectedObjectId(objectId, state.objects)
        return {
          selectedObjectId,
          selectedObjectIdByPage: createSyncedObjectSelections(selectedObjectId, state.selectedObjectIdByPage),
        }
      }),
      setSelectedObjectForPage: (pageKey, objectId) => set((state) => {
        const selectedObjectId = normalizeSelectedObjectId(objectId, state.objects)
        return {
          selectedObjectId,
          selectedObjectIdByPage: {
            ...createSyncedObjectSelections(selectedObjectId, state.selectedObjectIdByPage),
            [pageKey]: selectedObjectId,
          },
        }
      }),
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
          selectedObjectId: objectId,
          selectedObjectIdByPage: {
            ...createSyncedObjectSelections(objectId, state.selectedObjectIdByPage),
          },
        }))
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
        project: state.project,
        projects: state.projects,
        activeProjectId: state.activeProjectId,
        objects: state.objects,
        selectedObjectId: state.selectedObjectId,
        selectedObjectIdByPage: state.selectedObjectIdByPage,
        estimateVersions: state.estimateVersions,
        summary: state.summary ?? villaEstimateSummary,
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
      }),
      version: 6,
      migrate: (persisted) => {
        const saved = persisted as Partial<ProjectProgressData>
        const hasIncompleteSeedWorkers = !saved.workerAssignments?.length || saved.workerAssignments.length < 20
        const hasIncompleteObjects = !saved.objects?.length || saved.objects.length < projectProgressSeed.objects.length
        const shouldRefreshObjectPortfolio = hasIncompleteObjects || hasIncompleteSeedWorkers
        const objects = shouldRefreshObjectPortfolio ? projectProgressSeed.objects : saved.objects ?? projectProgressSeed.objects
        const selectedObjectId = normalizeSelectedObjectId(saved.selectedObjectId ?? saved.selectedObjectIdByPage?.dashboard ?? firstSavedObjectSelection(saved.selectedObjectIdByPage), objects)
        const workerAssignments = shouldRefreshObjectPortfolio
          ? projectProgressSeed.workerAssignments
          : saved.workerAssignments ?? projectProgressSeed.workerAssignments

        return {
          ...projectProgressSeed,
          ...saved,
          projects: saved.projects?.length ? saved.projects : [saved.project ?? projectProgressSeed.project],
          activeProjectId: saved.activeProjectId ?? saved.project?.id ?? projectProgressSeed.activeProjectId,
          objects,
          selectedObjectId,
          selectedObjectIdByPage: createSyncedObjectSelections(selectedObjectId, saved.selectedObjectIdByPage ?? projectProgressSeed.selectedObjectIdByPage),
          stages: shouldRefreshObjectPortfolio ? projectProgressSeed.stages : saved.stages ?? projectProgressSeed.stages,
          workItems: shouldRefreshObjectPortfolio ? projectProgressSeed.workItems : saved.workItems ?? projectProgressSeed.workItems,
          crews: shouldRefreshObjectPortfolio ? projectProgressSeed.crews : saved.crews ?? projectProgressSeed.crews,
          workerAssignments: removeDummyIlhamWorker(workerAssignments),
          materials: shouldRefreshObjectPortfolio ? projectProgressSeed.materials : saved.materials ?? projectProgressSeed.materials,
          attendanceSessions: shouldRefreshObjectPortfolio ? projectProgressSeed.attendanceSessions : saved.attendanceSessions ?? projectProgressSeed.attendanceSessions,
          workHourAllocations: shouldRefreshObjectPortfolio ? projectProgressSeed.workHourAllocations : saved.workHourAllocations ?? projectProgressSeed.workHourAllocations,
          dailyReports: shouldRefreshObjectPortfolio ? projectProgressSeed.dailyReports : saved.dailyReports ?? projectProgressSeed.dailyReports,
          issues: shouldRefreshObjectPortfolio ? projectProgressSeed.issues : saved.issues ?? projectProgressSeed.issues,
          risks: shouldRefreshObjectPortfolio ? projectProgressSeed.risks : saved.risks ?? projectProgressSeed.risks,
        }
      },
    },
  ),
)
