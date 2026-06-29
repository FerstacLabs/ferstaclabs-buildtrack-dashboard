import dayjs from 'dayjs'
import type {
  AttendanceRecord,
  BuildTrackData,
  CostCodeRecord,
  PayrollRecord,
  RiskRecord,
  Site,
  SupervisorAuditRecord,
  Worker,
} from '../../types/models'
import type {
  ChartPoint,
  CustomReportRow,
  DailyAttendanceRow,
  DelayPermissionRow,
  ExportValidationRow,
  PayrollRow,
  PerformanceRow,
  ReportFilters,
  RiskWorkerRow,
  SiteHoursRow,
  TimelineEvent,
} from '../../types/reports'
import { isInDateRange, rangeLabel, toDisplayDate, toDisplayDateTime } from '../../utils/dateUtils'

export const createLookups = (data: BuildTrackData) => {
  const workerById = new Map(data.workers.map((worker) => [worker.worker_id, worker]))
  const siteById = new Map(data.sites.map((site) => [site.site_id, site]))
  const assignmentByWorker = new Map(data.assignments.map((assignment) => [assignment.worker_id, assignment]))

  return { workerById, siteById, assignmentByWorker }
}

export const getFilterOptions = (data: BuildTrackData) => ({
  sites: data.sites.map((site) => ({ label: site.site_name, value: site.site_id })),
  brigades: Array.from(new Set(data.workers.map((worker) => worker.brigade))).map((brigade) => ({
    label: `${brigade} briqadası`,
    value: brigade,
  })),
  positions: Array.from(new Set(data.workers.map((worker) => worker.position))).map((position) => ({
    label: position,
    value: position,
  })),
  supervisors: data.supervisorAuditRecords.map((audit) => ({
    label: audit.supervisor_name,
    value: audit.supervisor_id,
  })),
})

const workerMatches = (worker: Worker | undefined, filters: ReportFilters) => {
  if (!worker) return false
  if (filters.brigade !== 'all' && worker.brigade !== filters.brigade) return false
  if (filters.position !== 'all' && worker.position !== filters.position) return false
  return true
}

const siteMatches = (siteId: string, filters: ReportFilters) => filters.siteId === 'all' || filters.siteId === siteId

export const filterAttendance = (data: BuildTrackData, filters: ReportFilters) => {
  const { workerById } = createLookups(data)
  return data.attendanceRecords.filter((record) => {
    if (!isInDateRange(record.date, filters.dateRange)) return false
    if (!siteMatches(record.site_id, filters)) return false
    if (filters.status !== 'all' && record.status !== filters.status) return false
    if (filters.riskLevel !== 'all' && record.risk_level !== filters.riskLevel) return false
    if (filters.entryMethod !== 'all' && record.entry_method !== filters.entryMethod) return false
    return workerMatches(workerById.get(record.worker_id), filters)
  })
}

export const filterPayroll = (data: BuildTrackData, filters: ReportFilters) => {
  const { workerById } = createLookups(data)
  return data.payrollRecords.filter((record) => {
    if (record.month !== filters.month) return false
    if (!siteMatches(record.site_id, filters)) return false
    if (filters.exportStatus !== 'all' && record.export_status !== filters.exportStatus) return false
    return workerMatches(workerById.get(record.worker_id), filters)
  })
}

export const filterRisk = (data: BuildTrackData, filters: ReportFilters) => {
  const { workerById } = createLookups(data)
  return data.riskRecords.filter((record) => {
    const date = record.date.slice(0, 10)
    if (!isInDateRange(date, filters.dateRange)) return false
    if (!siteMatches(record.site_id, filters)) return false
    if (filters.riskLevel !== 'all' && record.risk_level !== filters.riskLevel) return false
    if (filters.entryMethod !== 'all' && record.entry_method !== filters.entryMethod) return false
    if (filters.supervisor !== 'all') {
      const audit = data.supervisorAuditRecords.find((item) => item.supervisor_id === filters.supervisor)
      if (!audit || audit.site_id !== record.site_id) return false
    }
    return workerMatches(workerById.get(record.worker_id), filters)
  })
}

