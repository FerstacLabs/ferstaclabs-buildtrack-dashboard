import { DeleteOutlined, EditOutlined, PlusOutlined, TeamOutlined, UserOutlined } from '@ant-design/icons'
import { Button, Drawer, Form, Input, InputNumber, Modal, Progress, Select, Space, Table, Tag, message } from 'antd'
import type { TableColumnsType } from 'antd'
import { useMemo, useState } from 'react'
import { ProjectSelect } from '../../components/ProjectSelect'
import { KpiCard } from '../../components/ui/KpiCard'
import { PageTitle } from '../../components/ui/PageTitle'
import type { AttendanceSource, WorkerAssignment, WorkerStatus } from '../../types/projectProgress'
import { formatCurrency, formatHours, formatNumber } from '../../utils/formatters'
import { ALL_OBJECTS_ID, getCrewsByObject, getEstimateRowsByObject, getProjectWorkers, getWorkerTotalHours } from '../projectProgress/projectSelectors'
import { useProjectProgressStore } from '../projectProgress/projectProgressStore'
import { useProjectSelectionStore } from '../../stores/projectSelectionStore'

interface WorkerFormValues {
  workerName: string
  workerExternalId: string
  crewId: string
  role: string
  hourlyRate: number
  plannedDailyHours: number
  activeWorkItemId?: string
  attendanceSource: AttendanceSource
  status: WorkerStatus
  riskScore: number
  notes?: string
}

const sourceLabel: Record<AttendanceSource, string> = {
  Camera: 'Kamera',
  Manual: 'Manual',
  ForemanTablet: 'Prorab tablet',
}

const generateNextWorkerCode = (workers: WorkerAssignment[]) => {
  const used = new Set(workers.map((worker) => worker.workerExternalId.toUpperCase()))
  const maxExisting = workers.reduce((max, worker) => {
    const match = /^W-(\d{4})$/i.exec(worker.workerExternalId.trim())
    return match ? Math.max(max, Number(match[1])) : max
  }, 0)

  for (let next = maxExisting + 1; next < 10000; next += 1) {
    const candidate = `W-${String(next).padStart(4, '0')}`
    if (!used.has(candidate.toUpperCase())) return candidate
  }

  return `W-${String(workers.length + 1).padStart(4, '0')}`
}

