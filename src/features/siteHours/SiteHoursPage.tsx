import { BarChartOutlined, ClockCircleOutlined, DollarCircleOutlined, DownloadOutlined, TeamOutlined } from '@ant-design/icons'
import { Progress } from 'antd'
import type { TableColumnsType } from 'antd'
import { BarChartCard } from '../../components/charts/BarChartCard'
import { FilterBar } from '../../components/layout/FilterBar'
import { DataTable } from '../../components/tables/DataTable'
import { ExplanationCard } from '../../components/ui/ExplanationCard'
import { KpiCard } from '../../components/ui/KpiCard'
import { ToolbarButton } from '../../components/ui/ToolbarButton'
import { PageTitle } from '../../components/ui/PageTitle'
import { siteHoursRows } from '../../services/data/reportCalculations'
import { useBuildTrackStore } from '../../services/data/dataService'
import { exportRowsToCsv, exportRowsToExcel } from '../../services/data/exportService'
import type { SiteHoursRow } from '../../types/reports'
import { formatCurrency, formatHours, formatNumber, formatPercent } from '../../utils/formatters'

export const SiteHoursPage = () => {
  const { data, filters } = useBuildTrackStore()
  if (!data) return null

  const rows = siteHoursRows(data, filters)
  const totals = rows.reduce(
    (acc, row) => ({
      planned: acc.planned + row.planned_workers,
      actual: acc.actual + row.actual_workers,
      hours: acc.hours + row.normal_hours + row.overtime_hours,
      cost: acc.cost + row.labor_cost,
    }),
    { planned: 0, actual: 0, hours: 0, cost: 0 },
  )
  const chartData = rows.map((row) => ({ name: row.site_name, plan: row.normal_hours + row.overtime_hours + row.risky_hours, faktiki: row.normal_hours + row.overtime_hours }))
  const columns: TableColumnsType<SiteHoursRow> = [
    { title: 'Obyekt', dataIndex: 'site_name', sorter: (a, b) => a.site_name.localeCompare(b.site_name) },
    { title: 'Plan İşçi', dataIndex: 'planned_workers', sorter: (a, b) => a.planned_workers - b.planned_workers },
    { title: 'Faktiki İşçi', dataIndex: 'actual_workers', sorter: (a, b) => a.actual_workers - b.actual_workers },
    { title: 'Gəlməyən', dataIndex: 'absent_workers' },
    { title: 'Normal Saat', dataIndex: 'normal_hours' },
    { title: 'Overtime', dataIndex: 'overtime_hours' },
    { title: 'Riskli Saat', dataIndex: 'risky_hours' },
    { title: 'Auto Geofence', dataIndex: 'auto_geofence', render: (value) => formatPercent(value, 0) },
    { title: 'Əmək Xərci', dataIndex: 'labor_cost', render: (value) => formatCurrency(value) },
    { title: 'İcra Faizi', dataIndex: 'execution_percent', render: (value) => <Progress percent={value} size="small" strokeColor="#078b55" /> },
  ]

  return (
    <div className="page-stack">
      <PageTitle title="2. Obyekt Üzrə İş Saatı və Əmək Yükü" />
      <FilterBar data={data} showPosition sitePlaceholder="Bütün Obyekt Qrupları" />

      <section className="kpi-grid">
        <KpiCard icon={<TeamOutlined />} title="Plan İşçi" value={formatNumber(totals.planned)} trend="8 (öncəki aya nisbətən)" tone="green" />
        <KpiCard icon={<TeamOutlined />} title="Faktiki İşçi" value={formatNumber(totals.actual)} trend="12 (öncəki aya nisbətən)" tone="blue" />
        <KpiCard icon={<ClockCircleOutlined />} title="Toplam Saat" value={formatHours(totals.hours, 0)} trend="6% (öncəki aya nisbətən)" tone="blue" />
        <KpiCard icon={<DollarCircleOutlined />} title="Əmək Xərci" value={formatCurrency(totals.cost)} trend="6% (öncəki aya nisbətən)" tone="green" />
      </section>

      <DataTable
        title="Obyektlər üzrə iş saatı və əmək yükü"
        columns={columns}
        data={rows}
        extra={
          <div className="table-actions">
            <ToolbarButton icon={<DownloadOutlined />} onClick={() => exportRowsToExcel('obyekt-saatlari', rows)}>Excel Export</ToolbarButton>
            <ToolbarButton icon={<DownloadOutlined />} tone="purple" onClick={() => exportRowsToCsv('obyekt-saatlari', rows)}>CSV Export</ToolbarButton>
          </div>
        }
      />
      <BarChartCard
        title="Plan vs Faktiki Saat"
        data={chartData}
        bars={[
          { dataKey: 'plan', color: '#1479ff', name: 'Plan Saat' },
          { dataKey: 'faktiki', color: '#078b55', name: 'Faktiki Saat' },
        ]}
      />

      <section className="explanation-grid">
        <ExplanationCard icon={<BarChartOutlined />} title="Əsas sütunlar" tone="blue">
          <ul>
            <li>Plan İşçi və Faktiki İşçi sahədəki resurs fərqini göstərir.</li>
            <li>Overtime və riskli saatlar xərc nəzarəti üçün ayrılır.</li>
            <li>İcra faizi plan saatına görə operativ göstəricidir.</li>
          </ul>
        </ExplanationCard>
        <ExplanationCard icon={<ClockCircleOutlined />} title="Nə üçün istifadə olunur?">
          <ul>
            <li>Obyektlər üzrə işçi sayı və iş saatlarının monitorinqi.</li>
            <li>Əmək xərclərinin obyektlər üzrə təhlili.</li>
            <li>Qərarverməni məlumat əsaslı və sürətli etmək.</li>
          </ul>
        </ExplanationCard>
      </section>
    </div>
  )
}
