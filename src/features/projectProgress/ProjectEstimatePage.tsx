import { DeleteOutlined, DownloadOutlined, EditOutlined, PlusOutlined, UploadOutlined } from '@ant-design/icons'
import { Button, Drawer, Form, Input, InputNumber, Modal, Select, Slider, Space, Table, Tag, Upload, message } from 'antd'
import type { TableColumnsType } from 'antd'
import { useMemo, useState } from 'react'
import * as XLSX from 'xlsx'
import { ObjectFilter } from '../../components/filters/ObjectFilter'
import { PageTitle } from '../../components/ui/PageTitle'
import type { ProjectWorkStatus, WorkItem } from '../../types/projectProgress'
import { formatCurrency, formatHours, formatNumber } from '../../utils/formatters'
import { ALL_OBJECTS_ID, getCrewsByObject, getEstimateRowsByObject, getMaterialsByObject, getStagesByObject } from './projectSelectors'
import { statusColor, statusLabel, useProjectProgressStore } from './projectProgressStore'

interface WorkItemFormValues {
  stageId: string
  name: string
  costCode?: string
  unit: string
  quantity: number
  unitPrice?: number
  completedQuantity?: number
  laborUnitPrice: number
  materialUnit?: string
  materialQuantity: number
  materialUnitPrice: number
  plannedHours: number
  actualHours: number
  assignedCrewId?: string
  status: ProjectWorkStatus
  progressPercent: number
  plannedStartDate?: string
  plannedEndDate?: string
  notes?: string
}

const statusOptions = Object.entries(statusLabel).map(([value, label]) => ({ value, label }))

