import type { FieldDailyReport, FieldDailyReportLine, FieldDailyReportStatus } from '../../services/api/buildTrackBackendApi'
import { formatNumber } from '../../utils/formatters'

export const managementVisibleReportStatuses = new Set<FieldDailyReportStatus>([
  'Submitted',
  'Approved',
  'NeedsCorrection',
  'Rejected',
])

export const fieldDailyReportStatusColor: Record<FieldDailyReportStatus, string> = {
  Draft: 'default',
  Submitted: 'blue',
  Approved: 'green',
  NeedsCorrection: 'orange',
  Rejected: 'red',
}

export const fieldDailyReportStatusLabel: Record<FieldDailyReportStatus, string> = {
  Draft: 'Qaralama',
  Submitted: 'Təsdiq gözləyir',
  Approved: 'Təsdiqlənib',
  NeedsCorrection: 'Düzəliş tələb olunur',
  Rejected: 'Rədd edilib',
}

export const fieldDailyReportStatusOptions = Object.entries(fieldDailyReportStatusLabel)
  .map(([value, label]) => ({ value, label }))

export const totalDailyReportLineValue = (lines: FieldDailyReportLine[], key: 'workerCount' | 'workHours') =>
  lines.reduce((sum, line) => sum + (Number(line[key]) || 0), 0)

export const dailyReportQuantitySummary = (lines: FieldDailyReportLine[]) => {
  if (lines.length === 0) return '-'
  const byUnit = lines.reduce<Record<string, number>>((acc, line) => {
    const unit = line.unit || '-'
    acc[unit] = (acc[unit] ?? 0) + (Number(line.reportedQuantity) || 0)
    return acc
  }, {})
  return Object.entries(byUnit)
    .map(([unit, value]) => `${formatNumber(value)} ${unit}`)
    .join(', ')
}

export const dailyReportWorkSummary = (lines: FieldDailyReportLine[], visibleCount = 2) =>
  lines.length === 0
    ? '-'
    : lines
      .slice(0, visibleCount)
      .map((line) => line.workName)
      .join(', ') + (lines.length > visibleCount ? ` +${lines.length - visibleCount}` : '')

const reportTimestamp = (report: FieldDailyReport) =>
  report.submittedAt
  ?? report.reviewedAt
  ?? report.updatedAt
  ?? report.createdAt
  ?? ''

export const sortFieldReportsNewestFirst = (reports: FieldDailyReport[]) =>
  [...reports].sort((a, b) => {
    const dateCompare = b.reportDate.localeCompare(a.reportDate)
    if (dateCompare !== 0) return dateCompare
    return reportTimestamp(b).localeCompare(reportTimestamp(a))
  })