export const dashboardSummary = (data: BuildTrackData, filters: ReportFilters) => {
  const attendance = filterAttendance(data, filters)
  const payroll = filterPayroll(data, filters)
  const activeWorkerIds = new Set(attendance.filter((record) => record.status !== 'Gəlməyib').map((record) => record.worker_id))
  const absentWorkerIds = new Set(attendance.filter((record) => record.status === 'Gəlməyib').map((record) => `${record.worker_id}-${record.date}`))
  const riskyWorkerIds = new Set(attendance.filter((record) => record.risk_score >= 60).map((record) => record.worker_id))

  return {
    activeWorkers: activeWorkerIds.size,
    absent: absentWorkerIds.size,
    risk: riskyWorkerIds.size,
    totalHours: attendance.reduce((sum, record) => sum + record.worked_hours, 0),
    laborCost: payroll.reduce((sum, record) => sum + record.net_amount, 0),
  }
}

export const dailyAttendanceRows = (data: BuildTrackData, filters: ReportFilters): DailyAttendanceRow[] => {
  const { workerById, siteById } = createLookups(data)
  return filterAttendance(data, filters)
    .slice()
    .sort((a, b) => b.date.localeCompare(a.date) || b.risk_score - a.risk_score)
    .map((record) => {
      const worker = workerById.get(record.worker_id)
      const site = siteById.get(record.site_id)
      return {
        key: record.attendance_id,
        worker_id: record.worker_id,
        full_name: worker?.full_name ?? record.worker_id,
        site_name: site?.site_name ?? record.site_id,
        position: worker?.position ?? '-',
        brigade: worker?.brigade ?? '-',
        planned_check_in: record.planned_check_in,
        actual_check_in: record.actual_check_in || '-',
        planned_check_out: record.planned_check_out,
        actual_check_out: record.actual_check_out || '-',
        status: record.status,
        late_minutes: record.late_minutes,
        worked_hours: record.worked_hours,
        entry_method: record.entry_method,
        risk_score: record.risk_score,
        risk_level: record.risk_level,
      }
    })
}

export const dailySummary = (data: BuildTrackData, filters: ReportFilters) => {
  const records = filterAttendance(data, filters)
  const present = records.filter((record) => record.status === 'Gəlib').length
  const absent = records.filter((record) => record.status === 'Gəlməyib').length
  const late = records.filter((record) => record.status === 'Gecikib').length
  const early = records.filter((record) => record.status === 'Erkən çıxıb').length
  const activeHours = records.reduce((sum, record) => sum + record.worked_hours, 0)
  const total = Math.max(1, records.length)

  return {
    present,
    absent,
    late,
    early,
    activeHours,
    total,
    donut: [
      { name: 'Gələn', value: present },
      { name: 'Gəlməyən', value: absent },
      { name: 'Gecikən', value: late },
      { name: 'Erkən çıxan', value: early },
    ],
  }
}

const activeWorkersForSite = (site: Site, data: BuildTrackData, filters: ReportFilters) =>
  data.assignments.filter((assignment) => {
    if (!assignment.active || assignment.site_id !== site.site_id) return false
    return workerMatches(createLookups(data).workerById.get(assignment.worker_id), filters)
  })

export const siteHoursRows = (data: BuildTrackData, filters: ReportFilters): SiteHoursRow[] => {
  const records = filterAttendance(data, filters)
  const payroll = filterPayroll(data, filters)
  return data.sites
    .filter((site) => siteMatches(site.site_id, filters))
    .map((site) => {
      const siteRecords = records.filter((record) => record.site_id === site.site_id)
      const presentWorkers = new Set(siteRecords.filter((record) => record.status !== 'Gəlməyib').map((record) => record.worker_id))
      const absentWorkers = new Set(siteRecords.filter((record) => record.status === 'Gəlməyib').map((record) => record.worker_id))
      const normalHours = siteRecords.reduce((sum, record) => sum + Math.max(0, record.worked_hours - record.overtime_hours), 0)
      const overtimeHours = siteRecords.reduce((sum, record) => sum + record.overtime_hours, 0)
      const riskyHours = siteRecords.filter((record) => record.risk_score >= 60).reduce((sum, record) => sum + record.worked_hours, 0)
      const geofenceRecords = siteRecords.filter((record) => record.entry_method === 'Mobil App' || record.entry_method === 'Turniket')
      const plannedWorkers = activeWorkersForSite(site, data, filters).length
      const laborCost = payroll.filter((record) => record.site_id === site.site_id).reduce((sum, record) => sum + record.net_amount, 0)

      return {
        key: site.site_id,
        site_name: site.site_name,
        planned_workers: plannedWorkers,
        actual_workers: presentWorkers.size,
        absent_workers: absentWorkers.size,
        normal_hours: Number(normalHours.toFixed(1)),
        overtime_hours: Number(overtimeHours.toFixed(1)),
        risky_hours: Number(riskyHours.toFixed(1)),
        auto_geofence: siteRecords.length ? Math.round((geofenceRecords.length / siteRecords.length) * 100) : 0,
        labor_cost: Number(laborCost.toFixed(2)),
        execution_percent: plannedWorkers ? Math.round((presentWorkers.size / plannedWorkers) * 100) : 0,
      }
    })
}