export const ProjectEstimatePage = () => {
  const store = useProjectProgressStore()
  const {
    addStage,
    addWorkItem,
    crews,
    deleteStage,
    deleteWorkItem,
    estimateVersions,
    project,
    stages,
    summary,
    updateWorkItem,
  } = store
  const selectedObjectId = store.selectedObjectIdByPage.estimate ?? ALL_OBJECTS_ID
  const scopedStages = getStagesByObject(store, selectedObjectId)
  const scopedWorkItems = getEstimateRowsByObject(store, selectedObjectId)
  const scopedCrews = getCrewsByObject(store, selectedObjectId)
  const scopedMaterials = getMaterialsByObject(store, selectedObjectId)
  const [itemForm] = Form.useForm<WorkItemFormValues>()
  const [stageForm] = Form.useForm<{ name: string; totalCost: number; plannedHours: number; plannedStartDate: string; plannedEndDate: string }>()
  const [editingItem, setEditingItem] = useState<WorkItem>()
  const [itemDrawerOpen, setItemDrawerOpen] = useState(false)
  const [stageModalOpen, setStageModalOpen] = useState(false)
  const [previewOpen, setPreviewOpen] = useState(false)
  const [previewSheetNames, setPreviewSheetNames] = useState<string[]>([])
  const [previewRows, setPreviewRows] = useState<unknown[][]>([])

  const stageOptions = scopedStages.map((stage) => ({ value: stage.id, label: stage.name }))
  const crewOptions = scopedCrews.map((crew) => ({ value: crew.id, label: crew.name }))
  const stageNameById = useMemo(() => new Map(stages.map((stage) => [stage.id, stage.name])), [stages])
  const crewNameById = useMemo(() => new Map(crews.map((crew) => [crew.id, crew.name])), [crews])

  const openItemDrawer = (item?: WorkItem) => {
    setEditingItem(item)
    itemForm.setFieldsValue(item ?? {
      stageId: scopedStages[0]?.id,
      name: '',
      costCode: '',
      unit: 'iş',
      quantity: 1,
      unitPrice: 0,
      completedQuantity: 0,
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
    const progressPercent = values.quantity > 0 && typeof values.completedQuantity === 'number'
      ? Math.min(100, Math.round((values.completedQuantity / values.quantity) * 1000) / 10)
      : values.progressPercent
    const selectedStage = stages.find((stage) => stage.id === values.stageId)
    const objectId = editingItem?.objectId ?? selectedStage?.objectId ?? (selectedObjectId === ALL_OBJECTS_ID ? store.objects[0]?.id : selectedObjectId)
    const payload = {
      ...values,
      objectId,
      progressPercent,
      laborTotal,
      materialTotal,
      totalCost: laborTotal + materialTotal,
      remainingHours: Math.max(0, values.plannedHours - values.actualHours),
    }
    if (editingItem) updateWorkItem(editingItem.id, payload)
    else addWorkItem(payload)
    setItemDrawerOpen(false)
    void message.success('Smeta sətiri yadda saxlandı')
  }

  const addNewStage = (values: { name: string; totalCost: number; plannedHours: number; plannedStartDate: string; plannedEndDate: string }) => {
    addStage({
      name: values.name,
      objectId: selectedObjectId === ALL_OBJECTS_ID ? store.objects[0]?.id : selectedObjectId,
      totalCost: values.totalCost,
      laborCost: 0,
      materialCost: values.totalCost,
      plannedStartDate: values.plannedStartDate || '2026-10-01',
      plannedEndDate: values.plannedEndDate || '2026-10-15',
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
    const rows = XLSX.utils.sheet_to_json<unknown[]>(sheet, { header: 1, blankrows: false }).slice(0, 25)
    setPreviewRows(rows)
    setPreviewOpen(true)
    void message.success('Smeta faylı oxundu. Tətbiq etməzdən əvvəl önizləməyə baxın.')
  }

  const exportEstimate = () => {
    const workbook = XLSX.utils.book_new()
    XLSX.utils.book_append_sheet(workbook, XLSX.utils.json_to_sheet([{
      Layihə: project.name,
      Versiya: estimateVersions.find((version) => version.id === project.activeEstimateVersionId)?.name ?? 'Cari smeta',
      'Yekun smeta': summary.totalAmount,
      İşçilik: summary.laborAmount,
      Material: summary.materialAmount,
      'Gizli xərc': summary.hiddenCostAmount,
    }]), 'Summary')
    XLSX.utils.book_append_sheet(workbook, XLSX.utils.json_to_sheet(scopedStages), 'Stages')
    XLSX.utils.book_append_sheet(workbook, XLSX.utils.json_to_sheet(scopedWorkItems.map((item) => ({
      ...item,
      remainingHours: Math.max(0, item.plannedHours - item.actualHours),
      progressFormula: item.quantity ? `${item.completedQuantity ?? 0}/${item.quantity}` : '',
    }))), 'Work Items')
    XLSX.utils.book_append_sheet(workbook, XLSX.utils.json_to_sheet(scopedCrews), 'Crews')
    XLSX.utils.book_append_sheet(workbook, XLSX.utils.json_to_sheet(scopedMaterials), 'Materials')
    XLSX.writeFile(workbook, `${project.name.replace(/\s+/g, '-')}-smeta.xlsx`)
  }

  const columns: TableColumnsType<WorkItem> = [
    { title: 'Etap', dataIndex: 'stageId', width: 210, render: (value) => stageNameById.get(String(value)) ?? value, filters: scopedStages.map((stage) => ({ text: stage.name, value: stage.id })), onFilter: (value, record) => record.stageId === value },
    { title: 'İş adı', dataIndex: 'name', width: 250, render: (value, row) => <strong>{value}<br /><span className="muted-text">{row.costCode ?? 'Cost code yoxdur'}</span></strong> },
    { title: 'Vahid', dataIndex: 'unit', width: 80 },
    { title: 'Miqdar', dataIndex: 'quantity', width: 100, align: 'right', sorter: (a, b) => a.quantity - b.quantity },
    { title: 'Tamamlanan', dataIndex: 'completedQuantity', width: 120, align: 'right', render: (value, row) => `${formatNumber(Number(value ?? 0), 1)} / ${formatNumber(row.quantity, 1)}` },
    { title: 'İşçilik', dataIndex: 'laborTotal', width: 130, align: 'right', render: (value) => formatCurrency(Number(value)), sorter: (a, b) => a.laborTotal - b.laborTotal },
    { title: 'Material', dataIndex: 'materialTotal', width: 130, align: 'right', render: (value) => formatCurrency(Number(value)), sorter: (a, b) => a.materialTotal - b.materialTotal },
    { title: 'Ümumi xərc', dataIndex: 'totalCost', width: 130, align: 'right', render: (value) => formatCurrency(Number(value)), sorter: (a, b) => a.totalCost - b.totalCost },
    { title: 'Plan saat', dataIndex: 'plannedHours', width: 120, align: 'right', render: (value) => formatHours(Number(value), 0) },
    { title: 'Faktiki saat', dataIndex: 'actualHours', width: 120, align: 'right', render: (value) => formatHours(Number(value), 0) },
    { title: 'Briqada', dataIndex: 'assignedCrewId', width: 170, render: (value) => crewNameById.get(String(value)) ?? 'Təyin edilməyib' },
    { title: 'Status', dataIndex: 'status', width: 130, render: (value: ProjectWorkStatus) => <Tag color={statusColor[value]}>{statusLabel[value]}</Tag> },
    { title: 'Gedişat %', dataIndex: 'progressPercent', width: 160, render: (value, row) => <Slider min={0} max={100} value={Number(value)} onChange={(progressPercent) => updateWorkItem(row.id, { progressPercent })} /> },
    {
      title: 'Əməliyyat',
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
        title="Smeta"
        subtitle={`${project.name} üzrə etap, iş sətiri, miqdar, saat və xərc redaktoru`}
        extra={
          <Space wrap>
            <Button onClick={() => void message.info('Yeni layihə forması növbəti mərhələdə ayrıca aktiv ediləcək')}>Yeni layihə</Button>
            <ObjectFilter pageKey="estimate" />
            <Upload accept=".xlsx,.xls" showUploadList={false} beforeUpload={(file) => { void parseWorkbook(file as File); return false }}>
              <Button icon={<UploadOutlined />}>Smeta import et</Button>
            </Upload>
            <Button icon={<PlusOutlined />} onClick={() => setStageModalOpen(true)}>Yeni etap</Button>
            <Button type="primary" icon={<PlusOutlined />} onClick={() => openItemDrawer()}>Yeni iş sətiri</Button>
            <Button icon={<DownloadOutlined />} onClick={exportEstimate}>Excel export</Button>
          </Space>
        }
      />

      <section className="table-card">
        <div className="card-heading">
          <h2>Cari smeta</h2>
          <Space wrap>
            {estimateVersions.map((version) => <Tag color="blue" key={version.id}>{version.name}</Tag>)}
            <Button size="small" onClick={() => void message.success('Yeni smeta versiyası üçün hazır struktur yaradılıb')}>Yeni versiya yarat</Button>
          </Space>
        </div>
        <Table rowKey="id" columns={columns} dataSource={scopedWorkItems} pagination={{ pageSize: 8 }} scroll={{ x: 1640 }} />
      </section>

      <section className="table-card">
        <div className="card-heading">
          <h2>Etaplar</h2>
        </div>
        <Table
          rowKey="id"
          dataSource={scopedStages}
          pagination={{ pageSize: 6 }}
          columns={[
            { title: 'Sıra', dataIndex: 'order', width: 70 },
            { title: 'Etap', dataIndex: 'name' },
            { title: 'Məbləğ', dataIndex: 'totalCost', align: 'right', render: (value) => formatCurrency(Number(value)) },
            { title: 'Plan tarix', render: (_, row) => `${row.plannedStartDate} - ${row.plannedEndDate}` },
            { title: 'Status', dataIndex: 'status', render: (value: ProjectWorkStatus) => <Tag color={statusColor[value]}>{statusLabel[value]}</Tag> },
            { title: 'Əməliyyat', width: 120, render: (_, row) => <Button danger icon={<DeleteOutlined />} onClick={() => Modal.confirm({ title: 'Etapı və ona bağlı işləri silmək istəyirsiniz?', okText: 'Sil', cancelText: 'İmtina', onOk: () => deleteStage(row.id) })} /> },
          ]}
        />
      </section>

      <Drawer title={editingItem ? 'İş sətrini redaktə et' : 'Yeni iş sətiri'} open={itemDrawerOpen} width={560} onClose={() => setItemDrawerOpen(false)}>
        <Form form={itemForm} layout="vertical" onFinish={saveWorkItem}>
          <Form.Item name="stageId" label="Etap" rules={[{ required: true }]}><Select showSearch options={stageOptions} /></Form.Item>
          <Form.Item name="name" label="İş adı" rules={[{ required: true }]}><Input /></Form.Item>
          <Space.Compact block>
            <Form.Item name="costCode" label="Cost Code" className="form-half"><Input /></Form.Item>
            <Form.Item name="unit" label="Vahid" rules={[{ required: true }]} className="form-half"><Input /></Form.Item>
          </Space.Compact>
          <Space.Compact block>
            <Form.Item name="quantity" label="Miqdar" rules={[{ required: true }]} className="form-half"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
            <Form.Item name="completedQuantity" label="Tamamlanan miqdar" className="form-half"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
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
          <Space.Compact block>
            <Form.Item name="plannedStartDate" label="Başlama tarixi" className="form-half"><Input placeholder="YYYY-MM-DD" /></Form.Item>
            <Form.Item name="plannedEndDate" label="Bitmə tarixi" className="form-half"><Input placeholder="YYYY-MM-DD" /></Form.Item>
          </Space.Compact>
          <Form.Item name="assignedCrewId" label="Briqada"><Select allowClear showSearch options={crewOptions} /></Form.Item>
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
          <Space.Compact block>
            <Form.Item name="plannedStartDate" label="Başlama" className="form-half"><Input placeholder="YYYY-MM-DD" /></Form.Item>
            <Form.Item name="plannedEndDate" label="Bitmə" className="form-half"><Input placeholder="YYYY-MM-DD" /></Form.Item>
          </Space.Compact>
        </Form>
      </Modal>

      <Modal title="Smeta import önizləmə" open={previewOpen} onCancel={() => setPreviewOpen(false)} footer={<Button onClick={() => setPreviewOpen(false)}>Bağla</Button>} width={920}>
        <p>Tapılan sheet-lər: {previewSheetNames.join(', ')}</p>
        <Table
          size="small"
          pagination={{ pageSize: 8 }}
          dataSource={previewRows.map((row, index) => ({ key: index, row }))}
          columns={[{ title: 'Sətir önizləmə', dataIndex: 'row', render: (row: unknown[]) => row.map((cell) => String(cell ?? '')).join(' | ') }]}
        />
      </Modal>
    </div>
  )
}
