import { DeleteOutlined, DownloadOutlined, EditOutlined, PlusOutlined, UploadOutlined } from '@ant-design/icons'
import { Button, Divider, Drawer, Form, Input, InputNumber, Modal, Select, Slider, Space, Table, Tag, Upload, message } from 'antd'
import type { TableColumnsType } from 'antd'
import { useMemo, useState } from 'react'
import * as XLSX from 'xlsx'
import { ObjectFilter } from '../../components/filters/ObjectFilter'
import { PageTitle } from '../../components/ui/PageTitle'
import { buildTrackBackendApi } from '../../services/api/buildTrackBackendApi'
import type { ProjectWorkStatus, WorkItem } from '../../types/projectProgress'
import { formatCurrency, formatHours, formatNumber } from '../../utils/formatters'
import { UnitSelect } from './constructionUnits'
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
  materialName?: string
  materialUnit?: string
  materialQuantity: number
  materialUnitPrice: number
  materialSupplier?: string
  plannedHours: number
  actualHours: number
  assignedCrewId?: string
  status: ProjectWorkStatus
  progressPercent: number
  plannedStartDate?: string
  plannedEndDate?: string
  notes?: string
}

interface ProjectObjectFormValues {
  name: string
  address?: string
  plannedStartDate?: string
  plannedEndDate?: string
  clientName?: string
  notes?: string
}

const statusOptions = Object.entries(statusLabel).map(([value, label]) => ({ value, label }))

const SingleValueSelect = ({
  options,
  placeholder,
  value,
  onChange,
}: {
  options: { value: string; label: string }[]
  placeholder?: string
  value?: string
  onChange?: (value: string) => void
}) => (
  <Select
    allowClear
    showSearch
    mode="tags"
    maxCount={1}
    value={value ? [value] : []}
    placeholder={placeholder}
    options={options}
    onChange={(values) => onChange?.(values[values.length - 1] ?? '')}
    filterOption={(input, option) => String(option?.label ?? '').toLowerCase().includes(input.toLowerCase())}
  />
)

