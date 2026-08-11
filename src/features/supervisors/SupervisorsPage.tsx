import { EyeOutlined, PlusOutlined, ReloadOutlined } from '@ant-design/icons'
import { Button, Card, DatePicker, Descriptions, Drawer, Form, Input, Modal, Select, Space, Table, Tag, message } from 'antd'
import type { Dayjs } from 'dayjs'
import { useEffect, useMemo, useState } from 'react'
import {
  buildTrackBackendApi,
  type BackendSite,
  type FieldDailyReport,
  type FieldDailyReportLine,
  type FieldDailyReportStatus,
  type FieldWarehouseRequestStatus,
  type ManagementWarehouseRequest,
  type SupervisorAuditEventRow,
  type SupervisorSummary,
} from '../../services/api/buildTrackBackendApi'
import { priorityLabel } from '../../utils/warehouseWorkflowLabels'
import { FieldStatusTag } from '../fieldPortal/FieldStatusTag'
import { fieldStatusLabel } from '../fieldPortal/fieldPortalStore'

type SupervisorFormValues = {
  fullName: string
  email: string
  phone?: string
  password?: string
  siteIds: string[]
  status?: 'Active' | 'Disabled'
}

const DASH = '—'

const isNumber = (value: unknown) => value !== null && value !== undefined && Number.isFinite(Number(value))

const formatNumber = (value: unknown) => (isNumber(value) ? Number(value).toLocaleString('az-AZ') : DASH)

const formatHours = (value: unknown) => (isNumber(value) ? `${Number(value).toLocaleString('az-AZ')} saat` : DASH)

const formatDateTime = (value?: string) => (value ? new Date(value).toLocaleString('az-AZ') : DASH)

const totalLineValue = (lines: FieldDailyReportLine[], key: 'workerCount' | 'workHours') => {
  const values = lines.map((line) => Number(line[key])).filter(Number.isFinite)
  if (!values.length) return undefined
  return values.reduce((sum, value) => sum + value, 0)
}

const actionLabel = (action?: string) => {
  const labels: Record<string, string> = {
    DailyReportCreated: 'Gündəlik hesabat yaradıldı',
    DailyReportUpdated: 'Gündəlik hesabat yeniləndi',
    DailyReportSubmitted: 'Gündəlik hesabat göndərildi',
    DailyReportApproved: 'Gündəlik hesabat təsdiqləndi',
    DailyReportNeedsCorrection: 'Düzəliş tələb olundu',
    DailyReportRejected: 'Gündəlik hesabat rədd edildi',
    WarehouseRequestCreated: 'Anbar sorğusu yaradıldı',
    WarehouseRequestApproved: 'Anbar sorğusu təsdiqləndi',
    WarehouseRequestNeedsJustification: 'Əsaslandırma tələb olundu',
    WarehouseRequestRejected: 'Anbar sorğusu rədd edildi',
    WarehouseRequestIssued: 'Anbar sorğusu verildi',
    SiteNoteCreated: 'Sahə qeydi əlavə edildi',
    AssignmentChanged: 'Prorab təyinatı dəyişdirildi',
    SupervisorPasswordReset: 'Prorab şifrəsi yeniləndi',
  }
  const value = action?.trim()
  return value ? labels[value] ?? value : 'Məlumat mövcud deyil'
}

const moduleLabel = (entityType?: string) => {
  const labels: Record<string, string> = {
    SupervisorDailyReport: 'Gündəlik hesabat',
    FieldWarehouseRequest: 'Anbar sorğusu',
    SupervisorSiteNote: 'Sahə qeydi',
    SupervisorWorkerEvent: 'İşçi hadisəsi',
    AppUser: 'Prorab idarəetməsi',
  }
  const value = entityType?.trim()
  return value ? labels[value] ?? value : DASH
}

const decisionLabel = (status: FieldDailyReportStatus) => {
  if (status === 'Approved') return 'Təsdiqlənib'
  if (status === 'NeedsCorrection') return 'Düzəliş tələb olunur'
  if (status === 'Rejected') return 'Rədd edilib'
  return fieldStatusLabel(status)
}

const replaceReport = (items: FieldDailyReport[], updated: FieldDailyReport) =>
  items.map((item) => (item.id === updated.id ? updated : item))

type WarehouseReviewAction = 'Approved' | 'NeedsJustification' | 'Rejected' | 'Issued'

type WarehouseDecisionFormValues = {
  note: string
}

const warehouseTerminalStatuses = new Set<FieldWarehouseRequestStatus>(['Rejected', 'Issued', 'Closed', 'Cancelled'])

