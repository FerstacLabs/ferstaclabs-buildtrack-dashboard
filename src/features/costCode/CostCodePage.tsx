import { BarChartOutlined, CheckCircleOutlined, ClockCircleOutlined, DownloadOutlined, TeamOutlined } from '@ant-design/icons'
import type { TableColumnsType } from 'antd'
import { BarChartCard } from '../../components/charts/BarChartCard'
import { DonutChartCard } from '../../components/charts/DonutChartCard'
import { FilterBar } from '../../components/layout/FilterBar'
import { DataTable } from '../../components/tables/DataTable'
import { ExplanationCard } from '../../components/ui/ExplanationCard'
import { KpiCard } from '../../components/ui/KpiCard'
import { ToolbarButton } from '../../components/ui/ToolbarButton'
import { PageTitle } from '../../components/ui/PageTitle'
import { StatusBadge } from '../../components/ui/StatusBadge'
import { costCodeRows } from '../../services/data/reportCalculations'
import { useBuildTrackStore } from '../../services/data/dataService'
import { exportRowsToCsv, exportRowsToExcel } from '../../services/data/exportService'
import type { CostCodeRecord } from '../../types/models'
import { formatCurrency, formatHours, formatNumber, formatPercent } from '../../utils/formatters'

export const CostCodePage = () => {
  const { data, filters } = useBuildTrackStore()
  if (!data) return null

  const rows = costCodeRows(data, filters)
  const plan = rows.reduce((sum, row) => sum + row.planned_hours, 0)
  const actual = rows.reduce((sum, row) => sum + row.actual_hours, 0)
  const productivity = plan ? (actual / plan) * 100 : 0
  const tableRows = rows.map((row, index) => ({ ...row, key: `${row.site_id}-${row.cost_code}-${index}` }))
  const chartData = rows.map((row) => ({ name: row.phase_name, plan: row.planned_hours, faktiki: row.actual_hours, xərc: row.labor_cost, value: row.labor_cost }))
  const columns: TableColumnsType<CostCodeRecord> = [
    { title: 'Obyekt', dataIndex: 'site_id', render: (value) => data.sites.find((site) => site.site_id === value)?.site_name ?? value },
    { title: 'Cost Code', dataIndex: 'cost_code' },
    { title: 'İş Fazası', dataIndex: 'phase_name', sorter: (a, b) => a.phase_name.localeCompare(b.phase_name) },
    { title: 'Briqada', dataIndex: 'brigade' },
    { title: 'Plan Saat', dataIndex: 'planned_hours' },
    { title: 'Faktiki Saat', dataIndex: 'actual_hours' },
    { title: 'Saat Fərqi', dataIndex: 'hour_difference' },
    { title: 'Plan Həcm', render: (_, row) => `${formatNumber(row.planned_quantity, 1)} ${row.unit}` },
    { title: 'Faktiki Həcm', render: (_, row) => `${formatNumber(row.actual_quantity, 1)} ${row.unit}` },
    { title: 'Məhsuldarlıq', dataIndex: 'productivity_percent', render: (value) => formatPercent(value) },
    { title: 'Əmək Xərci', dataIndex: 'labor_cost', render: (value) => formatCurrency(value) },
    { title: 'Status', dataIndex: 'status', render: (value) => <StatusBadge status={value} /> },
  ]

  return (
    <div className="page-stack">
      <PageTitle title="8. İş Fazası və Cost Code Məhsuldarlıq Hesabatı" />
      <FilterBar data={data} advancedFields={['siteId', 'brigade']} />

      <section className="kpi-grid">
        <KpiCard icon={<TeamOutlined />} title="Aktiv Fazalar" value={formatNumber(rows.length)} trend="2 (öncəki aya nisbətən)" tone="green" />
        <KpiCard icon={<ClockCircleOutlined />} title="Plan Saat" value={formatHours(plan, 0)} trend="6% (öncəki aya nisbətən)" tone="blue" />
        <KpiCard icon={<ClockCircleOutlined />} title="Faktiki Saat" value={formatHours(actual, 0)} trend="7% (öncəki aya nisbətən)" tone="orange" />
        <KpiCard icon={<CheckCircleOutlined />} title="Məhsuldarlıq %" value={formatPercent(productivity)} trend="1,3% (öncəki aya nisbətən)" tone="green" />
      </section>

      <DataTable
        title="İş Fazası üzrə Plan-Fakt və Cost Code Analizi"
        columns={columns}
        data={tableRows}
        pageSize={15}
        extra={
          <div className="table-actions">
            <ToolbarButton icon={<DownloadOutlined />} onClick={() => exportRowsToExcel('cost-code-analizi', tableRows)}>Excel Export</ToolbarButton>
            <ToolbarButton icon={<DownloadOutlined />} tone="purple" onClick={() => exportRowsToCsv('cost-code-analizi', tableRows)}>CSV Export</ToolbarButton>
          </div>
        }
      />

      <section className="chart-grid">
        <BarChartCard
          title="Plan vs Faktiki Saat (İş Fazası üzrə)"
          data={chartData}
          bars={[
            { dataKey: 'plan', color: '#1479ff', name: 'Plan Saat' },
            { dataKey: 'faktiki', color: '#078b55', name: 'Faktiki Saat' },
          ]}
        />
        <DonutChartCard title="Fazalar üzrə Xərc Paylanması" data={chartData.slice(0, 8)} centerValue={formatCurrency(rows.reduce((sum, row) => sum + row.labor_cost, 0))} centerLabel="cəmi" />
      </section>

      <section className="explanation-grid">
        <ExplanationCard icon={<BarChartOutlined />} title="Bu tablo niyə lazımdır?">
          <p>Layihədə iş fazalarının plan və faktiki icrasını, məhsuldarlığını və cost code səviyyəsində əmək dəyərini analiz edir.</p>
        </ExplanationCard>
        <ExplanationCard icon={<CheckCircleOutlined />} title="Custom imkanlar" tone="purple">
          <ul>
            <li>Tarix, obyekt, briqada və iş fazası üzrə filtr.</li>
            <li>Cost code üzrə qruplaşdırma və sıralama.</li>
            <li>Plan-fakt fərqləri üçün xəbərdarlıq.</li>
          </ul>
        </ExplanationCard>
      </section>
    </div>
  )
}
