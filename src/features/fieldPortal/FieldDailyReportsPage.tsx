import { EditOutlined, PlusOutlined, SendOutlined } from '@ant-design/icons'
import { Alert, Button, Card, DatePicker, Drawer, Form, Input, InputNumber, Select, Space, Table, message } from 'antd'
import dayjs from 'dayjs'
import { useEffect, useMemo, useState } from 'react'
import {
  BackendApiError,
  buildTrackBackendApi,
  type FieldDailyReport,
  type FieldSmetaItem,
  type SaveFieldDailyReportBody,
} from '../../services/api/buildTrackBackendApi'
import { FieldStatusTag } from './FieldStatusTag'
import { useFieldPortalStore } from './fieldPortalStore'

type ReportFormValues = {
  reportDate: dayjs.Dayjs
  weather?: string
  generalNote?: string
  lines: {
    id?: string
    smetaItemId: string
    reportedQuantity: number
    workerCount: number
    workHours: number
    note?: string
  }[]
}

const isUsableNumber = (value: unknown): value is number => value !== null && value !== undefined && Number.isFinite(Number(value))

const formatNumber = (value: unknown) => (isUsableNumber(value) ? Number(value).toLocaleString('az-AZ') : '—')

const formatHours = (value: unknown) => (isUsableNumber(value) ? `${Number(value).toLocaleString('az-AZ')} saat` : '—')

const getTotalWorkHours = (report: FieldDailyReport) => {
  const values = report.lines.map((line) => Number(line.workHours)).filter(Number.isFinite)
  if (!values.length) return undefined
  return values.reduce((sum, value) => sum + value, 0)
}

const canEditReport = (status: FieldDailyReport['status']) => status === 'Draft' || status === 'NeedsCorrection'

const submitLabel = (status: FieldDailyReport['status']) => status === 'NeedsCorrection' ? 'Yenidən göndər' : 'Göndər'

const replaceReport = (items: FieldDailyReport[], updated: FieldDailyReport) =>
  items.map((item) => (item.id === updated.id ? updated : item))