export const riskWorkerRows = (data: BuildTrackData, filters: ReportFilters): RiskWorkerRow[] => {
  const { workerById, siteById } = createLookups(data)
  return filterRisk(data, filters)
    .slice()
    .sort((a, b) => b.risk_score - a.risk_score)
    .map((record) => {
      const worker = workerById.get(record.worker_id)
      return {
        key: record.risk_id,
        full_name: worker?.full_name ?? record.worker_id,
        site_name: siteById.get(record.site_id)?.site_name ?? record.site_id,
        position: worker?.position ?? '-',
        risk_score: record.risk_score,
        risk_level: record.risk_level,
        risk_reason: record.risk_reason,
        repeat_count: record.repeat_count,
        date: toDisplayDateTime(record.date),
        entry_method: record.entry_method,
        approved_by: data.attendanceRecords.find((attendance) => attendance.worker_id === record.worker_id)?.approved_by ?? '-',
        recommendation: record.recommendation,
      }
    })
}

export const delayPermissionRows = (data: BuildTrackData, filters: ReportFilters): DelayPermissionRow[] => {
  const records = filterAttendance(data, filters)
  const { workerById, siteById, assignmentByWorker } = createLookups(data)
  const grouped = new Map<string, AttendanceRecord[]>()
  records.forEach((record) => {
    const current = grouped.get(record.worker_id) ?? []
    current.push(record)
    grouped.set(record.worker_id, current)
  })

  return Array.from(grouped.entries()).map(([workerId, workerRecords], index) => {
    const worker = workerById.get(workerId)
    const siteId = assignmentByWorker.get(workerId)?.site_id ?? workerRecords[0]?.site_id ?? ''
    const lateMinutes = workerRecords.reduce((sum, record) => sum + record.late_minutes, 0)
    const lateCount = workerRecords.filter((record) => record.late_minutes > 0).length
    const earlyCount = workerRecords.filter((record) => record.early_leave_minutes > 0).length
    const permissionHours = workerRecords.filter((record) => record.status === 'İcazəli').length * 8
    const attended = workerRecords.filter((record) => record.status !== 'Gəlməyib').length
    const attendancePercent = workerRecords.length ? (attended / workerRecords.length) * 100 : 0
    const trend = lateCount > 8 ? 'down' : lateCount > 4 ? 'stable' : 'up'

    return {
      key: workerId,
      full_name: worker?.full_name ?? workerId,
      site_name: siteById.get(siteId)?.site_name ?? '-',
      position: worker?.position ?? '-',
      late_count: lateCount,
      late_minutes: lateMinutes,
      early_count: earlyCount,
      permission_hours: permissionHours,
      attendance_percent: Number(attendancePercent.toFixed(1)),
      trend,
      note: trend === 'up' ? 'Yaxşı' : trend === 'stable' ? 'Nəzarət tələb olunur' : index % 2 ? 'Xəbərdarlıq' : 'Dəstək lazımdır',
    }
  })
}

