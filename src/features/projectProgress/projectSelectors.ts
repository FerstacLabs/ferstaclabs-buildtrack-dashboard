import type {
  ConstructionObject,
  Crew,
  DailyForemanReport,
  ProjectProgressData,
  ProjectProgressMetrics,
  ProjectWorkStatus,
  RiskEvent,
  WorkItem,
  WorkStage,
  WorkerAssignment,
} from '../../types/projectProgress'
import { calculateProjectMetrics, calculateStageProgress } from './projectProgressStore'

export const ALL_OBJECTS_ID = 'all'

type ObjectScoped = { objectId?: string }

export interface ProjectPayrollRow {
  id: string
  objectId?: string
  objectName: string
  workerId: string
  workerName: string
  workerExternalId: string
  crewName: string
  role: string
  hourlyRate: number
  normalHours: number
  overtimeHours: number
  approvedHours: number
  riskHours: number
  manualAdjustment: number
  grossAmount: number
  correctionAmount: number
  finalAmount: number
  exportStatus: 'Hazır' | 'Xəta' | 'Xəbərdarlıq' | 'Göndərilib'
}

export interface DashboardSummary extends ProjectProgressMetrics {
  payrollTotal: number
  workerCount: number
  riskWorkerCount: number
}

export interface AttendancePanelRow {
  id: string
  objectId?: string
  objectName: string
  workerId: string
  workerName: string
  workerExternalId: string
  crewName: string
  role: string
  date: string
  firstSeen: string
  lastSeen: string
  totalHours: number
  source: string
  riskScore: number
  status: 'Gəlib' | 'Gecikib' | 'Riskli'
}

export interface DelayRiskRow {
  id: string
  objectId?: string
  objectName: string
  workerName: string
  crewName: string
  role: string
  riskScore: number
  riskLevel: 'Aşağı' | 'Orta' | 'Yüksək' | 'Kritik'
  reason: string
  delayCount: number
  totalDelayMinutes: number
  source: string
}

export interface AuditPanelRow {
  id: string
  objectId?: string
  objectName: string
  prorabName: string
  crewName: string
  period: string
  manualEntries: number
  corrections: number
  riskyApprovals: number
  repeatedWorkerEntries: number
  lateApprovals: number
  auditStatus: 'Uyğun' | 'Yaxşı' | 'Nəzarət lazımdır'
}

export interface ExportPanelRow {
  id: string
  objectId?: string
  objectName: string
  workerName: string
  workerExternalId: string
  crewName: string
  role: string
  approvedHours: number
  finalAmount: number
  accountCode: string
  exportStatus: ProjectPayrollRow['exportStatus']
  errorMessage: string
}

export const normalizeObjectId = (objectId?: string) =>
  objectId && objectId !== ALL_OBJECTS_ID ? objectId : ALL_OBJECTS_ID

export const filterByObject = <T extends ObjectScoped>(rows: T[], objectId?: string) => {
  const normalized = normalizeObjectId(objectId)
  return normalized === ALL_OBJECTS_ID ? rows : rows.filter((row) => row.objectId === normalized)
}

export const getObjects = (data: ProjectProgressData): ConstructionObject[] => data.objects

export const getObjectById = (data: ProjectProgressData, objectId?: string) =>
  data.objects.find((object) => object.id === objectId)

export const getObjectName = (data: ProjectProgressData, objectId?: string) =>
  getObjectById(data, objectId)?.name ?? 'Bütün obyektlər'

const getObjectScopedData = (data: ProjectProgressData, objectId?: string): ProjectProgressData => {
  const normalized = normalizeObjectId(objectId)
  if (normalized === ALL_OBJECTS_ID) return data
  return {
    ...data,
    stages: getStagesByObject(data, normalized),
    workItems: getEstimateRowsByObject(data, normalized),
    crews: getCrewsByObject(data, normalized),
    workerAssignments: getWorkersByObject(data, normalized),
    materials: getMaterialsByObject(data, normalized),
    attendanceSessions: getAttendanceByObject(data, normalized),
    workHourAllocations: filterByObject(data.workHourAllocations, normalized),
    dailyReports: getDailyReportsByObject(data, normalized),
    issues: filterByObject(data.issues, normalized),
    risks: filterByObject(data.risks, normalized),
  }
}

export const getActiveProject = (data: ProjectProgressData) =>
  data.projects.find((project) => project.id === data.activeProjectId) ?? data.project

export const getProjectStages = (data: ProjectProgressData, projectId = data.project.id, objectId = ALL_OBJECTS_ID) =>
  projectId === data.project.id ? getStagesByObject(data, objectId) : []

