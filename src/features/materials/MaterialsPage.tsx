import { DeleteOutlined, EditOutlined, PlusOutlined, ToolOutlined } from '@ant-design/icons'
import { Button, Drawer, Form, Input, InputNumber, Modal, Progress, Select, Space, Table, Tag, message } from 'antd'
import type { TableColumnsType } from 'antd'
import { useMemo, useState } from 'react'
import { ProjectSelect } from '../../components/ProjectSelect'
import { KpiCard } from '../../components/ui/KpiCard'
import { PageTitle } from '../../components/ui/PageTitle'
import type { MaterialItem } from '../../types/projectProgress'
import { formatCurrency, formatNumber } from '../../utils/formatters'
import { UnitSelect } from '../projectProgress/constructionUnits'
import { ALL_OBJECTS_ID, getEstimateRowsByObject, getMaterialsByObject, getStagesByObject } from '../projectProgress/projectSelectors'
import { useProjectProgressStore } from '../projectProgress/projectProgressStore'
import { useProjectSelectionStore } from '../../stores/projectSelectionStore'

interface MaterialFormValues {
  name: string
  unit: string
  quantity: number
  usedQuantity: number
  unitPrice?: number
  linkedStageId?: string
  linkedWorkItemId?: string
  deliveryDate?: string
  supplier?: string
  notes?: string
}

