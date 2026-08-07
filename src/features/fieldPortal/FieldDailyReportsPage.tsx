import { PlusOutlined, SendOutlined } from '@ant-design/icons'
import { Alert, Button, Card, DatePicker, Drawer, Form, Input, InputNumber, Select, Space, Table, Tag, message } from 'antd'
import dayjs from 'dayjs'
import { useEffect, useMemo, useState } from 'react'
import {
  buildTrackBackendApi,
  type FieldDailyReport,
  type FieldSmetaItem,
  type SaveFieldDailyReportBody,
} from '../../services/api/buildTrackBackendApi'
import { fieldStatusColor, fieldStatusLabel, useFieldPortalStore } from './fieldPortalStore'

type ReportFormValues = {
  reportDate: dayjs.Dayjs
  weather?: string
  generalNote?: string
  lines: {
    smetaItemId: string
    completedQuantity: number
    workerCount: number
    workHours: number
    note?: string
  }[]
}

export const FieldDailyReportsPage = () => {
  const selectedSiteId = useFieldPortalStore((state) => state.selectedSiteId)
  const [reports, setReports] = useState<FieldDailyReport[]>([])
  const [smetaItems, setSmetaItems] = useState<FieldSmetaItem[]>([])
  const [loading, setLoading] = useState(false)
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [form] = Form.useForm<ReportFormValues>()

  const load = async () => {
    if (!selectedSiteId) return
    setLoading(true)
    try {
      const [nextReports, nextItems] = await Promise.all([
        buildTrackBackendApi.getFieldDailyReports(selectedSiteId),
        buildTrackBackendApi.getFieldSmetaItems(selectedSiteId),
      ])
      setReports(nextReports)
      setSmetaItems(nextItems)
    } finally {
      setLoading(false)
    }
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

  const saveReport = async (values: ReportFormValues) => {
    if (!selectedSiteId) return
    const body: SaveFieldDailyReportBody = {
      siteId: selectedSiteId,
      reportDate: values.reportDate.format('YYYY-MM-DD'),
      weather: values.weather,
      generalNote: values.generalNote,
      lines: values.lines,
    }
    await buildTrackBackendApi.saveFieldDailyReport(body)
    message.success('Gündəlik hesabat saxlandı')
    setDrawerOpen(false)
    form.resetFields()
    await load()
  }

  const submitReport = async (id: string) => {
    await buildTrackBackendApi.submitFieldDailyReport(id)
    message.success('Hesabat rəhbərliyə göndərildi')
    await load()
  }

  if (!selectedSiteId) return <Alert type="info" showIcon message="Obyekt seçin" />

  return (
    <div className="field-page">
      <div className="field-toolbar">
        <div>
          <span className="field-eyebrow">Faktiki iş həcmi</span>
          <h2>Gündəlik hesabat</h2>
        </div>
        <Button type="primary" icon={<PlusOutlined />} onClick={() => {
          form.setFieldsValue({ reportDate: dayjs(), lines: [{ completedQuantity: 0, workerCount: 1, workHours: 8 }] })
          setDrawerOpen(true)
        }}>
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
            { title: 'İş saatı', render: (_, row) => row.lines.reduce((sum, line) => sum + line.workHours, 0).toFixed(1) },
            { title: 'Status', dataIndex: 'status', render: (status) => <Tag color={fieldStatusColor(status)}>{fieldStatusLabel(status)}</Tag> },
            {
              title: 'Əməliyyat',
              render: (_, row) => row.status === 'Draft'
                ? <Button icon={<SendOutlined />} onClick={() => submitReport(row.id)}>Göndər</Button>
                : <span>Read-only</span>,
            },
          ]}
          expandable={{
            expandedRowRender: (row) => (
              <Table
                size="small"
                rowKey="id"
                dataSource={row.lines}
                pagination={false}
                columns={[
                  { title: 'Etap', dataIndex: 'stageName' },
                  { title: 'İş', dataIndex: 'workName' },
                  { title: 'Miqdar', render: (_, line) => `${line.completedQuantity} ${line.unit}` },
                  { title: 'İşçi', dataIndex: 'workerCount' },
                  { title: 'Saat', dataIndex: 'workHours' },
                  { title: 'Qeyd', dataIndex: 'note' },
                ]}
              />
            ),
          }}
        />
      </Card>
      <Drawer title="Yeni gündəlik hesabat" open={drawerOpen} width={680} onClose={() => setDrawerOpen(false)}>
        <Form layout="vertical" form={form} onFinish={saveReport}>
          <Form.Item name="reportDate" label="Hesabat tarixi" rules={[{ required: true, message: 'Tarix seçin' }]}>
            <DatePicker className="full-width" />
          </Form.Item>
          <Form.Item name="weather" label="Hava şəraiti">
            <Input placeholder="Məsələn: günəşli, küləkli" />
          </Form.Item>
          <Form.List name="lines">
            {(fields, { add, remove }) => (
              <Space direction="vertical" className="full-width">
                {fields.map((field) => (
                  <Card key={field.key} size="small" title={`İş sətri ${field.name + 1}`}>
                    <Form.Item name={[field.name, 'smetaItemId']} label="Smeta işi" rules={[{ required: true, message: 'İş seçin' }]}>
                      <Select showSearch options={smetaOptions} optionFilterProp="label" />
                    </Form.Item>
                    <Space wrap>
                      <Form.Item name={[field.name, 'completedQuantity']} label="Fakt miqdar" rules={[{ required: true }]}>
                        <InputNumber min={0} />
                      </Form.Item>
                      <Form.Item name={[field.name, 'workerCount']} label="İşçi sayı" rules={[{ required: true }]}>
                        <InputNumber min={1} />
                      </Form.Item>
                      <Form.Item name={[field.name, 'workHours']} label="Saat" rules={[{ required: true }]}>
                        <InputNumber min={0} step={0.5} />
                      </Form.Item>
                    </Space>
                    <Form.Item name={[field.name, 'note']} label="Qeyd">
                      <Input.TextArea rows={2} />
                    </Form.Item>
                    {fields.length > 1 && <Button danger onClick={() => remove(field.name)}>Sətri sil</Button>}
                  </Card>
                ))}
                <Button onClick={() => add({ completedQuantity: 0, workerCount: 1, workHours: 8 })}>İş sətri əlavə et</Button>
              </Space>
            )}
          </Form.List>
          <Form.Item name="generalNote" label="Ümumi qeyd">
            <Input.TextArea rows={3} />
          </Form.Item>
          <Button type="primary" htmlType="submit">Saxla</Button>
        </Form>
      </Drawer>
    </div>
  )
}
