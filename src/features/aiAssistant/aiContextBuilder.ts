import type {
  ConstructionObject,
  Crew,
  DailyForemanReport,
  MaterialItem,
  ProjectEstimateSummary,
  ProjectIssue,
  ProjectProgressData,
  ProjectWorkStatus,
  RiskEvent,
  WorkItem,
  WorkStage,
  WorkerAssignment,
} from '../../types/projectProgress'
import {
  ALL_OBJECTS_ID,
  getActiveProject,
  getAuditRowsByObject,
  getAttendanceRowsByObject,
  getCrewsByObject,
  getDailyReportsByObject,
  getDashboardSummary,
  getDelayRowsByObject,
  getEstimateRowsByObject,
  getExportRowsByObject,
  getMaterialsByObject,
  getObjectById,
  getObjects,
  getPayrollRowsByObject,
  getRiskRowsByObject,
  getStageActualHours,
  getStagesByObject,
  getWorkersByObject,
  type AttendancePanelRow,
  type AuditPanelRow,
  type DelayRiskRow,
  type ExportPanelRow,
  type ProjectPayrollRow,
} from '../projectProgress/projectSelectors'
import { calculateStageProgress, useProjectProgressStore } from '../projectProgress/projectProgressStore'

export type AiInsightSeverity = 'info' | 'warning' | 'critical' | 'success'

export interface AiRelatedEntity {
  type: string
  id: string
  name: string
}

export interface AiInsight {
  id: string
  title: string
  detail: string
  source: 'material' | 'delay' | 'worker' | 'attendance' | 'daily-report' | 'audit' | 'finance' | 'export' | 'project'
  severity: AiInsightSeverity
  relatedEntities?: AiRelatedEntity[]
}

export interface AiStageContext extends WorkStage {
  objectName: string
  calculatedProgress: number
  actualHoursDerived: number
  planFactGapHours: number
}

export interface AiWorkItemContext extends WorkItem {
  objectName: string
  stageName: string
  actualHoursDerived: number
  planFactGapHours: number
}

export interface AiCrewContext extends Crew {
  objectName: string
  activeStageName?: string
  activeWorkItemName?: string
  actualHoursDerived: number
  plannedHours: number
  planFactGapHours: number
  workers: WorkerAssignment[]
}

export interface AiWorkerContext extends WorkerAssignment {
  objectName: string
  crewName: string
  approvedHours: number
  overtimeHours: number
  payrollAmount: number
}

export interface AiMaterialContext extends MaterialItem {
  objectName: string
  stageName?: string
  workItemName?: string
  usedPercent: number
  remainingPercent: number
  totalValue: number
  isCritical: boolean
}

export interface AiDailyReportContext extends DailyForemanReport {
  objectName: string
  crewNames: string[]
  workedItemNames: string[]
  openIssueCount: number
}

export interface AiProjectContextSummary extends ProjectEstimateSummary {
  activeObjectCount: number
  totalSmetaAmount: number
  totalLaborBudget: number
  totalMaterialBudget: number
  totalHiddenCost: number
  overallProgressPercent: number
  totalPlannedHours: number
  totalActualHours: number
  remainingHours: number
  activeCrewsCount: number
  totalWorkersCount: number
  activeWorkersCount: number
  attendancePercent: number
  delayedStagesCount: number
  pausedStagesCount: number
  riskWorkersCount: number
  payrollGrossTotal: number
  payrollFinalTotal: number
  overtimeHours: number
  materialCriticalCount: number
  openDailyReportIssuesCount: number
  exportReadyCount: number
  exportWarningCount: number
  lastReportDate?: string
}

export interface AiProjectContext {
  selectedObject?: ConstructionObject
  objects: ConstructionObject[]
  project: ReturnType<typeof getActiveProject>
  summary: AiProjectContextSummary
  stages: AiStageContext[]
  workItems: AiWorkItemContext[]
  crews: AiCrewContext[]
  workers: AiWorkerContext[]
  attendance: AttendancePanelRow[]
  payroll: ProjectPayrollRow[]
  payrollRows: ProjectPayrollRow[]
  materials: AiMaterialContext[]
  dailyReports: AiDailyReportContext[]
  risks: DelayRiskRow[]
  delays: DelayRiskRow[]
  audit: AuditPanelRow[]
  exportRows: ExportPanelRow[]
  rawRisks: RiskEvent[]
  issues: ProjectIssue[]
  topInsights: AiInsight[]
}

