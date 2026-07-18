export type ProjectWorkStatus = 'NotStarted' | 'InProgress' | 'Paused' | 'Completed' | 'Delayed'

export interface ProjectEstimateSummary {
  totalAmount: number
  laborAmount: number
  materialAmount: number
  hiddenCostAmount: number
  currency: 'AZN'
}

export interface WorkStage {
  id: string
  name: string
  order: number
  totalCost: number
  laborCost: number
  materialCost: number
  plannedStartDate: string
  plannedEndDate: string
  status: ProjectWorkStatus
  progressPercent: number
  assignedCrewId?: string
  plannedHours: number
  actualHours: number
  notes?: string
}

export interface WorkItem {
  id: string
  stageId: string
  name: string
  unit: string
  quantity: number
  laborUnitPrice: number
  laborTotal: number
  materialUnit?: string
  materialQuantity: number
  materialUnitPrice: number
  materialTotal: number
  totalCost: number
  plannedHours: number
  actualHours: number
  assignedCrewId?: string
  status: ProjectWorkStatus
  progressPercent: number
  notes?: string
}

export interface Crew {
  id: string
  name: string
  type: string
  foremanName: string
  workerCount: number
  activeWorkStageId?: string
  activeWorkItemId?: string
  plannedDailyHours: number
  notes?: string
}

export interface WorkerAssignment {
  id: string
  workerName: string
  workerExternalId: string
  crewId: string
  role: string
  plannedDailyHours: number
  activeWorkItemId?: string
}

export interface MaterialItem {
  id: string
  name: string
  unit: string
  quantity: number
  usedQuantity: number
  remainingQuantity: number
  linkedStageId?: string
}

export interface ProjectProgressData {
  summary: ProjectEstimateSummary
  stages: WorkStage[]
  workItems: WorkItem[]
  crews: Crew[]
  workerAssignments: WorkerAssignment[]
  materials: MaterialItem[]
}

export interface ProjectProgressMetrics {
  weightedProgress: number
  activeCrews: number
  delayedStages: number
  plannedHours: number
  actualHours: number
}
