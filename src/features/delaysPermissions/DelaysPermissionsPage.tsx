import { CheckCircleOutlined, ClockCircleOutlined, DownloadOutlined, LoginOutlined, QuestionCircleOutlined } from '@ant-design/icons'
import type { TableColumnsType } from 'antd'
import { LineChartCard } from '../../components/charts/LineChartCard'
import { ObjectFilter } from '../../components/filters/ObjectFilter'
import { DataTable } from '../../components/tables/DataTable'
import { ExplanationCard } from '../../components/ui/ExplanationCard'
import { KpiCard } from '../../components/ui/KpiCard'
import { ToolbarButton } from '../../components/ui/ToolbarButton'
import { PageTitle } from '../../components/ui/PageTitle'
import { exportRowsToCsv, exportRowsToExcel } from '../../services/data/exportService'
import { formatNumber, formatPercent } from '../../utils/formatters'
import { ALL_OBJECTS_ID, getDelayRowsByObject, getWorkersByObject, type DelayRiskRow } from '../projectProgress/projectSelectors'
import { useProjectProgressStore } from '../projectProgress/projectProgressStore'

export const DelaysPermissionsPage = () => {
  const store = useProjectProgressStore()
  const selectedObjectId = store.selectedObjectId ?? ALL_OBJECTS_ID
  const rows = getDelayRowsByObject(store, selectedObjectId)
  const workers = getWorkersByObject(store, selectedObjectId)
  const lateCount = rows.reduce((sum, row) => sum + row.delayCount, 0)
  const lateMinutes = rows.reduce((sum, row) => sum + row.totalDelayMinutes, 0)
  const earlyCount = rows.filter((row) => row.riskScore >= 60).length
  const attendance = workers.length ? ((workers.length - rows.length) / workers.length) * 100 : 100
  const trend = rows.slice(0, 8).map((row) => ({
    name: row.crewName.slice(0, 14),
    gecikmə: row.delayCount,
    saat: Math.round(row.totalDelayMinutes / 60),
    erkən: row.riskScore >= 60 ? 1 : 0,
  }))

  const columns: TableColumnsType<DelayRiskRow> = [
    { title: 'İşçi adı', dataIndex: 'workerName', sorter: (a, b) => a.workerName.localeCompare(b.workerName) },
    { title: 'Obyekt', dataIndex: 'objectName' },
    { title: 'Vəzifə', dataIndex: 'role' },
    { title: 'Briqada', dataIndex: 'crewName' },
    { title: 'Gecikmə Sayı', dataIndex: 'delayCount', sorter: (a, b) => a.delayCount - b.delayCount },
    { title: 'Ümumi Gecikmə', dataIndex: 'totalDelayMinutes', render: (value) => `${value} dəq` },
    { title: 'Erkən Çıxış Sayı', render: (_, row) => row.riskScore >= 60 ? 1 : 0 },
    { title: 'İcazə Saat/Gün', render: () => '0' },
    { title: 'Davamiyyət %', render: (_, row) => formatPercent(Math.max(0, 100 - row.riskScore / 2)) },
    { title: 'Trend', render: (_, row) => <span className={`trend-pill trend-${row.riskScore >= 60 ? 'down' : 'stable'}`}>{row.riskScore >= 60 ? 'Risk' : 'Sabit'}</span> },
    { title: 'Qeyd', dataIndex: 'reason' },
  ]

  return (
    <div className="page-stack">
      <PageTitle title="4. Gecikmə, Erkən Çıxış və İcazələr" extra={<ObjectFilter pageKey="delays" />} />

      <section className="kpi-grid">
        <KpiCard icon={<ClockCircleOutlined />} title="Gecikmə Sayı" value={formatNumber(lateCount)} trend="filtered object" tone="green" />
        <KpiCard icon={<ClockCircleOutlined />} title="Ümumi Gecikmə Dəq" value={formatNumber(lateMinutes)} trend="central risk rows" tone="blue" />
        <KpiCard icon={<LoginOutlined />} title="Erkən Çıxış" value={formatNumber(earlyCount)} trend="risk score əsaslı" tone="orange" />
        <KpiCard icon={<CheckCircleOutlined />} title="Davamiyyət Faizi" value={formatPercent(attendance)} trend={`${workers.length} işçi`} tone="green" />
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
          <p>İşçilərin zaman intizamını eyni obyekt, briqada və worker modelində izləmək üçün əsas idarəetmə vasitəsidir.</p>
        </ExplanationCard>
        <ExplanationCard icon={<ClockCircleOutlined />} title="Custom imkanlar" tone="orange">
          <ul>
            <li>Obyekt üzrə filtr.</li>
            <li>Risk score dəyişdikcə gecikmə göstəriciləri yenilənir.</li>
            <li>Excel və CSV formatında ixrac imkanı.</li>
          </ul>
        </ExplanationCard>
      </section>
    </div>
  )
}