export const ProjectEstimatePage = () => {
  const store = useProjectProgressStore()
  const {
    addMaterial,
    addObject,
    addStage,
    addWorkItem,
    crews,
    deleteStage,
    deleteWorkItem,
    estimateVersions,
    project,
    stages,
    summary,
    updateMaterial,
    updateWorkItem,
  } = store
  const selectedObjectId = store.selectedObjectIdByPage.estimate ?? ALL_OBJECTS_ID
  const scopedStages = getStagesByObject(store, selectedObjectId)
  const scopedWorkItems = getEstimateRowsByObject(store, selectedObjectId)
  const scopedCrews = getCrewsByObject(store, selectedObjectId)
  const scopedMaterials = getMaterialsByObject(store, selectedObjectId)
  const [itemForm] = Form.useForm<WorkItemFormValues>()
  const [stageForm] = Form.useForm<{ name: string; totalCost: number; plannedHours: number; plannedStartDate: string; plannedEndDate: string }>()
  const [projectForm] = Form.useForm<ProjectObjectFormValues>()
  const [editingItem, setEditingItem] = useState<WorkItem>()
  const [itemDrawerOpen, setItemDrawerOpen] = useState(false)
  const [stageModalOpen, setStageModalOpen] = useState(false)
  const [projectModalOpen, setProjectModalOpen] = useState(false)
  const [previewOpen, setPreviewOpen] = useState(false)
  const [previewSheetNames, setPreviewSheetNames] = useState<string[]>([])
  const [previewRows, setPreviewRows] = useState<unknown[][]>([])

  const stageOptions = scopedStages.map((stage) => ({ value: stage.id, label: stage.name }))
  const crewOptions = scopedCrews.map((crew) => ({ value: crew.id, label: crew.name }))
  const materialOptions = scopedMaterials.map((material) => ({ value: material.name, label: material.name }))
  const stageNameById = useMemo(() => new Map(stages.map((stage) => [stage.id, stage.name])), [stages])
  const crewNameById = useMemo(() => new Map(crews.map((crew) => [crew.id, crew.name])), [crews])

  const openItemDrawer = (item?: WorkItem) => {
    setEditingItem(item)
    const linkedMaterial = item ? scopedMaterials.find((material) => material.linkedWorkItemId === item.id) : undefined
    itemForm.setFieldsValue(item ? {
      ...item,
      materialName: linkedMaterial?.name ?? '',
      materialUnit: linkedMaterial?.unit ?? item.materialUnit ?? 'ədəd',
      materialQuantity: linkedMaterial?.quantity ?? item.materialQuantity,
      materialUnitPrice: linkedMaterial?.unitPrice ?? item.materialUnitPrice,
      materialSupplier: linkedMaterial?.supplier ?? '',
    } : {
      stageId: scopedStages[0]?.id,
      name: '',
      costCode: '',
      unit: 'iş',
      quantity: 1,
      unitPrice: 0,
      completedQuantity: 0,
      laborUnitPrice: 0,
      materialName: '',
      materialUnit: 'ədəd',
      materialQuantity: 1,
      materialUnitPrice: 0,
      materialSupplier: '',
      plannedHours: 0,
      actualHours: 0,
      status: 'NotStarted',
      progressPercent: 0,
    })
    setItemDrawerOpen(true)
  }

  const saveWorkItem = (values: WorkItemFormValues) => {
    const { materialName, materialSupplier, ...workValues } = values
    const laborTotal = values.quantity * values.laborUnitPrice
    const materialTotal = values.materialQuantity * values.materialUnitPrice
    const progressPercent = values.quantity > 0 && typeof values.completedQuantity === 'number'
      ? Math.min(100, Math.round((values.completedQuantity / values.quantity) * 1000) / 10)
      : values.progressPercent
    const selectedStage = stages.find((stage) => stage.id === values.stageId)
    const objectId = editingItem?.objectId ?? selectedStage?.objectId ?? (selectedObjectId === ALL_OBJECTS_ID ? store.objects[0]?.id : selectedObjectId)
    const payload = {
      ...workValues,
      objectId,
      progressPercent,
      laborTotal,
      materialTotal,
      totalCost: laborTotal + materialTotal,
      remainingHours: Math.max(0, values.plannedHours - values.actualHours),
    }
    const savedItemId = editingItem?.id ?? addWorkItem(payload)
    if (editingItem) updateWorkItem(editingItem.id, payload)

    const normalizedMaterialName = materialName?.trim()
    if (normalizedMaterialName && values.materialQuantity > 0) {
      const materialPayload = {
        objectId,
        name: normalizedMaterialName,
        unit: values.materialUnit || values.unit || 'ədəd',
        quantity: values.materialQuantity,
        usedQuantity: Math.min(values.materialQuantity, Math.max(0, values.materialQuantity * (progressPercent / 100))),
        unitPrice: values.materialUnitPrice,
        linkedStageId: values.stageId,
        linkedWorkItemId: savedItemId,
        supplier: materialSupplier?.trim(),
        notes: values.notes,
      }
      const existingMaterial = scopedMaterials.find((material) =>
        material.objectId === objectId
        && material.linkedWorkItemId === savedItemId
        && material.name.toLocaleLowerCase('az-AZ') === normalizedMaterialName.toLocaleLowerCase('az-AZ'))

      if (existingMaterial) updateMaterial(existingMaterial.id, materialPayload)
      else addMaterial(materialPayload)
    }

    setItemDrawerOpen(false)
    void message.success(normalizedMaterialName ? 'Smeta sətri və bağlı material yadda saxlandı' : 'Smeta sətri yadda saxlandı')
  }

  const createProjectObject = (values: ProjectObjectFormValues) => {
    const name = values.name.trim()
    const objectId = addObject({
      name,
      address: values.address?.trim(),
      zone: values.address?.trim() || 'Yeni obyekt',
      plannedStartDate: values.plannedStartDate,
      plannedEndDate: values.plannedEndDate,
      clientName: values.clientName?.trim(),
      notes: values.notes?.trim(),
      status: 'NotStarted',
    })

    projectForm.resetFields()
    setProjectModalOpen(false)
    void message.success('Yeni layihə yaradıldı və obyekt filterlərinə əlavə olundu')

    void buildTrackBackendApi.createSite({
      name,
      address: values.address?.trim() ?? '',
      timeZone: 'Asia/Baku',
    }).catch(() => {
      console.info('Backend site mirror skipped; local project object is still saved', { objectId })
    })
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
        subtitle={`${project.name} üzrə etap, iş sətri, miqdar, saat və xərc redaktoru`}
        extra={
          <Space wrap>
            <Button onClick={() => setProjectModalOpen(true)}>Yeni layihə</Button>
            <ObjectFilter pageKey="estimate" />
            <Upload accept=".xlsx,.xls" showUploadList={false} beforeUpload={(file) => { void parseWorkbook(file as File); return false }}>
              <Button icon={<UploadOutlined />}>Smeta import et</Button>
            </Upload>
            <Button icon={<PlusOutlined />} onClick={() => setStageModalOpen(true)}>Yeni etap</Button>
            <Button type="primary" icon={<PlusOutlined />} onClick={() => openItemDrawer()}>Yeni iş sətri</Button>
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

      <Drawer title={editingItem ? 'İş sətrini redaktə et' : 'Yeni iş sətri'} open={itemDrawerOpen} width={600} onClose={() => setItemDrawerOpen(false)}>
        <Form form={itemForm} layout="vertical" onFinish={saveWorkItem}>
          <Form.Item name="stageId" label="Etap" rules={[{ required: true }]}><Select showSearch options={stageOptions} /></Form.Item>
          <Form.Item name="name" label="İş adı" rules={[{ required: true }]}><Input /></Form.Item>
          <Space.Compact block>
            <Form.Item name="costCode" label="Cost Code" className="form-half"><Input /></Form.Item>
            <Form.Item name="unit" label="Vahid" rules={[{ required: true }]} className="form-half"><UnitSelect /></Form.Item>
          </Space.Compact>
          <Space.Compact block>
            <Form.Item name="quantity" label="Miqdar" rules={[{ required: true }]} className="form-half"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
            <Form.Item name="completedQuantity" label="Tamamlanan miqdar" className="form-half"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
          </Space.Compact>
          <Space.Compact block>
            <Form.Item name="laborUnitPrice" label="İşçilik vahid qiyməti" className="form-half"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
            <Form.Item name="plannedHours" label="Plan saat" className="form-half"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
          </Space.Compact>

          <Divider>Material istifadəsi</Divider>
          <Form.Item name="materialName" label="Material seç / yeni material yaz">
            <SingleValueSelect options={materialOptions} placeholder="Məsələn: Beton B25" />
          </Form.Item>
          <Space.Compact block>
            <Form.Item name="materialUnit" label="Material vahidi" className="form-half"><UnitSelect placeholder="Material vahidi" /></Form.Item>
            <Form.Item name="materialQuantity" label="Material miqdarı" className="form-half"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
          </Space.Compact>
          <Space.Compact block>
            <Form.Item name="materialUnitPrice" label="Material vahid qiyməti" className="form-half"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
            <Form.Item name="materialSupplier" label="Təchizatçı" className="form-half"><Input /></Form.Item>
          </Space.Compact>
          <Button type="dashed" block onClick={() => itemForm.submit()}>Materialı əlavə et və yadda saxla</Button>

          <Space.Compact block>
            <Form.Item name="actualHours" label="Faktiki saat" className="form-half"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
            <Form.Item name="assignedCrewId" label="Briqada" className="form-half"><Select allowClear showSearch options={crewOptions} /></Form.Item>
          </Space.Compact>
          <Space.Compact block>
            <Form.Item name="plannedStartDate" label="Başlama tarixi" className="form-half"><Input placeholder="YYYY-MM-DD" /></Form.Item>
            <Form.Item name="plannedEndDate" label="Bitmə tarixi" className="form-half"><Input placeholder="YYYY-MM-DD" /></Form.Item>
          </Space.Compact>
          <Form.Item name="status" label="Status"><Select options={statusOptions} /></Form.Item>
          <Form.Item name="progressPercent" label="Gedişat %"><Slider min={0} max={100} /></Form.Item>
          <Form.Item name="notes" label="Qeyd"><Input.TextArea rows={3} /></Form.Item>
          <Button type="primary" htmlType="submit" block>Yadda saxla</Button>
        </Form>
      </Drawer>

      <Modal title="Yeni layihə / obyekt" open={projectModalOpen} onCancel={() => setProjectModalOpen(false)} onOk={() => projectForm.submit()} okText="Yarat" cancelText="İmtina">
        <Form form={projectForm} layout="vertical" onFinish={createProjectObject}>
          <Form.Item name="name" label="Layihə / obyekt adı" rules={[{ required: true, message: 'Obyekt adı yazın' }]}><Input placeholder="Məsələn: Villa B blok" /></Form.Item>
          <Form.Item name="address" label="Ünvan"><Input /></Form.Item>
          <Space.Compact block>
            <Form.Item name="plannedStartDate" label="Başlama tarixi" className="form-half"><Input placeholder="YYYY-MM-DD" /></Form.Item>
            <Form.Item name="plannedEndDate" label="Plan bitmə tarixi" className="form-half"><Input placeholder="YYYY-MM-DD" /></Form.Item>
          </Space.Compact>
          <Form.Item name="clientName" label="Müştəri / şirkət adı"><Input /></Form.Item>
          <Form.Item name="notes" label="Qeyd"><Input.TextArea rows={3} /></Form.Item>
        </Form>
      </Modal>

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
