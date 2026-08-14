import { EyeOutlined, ProfileOutlined, ReloadOutlined } from '@ant-design/icons'
import { Alert, Button, DatePicker, Drawer, Select, Space, Table, Tag, Typography, message } from 'antd'
import type { TableColumnsType } from 'antd'
import type { Dayjs } from 'dayjs'
import dayjs from 'dayjs'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { ProjectSelect } from '../../components/ProjectSelect'
import { KpiCard } from '../../components/ui/KpiCard'
import { PageTitle } from '../../components/ui/PageTitle'
import { buildTrackBackendApi, type FieldDailyReport, type FieldDailyReportLine, type FieldDailyReportStatus } from '../../services/api/buildTrackBackendApi'
import { formatNumber } from '../../utils/formatters'
import { ALL_OBJECTS_ID } from '../projectProgress/projectSelectors'
import { useProjectSelectionStore } from '../../stores/projectSelectionStore'
import {
  dailyReportQuantitySummary,
  dailyReportWorkSummary,
  fieldDailyReportStatusColor,
  fieldDailyReportStatusLabel,
  fieldDailyReportStatusOptions,
  totalDailyReportLineValue,
} from './dailyReportHelpers'

const { RangePicker } = DatePicker

export const DailyReportsPage = () => {
  const selectedObjectId = useProjectSelectionStore((state) => state.selectedProjectId)
  const [reports, setReports] = useState<FieldDailyReport[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [dateRange, setDateRange] = useState<[Dayjs, Dayjs] | null>(() => [dayjs().subtract(30, 'day'), dayjs()])
  const [statusFilter, setStatusFilter] = useState<FieldDailyReportStatus | 'all'>('all')
  const [supervisorFilter, setSupervisorFilter] = useState<string>('all')
  const [selectedReport, setSelectedReport] = useState<FieldDailyReport | null>(null)

  const loadReports = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const siteId = selectedObjectId && selectedObjectId !== ALL_OBJECTS_ID ? selectedObjectId : undefined
      const rows = await buildTrackBackendApi.getManagementFieldReports(siteId)
      setReports(rows)
    } catch (err) {
      const text = err instanceof Error ? err.message : 'Gündəlik hesabatlar yüklənmədi'
      setError(text)
      void message.error(text)
    } finally {
      setLoading(false)
    }
  }, [selectedObjectId])

  useEffect(() => {
    void loadReports()
  }, [loadReports])

  const supervisorOptions = useMemo(() => {
    const names = Array.from(new Set(reports.map((report) => report.supervisorName).filter(Boolean)))
    return [{ value: 'all', label: 'Bütün prorablar' }, ...names.map((name) => ({ value: name!, label: name! }))]
  }, [reports])

  const filteredReports = useMemo(() => {
    return reports.filter((report) => {
      const reportDate = dayjs(report.reportDate)
      const inDateRange = !dateRange || (reportDate.isSame(dateRange[0], 'day') || reportDate.isAfter(dateRange[0], 'day')) && (reportDate.isSame(dateRange[1], 'day') || reportDate.isBefore(dateRange[1], 'day'))
      const statusMatches = statusFilter === 'all' || report.status === statusFilter
      const supervisorMatches = supervisorFilter === 'all' || report.supervisorName === supervisorFilter
      return inDateRange && statusMatches && supervisorMatches
    })
  }, [dateRange, reports, statusFilter, supervisorFilter])

  const submittedCount = filteredReports.filter((report) => report.status === 'Submitted').length
  const approvedCount = filteredReports.filter((report) => report.status === 'Approved').length
  const correctionCount = filteredReports.filter((report) => report.status === 'NeedsCorrection').length
  const totalHours = filteredReports.reduce((sum, report) => sum + totalDailyReportLineValue(report.lines, 'workHours'), 0)

  const columns: TableColumnsType<FieldDailyReport> = [
    { title: 'Tarix', dataIndex: 'reportDate', sorter: (a, b) => a.reportDate.localeCompare(b.reportDate), width: 120 },
    { title: 'Layihə', dataIndex: 'siteName', sorter: (a, b) => (a.siteName || '').localeCompare(b.siteName || ''), width: 180 },
    { title: 'Prorab', dataIndex: 'supervisorName', sorter: (a, b) => (a.supervisorName || '').localeCompare(b.supervisorName || ''), width: 170 },
    { title: 'Hava', dataIndex: 'weatherCondition', width: 120, render: (value) => value || '-' },
    { title: 'Görülən işlər', render: (_, row) => dailyReportWorkSummary(row.lines), ellipsis: true },
    { title: 'Miqdar', render: (_, row) => dailyReportQuantitySummary(row.lines), width: 170 },
    { title: 'İşçi', align: 'right', width: 90, render: (_, row) => formatNumber(totalDailyReportLineValue(row.lines, 'workerCount')) },
    { title: 'İş saatı', align: 'right', width: 110, render: (_, row) => formatNumber(totalDailyReportLineValue(row.lines, 'workHours')) },
    { title: 'Status', dataIndex: 'status', width: 170, render: (value: FieldDailyReportStatus) => <Tag color={fieldDailyReportStatusColor[value]}>{fieldDailyReportStatusLabel[value]}</Tag> },
    {
      title: 'Əməliyyat',
      width: 90,
      render: (_, row) => <Button icon={<EyeOutlined />} onClick={() => setSelectedReport(row)}>Bax</Button>,
    },
  ]

  return (
    <div className="page-stack">
      <PageTitle
        title="Gündəlik hesabatlar"
        subtitle="Prorab Field Portal hesabatlarının tam tarixçəsi və təsdiq statusu"
        extra={(
          <Space wrap>
            <ProjectSelect pageKey="dailyReports" />
            <Button icon={<ReloadOutlined />} onClick={loadReports} loading={loading}>Yenilə</Button>
          </Space>
        )}
      />

      {error && <Alert type="error" showIcon message="Hesabatlar yüklənmədi" description={error} />}

      <section className="filter-bar">
        <RangePicker
          value={dateRange}
          onChange={(value) => setDateRange(value && value[0] && value[1] ? [value[0], value[1]] : null)}
          format="YYYY-MM-DD"
        />
        <Select value={supervisorFilter} onChange={setSupervisorFilter} options={supervisorOptions} style={{ minWidth: 220 }} />
        <Select
          value={statusFilter}
          onChange={setStatusFilter}
          options={[{ value: 'all', label: 'Bütün statuslar' }, ...fieldDailyReportStatusOptions]}
          style={{ minWidth: 220 }}
        />
      </section>

      <section className="kpi-grid four">
        <KpiCard icon={<ProfileOutlined />} title="Hesabat sayı" value={formatNumber(filteredReports.length)} tone="blue" />
        <KpiCard icon={<ProfileOutlined />} title="Təsdiqlənmiş" value={formatNumber(approvedCount)} tone="green" />
        <KpiCard icon={<ProfileOutlined />} title="Təsdiq gözləyir" value={formatNumber(submittedCount)} tone="orange" />
        <KpiCard icon={<ProfileOutlined />} title="İş saatı" value={formatNumber(totalHours)} tone={correctionCount > 0 ? 'purple' : 'blue'} />
      </section>

      <section className="table-card">
        <div className="card-heading">
          <h2>Canonical prorab hesabatları</h2>
          <p>Bu siyahı Field Portal və Management təsdiq axını ilə eyni backend modelindən oxunur.</p>
        </div>
        <Table
          rowKey="id"
          columns={columns}
          dataSource={filteredReports}
          loading={loading}
          pagination={{ pageSize: 10 }}
          scroll={{ x: 1250 }}
          locale={{ emptyText: 'Bu filterlərə uyğun hesabat tapılmadı' }}
        />
      </section>

      <Drawer
        title="Gündəlik hesabat detalları"
        open={!!selectedReport}
        width={680}
        onClose={() => setSelectedReport(null)}
      >
        {selectedReport && (
          <Space direction="vertical" size={18} style={{ width: '100%' }}>
            <Space wrap>
              <Tag color={fieldDailyReportStatusColor[selectedReport.status]}>{fieldDailyReportStatusLabel[selectedReport.status]}</Tag>
              <Typography.Text strong>{selectedReport.reportDate}</Typography.Text>
              <Typography.Text>{selectedReport.siteName}</Typography.Text>
              <Typography.Text>{selectedReport.supervisorName}</Typography.Text>
            </Space>
            <Typography.Paragraph>{selectedReport.generalNote || 'Ümumi qeyd daxil edilməyib.'}</Typography.Paragraph>
            {selectedReport.reviewNote && (
              <Alert type={selectedReport.status === 'Rejected' ? 'error' : 'warning'} showIcon message="Management qeydi" description={selectedReport.reviewNote} />
            )}
            <Table
              rowKey="id"
              columns={[
                { title: 'Etap', dataIndex: 'stageName' },
                { title: 'İş', dataIndex: 'workName' },
                { title: 'Miqdar', render: (_: unknown, line: FieldDailyReportLine) => `${formatNumber(line.reportedQuantity)} ${line.unit}` },
                { title: 'İşçi', dataIndex: 'workerCount', align: 'right' },
                { title: 'Saat', dataIndex: 'workHours', align: 'right' },
                { title: 'Qeyd', dataIndex: 'note', render: (value?: string) => value || '-' },
              ]}
              dataSource={selectedReport.lines}
              pagination={false}
              size="small"
            />
          </Space>
        )}
      </Drawer>
    </div>
  )
}
