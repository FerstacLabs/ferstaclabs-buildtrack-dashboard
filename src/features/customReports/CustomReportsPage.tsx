import { DownloadOutlined, FileSearchOutlined, FileTextOutlined, PlusOutlined, SettingOutlined } from '@ant-design/icons'
import { Button, Select, Tag, message } from 'antd'
import type { TableColumnsType } from 'antd'
import { useState } from 'react'
import { DonutChartCard } from '../../components/charts/DonutChartCard'
import { LineChartCard } from '../../components/charts/LineChartCard'
import { FilterBar } from '../../components/layout/FilterBar'
import { DataTable } from '../../components/tables/DataTable'
import { ExplanationCard } from '../../components/ui/ExplanationCard'
import { KpiCard } from '../../components/ui/KpiCard'
import { PageTitle } from '../../components/ui/PageTitle'
import { StatusBadge } from '../../components/ui/StatusBadge'
import { ToolbarButton } from '../../components/ui/ToolbarButton'
import { customReportRows, exportTrend, reportTypeDistribution } from '../../services/data/reportCalculations'
import { useBuildTrackStore } from '../../services/data/dataService'
import { exportRowsToExcel } from '../../services/data/exportService'
import type { CustomReportRow } from '../../types/reports'
import { formatNumber } from '../../utils/formatters'
import dayjs from 'dayjs'

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
  { label: 'Prorab Audit', value: 'audit' },
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

const sourceCategory = (source: string) => {
  if (source === 'payroll') return 'Maaş'
  if (source === 'risk') return 'Risk'
  if (source === 'audit') return 'Audit'
  return 'Davamiyyət'
}

export const CustomReportsPage = () => {
  const { addCustomReport, customReports, data, filters } = useBuildTrackStore()
  const [dataSource, setDataSource] = useState('attendance')
  const [selectedColumns, setSelectedColumns] = useState<string[]>(columnOptions.slice(0, 7))
  const [grouping, setGrouping] = useState('Obyekt -> Briqada')
  const [exportFormat, setExportFormat] = useState('Excel')
  if (!data) return null

  const rows = customReportRows(data, customReports, filters)
  const active = rows.filter((row) => row.status === 'Aktiv').length
  const recentExports = exportTrend().at(-1)?.export ?? 0
  const oneC = rows.filter((row) => row.export_format.includes('1C')).length
  const distribution = reportTypeDistribution(rows)
  const columns: TableColumnsType<CustomReportRow> = [
    { title: 'Hesabat adı', dataIndex: 'name', sorter: (a, b) => a.name.localeCompare(b.name) },
    { title: 'Kateqoriya', dataIndex: 'category', render: (value) => <Tag color="blue">{value}</Tag> },
    { title: 'Yaradılma Tarixi', dataIndex: 'created_at' },
    { title: 'Son Yenilənmə', dataIndex: 'updated_at' },
    { title: 'Filter sayı', dataIndex: 'filter_count' },
    { title: 'Sütun sayı', dataIndex: 'column_count' },
    { title: 'Export formatı', dataIndex: 'export_format' },
    { title: 'Sahibi', dataIndex: 'owner' },
    { title: 'Status', dataIndex: 'status', render: (value) => <StatusBadge status={value} /> },
    { title: 'Son İstifadə', dataIndex: 'last_used' },
  ]

  const createReport = () => {
    const category = sourceCategory(dataSource)
    const report: CustomReportRow = {
      key: `USER-${Date.now()}`,
      name: `${category} custom hesabatı`,
      category,
      report_type: dataSource,
      site_id: filters.siteId,
      brigade: filters.brigade,
      created_at: dayjs(filters.dateRange[1]).format('DD.MM.YYYY'),
      updated_at: dayjs(filters.dateRange[1]).format('DD.MM.YYYY'),
      filter_count: [filters.siteId, filters.brigade, filters.status, filters.riskLevel, filters.entryMethod].filter((value) => value !== 'all').length + 1,
      column_count: selectedColumns.length,
      export_format: exportFormat,
      owner: 'Demo istifadəçi',
      status: 'Aktiv',
      last_used: dayjs(filters.dateRange[1]).format('DD.MM.YYYY'),
    }
    addCustomReport(report)
    void message.success('Hesabat yaradıldı və yadda saxlanıldı')
  }

  return (
    <div className="page-stack">
      <PageTitle title="9. Custom Hesabatlar və Report Builder" />
      <FilterBar data={data} showReportType advancedFields={['dateRange', 'siteId', 'brigade', 'status', 'reportType']} />

      <section className="kpi-grid">
        <KpiCard icon={<FileTextOutlined />} title="Yaradılmış Hesabatlar" value={formatNumber(rows.length)} trend="demo insight: cari filtr" tone="green" />
        <KpiCard icon={<SettingOutlined />} title="Aktiv Şablonlar" value={formatNumber(active)} trend="demo insight: saxlanmış + hazır" tone="purple" />
        <KpiCard icon={<DownloadOutlined />} title="Son Exportlar" value={formatNumber(recentExports)} trend="demo insight: son 6 ay" tone="blue" />
        <KpiCard icon={<FileSearchOutlined />} title="1C Uyumlu Hesabatlar" value={formatNumber(oneC)} trend="demo insight: cari filtr" tone="green" />
      </section>

      <section className="content-grid wide-side">
        <DataTable
          title="Yaradılmış Custom Hesabatlar"
          columns={columns}
          data={rows}
          extra={<ToolbarButton icon={<DownloadOutlined />} onClick={() => exportRowsToExcel('custom-hesabatlar', rows)}>Excel Export</ToolbarButton>}
        />
        <aside className="panel-card builder-panel">
          <h2>Report Builder</h2>
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
    </div>
  )
}
