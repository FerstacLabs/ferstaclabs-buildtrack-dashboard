import { CheckCircleOutlined, DownloadOutlined, FileExcelOutlined, FileTextOutlined, WarningOutlined } from '@ant-design/icons'
import type { TableColumnsType } from 'antd'
import { Button } from 'antd'
import { ProjectSelect } from '../../components/ProjectSelect'
import { DataTable } from '../../components/tables/DataTable'
import { ExplanationCard } from '../../components/ui/ExplanationCard'
import { KpiCard } from '../../components/ui/KpiCard'
import { PageTitle } from '../../components/ui/PageTitle'
import { StatusBadge } from '../../components/ui/StatusBadge'
import { ToolbarButton } from '../../components/ui/ToolbarButton'
import { exportRowsTo1CMock, exportRowsToCsv, exportRowsToExcel } from '../../services/data/exportService'
import { formatCurrency, formatNumber } from '../../utils/formatters'
import { getExportRowsByObject, type ExportPanelRow } from '../projectProgress/projectSelectors'
import { useProjectProgressStore } from '../projectProgress/projectProgressStore'
import { useProjectSelectionStore } from '../../stores/projectSelectionStore'

export const ExportPage = () => {
  const store = useProjectProgressStore()
  const selectedObjectId = useProjectSelectionStore((state) => state.selectedProjectId)
  const validationRows = getExportRowsByObject(store, selectedObjectId)
  const ready = validationRows.filter((row) => row.exportStatus === 'Hazır').length
  const errors = validationRows.filter((row) => row.exportStatus === 'Xəta').length
  const sent = validationRows.filter((row) => row.exportStatus === 'Göndərilib').length
  const amount = validationRows.reduce((sum, row) => sum + row.finalAmount, 0)

  const columns: TableColumnsType<ExportPanelRow> = [
    { title: 'Sətir ID', dataIndex: 'id' },
    { title: 'İşçi adı', dataIndex: 'workerName', sorter: (a, b) => a.workerName.localeCompare(b.workerName) },
    { title: 'Obyekt', dataIndex: 'objectName' },
    { title: 'Briqada', dataIndex: 'crewName' },
    { title: 'Vəzifə', dataIndex: 'role' },
    { title: 'Təsdiqli saat', dataIndex: 'approvedHours' },
    { title: 'Yekun məbləğ', dataIndex: 'finalAmount', render: (value) => formatCurrency(Number(value)) },
    { title: '1C hesab kodu', dataIndex: 'accountCode' },
    { title: 'Export statusu', dataIndex: 'exportStatus', render: (value) => <StatusBadge status={value as never} /> },
    { title: 'Xəta mesajı', dataIndex: 'errorMessage' },
    { title: 'Əməliyyat', render: () => <Button size="small">Yoxla</Button> },
  ]

  return (
    <div className="page-stack">
      <PageTitle title="Export / 1C Mərkəzi" extra={<ProjectSelect pageKey="export" />} />

      <section className="kpi-grid">
        <KpiCard icon={<CheckCircleOutlined />} title="Hazır sətir" value={formatNumber(ready)} trend="export üçün hazır" tone="green" />
        <KpiCard icon={<WarningOutlined />} title="Xəta" value={formatNumber(errors)} trend="yoxlama tələb edir" tone="red" />
        <KpiCard icon={<DownloadOutlined />} title="Göndərilib" value={formatNumber(sent)} trend="son exportlar" tone="blue" />
        <KpiCard icon={<FileExcelOutlined />} title="Yekun məbləğ" value={formatCurrency(amount)} trend="cari görünüş" tone="green" />
      </section>

      <section className="chart-grid">
        <section className="panel-card export-panel">
          <h2>Export tarixçəsi</h2>
          <p>Son exportlar cari obyekt filterinə və payroll hesablamalarına əsaslanır.</p>
          <ToolbarButton icon={<FileExcelOutlined />} tone="green" onClick={() => exportRowsToExcel('export-validation', validationRows)}>Excel export</ToolbarButton>
          <ToolbarButton icon={<FileTextOutlined />} tone="purple" onClick={() => exportRowsToCsv('export-validation', validationRows)}>CSV export</ToolbarButton>
        </section>
        <section className="panel-card export-panel">
          <h2>1C XML/TXT export</h2>
          <p>1C üçün mock fayl cari görünən payroll/object rows-dan yaradılır.</p>
          <ToolbarButton icon={<DownloadOutlined />} tone="orange" onClick={() => exportRowsTo1CMock('buildtrack-1c-export', validationRows)}>1C mock yarat</ToolbarButton>
          <ToolbarButton icon={<FileTextOutlined />} onClick={() => exportRowsToCsv('buildtrack-1c-validation', validationRows)}>1C CSV yarat</ToolbarButton>
        </section>
      </section>

      <DataTable title="Export yoxlama cədvəli" columns={columns} data={validationRows} />

      <section className="explanation-grid">
        <ExplanationCard icon={<CheckCircleOutlined />} title="Excel export">
          <p>Cari obyekt filterinə uyğun sətirlər real .xlsx faylı kimi yüklənir.</p>
        </ExplanationCard>
        <ExplanationCard icon={<WarningOutlined />} title="Export yoxlaması" tone="orange">
          <p>Statuslar payroll selectorundan gəlir və 1C göndərişindən əvvəl yoxlama prosesini göstərir.</p>
        </ExplanationCard>
      </section>
    </div>
  )
}
