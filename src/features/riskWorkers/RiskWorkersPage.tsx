import { CloudSyncOutlined, DownloadOutlined, SafetyCertificateOutlined, TabletOutlined, WarningOutlined } from '@ant-design/icons'
import type { TableColumnsType } from 'antd'
import { Button } from 'antd'
import { FilterBar } from '../../components/layout/FilterBar'
import { DataTable } from '../../components/tables/DataTable'
import { ExplanationCard } from '../../components/ui/ExplanationCard'
import { KpiCard } from '../../components/ui/KpiCard'
import { PageTitle } from '../../components/ui/PageTitle'
import { RiskBadge } from '../../components/ui/RiskBadge'
import { ToolbarButton } from '../../components/ui/ToolbarButton'
import { riskWorkerRows } from '../../services/data/reportCalculations'
import { useBuildTrackStore } from '../../services/data/dataService'
import { exportRowsToExcel } from '../../services/data/exportService'
import type { RiskWorkerRow } from '../../types/reports'
import { formatNumber } from '../../utils/formatters'

export const RiskWorkersPage = () => {
  const { data, filters } = useBuildTrackStore()
  if (!data) return null

  const rows = riskWorkerRows(data, filters)
  const critical = rows.filter((row) => row.risk_level === 'Kritik').length
  const tablet = rows.filter((row) => row.entry_method === 'Prorab Tablet').length
  const offline = rows.filter((row) => row.entry_method === 'Offline').length
  const columns: TableColumnsType<RiskWorkerRow> = [
    { title: 'İşçi adı', dataIndex: 'full_name', sorter: (a, b) => a.full_name.localeCompare(b.full_name) },
    { title: 'Obyekt', dataIndex: 'site_name' },
    { title: 'Vəzifə', dataIndex: 'position' },
    { title: 'Risk Balı', dataIndex: 'risk_score', sorter: (a, b) => a.risk_score - b.risk_score },
    { title: 'Risk Səviyyəsi', dataIndex: 'risk_level', render: (_, row) => <RiskBadge level={row.risk_level} /> },
    { title: 'Risk Səbəbi', dataIndex: 'risk_reason' },
    { title: 'Təkrar Sayı', dataIndex: 'repeat_count' },
    { title: 'Son Risk Tarixi', dataIndex: 'date' },
    { title: 'Giriş Metodu', dataIndex: 'entry_method' },
    { title: 'Təsdiq edən', dataIndex: 'approved_by' },
    { title: 'Tövsiyə', dataIndex: 'recommendation', render: (value) => <Button size="small">{value}</Button> },
  ]

  return (
    <div className="page-stack">
      <PageTitle title="3. Riskli İşçilər və Şübhəli Davamiyyət" />
      <FilterBar data={data} showRisk showSupervisor advancedFields={['dateRange', 'siteId', 'brigade', 'position', 'riskLevel', 'entryMethod', 'supervisor']} />

      <section className="kpi-grid">
        <KpiCard icon={<SafetyCertificateOutlined />} title="Riskli İşçi" value={formatNumber(rows.length)} trend="16 (öncəki aya nisbətən)" tone="green" />
        <KpiCard icon={<WarningOutlined />} title="Kritik Risk" value={formatNumber(critical)} trend="2 (öncəki aya nisbətən)" tone="red" />
        <KpiCard icon={<TabletOutlined />} title="Tablet Giriş Təkrarı" value={formatNumber(tablet)} trend="14 (öncəki aya nisbətən)" tone="blue" />
        <KpiCard icon={<CloudSyncOutlined />} title="Offline Sync Risk" value={formatNumber(offline)} trend="9 (öncəki aya nisbətən)" tone="orange" />
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
            ['Offline risk', 'Offline giriş, gec sync və saat fərqi.'],
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
          <p>Bu hesabat şübhəli davamiyyət nümunələrini aşkarlayır və müdaxiləni vaxtında etməyə kömək edir.</p>
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
