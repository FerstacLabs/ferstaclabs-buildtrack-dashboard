import { PlusOutlined } from '@ant-design/icons'
import { Alert, Button, Card, DatePicker, Drawer, Form, Input, Select, Space, Table, Tag, message } from 'antd'
import dayjs from 'dayjs'
import { useEffect, useMemo, useState } from 'react'
import {
  buildTrackBackendApi,
  type FieldWorker,
  type FieldWorkerEvent,
  type FieldWorkerEventType,
} from '../../services/api/buildTrackBackendApi'
import { fieldStatusColor, fieldStatusLabel, useFieldPortalStore } from './fieldPortalStore'

const eventTypeOptions: { value: FieldWorkerEventType; label: string }[] = [
  { value: 'Late', label: 'Gecikmə' },
  { value: 'LeftEarly', label: 'Erkən çıxış' },
  { value: 'Absent', label: 'Gəlməyib' },
  { value: 'Permission', label: 'İcazə' },
  { value: 'Medical', label: 'Tibbi səbəb' },
  { value: 'SiteTransfer', label: 'Obyekt dəyişimi' },
  { value: 'SafetyWarning', label: 'Təhlükəsizlik xəbərdarlığı' },
  { value: 'ManualAttendanceCorrectionRequest', label: 'Davamiyyət düzəlişi sorğusu' },
  { value: 'Other', label: 'Digər' },
]

export const FieldWorkersPage = () => {
  const selectedSiteId = useFieldPortalStore((state) => state.selectedSiteId)
  const [workers, setWorkers] = useState<FieldWorker[]>([])
  const [events, setEvents] = useState<FieldWorkerEvent[]>([])
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [loading, setLoading] = useState(false)
  const [form] = Form.useForm()

  const load = async () => {
    if (!selectedSiteId) return
    setLoading(true)
    try {
      const [nextWorkers, nextEvents] = await Promise.all([
        buildTrackBackendApi.getFieldWorkers(selectedSiteId),
        buildTrackBackendApi.getFieldWorkerEvents(selectedSiteId),
      ])
      setWorkers(nextWorkers)
      setEvents(nextEvents)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void load()
  }, [selectedSiteId])

  const workerOptions = useMemo(() => workers.map((worker) => ({ value: worker.id, label: `${worker.fullName} · ${worker.externalWorkerCode}` })), [workers])

  const createEvent = async (values: { workerId: string; eventType: FieldWorkerEventType; eventDateTime: dayjs.Dayjs; reason: string }) => {
    if (!selectedSiteId) return
    await buildTrackBackendApi.createFieldWorkerEvent({
      siteId: selectedSiteId,
      workerId: values.workerId,
      eventType: values.eventType,
      eventDateTime: values.eventDateTime.toISOString(),
      reason: values.reason,
    })
    message.success('İşçi qeydi rəhbərliyə göndərildi')
    setDrawerOpen(false)
    form.resetFields()
    await load()
  }

  if (!selectedSiteId) return <Alert type="info" showIcon message="Obyekt seçin" />

  return (
    <div className="field-page">
      <div className="field-toolbar">
        <div>
          <span className="field-eyebrow">İşçi davamiyyəti</span>
          <h2>İşçi qeydləri</h2>
        </div>
        <Button type="primary" icon={<PlusOutlined />} onClick={() => {
          form.setFieldsValue({ eventDateTime: dayjs() })
          setDrawerOpen(true)
        }}>
          Qeyd yarat
        </Button>
      </div>
      <Card className="soft-card" title="Obyekt işçiləri">
        <Table
          rowKey="id"
          loading={loading}
          dataSource={workers}
          pagination={{ pageSize: 8 }}
          columns={[
            { title: 'İşçi', dataIndex: 'fullName' },
            { title: 'ID', dataIndex: 'externalWorkerCode' },
            { title: 'Briqada', dataIndex: 'brigade' },
            { title: 'Vəzifə', dataIndex: 'role' },
            { title: 'Bugünkü status', dataIndex: 'todayStatus' },
            { title: 'Son görülmə', dataIndex: 'lastSeenAt', render: (value) => value ? new Date(value).toLocaleTimeString('az-AZ') : '-' },
            { title: 'Risk', dataIndex: 'riskScore', render: (value) => <Tag color={value > 60 ? 'red' : value > 30 ? 'orange' : 'green'}>{value}</Tag> },
          ]}
        />
      </Card>
      <Card className="soft-card" title="Prorab tərəfindən göndərilən qeydlər">
        <Table
          rowKey="id"
          loading={loading}
          dataSource={events}
          pagination={{ pageSize: 6 }}
          columns={[
            { title: 'İşçi', dataIndex: 'workerName' },
            { title: 'Hadisə', dataIndex: 'eventType' },
            { title: 'Tarix', dataIndex: 'eventDateTime', render: (value) => new Date(value).toLocaleString('az-AZ') },
            { title: 'Risk artımı', dataIndex: 'riskDelta' },
            { title: 'Status', dataIndex: 'status', render: (status) => <Tag color={fieldStatusColor(status)}>{fieldStatusLabel(status)}</Tag> },
            { title: 'Səbəb', dataIndex: 'reason' },
          ]}
        />
      </Card>
      <Drawer title="İşçi qeydi yarat" open={drawerOpen} width={560} onClose={() => setDrawerOpen(false)}>
        <Form layout="vertical" form={form} onFinish={createEvent}>
          <Form.Item name="workerId" label="İşçi" rules={[{ required: true, message: 'İşçi seçin' }]}>
            <Select showSearch options={workerOptions} optionFilterProp="label" />
          </Form.Item>
          <Form.Item name="eventType" label="Hadisə növü" rules={[{ required: true, message: 'Hadisə seçin' }]}>
            <Select options={eventTypeOptions} />
          </Form.Item>
          <Form.Item name="eventDateTime" label="Vaxt" rules={[{ required: true, message: 'Vaxt seçin' }]}>
            <DatePicker showTime className="full-width" />
          </Form.Item>
          <Form.Item name="reason" label="Səbəb / qeyd" rules={[{ required: true, message: 'Səbəb yazın' }]}>
            <Input.TextArea rows={4} />
          </Form.Item>
          <Space>
            <Button type="primary" htmlType="submit">Göndər</Button>
            <Button onClick={() => setDrawerOpen(false)}>Bağla</Button>
          </Space>
        </Form>
      </Drawer>
    </div>
  )
}
