import { DownloadOutlined, EyeOutlined, FileSearchOutlined, FileTextOutlined, PlusOutlined, SettingOutlined } from '@ant-design/icons'
import { Button, Drawer, Empty, Select, Space, Table, Tag, message } from 'antd'
import type { TableColumnsType } from 'antd'
import { useState } from 'react'
import dayjs from 'dayjs'
import { DonutChartCard } from '../../components/charts/DonutChartCard'
import { LineChartCard } from '../../components/charts/LineChartCard'
import { FilterBar } from '../../components/layout/FilterBar'
import { DataTable } from '../../components/tables/DataTable'
import { ExplanationCard } from '../../components/ui/ExplanationCard'
import { KpiCard } from '../../components/ui/KpiCard'
import { PageTitle } from '../../components/ui/PageTitle'
import { StatusBadge } from '../../components/ui/StatusBadge'
import { ToolbarButton } from '../../components/ui/ToolbarButton'
import {
  costCodeRows,
  customReportRows,
  dailyAttendanceRows,
  exportTrend,
  payrollRows,
  reportTypeDistribution,
  riskWorkerRows,
  siteHoursRows,
  supervisorRows,
} from '../../services/data/reportCalculations'
import { useBuildTrackStore } from '../../services/data/dataService'
import { exportRowsTo1CMock, exportRowsToCsv, exportRowsToExcel } from '../../services/data/exportService'
import type { CustomReportRow, ReportFilters } from '../../types/reports'
import { formatNumber } from '../../utils/formatters'

const columnOptions = [
  'İşçi adı',
  'Obyekt',
  'Briqada',
  'Tarix',
  'Giriş saatı',
  'Çıxış saatı',
  'Davamiyyət %',
  'Risk səviyyəsi',
  'Overtime saatı',
  'Yekun məbləğ',
]

const dataSourceOptions = [
  { label: 'Davamiyyət və Saatlar', value: 'attendance' },
  { label: 'Maaş Hesabatı', value: 'payroll' },
  { label: 'Riskli İşçilər', value: 'risk' },
  { label: 'Obyekt Saatları', value: 'hours' },
  { label: 'Prorab Audit', value: 'audit' },
  { label: 'İş Fazası & Cost Code', value: 'costcode' },
]

const groupingOptions = [
  { label: 'Obyekt -> Briqada', value: 'Obyekt -> Briqada' },
  { label: 'Briqada -> Vəzifə', value: 'Briqada -> Vəzifə' },
  { label: 'Risk səviyyəsi', value: 'Risk səviyyəsi' },
  { label: 'Export statusu', value: 'Export statusu' },
]

const exportFormatOptions = [
  { label: 'Excel', value: 'Excel' },
  { label: 'CSV', value: 'CSV' },
  { label: 'PDF', value: 'PDF' },
  { label: '1C XML', value: '1C XML' },
]

type PreviewRow = Record<string, string | number | boolean | null | undefined>

const sourceCategory = (source: string) => {
  if (source === 'payroll') return 'Maaş'
  if (source === 'risk') return 'Risk'
  if (source === 'audit') return 'Audit'
  if (source === 'hours') return 'Saatlar'
  if (source === 'costcode') return 'Cost Code'
  return 'Davamiyyət'
}

const sourceLabel = (source: string) => dataSourceOptions.find((option) => option.value === source)?.label ?? 'Davamiyyət və Saatlar'

const baseReportName = (source: string, filters: ReportFilters) => {
  const date = dayjs(filters.dateRange[1]).format('DD.MM.YYYY')
  if (source === 'payroll') return `Maaş hesabatı — ${dayjs(filters.month).format('MMMM YYYY')}`
  if (source === 'risk') return 'Risk hesabatı — Kritik işçilər'
  if (source === 'audit') return `Prorab audit hesabatı — ${date}`
  if (source === 'hours') return `Obyekt saatları hesabatı — ${date}`
  if (source === 'costcode') return `Cost Code hesabatı — ${date}`
  return `Davamiyyət hesabatı — ${date}`
}

const reportFiltersForRow = (row: CustomReportRow, filters: ReportFilters): ReportFilters => ({
  ...filters,
  siteId: row.site_id === 'all' ? filters.siteId : row.site_id,
  brigade: row.brigade === 'all' ? filters.brigade : row.brigade,
  reportType: row.report_type,
})