export const payrollRows = (data: BuildTrackData, filters: ReportFilters): PayrollRow[] => {
  const { workerById, siteById } = createLookups(data)
  return filterPayroll(data, filters).map((record) => {
    const worker = workerById.get(record.worker_id)
    return {
      key: record.payroll_id,
      full_name: worker?.full_name ?? record.worker_id,
      site_name: siteById.get(record.site_id)?.site_name ?? record.site_id,
      position: worker?.position ?? '-',
      salary_type: record.salary_type,
      salary_rate: record.salary_rate,
      normal_hours: record.normal_hours,
      overtime_hours: record.overtime_hours,
      permission_hours: record.permission_hours,
      risky_hours: record.risky_hours,
      approved_hours: record.approved_hours,
      gross_amount: record.gross_amount,
      adjustment: record.adjustment,
      net_amount: record.net_amount,
      export_status: record.export_status,
    }
  })
}

export const performanceRows = (data: BuildTrackData, filters: ReportFilters): PerformanceRow[] => {
  const attendance = filterAttendance(data, filters)
  const risks = filterRisk(data, filters)
  const payroll = filterPayroll(data, filters)
  const { workerById, siteById, assignmentByWorker } = createLookups(data)
  const grouped = new Map<string, AttendanceRecord[]>()
  attendance.forEach((record) => {
    const current = grouped.get(record.worker_id) ?? []
    current.push(record)
    grouped.set(record.worker_id, current)
  })

  return Array.from(grouped.entries())
    .map(([workerId, workerRecords], index) => {
      const worker = workerById.get(workerId)
      const siteId = assignmentByWorker.get(workerId)?.site_id ?? workerRecords[0]?.site_id ?? ''
      const attended = workerRecords.filter((record) => record.status !== 'Gəlməyib').length
      const attendancePercent = workerRecords.length ? (attended / workerRecords.length) * 100 : 0
      const lateRecords = workerRecords.filter((record) => record.late_minutes > 0)
      const averageLate = lateRecords.length ? lateRecords.reduce((sum, record) => sum + record.late_minutes, 0) / lateRecords.length : 0
      const riskEvents = risks.filter((risk) => risk.worker_id === workerId).length
      const totalHours = workerRecords.reduce((sum, record) => sum + record.worked_hours, 0)
      const overtimeHours = workerRecords.reduce((sum, record) => sum + record.overtime_hours, 0)
      const salary = payroll.find((record) => record.worker_id === workerId)
      const performanceStatus: PerformanceRow['performance_status'] =
        attendancePercent >= 94 && riskEvents <= 1
          ? 'Yüksək'
          : attendancePercent >= 86 && riskEvents <= 2
            ? 'Orta'
            : attendancePercent >= 76
              ? 'Aşağı'
              : 'Zəif'

      return {
        key: workerId,
        full_name: worker?.full_name ?? workerId,
        position: worker?.position ?? '-',
        site_brigade: `${siteById.get(siteId)?.site_name ?? '-'} / ${worker?.brigade ?? '-'}`,
        period: rangeLabel(filters.dateRange),
        attendance_percent: Number(attendancePercent.toFixed(1)),
        average_late: Number(averageLate.toFixed(0)),
        risk_events: riskEvents,
        total_hours: Number(totalHours.toFixed(1)),
        overtime_hours: Number(overtimeHours.toFixed(1)),
        raise_count: performanceStatus === 'Yüksək' ? 1 : 0,
        last_raise: performanceStatus === 'Yüksək' ? toDisplayDate(dayjs(filters.dateRange[1]).subtract(index % 80, 'day').format('YYYY-MM-DD')) : '-',
        current_rate: salary?.salary_rate ?? worker?.salary_rate ?? 0,
        performance_status: performanceStatus,
        recommendation:
          performanceStatus === 'Yüksək'
            ? 'Artım verilsin'
            : performanceStatus === 'Orta'
              ? 'İzləmədə saxla'
              : performanceStatus === 'Aşağı'
                ? 'Xəbərdarlıq'
                : 'Yenidən qiymətləndir',
      }
    })
    .sort((a, b) => b.attendance_percent - a.attendance_percent)
}

