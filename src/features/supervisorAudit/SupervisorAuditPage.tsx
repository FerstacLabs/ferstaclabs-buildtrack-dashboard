import { AuditOutlined, ClockCircleOutlined, DownloadOutlined, EditOutlined, SafetyCertificateOutlined } from '@ant-design/icons'
import type { TableColumnsType } from 'antd'
import { Tag } from 'antd'
import { DonutChartCard } from '../../components/charts/DonutChartCard'
import { ObjectFilter } from '../../components/filters/ObjectFilter'
import { DataTable } from '../../components/tables/DataTable'
import { ExplanationCard } from '../../components/ui/ExplanationCard'
import { KpiCard } from '../../components/ui/KpiCard'
import { ToolbarButton } from '../../components/ui/ToolbarButton'
import { PageTitle } from '../../components/ui/PageTitle'
import { exportRowsToCsv, exportRowsToExcel } from '../../services/data/exportService'
import { formatNumber, formatPercent } from '../../utils/formatters'
import { ALL_OBJECTS_ID, getAuditRowsByObject, type AuditPanelRow } from '../projectProgress/projectSelectors'
import { useProjectProgressStore } from '../projectProgress/projectProgressStore'

const auditColor: Record<AuditPanelRow['auditStatus'], string> = {
  Uyğun: 'green',
  Yaxşı: 'blue',
  'Nəzarət lazımdır': 'orange',
}

export const SupervisorAuditPage = () => {
  const store = useProjectProgressStore()
  const selectedObjectId = store.selectedObjectId ?? ALL_OBJECTS_ID
  const rows = getAuditRowsByObject(store, selectedObjectId)
  const manual = rows.reduce((sum, row) => sum + row.manualEntries, 0)
  const changes = rows.reduce((sum, row) => sum + row.corrections, 0)
  const risky = rows.reduce((sum, row) => sum + row.riskyApprovals, 0)
  const compatible = rows.length ? (rows.filter((row) => row.auditStatus === 'Uyğun' || row.auditStatus === 'Yaxşı').length / rows.length) * 100 : 0
  const distribution = ['Uyğun', 'Yaxşı', 'Nəzarət lazımdır'].map((status) => ({
    name: status,
    value: rows.filter((row) => row.auditStatus === status).length,
  }))

  const columns: TableColumnsType<AuditPanelRow> = [
    { title: 'Prorab adı', dataIndex: 'prorabName', sorter: (a, b) => a.prorabName.localeCompare(b.prorabName) },
    { title: 'Obyekt', dataIndex: 'objectName' },
    { title: 'Briqada', dataIndex: 'crewName' },
    { title: 'Period', dataIndex: 'period' },
    { title: 'Tablet Giriş', dataIndex: 'manualEntries' },
    { title: 'Manual Düzəliş', dataIndex: 'corrections' },
    { title: 'Riskli Təsdiq', dataIndex: 'riskyApprovals' },
    { title: 'Eyni İşçiyə Təkrar', dataIndex: 'repeatedWorkerEntries' },
    { title: 'Gec Təsdiq', dataIndex: 'lateApprovals' },
    { title: 'Audit Statusu', dataIndex: 'auditStatus', render: (value: AuditPanelRow['auditStatus']) => <Tag color={auditColor[value]}>{value}</Tag> },
  ]

  return (
    <div className="page-stack">
      <PageTitle title="7. Prorab/Briqadir Müdaxilələri və Audit Hesabatı" extra={<ObjectFilter pageKey="audit" />} />

      <section className="kpi-grid">
        <KpiCard icon={<EditOutlined />} title="Manual Giriş" value={formatNumber(manual)} trend="prorab gündəlikləri" tone="green" />
        <KpiCard icon={<ClockCircleOutlined />} title="Saat Düzəlişi" value={formatNumber(changes)} trend="plan/fakt fərqi" tone="blue" />
        <KpiCard icon={<SafetyCertificateOutlined />} title="Riskli Təsdiq" value={formatNumber(risky)} trend="riskli worker sayı" tone="orange" />
        <KpiCard icon={<AuditOutlined />} title="Audit Uyğunluğu" value={formatPercent(compatible)} trend={`${rows.length} briqada`} tone="green" />
      </section>

      <DataTable
        title="Prorab Müdaxilələri Xülasəsi"
        columns={columns}
        data={rows}
        extra={
          <div className="table-actions">
            <ToolbarButton icon={<DownloadOutlined />} onClick={() => exportRowsToExcel('prorab-audit', rows)}>Excel Export</ToolbarButton>
            <ToolbarButton icon={<DownloadOutlined />} tone="purple" onClick={() => exportRowsToCsv('prorab-audit', rows)}>CSV Export</ToolbarButton>
          </div>
        }
      />

      <section className="chart-grid">
        <DonutChartCard title="Audit Statusu üzrə Paylanma" data={distribution} centerValue={formatNumber(rows.length)} centerLabel="cəmi prorab" />
        <section className="panel-card">
          <h2>Son Müdaxilələr</h2>
          <div className="timeline">
            {rows.slice(0, 5).map((item) => (
              <div className="timeline-item" key={item.id}>
                <span className="timeline-dot" style={{ background: item.auditStatus === 'Nəzarət lazımdır' ? 'var(--bt-orange)' : 'var(--bt-green)' }} />
                <div>
                  <strong>{item.prorabName}</strong>
                  <span>{item.objectName} · {item.crewName}</span>
                </div>
                <span>{item.auditStatus}</span>
              </div>
            ))}
          </div>
        </section>
      </section>

      <section className="explanation-grid">
        <ExplanationCard icon={<AuditOutlined />} title="Bu tablo niyə lazımdır?">
          <p>Prorab və briqadir müdaxilələrini obyekt, crew və gündəlik hesabat datası ilə uyğun göstərir.</p>
        </ExplanationCard>
        <ExplanationCard icon={<EditOutlined />} title="Custom imkanlar" tone="blue">
          <ul>
            <li>Obyekt üzrə audit filteri.</li>
            <li>Riskli işçi və plan/fakt saat fərqindən avtomatik audit göstəriciləri.</li>
            <li>Excel və CSV formatlarında ixrac.</li>
          </ul>
        </ExplanationCard>
      </section>
    </div>
  )
}