const canApproveWarehouse = (row: ManagementWarehouseRequest) =>
  ['PendingApproval', 'Approved'].includes(row.status)

const canRequestWarehouseJustification = (row: ManagementWarehouseRequest) =>
  ['Draft', 'Submitted', 'UnderReview', 'PendingApproval'].includes(row.status)

const canRejectWarehouse = (row: ManagementWarehouseRequest) =>
  !warehouseTerminalStatuses.has(row.status)

const canIssueWarehouse = (row: ManagementWarehouseRequest) =>
  row.status === 'ReadyForPickup' && row.lines.every((line) => line.reservedQuantity >= line.requestedQuantity)

const warehouseMaterialSummary = (row: ManagementWarehouseRequest) => {
  const linesWithName = row.lines.filter((line) => line.itemName?.trim())
  if (!linesWithName.length) return 'Məlumat mövcud deyil'
  if (linesWithName.length === 1) return linesWithName[0].itemName
  return `${linesWithName[0].itemName} +${linesWithName.length - 1}`
}

const warehouseActionMessage: Record<WarehouseReviewAction, string> = {
  Approved: 'Anbar sorğusu təsdiqləndi və stok yoxlanıldı',
  NeedsJustification: 'Əsaslandırma tələbi göndərildi',
  Rejected: 'Anbar sorğusu rədd edildi',
  Issued: 'Material verildi',
}

