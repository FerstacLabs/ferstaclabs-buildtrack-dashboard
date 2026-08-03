import { CopyOutlined, KeyOutlined, ReloadOutlined, SafetyCertificateOutlined } from '@ant-design/icons'
import { Alert, Button, DatePicker, Form, InputNumber, Modal, Result, Select, Space, Table, Tag, message } from 'antd'
import type { TableColumnsType } from 'antd'
import type { Dayjs } from 'dayjs'
import { useEffect, useMemo, useState } from 'react'
import { PageTitle } from '../../components/ui/PageTitle'
import { ToolbarButton } from '../../components/ui/ToolbarButton'
import {
  BackendApiError,
  buildTrackBackendApi,
  type AdminTenantLicenseRow,
  type LicensePlan,
  type LicenseStatus,
} from '../../services/api/buildTrackBackendApi'
import { useAuthStore } from '../auth/authStore'

type LicenseFormValues = {
  tenantId: string
  plan: LicensePlan
  expiresAt?: Dayjs
  maxProjects?: number
  maxUsers?: number
  maxCameras?: number
}

const planOptions: LicensePlan[] = ['Starter', 'Business', 'Enterprise', 'Unlimited']

const statusColor: Record<LicenseStatus, string> = {
  Pending: 'orange',
  Active: 'green',
  Expired: 'red',
  Revoked: 'red',
}

const formatDate = (value?: string) => value ? new Date(value).toLocaleDateString('az-AZ') : '-'

const cleanAdminError = (error: unknown) => {
  if (error instanceof BackendApiError) {
    if (error.status === 403) return 'Bu səhifə yalnız FerstacLabs admin hesabı üçün aktivdir.'
    if (error.details.includes('Tenant was not found')) return 'Şirkət hesabı tapılmadı.'
    if (error.details.includes('License was not found')) return 'Aktivləşdiriləcək lisenziya tapılmadı.'
    return 'Lisenziya əməliyyatı alınmadı. Yenidən yoxlayın.'
  }

  return error instanceof Error ? error.message : 'Əməliyyat alınmadı.'
}