export const getProjectWorkItems = (data: ProjectProgressData, projectId = data.project.id, objectId = ALL_OBJECTS_ID) =>
  projectId === data.project.id ? getEstimateRowsByObject(data, objectId) : []

export const getProjectCrews = (data: ProjectProgressData, projectId = data.project.id, objectId = ALL_OBJECTS_ID) =>
  projectId === data.project.id ? getCrewsByObject(data, objectId) : []

export const getProjectWorkers = (data: ProjectProgressData, projectId = data.project.id, objectId = ALL_OBJECTS_ID) =>
  filterByObject(data.workerAssignments.filter((worker) => !worker.projectId || worker.projectId === projectId), objectId)

export const getStagesByObject = (data: ProjectProgressData, objectId = ALL_OBJECTS_ID) =>
  filterByObject(data.stages, objectId).slice().sort((a, b) => {
    const objectCompare = (a.objectId ?? '').localeCompare(b.objectId ?? '')
    return objectCompare || a.order - b.order
  })

export const getEstimateRowsByObject = (data: ProjectProgressData, objectId = ALL_OBJECTS_ID) =>
  filterByObject(data.workItems, objectId)

export const getCrewsByObject = (data: ProjectProgressData, objectId = ALL_OBJECTS_ID) =>
  filterByObject(data.crews, objectId)

export const getWorkersByObject = (data: ProjectProgressData, objectId = ALL_OBJECTS_ID) =>
  filterByObject(data.workerAssignments, objectId)

export const getMaterialsByObject = (data: ProjectProgressData, objectId = ALL_OBJECTS_ID) =>
  filterByObject(data.materials, objectId)

export const getDailyReportsByObject = (data: ProjectProgressData, objectId = ALL_OBJECTS_ID) =>
  filterByObject(data.dailyReports, objectId)

export const getAttendanceByObject = (data: ProjectProgressData, objectId = ALL_OBJECTS_ID) =>
  filterByObject(data.attendanceSessions, objectId)

export const getWorkersByCrew = (data: ProjectProgressData, crewId: string, objectId = ALL_OBJECTS_ID) =>
  filterByObject(data.workerAssignments, objectId).filter((worker) => worker.crewId === crewId)

export const getWorkItemActualHours = (data: ProjectProgressData, workItemId: string) =>
  data.workHourAllocations
    .filter((allocation) => allocation.workItemId === workItemId)
    .reduce((sum, allocation) => sum + allocation.hours, 0)

export const getStageActualHours = (data: ProjectProgressData, stageId: string) =>
  data.workHourAllocations
    .filter((allocation) => allocation.stageId === stageId)
    .reduce((sum, allocation) => sum + allocation.hours, 0)

export const getCrewActualHours = (data: ProjectProgressData, crewId: string) =>
  data.workHourAllocations
    .filter((allocation) => allocation.crewId === crewId)
    .reduce((sum, allocation) => sum + allocation.hours, 0)

export const getWorkerTotalHours = (data: ProjectProgressData, workerId: string) => {
  const allocationHours = data.workHourAllocations
    .filter((allocation) => allocation.workerId === workerId)
    .reduce((sum, allocation) => sum + allocation.hours, 0)
  if (allocationHours > 0) return Math.round(allocationHours * 10) / 10

  return Math.round(data.attendanceSessions
    .filter((session) => session.workerId === workerId)
    .reduce((sum, session) => sum + session.totalHours, 0) * 10) / 10
}

export const getWorkerPayroll = (data: ProjectProgressData, worker: WorkerAssignment): ProjectPayrollRow => {
  const hours = getWorkerTotalHours(data, worker.id)
  const overtimeHours = Math.max(0, Math.round((hours - 176) * 10) / 10)
  const normalHours = Math.max(0, Math.round((hours - overtimeHours) * 10) / 10)
  const approvedHours = Math.round((normalHours + overtimeHours) * 10) / 10
  const riskHours = worker.riskScore >= 60 ? Math.round(approvedHours * 0.08 * 10) / 10 : worker.riskScore >= 35 ? Math.round(approvedHours * 0.03 * 10) / 10 : 0
  const grossAmount = Math.round((normalHours * worker.hourlyRate + overtimeHours * worker.hourlyRate * 1.5) * 100) / 100
  const correctionAmount = worker.riskScore >= 70 ? -25 : 0
  const finalAmount = Math.round((grossAmount + correctionAmount) * 100) / 100
  const crewName = data.crews.find((crew) => crew.id === worker.crewId)?.name ?? 'Təyin edilməyib'
  const objectName = getObjectName(data, worker.objectId)

  return {
    id: `payroll-${worker.id}`,
    objectId: worker.objectId,
    objectName,
    workerId: worker.id,
    workerName: worker.workerName,
    workerExternalId: worker.workerExternalId,
    crewName,
    role: worker.role,
    hourlyRate: worker.hourlyRate,
    normalHours,
    overtimeHours,
    approvedHours,
    riskHours,
    manualAdjustment: correctionAmount,
    grossAmount,
    correctionAmount,
    finalAmount,
    exportStatus: worker.status === 'inactive' ? 'Xəbərdarlıq' : worker.riskScore >= 75 ? 'Xəbərdarlıq' : 'Hazır',
  }
}

