import { DeleteOutlined, EditOutlined, PlusOutlined, TeamOutlined } from '@ant-design/icons'
import { Button, Card, Drawer, Form, Input, InputNumber, Modal, Progress, Select, Space, Table, Tag, message } from 'antd'
import type { TableColumnsType } from 'antd'
import { useMemo, useState } from 'react'
import { ObjectFilter } from '../../components/filters/ObjectFilter'
import { KpiCard } from '../../components/ui/KpiCard'
import { PageTitle } from '../../components/ui/PageTitle'
import type { Crew, ProjectWorkStatus } from '../../types/projectProgress'
import { formatHours, formatNumber, formatPercent } from '../../utils/formatters'
import { ALL_OBJECTS_ID, getCrewsByObject, getCrewActualHours, getEstimateRowsByObject, getStagesByObject, getWorkersByCrew } from './projectSelectors'
import { calculateStageProgress, statusColor, statusLabel, useProjectProgressStore } from './projectProgressStore'

interface CrewFormValues {
  name: string
  type: string
  foremanName: string
  workerCount: number
  activeWorkStageId?: string
  activeWorkItemId?: string
  plannedDailyHours: number
  notes?: string
}

const crewTypes = ['Monolit', 'Hörgü', 'Suvaq', 'Dam', 'Pəncərə/Qapı', 'Material/logistika', 'Digər']

const isProjectWorkStatus = (value: unknown): value is ProjectWorkStatus =>
  typeof value === 'string' && value in statusLabel

