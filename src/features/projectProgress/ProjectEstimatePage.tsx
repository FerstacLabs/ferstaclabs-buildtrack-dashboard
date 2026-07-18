import { DeleteOutlined, EditOutlined, PlusOutlined, UploadOutlined } from '@ant-design/icons'
import { Button, Drawer, Form, Input, InputNumber, Modal, Select, Slider, Space, Table, Tag, Upload, message } from 'antd'
import type { TableColumnsType } from 'antd'
import { useMemo, useState } from 'react'
import * as XLSX from 'xlsx'
import { PageTitle } from '../../components/ui/PageTitle'
import type { ProjectWorkStatus, WorkItem } from '../../types/projectProgress'
import { formatNumber } from '../../utils/formatters'
import { statusColor, statusLabel, useProjectProgressStore } from './projectProgressStore'

interface WorkItemFormValues {
  stageId: string
  name: string
  unit: string
  quantity: number
  laborUnitPrice: number
  materialUnit?: string
  materialQuantity: number
  materialUnitPrice: number
  plannedHours: number
  actualHours: number
  assignedCrewId?: string
  status: ProjectWorkStatus
  progressPercent: number
  notes?: string
}

const statusOptions = Object.entries(statusLabel).map(([value, label]) => ({ value, label }))
const formatAzn = (value: number) => `${formatNumber(value, value % 1 === 0 ? 0 : 2)} AZN`