export const SupervisorsPage = () => {
  const [rows, setRows] = useState<SupervisorSummary[]>([])
  const [sites, setSites] = useState<BackendSite[]>([])
  const [reports, setReports] = useState<FieldDailyReport[]>([])
  const [warehouseRequests, setWarehouseRequests] = useState<ManagementWarehouseRequest[]>([])
  const [auditEvents, setAuditEvents] = useState<SupervisorAuditEventRow[]>([])
  const [editing, setEditing] = useState<SupervisorSummary>()
  const [selectedReport, setSelectedReport] = useState<FieldDailyReport | null>(null)
  const [selectedWarehouseRequest, setSelectedWarehouseRequest] = useState<ManagementWarehouseRequest | null>(null)
  const [selectedAudit, setSelectedAudit] = useState<SupervisorAuditEventRow | null>(null)
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [reportDrawerOpen, setReportDrawerOpen] = useState(false)
  const [warehouseDrawerOpen, setWarehouseDrawerOpen] = useState(false)
  const [auditDrawerOpen, setAuditDrawerOpen] = useState(false)
  const [warehouseDecision, setWarehouseDecision] = useState<{ row: ManagementWarehouseRequest; status: Extract<WarehouseReviewAction, 'NeedsJustification' | 'Rejected'> } | null>(null)
  const [loading, setLoading] = useState(false)
  const [reviewingReportId, setReviewingReportId] = useState<string | null>(null)
  const [reviewingWarehouseId, setReviewingWarehouseId] = useState<string | null>(null)
  const [auditDateRange, setAuditDateRange] = useState<[Dayjs, Dayjs] | null>(null)
  const [auditSiteId, setAuditSiteId] = useState<string>()
  const [auditSupervisorId, setAuditSupervisorId] = useState<string>()
  const [auditAction, setAuditAction] = useState<string>()
  const [form] = Form.useForm<SupervisorFormValues>()
  const [warehouseDecisionForm] = Form.useForm<WarehouseDecisionFormValues>()

  const load = async () => {
    setLoading(true)
    try {
      const [nextRows, nextSites, nextReports, nextWarehouseRequests, nextAuditEvents] = await Promise.all([
        buildTrackBackendApi.getSupervisors(),
        buildTrackBackendApi.getSites(),
        buildTrackBackendApi.getManagementFieldReports(),
        buildTrackBackendApi.getProcurementWarehouseRequests(),
        buildTrackBackendApi.getSupervisorAuditEvents(),
      ])
      setRows(nextRows)
      setSites(nextSites)
      setReports(nextReports)
      setWarehouseRequests(nextWarehouseRequests)
      setAuditEvents(nextAuditEvents)
    } finally {
      setLoading(false)
    }
  }

  const refreshManagementReports = async (selectedReportId?: string) => {
    const nextReports = await buildTrackBackendApi.getManagementFieldReports()
    setReports(nextReports)
    if (selectedReportId) {
      const freshReport = nextReports.find((report) => report.id === selectedReportId)
      if (freshReport && selectedReport?.id === selectedReportId) setSelectedReport(freshReport)
    }
    return nextReports
  }

  const refreshManagementWarehouseRequests = async (selectedRequestId?: string) => {
    const nextRequests = await buildTrackBackendApi.getProcurementWarehouseRequests()
    setWarehouseRequests(nextRequests)
    if (selectedRequestId) {
      const freshRequest = nextRequests.find((request) => request.id === selectedRequestId)
      if (freshRequest && selectedWarehouseRequest?.id === selectedRequestId) setSelectedWarehouseRequest(freshRequest)
    }
    return nextRequests
  }

  useEffect(() => {
    void load()
  }, [])

  const siteOptions = useMemo(() => sites.map((site) => ({ value: site.id, label: site.name })), [sites])

  const supervisorOptions = useMemo(
    () => rows.map((row) => ({ value: row.id, label: row.fullName })),
    [rows],
  )

  const auditActionOptions = useMemo(() => {
    const actions = Array.from(new Set(auditEvents.map((event) => event.action ?? event.eventType).filter(Boolean) as string[]))
    return actions.map((action) => ({ value: action, label: actionLabel(action) }))
  }, [auditEvents])

  const filteredAuditEvents = useMemo(() => auditEvents.filter((event) => {
    if (auditSiteId && event.siteId !== auditSiteId) return false
    if (auditSupervisorId && event.supervisorUserId !== auditSupervisorId) return false
    const action = event.action ?? event.eventType
    if (auditAction && action !== auditAction) return false
    if (auditDateRange) {
      const timestamp = new Date(event.timestamp).getTime()
      if (timestamp < auditDateRange[0].startOf('day').valueOf() || timestamp > auditDateRange[1].endOf('day').valueOf()) return false
    }
    return true
  }), [auditAction, auditDateRange, auditEvents, auditSiteId, auditSupervisorId])

  const openNew = () => {
    setEditing(undefined)
    form.resetFields()
    form.setFieldsValue({ siteIds: [] })
    setDrawerOpen(true)
  }

  const openEdit = (row: SupervisorSummary) => {
    setEditing(row)
    form.setFieldsValue({
      fullName: row.fullName,
      email: row.email,
      phone: row.phone,
      status: row.status,
      siteIds: row.assignments.map((assignment) => assignment.siteId),
    })
    setDrawerOpen(true)
  }

  const openReport = (row: FieldDailyReport) => {
    setSelectedReport(row)
    setReportDrawerOpen(true)
  }

  const openWarehouseRequest = (row: ManagementWarehouseRequest) => {
    setSelectedWarehouseRequest(row)
    setWarehouseDrawerOpen(true)
  }

  const openAudit = (row: SupervisorAuditEventRow) => {
    setSelectedAudit(row)
    setAuditDrawerOpen(true)
  }

  const save = async (values: SupervisorFormValues) => {
    if (editing) {
      await buildTrackBackendApi.updateSupervisor(editing.id, {
        fullName: values.fullName,
        phone: values.phone,
        siteIds: values.siteIds,
        status: values.status ?? editing.status,
      })
      message.success('Prorab məlumatları yeniləndi')
    } else {
      if (!values.password) {
        message.error('İlkin şifrə daxil edin')
        return
      }
      await buildTrackBackendApi.createSupervisor({
        fullName: values.fullName,
        email: values.email,
        phone: values.phone,
        password: values.password,
        siteIds: values.siteIds,
      })
      message.success('Prorab yaradıldı')
    }
    setDrawerOpen(false)
    await load()
  }

  const resetPassword = async (row: SupervisorSummary) => {
    const password = window.prompt(`${row.fullName} üçün yeni şifrə`)
    if (!password) return
    await buildTrackBackendApi.resetSupervisorPassword(row.id, password)
    message.success('Şifrə yeniləndi')
  }

  const reviewReport = async (row: FieldDailyReport, status: 'Approved' | 'NeedsCorrection' | 'Rejected') => {
    if (reviewingReportId) return
    const reviewNote = status === 'Approved'
      ? 'Təsdiqləndi'
      : window.prompt(status === 'NeedsCorrection' ? 'Düzəliş səbəbi' : 'Rədd səbəbi')?.trim()
    if (status !== 'Approved' && !reviewNote) {
      message.warning('Rəhbər qeydi daxil edilməlidir')
      return
    }
    setReviewingReportId(row.id)
    try {
      const updated = await buildTrackBackendApi.reviewManagementFieldReport(row.id, { status, reviewNote })
      setReports((items) => replaceReport(items, updated))
      if (selectedReport?.id === updated.id) setSelectedReport(updated)
      message.success('Hesabat statusu yeniləndi')
      await refreshManagementReports(updated.id)
    } catch (error) {
      message.error(error instanceof Error ? error.message : 'Hesabat statusu yenilənmədi')
    } finally {
      setReviewingReportId(null)
    }
  }

  const reviewWarehouse = async (row: ManagementWarehouseRequest, status: WarehouseReviewAction) => {
    if (reviewingWarehouseId) return
    const managerNote = status === 'Approved'
      ? 'Anbar sorğusu təsdiqləndi. Sistem stok yoxlaması və rezerv prosesini avtomatik icra etdi.'
      : status === 'Issued'
        ? 'Management paneldən verildi'
        : undefined

    setReviewingWarehouseId(row.id)
    try {
      if (status === 'Approved') {
        await buildTrackBackendApi.approveProcurementWarehouseRequest(row.id, managerNote)
      } else if (status === 'Issued') {
        await buildTrackBackendApi.issueProcurementWarehouseRequest(row.id, { recipientName: row.supervisorName ?? 'Prorab', handoverNote: managerNote })
      } else {
        await buildTrackBackendApi.reviewManagementWarehouseRequest(row.id, { status, managerNote })
      }
      message.success(warehouseActionMessage[status])
      await refreshManagementWarehouseRequests(row.id)
    } catch (error) {
      message.error(error instanceof Error ? error.message : 'Anbar sorğusu yenilənmədi')
    } finally {
      setReviewingWarehouseId(null)
    }
  }

  const openWarehouseDecision = (row: ManagementWarehouseRequest, status: Extract<WarehouseReviewAction, 'NeedsJustification' | 'Rejected'>) => {
    warehouseDecisionForm.resetFields()
    setWarehouseDecision({ row, status })
  }

  const submitWarehouseDecision = async (values: WarehouseDecisionFormValues) => {
    if (!warehouseDecision || reviewingWarehouseId) return
    const note = values.note?.trim()
    if (!note) {
      message.warning('Rəhbər qeydi daxil edilməlidir')
      return
    }

    setReviewingWarehouseId(warehouseDecision.row.id)
    try {
      await buildTrackBackendApi.reviewManagementWarehouseRequest(warehouseDecision.row.id, {
        status: warehouseDecision.status,
        managerNote: note,
      })
      message.success(warehouseActionMessage[warehouseDecision.status])
      const requestId = warehouseDecision.row.id
      setWarehouseDecision(null)
      warehouseDecisionForm.resetFields()
      await refreshManagementWarehouseRequests(requestId)
    } catch (error) {
      message.error(error instanceof Error ? error.message : 'Anbar sorğusu yenilənmədi')
    } finally {
      setReviewingWarehouseId(null)
    }
  }

  const reportActions = (row: FieldDailyReport) => (
    <Space wrap>
      <Button icon={<EyeOutlined />} onClick={() => openReport(row)}>Bax</Button>
      <Button loading={reviewingReportId === row.id} disabled={row.status !== 'Submitted' || Boolean(reviewingReportId)} onClick={() => reviewReport(row, 'Approved')}>Təsdiq et</Button>
      <Button loading={reviewingReportId === row.id} disabled={row.status !== 'Submitted' || Boolean(reviewingReportId)} onClick={() => reviewReport(row, 'NeedsCorrection')}>Düzəliş tələb et</Button>
      <Button danger loading={reviewingReportId === row.id} disabled={row.status !== 'Submitted' || Boolean(reviewingReportId)} onClick={() => reviewReport(row, 'Rejected')}>Rədd et</Button>
    </Space>
  )

  const warehouseActions = (row: ManagementWarehouseRequest) => (
    <Space wrap>
      <Button icon={<EyeOutlined />} onClick={() => openWarehouseRequest(row)}>Bax</Button>
      {canApproveWarehouse(row) && (
        <Button loading={reviewingWarehouseId === row.id} disabled={Boolean(reviewingWarehouseId)} onClick={() => reviewWarehouse(row, 'Approved')}>Təsdiq</Button>
      )}
      {canRequestWarehouseJustification(row) && (
        <Button loading={reviewingWarehouseId === row.id} disabled={Boolean(reviewingWarehouseId)} onClick={() => openWarehouseDecision(row, 'NeedsJustification')}>Əsaslandırma tələb et</Button>
      )}
      {canIssueWarehouse(row) && (
        <Button loading={reviewingWarehouseId === row.id} disabled={Boolean(reviewingWarehouseId)} onClick={() => reviewWarehouse(row, 'Issued')}>Verildi</Button>
      )}
      {canRejectWarehouse(row) && (
        <Button danger loading={reviewingWarehouseId === row.id} disabled={Boolean(reviewingWarehouseId)} onClick={() => openWarehouseDecision(row, 'Rejected')}>Rədd</Button>
      )}
    </Space>
  )

  const selectedReportTotalHours = selectedReport ? totalLineValue(selectedReport.lines, 'workHours') : undefined
  const selectedReportTotalWorkers = selectedReport ? totalLineValue(selectedReport.lines, 'workerCount') : undefined
  const selectedWarehouseSummary = selectedWarehouseRequest ? {
    lineCount: selectedWarehouseRequest.lines.length,
    materialCount: new Set(selectedWarehouseRequest.lines.map((line) => line.catalogItemId)).size,
    fullyAvailable: selectedWarehouseRequest.lines.filter((line) => line.shortfallQuantity <= 0).length,
    withShortfall: selectedWarehouseRequest.lines.filter((line) => line.shortfallQuantity > 0).length,
  } : undefined

  return (
    <div className="page-stack">
      <div className="field-toolbar">
        <div>
          <span className="field-eyebrow">Field Portal istifadəçiləri</span>
          <h2>Prorablar</h2>
        </div>
        <Space>
          <Button icon={<ReloadOutlined />} onClick={load}>Yenilə</Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={openNew}>Prorab əlavə et</Button>
        </Space>
      </div>
      <Card className="soft-card">
        <Table
          rowKey="id"
          loading={loading}
          dataSource={rows}
          pagination={{ pageSize: 10 }}
          columns={[
            { title: 'Ad Soyad', dataIndex: 'fullName' },
            { title: 'Email', dataIndex: 'email' },
            { title: 'Telefon', dataIndex: 'phone' },
            { title: 'Obyektlər', render: (_, row) => row.assignments.map((assignment) => <Tag key={assignment.siteId}>{assignment.siteName}</Tag>) },
            { title: 'Status', dataIndex: 'status', render: (status) => <Tag color={status === 'Active' ? 'green' : 'red'}>{status === 'Active' ? 'Aktiv' : 'Deaktiv'}</Tag> },
            { title: 'Hesabat', dataIndex: 'pendingReports' },
            { title: 'Anbar sorğusu', dataIndex: 'openWarehouseRequests' },
            {
              title: 'Əməliyyat',
              render: (_, row) => (
                <Space wrap>
                  <Button onClick={() => openEdit(row)}>Redaktə</Button>
                  <Button onClick={() => resetPassword(row)}>Şifrə</Button>
                  {row.status === 'Active'
                    ? <Button danger onClick={async () => { await buildTrackBackendApi.suspendSupervisor(row.id); await load() }}>Deaktiv et</Button>
                    : <Button onClick={async () => { await buildTrackBackendApi.reactivateSupervisor(row.id); await load() }}>Aktiv et</Button>}
                </Space>
              ),
            },
          ]}
        />
      </Card>
      <Card className="soft-card" title="Gündəlik hesabat təsdiqi">
        <Table
          rowKey="id"
          loading={loading}
          dataSource={reports}
          pagination={{ pageSize: 5 }}
          columns={[
            { title: 'Tarix', dataIndex: 'reportDate' },
            { title: 'Obyekt', dataIndex: 'siteName' },
            { title: 'Prorab', dataIndex: 'supervisorName' },
            { title: 'Sətir', render: (_, row) => row.lines.length },
            {
              title: 'Status',
              dataIndex: 'status',
              render: (_, row) => <FieldStatusTag key={`${row.id}:${row.status}`} status={row.status} />,
            },
            { title: 'Əməliyyat', render: (_, row) => reportActions(row) },
          ]}
        />
      </Card>
      <Card className="soft-card" title="Anbar sorğuları təsdiqi">
        <Table
          rowKey="id"
          loading={loading}
          dataSource={warehouseRequests}
          pagination={{ pageSize: 5 }}
          columns={[
            { title: 'Sorğu', dataIndex: 'code', render: (value, row) => value || row.id.slice(0, 8) },
            { title: 'Material', render: (_, row) => warehouseMaterialSummary(row) },
            { title: 'Miqdar', render: (_, row) => `${formatNumber(row.totalRequested)} vahid` },
            { title: 'Obyekt', dataIndex: 'siteName' },
            { title: 'Prorab', dataIndex: 'supervisorName' },
            {
              title: 'Status',
              dataIndex: 'status',
              render: (_, row) => <FieldStatusTag key={`${row.id}:${row.status}`} status={row.status} />,
            },
            { title: 'Çatışmazlıq', render: (_, row) => row.totalShortfall > 0 ? <Tag color="red">{formatNumber(row.totalShortfall)}</Tag> : <Tag color="green">Yoxdur</Tag> },
            {
              title: 'Əməliyyat',
              render: (_, row) => warehouseActions(row),
            },
          ]}
        />
      </Card>
      <Card className="soft-card" title="Supervisor audit axını">
        <Space wrap style={{ marginBottom: 16 }}>
          <DatePicker.RangePicker onChange={(range) => setAuditDateRange(range as [Dayjs, Dayjs] | null)} />
          <Select allowClear placeholder="Obyekt" style={{ minWidth: 180 }} options={siteOptions} value={auditSiteId} onChange={setAuditSiteId} />
          <Select allowClear placeholder="Prorab" style={{ minWidth: 180 }} options={supervisorOptions} value={auditSupervisorId} onChange={setAuditSupervisorId} />
          <Select allowClear placeholder="Hadisə / modul" style={{ minWidth: 220 }} options={auditActionOptions} value={auditAction} onChange={setAuditAction} />
        </Space>
        <Table
          rowKey="id"
          loading={loading}
          dataSource={filteredAuditEvents}
          pagination={{ pageSize: 6 }}
          columns={[
            { title: 'Vaxt', dataIndex: 'timestamp', render: formatDateTime },
            { title: 'Obyekt', render: (_, row) => row.siteName || DASH },
            { title: 'Prorab', render: (_, row) => row.supervisorName || DASH },
            { title: 'Hadisə', render: (_, row) => actionLabel(row.action ?? row.eventType) },
            { title: 'Bölmə', render: (_, row) => moduleLabel(row.entityType) },
            { title: 'Nəticə', render: (_, row) => row.requiresManagerReview ? <Tag color="orange">Diqqət tələb edir</Tag> : <Tag color="green">Uğurlu</Tag> },
            { title: 'Ətraflı', render: (_, row) => <Button icon={<EyeOutlined />} onClick={() => openAudit(row)}>Bax</Button> },
          ]}
        />
      </Card>

      <Drawer title={editing ? 'Prorab redaktəsi' : 'Yeni prorab'} open={drawerOpen} width={560} onClose={() => setDrawerOpen(false)}>
        <Form layout="vertical" form={form} onFinish={save}>
          <Form.Item name="fullName" label="Ad Soyad" rules={[{ required: true, message: 'Ad Soyad daxil edin' }]}>
            <Input />
          </Form.Item>
          <Form.Item name="email" label="Email" rules={[{ required: true, message: 'Email daxil edin' }]}>
            <Input disabled={Boolean(editing)} />
          </Form.Item>
          {!editing && (
            <Form.Item name="password" label="İlkin şifrə" rules={[{ required: true, message: 'Şifrə daxil edin' }]}>
              <Input.Password />
            </Form.Item>
          )}
          <Form.Item name="phone" label="Telefon">
            <Input />
          </Form.Item>
          <Form.Item name="siteIds" label="Təyin edilən obyektlər" rules={[{ required: true, message: 'Ən azı bir obyekt seçin' }]}>
            <Select mode="multiple" options={siteOptions} />
          </Form.Item>
          {editing && (
            <Form.Item name="status" label="Status">
              <Select options={[
                { value: 'Active', label: 'Aktiv' },
                { value: 'Disabled', label: 'Deaktiv' },
              ]} />
            </Form.Item>
          )}
          <Button type="primary" htmlType="submit">Yadda saxla</Button>
        </Form>
      </Drawer>

      <Drawer
        title="Gündəlik hesabat"
        open={reportDrawerOpen}
        width={860}
        onClose={() => setReportDrawerOpen(false)}
        footer={selectedReport?.status === 'Submitted' ? reportActions(selectedReport) : null}
      >
        {selectedReport && (
          <Space direction="vertical" className="full-width" size="middle">
            <Card size="small">
              <Space wrap size="large">
                <div><strong>Sətir sayı:</strong> {selectedReport.lines.length}</div>
                <div><strong>Ümumi iş saatı:</strong> {formatHours(selectedReportTotalHours)}</div>
                <div><strong>Ümumi işçi sayı:</strong> {formatNumber(selectedReportTotalWorkers)}</div>
              </Space>
            </Card>
            <Descriptions bordered size="small" column={1}>
              <Descriptions.Item label="Tarix">{selectedReport.reportDate}</Descriptions.Item>
              <Descriptions.Item label="Obyekt">{selectedReport.siteName || DASH}</Descriptions.Item>
              <Descriptions.Item label="Prorab">{selectedReport.supervisorName || DASH}</Descriptions.Item>
              <Descriptions.Item label="Status"><FieldStatusTag key={`${selectedReport.id}:${selectedReport.status}`} status={selectedReport.status} /></Descriptions.Item>
              <Descriptions.Item label="Hava şəraiti">{selectedReport.weatherCondition || selectedReport.weather || DASH}</Descriptions.Item>
              <Descriptions.Item label="Ümumi qeyd">{selectedReport.generalNote || DASH}</Descriptions.Item>
              <Descriptions.Item label="Göndərilmə vaxtı">{formatDateTime(selectedReport.submittedAt)}</Descriptions.Item>
              {selectedReport.status !== 'Submitted' && selectedReport.status !== 'Draft' && (
                <>
                  <Descriptions.Item label="Qərar">{decisionLabel(selectedReport.status)}</Descriptions.Item>
                  <Descriptions.Item label="Qərarı verən">{selectedReport.reviewedByName || DASH}</Descriptions.Item>
                  <Descriptions.Item label="Qərar vaxtı">{formatDateTime(selectedReport.reviewedAt)}</Descriptions.Item>
                  <Descriptions.Item label="Rəhbər qeydi">{selectedReport.reviewNote || DASH}</Descriptions.Item>
                </>
              )}
            </Descriptions>
            <Table
              size="small"
              rowKey="id"
              dataSource={selectedReport.lines}
              pagination={false}
              columns={[
                { title: 'Etap', dataIndex: 'stageName' },
                { title: 'İş', dataIndex: 'workName' },
                { title: 'Miqdar', render: (_, line) => `${formatNumber(line.reportedQuantity)} ${line.unit || ''}`.trim() },
                { title: 'İşçi sayı', render: (_, line) => formatNumber(line.workerCount) },
                { title: 'Saat', render: (_, line) => formatHours(line.workHours) },
                { title: 'Qeyd', render: (_, line) => line.note || DASH },
              ]}
            />
          </Space>
        )}
      </Drawer>

      <Drawer
        title="Anbar sorğusu"
        open={warehouseDrawerOpen}
        width={920}
        onClose={() => setWarehouseDrawerOpen(false)}
        footer={selectedWarehouseRequest ? warehouseActions(selectedWarehouseRequest) : null}
      >
        {selectedWarehouseRequest && (
          <Space direction="vertical" className="full-width" size="middle">
            {selectedWarehouseSummary && (
              <Card size="small">
                <Space wrap size="large">
                  <div><strong>Material sayı:</strong> {formatNumber(selectedWarehouseSummary.materialCount)}</div>
                  <div><strong>Ümumi sətir sayı:</strong> {formatNumber(selectedWarehouseSummary.lineCount)}</div>
                  <div><strong>Tam təmin edilə bilən:</strong> {formatNumber(selectedWarehouseSummary.fullyAvailable)}</div>
                  <div><strong>Çatışmazlıq olan:</strong> {formatNumber(selectedWarehouseSummary.withShortfall)}</div>
                </Space>
              </Card>
            )}
            <Descriptions bordered size="small" column={1}>
              <Descriptions.Item label="Sorğu nömrəsi">{selectedWarehouseRequest.code || selectedWarehouseRequest.id.slice(0, 8)}</Descriptions.Item>
              <Descriptions.Item label="Obyekt">{selectedWarehouseRequest.siteName || DASH}</Descriptions.Item>
              <Descriptions.Item label="Prorab">{selectedWarehouseRequest.supervisorName || DASH}</Descriptions.Item>
              <Descriptions.Item label="Tarix">{formatDateTime(selectedWarehouseRequest.createdAt)}</Descriptions.Item>
              <Descriptions.Item label="Təcillik">{priorityLabel(selectedWarehouseRequest.urgency)}</Descriptions.Item>
              <Descriptions.Item label="Status"><FieldStatusTag key={`${selectedWarehouseRequest.id}:${selectedWarehouseRequest.status}`} status={selectedWarehouseRequest.status} /></Descriptions.Item>
              <Descriptions.Item label="Ümumi qeyd">{selectedWarehouseRequest.generalNote || DASH}</Descriptions.Item>
              <Descriptions.Item label="Rəhbərin əsaslandırma tələbi">{selectedWarehouseRequest.justificationRequestNote || (selectedWarehouseRequest.status === 'NeedsJustification' ? 'Sistem yoxlamasına görə bu sorğu üçün əlavə əsaslandırma tələb olunur.' : DASH)}</Descriptions.Item>
              <Descriptions.Item label="Prorabın əsaslandırması">{selectedWarehouseRequest.justification || DASH}</Descriptions.Item>
              <Descriptions.Item label="Son qərar qeydi">{selectedWarehouseRequest.managerComment || DASH}</Descriptions.Item>
            </Descriptions>
            <Table
              size="small"
              rowKey="id"
              dataSource={selectedWarehouseRequest.lines}
              pagination={false}
              columns={[
                { title: 'Material', render: (_, line) => line.itemName || 'Məlumat mövcud deyil' },
                { title: 'Kod', render: (_, line) => line.code || DASH },
                { title: 'Miqdar', render: (_, line) => formatNumber(line.requestedQuantity) },
                { title: 'Vahid', dataIndex: 'unit' },
                { title: 'Səbəb', render: (_, line) => line.reason || DASH },
                { title: 'Anbarda', render: (_, line) => `${formatNumber(line.onHandQuantity)} ${line.unit}` },
                { title: 'Rezerv', render: (_, line) => `${formatNumber(line.reservedQuantity)} ${line.unit}` },
                { title: 'Çatışmır', render: (_, line) => line.shortfallQuantity > 0 ? <Tag color="red">{formatNumber(line.shortfallQuantity)} {line.unit}</Tag> : <Tag color="green">Yoxdur</Tag> },
                { title: 'Sətir statusu', render: (_, line) => <FieldStatusTag key={`${line.id}:${line.status}`} status={line.status} /> },
              ]}
            />
          </Space>
        )}
      </Drawer>

      <Drawer title="Audit detalları" open={auditDrawerOpen} width={620} onClose={() => setAuditDrawerOpen(false)}>
        {selectedAudit && (
          <Descriptions bordered size="small" column={1}>
            <Descriptions.Item label="Vaxt">{formatDateTime(selectedAudit.timestamp)}</Descriptions.Item>
            <Descriptions.Item label="Prorab">{selectedAudit.supervisorName || DASH}</Descriptions.Item>
            <Descriptions.Item label="Obyekt">{selectedAudit.siteName || DASH}</Descriptions.Item>
            <Descriptions.Item label="Hadisə">{actionLabel(selectedAudit.action ?? selectedAudit.eventType)}</Descriptions.Item>
            <Descriptions.Item label="Bölmə">{moduleLabel(selectedAudit.entityType)}</Descriptions.Item>
            <Descriptions.Item label="Əməliyyat">{selectedAudit.action || selectedAudit.eventType || DASH}</Descriptions.Item>
            <Descriptions.Item label="Nəticə">{selectedAudit.requiresManagerReview ? 'Diqqət tələb edir' : 'Uğurlu'}</Descriptions.Item>
            <Descriptions.Item label="Related entity ID">{selectedAudit.entityId || DASH}</Descriptions.Item>
            <Descriptions.Item label="Qeyd">{selectedAudit.message || DASH}</Descriptions.Item>
          </Descriptions>
        )}
      </Drawer>

      <Modal
        title={warehouseDecision?.status === 'NeedsJustification' ? 'Əsaslandırma tələb et' : 'Sorğunu rədd et'}
        open={Boolean(warehouseDecision)}
        onCancel={() => setWarehouseDecision(null)}
        footer={null}
        destroyOnHidden
      >
        <Form form={warehouseDecisionForm} layout="vertical" onFinish={submitWarehouseDecision}>
          <Form.Item
            name="note"
            label={warehouseDecision?.status === 'NeedsJustification' ? 'Proraba qeyd / sual' : 'Rədd səbəbi'}
            rules={[{ required: true, message: 'Qeyd daxil edin' }]}
          >
            <Input.TextArea
              rows={4}
              placeholder={warehouseDecision?.status === 'NeedsJustification'
                ? '100 litr materialın hansı iş üçün və niyə bu həcmdə lazım olduğunu izah edin.'
                : 'Sorğunun niyə rədd edildiyini yazın.'}
            />
          </Form.Item>
          <Space className="field-form-actions">
            <Button onClick={() => setWarehouseDecision(null)}>İmtina</Button>
            <Button type="primary" htmlType="submit" loading={Boolean(warehouseDecision && reviewingWarehouseId === warehouseDecision.row.id)}>
              {warehouseDecision?.status === 'NeedsJustification' ? 'Tələb göndər' : 'Rədd et'}
            </Button>
          </Space>
        </Form>
      </Modal>
    </div>
  )
}
