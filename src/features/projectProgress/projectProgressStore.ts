import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import type { Crew, ProjectProgressData, ProjectProgressMetrics, ProjectWorkStatus, WorkItem, WorkStage } from '../../types/projectProgress'
import { projectProgressSeed, villaEstimateSummary } from './projectProgressSeed'

interface ProjectProgressState extends ProjectProgressData {
  applyBackendData: (data: Partial<ProjectProgressData>) => void
  resetDemoData: () => void
  addStage: (stage: Omit<WorkStage, 'id' | 'order'>) => void
  updateStage: (stageId: string, patch: Partial<WorkStage>) => void
  addWorkItem: (item: Omit<WorkItem, 'id'>) => void
  updateWorkItem: (itemId: string, patch: Partial<WorkItem>) => void
  deleteWorkItem: (itemId: string) => void
  addCrew: (crew: Omit<Crew, 'id'>) => void
  updateCrew: (crewId: string, patch: Partial<Crew>) => void
}

const createId = (prefix: string) => `${prefix}-${Date.now()}-${Math.random().toString(16).slice(2, 8)}`

export const calculateStageProgress = (stage: WorkStage, workItems: WorkItem[]) => {
  const items = workItems.filter((item) => item.stageId === stage.id && item.totalCost > 0)
  const total = items.reduce((sum, item) => sum + item.totalCost, 0)
  if (!items.length || total <= 0) return stage.progressPercent
  return Math.round((items.reduce((sum, item) => sum + item.totalCost * item.progressPercent, 0) / total) * 10) / 10
}

export const calculateProjectMetrics = (data: ProjectProgressData): ProjectProgressMetrics => {
  const totalCost = data.stages.reduce((sum, stage) => sum + stage.totalCost, 0)
  const weightedProgress = totalCost > 0
    ? data.stages.reduce((sum, stage) => sum + stage.totalCost * calculateStageProgress(stage, data.workItems), 0) / totalCost
    : 0

  return {
    weightedProgress: Math.round(weightedProgress * 10) / 10,
    activeCrews: data.crews.filter((crew) => crew.activeWorkStageId || crew.activeWorkItemId).length,
    delayedStages: data.stages.filter((stage) => stage.status === 'Delayed').length,
    plannedHours: data.stages.reduce((sum, stage) => sum + stage.plannedHours, 0),
    actualHours: data.stages.reduce((sum, stage) => sum + stage.actualHours, 0),
  }
}

const syncStageFromItems = (stage: WorkStage, workItems: WorkItem[]): WorkStage => ({
  ...stage,
  progressPercent: calculateStageProgress(stage, workItems),
})

const recalculateItemTotals = (item: WorkItem): WorkItem => ({
  ...item,
  laborTotal: Math.round(item.quantity * item.laborUnitPrice * 100) / 100,
  materialTotal: Math.round(item.materialQuantity * item.materialUnitPrice * 100) / 100,
  totalCost: Math.round((item.quantity * item.laborUnitPrice + item.materialQuantity * item.materialUnitPrice) * 100) / 100,
})

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
      applyBackendData: (data) => set((state) => ({
        summary: data.summary ?? state.summary,
        stages: data.stages?.length ? data.stages : state.stages,
        workItems: data.workItems?.length ? data.workItems : state.workItems,
        crews: data.crews?.length ? data.crews : state.crews,
        workerAssignments: data.workerAssignments?.length ? data.workerAssignments : state.workerAssignments,
        materials: data.materials?.length ? data.materials : state.materials,
      })),
      resetDemoData: () => set(projectProgressSeed),
      addStage: (stage) => set((state) => ({
        stages: [
          ...state.stages,
          {
            ...stage,
            id: createId('stage'),
            order: state.stages.length + 1,
          },
        ],
      })),
      updateStage: (stageId, patch) => set((state) => ({
        stages: state.stages.map((stage) => (stage.id === stageId ? { ...stage, ...patch } : stage)),
      })),
      addWorkItem: (item) => set((state) => {
        const workItem = recalculateItemTotals({ ...item, id: createId('item') })
        const workItems = [...state.workItems, workItem]
        return {
          workItems,
          stages: state.stages.map((stage) => (stage.id === item.stageId ? syncStageFromItems(stage, workItems) : stage)),
        }
      }),
      updateWorkItem: (itemId, patch) => set((state) => {
        const workItems = state.workItems.map((item) => (item.id === itemId ? recalculateItemTotals({ ...item, ...patch }) : item))
        return {
          workItems,
          stages: state.stages.map((stage) => syncStageFromItems(stage, workItems)),
        }
      }),
      deleteWorkItem: (itemId) => set((state) => {
        const workItems = state.workItems.filter((item) => item.id !== itemId)
        return {
          workItems,
          stages: state.stages.map((stage) => syncStageFromItems(stage, workItems)),
        }
      }),
      addCrew: (crew) => set((state) => ({
        crews: [...state.crews, { ...crew, id: createId('crew') }],
      })),
      updateCrew: (crewId, patch) => set((state) => ({
        crews: state.crews.map((crew) => (crew.id === crewId ? { ...crew, ...patch } : crew)),
      })),
    }),
    {
      name: 'buildtrack-project-progress',
      partialize: (state) => ({
        summary: state.summary ?? villaEstimateSummary,
        stages: state.stages,
        workItems: state.workItems,
        crews: state.crews,
        workerAssignments: state.workerAssignments,
        materials: state.materials,
      }),
    },
  ),
)