export const trendByDate = (data: BuildTrackData, filters: ReportFilters): ChartPoint[] => {
  const records = filterAttendance(data, filters)
  const grouped = new Map<string, AttendanceRecord[]>()
  records.forEach((record) => {
    const current = grouped.get(record.date) ?? []
    current.push(record)
    grouped.set(record.date, current)
  })

  return Array.from(grouped.entries()).map(([date, dateRecords]) => {
    const attended = dateRecords.filter((record) => record.status !== 'Gəlməyib').length
    return {
      name: dayjs(date).format('DD MMM'),
      davamiyyət: Number(((attended / Math.max(1, dateRecords.length)) * 100).toFixed(1)),
      saat: Number(dateRecords.reduce((sum, record) => sum + record.worked_hours, 0).toFixed(1)),
      gecikmə: dateRecords.filter((record) => record.late_minutes > 0).length,
      erkən: dateRecords.filter((record) => record.early_leave_minutes > 0).length,
    }
  })
}

export const supervisorRows = (data: BuildTrackData, filters: ReportFilters): SupervisorAuditRecord[] =>
  data.supervisorAuditRecords.filter((record) => {
    if (filters.supervisor !== 'all' && filters.supervisor !== record.supervisor_id) return false
    return siteMatches(record.site_id, filters)
  })

export const supervisorTimeline = (data: BuildTrackData): TimelineEvent[] =>
  data.supervisorAuditRecords.slice(0, 5).map((record, index) => ({
    id: record.supervisor_id,
    date: dayjs('2025-05-31').subtract(index, 'day').format('DD.MM.YYYY HH:mm'),
    title: `${record.supervisor_name} - ${createLookups(data).siteById.get(record.site_id)?.site_name ?? record.site_id}`,
    subtitle: index % 2 ? 'Manual giriş əlavə edildi' : 'Check-out vaxtı dəqiqləşdirildi',
    tone: record.audit_status === 'Riskli' ? 'red' : record.audit_status === 'Nəzarət tələb edir' ? 'orange' : 'green',
  }))

export const costCodeRows = (data: BuildTrackData, filters: ReportFilters): CostCodeRecord[] =>
  data.costCodeRecords.filter((record) => {
    if (!siteMatches(record.site_id, filters)) return false
    return filters.brigade === 'all' || filters.brigade === record.brigade
  })

export const exportValidationRows = (data: BuildTrackData, filters: ReportFilters): ExportValidationRow[] => {
  const payroll = filterPayroll(data, filters)
  const { workerById, siteById } = createLookups(data)

  return payroll.slice(0, 80).map((record, index) => ({
    key: record.payroll_id,
    row_id: `EXP-${String(index + 1).padStart(4, '0')}`,
    full_name: workerById.get(record.worker_id)?.full_name ?? record.worker_id,
    site_name: siteById.get(record.site_id)?.site_name ?? record.site_id,
    cost_code: data.costCodeRecords[index % Math.max(1, data.costCodeRecords.length)]?.cost_code ?? 'CC-00-000',
    salary_type: record.salary_type,
    approved_hours: record.approved_hours,
    net_amount: record.net_amount,
    account_code: `711.${(index % 8) + 1}.${(index % 5) + 10}`,
    export_status: record.export_status,
    error_message: record.export_status === 'Xəta' ? '1C hesab kodu yoxlanmalıdır' : record.export_status === 'Xəbərdarlıq' ? 'Riskli saat təsdiqi gözləyir' : '-',
    checked_at: dayjs().format('DD.MM.YYYY HH:mm'),
  }))
}

const toIsoFromDisplayDate = (value: string) => {
  const [day, month, year] = value.split('.')
  return year && month && day ? `${year}-${month}-${day}` : value
}

const reportTypeFromCategory = (category: string) => {
  const normalized = category.toLowerCase()
  if (normalized.includes('maa')) return 'payroll'
  if (normalized.includes('risk')) return 'risk'
  if (normalized.includes('audit')) return 'audit'
  if (normalized.includes('saat')) return 'hours'
  if (normalized.includes('perform')) return 'performance'
  if (normalized.includes('1c') || normalized.includes('export')) return 'export'
  return 'attendance'
}