export const AdminLicensesPage = () => {
  const { tenant, user } = useAuthStore()
  const [form] = Form.useForm<LicenseFormValues>()
  const [rows, setRows] = useState<AdminTenantLicenseRow[]>([])
  const [loading, setLoading] = useState(false)
  const [rawLicenseKey, setRawLicenseKey] = useState('')

  const isAdmin = tenant?.code === 'DEMO' && (user?.role === 'Owner' || user?.role === 'Admin')

  const loadRows = async () => {
    if (!isAdmin) return
    setLoading(true)
    try {
      const nextRows = await buildTrackBackendApi.getAdminLicenses()
      setRows(nextRows)
      if (!form.getFieldValue('tenantId') && nextRows[0]) form.setFieldValue('tenantId', nextRows[0].tenantId)
    } catch (error) {
      message.error(cleanAdminError(error))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void loadRows()
  }, [isAdmin])

  const tenantOptions = useMemo(
    () => rows.map((row) => ({ value: row.tenantId, label: `${row.companyName}${row.ownerEmail ? ` (${row.ownerEmail})` : ''}` })),
    [rows],
  )

  const createLicense = async (values: LicenseFormValues) => {
    setLoading(true)
    try {
      const response = await buildTrackBackendApi.createAdminLicense({
        tenantId: values.tenantId,
        plan: values.plan,
        expiresAt: values.expiresAt?.endOf('day').toISOString(),
        maxProjects: values.maxProjects,
        maxUsers: values.maxUsers,
        maxCameras: values.maxCameras,
      })
      setRawLicenseKey(response.licenseKey)
      message.success('Lisenziya açarı yaradıldı')
      await loadRows()
    } catch (error) {
      message.error(cleanAdminError(error))
    } finally {
      setLoading(false)
    }
  }

  const activateTenant = async (row: AdminTenantLicenseRow) => {
    setLoading(true)
    try {
      await buildTrackBackendApi.activateTenantLicense(row.tenantId, row.licenseId)
      message.success('Lisenziya birbaşa aktivləşdirildi')
      await loadRows()
    } catch (error) {
      message.error(cleanAdminError(error))
    } finally {
      setLoading(false)
    }
  }

  const copyRawKey = async () => {
    await navigator.clipboard.writeText(rawLicenseKey)
    message.success('Lisenziya açarı kopyalandı')
  }

  if (!isAdmin) {
    return (
      <Result
        status="403"
        title="Admin icazəsi tələb olunur"
        subTitle="Bu səhifə yalnız FerstacLabs Demo tenantında Owner/Admin rolları üçün açıqdır."
      />
    )
  }

  const columns: TableColumnsType<AdminTenantLicenseRow> = [
    { title: 'Şirkət', dataIndex: 'companyName', sorter: (a, b) => a.companyName.localeCompare(b.companyName) },
    { title: 'Owner email', dataIndex: 'ownerEmail', render: (value) => value ?? '-' },
    { title: 'Tenant statusu', dataIndex: 'tenantStatus', render: (value) => <Tag color={value === 'Active' ? 'green' : 'red'}>{value}</Tag> },
    { title: 'Plan', dataIndex: 'licensePlan', render: (value) => value ?? '-' },
    { title: 'Lisenziya statusu', dataIndex: 'licenseStatus', render: (value?: LicenseStatus) => value ? <Tag color={statusColor[value]}>{value}</Tag> : '-' },
    { title: 'Bitmə tarixi', dataIndex: 'expiresAt', render: formatDate },
    { title: 'Max layihə', dataIndex: 'maxProjects', render: (value) => value ?? '-' },
    { title: 'Max istifadəçi', dataIndex: 'maxUsers', render: (value) => value ?? '-' },
    { title: 'Max kamera', dataIndex: 'maxCameras', render: (value) => value ?? '-' },
    { title: 'Yaradılıb', dataIndex: 'createdAt', render: formatDate },
    {
      title: 'Əməliyyat',
      key: 'actions',
      fixed: 'right',
      render: (_, row) => (
        <Button size="small" icon={<SafetyCertificateOutlined />} disabled={!row.licenseId} onClick={() => void activateTenant(row)}>
          Birbaşa aktiv et
        </Button>
      ),
    },
  ]

  return (
    <div className="page-stack">
      <PageTitle title="Admin lisenziyaları" subtitle="Tenant lisenziyalarını yaratmaq və demo onboarding üçün aktivləşdirmək" />

      <section className="integration-grid">
        <section className="table-card">
          <div className="card-heading">
            <h2>Tenant və lisenziya siyahısı</h2>
            <ToolbarButton icon={<ReloadOutlined />} onClick={loadRows}>Yenilə</ToolbarButton>
          </div>
          <Table<AdminTenantLicenseRow>
            columns={columns}
            dataSource={rows}
            loading={loading}
            rowKey="tenantId"
            pagination={{ pageSize: 10 }}
            scroll={{ x: 'max-content' }}
            locale={{ emptyText: 'Tenant tapılmadı' }}
          />
        </section>

        <aside className="panel-card builder-panel">
          <h2>Yeni lisenziya açarı</h2>
          <Alert
            type="warning"
            showIcon
            message="Raw açar yalnız bir dəfə göstərilir"
            description="Açar yaradıldıqdan sonra modalda görünəcək. İndi kopyalayın və müştəriyə göndərin."
          />
          <Form<LicenseFormValues>
            form={form}
            layout="vertical"
            initialValues={{ plan: 'Business', maxProjects: 3, maxUsers: 25, maxCameras: 5 }}
            onFinish={createLicense}
          >
            <Form.Item label="Şirkət hesabı" name="tenantId" rules={[{ required: true, message: 'Tenant seçin' }]}>
              <Select options={tenantOptions} showSearch optionFilterProp="label" placeholder="Tenant seçin" />
            </Form.Item>
            <Form.Item label="Plan" name="plan" rules={[{ required: true, message: 'Plan seçin' }]}>
              <Select options={planOptions.map((plan) => ({ value: plan, label: plan }))} />
            </Form.Item>
            <Form.Item label="Bitmə tarixi" name="expiresAt">
              <DatePicker style={{ width: '100%' }} />
            </Form.Item>
            <Form.Item label="Max layihə" name="maxProjects"><InputNumber min={1} style={{ width: '100%' }} /></Form.Item>
            <Form.Item label="Max istifadəçi" name="maxUsers"><InputNumber min={1} style={{ width: '100%' }} /></Form.Item>
            <Form.Item label="Max kamera" name="maxCameras"><InputNumber min={1} style={{ width: '100%' }} /></Form.Item>
            <Button type="primary" htmlType="submit" icon={<KeyOutlined />} loading={loading} block>
              Lisenziya açarı yarat
            </Button>
          </Form>
        </aside>
      </section>

      <Modal
        title="Yeni lisenziya açarı"
        open={Boolean(rawLicenseKey)}
        onCancel={() => setRawLicenseKey('')}
        footer={<Button type="primary" onClick={() => setRawLicenseKey('')}>Bağla</Button>}
      >
        <Alert
          type="warning"
          showIcon
          message="Bu açar yalnız bir dəfə göstərilir. İndi kopyalayın."
          className="auth-alert"
        />
        <Space.Compact className="full-width">
          <Button className="license-key-display">{rawLicenseKey}</Button>
          <Button icon={<CopyOutlined />} onClick={() => void copyRawKey()}>Kopyala</Button>
        </Space.Compact>
      </Modal>
    </div>
  )
}