export const getProjectPayrollRows = (data: ProjectProgressData, projectId = data.project.id, objectId = ALL_OBJECTS_ID) =>
  getProjectWorkers(data, projectId, objectId).map((worker) => getWorkerPayroll(data, worker))

export const getPayrollRowsByObject = (data: ProjectProgressData, objectId = ALL_OBJECTS_ID) =>
  getProjectPayrollRows(data, data.project.id, objectId)

export const getRiskWorkers = (data: ProjectProgressData, projectId = data.project.id, objectId = ALL_OBJECTS_ID) =>
  getProjectWorkers(data, projectId, objectId).filter((worker) => worker.riskScore >= 35)

const riskLevel = (score: number): DelayRiskRow['riskLevel'] => {
  if (score >= 80) return 'Kritik'
  if (score >= 60) return 'Yüksək'
  if (score >= 35) return 'Orta'
  return 'Aşağı'
}

export const getRiskRowsByObject = (data: ProjectProgressData, objectId = ALL_OBJECTS_ID): DelayRiskRow[] => {
  const crewById = new Map(data.crews.map((crew) => [crew.id, crew]))
  const riskEventsByWorker = new Map<string, RiskEvent[]>()
  filterByObject(data.risks, objectId).forEach((risk) => {
    if (!risk.workerId) return
    riskEventsByWorker.set(risk.workerId, [...(riskEventsByWorker.get(risk.workerId) ?? []), risk])
  })

  return getWorkersByObject(data, objectId)
    .filter((worker) => worker.riskScore >= 35 || riskEventsByWorker.has(worker.id))
    .map((worker) => {
      const crew = crewById.get(worker.crewId)
      const events = riskEventsByWorker.get(worker.id) ?? []
      return {
        id: `risk-${worker.id}`,
        objectId: worker.objectId,
        objectName: getObjectName(data, worker.objectId),
        workerName: worker.workerName,
        crewName: crew?.name ?? 'Təyin edilməyib',
        role: worker.role,
        riskScore: worker.riskScore,
        riskLevel: riskLevel(worker.riskScore),
        reason: events[0]?.title ?? (worker.riskScore >= 60 ? 'Gecikmə və manual qeyd riski' : 'Təkrar kamera/prorab qeydləri'),
        delayCount: Math.max(1, Math.round(worker.riskScore / 25)),
        totalDelayMinutes: Math.round(worker.riskScore * 1.6),
        source: events[0]?.source ?? 'attendance',
      }
    })
}

export const getDelayRowsByObject = (data: ProjectProgressData, objectId = ALL_OBJECTS_ID) =>
  getRiskRowsByObject(data, objectId)

export const getAttendanceRowsByObject = (data: ProjectProgressData, objectId = ALL_OBJECTS_ID): AttendancePanelRow[] => {
  const workerById = new Map(data.workerAssignments.map((worker) => [worker.id, worker]))
  const crewById = new Map(data.crews.map((crew) => [crew.id, crew]))
  return getAttendanceByObject(data, objectId).map((session) => {
    const worker = session.workerId ? workerById.get(session.workerId) : undefined
    const crew = worker ? crewById.get(worker.crewId) : undefined
    const riskScore = worker?.riskScore ?? 0
    return {
      id: session.id,
      objectId: session.objectId,
      objectName: getObjectName(data, session.objectId),
      workerId: worker?.id ?? session.workerExternalId,
      workerName: worker?.workerName ?? session.workerExternalId,
      workerExternalId: worker?.workerExternalId ?? session.workerExternalId,
      crewName: crew?.name ?? 'Təyin edilməyib',
      role: worker?.role ?? 'Təyin edilməyib',
      date: session.date,
      firstSeen: session.firstSeen,
      lastSeen: session.lastSeen,
      totalHours: session.totalHours,
      source: session.source,
      riskScore,
      status: riskScore >= 60 ? 'Riskli' : session.totalHours < 7 ? 'Gecikib' : 'Gəlib',
    }
  })
}

const statusRank: Record<ProjectWorkStatus, number> = {
  NotStarted: 0,
  InProgress: 1,
  Paused: 2,
  Completed: 3,
  Delayed: 4,
}

