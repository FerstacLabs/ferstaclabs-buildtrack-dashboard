import { DeleteOutlined, EditOutlined, LinkOutlined, PlusOutlined, TeamOutlined, UserOutlined } from '@ant-design/icons'
import { Alert, Button, Divider, Drawer, Form, Input, InputNumber, Modal, Progress, Select, Space, Table, Tag, Typography, message } from 'antd'
import type { TableColumnsType } from 'antd'
import { useEffect, useMemo, useState } from 'react'
import { ProjectSelect } from '../../components/ProjectSelect'
import { KpiCard } from '../../components/ui/KpiCard'
import { PageTitle } from '../../components/ui/PageTitle'
import { buildTrackBackendApi, type BackendDevice, type BackendSite, type BackendWorker, type SaveWorkerBody } from '../../services/api/buildTrackBackendApi'
import type { AttendanceSource, WorkerAssignment, WorkerStatus } from '../../types/projectProgress'
import { formatCurrency, formatHours, formatNumber } from '../../utils/formatters'
import { ALL_OBJECTS_ID, getCrewsByObject, getEstimateRowsByObject, getProjectWorkers, getWorkerTotalHours } from '../projectProgress/projectSelectors'
import { useProjectProgressStore } from '../projectProgress/projectProgressStore'
import { useProjectSelectionStore } from '../../stores/projectSelectionStore'

interface WorkerFormValues {
  workerName: string
  workerExternalId: string
  crewId?: string
  role?: string
  hourlyRate: number
  plannedDailyHours: number
  activeWorkItemId?: string
  attendanceSource: AttendanceSource
  status: WorkerStatus
  riskScore: number
  notes?: string
  dahuaCardName?: string
  dahuaUserId?: string
  cameraDeviceId?: string
  assignedSiteIds?: string[]
  primarySiteId?: string
}

