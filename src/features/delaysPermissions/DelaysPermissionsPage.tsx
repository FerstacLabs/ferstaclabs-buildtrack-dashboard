import { CheckCircleOutlined, ClockCircleOutlined, DownloadOutlined, LoginOutlined, QuestionCircleOutlined } from '@ant-design/icons'
import type { TableColumnsType } from 'antd'
import { LineChartCard } from '../../components/charts/LineChartCard'
import { FilterBar } from '../../components/layout/FilterBar'
import { DataTable } from '../../components/tables/DataTable'
import { ExplanationCard } from '../../components/ui/ExplanationCard'
import { KpiCard } from '../../components/ui/KpiCard'
import { ToolbarButton } from '../../components/ui/ToolbarButton'
import { PageTitle } from '../../components/ui/PageTitle'
import { delayPermissionRows, trendByDate } from '../../services/data/reportCalculations'
import { useBuildTrackStore } from '../../services/data/dataService'
import { exportRowsToCsv, exportRowsToExcel } from '../../services/data/exportService'
import type { DelayPermissionRow } from '../../types/reports'
import { formatNumber, formatPercent } from '../../utils/formatters'

export const DelaysPermissionsPage = () => {
  const { data, filters } = useBuildTrackStore()
  if (!data) return null

  const rows = delayPermissionRows(data, filters)
  const trend = trendByDate(data, filters)
  const lateCount = rows.reduce((sum, row) => sum + row.late_count, 0)
  const lateMinutes = rows.reduce((sum, row) => sum + row.late_minutes, 0)
  const earlyCount = rows.reduce((sum, row) => sum + row.early_count, 0)
  const attendance = rows.length ? rows.reduce((sum, row) => sum + row.attendance_percent, 0) / rows.length : 0
  const columns: TableColumnsType<DelayPermissionRow> = [
    { title: 'İşçi adı', dataIndex: 'full_name', sorter: (a, b) => a.full_name.localeCompare(b.full_name) },
    { title: 'Obyekt', dataIndex: 'site_name' },
    { title: 'Vəzifə', dataIndex: 'position' },
    { title: 'Gecikmə Sayı', dataIndex: 'late_count', sorter: (a, b) => a.late_count - b.late_count },
    { title: 'Ümumi Gecikmə', dataIndex: 'late_minutes' },
    { title: 'Erkən Çıxış Sayı', dataIndex: 'early_count' },
    { title: 'İcazə Saat/Gün', dataIndex: 'permission_hours' },
    { title: 'Davamiyyət %', dataIndex: 'attendance_percent', render: (value) => formatPercent(value) },
    { title: 'Trend', dataIndex: 'trend', render: (value) => <span className={`trend-pill trend-${value}`}>{value === 'up' ? 'Yaxşı' : value === 'stable' ? 'Sabit' : 'Risk'}</span> },
    { title: 'Qeyd', dataIndex: 'note' },
  ]

  return (
    <div className="page-stack">
      <PageTitle title="4. Gecikmə, Erkən Çıxış və İcazələr" />
      <FilterBar data={data} showPosition advancedFields={['dateRange', 'siteId', 'brigade', 'position', 'status', 'entryMethod']} />

      <section className="kpi-grid">
        <KpiCard icon={<ClockCircleOutlined />} title="Gecikmə Sayı" value={formatNumber(lateCount)} trend="8% (öncəki aya nisbətən)" tone="green" />
        <KpiCard icon={<ClockCircleOutlined />} title="Ümumi Gecikmə Dəq" value={formatNumber(lateMinutes)} trend="6% (öncəki aya nisbətən)" tone="blue" />
        <KpiCard icon={<LoginOutlined />} title="Erkən Çıxış" value={formatNumber(earlyCount)} trend="5% (öncəki aya nisbətən)" tone="orange" />
        <KpiCard icon={<CheckCircleOutlined />} title="Davamiyyət Faizi" value={formatPercent(attendance)} trend="2,1% (öncəki aya nisbətən)" tone="green" />
      </section>

      <DataTable
        title="Gecikmə və İcazə Hesabatı"
        columns={columns}
        data={rows}
        extra={
          <div className="table-actions">
            <ToolbarButton icon={<DownloadOutlined />} onClick={() => exportRowsToExcel('gecikme-icaze', rows)}>Excel Export</ToolbarButton>
            <ToolbarButton icon={<DownloadOutlined />} tone="purple" onClick={() => exportRowsToCsv('gecikme-icaze', rows)}>CSV Export</ToolbarButton>
          </div>
        }
      />
      <LineChartCard
        title="Gecikmə və Erkən Çıxış Trendi"
        data={trend}
        lines={[
          { dataKey: 'gecikmə', color: '#078b55', name: 'Gecikmə Sayı' },
          { dataKey: 'saat', color: '#1479ff', name: 'Ümumi Saat' },
          { dataKey: 'erkən', color: '#ff8a00', name: 'Erkən Çıxış Sayı' },
        ]}
      />

      <section className="explanation-grid">
        <ExplanationCard icon={<QuestionCircleOutlined />} title="Bu tablo niyə lazımdır?">
          <p>İşçilərin zaman intizamını və icazə istifadəsini izləmək üçün əsas idarəetmə vasitəsidir.</p>
        </ExplanationCard>
        <ExplanationCard icon={<ClockCircleOutlined />} title="Custom imkanlar" tone="orange">
          <ul>
            <li>Tarix, obyekt, işçi və vəzifə üzrə filtr.</li>
            <li>Gecikmə limiti keçildikdə xəbərdarlıq.</li>
            <li>Excel və CSV formatında ixrac imkanı.</li>
          </ul>
        </ExplanationCard>
      </section>
    </div>
  )
}