export const getAuditRowsByObject = (data: ProjectProgressData, objectId = ALL_OBJECTS_ID): AuditPanelRow[] => {
  const reports = getDailyReportsByObject(data, objectId)
  const reportCountByCrew = new Map<string, DailyForemanReport[]>()
  reports.forEach((report) => {
    report.crewIds.forEach((crewId) => {
      reportCountByCrew.set(crewId, [...(reportCountByCrew.get(crewId) ?? []), report])
    })
  })

  return getCrewsByObject(data, objectId).map((crew) => {
    const crewReports = reportCountByCrew.get(crew.id) ?? []
    const riskyWorkers = getWorkersByCrew(data, crew.id, objectId).filter((worker) => worker.riskScore >= 35)
    const status: AuditPanelRow['auditStatus'] = riskyWorkers.length > 4 || crew.status === 'Delayed'
      ? 'Nəzarət lazımdır'
      : crewReports.length >= 2
        ? 'Uyğun'
        : 'Yaxşı'
    return {
      id: `audit-${crew.id}`,
      objectId: crew.objectId,
      objectName: getObjectName(data, crew.objectId),
      prorabName: crew.foremanName,
      crewName: crew.name,
      period: 'Cari ay',
      manualEntries: Math.max(0, crewReports.length * 2),
      corrections: Math.max(0, Math.round((crew.progressPercent ?? 0) / 25)),
      riskyApprovals: riskyWorkers.length,
      repeatedWorkerEntries: Math.max(0, riskyWorkers.filter((worker) => worker.riskScore >= 60).length),
      lateApprovals: crew.status === 'Delayed' ? 2 : crew.status === 'Paused' ? 1 : 0,
      auditStatus: status,
    }
  }).sort((a, b) => statusRank[data.crews.find((crew) => crew.id === a.id.replace('audit-', ''))?.status ?? 'InProgress'] - statusRank[data.crews.find((crew) => crew.id === b.id.replace('audit-', ''))?.status ?? 'InProgress'])
}

export const getExportRowsByObject = (data: ProjectProgressData, objectId = ALL_OBJECTS_ID): ExportPanelRow[] =>
  getPayrollRowsByObject(data, objectId).map((row, index) => ({
    id: `export-${row.workerId}`,
    objectId: row.objectId,
    objectName: row.objectName,
    workerName: row.workerName,
    workerExternalId: row.workerExternalId,
    crewName: row.crewName,
    role: row.role,
    approvedHours: row.approvedHours,
    finalAmount: row.finalAmount,
    accountCode: `601.${String(index + 1).padStart(3, '0')}`,
    exportStatus: row.exportStatus,
    errorMessage: row.exportStatus === 'Xəta' ? 'Məlumat uyğunsuzluğu' : '',
  }))

export const getDashboardSummary = (data: ProjectProgressData, projectId = data.project.id, objectId = ALL_OBJECTS_ID): DashboardSummary => {
  const scoped = getObjectScopedData(data, objectId)
  const metrics = calculateProjectMetrics(scoped)
  const payrollRows = getProjectPayrollRows(data, projectId, objectId)
  return {
    ...metrics,
    payrollTotal: payrollRows.reduce((sum, row) => sum + row.finalAmount, 0),
    workerCount: getProjectWorkers(data, projectId, objectId).length,
    riskWorkerCount: getRiskWorkers(data, projectId, objectId).length,
  }
}

export const getAiContextSummary = (data: ProjectProgressData, projectId = data.project.id, objectId = ALL_OBJECTS_ID) => ({
  project: getActiveProject(data),
  selectedObject: getObjectById(data, objectId),
  summary: data.summary,
  metrics: getDashboardSummary(data, projectId, objectId),
  stages: getProjectStages(data, projectId, objectId).map((stage: WorkStage) => ({
    ...stage,
    calculatedProgress: calculateStageProgress(stage, data.workItems),
    actualHours: getStageActualHours(data, stage.id),
  })),
  workItems: getProjectWorkItems(data, projectId, objectId).map((item: WorkItem) => ({
    ...item,
    actualHours: getWorkItemActualHours(data, item.id) || item.actualHours,
  })),
  crews: getProjectCrews(data, projectId, objectId).map((crew: Crew) => ({
    ...crew,
    workerCount: getWorkersByCrew(data, crew.id, objectId).length,
    actualHours: getCrewActualHours(data, crew.id),
  })),
  workers: getProjectWorkers(data, projectId, objectId),
  payrollRows: getProjectPayrollRows(data, projectId, objectId),
  riskWorkers: getRiskWorkers(data, projectId, objectId),
  materials: getMaterialsByObject(data, objectId),
  dailyReports: getDailyReportsByObject(data, objectId),
  risks: filterByObject(data.risks, objectId),
})
