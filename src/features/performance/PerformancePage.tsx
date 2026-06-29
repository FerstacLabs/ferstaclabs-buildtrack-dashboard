import { ClockCircleOutlined, DownloadOutlined, RiseOutlined, SafetyCertificateOutlined, TeamOutlined } from '@ant-design/icons'
import type { TableColumnsType } from 'antd'
import { LineChartCard } from '../../components/charts/LineChartCard'
import { FilterBar } from '../../components/layout/FilterBar'
import { DataTable } from '../../components/tables/DataTable'
import { ExplanationCard } from '../../components/ui/ExplanationCard'
import { KpiCard } from '../../components/ui/KpiCard'
import { ToolbarButton } from '../../components/ui/ToolbarButton'
import { PageTitle } from '../../components/ui/PageTitle'
import { StatusBadge } from '../../components/ui/StatusBadge'
import { performanceRows, trendByDate } from '../../services/data/reportCalculations'
import { useBuildTrackStore } from '../../services/data/dataService'
import { exportRowsToCsv, exportRowsToExcel } from '../../services/data/exportService'
import type { PerformanceRow } from '../../types/reports'
import { formatCurrency, formatHours, formatNumber, formatPercent } from '../../utils/formatters'

export const PerformancePage = () => {
  const { data, filters } = useBuildTrackStore()
  if (!data) return null

  const rows = performanceRows(data, filters)
  const trend = trendByDate(data, filters)
  const attendance = rows.length ? rows.reduce((sum, row) => sum + row.attendance_percent, 0) / rows.length : 0
  const averageLate = rows.length ? rows.reduce((sum, row) => sum + row.average_late, 0) / rows.length : 0
  const riskEvents = rows.reduce((sum, row) => sum + row.risk_events, 0)
  const raises = rows.reduce((sum, row) => sum + row.raise_count, 0)
  const columns: TableColumnsType<PerformanceRow> = [
    { title: 'İşçi adı', dataIndex: 'full_name', sorter: (a, b) => a.full_name.localeCompare(b.full_name) },
    { title: 'Vəzifə', dataIndex: 'position' },
    { title: 'Obyekt/Briqada', dataIndex: 'site_brigade' },
    { title: 'Period', dataIndex: 'period' },
    { title: 'Davamiyyət %', dataIndex: 'attendance_percent', render: (value) => formatPercent(value) },
    { title: 'Orta Gecikmə', dataIndex: 'average_late', render: (value) => `${formatNumber(value)} dəq` },
    { title: 'Risk Hadisəsi', dataIndex: 'risk_events' },
    { title: 'Toplam Saat', dataIndex: 'total_hours', render: (value) => formatHours(value) },
    { title: 'Overtime', dataIndex: 'overtime_hours' },
    { title: 'Maaş Artımı Sayı', dataIndex: 'raise_count' },
    { title: 'Son Artım', dataIndex: 'last_raise' },
    { title: 'Cari Tarif', dataIndex: 'current_rate', render: (value) => formatCurrency(value) },
    { title: 'Performans Statusu', dataIndex: 'performance_status', render: (value) => <StatusBadge status={value} /> },
    { title: 'Tövsiyə', dataIndex: 'recommendation' },
  ]

  return (
    <div className="page-stack">
      <PageTitle title="6. İşçi Performansı və Davamiyyət Trendi" />
      <FilterBar data={data} advancedFields={['dateRange', 'siteId', 'brigade', 'position', 'riskLevel']} />

      <section className="kpi-grid">
        <KpiCard icon={<TeamOutlined />} title="Davamiyyət %" value={formatPercent(attendance)} trend="4,3% (əvvəlki perioda nisbətən)" tone="green" />
        <KpiCard icon={<ClockCircleOutlined />} title="Orta Gecikmə" value={`${formatNumber(averageLate)} dəq`} trend="3 dəq azalma" tone="orange" />
        <KpiCard icon={<SafetyCertificateOutlined />} title="Risk Hadisəsi" value={formatNumber(riskEvents)} trend="6 azalma" tone="red" />
        <KpiCard icon={<RiseOutlined />} title="Maaş Artımı Namizədləri" value={formatNumber(raises)} trend="2 artım" tone="green" />
      </section>

      <DataTable
        title="Performans və Davamiyyət Trendi"
        columns={columns}
        data={rows}
        extra={
          <div className="table-actions">
            <ToolbarButton icon={<DownloadOutlined />} onClick={() => exportRowsToExcel('performans-trendi', rows)}>Excel Export</ToolbarButton>
            <ToolbarButton icon={<DownloadOutlined />} tone="purple" onClick={() => exportRowsToCsv('performans-trendi', rows)}>CSV Export</ToolbarButton>
          </div>
        }
      />
      <LineChartCard
        title="Davamiyyət % və Toplam Saat Trendi"
        data={trend}
        lines={[
          { dataKey: 'davamiyyət', color: '#078b55', name: 'Davamiyyət %' },
          { dataKey: 'saat', color: '#1479ff', name: 'Toplam Saat' },
        ]}
      />

      <section className="explanation-grid">
        <ExplanationCard icon={<RiseOutlined />} title="Nə üçün istifadə olunur?" tone="blue">
          <ul>
            <li>Performans qiymətləndirmələri üçün.</li>
            <li>Bonus və motivasiya qərarları üçün.</li>
            <li>Yüksək riskli və zəif performanslı işçiləri seçmək üçün.</li>
          </ul>
        </ExplanationCard>
        <ExplanationCard icon={<ClockCircleOutlined />} title="Əsas sütunlar" tone="orange">
          <ul>
            <li>Davamiyyət %, Orta Gecikmə və Risk Hadisəsi əsas göstəricilərdir.</li>
            <li>Toplam Saat və Overtime iş yükünü göstərir.</li>
            <li>Tövsiyə sistemi qərar dəstəyi təklif edir.</li>
          </ul>
        </ExplanationCard>
      </section>
    </div>
  )
}
