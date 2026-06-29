import { CheckCircleOutlined, DownloadOutlined, FileExcelOutlined, FileTextOutlined, WarningOutlined } from '@ant-design/icons'
import type { TableColumnsType } from 'antd'
import { Button } from 'antd'
import { FilterBar } from '../../components/layout/FilterBar'
import { DataTable } from '../../components/tables/DataTable'
import { ExplanationCard } from '../../components/ui/ExplanationCard'
import { KpiCard } from '../../components/ui/KpiCard'
import { PageTitle } from '../../components/ui/PageTitle'
import { StatusBadge } from '../../components/ui/StatusBadge'
import { ToolbarButton } from '../../components/ui/ToolbarButton'
import { exportValidationRows, payrollRows } from '../../services/data/reportCalculations'
import { useBuildTrackStore } from '../../services/data/dataService'
import { exportPayrollTo1CXml, exportRowsToCsv, exportRowsToExcel, exportValidationToTxt } from '../../services/data/exportService'
import type { ExportValidationRow } from '../../types/reports'
import { formatCurrency, formatNumber } from '../../utils/formatters'

export const ExportPage = () => {
  const { data, filters } = useBuildTrackStore()
  if (!data) return null

  const validationRows = exportValidationRows(data, filters)
  const payroll = payrollRows(data, filters)
  const ready = validationRows.filter((row) => row.export_status === 'Hazır').length
  const errors = validationRows.filter((row) => row.export_status === 'Xəta').length
  const sent = validationRows.filter((row) => row.export_status === 'Göndərilib').length
  const amount = validationRows.reduce((sum, row) => sum + row.net_amount, 0)
  const columns: TableColumnsType<ExportValidationRow> = [
    { title: 'Sətir ID', dataIndex: 'row_id' },
    { title: 'İşçi adı', dataIndex: 'full_name', sorter: (a, b) => a.full_name.localeCompare(b.full_name) },
    { title: 'Obyekt', dataIndex: 'site_name' },
    { title: 'Cost Code', dataIndex: 'cost_code' },
    { title: 'Maaş Tipi', dataIndex: 'salary_type' },
    { title: 'Təsdiqli Saat', dataIndex: 'approved_hours' },
    { title: 'Yekun Məbləğ', dataIndex: 'net_amount', render: (value) => formatCurrency(value) },
    { title: '1C Hesab Kodu', dataIndex: 'account_code' },
    { title: 'Export Statusu', dataIndex: 'export_status', render: (value) => <StatusBadge status={value} /> },
    { title: 'Xəta Mesajı', dataIndex: 'error_message' },
    { title: 'Son Yoxlama', dataIndex: 'checked_at' },
    { title: 'Action', render: () => <Button size="small">Yoxla</Button> },
  ]

  return (
    <div className="page-stack">
      <PageTitle title="1C / Export Mərkəzi" />
      <FilterBar data={data} showMonth showPosition advancedFields={['siteId', 'brigade', 'position', 'exportStatus']} />

      <section className="kpi-grid">
        <KpiCard icon={<CheckCircleOutlined />} title="Hazır Sətir" value={formatNumber(ready)} trend="export üçün hazır" tone="green" />
        <KpiCard icon={<WarningOutlined />} title="Xəta" value={formatNumber(errors)} trend="yoxlama tələb edir" tone="red" />
        <KpiCard icon={<DownloadOutlined />} title="Göndərilib" value={formatNumber(sent)} trend="son exportlar" tone="blue" />
        <KpiCard icon={<FileExcelOutlined />} title="Yekun Məbləğ" value={formatCurrency(amount)} trend="cari görünüş" tone="green" />
      </section>

      <section className="chart-grid">
        <section className="panel-card export-panel">
          <h2>Export history</h2>
          <p>Son exportlar lokal demo yaddaşında saxlanılan hesablamalara əsaslanır.</p>
          <ToolbarButton icon={<FileExcelOutlined />} tone="green" onClick={() => exportRowsToExcel('export-validation', validationRows)}>Excel export</ToolbarButton>
          <ToolbarButton icon={<FileTextOutlined />} tone="purple" onClick={() => exportRowsToCsv('export-validation', validationRows)}>CSV export</ToolbarButton>
        </section>
        <section className="panel-card export-panel">
          <h2>1C XML/TXT mock export</h2>
          <p>1C üçün XML və TXT demo faylları cari görünən məlumatlardan yaradılır.</p>
          <ToolbarButton icon={<DownloadOutlined />} tone="orange" onClick={() => exportPayrollTo1CXml('buildtrack-1c-payroll', payroll)}>1C XML yarat</ToolbarButton>
          <ToolbarButton icon={<FileTextOutlined />} onClick={() => exportValidationToTxt('buildtrack-1c-validation', validationRows)}>1C TXT yarat</ToolbarButton>
        </section>
      </section>

      <DataTable title="Export validation table" columns={columns} data={validationRows} />

      <section className="explanation-grid">
        <ExplanationCard icon={<CheckCircleOutlined />} title="Excel export">
          <p>Cari filtrə uyğun sətirlər real .xlsx faylı kimi yüklənir və təqdimat zamanı açılıb yoxlana bilər.</p>
        </ExplanationCard>
        <ExplanationCard icon={<WarningOutlined />} title="Export validation" tone="orange">
          <p>Xəta və xəbərdarlıq statusları 1C göndərişindən əvvəl yoxlama prosesini göstərmək üçün yaradılıb.</p>
        </ExplanationCard>
      </section>
    </div>
  )
}