export const FieldDailyReportsPage = () => {
  const selectedSiteId = useFieldPortalStore((state) => state.selectedSiteId)
  const [reports, setReports] = useState<FieldDailyReport[]>([])
  const [smetaItems, setSmetaItems] = useState<FieldSmetaItem[]>([])
  const [loading, setLoading] = useState(false)
  const [saving, setSaving] = useState(false)
  const [submittingId, setSubmittingId] = useState<string | null>(null)
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [editingReport, setEditingReport] = useState<FieldDailyReport | null>(null)
  const [form] = Form.useForm<ReportFormValues>()

  const load = async () => {
    if (!selectedSiteId) return []
    setLoading(true)
    try {
      const [nextReports, nextItems] = await Promise.all([
        buildTrackBackendApi.getFieldDailyReports(selectedSiteId),
        buildTrackBackendApi.getFieldSmetaItems(selectedSiteId),
      ])
      setReports(nextReports)
      setSmetaItems(nextItems)
      return nextReports
    } finally {
      setLoading(false)
    }
  }

  const refreshReports = async (selectedReportId?: string) => {
    if (!selectedSiteId) return []
    const nextReports = await buildTrackBackendApi.getFieldDailyReports(selectedSiteId)
    setReports(nextReports)
    if (selectedReportId && editingReport?.id === selectedReportId) {
      const freshReport = nextReports.find((report) => report.id === selectedReportId)
      if (freshReport) setEditingReport(freshReport)
    }
    return nextReports
  }

  useEffect(() => {
    void load()
  }, [selectedSiteId])

  const smetaOptions = useMemo(
    () => smetaItems.map((item) => ({
      value: item.id,
      label: `${item.stageName} · ${item.workName} (${item.unit})`,
    })),
    [smetaItems],
  )

  const closeDrawer = () => {
    setDrawerOpen(false)
    setEditingReport(null)
    form.resetFields()
  }

  const openCreateDrawer = () => {
    setEditingReport(null)
    form.resetFields()
    form.setFieldsValue({
      reportDate: dayjs(),
      weather: undefined,
      generalNote: undefined,
      lines: [{ reportedQuantity: 0, workerCount: 1, workHours: 8 }],
    })
    setDrawerOpen(true)
  }

  const openEditDrawer = (report: FieldDailyReport) => {
    setEditingReport(report)
    form.resetFields()
    form.setFieldsValue({
      reportDate: dayjs(report.reportDate),
      weather: report.weatherCondition ?? report.weather,
      generalNote: report.generalNote,
      lines: report.lines.map((line) => ({
        id: line.id,
        smetaItemId: line.smetaItemId,
        reportedQuantity: line.reportedQuantity,
        workerCount: line.workerCount ?? 1,
        workHours: line.workHours ?? 8,
        note: line.note,
      })),
    })
    setDrawerOpen(true)
  }

  const saveReport = async (values: ReportFormValues) => {
    if (!selectedSiteId) return
    if (saving) return
    if (!editingReport) {
      const requestedDate = values.reportDate.format('YYYY-MM-DD')
      const existing = reports.find((report) => report.reportDate === requestedDate)
      if (existing) {
        message.warning(existing.status === 'NeedsCorrection'
          ? 'Bu tarix üçün hesabat artıq mövcuddur və düzəliş tələb olunur. Mövcud hesabatı redaktə edin.'
          : 'Bu tarix üçün hesabat artıq mövcuddur.')
        if (existing.status === 'NeedsCorrection' || existing.status === 'Draft') openEditDrawer(existing)
        return
      }
    }

    const body: SaveFieldDailyReportBody = {
      siteId: selectedSiteId,
      reportDate: editingReport?.reportDate ?? values.reportDate.format('YYYY-MM-DD'),
      weatherCondition: values.weather,
      generalNote: values.generalNote,
      lines: values.lines,
    }
    setSaving(true)
    try {
      if (editingReport) {
        const updated = await buildTrackBackendApi.updateFieldDailyReport(editingReport.id, body)
        setReports((items) => replaceReport(items, updated))
        setEditingReport(updated)
        message.success(editingReport.status === 'NeedsCorrection' ? 'Düzəlişlər saxlanıldı' : 'Gündəlik hesabat yeniləndi')
      } else {
        const created = await buildTrackBackendApi.saveFieldDailyReport(body)
        setReports((items) => [created, ...items.filter((item) => item.id !== created.id)])
        message.success('Gündəlik hesabat saxlanıldı')
      }
      closeDrawer()
      await refreshReports()
    } catch (error) {
      const text = error instanceof Error ? error.message : 'Gündəlik hesabat saxlanmadı'
      const friendly = error instanceof BackendApiError && error.status === 409 && text.includes('daily report already exists')
        ? text.includes('NeedsCorrection')
          ? 'Bu tarix üçün hesabat artıq mövcuddur və düzəliş tələb olunur. Mövcud hesabatı redaktə edin.'
          : 'Bu tarix üçün gündəlik hesabat artıq mövcuddur.'
        : error instanceof BackendApiError && error.status === 409 && text.includes('changed by another operation')
          ? 'Hesabat başqa əməliyyat zamanı dəyişdirilib. Səhifəni yeniləyib təkrar cəhd edin.'
          : text.includes('Reported quantity must be greater than zero')
            ? 'Fakt miqdar 0-dan böyük olmalıdır.'
            : text || 'Gündəlik hesabat saxlanmadı'
      message.error(friendly)
    } finally {
      setSaving(false)
    }
  }

  const submitReport = async (id: string) => {
    if (submittingId) return
    setSubmittingId(id)
    try {
      const updated = await buildTrackBackendApi.submitFieldDailyReport(id)
      setReports((items) => replaceReport(items, updated))
      if (editingReport?.id === updated.id) setEditingReport(updated)
      message.success('Hesabat rəhbərliyə göndərildi')
      await refreshReports(updated.id)
    } catch (error) {
      message.error(error instanceof Error ? error.message : 'Hesabat göndərilmədi')
    } finally {
      setSubmittingId(null)
    }
  }

  if (!selectedSiteId) return <Alert type="info" showIcon message="Obyekt seçin" />

  return (
    <div className="field-page">
      <div className="field-toolbar">
        <div>
          <span className="field-eyebrow">Faktiki iş həcmi</span>
          <h2>Gündəlik hesabat</h2>
        </div>
        <Button type="primary" icon={<PlusOutlined />} onClick={openCreateDrawer}>
          Yeni hesabat
        </Button>
      </div>
      <Card className="soft-card">
        <Table
          rowKey="id"
          loading={loading}
          dataSource={reports}
          pagination={{ pageSize: 8 }}
          columns={[
            { title: 'Tarix', dataIndex: 'reportDate' },
            { title: 'Obyekt', dataIndex: 'siteName' },
            { title: 'Sətir sayı', render: (_, row) => row.lines.length },
            {
              title: 'İş saatı',
              render: (_, row) => {
                const total = getTotalWorkHours(row)
                return total === undefined ? '—' : formatHours(total)
              },
            },
            {
              title: 'Status',
              dataIndex: 'status',
              render: (_, row) => <FieldStatusTag key={`${row.id}:${row.status}`} status={row.status} />,
            },
            {
              title: 'Əməliyyat',
              render: (_, row) => canEditReport(row.status)
                ? (
                  <Space wrap>
                    <Button icon={<EditOutlined />} disabled={Boolean(submittingId)} onClick={() => openEditDrawer(row)}>
                      Düzəliş et
                    </Button>
                    <Button icon={<SendOutlined />} loading={submittingId === row.id} disabled={Boolean(submittingId)} onClick={() => submitReport(row.id)}>
                      {submitLabel(row.status)}
                    </Button>
                  </Space>
                )
                : <span>Yalnız baxış</span>,
            },
          ]}
          expandable={{
            expandedRowRender: (row) => (
              <Space direction="vertical" className="full-width" size="middle">
                <Card size="small">
                  <Space direction="vertical" className="full-width">
                    {row.status === 'NeedsCorrection' && (
                      <Alert
                        showIcon
                        type="warning"
                        message="Düzəliş tələb olunur"
                        description={`Rəhbərin qeydi: ${row.reviewNote?.trim() || '—'}`}
                      />
                    )}
                    <span><strong>Hava şəraiti:</strong> {row.weatherCondition ?? row.weather ?? '—'}</span>
                    <span><strong>Ümumi qeyd:</strong> {row.generalNote?.trim() || '—'}</span>
                  </Space>
                </Card>
                <Table
                  size="small"
                  rowKey="id"
                  dataSource={row.lines}
                  pagination={false}
                  columns={[
                    { title: 'Etap', dataIndex: 'stageName' },
                    { title: 'İş', dataIndex: 'workName' },
                    { title: 'Miqdar', render: (_, line) => `${formatNumber(line.reportedQuantity)} ${line.unit || ''}`.trim() },
                    { title: 'İşçi', render: (_, line) => formatNumber(line.workerCount) },
                    { title: 'Saat', render: (_, line) => formatHours(line.workHours) },
                    { title: 'Qeyd', render: (_, line) => line.note?.trim() || '—' },
                  ]}
                />
              </Space>
            ),
          }}
        />
      </Card>
      <Drawer
        title={editingReport ? 'Gündəlik hesabatı redaktə et' : 'Yeni gündəlik hesabat'}
        open={drawerOpen}
        width={680}
        onClose={closeDrawer}
      >
        {editingReport?.status === 'NeedsCorrection' && (
          <Alert
            showIcon
            type="warning"
            style={{ marginBottom: 16 }}
            message="Düzəliş tələb olunur"
            description={`Rəhbərin qeydi: ${editingReport.reviewNote?.trim() || '—'}`}
          />
        )}
        <Form layout="vertical" form={form} onFinish={saveReport}>
          <Form.Item name="reportDate" label="Hesabat tarixi" rules={[{ required: true, message: 'Tarix seçin' }]}>
            <DatePicker className="full-width" disabled={Boolean(editingReport)} />
          </Form.Item>
          <Form.Item name="weather" label="Hava şəraiti">
            <Input placeholder="Məsələn: günəşli, küləkli" />
          </Form.Item>
          <Form.List name="lines">
            {(fields, { add, remove }) => (
              <Space direction="vertical" className="full-width">
                {fields.map((field) => (
                  <Card key={field.key} size="small" title={`İş sətri ${field.name + 1}`}>
                    <Form.Item name={[field.name, 'id']} hidden>
                      <Input />
                    </Form.Item>
                    <Form.Item name={[field.name, 'smetaItemId']} label="Smeta işi" rules={[{ required: true, message: 'İş seçin' }]}>
                      <Select showSearch options={smetaOptions} optionFilterProp="label" />
                    </Form.Item>
                    <Space wrap>
                      <Form.Item name={[field.name, 'reportedQuantity']} label="Fakt miqdar" rules={[{ required: true, message: 'Miqdar daxil edin' }]}>
                        <InputNumber min={0} />
                      </Form.Item>
                      <Form.Item name={[field.name, 'workerCount']} label="İşçi sayı" rules={[{ required: true, message: 'İşçi sayı daxil edin' }]}>
                        <InputNumber min={1} />
                      </Form.Item>
                      <Form.Item name={[field.name, 'workHours']} label="Saat" rules={[{ required: true, message: 'Saat daxil edin' }]}>
                        <InputNumber min={0} step={0.5} />
                      </Form.Item>
                    </Space>
                    <Form.Item name={[field.name, 'note']} label="Qeyd">
                      <Input.TextArea rows={2} />
                    </Form.Item>
                    {fields.length > 1 && <Button danger onClick={() => remove(field.name)}>Sətri sil</Button>}
                  </Card>
                ))}
                <Button onClick={() => add({ reportedQuantity: 0, workerCount: 1, workHours: 8 })}>İş sətri əlavə et</Button>
              </Space>
            )}
          </Form.List>
          <Form.Item name="generalNote" label="Ümumi qeyd">
            <Input.TextArea rows={3} />
          </Form.Item>
          <Button type="primary" htmlType="submit" loading={saving} disabled={saving}>
            {editingReport ? 'Yenilə' : 'Saxla'}
          </Button>
        </Form>
      </Drawer>
    </div>
  )
}