export const CustomReportsPage = () => {
  const { addCustomReport, customReports, data, filters } = useBuildTrackStore()
  const [dataSource, setDataSource] = useState('attendance')
  const [selectedColumns, setSelectedColumns] = useState<string[]>(columnOptions.slice(0, 7))
  const [grouping, setGrouping] = useState('Obyekt -> Briqada')
  const [exportFormat, setExportFormat] = useState('Excel')
  const [previewReport, setPreviewReport] = useState<CustomReportRow | null>(null)
  if (!data) return null

  const rows = customReportRows(data, customReports, filters)
  const active = rows.filter((row) => row.status === 'Aktiv').length
  const recentExports = exportTrend().at(-1)?.export ?? 0
  const oneC = rows.filter((row) => row.export_format.includes('1C')).length
  const distribution = reportTypeDistribution(rows)

  const getPreviewRows = (report: CustomReportRow): PreviewRow[] => {
    const reportType = report.data_source ?? report.report_type
    const reportFilters = reportFiltersForRow(report, filters)
    const sourceRows =
      reportType === 'payroll'
        ? payrollRows(data, reportFilters)
        : reportType === 'risk'
          ? riskWorkerRows(data, reportFilters)
          : reportType === 'hours'
            ? siteHoursRows(data, reportFilters)
            : reportType === 'audit'
              ? supervisorRows(data, reportFilters)
              : reportType === 'costcode'
                ? costCodeRows(data, reportFilters)
                : dailyAttendanceRows(data, reportFilters)

    return sourceRows.slice(0, 25).map((row) => row as unknown as PreviewRow)
  }

  const previewRows = previewReport ? getPreviewRows(previewReport) : []
  const previewColumns: TableColumnsType<PreviewRow> = (() => {
    const firstRow = previewRows[0]
    if (!firstRow) return []
    return Object.keys(firstRow)
      .filter((key) => key !== 'key')
      .slice(0, 9)
      .map((key) => ({
        title: key.replaceAll('_', ' '),
        dataIndex: key,
        render: (value) => String(value ?? '-'),
      }))
  })()

  const exportReport = (report: CustomReportRow, format = report.export_format) => {
    const exportRows = getPreviewRows(report)
    const fileName = report.name.toLowerCase().replace(/[^a-z0-9əöğüçıŞƏÖĞÜÇİ]+/gi, '-').replace(/^-|-$/g, '') || 'custom-hesabat'

    if (format.includes('1C')) {
      exportRowsTo1CMock(fileName, exportRows)
    } else if (format.includes('CSV')) {
      exportRowsToCsv(fileName, exportRows)
    } else {
      exportRowsToExcel(fileName, exportRows)
    }
  }

  const columns: TableColumnsType<CustomReportRow> = [
    {
      title: 'Hesabat adı',
      dataIndex: 'name',
      sorter: (a, b) => a.name.localeCompare(b.name),
      render: (value, row) => (
        <Space size={6} wrap>
          <span>{value}</span>
          {row.key.startsWith('USER-') && <Tag color="green">Yeni</Tag>}
        </Space>
      ),
    },
    { title: 'Kateqoriya', dataIndex: 'category', render: (value) => <Tag color="blue">{value}</Tag> },
    { title: 'Yaradılma Tarixi', dataIndex: 'created_at' },
    { title: 'Son Yenilənmə', dataIndex: 'updated_at' },
    { title: 'Filter sayı', dataIndex: 'filter_count' },
    { title: 'Sütun sayı', dataIndex: 'column_count' },
    { title: 'Export formatı', dataIndex: 'export_format' },
    { title: 'Sahibi', dataIndex: 'owner' },
    { title: 'Status', dataIndex: 'status', render: (value) => <StatusBadge status={value} /> },
    { title: 'Son İstifadə', dataIndex: 'last_used' },
    {
      title: 'Əməliyyatlar',
      key: 'actions',
      fixed: 'right',
      render: (_, row) => (
        <Space>
          <Button size="small" icon={<EyeOutlined />} onClick={() => setPreviewReport(row)}>Bax</Button>
          <Button size="small" icon={<DownloadOutlined />} onClick={() => exportReport(row)}>Export</Button>
        </Space>
      ),
    },
  ]

  const createReport = () => {
    const category = sourceCategory(dataSource)
    const baseName = baseReportName(dataSource, filters)
    const sameNameCount = customReports.filter((report) => report.name.startsWith(baseName)).length
    const name = sameNameCount ? `${baseName} #${sameNameCount + 1}` : baseName
    const appliedFilters = [
      `Tarix: ${filters.dateRange[0]} - ${filters.dateRange[1]}`,
      `Obyekt: ${filters.siteId === 'all' ? 'Hamısı' : filters.siteId}`,
      `Briqada: ${filters.brigade === 'all' ? 'Hamısı' : filters.brigade}`,
      `Status: ${filters.status === 'all' ? 'Hamısı' : filters.status}`,
    ]
    const report: CustomReportRow = {
      key: `USER-${Date.now()}`,
      name,
      category,
      report_type: dataSource,
      data_source: dataSource,
      site_id: filters.siteId,
      brigade: filters.brigade,
      created_at: dayjs(filters.dateRange[1]).format('DD.MM.YYYY'),
      updated_at: dayjs(filters.dateRange[1]).format('DD.MM.YYYY'),
      filter_count: appliedFilters.length,
      column_count: selectedColumns.length,
      export_format: exportFormat,
      owner: 'Sistem istifadəçisi',
      status: 'Aktiv',
      last_used: dayjs(filters.dateRange[1]).format('DD.MM.YYYY'),
      selected_columns: selectedColumns,
      applied_filters: appliedFilters,
      grouping,
    }
    addCustomReport(report)
    setPreviewReport(report)
    void message.success("Hesabat yaradıldı. Cədvəldə 'Bax' düyməsi ilə önizləyə bilərsiniz.")
  }

  return (
    <div className="page-stack">
      <PageTitle title="9. Custom Hesabatlar və Report Builder" />
      <FilterBar data={data} showReportType advancedFields={['dateRange', 'siteId', 'brigade', 'status', 'reportType']} />

      <section className="kpi-grid">
        <KpiCard icon={<FileTextOutlined />} title="Yaradılmış Hesabatlar" value={formatNumber(rows.length)} trend="cari filtr" tone="green" />
        <KpiCard icon={<SettingOutlined />} title="Aktiv Şablonlar" value={formatNumber(active)} trend="saxlanmış + hazır" tone="purple" />
        <KpiCard icon={<DownloadOutlined />} title="Son Exportlar" value={formatNumber(recentExports)} trend="son 6 ay" tone="blue" />
        <KpiCard icon={<FileSearchOutlined />} title="1C Uyumlu Hesabatlar" value={formatNumber(oneC)} trend="cari filtr" tone="green" />
      </section>

      <section className="content-grid wide-side">
        <DataTable
          title="Yaradılmış Custom Hesabatlar"
          columns={columns}
          data={rows}
          emptyText={<Empty description="Bu filterlərə uyğun hesabat tapılmadı." />}
          extra={<ToolbarButton icon={<DownloadOutlined />} onClick={() => exportRowsToExcel('custom-hesabatlar', rows)}>Excel Export</ToolbarButton>}
        />
        <aside className="panel-card builder-panel">
          <h2>Report Builder</h2>
          <p className="report-builder-helper">Yaradılan hesabatlar soldakı cədvələ əlavə olunur. “Bax” ilə önizləyə, “Export” ilə fayl kimi yükləyə bilərsiniz.</p>
          <div className="builder-group">
            <label>1. Data mənbəyi</label>
            <Select value={dataSource} onChange={setDataSource} options={dataSourceOptions} />
          </div>
          <div className="builder-group">
            <label>2. Sütunlar</label>
            <Select
              mode="multiple"
              value={selectedColumns}
              onChange={setSelectedColumns}
              options={columnOptions.map((item) => ({ label: item, value: item }))}
            />
            <Button icon={<PlusOutlined />} onClick={() => setSelectedColumns(columnOptions)}>Bütün sütunlar</Button>
          </div>
          <div className="builder-group">
            <label>3. Filtrlər</label>
            <div className="tag-cloud">
              {[
                `Tarix: ${filters.dateRange[0]} - ${filters.dateRange[1]}`,
                `Obyekt: ${filters.siteId === 'all' ? 'Hamısı' : filters.siteId}`,
                `Briqada: ${filters.brigade === 'all' ? 'Hamısı' : filters.brigade}`,
                `Status: ${filters.status === 'all' ? 'Hamısı' : filters.status}`,
              ].map((item) => (
                <Tag key={item}>{item}</Tag>
              ))}
            </div>
          </div>
          <div className="builder-group">
            <label>4. Qruplaşdırma</label>
            <Select value={grouping} onChange={setGrouping} options={groupingOptions} />
          </div>
          <div className="builder-group">
            <label>5. Export formatı</label>
            <Select value={exportFormat} onChange={setExportFormat} options={exportFormatOptions} />
          </div>
          <ToolbarButton icon={<FileSearchOutlined />} tone="green" onClick={createReport}>Hesabat Yarat</ToolbarButton>
        </aside>
      </section>

      <section className="chart-grid">
        <DonutChartCard title="Ən çox istifadə olunan hesabat növləri" data={distribution} centerValue={formatNumber(rows.length)} centerLabel="cəmi" />
        <LineChartCard title="Export trendi (Son 6 ay)" data={exportTrend()} lines={[{ dataKey: 'export', color: '#1479ff', name: 'Export sayı' }]} />
      </section>

      <section className="explanation-grid">
        <ExplanationCard icon={<FileSearchOutlined />} title="Bu tablo niyə lazımdır?">
          <p>Custom hesabat modulu istifadəçilərə ehtiyaclarına uyğun dinamik hesabat yaratmaq və ixrac etmək imkanı verir.</p>
        </ExplanationCard>
        <ExplanationCard icon={<SettingOutlined />} title="Əsas xüsusiyyətlər" tone="orange">
          <ul>
            <li>Sütun seçimi və cari filtr konteksti.</li>
            <li>Çoxsaylı filtr və qruplaşdırma seçimləri.</li>
            <li>Excel, PDF, CSV və 1C XML export.</li>
          </ul>
        </ExplanationCard>
      </section>

      <Drawer
        title="Hesabat önizləməsi"
        width={860}
        open={Boolean(previewReport)}
        onClose={() => setPreviewReport(null)}
        extra={previewReport && (
          <Space>
            <Button icon={<DownloadOutlined />} onClick={() => exportReport(previewReport, 'Excel')}>Excel</Button>
            <Button icon={<DownloadOutlined />} onClick={() => exportReport(previewReport, 'CSV')}>CSV</Button>
            {previewReport.export_format.includes('1C') && <Button icon={<DownloadOutlined />} onClick={() => exportReport(previewReport, '1C XML')}>1C XML/TXT</Button>}
          </Space>
        )}
      >
        {previewReport && (
          <div className="report-preview-stack">
            <section className="preview-meta-grid">
              <div><span>Hesabat adı</span><strong>{previewReport.name}</strong></div>
              <div><span>Kateqoriya</span><strong>{previewReport.category}</strong></div>
              <div><span>Data mənbəyi</span><strong>{sourceLabel(previewReport.data_source ?? previewReport.report_type)}</strong></div>
              <div><span>Export formatı</span><strong>{previewReport.export_format}</strong></div>
              <div><span>Qruplaşdırma</span><strong>{previewReport.grouping ?? 'Cari qruplaşdırma'}</strong></div>
              <div><span>Sütun sayı</span><strong>{previewReport.column_count}</strong></div>
            </section>
            <section className="preview-tag-section">
              <h3>Seçilmiş sütunlar</h3>
              <Space wrap>{(previewReport.selected_columns ?? columnOptions.slice(0, previewReport.column_count)).map((item) => <Tag key={item}>{item}</Tag>)}</Space>
            </section>
            <section className="preview-tag-section">
              <h3>Tətbiq olunan filtrlər</h3>
              <Space wrap>{(previewReport.applied_filters ?? ['Cari səhifə filtrləri']).map((item) => <Tag key={item}>{item}</Tag>)}</Space>
            </section>
            <Table<PreviewRow>
              columns={previewColumns}
              dataSource={previewRows}
              locale={{ emptyText: <Empty description="Önizləmə üçün məlumat tapılmadı." /> }}
              pagination={{ pageSize: 8 }}
              rowKey={(row, index) => String(row.key ?? index)}
              scroll={{ x: 'max-content' }}
              size="middle"
            />
          </div>
        )}
      </Drawer>
    </div>
  )
}