interface WorkerRow {
  id: string
  siteId?: string
  objectId?: string
  isBackend: boolean
  workerName: string
  workerExternalId: string
  crewId?: string
  crewName: string
  role: string
  activeWork: string
  attendanceSource: AttendanceSource
  hourlyRate: number
  plannedDailyHours: number
  totalHours: number
  laborCost: number
  todayCameraHours: number
  todayEstimatedPay: number
  monthlyCameraHours: number
  monthlyEstimatedPay: number
  riskScore: number
  status: WorkerStatus
  notes?: string
  cameraIdentityId?: string
  dahuaCardName?: string
  dahuaUserId?: string
  cameraDeviceId?: string
  cameraDeviceName?: string
  siteAssignments?: BackendWorker['siteAssignments']
  assignedSiteIds?: string[]
  primarySiteId?: string
  assignedSiteNames?: string[]
  isCurrentlyActive?: boolean
  lastSeenAt?: string
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

const toBackendStatus = (status: WorkerStatus): SaveWorkerBody['status'] => (status === 'active' ? 'Active' : 'Inactive')
const toUiStatus = (status: BackendWorker['status']): WorkerStatus => (status === 'Active' ? 'active' : 'inactive')
const hasCameraIdentity = (values: Pick<WorkerFormValues, 'dahuaCardName' | 'dahuaUserId'>) =>
  Boolean(values.dahuaCardName?.trim() || values.dahuaUserId?.trim())
const resolveBackendSiteId = (selectedObjectId: string, siteRows: BackendSite[], objectName?: string) => {
  if (selectedObjectId === ALL_OBJECTS_ID) return undefined
  if (siteRows.some((site) => site.id === selectedObjectId)) return selectedObjectId
  return objectName ? siteRows.find((site) => site.name.trim().toLowerCase() === objectName.trim().toLowerCase())?.id : undefined
}

export const WorkersPage = () => {
  const store = useProjectProgressStore()
  const { addWorker, crews, deleteWorker, updateWorker, workItems } = store
  const selectedObjectId = useProjectSelectionStore((state) => state.selectedProjectId)
  const scopedCrews = getCrewsByObject(store, selectedObjectId)
  const scopedWorkItems = getEstimateRowsByObject(store, selectedObjectId)
  const [form] = Form.useForm<WorkerFormValues>()
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [editingWorker, setEditingWorker] = useState<WorkerRow>()
  const [crewFilter, setCrewFilter] = useState('all')
  const [roleFilter, setRoleFilter] = useState('all')
  const [statusFilter, setStatusFilter] = useState('all')
  const [riskFilter, setRiskFilter] = useState('all')
  const [backendWorkers, setBackendWorkers] = useState<BackendWorker[]>([])
  const [sites, setSites] = useState<BackendSite[]>([])
  const [devices, setDevices] = useState<BackendDevice[]>([])
  const [loadingBackend, setLoadingBackend] = useState(false)
  const [backendError, setBackendError] = useState('')

  const localWorkers = getProjectWorkers(store, store.project.id, selectedObjectId)
  const crewById = useMemo(() => new Map(crews.map((crew) => [crew.id, crew])), [crews])
  const itemById = useMemo(() => new Map(workItems.map((item) => [item.id, item])), [workItems])
  const crewOptions = scopedCrews.map((crew) => ({ value: crew.id, label: crew.name }))
  const itemOptions = scopedWorkItems.map((item) => ({ value: item.id, label: item.name }))
  const deviceOptions = [
    { value: 'all', label: 'Bütün kameralar' },
    ...devices.map((device) => ({ value: device.id, label: device.name })),
  ]

  const loadBackendWorkers = async () => {
    setLoadingBackend(true)
    setBackendError('')
    try {
      const [siteRows, deviceRows] = await Promise.all([
        buildTrackBackendApi.getSites(),
        buildTrackBackendApi.getDevices(),
      ])
      const selectedObjectName = store.objects.find((object) => object.id === selectedObjectId)?.name
      const workerRows = await buildTrackBackendApi.getWorkers(resolveBackendSiteId(selectedObjectId, siteRows, selectedObjectName))
      setSites(siteRows)
      setDevices(deviceRows)
      setBackendWorkers(workerRows)
    } catch (err) {
      setBackendError(err instanceof Error ? err.message : 'Backend işçi məlumatları yüklənmədi')
    } finally {
      setLoadingBackend(false)
    }
  }

  useEffect(() => {
    void loadBackendWorkers()
    const timer = window.setInterval(() => {
      void loadBackendWorkers()
    }, 30000)
    return () => window.clearInterval(timer)
  }, [selectedObjectId])

  const backendRows: WorkerRow[] = backendWorkers.map((worker) => {
    const primaryIdentity = worker.cameraIdentities?.[0]
    return {
      id: worker.id,
      siteId: worker.siteId,
      isBackend: true,
      workerName: worker.fullName,
      workerExternalId: worker.externalWorkerCode,
      crewName: worker.brigade || 'Təyin edilməyib',
      role: worker.role || 'Təyin edilməyib',
      activeWork: 'Kamera davamiyyəti',
      attendanceSource: worker.attendanceSource,
      hourlyRate: Number(worker.hourlyRate ?? 0),
      plannedDailyHours: Number(worker.plannedDailyHours ?? 8),
      totalHours: Number(worker.payrollSummary?.monthlyCameraHours ?? 0),
      laborCost: Number(worker.payrollSummary?.monthlyEstimatedPay ?? 0),
      todayCameraHours: Number(worker.payrollSummary?.todayCameraHours ?? 0),
      todayEstimatedPay: Number(worker.payrollSummary?.todayEstimatedPay ?? 0),
      monthlyCameraHours: Number(worker.payrollSummary?.monthlyCameraHours ?? 0),
      monthlyEstimatedPay: Number(worker.payrollSummary?.monthlyEstimatedPay ?? 0),
      riskScore: Number(worker.riskScore ?? 0),
      status: toUiStatus(worker.status),
      notes: worker.notes,
      siteAssignments: worker.siteAssignments ?? [],
      assignedSiteIds: (worker.siteAssignments ?? []).filter((assignment) => assignment.status === 'Active').map((assignment) => assignment.siteId),
      primarySiteId: (worker.siteAssignments ?? []).find((assignment) => assignment.isPrimary && assignment.status === 'Active')?.siteId ?? worker.siteId,
      assignedSiteNames: (worker.siteAssignments ?? []).filter((assignment) => assignment.status === 'Active').map((assignment) => assignment.siteName || assignment.siteId),
      isCurrentlyActive: Boolean(worker.payrollSummary?.isCurrentlyActive),
      lastSeenAt: worker.payrollSummary?.lastSeenAt,
      cameraIdentityId: primaryIdentity?.id,
      dahuaCardName: primaryIdentity?.cardName,
      dahuaUserId: primaryIdentity?.externalUserId,
      cameraDeviceId: primaryIdentity?.deviceId ?? 'all',
      cameraDeviceName: primaryIdentity?.deviceName ?? 'Bütün kameralar',
    }
  })

  const localRows: WorkerRow[] = localWorkers.map((worker) => {
    const hours = getWorkerTotalHours(store, worker.id)
    return {
      id: worker.id,
      isBackend: false,
      objectId: worker.objectId,
      workerName: worker.workerName,
      workerExternalId: worker.workerExternalId,
      crewId: worker.crewId,
      crewName: crewById.get(worker.crewId)?.name ?? 'Təyin edilməyib',
      role: worker.role,
      activeWork: worker.activeWorkItemId ? itemById.get(worker.activeWorkItemId)?.name ?? 'Təyin edilməyib' : 'Təyin edilməyib',
      attendanceSource: worker.attendanceSource,
      hourlyRate: worker.hourlyRate,
      plannedDailyHours: worker.plannedDailyHours,
      totalHours: hours,
      laborCost: hours * worker.hourlyRate,
      todayCameraHours: 0,
      todayEstimatedPay: 0,
      monthlyCameraHours: hours,
      monthlyEstimatedPay: hours * worker.hourlyRate,
      riskScore: worker.riskScore,
      status: worker.status,
      notes: worker.notes,
    }
  })

  const rowsSource = backendRows.length > 0 || !backendError ? backendRows : localRows
  const roleOptions = Array.from(new Set(rowsSource.map((worker) => worker.role).filter(Boolean))).sort().map((role) => ({ value: role, label: role }))

  const rows = rowsSource
    .filter((worker) => crewFilter === 'all' || worker.crewName === crewFilter || worker.crewId === crewFilter)
    .filter((worker) => roleFilter === 'all' || worker.role === roleFilter)
    .filter((worker) => statusFilter === 'all' || worker.status === statusFilter)
    .filter((worker) => riskFilter === 'all' || (riskFilter === 'high' ? worker.riskScore >= 35 : worker.riskScore < 35))

  const openDrawer = (worker?: WorkerRow) => {
    const selectedObjectName = store.objects.find((object) => object.id === selectedObjectId)?.name
    const selectedBackendSiteId = resolveBackendSiteId(selectedObjectId, sites, selectedObjectName)
    setEditingWorker(worker)
    form.setFieldsValue(worker ? {
      workerName: worker.workerName,
      workerExternalId: worker.workerExternalId,
      crewId: worker.crewId,
      role: worker.role === 'Təyin edilməyib' ? '' : worker.role,
      hourlyRate: worker.hourlyRate,
      plannedDailyHours: worker.plannedDailyHours,
      attendanceSource: worker.attendanceSource,
      status: worker.status,
      riskScore: worker.riskScore,
      notes: worker.notes,
      dahuaCardName: worker.dahuaCardName,
      dahuaUserId: worker.dahuaUserId,
      cameraDeviceId: worker.cameraDeviceId ?? 'all',
      assignedSiteIds: worker.assignedSiteIds,
      primarySiteId: worker.primarySiteId,
    } : {
      workerName: '',
      workerExternalId: generateNextWorkerCode(localWorkers),
      crewId: scopedCrews[0]?.id,
      role: '',
      hourlyRate: 0,
      plannedDailyHours: 8,
      attendanceSource: 'Camera',
      status: 'active',
      riskScore: 0,
      cameraDeviceId: 'all',
      assignedSiteIds: selectedBackendSiteId ? [selectedBackendSiteId] : [],
      primarySiteId: selectedBackendSiteId,
    })
    setDrawerOpen(true)
  }

  const saveWorker = async (values: WorkerFormValues) => {
    if (values.attendanceSource === 'Camera' && !hasCameraIdentity(values)) {
      void message.warning('Bu işçi kamera davamiyyəti üçün tanınma məlumatına bağlanmayıb.')
    }

    if (sites.length > 0) {
      const assignedSiteIds = [...new Set((values.assignedSiteIds ?? []).filter((siteId) => sites.some((site) => site.id === siteId)))]
      const primarySiteId = values.primarySiteId && assignedSiteIds.includes(values.primarySiteId)
        ? values.primarySiteId
        : assignedSiteIds[0]
      const siteId = primarySiteId ?? editingWorker?.siteId ?? sites[0].id
      if (assignedSiteIds.length === 0) {
        void message.warning('Obyekt təyinatı seçilməyib. Bu işçi yalnız “Bütün obyektlər” görünüşündə qalacaq.')
      }
      if (values.attendanceSource === 'Manual' && hasCameraIdentity(values)) {
        void message.warning('Kamera identifikasiyası daxil edilib, amma saat mənbəyi Manual seçilib.')
      }
      const body: SaveWorkerBody = {
        siteId,
        externalWorkerCode: values.workerExternalId,
        fullName: values.workerName,
        status: toBackendStatus(values.status),
        brigade: scopedCrews.find((crew) => crew.id === values.crewId)?.name,
        role: values.role,
        hourlyRate: values.hourlyRate,
        plannedDailyHours: values.plannedDailyHours,
        attendanceSource: values.attendanceSource,
        riskScore: values.riskScore,
        notes: values.notes,
        siteAssignments: assignedSiteIds.map((assignedSiteId) => ({ siteId: assignedSiteId, isPrimary: assignedSiteId === primarySiteId })),
        cameraIdentity: hasCameraIdentity(values)
          ? {
              deviceId: values.cameraDeviceId && values.cameraDeviceId !== 'all' ? values.cameraDeviceId : undefined,
              cardName: values.dahuaCardName,
              externalUserId: values.dahuaUserId,
              isPrimary: true,
            }
          : undefined,
      }
      try {
        if (editingWorker?.isBackend) await buildTrackBackendApi.updateWorker(editingWorker.id, body)
        else await buildTrackBackendApi.createWorker(body)
        await loadBackendWorkers()
        setDrawerOpen(false)
        void message.success('İşçi məlumatları yadda saxlandı')
        return
      } catch (err) {
        void message.error(err instanceof Error ? err.message : 'İşçi yadda saxlanmadı')
        return
      }
    }

    const activeItem = workItems.find((item) => item.id === values.activeWorkItemId)
    const fallbackObjectId = (!editingWorker?.isBackend ? editingWorker?.objectId : undefined) ?? activeItem?.objectId ?? (selectedObjectId === ALL_OBJECTS_ID ? store.objects[0]?.id : selectedObjectId)
    const payload = {
      ...values,
      crewId: values.crewId ?? scopedCrews[0]?.id ?? '',
      role: values.role ?? '',
      projectId: store.project.id,
      objectId: fallbackObjectId,
      activeStageId: activeItem?.stageId,
    }
    if (editingWorker && !editingWorker.isBackend) updateWorker(editingWorker.id, payload)
    else addWorker(payload)
    setDrawerOpen(false)
    void message.success('İşçi məlumatları yadda saxlandı')
  }

  const removeWorker = async (row: WorkerRow) => {
    if (row.isBackend) {
      await buildTrackBackendApi.deleteWorker(row.id)
      await loadBackendWorkers()
      return
    }
    deleteWorker(row.id)
  }

  const testMapping = async () => {
    if (!editingWorker?.isBackend) {
      void message.info('Test üçün əvvəlcə işçini yadda saxlayın.')
      return
    }
    const values = form.getFieldsValue()
    try {
      const result = await buildTrackBackendApi.testWorkerCameraIdentity(editingWorker.id, {
        deviceId: values.cameraDeviceId && values.cameraDeviceId !== 'all' ? values.cameraDeviceId : undefined,
        cardName: values.dahuaCardName,
        externalUserId: values.dahuaUserId,
      })
      if (result.matched) void message.success(`Mapping tapıldı: ${result.workerName} / ${result.workerCode}`)
      else void message.warning(result.reason || 'Bu kamera identifikasiyası hələ işçiyə bağlanmayıb.')
    } catch (err) {
      void message.error(err instanceof Error ? err.message : 'Mapping testi alınmadı')
    }
  }

  const remapWorker = async () => {
    if (!editingWorker?.isBackend) return
    try {
      const result = await buildTrackBackendApi.remapWorkerCameraEvents(editingWorker.id, editingWorker.cameraIdentityId)
      await loadBackendWorkers()
      void message.success(`Keçmiş qeydlər bağlandı: ${result.attendanceEventsUpdated} event, ${result.attendanceSessionsUpdated} sessiya`)
    } catch (err) {
      void message.error(err instanceof Error ? err.message : 'Remap alınmadı')
    }
  }

  const cameraWorkersCount = rowsSource.filter((worker) => worker.attendanceSource === 'Camera' || Boolean(worker.dahuaCardName || worker.dahuaUserId)).length
  const siteOptions = sites.map((site) => ({ value: site.id, label: site.name }))

  const columns: TableColumnsType<WorkerRow> = [
    { title: 'İşçi', dataIndex: 'workerName', render: (value, row) => <strong>{value}<br /><span className="muted-text">İşçi kodu: {row.workerExternalId}</span></strong> },
    { title: 'Briqada', dataIndex: 'crewName' },
    { title: 'Obyekt təyinatı', render: (_, row) => row.assignedSiteNames?.length ? row.assignedSiteNames.join(', ') : <Tag>Yalnız bütün obyektlər</Tag> },
    { title: 'Rol', dataIndex: 'role' },
    { title: 'Kamera identifikasiyası', render: (_, row) => row.dahuaCardName || row.dahuaUserId ? <span>Dahua CardName: {row.dahuaCardName || '-'}<br /><span className="muted-text">UserID: {row.dahuaUserId || '-'} | {row.cameraDeviceName || 'Bütün kameralar'}</span></span> : <Tag>Bağlanmayıb</Tag> },
    { title: 'Saat mənbəyi', dataIndex: 'attendanceSource', render: (value: AttendanceSource) => sourceLabel[value] },
    { title: 'Tarif', dataIndex: 'hourlyRate', align: 'right', render: (value) => `${formatCurrency(Number(value))}/saat` },
    { title: 'Bugün kamera saatı', dataIndex: 'todayCameraHours', align: 'right', render: (value) => formatHours(Number(value), 1), sorter: (a, b) => a.todayCameraHours - b.todayCameraHours },
    { title: 'Bugün təxmini məbləğ', dataIndex: 'todayEstimatedPay', align: 'right', render: (value) => formatCurrency(Number(value)) },
    { title: 'Aylıq kamera saatı', dataIndex: 'monthlyCameraHours', align: 'right', render: (value) => formatHours(Number(value), 1) },
    { title: 'Aylıq təxmini məbləğ', dataIndex: 'monthlyEstimatedPay', align: 'right', render: (value) => formatCurrency(Number(value)) },
    { title: 'Risk', dataIndex: 'riskScore', render: (value) => <Progress percent={Number(value)} size="small" status={Number(value) >= 60 ? 'exception' : 'normal'} /> },
    { title: 'Status', dataIndex: 'status', render: (value: WorkerStatus) => <Tag color={value === 'active' ? 'green' : 'default'}>{value === 'active' ? 'Aktiv' : 'Passiv'}</Tag> },
    {
      title: 'Əməliyyat',
      width: 120,
      render: (_, row) => (
        <Space>
          <Button icon={<EditOutlined />} onClick={() => openDrawer(row)} />
          <Button danger icon={<DeleteOutlined />} onClick={() => Modal.confirm({ title: 'İşçini silmək istəyirsiniz?', okText: 'Sil', cancelText: 'İmtina', onOk: () => removeWorker(row) })} />
        </Space>
      ),
    },
  ]

  return (
    <div className="page-stack">
      <PageTitle
        title="İşçilər"
        subtitle="HR profili, kamera identifikasiyası və payroll-ready davamiyyət saatları"
        extra={<Space wrap><ProjectSelect pageKey="workers" /><Button type="primary" icon={<PlusOutlined />} onClick={() => openDrawer()}>İşçi əlavə et</Button></Space>}
      />

      {backendError && <Alert type="warning" showIcon message="Backend worker məlumatı alınmadı" description="Local demo işçi siyahısı göstərilir. Backend API bərpa olunanda tenant worker-ləri avtomatik yüklənəcək." />}

      <section className="kpi-grid four">
        <KpiCard icon={<UserOutlined />} title="İşçi sayı" value={formatNumber(rowsSource.length)} tone="blue" />
        <KpiCard icon={<TeamOutlined />} title="Aktiv işçi" value={formatNumber(rowsSource.filter((worker) => worker.isCurrentlyActive).length)} tone="green" />
        <KpiCard icon={<UserOutlined />} title="Kamera mənbəli" value={formatNumber(cameraWorkersCount)} tone="purple" />
        <KpiCard icon={<UserOutlined />} title="Risk nəzarəti" value={formatNumber(rowsSource.filter((worker) => worker.riskScore >= 35).length)} tone="orange" />
      </section>

      <section className="table-card">
        <div className="card-heading">
          <h2>İşçi siyahısı</h2>
          <Space wrap>
            <Select value={crewFilter} onChange={setCrewFilter} style={{ minWidth: 190 }} options={[{ value: 'all', label: 'Bütün briqadalar' }, ...Array.from(new Set(rowsSource.map((row) => row.crewName))).map((crew) => ({ value: crew, label: crew }))]} />
            <Select value={roleFilter} onChange={setRoleFilter} style={{ minWidth: 170 }} options={[{ value: 'all', label: 'Bütün rollar' }, ...roleOptions]} />
            <Select value={statusFilter} onChange={setStatusFilter} style={{ minWidth: 140 }} options={[{ value: 'all', label: 'Bütün statuslar' }, { value: 'active', label: 'Aktiv' }, { value: 'inactive', label: 'Passiv' }]} />
            <Select value={riskFilter} onChange={setRiskFilter} style={{ minWidth: 150 }} options={[{ value: 'all', label: 'Bütün risklər' }, { value: 'high', label: 'Riskli' }, { value: 'normal', label: 'Normal' }]} />
          </Space>
        </div>
        <Table rowKey="id" columns={columns} dataSource={rows} loading={loadingBackend} pagination={{ pageSize: 12 }} scroll={{ x: 1680 }} />
      </section>

      <Drawer title={editingWorker ? 'İşçini redaktə et' : 'Yeni işçi'} open={drawerOpen} width={620} onClose={() => setDrawerOpen(false)}>
        <Form
          form={form}
          layout="vertical"
          onFinish={saveWorker}
          onValuesChange={(changed, allValues) => {
            if ((changed.dahuaCardName !== undefined || changed.dahuaUserId !== undefined) && hasCameraIdentity(allValues) && allValues.attendanceSource !== 'Manual') {
              form.setFieldValue('attendanceSource', 'Camera')
            }
          }}
        >
          <Form.Item name="workerName" label="İşçi adı" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="workerExternalId" label="İşçi kodu" rules={[{ required: true }]} extra="Daxili HR/payroll kodudur. Dahua UserID ilə eyni olmaq məcburiyyətində deyil.">
            <Input />
          </Form.Item>
          <Form.Item name="crewId" label="Briqada"><Select allowClear showSearch options={crewOptions} /></Form.Item>
          <Form.Item name="activeWorkItemId" label="Aktiv iş sətri"><Select allowClear showSearch options={itemOptions} /></Form.Item>
          <Space.Compact block>
            <Form.Item name="role" label="Rol" className="form-half"><Input /></Form.Item>
            <Form.Item name="hourlyRate" label="Saatlıq tarif" className="form-half"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
          </Space.Compact>
          <Space.Compact block>
            <Form.Item name="plannedDailyHours" label="Plan gündəlik saat" className="form-half"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
            <Form.Item name="riskScore" label="Risk balı" className="form-half"><InputNumber min={0} max={100} style={{ width: '100%' }} /></Form.Item>
          </Space.Compact>
          <Form.Item name="attendanceSource" label="Kamera mənbəyi"><Select options={[{ value: 'Camera', label: 'Kamera' }, { value: 'Manual', label: 'Manual' }, { value: 'ForemanTablet', label: 'Qarışıq / Prorab tablet' }]} /></Form.Item>
          <Form.Item name="status" label="Status"><Select options={[{ value: 'active', label: 'Aktiv' }, { value: 'inactive', label: 'Passiv' }]} /></Form.Item>

          <Divider>Obyekt təyinatı</Divider>
          <Typography.Paragraph type="secondary">
            İşçi seçilmiş obyektlərdə görünəcək. Təyinat boş saxlanarsa, işçi yalnız “Bütün obyektlər” görünüşündə göstərilir.
          </Typography.Paragraph>
          <Form.Item name="assignedSiteIds" label="Obyektlər">
            <Select mode="multiple" allowClear showSearch options={siteOptions} placeholder="Obyekt seçin" />
          </Form.Item>
          <Form.Item shouldUpdate={(prev, current) => prev.assignedSiteIds !== current.assignedSiteIds} noStyle>
            {({ getFieldValue }) => {
              const assignedSiteIds = getFieldValue('assignedSiteIds') ?? []
              return (
                <Form.Item name="primarySiteId" label="Əsas obyekt">
                  <Select allowClear options={siteOptions.filter((site) => assignedSiteIds.includes(site.value))} placeholder="Əsas obyekti seçin" />
                </Form.Item>
              )
            }}
          </Form.Item>

          <Divider>Kamera identifikasiyası</Divider>
          <Typography.Paragraph type="secondary">
            Kamera işçini CardName/UserID ilə tanıyır. Bu dəyərlər işçinin daxili kodundan fərqli ola bilər.
          </Typography.Paragraph>
          <Form.Item name="dahuaCardName" label="Dahua CardName"><Input placeholder="məsələn: ilham" /></Form.Item>
          <Form.Item name="dahuaUserId" label="Dahua UserID"><Input placeholder="məsələn: 1" /></Form.Item>
          <Form.Item name="cameraDeviceId" label="Kamera cihazı"><Select options={deviceOptions} /></Form.Item>
          <Space wrap style={{ marginBottom: 16 }}>
            <Button icon={<LinkOutlined />} onClick={testMapping}>Test mapping</Button>
            <Button disabled={!editingWorker?.isBackend} onClick={remapWorker}>Keçmiş kamera qeydlərini bu işçiyə bağla</Button>
          </Space>

          <Form.Item name="notes" label="Qeyd"><Input.TextArea rows={3} /></Form.Item>
          <Button type="primary" htmlType="submit" block>Yadda saxla</Button>
        </Form>
      </Drawer>
    </div>
  )
}
