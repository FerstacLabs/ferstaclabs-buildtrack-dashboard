import { DeleteOutlined, EditOutlined, PlusOutlined, ProfileOutlined } from '@ant-design/icons'
import { Button, Drawer, Form, Input, InputNumber, Modal, Select, Space, Table, Tag, message } from 'antd'
import type { TableColumnsType } from 'antd'
import { useMemo, useState } from 'react'
import { ObjectFilter } from '../../components/filters/ObjectFilter'
import { KpiCard } from '../../components/ui/KpiCard'
import { PageTitle } from '../../components/ui/PageTitle'
import type { DailyForemanReport, DailyReportStatus, WeatherType } from '../../types/projectProgress'
import { formatNumber } from '../../utils/formatters'
import { ALL_OBJECTS_ID, getCrewsByObject, getDailyReportsByObject, getEstimateRowsByObject } from '../projectProgress/projectSelectors'
import { useProjectProgressStore } from '../projectProgress/projectProgressStore'

interface DailyReportFormValues {
  date: string
  weather: WeatherType
  foremanName: string
  crewIds: string[]
  workedItemIds: string[]
  completedWorkItemId?: string
  completedQuantity?: number
  todayNotes: string
  remainingNotes?: string
  delayReason?: string
  materialShortage?: string
  equipmentIssue?: string
  weatherIssue?: string
  status: DailyReportStatus
  photoCount: number
}

const statusColor: Record<DailyReportStatus, string> = {
  Draft: 'default',
  Submitted: 'blue',
  Approved: 'green',
  Rejected: 'red',
}

