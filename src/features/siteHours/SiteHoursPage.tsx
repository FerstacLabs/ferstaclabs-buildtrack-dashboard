import { BarChartOutlined, ClockCircleOutlined, DollarCircleOutlined, DownloadOutlined, TeamOutlined } from '@ant-design/icons'
import { Progress } from 'antd'
import type { TableColumnsType } from 'antd'
import { BarChartCard } from '../../components/charts/BarChartCard'
import { ObjectFilter } from '../../components/filters/ObjectFilter'
import { DataTable } from '../../components/tables/DataTable'
import { ExplanationCard } from '../../components/ui/ExplanationCard'
import { KpiCard } from '../../components/ui/KpiCard'
import { ToolbarButton } from '../../components/ui/ToolbarButton'
import { PageTitle } from '../../components/ui/PageTitle'
import { exportRowsToCsv, exportRowsToExcel } from '../../services/data/exportService'
import { formatCurrency, formatHours, formatNumber, formatPercent } from '../../utils/formatters'
import { ALL_OBJECTS_ID, getObjects, getPayrollRowsByObject, getStagesByObject, getWorkersByObject } from '../projectProgress/projectSelectors'
import { useProjectProgressStore } from '../projectProgress/projectProgressStore'

interface SiteHoursPanelRow {
  id: string
  objectName: string
  plannedWorkers: number
  actualWorkers: number
  absentWorkers: number
  normalHours: number
  overtimeHours: number
  riskyHours: number
  autoGeofence: number
  laborCost: number
  executionPercent: number
}

export const SiteHoursPage = () => {
  const store = useProjectProgressStore()
  const selectedObjectId = store.selectedObjectIdByPage.siteHours ?? ALL_OBJECTS_ID
  const objects = selectedObjectId === ALL_OBJECTS_ID ? getObjects(store) : getObjects(store).filter((object) => object.id === selectedObjectId)
  const rows: SiteHoursPanelRow[] = objects.map((object) => {
    const workers = getWorkersByObject(store, object.id)
    const payrollRows = getPayrollRowsByObject(store, object.id)
    const stages = getStagesByObject(store, object.id)
    const plannedHours = stages.reduce((sum, stage) => sum + stage.plannedHours, 0)
    const actualHours = payrollRows.reduce((sum, row) => sum + row.approvedHours, 0)
    const riskyHours = payrollRows.reduce((sum, row) => sum + row.riskHours, 0)
    return {
      id: object.id,
      objectName: object.name,
      plannedWorkers: workers.length,
      actualWorkers: workers.filter((worker) => worker.status === 'active').length,
      absentWorkers: workers.filter((worker) => worker.status === 'inactive').length,
      normalHours: payrollRows.reduce((sum, row) => sum + row.normalHours, 0),
      overtimeHours: payrollRows.reduce((sum, row) => sum + row.overtimeHours, 0),
      riskyHours,
      autoGeofence: Math.max(70, Math.min(99, 95 - riskyHours / Math.max(1, actualHours) * 20)),
      laborCost: payrollRows.reduce((sum, row) => sum + row.finalAmount, 0),
      executionPercent: Math.round(Math.min(100, (actualHours / Math.max(1, plannedHours)) * 100)),
    }
  })
  const totals = rows.reduce(
    (acc, row) => ({
      planned: acc.planned + row.plannedWorkers,
      actual: acc.actual + row.actualWorkers,
      hours: acc.hours + row.normalHours + row.overtimeHours,
      cost: acc.cost + row.laborCost,
    }),
    { planned: 0, actual: 0, hours: 0, cost: 0 },
  )
  const chartData = rows.map((row) => ({ name: row.objectName, plan: row.normalHours + row.overtimeHours + row.riskyHours, faktiki: row.normalHours + row.overtimeHours }))

  const columns: TableColumnsType<SiteHoursPanelRow> = [
    { title: 'Obyekt', dataIndex: 'objectName', sorter: (a, b) => a.objectName.localeCompare(b.objectName) },
    { title: 'Plan İşçi', dataIndex: 'plannedWorkers', sorter: (a, b) => a.plannedWorkers - b.plannedWorkers },
    { title: 'Faktiki İşçi', dataIndex: 'actualWorkers', sorter: (a, b) => a.actualWorkers - b.actualWorkers },
    { title: 'Gəlməyən', dataIndex: 'absentWorkers' },
    { title: 'Normal Saat', dataIndex: 'normalHours', render: (value) => formatHours(Number(value), 1) },
    { title: 'Overtime', dataIndex: 'overtimeHours', render: (value) => formatHours(Number(value), 1) },
    { title: 'Riskli Saat', dataIndex: 'riskyHours', render: (value) => formatHours(Number(value), 1) },
    { title: 'Auto Geofence', dataIndex: 'autoGeofence', render: (value) => formatPercent(Number(value), 0) },
    { title: 'Əmək Xərci', dataIndex: 'laborCost', render: (value) => formatCurrency(Number(value)) },
    { title: 'İcra Faizi', dataIndex: 'executionPercent', render: (value) => <Progress percent={Number(value)} size="small" strokeColor="#078b55" /> },
  ]

  return (
    <div className="page-stack">
      <PageTitle title="2. Obyekt Üzrə İş Saatı və Əmək Yükü" extra={<ObjectFilter pageKey="siteHours" />} />

      <section className="kpi-grid">
        <KpiCard icon={<TeamOutlined />} title="Plan İşçi" value={formatNumber(totals.planned)} trend="central worker planı" tone="green" />
        <KpiCard icon={<TeamOutlined />} title="Faktiki İşçi" value={formatNumber(totals.actual)} trend="aktiv işçilər" tone="blue" />
        <KpiCard icon={<ClockCircleOutlined />} title="Toplam Saat" value={formatHours(totals.hours, 0)} trend="payroll saatları" tone="blue" />
        <KpiCard icon={<DollarCircleOutlined />} title="Əmək Xərci" value={formatCurrency(totals.cost)} trend="worker tarifləri" tone="green" />
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
            <li>Plan və faktiki işçi sayı central worker assignments-dan gəlir.</li>
            <li>Overtime və riskli saatlar payroll selectorunda hesablanır.</li>
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
