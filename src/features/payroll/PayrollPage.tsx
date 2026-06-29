import { DollarCircleOutlined, DownloadOutlined, FileExcelOutlined, FileTextOutlined, WalletOutlined } from '@ant-design/icons'
import type { TableColumnsType } from 'antd'
import { FilterBar } from '../../components/layout/FilterBar'
import { DataTable } from '../../components/tables/DataTable'
import { ExplanationCard } from '../../components/ui/ExplanationCard'
import { KpiCard } from '../../components/ui/KpiCard'
import { PageTitle } from '../../components/ui/PageTitle'
import { StatusBadge } from '../../components/ui/StatusBadge'
import { ToolbarButton } from '../../components/ui/ToolbarButton'
import { payrollRows } from '../../services/data/reportCalculations'
import { useBuildTrackStore } from '../../services/data/dataService'
import { exportPayrollTo1CXml, exportRowsToCsv, exportRowsToExcel } from '../../services/data/exportService'
import type { PayrollRow } from '../../types/reports'
import { formatCurrency, formatHours, formatNumber } from '../../utils/formatters'

export const PayrollPage = () => {
  const { data, filters } = useBuildTrackStore()
  if (!data) return null

  const rows = payrollRows(data, filters)
  const totals = rows.reduce(
    (acc, row) => ({
      approved: acc.approved + row.approved_hours,
      overtime: acc.overtime + row.overtime_hours,
      gross: acc.gross + row.gross_amount,
      ready: acc.ready + (row.export_status === 'Hazır' || row.export_status === 'Göndərilib' ? 1 : 0),
    }),
    { approved: 0, overtime: 0, gross: 0, ready: 0 },
  )
  const columns: TableColumnsType<PayrollRow> = [
    { title: 'İşçi adı', dataIndex: 'full_name', sorter: (a, b) => a.full_name.localeCompare(b.full_name) },
    { title: 'Obyekt', dataIndex: 'site_name' },
    { title: 'Vəzifə', dataIndex: 'position' },
    { title: 'Maaş Tipi', dataIndex: 'salary_type' },
    { title: 'Tarif', dataIndex: 'salary_rate', render: (value) => formatCurrency(value) },
    { title: 'Normal Saat', dataIndex: 'normal_hours' },
    { title: 'Overtime', dataIndex: 'overtime_hours' },
    { title: 'İcazə', dataIndex: 'permission_hours' },
    { title: 'Riskli Saat', dataIndex: 'risky_hours' },
    { title: 'Təsdiqlənmiş Saat', dataIndex: 'approved_hours' },
    { title: 'Brutto', dataIndex: 'gross_amount', render: (value) => formatCurrency(value) },
    { title: 'Düzəliş', dataIndex: 'adjustment', render: (value) => formatCurrency(value) },
    { title: 'Yekun Məbləğ', dataIndex: 'net_amount', render: (value) => <strong>{formatCurrency(value)}</strong> },
    { title: 'Export', dataIndex: 'export_status', render: (value) => <StatusBadge status={value} /> },
  ]

  return (
    <div className="page-stack">
      <PageTitle title="5. Maaş və Mühasibat Hazırlıq Hesabatı" />
      <FilterBar data={data} showMonth showPosition advancedFields={['siteId', 'brigade', 'position', 'exportStatus']} />

      <section className="kpi-grid">
        <KpiCard icon={<DollarCircleOutlined />} title="Təsdiqli Saat" value={formatHours(totals.approved)} trend="+8% (öncəki aya nisbətən)" tone="green" />
        <KpiCard icon={<DollarCircleOutlined />} title="Overtime" value={formatHours(totals.overtime)} trend="+15% (öncəki aya nisbətən)" tone="orange" />
        <KpiCard icon={<WalletOutlined />} title="Brutto Məbləğ" value={formatCurrency(totals.gross)} trend="+7% (öncəki aya nisbətən)" tone="blue" />
        <KpiCard icon={<FileExcelOutlined />} title="Exporta Hazır" value={`${formatNumber((totals.ready / Math.max(1, rows.length)) * 100, 0)}%`} trend={`${totals.ready} işçi`} tone="green" />
      </section>

      <section className="content-grid wide-side">
        <DataTable title="Maaş Hazırlıq Cədvəli" columns={columns} data={rows} />
        <aside className="panel-card">
          <h2>Export və Mühasibat inteqrasiyası</h2>
          <div className="export-panel">
            <div className="export-option">
              <strong>Excel Export</strong>
              <p>Maaş hesabını .xlsx formatında yükləyin.</p>
              <ToolbarButton icon={<FileExcelOutlined />} tone="green" onClick={() => exportRowsToExcel('maas-hesabati', rows)}>Excel yüklə</ToolbarButton>
            </div>
            <div className="export-option">
              <strong>CSV Export</strong>
              <p>Sistemdə uyğun CSV formatında yükləyin.</p>
              <ToolbarButton icon={<FileTextOutlined />} tone="purple" onClick={() => exportRowsToCsv('maas-hesabati', rows)}>CSV yüklə</ToolbarButton>
            </div>
            <div className="export-option">
              <strong>1C üçün Export</strong>
              <p>1C mühasibat sisteminə uyğun fayl yaradın.</p>
              <ToolbarButton icon={<DownloadOutlined />} tone="orange" onClick={() => exportPayrollTo1CXml('maas-1c-export', rows)}>1C üçün hazırla</ToolbarButton>
            </div>
          </div>
        </aside>
      </section>

      <section className="explanation-grid">
        <ExplanationCard icon={<WalletOutlined />} title="Bu tablo niyə lazımdır?">
          <p>İşçilərin ay ərzində işlədiyi saatlar, overtime, icazə və riskli saatlara əsaslanaraq maaş hazırlığı üçün vahid mənbə yaradır.</p>
        </ExplanationCard>
        <ExplanationCard icon={<FileTextOutlined />} title="Əsas sütunlar" tone="orange">
          <ul>
            <li>Normal Saat və Overtime hesablamanın əsasını təşkil edir.</li>
            <li>Riskli Saat təsdiq prosesində ayrıca görünür.</li>
            <li>Yekun Məbləğ ödəniş üçün hazır məbləğdir.</li>
          </ul>
        </ExplanationCard>
      </section>
    </div>
  )
}