export const DailyReportsPage = () => {
  const store = useProjectProgressStore()
  const { addDailyReport, crews, deleteDailyReport, project, updateDailyReport, workItems } = store
  const selectedObjectId = store.selectedObjectId ?? ALL_OBJECTS_ID
  const dailyReports = getDailyReportsByObject(store, selectedObjectId)
  const scopedCrews = getCrewsByObject(store, selectedObjectId)
  const scopedWorkItems = getEstimateRowsByObject(store, selectedObjectId)
  const [form] = Form.useForm<DailyReportFormValues>()
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [editingReport, setEditingReport] = useState<DailyForemanReport>()

  const crewById = useMemo(() => new Map(crews.map((crew) => [crew.id, crew.name])), [crews])
  const itemById = useMemo(() => new Map(workItems.map((item) => [item.id, item.name])), [workItems])
  const crewOptions = scopedCrews.map((crew) => ({ value: crew.id, label: crew.name }))
  const itemOptions = scopedWorkItems.map((item) => ({ value: item.id, label: item.name }))

  const openDrawer = (report?: DailyForemanReport) => {
    setEditingReport(report)
    form.setFieldsValue(report ? {
      ...report,
      completedWorkItemId: report.completedWorks[0]?.workItemId,
      completedQuantity: report.completedWorks[0]?.completedQuantity,
    } : {
      date: new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Baku' }).format(new Date()),
      weather: 'Günəşli',
      foremanName: '',
      crewIds: [],
      workedItemIds: [],
      todayNotes: '',
      status: 'Draft',
      photoCount: 0,
    })
    setDrawerOpen(true)
  }

  const saveReport = (values: DailyReportFormValues) => {
    const completedWorks = values.completedWorkItemId && values.completedQuantity
      ? [{ workItemId: values.completedWorkItemId, completedQuantity: values.completedQuantity }]
      : []
    const payload = {
      projectId: project.id,
      objectId: editingReport?.objectId ?? (selectedObjectId === ALL_OBJECTS_ID ? store.objects[0]?.id : selectedObjectId),
      date: values.date,
      weather: values.weather,
      foremanName: values.foremanName,
      crewIds: values.crewIds,
      workedItemIds: values.workedItemIds,
      completedWorks,
      todayNotes: values.todayNotes,
      remainingNotes: values.remainingNotes,
      delayReason: values.delayReason,
      materialShortage: values.materialShortage,
      equipmentIssue: values.equipmentIssue,
      weatherIssue: values.weatherIssue,
      status: values.status,
      photoCount: values.photoCount,
      photos: [],
    }

    if (editingReport) updateDailyReport(editingReport.id, payload)
    else addDailyReport(payload)
    setDrawerOpen(false)
    void message.success('Gündəlik hesabat yadda saxlandı')
  }

  const rows = dailyReports.map((report) => ({
    ...report,
    crewNames: report.crewIds.map((id) => crewById.get(id)).filter(Boolean).join(', ') || 'Təyin edilməyib',
    workNames: report.workedItemIds.map((id) => itemById.get(id)).filter(Boolean).join(', ') || 'Təyin edilməyib',
  }))

  const columns: TableColumnsType<(typeof rows)[number]> = [
    { title: 'Tarix', dataIndex: 'date', sorter: (a, b) => a.date.localeCompare(b.date) },
    { title: 'Prorab', dataIndex: 'foremanName' },
    { title: 'Hava', dataIndex: 'weather' },
    { title: 'Briqadalar', dataIndex: 'crewNames' },
    { title: 'Görülən işlər', dataIndex: 'workNames' },
    { title: 'Qeyd', dataIndex: 'todayNotes', ellipsis: true },
    { title: 'Foto', dataIndex: 'photoCount', align: 'right' },
    { title: 'Status', dataIndex: 'status', render: (value: DailyReportStatus) => <Tag color={statusColor[value]}>{value}</Tag> },
    {
      title: 'Əməliyyat',
      width: 110,
      render: (_, row) => (
        <Space>
          <Button icon={<EditOutlined />} onClick={() => openDrawer(row)} />
          <Button danger icon={<DeleteOutlined />} onClick={() => Modal.confirm({ title: 'Hesabatı silmək istəyirsiniz?', okText: 'Sil', cancelText: 'İmtina', onOk: () => deleteDailyReport(row.id) })} />
        </Space>
      ),
    },
  ]

  return (
    <div className="page-stack">
      <PageTitle
        title="Gündəlik hesabatlar"
        subtitle="Prorab qeydləri, görülən iş miqdarı, gecikmə səbəbləri və foto sayları"
        extra={<Space wrap><ObjectFilter pageKey="dailyReports" /><Button type="primary" icon={<PlusOutlined />} onClick={() => openDrawer()}>Gündəlik əlavə et</Button></Space>}
      />

      <section className="kpi-grid four">
        <KpiCard icon={<ProfileOutlined />} title="Hesabat sayı" value={formatNumber(dailyReports.length)} tone="blue" />
        <KpiCard icon={<ProfileOutlined />} title="Təsdiqlənmiş" value={formatNumber(dailyReports.filter((report) => report.status === 'Approved').length)} tone="green" />
        <KpiCard icon={<ProfileOutlined />} title="Açıq qeydlər" value={formatNumber(dailyReports.filter((report) => report.status !== 'Approved').length)} tone="orange" />
        <KpiCard icon={<ProfileOutlined />} title="Foto sayı" value={formatNumber(dailyReports.reduce((sum, report) => sum + report.photoCount, 0))} tone="purple" />
      </section>

      <section className="table-card">
        <div className="card-heading">
          <h2>Prorab gündəliyi</h2>
        </div>
        <Table rowKey="id" columns={columns} dataSource={rows} pagination={{ pageSize: 8 }} scroll={{ x: 1100 }} />
      </section>

      <Drawer title={editingReport ? 'Gündəliyi redaktə et' : 'Yeni gündəlik'} open={drawerOpen} width={560} onClose={() => setDrawerOpen(false)}>
        <Form form={form} layout="vertical" onFinish={saveReport}>
          <Space.Compact block>
            <Form.Item name="date" label="Tarix" rules={[{ required: true }]} className="form-half"><Input placeholder="YYYY-MM-DD" /></Form.Item>
            <Form.Item name="weather" label="Hava" className="form-half"><Select options={['Günəşli', 'Yağışlı', 'Küləkli', 'Soyuq', 'İsti'].map((value) => ({ value, label: value }))} /></Form.Item>
          </Space.Compact>
          <Form.Item name="foremanName" label="Prorab" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="crewIds" label="İştirak edən briqadalar"><Select mode="multiple" showSearch options={crewOptions} /></Form.Item>
          <Form.Item name="workedItemIds" label="Bu gün işlənən işlər"><Select mode="multiple" showSearch options={itemOptions} /></Form.Item>
          <Space.Compact block>
            <Form.Item name="completedWorkItemId" label="Miqdar yazılan iş" className="form-half"><Select allowClear showSearch options={itemOptions} /></Form.Item>
            <Form.Item name="completedQuantity" label="Tamamlanan miqdar" className="form-half"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
          </Space.Compact>
          <Form.Item name="todayNotes" label="Bu gün görülən işlər" rules={[{ required: true }]}><Input.TextArea rows={3} /></Form.Item>
          <Form.Item name="remainingNotes" label="Qalan işlər"><Input.TextArea rows={2} /></Form.Item>
          <Form.Item name="delayReason" label="Problem/gecikmə səbəbi"><Input.TextArea rows={2} /></Form.Item>
          <Form.Item name="materialShortage" label="Material çatışmazlığı"><Input /></Form.Item>
          <Form.Item name="equipmentIssue" label="Texnika problemi"><Input /></Form.Item>
          <Form.Item name="weatherIssue" label="Hava şəraiti səbəbi"><Input /></Form.Item>
          <Space.Compact block>
            <Form.Item name="status" label="Status" className="form-half"><Select options={['Draft', 'Submitted', 'Approved', 'Rejected'].map((value) => ({ value, label: value }))} /></Form.Item>
            <Form.Item name="photoCount" label="Foto sayı" className="form-half"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
          </Space.Compact>
          <Button type="primary" htmlType="submit" block>Yadda saxla</Button>
        </Form>
      </Drawer>
    </div>
  )
}