export const customReportRows = (data: BuildTrackData, savedRows: CustomReportRow[], filters: ReportFilters): CustomReportRow[] => {
  const siteIds = ['all', ...data.sites.map((site) => site.site_id)]
  const brigades = ['all', ...Array.from(new Set(data.workers.map((worker) => worker.brigade)))]
  const baseReports = [
    { name: 'G\u00fcnl\u00fck davamiyy\u0259t icmal\u0131', category: 'Davamiyy\u0259t', columns: 14, format: 'Excel, PDF' },
    { name: 'Briqada performans analizi', category: 'Performans', columns: 18, format: 'Excel, PDF' },
    { name: 'Maa\u015f hesabat\u0131 detall\u0131', category: 'Maa\u015f', columns: 22, format: 'Excel, CSV' },
    { name: 'Riskli i\u015f\u00e7il\u0259r siyah\u0131s\u0131', category: 'Risk', columns: 12, format: 'Excel' },
    { name: '\u0130caz\u0259 v\u0259 m\u0259zuniyy\u0259t icmal\u0131', category: '\u0130caz\u0259', columns: 15, format: 'Excel, PDF' },
    { name: 'Obyekt \u00fczr\u0259 saat analizi', category: 'Saatlar', columns: 11, format: 'Excel' },
    { name: 'Prorab audit n\u0259tic\u0259l\u0259ri', category: 'Audit', columns: 13, format: 'Excel, CSV' },
    { name: '1C \u00fczr\u0259 hesabat uy\u011funlu\u011fu', category: '1C Export', columns: 20, format: '1C XML' },
  ]

  const generatedRows = Array.from({ length: 24 }, (_, index): CustomReportRow => {
    const template = baseReports[index % baseReports.length]
    const site_id = siteIds[index % siteIds.length]
    const brigade = brigades[index % brigades.length]
    return {
      key: `CR-${index + 1}`,
      name: template.name,
      category: template.category,
      report_type: reportTypeFromCategory(template.category),
      site_id,
      brigade,
      created_at: dayjs('2025-04-01').add(index, 'day').format('DD.MM.YYYY'),
      updated_at: dayjs('2025-05-31').subtract(index % 12, 'day').format('DD.MM.YYYY'),
      filter_count: 3 + (index % 6),
      column_count: template.columns + (index % 3),
      export_format: template.format,
      owner: ['R\u0259\u015fad \u0130.', 'V\u00fcqar H.', 'Elvin H.', 'Samir M.', 'Natiq M.'][index % 5],
      status: index % 9 === 0 ? 'Qaralama' : 'Aktiv',
      last_used: dayjs('2025-05-31').subtract(index % 18, 'day').format('DD.MM.YYYY'),
    }
  })

  return [...savedRows, ...generatedRows].filter((row) => {
    if (filters.reportType !== 'all' && row.report_type !== filters.reportType) return false
    if (filters.status !== 'all' && row.status !== filters.status) return false
    if (filters.siteId !== 'all' && row.site_id !== 'all' && row.site_id !== filters.siteId) return false
    if (filters.brigade !== 'all' && row.brigade !== 'all' && row.brigade !== filters.brigade) return false
    return isInDateRange(toIsoFromDisplayDate(row.created_at), filters.dateRange)
  })
}

export const reportTypeDistribution = (rows: CustomReportRow[]): ChartPoint[] => {
  const grouped = new Map<string, number>()
  rows.forEach((row) => grouped.set(row.category, (grouped.get(row.category) ?? 0) + 1))
  return Array.from(grouped.entries()).map(([name, value]) => ({ name, value }))
}

export const exportTrend = (): ChartPoint[] => [
  { name: 'Dek 2024', export: 86 },
  { name: 'Yan 2025', export: 98 },
  { name: 'Fev 2025', export: 112 },
  { name: 'Mar 2025', export: 124 },
  { name: 'Apr 2025', export: 138 },
  { name: 'May 2025', export: 156 },
]

export const statusDistribution = (items: Array<RiskRecord | PayrollRecord | SupervisorAuditRecord | CostCodeRecord>) => {
  const grouped = new Map<string, number>()
  items.forEach((item) => {
    const status = 'risk_level' in item ? item.risk_level : 'export_status' in item ? item.export_status : 'audit_status' in item ? item.audit_status : item.status
    grouped.set(status, (grouped.get(status) ?? 0) + 1)
  })
  return Array.from(grouped.entries()).map(([name, value]) => ({ name, value }))
}
