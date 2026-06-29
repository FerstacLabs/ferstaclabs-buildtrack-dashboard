import { AuditOutlined, ClockCircleOutlined, DownloadOutlined, EditOutlined, SafetyCertificateOutlined } from '@ant-design/icons'
import type { TableColumnsType } from 'antd'
import { DonutChartCard } from '../../components/charts/DonutChartCard'
import { FilterBar } from '../../components/layout/FilterBar'
import { DataTable } from '../../components/tables/DataTable'
import { ExplanationCard } from '../../components/ui/ExplanationCard'
import { KpiCard } from '../../components/ui/KpiCard'
import { ToolbarButton } from '../../components/ui/ToolbarButton'
import { PageTitle } from '../../components/ui/PageTitle'
import { StatusBadge } from '../../components/ui/StatusBadge'
import { statusDistribution, supervisorRows, supervisorTimeline } from '../../services/data/reportCalculations'
import { useBuildTrackStore } from '../../services/data/dataService'
import { exportRowsToCsv, exportRowsToExcel } from '../../services/data/exportService'
import type { SupervisorAuditRecord } from '../../types/models'
import { formatNumber, formatPercent } from '../../utils/formatters'

export const SupervisorAuditPage = () => {
  const { data, filters } = useBuildTrackStore()
  if (!data) return null

  const rows = supervisorRows(data, filters)
  const timeline = supervisorTimeline(data)
  const manual = rows.reduce((sum, row) => sum + row.tablet_entries + row.manual_edits, 0)
  const changes = rows.reduce((sum, row) => sum + row.checkin_changes + row.checkout_changes, 0)
  const risky = rows.reduce((sum, row) => sum + row.risky_approvals, 0)
  const compatible = rows.length ? (rows.filter((row) => row.audit_status === 'Uyğun' || row.audit_status === 'Yaxşı').length / rows.length) * 100 : 0
  const tableRows = rows.map((row) => ({ ...row, key: row.supervisor_id }))
  const columns: TableColumnsType<SupervisorAuditRecord & { key: string }> = [
    { title: 'Prorab adı', dataIndex: 'supervisor_name', sorter: (a, b) => a.supervisor_name.localeCompare(b.supervisor_name) },
    { title: 'Obyekt', dataIndex: 'site_id', render: (value) => data.sites.find((site) => site.site_id === value)?.site_name ?? value },
    { title: 'Period', dataIndex: 'period' },
    { title: 'Tablet Giriş', dataIndex: 'tablet_entries' },
    { title: 'Manual Düzəliş', dataIndex: 'manual_edits' },
    { title: 'Check-in Dəyişiklik', dataIndex: 'checkin_changes' },
    { title: 'Check-out Dəyişiklik', dataIndex: 'checkout_changes' },
    { title: 'Riskli Təsdiq', dataIndex: 'risky_approvals' },
    { title: 'Eyni İşçiyə Təkrar', dataIndex: 'repeated_worker_confirmations' },
    { title: 'Gec Təsdiq', dataIndex: 'late_approvals' },
    { title: 'Audit Statusu', dataIndex: 'audit_status', render: (value) => <StatusBadge status={value} /> },
  ]

  return (
    <div className="page-stack">
      <PageTitle title="7. Prorab/Briqadir Müdaxilələri və Audit Hesabatı" />
      <FilterBar data={data} showSupervisor advancedFields={['dateRange', 'siteId', 'supervisor']} />

      <section className="kpi-grid">
        <KpiCard icon={<EditOutlined />} title="Manual Giriş" value={formatNumber(manual)} trend="12% (öncəki aya nisbətən)" tone="green" />
        <KpiCard icon={<ClockCircleOutlined />} title="Saat Düzəlişi" value={formatNumber(changes)} trend="8% (öncəki aya nisbətən)" tone="blue" />
        <KpiCard icon={<SafetyCertificateOutlined />} title="Riskli Təsdiq" value={formatNumber(risky)} trend="23% (öncəki aya nisbətən)" tone="orange" />
        <KpiCard icon={<AuditOutlined />} title="Audit Uyğunluğu" value={formatPercent(compatible)} trend="4,1% (öncəki aya nisbətən)" tone="green" />
      </section>

      <DataTable
        title="Prorab Müdaxilələri Xülasəsi"
        columns={columns}
        data={tableRows}
        extra={
          <div className="table-actions">
            <ToolbarButton icon={<DownloadOutlined />} onClick={() => exportRowsToExcel('prorab-audit', tableRows)}>Excel Export</ToolbarButton>
            <ToolbarButton icon={<DownloadOutlined />} tone="purple" onClick={() => exportRowsToCsv('prorab-audit', tableRows)}>CSV Export</ToolbarButton>
          </div>
        }
      />

      <section className="chart-grid">
        <DonutChartCard title="Audit Statusu üzrə Paylanma" data={statusDistribution(rows)} centerValue={formatNumber(rows.length)} centerLabel="cəmi prorab" />
        <section className="panel-card">
          <h2>Son Müdaxilələr</h2>
          <div className="timeline">
            {timeline.map((item) => (
              <div className="timeline-item" key={item.id}>
                <span className="timeline-dot" style={{ background: `var(--bt-${item.tone})` }} />
                <div>
                  <strong>{item.title}</strong>
                  <span>{item.subtitle}</span>
                </div>
                <span>{item.date}</span>
              </div>
            ))}
          </div>
        </section>
      </section>

      <section className="explanation-grid">
        <ExplanationCard icon={<AuditOutlined />} title="Bu tablo niyə lazımdır?">
          <p>Prorab və briqadir tərəfindən edilən planlı və manual müdaxilələri izləyir, daxili nəzarəti gücləndirir.</p>
        </ExplanationCard>
        <ExplanationCard icon={<EditOutlined />} title="Custom imkanlar" tone="blue">
          <ul>
            <li>Prorab, obyekt, period və əməliyyat növü üzrə filtr.</li>
            <li>Risk səviyyəsi əsasında avtomatik qiymətləndirmə.</li>
            <li>Excel, CSV və 1C uyğun formatlarda ixrac.</li>
          </ul>
        </ExplanationCard>
      </section>
    </div>
  )
}
