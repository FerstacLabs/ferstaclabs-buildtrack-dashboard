import { PlusOutlined } from '@ant-design/icons'
import { Alert, Button, Card, Drawer, Form, Input, InputNumber, Select, Space, Table, Tag, Typography, message } from 'antd'
import { useEffect, useMemo, useState } from 'react'
import {
  buildTrackBackendApi,
  type FieldWarehouseCatalogItem,
  type FieldWarehouseRequest,
} from '../../services/api/buildTrackBackendApi'
import { fieldStatusColor, fieldStatusLabel, useFieldPortalStore } from './fieldPortalStore'

export const FieldWarehouseRequestsPage = () => {
  const selectedSiteId = useFieldPortalStore((state) => state.selectedSiteId)
  const [catalog, setCatalog] = useState<FieldWarehouseCatalogItem[]>([])
  const [requests, setRequests] = useState<FieldWarehouseRequest[]>([])
  const [loading, setLoading] = useState(false)
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [form] = Form.useForm()

  const load = async () => {
    if (!selectedSiteId) return
    setLoading(true)
    try {
      const [nextCatalog, nextRequests] = await Promise.all([
        buildTrackBackendApi.getFieldWarehouseCatalog(),
        buildTrackBackendApi.getFieldWarehouseRequests(selectedSiteId),
      ])
      setCatalog(nextCatalog)
      setRequests(nextRequests)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void load()
  }, [selectedSiteId])

  const catalogOptions = useMemo(() => catalog.map((item) => ({
    value: item.id,
    label: `${item.name} (${item.unit})`,
  })), [catalog])

  const createRequest = async (values: { catalogItemId: string; requestedQuantity: number; urgency: 'Normal' | 'Urgent' | 'Critical'; reason: string; justification?: string }) => {
    if (!selectedSiteId) return
    await buildTrackBackendApi.createFieldWarehouseRequest({ siteId: selectedSiteId, ...values })
    message.success('Anbar sorğusu yaradıldı')
    setDrawerOpen(false)
    form.resetFields()
    await load()
  }

  if (!selectedSiteId) return <Alert type="info" showIcon message="Obyekt seçin" />

  return (
    <div className="field-page">
      <div className="field-toolbar">
        <div>
          <span className="field-eyebrow">1C-ready anbar prosesi</span>
          <h2>Anbar sorğuları</h2>
        </div>
        <Button type="primary" icon={<PlusOutlined />} onClick={() => {
          form.setFieldsValue({ urgency: 'Normal' })
          setDrawerOpen(true)
        }}>
          Material sorğusu
        </Button>
      </div>
      <Alert
        type="info"
        showIcon
        className="field-alert"
        message="Prorab anbar qalığını görmür"
        description="Sistem stok yoxlamasını idarəetmə və gələcək 1C inteqrasiyası tərəfində aparacaq. Çatışmazlıq varsa sorğu əsaslandırma ilə təsdiq axınına düşür."
      />
      <Card className="soft-card">
        <Table
          rowKey="id"
          loading={loading}
          dataSource={requests}
          pagination={{ pageSize: 8 }}
          columns={[
            { title: 'Material', dataIndex: 'materialName' },
            { title: 'Miqdar', render: (_, row) => `${row.requestedQuantity} ${row.unit}` },
            { title: 'Təcillik', dataIndex: 'urgency' },
            { title: 'Status', dataIndex: 'status', render: (status) => <Tag color={fieldStatusColor(status)}>{fieldStatusLabel(status)}</Tag> },
            { title: 'Səbəb', dataIndex: 'reason' },
            { title: 'Rəhbər qeydi', dataIndex: 'managerNote', render: (value) => value || '-' },
            { title: 'Tarix', dataIndex: 'createdAt', render: (value) => new Date(value).toLocaleString('az-AZ') },
          ]}
        />
      </Card>
      <Drawer title="Material sorğusu yarat" open={drawerOpen} width={560} onClose={() => setDrawerOpen(false)}>
        <Typography.Paragraph type="secondary">
          Anbar qalığı prorab üçün gizlidir. Lazım olan material miqdarını və səbəbi yazın.
        </Typography.Paragraph>
        <Form layout="vertical" form={form} onFinish={createRequest}>
          <Form.Item name="catalogItemId" label="Material" rules={[{ required: true, message: 'Material seçin' }]}>
            <Select showSearch options={catalogOptions} optionFilterProp="label" />
          </Form.Item>
          <Form.Item name="requestedQuantity" label="Lazım olan miqdar" rules={[{ required: true, message: 'Miqdar yazın' }]}>
            <InputNumber min={1} className="full-width" />
          </Form.Item>
          <Form.Item name="urgency" label="Təcillik">
            <Select options={[
              { value: 'Normal', label: 'Normal' },
              { value: 'Urgent', label: 'Təcili' },
              { value: 'Critical', label: 'Kritik' },
            ]} />
          </Form.Item>
          <Form.Item name="reason" label="Səbəb" rules={[{ required: true, message: 'Səbəb yazın' }]}>
            <Input.TextArea rows={3} />
          </Form.Item>
          <Form.Item name="justification" label="Əlavə əsaslandırma">
            <Input.TextArea rows={3} placeholder="Miqdar böyükdürsə və ya çatışmazlıq varsa rəhbərlik üçün izah yazın" />
          </Form.Item>
          <Space>
            <Button type="primary" htmlType="submit">Sorğu yarat</Button>
            <Button onClick={() => setDrawerOpen(false)}>Bağla</Button>
          </Space>
        </Form>
      </Drawer>
    </div>
  )
}
