import { ClockCircleOutlined, DownloadOutlined, TeamOutlined, UserDeleteOutlined } from '@ant-design/icons'
import type { TableColumnsType } from 'antd'
import { Tag } from 'antd'
import { DonutChartCard } from '../../components/charts/DonutChartCard'
import { ProjectSelect } from '../../components/ProjectSelect'
import { DataTable } from '../../components/tables/DataTable'
import { ExplanationCard } from '../../components/ui/ExplanationCard'
import { KpiCard } from '../../components/ui/KpiCard'
import { PageTitle } from '../../components/ui/PageTitle'
import { RiskBadge } from '../../components/ui/RiskBadge'
import { ToolbarButton } from '../../components/ui/ToolbarButton'
import { exportRowsToExcel } from '../../services/data/exportService'
import { formatHours, formatNumber, formatPercent } from '../../utils/formatters'
import { getAttendanceRowsByObject, getWorkersByObject, type AttendancePanelRow } from '../projectProgress/projectSelectors'
import { useProjectProgressStore } from '../projectProgress/projectProgressStore'
import { useProjectSelectionStore } from '../../stores/projectSelectionStore'

const statusColor: Record<AttendancePanelRow['status'], string> = {
  'Gəlib': 'green',
  Gecikib: 'orange',
  Riskli: 'red',
}

export const DailyAttendancePage = () => {
  const store = useProjectProgressStore()
  const selectedObjectId = useProjectSelectionStore((state) => state.selectedProjectId)
  const rows = getAttendanceRowsByObject(store, selectedObjectId)
  const workers = getWorkersByObject(store, selectedObjectId)
  const present = new Set(rows.map((row) => row.workerId)).size
  const absent = Math.max(0, workers.filter((worker) => worker.status === 'active').length - present)
  const late = rows.filter((row) => row.status === 'Gecikib').length
  const risky = rows.filter((row) => row.status === 'Riskli').length
  const activeHours = rows.reduce((sum, row) => sum + row.totalHours, 0)
  const total = Math.max(1, present + absent)
  const donut = [
    { name: 'Gəlib', value: present },
    { name: 'Gəlməyib', value: absent },
    { name: 'Gecikib', value: late },
    { name: 'Riskli', value: risky },
  ]

  const columns: TableColumnsType<AttendancePanelRow> = [
    { title: 'İşçi ID', dataIndex: 'workerExternalId', sorter: (a, b) => a.workerExternalId.localeCompare(b.workerExternalId) },
    { title: 'İşçi adı', dataIndex: 'workerName', sorter: (a, b) => a.workerName.localeCompare(b.workerName) },
    { title: 'Obyekt', dataIndex: 'objectName' },
    { title: 'Vəzifə', dataIndex: 'role' },
    { title: 'Briqada', dataIndex: 'crewName' },
    { title: 'Plan Giriş', render: () => '08:00' },
    { title: 'Faktiki Giriş', dataIndex: 'firstSeen' },
    { title: 'Plan Çıxış', render: () => '18:00' },
    { title: 'Faktiki Çıxış', dataIndex: 'lastSeen' },
    { title: 'Status', dataIndex: 'status', render: (status: AttendancePanelRow['status']) => <Tag color={statusColor[status]}>{status}</Tag> },
    { title: 'Gecikmə', render: (_, row) => row.status === 'Gecikib' ? '25 dəq' : '0 dəq' },
    { title: 'İşlənmiş Saat', dataIndex: 'totalHours', sorter: (a, b) => a.totalHours - b.totalHours, render: (value) => formatHours(Number(value), 1) },
    { title: 'Giriş Metodu', dataIndex: 'source' },
    { title: 'Risk', dataIndex: 'riskScore', render: (score: number) => <RiskBadge level={score >= 80 ? 'Kritik' : score >= 60 ? 'Yüksək' : score >= 35 ? 'Orta' : 'Aşağı'} score={score} /> },
  ]

  return (
    <div className="page-stack">
      <PageTitle title="1. Günlük Davamiyyət Paneli" extra={<ProjectSelect pageKey="attendance" />} />

      <section className="kpi-grid">
        <KpiCard icon={<TeamOutlined />} title="Bugün Gələn" value={formatNumber(present)} trend={formatPercent((present / total) * 100)} tone="green" />
        <KpiCard icon={<UserDeleteOutlined />} title="Gəlməyən" value={formatNumber(absent)} trend={formatPercent((absent / total) * 100)} tone="red" />
        <KpiCard icon={<ClockCircleOutlined />} title="Gecikən" value={formatNumber(late)} trend={formatPercent((late / total) * 100)} tone="orange" />
        <KpiCard icon={<ClockCircleOutlined />} title="Aktiv Saat" value={formatHours(activeHours, 0)} trend="gələnlər üzrə" tone="blue" />
      </section>

      <DataTable
        title="Günlük Davamiyyət Siyahısı"
        columns={columns}
        data={rows}
        extra={<ToolbarButton icon={<DownloadOutlined />} onClick={() => exportRowsToExcel('gunluk-davamiyyet', rows)}>Export</ToolbarButton>}
      />

      <section className="daily-summary-grid">
        <DonutChartCard title="Status üzrə xülasə" data={donut} centerValue={formatNumber(present + absent)} centerLabel="cəmi nəfər" height={220} />
        <aside className="panel-card daily-insight-card">
          <h2>Günün xülasəsi</h2>
          <div className="summary-metric"><span>Davamiyyət faizi</span><strong>{formatPercent((present / total) * 100)}</strong></div>
          <div className="summary-metric"><span>Riskli və gecikən qeydlər</span><strong>{formatNumber(late + risky)}</strong></div>
          <div className="summary-metric"><span>Aktiv iş saatı</span><strong>{formatHours(activeHours, 0)}</strong></div>
          <p>Bu bölmə central işçi, briqada və obyekt datasından hesablanır.</p>
        </aside>
      </section>

      <section className="explanation-grid">
        <ExplanationCard icon={<TeamOutlined />} title="Bu tablo niyə lazımdır?">
          <ul>
            <li>Gündəlik davamiyyətin obyekt, briqada və işçi modeli ilə uyğun izlənməsi üçün.</li>
            <li>Gecikmə, gəlməmə və riskli qeyd hallarını eyni dashboard datasında görmək üçün.</li>
            <li>Maaş hesablamasına gedən iş saatlarını dəqiqləşdirmək üçün.</li>
          </ul>
        </ExplanationCard>
        <ExplanationCard icon={<DownloadOutlined />} title="Custom imkanlar" tone="orange">
          <ul>
            <li>Obyekt üzrə filtr və Excel export.</li>
            <li>Sütunların sıralanması və riskli işçilərin operativ seçilməsi.</li>
          </ul>
        </ExplanationCard>
      </section>
    </div>
  )
}