const round1 = (value: number) => Math.round(value * 10) / 10

const normalizeObjectId = (objectId?: string | null) =>
  objectId && objectId !== ALL_OBJECTS_ID ? objectId : ALL_OBJECTS_ID

const getObjectName = (objects: ConstructionObject[], objectId?: string) =>
  objects.find((object) => object.id === objectId)?.name ?? 'Bütün obyektlər'

const statusIsActive = (status: ProjectWorkStatus) => status !== 'Completed'

const includesExportStatus = (row: ExportPanelRow | ProjectPayrollRow, text: string) =>
  String(row.exportStatus).toLocaleLowerCase('az-AZ').includes(text.toLocaleLowerCase('az-AZ'))

export const buildAiProjectContext = (options?: { objectId?: string | null; data?: ProjectProgressData }): AiProjectContext => {
  const data = options?.data ?? useProjectProgressStore.getState()
  const selectedObjectId = normalizeObjectId(options?.objectId)
  const selectedObject = getObjectById(data, selectedObjectId)
  const objects = selectedObject ? [selectedObject] : getObjects(data)
  const stages = getStagesByObject(data, selectedObjectId)
  const workItems = getEstimateRowsByObject(data, selectedObjectId)
  const crews = getCrewsByObject(data, selectedObjectId)
  const workers = getWorkersByObject(data, selectedObjectId)
  const attendance = getAttendanceRowsByObject(data, selectedObjectId)
  const payroll = getPayrollRowsByObject(data, selectedObjectId)
  const materials = getMaterialsByObject(data, selectedObjectId)
  const dailyReports = getDailyReportsByObject(data, selectedObjectId).slice().sort((a, b) => b.date.localeCompare(a.date))
  const risks = getRiskRowsByObject(data, selectedObjectId)
  const delays = getDelayRowsByObject(data, selectedObjectId)
  const audit = getAuditRowsByObject(data, selectedObjectId)
  const exportRows = getExportRowsByObject(data, selectedObjectId)
  const rawRisks = data.risks.filter((risk) => selectedObjectId === ALL_OBJECTS_ID || risk.objectId === selectedObjectId)
  const issues = data.issues.filter((issue) => selectedObjectId === ALL_OBJECTS_ID || issue.objectId === selectedObjectId)

  const stageById = new Map(data.stages.map((stage) => [stage.id, stage]))
  const workItemById = new Map(data.workItems.map((item) => [item.id, item]))
  const crewById = new Map(data.crews.map((crew) => [crew.id, crew]))
  const payrollByWorkerId = new Map(payroll.map((row) => [row.workerId, row]))

  const aiStages: AiStageContext[] = stages.map((stage) => {
    const actualHoursDerived = getStageActualHours(data, stage.id) || stage.actualHours
    return {
      ...stage,
      objectName: getObjectName(data.objects, stage.objectId),
      calculatedProgress: calculateStageProgress(stage, data.workItems),
      actualHoursDerived,
      planFactGapHours: round1(stage.plannedHours - actualHoursDerived),
    }
  })

  const aiWorkItems: AiWorkItemContext[] = workItems.map((item) => {
    const allocationHours = data.workHourAllocations
      .filter((allocation) => allocation.workItemId === item.id)
      .reduce((sum, allocation) => sum + allocation.hours, 0)
    const actualHoursDerived = allocationHours || item.actualHours
    return {
      ...item,
      objectName: getObjectName(data.objects, item.objectId),
      stageName: stageById.get(item.stageId)?.name ?? 'Etap qeyd edilməyib',
      actualHoursDerived,
      planFactGapHours: round1(item.plannedHours - actualHoursDerived),
    }
  })

  const aiCrews: AiCrewContext[] = crews.map((crew) => {
    const crewWorkers = workers.filter((worker) => worker.crewId === crew.id)
    const relatedItems = aiWorkItems.filter((item) => item.assignedCrewId === crew.id)
    const actualHoursDerived = data.workHourAllocations
      .filter((allocation) => allocation.crewId === crew.id)
      .reduce((sum, allocation) => sum + allocation.hours, 0) || relatedItems.reduce((sum, item) => sum + item.actualHoursDerived, 0)
    const plannedHours = relatedItems.reduce((sum, item) => sum + item.plannedHours, 0)
    return {
      ...crew,
      objectName: getObjectName(data.objects, crew.objectId),
      activeStageName: crew.activeWorkStageId ? stageById.get(crew.activeWorkStageId)?.name : undefined,
      activeWorkItemName: crew.activeWorkItemId ? workItemById.get(crew.activeWorkItemId)?.name : undefined,
      actualHoursDerived: round1(actualHoursDerived),
      plannedHours: round1(plannedHours),
      planFactGapHours: round1(plannedHours - actualHoursDerived),
      workers: crewWorkers,
      workerCount: crewWorkers.length || crew.workerCount,
    }
  })

  const aiWorkers: AiWorkerContext[] = workers.map((worker) => {
    const payrollRow = payrollByWorkerId.get(worker.id)
    return {
      ...worker,
      objectName: getObjectName(data.objects, worker.objectId),
      crewName: crewById.get(worker.crewId)?.name ?? 'Təyin edilməyib',
      approvedHours: payrollRow?.approvedHours ?? 0,
      overtimeHours: payrollRow?.overtimeHours ?? 0,
      payrollAmount: payrollRow?.finalAmount ?? 0,
    }
  })

  const aiMaterials: AiMaterialContext[] = materials.map((material) => {
    const usedPercent = material.quantity > 0 ? round1((material.usedQuantity / material.quantity) * 100) : 0
    const remainingPercent = Math.max(0, round1(100 - usedPercent))
    return {
      ...material,
      objectName: getObjectName(data.objects, material.objectId),
      stageName: material.linkedStageId ? stageById.get(material.linkedStageId)?.name : undefined,
      workItemName: material.linkedWorkItemId ? workItemById.get(material.linkedWorkItemId)?.name : undefined,
      usedPercent,
      remainingPercent,
      totalValue: Math.round(material.quantity * (material.unitPrice ?? 0) * 100) / 100,
      isCritical: material.quantity > 0 && material.remainingQuantity / material.quantity <= 0.15,
    }
  })

  const aiDailyReports: AiDailyReportContext[] = dailyReports.map((report) => ({
    ...report,
    objectName: getObjectName(data.objects, report.objectId),
    crewNames: report.crewIds.map((crewId) => crewById.get(crewId)?.name).filter(Boolean) as string[],
    workedItemNames: report.workedItemIds.map((itemId) => workItemById.get(itemId)?.name).filter(Boolean) as string[],
    openIssueCount: [report.delayReason, report.materialShortage, report.equipmentIssue, report.weatherIssue].filter(Boolean).length,
  }))

  const dashboard = getDashboardSummary(data, data.project.id, selectedObjectId)
  const activeWorkersCount = aiWorkers.filter((worker) => worker.status === 'active').length
  const uniqueAttendanceWorkers = new Set(attendance.map((row) => row.workerId)).size
  const attendancePercent = activeWorkersCount ? round1((uniqueAttendanceWorkers / activeWorkersCount) * 100) : 0
  const stageTotals = aiStages.reduce(
    (acc, stage) => ({
      total: acc.total + stage.totalCost,
      labor: acc.labor + stage.laborCost,
      material: acc.material + stage.materialCost,
    }),
    { total: 0, labor: 0, material: 0 },
  )
  const hiddenCost = selectedObject ? data.summary.hiddenCostAmount / Math.max(1, data.objects.length) : data.summary.hiddenCostAmount
  const openDailyReportIssuesCount = aiDailyReports.reduce((sum, report) => sum + report.openIssueCount, 0)
  const payrollGrossTotal = payroll.reduce((sum, row) => sum + row.grossAmount, 0)
  const payrollFinalTotal = payroll.reduce((sum, row) => sum + row.finalAmount, 0)
  const overtimeHours = payroll.reduce((sum, row) => sum + row.overtimeHours, 0)
  const exportReadyCount = exportRows.filter((row) => includesExportStatus(row, 'haz')).length
  const exportWarningCount = exportRows.filter((row) => includesExportStatus(row, 'xəb') || includesExportStatus(row, 'xй') || includesExportStatus(row, 'warning')).length

  const summary: AiProjectContextSummary = {
    ...data.summary,
    activeObjectCount: objects.filter((object) => statusIsActive(object.status)).length,
    totalSmetaAmount: stageTotals.total,
    totalLaborBudget: stageTotals.labor,
    totalMaterialBudget: stageTotals.material,
    totalHiddenCost: hiddenCost,
    totalAmount: stageTotals.total,
    laborAmount: stageTotals.labor,
    materialAmount: stageTotals.material,
    hiddenCostAmount: hiddenCost,
    overallProgressPercent: dashboard.weightedProgress,
    totalPlannedHours: dashboard.plannedHours,
    totalActualHours: dashboard.actualHours,
    remainingHours: dashboard.remainingHours,
    activeCrewsCount: dashboard.activeCrews,
    totalWorkersCount: aiWorkers.length,
    activeWorkersCount,
    attendancePercent,
    delayedStagesCount: aiStages.filter((stage) => stage.status === 'Delayed').length,
    pausedStagesCount: aiStages.filter((stage) => stage.status === 'Paused').length,
    riskWorkersCount: risks.length,
    payrollGrossTotal,
    payrollFinalTotal,
    overtimeHours,
    materialCriticalCount: aiMaterials.filter((material) => material.isCritical).length,
    openDailyReportIssuesCount,
    exportReadyCount,
    exportWarningCount,
    lastReportDate: aiDailyReports[0]?.date,
  }

  const topInsights = buildTopInsights({
    audit,
    dailyReports: aiDailyReports,
    exportRows,
    materials: aiMaterials,
    payroll,
    risks,
    stages: aiStages,
    summary,
  })

  return {
    selectedObject,
    objects,
    project: getActiveProject(data),
    summary,
    stages: aiStages,
    workItems: aiWorkItems,
    crews: aiCrews,
    workers: aiWorkers,
    attendance,
    payroll,
    payrollRows: payroll,
    materials: aiMaterials,
    dailyReports: aiDailyReports,
    risks,
    delays,
    audit,
    exportRows,
    rawRisks,
    issues,
    topInsights,
  }
}

