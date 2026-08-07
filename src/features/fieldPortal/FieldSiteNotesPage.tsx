import { PlusOutlined } from '@ant-design/icons'
import { Alert, Button, Card, DatePicker, Drawer, Form, Input, Select, Space, Table, Tag, message } from 'antd'
import dayjs from 'dayjs'
import { useEffect, useState } from 'react'
import { buildTrackBackendApi, type FieldSiteNote, type FieldSiteNoteCategory } from '../../services/api/buildTrackBackendApi'
import { useFieldPortalStore } from './fieldPortalStore'

const categoryOptions: { value: FieldSiteNoteCategory; label: string }[] = [
  { value: 'Weather', label: 'Hava' },
  { value: 'MaterialDelay', label: 'Material gecikməsi' },
  { value: 'Equipment', label: 'Avadanlıq' },
  { value: 'Labor', label: 'İşçi heyəti' },
  { value: 'Safety', label: 'Təhlükəsizlik' },
  { value: 'Quality', label: 'Keyfiyyət' },
  { value: 'Access', label: 'Giriş / logistika' },
  { value: 'Other', label: 'Digər' },
]

export const FieldSiteNotesPage = () => {
  const selectedSiteId = useFieldPortalStore((state) => state.selectedSiteId)
  const [notes, setNotes] = useState<FieldSiteNote[]>([])
  const [loading, setLoading] = useState(false)
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [form] = Form.useForm()

  const load = async () => {
    if (!selectedSiteId) return
    setLoading(true)
    try {
      setNotes(await buildTrackBackendApi.getFieldSiteNotes(selectedSiteId))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void load()
  }, [selectedSiteId])

  const createNote = async (values: { category: FieldSiteNoteCategory; eventDateTime: dayjs.Dayjs; text: string }) => {
    if (!selectedSiteId) return
    await buildTrackBackendApi.createFieldSiteNote({
      siteId: selectedSiteId,
      category: values.category,
      eventDateTime: values.eventDateTime.toISOString(),
      text: values.text,
    })
    message.success('Sahə qeydi yaradıldı')
    setDrawerOpen(false)
    form.resetFields()
    await load()
  }

  if (!selectedSiteId) return <Alert type="info" showIcon message="Obyekt seçin" />

  return (
    <div className="field-page">
      <div className="field-toolbar">
        <div>
          <span className="field-eyebrow">Operativ qeyd jurnalı</span>
          <h2>Sahə qeydləri</h2>
        </div>
        <Button type="primary" icon={<PlusOutlined />} onClick={() => {
          form.setFieldsValue({ eventDateTime: dayjs(), category: 'Other' })
          setDrawerOpen(true)
        }}>
          Qeyd əlavə et
        </Button>
      </div>
      <Card className="soft-card">
        <Table
          rowKey="id"
          loading={loading}
          dataSource={notes}
          pagination={{ pageSize: 10 }}
          columns={[
            { title: 'Tarix', dataIndex: 'eventDateTime', render: (value) => new Date(value).toLocaleString('az-AZ') },
            { title: 'Kateqoriya', dataIndex: 'category', render: (value) => <Tag>{categoryOptions.find((item) => item.value === value)?.label ?? value}</Tag> },
            { title: 'Qeyd', dataIndex: 'text' },
            { title: 'Prorab', dataIndex: 'supervisorName' },
          ]}
        />
      </Card>
      <Drawer title="Sahə qeydi" open={drawerOpen} width={560} onClose={() => setDrawerOpen(false)}>
        <Form layout="vertical" form={form} onFinish={createNote}>
          <Form.Item name="eventDateTime" label="Hadisə vaxtı" rules={[{ required: true, message: 'Vaxt seçin' }]}>
            <DatePicker showTime className="full-width" />
          </Form.Item>
          <Form.Item name="category" label="Kateqoriya" rules={[{ required: true, message: 'Kateqoriya seçin' }]}>
            <Select options={categoryOptions} />
          </Form.Item>
          <Form.Item name="text" label="Qeyd" rules={[{ required: true, message: 'Qeyd yazın' }]}>
            <Input.TextArea rows={5} />
          </Form.Item>
          <Space>
            <Button type="primary" htmlType="submit">Yadda saxla</Button>
            <Button onClick={() => setDrawerOpen(false)}>Bağla</Button>
          </Space>
        </Form>
      </Drawer>
    </div>
  )
}