export const MaterialsPage = () => {
  const store = useProjectProgressStore()
  const { addMaterial, deleteMaterial, stages, updateMaterial } = store
  const selectedObjectId = useProjectSelectionStore((state) => state.selectedProjectId)
  const materials = getMaterialsByObject(store, selectedObjectId)
  const scopedStages = getStagesByObject(store, selectedObjectId)
  const scopedWorkItems = getEstimateRowsByObject(store, selectedObjectId)
  const [form] = Form.useForm<MaterialFormValues>()
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [editingMaterial, setEditingMaterial] = useState<MaterialItem>()

  const stageById = useMemo(() => new Map(stages.map((stage) => [stage.id, stage.name])), [stages])
  const stageOptions = scopedStages.map((stage) => ({ value: stage.id, label: stage.name }))
  const itemOptions = scopedWorkItems.map((item) => ({ value: item.id, label: item.name }))

  const openDrawer = (material?: MaterialItem) => {
    setEditingMaterial(material)
    form.setFieldsValue(material ?? { name: '', unit: 'ədəd', quantity: 0, usedQuantity: 0, unitPrice: 0 })
    setDrawerOpen(true)
  }

  const saveMaterial = (values: MaterialFormValues) => {
    const linkedWorkItem = values.linkedWorkItemId ? store.workItems.find((item) => item.id === values.linkedWorkItemId) : undefined
    const linkedStageId = linkedWorkItem?.stageId ?? values.linkedStageId
    const linkedStage = linkedStageId ? stages.find((stage) => stage.id === linkedStageId) : undefined
    const objectId = editingMaterial?.objectId
      ?? linkedWorkItem?.objectId
      ?? linkedStage?.objectId
      ?? (selectedObjectId === ALL_OBJECTS_ID ? store.objects[0]?.id : selectedObjectId)
    const normalizedValues = {
      ...values,
      objectId,
      linkedStageId,
      unitPrice: values.unitPrice && values.unitPrice > 0 ? values.unitPrice : linkedWorkItem?.materialUnitPrice ?? values.unitPrice,
    }
    if (editingMaterial) updateMaterial(editingMaterial.id, normalizedValues)
    else addMaterial(normalizedValues)
    setDrawerOpen(false)
    void message.success('Material məlumatı yadda saxlandı')
  }

  const rows = materials.map((material) => ({
    ...material,
    usedPercent: material.quantity > 0 ? Math.round((material.usedQuantity / material.quantity) * 100) : 0,
    stageName: material.linkedStageId ? stageById.get(material.linkedStageId) : 'Təyin edilməyib',
    totalValue: material.quantity * (material.unitPrice ?? 0),
  }))

  const columns: TableColumnsType<(typeof rows)[number]> = [
    { title: 'Material', dataIndex: 'name', render: (value, row) => <strong>{value}<br /><span className="muted-text">{row.supplier ?? 'Təchizatçı qeyd edilməyib'}</span></strong> },
    { title: 'Etap', dataIndex: 'stageName' },
    { title: 'Plan miqdar', render: (_, row) => `${formatNumber(row.quantity, 1)} ${row.unit}` },
    { title: 'İstifadə olunub', render: (_, row) => `${formatNumber(row.usedQuantity, 1)} ${row.unit}` },
    { title: 'Qalıq', render: (_, row) => <Tag color={row.quantity > 0 && row.remainingQuantity / row.quantity <= 0.15 ? 'red' : 'green'}>{formatNumber(row.remainingQuantity, 1)} {row.unit}</Tag> },
    { title: 'İstifadə %', dataIndex: 'usedPercent', render: (value) => <Progress percent={Number(value)} size="small" /> },
    { title: 'Dəyər', dataIndex: 'totalValue', align: 'right', render: (value) => formatCurrency(Number(value)) },
    { title: 'Çatdırılma', dataIndex: 'deliveryDate', render: (value) => value ?? '-' },
    {
      title: 'Əməliyyat',
      width: 110,
      render: (_, row) => (
        <Space>
          <Button icon={<EditOutlined />} onClick={() => openDrawer(row)} />
          <Button danger icon={<DeleteOutlined />} onClick={() => Modal.confirm({ title: 'Materialı silmək istəyirsiniz?', okText: 'Sil', cancelText: 'İmtina', onOk: () => deleteMaterial(row.id) })} />
        </Space>
      ),
    },
  ]

  return (
    <div className="page-stack">
      <PageTitle
        title="Materiallar"
        subtitle="Smeta materialları, istifadə miqdarı, qalıq və çatdırılma nəzarəti"
        extra={<Space wrap><ProjectSelect pageKey="materials" /><Button type="primary" icon={<PlusOutlined />} onClick={() => openDrawer()}>Yeni material</Button></Space>}
      />

      <section className="kpi-grid four">
        <KpiCard icon={<ToolOutlined />} title="Material sayı" value={formatNumber(materials.length)} tone="blue" />
        <KpiCard icon={<ToolOutlined />} title="Kritik qalıq" value={formatNumber(rows.filter((row) => row.quantity > 0 && row.remainingQuantity / row.quantity <= 0.15).length)} tone="red" />
        <KpiCard icon={<ToolOutlined />} title="Tam istifadə olunan" value={formatNumber(rows.filter((row) => row.remainingQuantity === 0).length)} tone="green" />
        <KpiCard icon={<ToolOutlined />} title="Material dəyəri" value={formatCurrency(rows.reduce((sum, row) => sum + row.totalValue, 0))} tone="purple" />
      </section>

      <section className="table-card">
        <div className="card-heading">
          <h2>Material izləmə cədvəli</h2>
        </div>
        <Table rowKey="id" columns={columns} dataSource={rows} pagination={{ pageSize: 8 }} scroll={{ x: 1100 }} />
      </section>

      <Drawer title={editingMaterial ? 'Materialı redaktə et' : 'Yeni material'} open={drawerOpen} width={520} onClose={() => setDrawerOpen(false)}>
        <Form form={form} layout="vertical" onFinish={saveMaterial}>
          <Form.Item name="name" label="Material adı" rules={[{ required: true }]}><Input /></Form.Item>
          <Space.Compact block>
            <Form.Item name="unit" label="Vahid" className="form-half"><UnitSelect /></Form.Item>
            <Form.Item name="unitPrice" label="Vahid qiymət" className="form-half"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
          </Space.Compact>
          <Space.Compact block>
            <Form.Item name="quantity" label="Plan miqdar" className="form-half"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
            <Form.Item name="usedQuantity" label="İstifadə olunub" className="form-half"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
          </Space.Compact>
          <Form.Item name="linkedStageId" label="Etap"><Select allowClear showSearch options={stageOptions} /></Form.Item>
          <Form.Item name="linkedWorkItemId" label="İş sətri"><Select allowClear showSearch options={itemOptions} /></Form.Item>
          <Form.Item name="deliveryDate" label="Çatdırılma tarixi"><Input placeholder="YYYY-MM-DD" /></Form.Item>
          <Form.Item name="supplier" label="Təchizatçı"><Input /></Form.Item>
          <Form.Item name="notes" label="Qeyd"><Input.TextArea rows={3} /></Form.Item>
          <Button type="primary" htmlType="submit" block>Yadda saxla</Button>
        </Form>
      </Drawer>
    </div>
  )
}