const buildTopInsights = ({
  audit,
  dailyReports,
  exportRows,
  materials,
  payroll,
  risks,
  stages,
  summary,
}: {
  audit: AuditPanelRow[]
  dailyReports: AiDailyReportContext[]
  exportRows: ExportPanelRow[]
  materials: AiMaterialContext[]
  payroll: ProjectPayrollRow[]
  risks: DelayRiskRow[]
  stages: AiStageContext[]
  summary: AiProjectContextSummary
}): AiInsight[] => {
  const insights: AiInsight[] = []
  const delayedStage = stages
    .filter((stage) => stage.status === 'Delayed' || stage.planFactGapHours > 80)
    .sort((a, b) => b.planFactGapHours - a.planFactGapHours)[0]
  if (delayedStage) {
    insights.push({
      id: `delay-${delayedStage.id}`,
      title: `${delayedStage.objectName}: ${delayedStage.name} qrafik riski yaradır`,
      detail: `Plan/fakt saat fərqi ${Math.max(0, round1(delayedStage.planFactGapHours))} saat, icra ${round1(delayedStage.calculatedProgress)}%-dir.`,
      source: 'delay',
      severity: delayedStage.status === 'Delayed' ? 'critical' : 'warning',
      relatedEntities: [{ type: 'stage', id: delayedStage.id, name: delayedStage.name }],
    })
  }

  const criticalMaterial = materials.filter((material) => material.isCritical).sort((a, b) => a.remainingPercent - b.remainingPercent)[0]
  if (criticalMaterial) {
    insights.push({
      id: `material-${criticalMaterial.id}`,
      title: `${criticalMaterial.name} qalığı kritik səviyyədədir`,
      detail: `${criticalMaterial.objectName} üzrə ${criticalMaterial.remainingQuantity} ${criticalMaterial.unit} qalıb (${criticalMaterial.remainingPercent}%).`,
      source: 'material',
      severity: 'critical',
      relatedEntities: [{ type: 'material', id: criticalMaterial.id, name: criticalMaterial.name }],
    })
  }

  const riskyWorker = risks.sort((a, b) => b.riskScore - a.riskScore)[0]
  if (riskyWorker) {
    insights.push({
      id: `worker-${riskyWorker.id}`,
      title: `${riskyWorker.workerName} üzrə risk balı yüksəkdir`,
      detail: `${riskyWorker.objectName} / ${riskyWorker.crewName}: ${riskyWorker.riskScore} bal, səbəb: ${riskyWorker.reason}.`,
      source: 'worker',
      severity: riskyWorker.riskScore >= 80 ? 'critical' : 'warning',
      relatedEntities: [{ type: 'worker', id: riskyWorker.id, name: riskyWorker.workerName }],
    })
  }

  const reportIssue = dailyReports.find((report) => report.openIssueCount > 0)
  if (reportIssue) {
    insights.push({
      id: `report-${reportIssue.id}`,
      title: `Son prorab hesabatında açıq qeyd var`,
      detail: `${reportIssue.date}, ${reportIssue.foremanName}: ${reportIssue.delayReason ?? reportIssue.materialShortage ?? reportIssue.equipmentIssue ?? reportIssue.weatherIssue}.`,
      source: 'daily-report',
      severity: 'warning',
      relatedEntities: [{ type: 'dailyReport', id: reportIssue.id, name: reportIssue.foremanName }],
    })
  }

  const auditWarning = audit.find((row) => !String(row.auditStatus).toLocaleLowerCase('az-AZ').includes('uy'))
  if (auditWarning) {
    insights.push({
      id: `audit-${auditWarning.id}`,
      title: `${auditWarning.prorabName} audit nəzarəti tələb edir`,
      detail: `${auditWarning.objectName} / ${auditWarning.crewName}: riskli təsdiq ${auditWarning.riskyApprovals}, gec təsdiq ${auditWarning.lateApprovals}.`,
      source: 'audit',
      severity: auditWarning.auditStatus === 'Nəzarət lazımdır' ? 'critical' : 'warning',
    })
  }

  if (summary.payrollFinalTotal > 0) {
    const topPayroll = payroll.slice().sort((a, b) => b.finalAmount - a.finalAmount)[0]
    insights.push({
      id: 'finance-payroll',
      title: `Cari payroll yükü ${Math.round(summary.payrollFinalTotal).toLocaleString('az-AZ')} AZN-dir`,
      detail: topPayroll ? `Ən böyük ödəniş sətiri: ${topPayroll.workerName}, ${Math.round(topPayroll.finalAmount).toLocaleString('az-AZ')} AZN.` : 'Payroll təsdiqi üçün görünən sətirlər hazırdır.',
      source: 'finance',
      severity: summary.exportWarningCount > 0 ? 'warning' : 'info',
    })
  }

  if (exportRows.length && summary.exportReadyCount < exportRows.length) {
    insights.push({
      id: 'export-warning',
      title: `Export təsdiqində əlavə yoxlama lazımdır`,
      detail: `${summary.exportReadyCount}/${exportRows.length} sətir hazır görünür; qalan sətirlər üçün status yoxlanmalıdır.`,
      source: 'export',
      severity: 'warning',
    })
  }

  return insights.slice(0, 6)
}
