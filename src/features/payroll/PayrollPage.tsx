import { DollarCircleOutlined, DownloadOutlined, FileExcelOutlined, FileTextOutlined, WalletOutlined } from '@ant-design/icons'
import type { TableColumnsType } from 'antd'
import { Select, Space, Table, Tag } from 'antd'
import { ObjectFilter } from '../../components/filters/ObjectFilter'
import { ExplanationCard } from '../../components/ui/ExplanationCard'
import { KpiCard } from '../../components/ui/KpiCard'
import { PageTitle } from '../../components/ui/PageTitle'
import { ToolbarButton } from '../../components/ui/ToolbarButton'
import { exportRowsTo1CMock, exportRowsToCsv, exportRowsToExcel } from '../../services/data/exportService'
import { formatCurrency, formatHours, formatNumber } from '../../utils/formatters'
import { ALL_OBJECTS_ID, getPayrollRowsByObject, type ProjectPayrollRow } from '../projectProgress/projectSelectors'
import { useProjectProgressStore } from '../projectProgress/projectProgressStore'
import { useState } from 'react'

const statusColor: Record<ProjectPayrollRow['exportStatus'], string> = {
  Hazır: 'green',
  Xəta: 'red',
  Xəbərdarlıq: 'orange',
  Göndərilib: 'blue',
}