export const WorkersPage = () => {
  const store = useProjectProgressStore()
  const { addWorker, crews, deleteWorker, updateWorker, workItems } = store
  const selectedObjectId = useProjectSelectionStore((state) => state.selectedProjectId)
  const scopedCrews = getCrewsByObject(store, selectedObjectId)
  const scopedWorkItems = getEstimateRowsByObject(store, selectedObjectId)
  const [form] = Form.useForm<WorkerFormValues>()
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [editingWorker, setEditingWorker] = useState<WorkerAssignment>()
  const [crewFilter, setCrewFilter] = useState('all')
  const [roleFilter, setRoleFilter] = useState('all')
  const [statusFilter, setStatusFilter] = useState('all')
  const [riskFilter, setRiskFilter] = useState('all')

  const workers = getProjectWorkers(store, store.project.id, selectedObjectId)
  const crewById = useMemo(() => new Map(crews.map((crew) => [crew.id, crew])), [crews])
  const itemById = useMemo(() => new Map(workItems.map((item) => [item.id, item])), [workItems])
  const crewOptions = scopedCrews.map((crew) => ({ value: crew.id, label: crew.name }))
  const itemOptions = scopedWorkItems.map((item) => ({ value: item.id, label: item.name }))
  const roleOptions = Array.from(new Set(workers.map((worker) => worker.role))).sort().map((role) => ({ value: role, label: role }))

  const rows = workers
    .map((worker) => {
      const hours = getWorkerTotalHours(store, worker.id)
      return {
        ...worker,
        crewName: crewById.get(worker.crewId)?.name ?? 'Təyin edilməyib',
        activeWork: worker.activeWorkItemId ? itemById.get(worker.activeWorkItemId)?.name : 'Təyin edilməyib',
        totalHours: hours,
        laborCost: hours * worker.hourlyRate,
      }
    })
    .filter((worker) => crewFilter === 'all' || worker.crewId === crewFilter)
    .filter((worker) => roleFilter === 'all' || worker.role === roleFilter)
    .filter((worker) => statusFilter === 'all' || worker.status === statusFilter)
    .filter((worker) => riskFilter === 'all' || (riskFilter === 'high' ? worker.riskScore >= 35 : worker.riskScore < 35))

  const openDrawer = (worker?: WorkerAssignment) => {
    setEditingWorker(worker)
    form.setFieldsValue(worker ?? {
      workerName: '',
      workerExternalId: generateNextWorkerCode(workers),
      crewId: scopedCrews[0]?.id,
      role: '',
      hourlyRate: 0,
      plannedDailyHours: 8,
      attendanceSource: 'Camera',
      status: 'active',
      riskScore: 0,
    })
    setDrawerOpen(true)
  }

  const saveWorker = (values: WorkerFormValues) => {
    const activeItem = workItems.find((item) => item.id === values.activeWorkItemId)
    const objectId = editingWorker?.objectId ?? activeItem?.objectId ?? (selectedObjectId === ALL_OBJECTS_ID ? store.objects[0]?.id : selectedObjectId)
    const payload = { ...values, projectId: store.project.id, objectId, activeStageId: activeItem?.stageId }
    if (editingWorker) updateWorker(editingWorker.id, payload)
    else addWorker(payload)
    setDrawerOpen(false)
    void message.success('İşçi məlumatları yadda saxlandı')
  }

  const columns: TableColumnsType<(typeof rows)[number]> = [
    { title: 'İşçi', dataIndex: 'workerName', render: (value, row) => <strong>{value}<br /><span className="muted-text">ID: {row.workerExternalId}</span></strong> },
    { title: 'Briqada', dataIndex: 'crewName' },
    { title: 'Rol', dataIndex: 'role' },
    { title: 'Aktiv iş', dataIndex: 'activeWork' },
    { title: 'Saat mənbəyi', dataIndex: 'attendanceSource', render: (value: AttendanceSource) => sourceLabel[value] },
    { title: 'Tarif', dataIndex: 'hourlyRate', align: 'right', render: (value) => `${formatCurrency(Number(value))}/saat` },
    { title: 'Toplam saat', dataIndex: 'totalHours', align: 'right', render: (value) => formatHours(Number(value), 1), sorter: (a, b) => a.totalHours - b.totalHours },
    { title: 'Əmək xərci', dataIndex: 'laborCost', align: 'right', render: (value) => formatCurrency(Number(value)) },
    { title: 'Risk', dataIndex: 'riskScore', render: (value) => <Progress percent={Number(value)} size="small" status={Number(value) >= 60 ? 'exception' : 'normal'} /> },
    { title: 'Status', dataIndex: 'status', render: (value: WorkerStatus) => <Tag color={value === 'active' ? 'green' : 'default'}>{value === 'active' ? 'Aktiv' : 'Passiv'}</Tag> },
    {
      title: 'Əməliyyat',
      width: 110,
      render: (_, row) => (
        <Space>
          <Button icon={<EditOutlined />} onClick={() => openDrawer(row)} />
          <Button danger icon={<DeleteOutlined />} onClick={() => Modal.confirm({ title: 'İşçini silmək istəyirsiniz?', okText: 'Sil', cancelText: 'İmtina', onOk: () => deleteWorker(row.id) })} />
        </Space>
      ),
    },
  ]

  return (
    <div className="page-stack">
      <PageTitle
        title="İşçilər"
        subtitle="Briqada, aktiv iş, saat mənbəyi və risk göstəriciləri üzrə işçi idarəetməsi"
        extra={<Space wrap><ProjectSelect pageKey="workers" /><Button type="primary" icon={<PlusOutlined />} onClick={() => openDrawer()}>İşçi əlavə et</Button></Space>}
      />

      <section className="kpi-grid four">
        <KpiCard icon={<UserOutlined />} title="İşçi sayı" value={formatNumber(workers.length)} tone="blue" />
        <KpiCard icon={<TeamOutlined />} title="Aktiv işçi" value={formatNumber(workers.filter((worker) => worker.status === 'active').length)} tone="green" />
        <KpiCard icon={<UserOutlined />} title="Kamera mənbəli" value={formatNumber(workers.filter((worker) => worker.attendanceSource === 'Camera').length)} tone="purple" />
        <KpiCard icon={<UserOutlined />} title="Risk nəzarəti" value={formatNumber(workers.filter((worker) => worker.riskScore >= 35).length)} tone="orange" />
      </section>

      <section className="table-card">
        <div className="card-heading">
          <h2>İşçi siyahısı</h2>
          <Space wrap>
            <Select value={crewFilter} onChange={setCrewFilter} style={{ minWidth: 190 }} options={[{ value: 'all', label: 'Bütün briqadalar' }, ...crewOptions]} />
            <Select value={roleFilter} onChange={setRoleFilter} style={{ minWidth: 170 }} options={[{ value: 'all', label: 'Bütün rollar' }, ...roleOptions]} />
            <Select value={statusFilter} onChange={setStatusFilter} style={{ minWidth: 140 }} options={[{ value: 'all', label: 'Bütün statuslar' }, { value: 'active', label: 'Aktiv' }, { value: 'inactive', label: 'Passiv' }]} />
            <Select value={riskFilter} onChange={setRiskFilter} style={{ minWidth: 150 }} options={[{ value: 'all', label: 'Bütün risklər' }, { value: 'high', label: 'Riskli' }, { value: 'normal', label: 'Normal' }]} />
          </Space>
        </div>
        <Table rowKey="id" columns={columns} dataSource={rows} pagination={{ pageSize: 12 }} scroll={{ x: 1360 }} />
      </section>

      <Drawer title={editingWorker ? 'İşçini redaktə et' : 'Yeni işçi'} open={drawerOpen} width={520} onClose={() => setDrawerOpen(false)}>
        <Form form={form} layout="vertical" onFinish={saveWorker}>
          <Form.Item name="workerName" label="İşçi adı" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="workerExternalId" label="İşçi kodu" rules={[{ required: true }]} extra="Sistem tərəfindən avtomatik verilir">
            <Input readOnly={!editingWorker} />
          </Form.Item>
          <Form.Item name="crewId" label="Briqada" rules={[{ required: true }]}><Select showSearch options={crewOptions} /></Form.Item>
          <Form.Item name="activeWorkItemId" label="Aktiv iş sətiri"><Select allowClear showSearch options={itemOptions} /></Form.Item>
          <Space.Compact block>
            <Form.Item name="role" label="Rol" className="form-half"><Input /></Form.Item>
            <Form.Item name="hourlyRate" label="Saatlıq tarif" className="form-half"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
          </Space.Compact>
          <Space.Compact block>
            <Form.Item name="plannedDailyHours" label="Plan gündəlik saat" className="form-half"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
            <Form.Item name="riskScore" label="Risk balı" className="form-half"><InputNumber min={0} max={100} style={{ width: '100%' }} /></Form.Item>
          </Space.Compact>
          <Form.Item name="attendanceSource" label="Davamiyyət mənbəyi"><Select options={[{ value: 'Camera', label: 'Kamera' }, { value: 'Manual', label: 'Manual' }, { value: 'ForemanTablet', label: 'Prorab tablet' }]} /></Form.Item>
          <Form.Item name="status" label="Status"><Select options={[{ value: 'active', label: 'Aktiv' }, { value: 'inactive', label: 'Passiv' }]} /></Form.Item>
          <Form.Item name="notes" label="Qeyd"><Input.TextArea rows={3} /></Form.Item>
          <Button type="primary" htmlType="submit" block>Yadda saxla</Button>
        </Form>
      </Drawer>
    </div>
  )
}