export const ProjectEstimatePage = () => {
  const { addStage, addWorkItem, crews, deleteWorkItem, stages, updateWorkItem, workItems } = useProjectProgressStore()
  const [itemForm] = Form.useForm<WorkItemFormValues>()
  const [stageForm] = Form.useForm<{ name: string; totalCost: number; plannedHours: number }>()
  const [editingItem, setEditingItem] = useState<WorkItem>()
  const [itemDrawerOpen, setItemDrawerOpen] = useState(false)
  const [stageModalOpen, setStageModalOpen] = useState(false)
  const [previewOpen, setPreviewOpen] = useState(false)
  const [previewSheetNames, setPreviewSheetNames] = useState<string[]>([])
  const [previewRows, setPreviewRows] = useState<unknown[][]>([])

  const stageOptions = stages.map((stage) => ({ value: stage.id, label: stage.name }))
  const crewOptions = crews.map((crew) => ({ value: crew.id, label: crew.name }))
  const stageNameById = useMemo(() => new Map(stages.map((stage) => [stage.id, stage.name])), [stages])
  const crewNameById = useMemo(() => new Map(crews.map((crew) => [crew.id, crew.name])), [crews])

  const openItemDrawer = (item?: WorkItem) => {
    setEditingItem(item)
    itemForm.setFieldsValue(item ?? {
      stageId: stages[0]?.id,
      name: '',
      unit: 'iş',
      quantity: 1,
      laborUnitPrice: 0,
      materialUnit: 'iş',
      materialQuantity: 1,
      materialUnitPrice: 0,
      plannedHours: 0,
      actualHours: 0,
      status: 'NotStarted',
      progressPercent: 0,
    })
    setItemDrawerOpen(true)
  }

  const saveWorkItem = (values: WorkItemFormValues) => {
    const laborTotal = values.quantity * values.laborUnitPrice
    const materialTotal = values.materialQuantity * values.materialUnitPrice
    const payload = {
      ...values,
      laborTotal,
      materialTotal,
      totalCost: laborTotal + materialTotal,
    }
    if (editingItem) updateWorkItem(editingItem.id, payload)
    else addWorkItem(payload)
    setItemDrawerOpen(false)
    void message.success('Smeta sətri yadda saxlanıldı')
  }

  const addNewStage = (values: { name: string; totalCost: number; plannedHours: number }) => {
    addStage({
      name: values.name,
      totalCost: values.totalCost,
      laborCost: 0,
      materialCost: values.totalCost,
      plannedStartDate: '2026-10-01',
      plannedEndDate: '2026-10-15',
      status: 'NotStarted',
      progressPercent: 0,
      plannedHours: values.plannedHours,
      actualHours: 0,
    })
    setStageModalOpen(false)
    stageForm.resetFields()
    void message.success('Yeni etap əlavə edildi')
  }

  const parseWorkbook = async (file: File) => {
    const buffer = await file.arrayBuffer()
    const workbook = XLSX.read(buffer, { type: 'array' })
    setPreviewSheetNames(workbook.SheetNames)
    const sheet = workbook.Sheets['Kaba işlər smetası'] ?? workbook.Sheets[workbook.SheetNames[0]]
    const rows = XLSX.utils.sheet_to_json<unknown[]>(sheet, { header: 1, blankrows: false }).slice(0, 20)
    setPreviewRows(rows)
    setPreviewOpen(true)
    void message.success('Smeta faylı oxundu. Hazırda preview rejimində göstərilir.')
  }

  const columns: TableColumnsType<WorkItem> = [
    { title: 'Stage', dataIndex: 'stageId', width: 210, render: (value) => stageNameById.get(String(value)) ?? value, filters: stages.map((stage) => ({ text: stage.name, value: stage.id })), onFilter: (value, record) => record.stageId === value },
    { title: 'Work name', dataIndex: 'name', width: 240, render: (value) => <strong>{value}</strong> },
    { title: 'Unit', dataIndex: 'unit', width: 80 },
    { title: 'Quantity', dataIndex: 'quantity', width: 110, align: 'right', sorter: (a, b) => a.quantity - b.quantity },
    { title: 'Labor total', dataIndex: 'laborTotal', width: 130, align: 'right', render: (value) => formatAzn(Number(value)), sorter: (a, b) => a.laborTotal - b.laborTotal },
    { title: 'Material total', dataIndex: 'materialTotal', width: 140, align: 'right', render: (value) => formatAzn(Number(value)), sorter: (a, b) => a.materialTotal - b.materialTotal },
    { title: 'Total cost', dataIndex: 'totalCost', width: 130, align: 'right', render: (value) => formatAzn(Number(value)), sorter: (a, b) => a.totalCost - b.totalCost },
    { title: 'Planned hours', dataIndex: 'plannedHours', width: 130, align: 'right' },
    { title: 'Actual hours', dataIndex: 'actualHours', width: 120, align: 'right' },
    { title: 'Crew', dataIndex: 'assignedCrewId', width: 170, render: (value) => crewNameById.get(String(value)) ?? 'Təyin edilməyib' },
    { title: 'Status', dataIndex: 'status', width: 130, render: (value: ProjectWorkStatus) => <Tag color={statusColor[value]}>{statusLabel[value]}</Tag> },
    { title: 'Progress %', dataIndex: 'progressPercent', width: 160, render: (value, row) => <Slider min={0} max={100} value={Number(value)} onChange={(progressPercent) => updateWorkItem(row.id, { progressPercent })} /> },
    {
      title: 'Actions',
      fixed: 'right',
      width: 110,
      render: (_, row) => (
        <Space>
          <Button icon={<EditOutlined />} onClick={() => openItemDrawer(row)} />
          <Button
            danger
            icon={<DeleteOutlined />}
            onClick={() => Modal.confirm({ title: 'Sətri silmək istəyirsiniz?', okText: 'Sil', cancelText: 'İmtina', onOk: () => deleteWorkItem(row.id) })}
          />
        </Space>
      ),
    },
  ]

  return (
    <div className="page-stack project-progress-page">
      <PageTitle
        title="Layihə Smetası"
        subtitle="Villa smetası üzrə etap və iş elementlərini redaktə edin"
        extra={
          <Space>
            <Upload accept=".xlsx,.xls" showUploadList={false} beforeUpload={(file) => { void parseWorkbook(file as File); return false }}>
              <Button icon={<UploadOutlined />}>Smeta import et</Button>
            </Upload>
            <Button icon={<PlusOutlined />} onClick={() => setStageModalOpen(true)}>Yeni etap</Button>
            <Button type="primary" icon={<PlusOutlined />} onClick={() => openItemDrawer()}>Yeni iş sətri</Button>
          </Space>
        }
      />

      <section className="table-card">
        <div className="card-heading">
          <h2>Editable smeta cədvəli</h2>
        </div>
        <Table rowKey="id" columns={columns} dataSource={workItems} pagination={{ pageSize: 8 }} scroll={{ x: 1480 }} />
      </section>

      <Drawer title={editingItem ? 'İş sətrini redaktə et' : 'Yeni iş sətri'} open={itemDrawerOpen} width={520} onClose={() => setItemDrawerOpen(false)}>
        <Form form={itemForm} layout="vertical" onFinish={saveWorkItem}>
          <Form.Item name="stageId" label="Etap" rules={[{ required: true }]}><Select options={stageOptions} /></Form.Item>
          <Form.Item name="name" label="İş adı" rules={[{ required: true }]}><Input /></Form.Item>
          <Space.Compact block>
            <Form.Item name="unit" label="Vahid" rules={[{ required: true }]} className="form-half"><Input /></Form.Item>
            <Form.Item name="quantity" label="Miqdar" rules={[{ required: true }]} className="form-half"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
          </Space.Compact>
          <Space.Compact block>
            <Form.Item name="laborUnitPrice" label="İşçilik vahid qiyməti" className="form-half"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
            <Form.Item name="materialUnitPrice" label="Material vahid qiyməti" className="form-half"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
          </Space.Compact>
          <Space.Compact block>
            <Form.Item name="materialUnit" label="Material vahidi" className="form-half"><Input /></Form.Item>
            <Form.Item name="materialQuantity" label="Material miqdarı" className="form-half"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
          </Space.Compact>
          <Space.Compact block>
            <Form.Item name="plannedHours" label="Plan saat" className="form-half"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
            <Form.Item name="actualHours" label="Faktiki saat" className="form-half"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
          </Space.Compact>
          <Form.Item name="assignedCrewId" label="Briqada"><Select allowClear options={crewOptions} /></Form.Item>
          <Form.Item name="status" label="Status"><Select options={statusOptions} /></Form.Item>
          <Form.Item name="progressPercent" label="Gedişat %"><Slider min={0} max={100} /></Form.Item>
          <Form.Item name="notes" label="Qeyd"><Input.TextArea rows={3} /></Form.Item>
          <Button type="primary" htmlType="submit" block>Yadda saxla</Button>
        </Form>
      </Drawer>

      <Modal title="Yeni etap" open={stageModalOpen} onCancel={() => setStageModalOpen(false)} onOk={() => stageForm.submit()} okText="Əlavə et" cancelText="İmtina">
        <Form form={stageForm} layout="vertical" onFinish={addNewStage}>
          <Form.Item name="name" label="Etap adı" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="totalCost" label="Ümumi məbləğ"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
          <Form.Item name="plannedHours" label="Plan saat"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
        </Form>
      </Modal>

      <Modal title="Smeta import preview" open={previewOpen} onCancel={() => setPreviewOpen(false)} footer={<Button onClick={() => setPreviewOpen(false)}>Bağla</Button>} width={900}>
        <p>Tapılan sheet-lər: {previewSheetNames.join(', ')}</p>
        <Table
          size="small"
          pagination={{ pageSize: 8 }}
          dataSource={previewRows.map((row, index) => ({ key: index, row }))}
          columns={[{ title: 'Sətir preview', dataIndex: 'row', render: (row: unknown[]) => row.map((cell) => String(cell ?? '')).join(' | ') }]}
        />
      </Modal>
    </div>
  )
}