export const PayrollPage = () => {
  const store = useProjectProgressStore()
  const selectedObjectId = store.selectedObjectIdByPage.payroll ?? ALL_OBJECTS_ID
  const [crewFilter, setCrewFilter] = useState('all')
  const [statusFilter, setStatusFilter] = useState('all')
  const allRows = getPayrollRowsByObject(store, selectedObjectId)
  const rows = allRows
    .filter((row) => crewFilter === 'all' || row.crewName === crewFilter)
    .filter((row) => statusFilter === 'all' || row.exportStatus === statusFilter)

  const totals = rows.reduce(
    (acc, row) => ({
      approved: acc.approved + row.approvedHours,
      overtime: acc.overtime + row.overtimeHours,
      gross: acc.gross + row.grossAmount,
      final: acc.final + row.finalAmount,
      ready: acc.ready + (row.exportStatus === 'Hazır' || row.exportStatus === 'Göndərilib' ? 1 : 0),
    }),
    { approved: 0, overtime: 0, gross: 0, final: 0, ready: 0 },
  )

  const crewOptions = Array.from(new Set(allRows.map((row) => row.crewName))).sort().map((crew) => ({ value: crew, label: crew }))

  const exportRows = rows.map((row) => ({
    workerName: row.workerName,
    workerId: row.workerExternalId,
    objectName: row.objectName,
    crew: row.crewName,
    role: row.role,
    hourlyRate: row.hourlyRate,
    normalHours: row.normalHours,
    overtimeHours: row.overtimeHours,
    approvedHours: row.approvedHours,
    riskHours: row.riskHours,
    manualAdjustment: row.manualAdjustment,
    grossAmount: row.grossAmount,
    correctionAmount: row.correctionAmount,
    finalAmount: row.finalAmount,
    exportStatus: row.exportStatus,
  }))

  const columns: TableColumnsType<ProjectPayrollRow> = [
    { title: 'İşçi adı', dataIndex: 'workerName', sorter: (a, b) => a.workerName.localeCompare(b.workerName), render: (value, row) => <strong>{value}<br /><span className="muted-text">{row.workerExternalId}</span></strong> },
    { title: 'Obyekt', dataIndex: 'objectName' },
    { title: 'Briqada', dataIndex: 'crewName', filters: crewOptions.map((option) => ({ text: option.label, value: option.value })), onFilter: (value, row) => row.crewName === value },
    { title: 'Rol', dataIndex: 'role' },
    { title: 'Tarif', dataIndex: 'hourlyRate', align: 'right', render: (value) => `${formatCurrency(Number(value))}/saat` },
    { title: 'Normal saat', dataIndex: 'normalHours', align: 'right', render: (value) => formatHours(Number(value), 1), sorter: (a, b) => a.normalHours - b.normalHours },
    { title: 'Overtime', dataIndex: 'overtimeHours', align: 'right', render: (value) => formatHours(Number(value), 1) },
    { title: 'Riskli saat', dataIndex: 'riskHours', align: 'right', render: (value) => formatHours(Number(value), 1) },
    { title: 'Təsdiqli saat', dataIndex: 'approvedHours', align: 'right', render: (value) => formatHours(Number(value), 1) },
    { title: 'Brutto', dataIndex: 'grossAmount', align: 'right', render: (value) => formatCurrency(Number(value)) },
    { title: 'Düzəliş', dataIndex: 'correctionAmount', align: 'right', render: (value) => formatCurrency(Number(value)) },
    { title: 'Yekun məbləğ', dataIndex: 'finalAmount', align: 'right', render: (value) => <strong>{formatCurrency(Number(value))}</strong>, sorter: (a, b) => a.finalAmount - b.finalAmount },
    { title: 'Export', dataIndex: 'exportStatus', render: (value: ProjectPayrollRow['exportStatus']) => <Tag color={statusColor[value]}>{value}</Tag> },
  ]

  return (
    <div className="page-stack">
      <PageTitle title="Maaş Hesabatı" subtitle={`${store.project.name} üzrə işçi saatları, tariflər və payroll hesablaması`} extra={<ObjectFilter pageKey="payroll" />} />

      <section className="kpi-grid">
        <KpiCard icon={<DollarCircleOutlined />} title="Təsdiqli saat" value={formatHours(totals.approved, 1)} trend={`${rows.length} işçi`} tone="green" />
        <KpiCard icon={<DollarCircleOutlined />} title="Overtime" value={formatHours(totals.overtime, 1)} trend="aylıq hesab" tone="orange" />
        <KpiCard icon={<WalletOutlined />} title="Brutto məbləğ" value={formatCurrency(totals.gross)} trend="tarif x saat" tone="blue" />
        <KpiCard icon={<FileExcelOutlined />} title="Exporta hazır" value={`${formatNumber((totals.ready / Math.max(1, rows.length)) * 100, 0)}%`} trend={`${totals.ready} işçi`} tone="green" />
      </section>

      <section className="content-grid wide-side">
        <section className="table-card">
          <div className="card-heading">
            <h2>Maaş hazırlıq cədvəli</h2>
            <Space wrap>
              <Select value={crewFilter} onChange={setCrewFilter} style={{ minWidth: 190 }} options={[{ value: 'all', label: 'Bütün briqadalar' }, ...crewOptions]} />
              <Select value={statusFilter} onChange={setStatusFilter} style={{ minWidth: 150 }} options={[{ value: 'all', label: 'Bütün statuslar' }, { value: 'Hazır', label: 'Hazır' }, { value: 'Xəbərdarlıq', label: 'Xəbərdarlıq' }, { value: 'Göndərilib', label: 'Göndərilib' }]} />
            </Space>
          </div>
          <Table rowKey="id" columns={columns} dataSource={rows} pagination={{ pageSize: 12 }} scroll={{ x: 1360 }} />
        </section>
        <aside className="panel-card">
          <h2>Export və mühasibat inteqrasiyası</h2>
          <div className="export-panel">
            <div className="export-option">
              <strong>Excel Export</strong>
              <p>Maaş hesabını .xlsx formatında yükləyin.</p>
              <ToolbarButton icon={<FileExcelOutlined />} tone="green" onClick={() => exportRowsToExcel('maas-hesabati', exportRows)}>Excel yüklə</ToolbarButton>
            </div>
            <div className="export-option">
              <strong>CSV Export</strong>
              <p>Sistemə uyğun CSV formatında yükləyin.</p>
              <ToolbarButton icon={<FileTextOutlined />} tone="purple" onClick={() => exportRowsToCsv('maas-hesabati', exportRows)}>CSV yüklə</ToolbarButton>
            </div>
            <div className="export-option">
              <strong>1C üçün Export</strong>
              <p>1C mühasibat sisteminə uyğun fayl yaradın.</p>
              <ToolbarButton icon={<DownloadOutlined />} tone="orange" onClick={() => exportRowsTo1CMock('maas-1c-export', exportRows)}>1C üçün hazırla</ToolbarButton>
            </div>
          </div>
        </aside>
      </section>

      <section className="explanation-grid">
        <ExplanationCard icon={<WalletOutlined />} title="Bu tablo niyə lazımdır?">
          <p>İşçilərin ay ərzində işlədiyi saatlar, overtime, riskli saatlar və tariflər eyni layihə store-u üzərindən hesablanır.</p>
        </ExplanationCard>
        <ExplanationCard icon={<FileTextOutlined />} title="Əsas sütunlar" tone="orange">
          <ul>
            <li>Normal saat və overtime işçinin work-hour allocation məlumatlarından gəlir.</li>
            <li>Tarif işçi kartında dəyişəndə yekun məbləğ avtomatik yenilənir.</li>
            <li>Yekun məbləğ export üçün hazır payroll sətiridir.</li>
          </ul>
        </ExplanationCard>
      </section>
    </div>
  )
}