export const ProjectCrewsPage = () => {
  const store = useProjectProgressStore()
  const { addCrew, deleteCrew, stages, updateCrew, workItems } = store
  const selectedObjectId = store.selectedObjectIdByPage.crews ?? ALL_OBJECTS_ID
  const scopedCrews = getCrewsByObject(store, selectedObjectId)
  const scopedStages = getStagesByObject(store, selectedObjectId)
  const scopedWorkItems = getEstimateRowsByObject(store, selectedObjectId)
  const [form] = Form.useForm<CrewFormValues>()
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [editingCrew, setEditingCrew] = useState<Crew>()

  const stageOptions = scopedStages.map((stage) => ({ value: stage.id, label: stage.name }))
  const workItemOptions = scopedWorkItems.map((item) => ({ value: item.id, label: item.name }))
  const stageById = useMemo(() => new Map(stages.map((stage) => [stage.id, stage])), [stages])
  const itemById = useMemo(() => new Map(workItems.map((item) => [item.id, item])), [workItems])

  const openDrawer = (crew?: Crew) => {
    setEditingCrew(crew)
    form.setFieldsValue(crew ?? {
      name: '',
      type: 'Monolit',
      foremanName: '',
      workerCount: 1,
      plannedDailyHours: 8,
    })
    setDrawerOpen(true)
  }

  const saveCrew = (values: CrewFormValues) => {
    const objectId = editingCrew?.objectId ?? (selectedObjectId === ALL_OBJECTS_ID ? store.objects[0]?.id : selectedObjectId)
    if (editingCrew) updateCrew(editingCrew.id, { ...values, objectId })
    else addCrew({ ...values, objectId })
    setDrawerOpen(false)
    void message.success('Briqada məlumatları yadda saxlandı')
  }

  const rows = scopedCrews.map((crew) => {
    const stage = crew.activeWorkStageId ? stageById.get(crew.activeWorkStageId) : undefined
    const item = crew.activeWorkItemId ? itemById.get(crew.activeWorkItemId) : undefined
    const relatedItems = workItems.filter((workItem) => workItem.assignedCrewId === crew.id)
    const plannedHours = relatedItems.reduce((sum, workItem) => sum + workItem.plannedHours, 0)
    const actualHours = getCrewActualHours(store, crew.id) || relatedItems.reduce((sum, workItem) => sum + workItem.actualHours, 0)
    const progress = stage ? calculateStageProgress(stage, workItems) : item?.progressPercent ?? 0
    const workers = getWorkersByCrew(store, crew.id)
    return {
      ...crew,
      workerCount: workers.length,
      activeWork: item?.name ?? stage?.name ?? 'Təyin edilməyib',
      activeStatus: item?.status ?? stage?.status,
      progress,
      plannedHours,
      actualHours,
      registeredWorkers: workers.length,
    }
  })

  const columns: TableColumnsType<(typeof rows)[number]> = [
    { title: 'Briqada', dataIndex: 'name', render: (value, row) => <strong>{value}<br /><span className="muted-text">{row.type}</span></strong> },
    { title: 'Prorab', dataIndex: 'foremanName' },
    { title: 'İşçi sayı', dataIndex: 'workerCount', align: 'right', sorter: (a, b) => a.workerCount - b.workerCount },
    { title: 'Qeydiyyatlı işçi', dataIndex: 'registeredWorkers', align: 'right' },
    { title: 'Aktiv iş', dataIndex: 'activeWork' },
    { title: 'Status', dataIndex: 'activeStatus', render: (value) => isProjectWorkStatus(value) ? <Tag color={statusColor[value]}>{statusLabel[value]}</Tag> : <Tag>Təyin edilməyib</Tag> },
    { title: 'Gedişat', dataIndex: 'progress', render: (value) => <Progress percent={Number(value)} size="small" /> },
    { title: 'Plan/Fakt saat', render: (_, row) => `${formatHours(row.plannedHours, 0)} / ${formatHours(row.actualHours, 0)}` },
    {
      title: 'Əməliyyat',
      width: 110,
      render: (_, row) => (
        <Space>
          <Button icon={<EditOutlined />} onClick={() => openDrawer(row)} />
          <Button danger icon={<DeleteOutlined />} onClick={() => Modal.confirm({ title: 'Briqadanı silmək istəyirsiniz?', okText: 'Sil', cancelText: 'İmtina', onOk: () => deleteCrew(row.id) })} />
        </Space>
      ),
    },
  ]

  return (
    <div className="page-stack project-progress-page">
      <PageTitle
        title="Briqadalar"
        subtitle="Briqadaları etaplara, iş sətirlərinə və prorab məsuliyyətinə görə idarə edin"
        extra={<Space wrap><ObjectFilter pageKey="crews" /><Button type="primary" icon={<PlusOutlined />} onClick={() => openDrawer()}>Briqada əlavə et</Button></Space>}
      />

      <section className="kpi-grid four">
        <KpiCard icon={<TeamOutlined />} title="Briqada sayı" value={formatNumber(scopedCrews.length)} tone="blue" />
        <KpiCard icon={<TeamOutlined />} title="Aktiv briqadalar" value={formatNumber(rows.filter((row) => row.activeWorkStageId || row.activeWorkItemId).length)} tone="green" />
        <KpiCard icon={<TeamOutlined />} title="Ümumi işçi sayı" value={formatNumber(rows.reduce((sum, crew) => sum + crew.workerCount, 0))} tone="orange" />
        <KpiCard icon={<TeamOutlined />} title="Orta gedişat" value={formatPercent(rows.reduce((sum, row) => sum + row.progress, 0) / Math.max(1, rows.length), 1)} tone="purple" />
      </section>

      <section className="crew-card-grid">
        {rows.map((crew) => (
          <Card key={crew.id} className="crew-card" title={crew.name} extra={<Button size="small" icon={<EditOutlined />} onClick={() => openDrawer(crew)} />}>
            <p><strong>Prorab:</strong> {crew.foremanName}</p>
            <p><strong>Aktiv iş:</strong> {crew.activeWork}</p>
            <Progress percent={crew.progress} />
            <div className="crew-card-footer">
              <span>{crew.workerCount} işçi</span>
              <span>{formatHours(crew.plannedDailyHours, 0)}/gün</span>
            </div>
          </Card>
        ))}
      </section>

      <section className="table-card">
        <div className="card-heading">
          <h2>Briqada plan-fakt cədvəli</h2>
        </div>
        <Table rowKey="id" columns={columns} dataSource={rows} pagination={{ pageSize: 8 }} />
      </section>

      <Drawer title={editingCrew ? 'Briqadanı redaktə et' : 'Yeni briqada'} open={drawerOpen} width={520} onClose={() => setDrawerOpen(false)}>
        <Form form={form} layout="vertical" onFinish={saveCrew}>
          <Form.Item name="name" label="Briqada adı" rules={[{ required: true }]}><Input /></Form.Item>
          <Space.Compact block>
            <Form.Item name="type" label="Növ" className="form-half"><Select options={crewTypes.map((value) => ({ value, label: value }))} /></Form.Item>
            <Form.Item name="workerCount" label="İşçi sayı" className="form-half"><InputNumber min={1} style={{ width: '100%' }} /></Form.Item>
          </Space.Compact>
          <Form.Item name="foremanName" label="Prorab" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="activeWorkStageId" label="Aktiv etap"><Select allowClear showSearch options={stageOptions} /></Form.Item>
          <Form.Item name="activeWorkItemId" label="Aktiv iş sətiri"><Select allowClear showSearch options={workItemOptions} /></Form.Item>
          <Form.Item name="plannedDailyHours" label="Plan gündəlik saat"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
          <Form.Item name="notes" label="Qeyd"><Input.TextArea rows={3} /></Form.Item>
          <Button type="primary" htmlType="submit" block>Yadda saxla</Button>
        </Form>
      </Drawer>
    </div>
  )
}
