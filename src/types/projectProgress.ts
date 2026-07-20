export type ProjectWorkStatus = 'NotStarted' | 'InProgress' | 'Paused' | 'Completed' | 'Delayed'

export type WorkerStatus = 'active' | 'inactive'

export type AttendanceSource = 'Camera' | 'Manual' | 'ForemanTablet'

export type DailyReportStatus = 'Draft' | 'Submitted' | 'Approved' | 'Rejected'

export type WeatherType = 'Günəşli' | 'Yağışlı' | 'Küləkli' | 'Soyuq' | 'İsti'

export type IssueStatus = 'Open' | 'Resolved' | 'Watching'

export type RiskSeverity = 'Low' | 'Medium' | 'High' | 'Critical'

export interface ConstructionObject {
  id: string
  name: string
  zone?: string
  address?: string
  projectId: string
  status: ProjectWorkStatus
}

export interface Project {
  id: string
  name: string
  currency: 'AZN'
  location?: string
  clientName?: string
  createdAt: string
  activeEstimateVersionId: string
}

export interface EstimateVersion {
  id: string
  projectId: string
  name: string
  createdAt: string
  totalAmount: number
  notes?: string
}

export interface ProjectEstimateSummary {
  totalAmount: number
  laborAmount: number
  materialAmount: number
  hiddenCostAmount: number
  currency: 'AZN'
}

export interface WorkStage {
  id: string
  objectId?: string
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
  objectId?: string
  stageId: string
  name: string
  costCode?: string
  unit: string
  quantity: number
  unitPrice?: number
  completedQuantity?: number
  laborUnitPrice: number
  laborTotal: number
  materialUnit?: string
  materialQuantity: number
  materialUnitPrice: number
  materialTotal: number
  totalCost: number
  plannedHours: number
  actualHours: number
  remainingHours?: number
  assignedCrewId?: string
  status: ProjectWorkStatus
  progressPercent: number
  plannedStartDate?: string
  plannedEndDate?: string
  notes?: string
}

export interface Crew {
  id: string
  objectId?: string
  name: string
  type: string
  foremanName: string
  workerCount: number
  workerIds?: string[]
  activeWorkStageId?: string
  activeWorkItemId?: string
  plannedDailyHours: number
  status?: ProjectWorkStatus
  progressPercent?: number
  notes?: string
}

export interface WorkerAssignment {
  id: string
  workerName: string
  workerExternalId: string
  projectId?: string
  objectId?: string
  crewId: string
  role: string
  hourlyRate: number
  plannedDailyHours: number
  activeStageId?: string
  activeWorkItemId?: string
  attendanceSource: AttendanceSource
  status: WorkerStatus
  riskScore: number
  notes?: string
}

export interface MaterialItem {
  id: string
  objectId?: string
  name: string
  unit: string
  quantity: number
  usedQuantity: number
  remainingQuantity: number
  unitPrice?: number
  linkedStageId?: string
  linkedWorkItemId?: string
  deliveryDate?: string
  supplier?: string
  notes?: string
}

export interface AttendanceSession {
  id: string
  workerId?: string
  workerExternalId: string
  projectId: string
  objectId?: string
  date: string
  firstSeen: string
  lastSeen: string
  totalHours: number
  source: AttendanceSource | 'Dahua'
  deviceId?: string
}

export interface WorkHourAllocation {
  id: string
  attendanceSessionId: string
  workerId: string
  projectId: string
  objectId?: string
  crewId: string
  stageId: string
  workItemId: string
  date: string
  hours: number
  allocationPercent: number
  source: 'auto' | 'manual' | 'prorab'
}

export interface DailyReportPhoto {
  id: string
  name: string
  url?: string
}

export interface DailyForemanReportWork {
  workItemId: string
  completedQuantity: number
  notes?: string
}

export interface DailyForemanReport {
  id: string
  projectId: string
  objectId?: string
  date: string
  weather: WeatherType
  foremanName: string
  crewIds: string[]
  workedItemIds: string[]
  completedWorks: DailyForemanReportWork[]
  todayNotes: string
  remainingNotes?: string
  delayReason?: string
  materialShortage?: string
  equipmentIssue?: string
  weatherIssue?: string
  status: DailyReportStatus
  photoCount: number
  photos: DailyReportPhoto[]
  createdAt: string
}

export interface ProjectIssue {
  id: string
  projectId: string
  objectId?: string
  stageId?: string
  workItemId?: string
  type: 'Delay' | 'Schedule' | 'Material' | 'Equipment' | 'Weather' | 'Quality'
  title: string
  severity: RiskSeverity
  status: IssueStatus
  dueDate?: string
  createdAt: string
}

export interface RiskEvent {
  id: string
  projectId: string
  objectId?: string
  stageId?: string
  workerId?: string
  crewId?: string
  title: string
  severity: RiskSeverity
  source: 'attendance' | 'daily-report' | 'schedule' | 'material'
  createdAt: string
}

export interface AiAssistantMessage {
  id: string
  role: 'user' | 'assistant'
  content: string
  createdAt: string
  source?: 'openai' | 'local-fallback'
}

export interface ProjectProgressData {
  projects: Project[]
  activeProjectId: string
  objects: ConstructionObject[]
  selectedObjectIdByPage: Record<string, string>
  project: Project
  estimateVersions: EstimateVersion[]
  summary: ProjectEstimateSummary
  stages: WorkStage[]
  workItems: WorkItem[]
  crews: Crew[]
  workerAssignments: WorkerAssignment[]
  materials: MaterialItem[]
  attendanceSessions: AttendanceSession[]
  workHourAllocations: WorkHourAllocation[]
  dailyReports: DailyForemanReport[]
  issues: ProjectIssue[]
  risks: RiskEvent[]
  assistantMessages: AiAssistantMessage[]
}

export interface ProjectProgressMetrics {
  weightedProgress: number
  activeCrews: number
  delayedStages: number
  delayedWorkItems: number
  plannedHours: number
  actualHours: number
  remainingHours: number
  todayWorkerHours: number
  todayReports: number
  materialWarnings: number
}
