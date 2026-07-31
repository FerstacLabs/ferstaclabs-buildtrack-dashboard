import { CloudSyncOutlined, DownloadOutlined, SafetyCertificateOutlined, TabletOutlined, WarningOutlined } from '@ant-design/icons'
import type { TableColumnsType } from 'antd'
import { Button } from 'antd'
import { ObjectFilter } from '../../components/filters/ObjectFilter'
import { DataTable } from '../../components/tables/DataTable'
import { ExplanationCard } from '../../components/ui/ExplanationCard'
import { KpiCard } from '../../components/ui/KpiCard'
import { PageTitle } from '../../components/ui/PageTitle'
import { RiskBadge } from '../../components/ui/RiskBadge'
import { ToolbarButton } from '../../components/ui/ToolbarButton'
import { exportRowsToExcel } from '../../services/data/exportService'
import { formatNumber } from '../../utils/formatters'
import { ALL_OBJECTS_ID, getRiskRowsByObject, type DelayRiskRow } from '../projectProgress/projectSelectors'
import { useProjectProgressStore } from '../projectProgress/projectProgressStore'

export const RiskWorkersPage = () => {
  const store = useProjectProgressStore()
  const selectedObjectId = store.selectedObjectId ?? ALL_OBJECTS_ID
  const rows = getRiskRowsByObject(store, selectedObjectId)
  const critical = rows.filter((row) => row.riskLevel === 'Kritik').length
  const high = rows.filter((row) => row.riskLevel === 'Yüksək').length
  const attendanceSource = rows.filter((row) => row.source === 'attendance').length
  const reportSource = rows.filter((row) => row.source === 'daily-report').length

  const columns: TableColumnsType<DelayRiskRow> = [
    { title: 'İşçi adı', dataIndex: 'workerName', sorter: (a, b) => a.workerName.localeCompare(b.workerName) },
    { title: 'Obyekt', dataIndex: 'objectName' },
    { title: 'Vəzifə', dataIndex: 'role' },
    { title: 'Briqada', dataIndex: 'crewName' },
    { title: 'Risk Balı', dataIndex: 'riskScore', sorter: (a, b) => a.riskScore - b.riskScore },
    { title: 'Risk Səviyyəsi', dataIndex: 'riskLevel', render: (_, row) => <RiskBadge level={row.riskLevel} /> },
    { title: 'Risk Səbəbi', dataIndex: 'reason' },
    { title: 'Təkrar Sayı', dataIndex: 'delayCount' },
    { title: 'Ümumi gecikmə', dataIndex: 'totalDelayMinutes', render: (value) => `${value} dəq` },
    { title: 'Mənbə', dataIndex: 'source' },
    { title: 'Tövsiyə', render: () => <Button size="small">Yoxla</Button> },
  ]

  return (
    <div className="page-stack">
      <PageTitle title="3. Riskli İşçilər və Şübhəli Davamiyyət" extra={<ObjectFilter pageKey="riskWorkers" />} />

      <section className="kpi-grid">
        <KpiCard icon={<SafetyCertificateOutlined />} title="Riskli İşçi" value={formatNumber(rows.length)} trend="central worker datası" tone="green" />
        <KpiCard icon={<WarningOutlined />} title="Kritik Risk" value={formatNumber(critical)} trend={`${high} yüksək risk`} tone="red" />
        <KpiCard icon={<TabletOutlined />} title="Davamiyyət riski" value={formatNumber(attendanceSource)} trend="kamera/prorab qeydləri" tone="blue" />
        <KpiCard icon={<CloudSyncOutlined />} title="Gündəlik hesabat riski" value={formatNumber(reportSource)} trend="prorab qeydləri" tone="orange" />
      </section>

      <DataTable
        title="Riskli İşçilər Siyahısı"
        columns={columns}
        data={rows}
        extra={<ToolbarButton icon={<DownloadOutlined />} tone="green" onClick={() => exportRowsToExcel('riskli-isciler', rows)}>Export (Excel)</ToolbarButton>}
      />

      <section className="table-card">
        <div className="card-heading">
          <h2>Risk balı necə formalaşır?</h2>
        </div>
        <div className="module-grid">
          {[
            ['Giriş davranışı', 'Təkrar giriş, gecikmə və çıxış siqnalı.'],
            ['Zaman uyğunsuzluğu', 'Uzun fasilə, hərəkətsizlik və erkən çıxış.'],
            ['Gündəlik hesabat', 'Prorab qeydləri və gecikmə səbəbləri.'],
            ['Tarixçə və nümunə', 'Keçmiş pozuntular və riskli davranış.'],
          ].map(([title, text], index) => (
            <div className="panel-card" key={title}>
              <h2>{title}</h2>
              <p>{text}</p>
              <strong>Çəki: {[30, 25, 20, 25][index]}%</strong>
            </div>
          ))}
        </div>
      </section>

      <section className="explanation-grid">
        <ExplanationCard icon={<WarningOutlined />} title="Bu tablo niyə lazımdır?" tone="red">
          <p>Bu hesabat eyni obyekt-worker-briqada modelində riskli davamiyyət və prorab qeydlərini göstərir.</p>
        </ExplanationCard>
        <ExplanationCard icon={<SafetyCertificateOutlined />} title="Risk balı necə oxunur?" tone="orange">
          <ul>
            <li>80-100: Kritik Risk.</li>
            <li>60-79: Yüksək Risk.</li>
            <li>40-59: Orta Risk.</li>
            <li>0-39: Aşağı Risk.</li>
          </ul>
        </ExplanationCard>
      </section>
    </div>
  )
}
