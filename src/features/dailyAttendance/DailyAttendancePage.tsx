import { ClockCircleOutlined, DownloadOutlined, TeamOutlined, UserDeleteOutlined } from '@ant-design/icons'
import type { TableColumnsType } from 'antd'
import { FilterBar } from '../../components/layout/FilterBar'
import { DonutChartCard } from '../../components/charts/DonutChartCard'
import { DataTable } from '../../components/tables/DataTable'
import { ExplanationCard } from '../../components/ui/ExplanationCard'
import { KpiCard } from '../../components/ui/KpiCard'
import { PageTitle } from '../../components/ui/PageTitle'
import { RiskBadge } from '../../components/ui/RiskBadge'
import { StatusBadge } from '../../components/ui/StatusBadge'
import { ToolbarButton } from '../../components/ui/ToolbarButton'
import { dailyAttendanceRows, dailySummary } from '../../services/data/reportCalculations'
import { useBuildTrackStore } from '../../services/data/dataService'
import { exportRowsToExcel } from '../../services/data/exportService'
import type { DailyAttendanceRow } from '../../types/reports'
import { formatHours, formatNumber, formatPercent } from '../../utils/formatters'

export const DailyAttendancePage = () => {
  const { data, filters } = useBuildTrackStore()
  if (!data) return null

  const rows = dailyAttendanceRows(data, filters)
  const summary = dailySummary(data, filters)
  const columns: TableColumnsType<DailyAttendanceRow> = [
    { title: 'İşçi ID', dataIndex: 'worker_id', sorter: (a, b) => a.worker_id.localeCompare(b.worker_id) },
    { title: 'İşçi adı', dataIndex: 'full_name', sorter: (a, b) => a.full_name.localeCompare(b.full_name) },
    { title: 'Obyekt', dataIndex: 'site_name' },
    { title: 'Vəzifə', dataIndex: 'position' },
    { title: 'Briqada', dataIndex: 'brigade' },
    { title: 'Plan Giriş', dataIndex: 'planned_check_in' },
    { title: 'Faktiki Giriş', dataIndex: 'actual_check_in' },
    { title: 'Plan Çıxış', dataIndex: 'planned_check_out' },
    { title: 'Faktiki Çıxış', dataIndex: 'actual_check_out' },
    { title: 'Status', dataIndex: 'status', render: (status) => <StatusBadge status={status} /> },
    { title: 'Gecikmə', dataIndex: 'late_minutes', sorter: (a, b) => a.late_minutes - b.late_minutes },
    { title: 'İşlənmiş Saat', dataIndex: 'worked_hours', sorter: (a, b) => a.worked_hours - b.worked_hours },
    { title: 'Giriş Metodu', dataIndex: 'entry_method' },
    { title: 'Risk', dataIndex: 'risk_level', render: (_, row) => <RiskBadge level={row.risk_level} score={row.risk_score} /> },
  ]

  return (
    <div className="page-stack">
      <PageTitle title="1. Günlük Davamiyyət Paneli" />
      <FilterBar data={data} showStatus advancedFields={['dateRange', 'siteId', 'brigade', 'status', 'entryMethod', 'riskLevel']} />

      <section className="kpi-grid">
        <KpiCard icon={<TeamOutlined />} title="Bugün Gələn" value={formatNumber(summary.present)} trend={formatPercent((summary.present / summary.total) * 100)} tone="green" />
        <KpiCard icon={<UserDeleteOutlined />} title="Gəlməyən" value={formatNumber(summary.absent)} trend={formatPercent((summary.absent / summary.total) * 100)} tone="red" />
        <KpiCard icon={<ClockCircleOutlined />} title="Gecikən" value={formatNumber(summary.late)} trend={formatPercent((summary.late / summary.total) * 100)} tone="orange" />
        <KpiCard icon={<ClockCircleOutlined />} title="Aktiv Saat" value={formatHours(summary.activeHours, 0)} trend="gələnlər üzrə" tone="blue" />
      </section>

      <section className="content-grid">
        <DataTable
          title="Günlük Davamiyyət Siyahısı"
          columns={columns}
          data={rows}
          extra={<ToolbarButton icon={<DownloadOutlined />} onClick={() => exportRowsToExcel('gunluk-davamiyyet', rows)}>Export</ToolbarButton>}
        />
        <DonutChartCard title="Status üzrə xülasə" data={summary.donut} centerValue={formatNumber(summary.total)} centerLabel="cəmi nəfər" />
      </section>

      <section className="explanation-grid">
        <ExplanationCard icon={<TeamOutlined />} title="Bu tablo niyə lazımdır?">
          <ul>
            <li>Gündəlik davamiyyətin real vaxtda izlənməsi üçün.</li>
            <li>Gecikmə, gəlməmə və erkən çıxış hallarını dərhal görmək üçün.</li>
            <li>Maaş hesablamasına gedən iş saatlarını dəqiqləşdirmək üçün.</li>
          </ul>
        </ExplanationCard>
        <ExplanationCard icon={<DownloadOutlined />} title="Custom imkanlar" tone="orange">
          <ul>
            <li>Tarix, obyekt, briqada və status üzrə filtrləmə.</li>
            <li>Sütunların sıralanması və Excel export.</li>
            <li>Riskli işçilərin operativ seçilməsi.</li>
          </ul>
        </ExplanationCard>
      </section>
    </div>
  )
}
