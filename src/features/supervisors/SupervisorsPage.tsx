import { PlusOutlined, ReloadOutlined } from '@ant-design/icons'
import { Button, Card, Drawer, Form, Input, Select, Space, Table, Tag, message } from 'antd'
import { useEffect, useMemo, useState } from 'react'
import {
  buildTrackBackendApi,
  type BackendSite,
  type FieldDailyReport,
  type FieldWarehouseRequest,
  type SupervisorAuditEventRow,
  type SupervisorSummary,
} from '../../services/api/buildTrackBackendApi'
import { fieldStatusColor, fieldStatusLabel } from '../fieldPortal/fieldPortalStore'

type SupervisorFormValues = {
  fullName: string
  email: string
  phone?: string
  password?: string
  siteIds: string[]
  status?: 'Active' | 'Disabled'
}

export const SupervisorsPage = () => {
  const [rows, setRows] = useState<SupervisorSummary[]>([])
  const [sites, setSites] = useState<BackendSite[]>([])
  const [reports, setReports] = useState<FieldDailyReport[]>([])
  const [warehouseRequests, setWarehouseRequests] = useState<FieldWarehouseRequest[]>([])
  const [auditEvents, setAuditEvents] = useState<SupervisorAuditEventRow[]>([])
  const [editing, setEditing] = useState<SupervisorSummary>()
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [loading, setLoading] = useState(false)
  const [form] = Form.useForm<SupervisorFormValues>()

  const load = async () => {
    setLoading(true)
    try {
      const [nextRows, nextSites, nextReports, nextWarehouseRequests, nextAuditEvents] = await Promise.all([
        buildTrackBackendApi.getSupervisors(),
        buildTrackBackendApi.getSites(),
        buildTrackBackendApi.getManagementFieldReports(),
        buildTrackBackendApi.getManagementWarehouseRequests(),
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

  useEffect(() => {
    void load()
  }, [])

  const siteOptions = useMemo(() => sites.map((site) => ({ value: site.id, label: site.name })), [sites])

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
    const reviewNote = status === 'Approved' ? 'Təsdiqləndi' : window.prompt('Rəhbər qeydi') || undefined
    await buildTrackBackendApi.reviewManagementFieldReport(row.id, { status, reviewNote })
    message.success('Hesabat statusu yeniləndi')
    await load()
  }

  const reviewWarehouse = async (row: FieldWarehouseRequest, status: 'Approved' | 'NeedsJustification' | 'Rejected' | 'Issued') => {
    const managerNote = status === 'Approved' ? 'Təsdiqləndi' : window.prompt('Rəhbər qeydi') || undefined
    await buildTrackBackendApi.reviewManagementWarehouseRequest(row.id, { status, managerNote })
    message.success('Anbar sorğusu yeniləndi')
    await load()
  }

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
            { title: 'Status', dataIndex: 'status', render: (status) => <Tag color={fieldStatusColor(status)}>{fieldStatusLabel(status)}</Tag> },
            {
              title: 'Əməliyyat',
              render: (_, row) => (
                <Space wrap>
                  <Button disabled={row.status !== 'Submitted'} onClick={() => reviewReport(row, 'Approved')}>Təsdiq</Button>
                  <Button disabled={row.status !== 'Submitted'} onClick={() => reviewReport(row, 'NeedsCorrection')}>Düzəliş</Button>
                  <Button danger disabled={row.status !== 'Submitted'} onClick={() => reviewReport(row, 'Rejected')}>Rədd</Button>
                </Space>
              ),
            },
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
            { title: 'Material', dataIndex: 'materialName' },
            { title: 'Miqdar', render: (_, row) => `${row.requestedQuantity} ${row.unit}` },
            { title: 'Obyekt', dataIndex: 'siteName' },
            { title: 'Prorab', dataIndex: 'supervisorName' },
            { title: 'Status', dataIndex: 'status', render: (status) => <Tag color={fieldStatusColor(status)}>{fieldStatusLabel(status)}</Tag> },
            {
              title: 'Əməliyyat',
              render: (_, row) => (
                <Space wrap>
                  <Button onClick={() => reviewWarehouse(row, 'Approved')}>Təsdiq</Button>
                  <Button onClick={() => reviewWarehouse(row, 'NeedsJustification')}>Əsaslandır</Button>
                  <Button onClick={() => reviewWarehouse(row, 'Issued')}>Verildi</Button>
                  <Button danger onClick={() => reviewWarehouse(row, 'Rejected')}>Rədd</Button>
                </Space>
              ),
            },
          ]}
        />
      </Card>
      <Card className="soft-card" title="Supervisor audit axını">
        <Table
          rowKey="id"
          loading={loading}
          dataSource={auditEvents}
          pagination={{ pageSize: 6 }}
          columns={[
            { title: 'Vaxt', dataIndex: 'timestamp', render: (value) => new Date(value).toLocaleString('az-AZ') },
            { title: 'Obyekt', dataIndex: 'siteName' },
            { title: 'Prorab', dataIndex: 'supervisorName' },
            { title: 'Hadisə', dataIndex: 'eventType' },
            { title: 'Yoxlama', dataIndex: 'requiresManagerReview', render: (value) => value ? <Tag color="orange">Baxılmalıdır</Tag> : <Tag color="green">Normal</Tag> },
            { title: 'Qeyd', dataIndex: 'message' },
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
    </div>
  )
}
