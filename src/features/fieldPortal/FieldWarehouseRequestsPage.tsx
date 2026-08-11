import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { Alert, Button, Card, Drawer, Form, Input, InputNumber, Select, Space, Table, Tag, Typography, message } from 'antd'
import { useEffect, useMemo, useState } from 'react'
import {
  buildTrackBackendApi,
  type FieldWarehouseCatalogItem,
  type FieldWarehouseRequest,
} from '../../services/api/buildTrackBackendApi'
import { priorityLabel } from '../../utils/warehouseWorkflowLabels'
import { fieldStatusColor, fieldStatusLabel, useFieldPortalStore } from './fieldPortalStore'

interface CartLineForm {
  catalogItemId?: string
  requestedQuantity?: number
  reason?: string
}

interface CartRequestForm {
  urgency: 'Normal' | 'Urgent' | 'Critical'
  generalNote?: string
  lines: CartLineForm[]
}

const defaultLine = (): CartLineForm => ({ requestedQuantity: 1 })

const urgencyOptions = [
  { value: 'Normal', label: priorityLabel('Normal') },
  { value: 'Urgent', label: priorityLabel('Urgent') },
  { value: 'Critical', label: priorityLabel('Critical') },
]

export const FieldWarehouseRequestsPage = () => {
  const selectedSiteId = useFieldPortalStore((state) => state.selectedSiteId)
  const [catalog, setCatalog] = useState<FieldWarehouseCatalogItem[]>([])
  const [requests, setRequests] = useState<FieldWarehouseRequest[]>([])
  const [loading, setLoading] = useState(false)
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [form] = Form.useForm<CartRequestForm>()
  const selectedUrgency = Form.useWatch('urgency', form) ?? 'Normal'

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

  const catalogById = useMemo(() => new Map(catalog.map((item) => [item.id, item])), [catalog])

  const catalogOptions = useMemo(() => catalog.map((item) => ({
    value: item.id,
    label: `${item.name} (${item.unit})`,
    searchText: `${item.name} ${item.category ?? ''} ${item.subcategory ?? ''} ${item.searchAliases ?? ''}`.toLocaleLowerCase('az-AZ'),
  })), [catalog])

  const openCart = () => {
    form.setFieldsValue({ urgency: 'Normal', lines: [defaultLine()] })
    setDrawerOpen(true)
  }

  const createCartRequest = async (values: CartRequestForm) => {
    if (!selectedSiteId) return
    const lines = (values.lines ?? [])
      .filter((line) => line.catalogItemId && Number(line.requestedQuantity) > 0)
      .map((line) => ({
        catalogItemId: line.catalogItemId!,
        requestedQuantity: Number(line.requestedQuantity),
        reason: line.reason?.trim() || values.generalNote?.trim() || 'Sahə ehtiyacı',
      }))

    if (!lines.length) {
      void message.warning('Ən azı bir material sətri əlavə edin')
      return
    }

    await buildTrackBackendApi.createFieldWarehouseCartRequest({
      siteId: selectedSiteId,
      urgency: values.urgency,
      generalNote: values.generalNote,
      lines,
    })
    void message.success('Anbar sorğusu yaradıldı')
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
        <Button type="primary" icon={<PlusOutlined />} onClick={openCart}>
          Material səbəti yarat
        </Button>
      </div>
      <Alert
        type="info"
        showIcon
        className="field-alert"
        message="Prorab anbar qalığını görmür"
        description="Sistem stok yoxlamasını serverdə aparır. Anbarda yetərli miqdar varsa rezerv edilir, çatışmayan hissə isə rəhbərlik təsdiqindən sonra satınalma ehtiyacına çevrilir."
      />
      <Card className="soft-card">
        <Table
          rowKey="id"
          loading={loading}
          dataSource={requests}
          pagination={{ pageSize: 8 }}
          expandable={{
            expandedRowRender: (row) => (
              <Table
                rowKey="id"
                pagination={false}
                dataSource={row.lines?.length ? row.lines : [{
                  id: `${row.id}-legacy`,
                  catalogItemId: row.catalogItemId,
                  itemName: row.materialName,
                  category: '',
                  requestedQuantity: row.requestedQuantity,
                  unit: row.unit,
                  status: row.status,
                  reason: row.reason,
                }]}
                columns={[
                  { title: 'Material', dataIndex: 'itemName' },
                  { title: 'Miqdar', render: (_, line) => `${line.requestedQuantity} ${line.unit}` },
                  { title: 'Sətir statusu', dataIndex: 'status', render: (status) => <Tag color={fieldStatusColor(status)}>{fieldStatusLabel(status)}</Tag> },
                  { title: 'Səbəb', dataIndex: 'reason', render: (value) => value || '-' },
                ]}
              />
            ),
          }}
          columns={[
            { title: 'Sorğu', dataIndex: 'code', render: (value, row) => value || row.id.slice(0, 8) },
            { title: 'Material', render: (_, row) => row.lines?.length ? `${row.lines.length} sətir` : row.materialName },
            { title: 'Miqdar', render: (_, row) => row.lines?.length ? `${row.lines.reduce((sum, line) => sum + line.requestedQuantity, 0)} vahid` : `${row.requestedQuantity} ${row.unit}` },
            { title: 'Təcillik', dataIndex: 'urgency', render: (value) => priorityLabel(value) },
            { title: 'Status', dataIndex: 'status', render: (status) => <Tag color={fieldStatusColor(status)}>{fieldStatusLabel(status)}</Tag> },
            { title: 'Qeyd', render: (_, row) => row.generalNote || row.reason || '-' },
            { title: 'Tarix', dataIndex: 'createdAt', render: (value) => new Date(value).toLocaleString('az-AZ') },
          ]}
        />
      </Card>
      <Drawer title="Material səbəti yarat" open={drawerOpen} width={720} onClose={() => setDrawerOpen(false)}>
        <Typography.Paragraph type="secondary">
          Bir sorğuda bir neçə material əlavə edin. Anbar qalığı gizli qalır; sistem yalnız nəticə statusunu göstərəcək.
        </Typography.Paragraph>
        <Form layout="vertical" form={form} onFinish={createCartRequest} initialValues={{ urgency: 'Normal', lines: [defaultLine()] }}>
          <Form.Item name="urgency" label="Təcillik" initialValue="Normal">
            <Select
              key={`urgency:${selectedUrgency}`}
              options={urgencyOptions}
            />
          </Form.Item>
          <Form.Item name="generalNote" label="Ümumi sorğu qeydi">
            <Input.TextArea rows={3} placeholder="Məsələn: 2-ci mərtəbə monolit işləri üçün PPE və sərfiyyat lazımdır" />
          </Form.Item>
          <Form.List name="lines">
            {(fields, { add, remove }) => (
              <Space direction="vertical" size={12} className="full-width">
                {fields.map((field, index) => (
                  <Card key={field.key} size="small" className="soft-card">
                    <div className="field-cart-line">
                      <Form.Item
                        {...field}
                        name={[field.name, 'catalogItemId']}
                        label={`Material ${index + 1}`}
                        rules={[{ required: true, message: 'Material seçin' }]}
                      >
                        <Select
                          showSearch
                          placeholder="Material axtarın"
                          options={catalogOptions}
                          optionFilterProp="searchText"
                          filterOption={(input, option) => String(option?.searchText ?? option?.label ?? '').toLocaleLowerCase('az-AZ').includes(input.toLocaleLowerCase('az-AZ'))}
                          optionRender={(option) => {
                            const item = catalogById.get(String(option.value))
                            return (
                              <div>
                                <strong>{item?.name ?? option.label}</strong>
                                <div className="muted-text">{[item?.category, item?.subcategory, item?.unit].filter(Boolean).join(' / ')}</div>
                              </div>
                            )
                          }}
                        />
                      </Form.Item>
                      <Form.Item
                        {...field}
                        name={[field.name, 'requestedQuantity']}
                        label="Miqdar"
                        rules={[{ required: true, message: 'Miqdar yazın' }]}
                      >
                        <InputNumber min={1} className="full-width" />
                      </Form.Item>
                      <Form.Item {...field} name={[field.name, 'reason']} label="Səbəb">
                        <Input placeholder="Hansı iş üçün lazımdır?" />
                      </Form.Item>
                      <Button danger icon={<DeleteOutlined />} disabled={fields.length === 1} onClick={() => remove(field.name)}>
                        Sil
                      </Button>
                    </div>
                  </Card>
                ))}
                <Button icon={<PlusOutlined />} onClick={() => add(defaultLine())}>
                  Sətir əlavə et
                </Button>
              </Space>
            )}
          </Form.List>
          <Space className="field-form-actions">
            <Button type="primary" htmlType="submit">Sorğu göndər</Button>
            <Button onClick={() => setDrawerOpen(false)}>Bağla</Button>
          </Space>
        </Form>
      </Drawer>
    </div>
  )
}
