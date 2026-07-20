import { ApiOutlined, CheckCircleOutlined, PlayCircleOutlined, PlusOutlined, ReloadOutlined } from '@ant-design/icons'
import { Alert, Button, Form, Input, InputNumber, Select, Space, Table, Tag, Tooltip, message } from 'antd'
import type { TableColumnsType } from 'antd'
import { useEffect, useMemo, useState } from 'react'
import { PageTitle } from '../../components/ui/PageTitle'
import { ToolbarButton } from '../../components/ui/ToolbarButton'
import {
  BackendApiError,
  buildTrackBackendApi,
  type BackendDevice,
  type BackendSite,
  type DeviceConnectionLog,
  type DeviceMode,
  type DeviceStatus,
  type ListenerStatus,
  type ActiveRegisterStatus,
} from '../../services/api/buildTrackBackendApi'

type DeviceFormValues = {
  siteId: string
  name: string
  model: string
  mode: DeviceMode
  registerDeviceId: string
  registerPort: number
  username: string
  password: string
}

const showDevSimulator = import.meta.env.VITE_SHOW_DEV_SIMULATOR === 'true'

const statusColor: Record<DeviceStatus, string> = {
  Pending: 'orange',
  Online: 'green',
  Offline: 'red',
  Error: 'red',
}

const statusLabel: Record<DeviceStatus, string> = {
  Pending: 'Gözləyir',
  Online: 'Qoşulub',
  Offline: 'Ayrılıb',
  Error: 'Xəta',
}

const modeOptions = [
  { label: 'Active Register', value: 'ActiveRegister' },
  { label: 'CGI polling', value: 'CgiPollingFallback' },
  { label: 'Simulator', value: 'Simulator' },
]

const modeLabel: Record<DeviceMode, string> = {
  ActiveRegister: 'Active Register',
  CgiPollingFallback: 'CGI polling',
  Simulator: 'Simulator',
}

const formatDateTime = (value?: string) => value ? new Date(value).toLocaleString() : '-'

const getActionErrorMessage = (err: unknown) => {
  if (err instanceof BackendApiError) {
    return err.details || `${err.status} status kodu ilə backend sorğusu alınmadı`
  }

  if (err instanceof Error) return err.message
  return 'Əməliyyat alınmadı'
}

export const DevicesPage = () => {
  const [form] = Form.useForm<DeviceFormValues>()
  const [sites, setSites] = useState<BackendSite[]>([])
  const [devices, setDevices] = useState<BackendDevice[]>([])
  const [connectionLogByDeviceId, setConnectionLogByDeviceId] = useState<Record<string, DeviceConnectionLog | undefined>>({})
  const [listenerStatus, setListenerStatus] = useState<ListenerStatus | null>(null)
  const [activeRegisterStatus, setActiveRegisterStatus] = useState<ActiveRegisterStatus | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  const loadData = async () => {
    setLoading(true)
    setError('')
    try {
      const [siteRows, deviceRows, status, activeStatus] = await Promise.all([
        buildTrackBackendApi.getSites(),
        buildTrackBackendApi.getDevices(),
        buildTrackBackendApi.getListenerStatus(),
        buildTrackBackendApi.getActiveRegisterStatus(),
      ])
      setSites(siteRows)
      setDevices(deviceRows)
      setListenerStatus(status)
      setActiveRegisterStatus(activeStatus)
      if (siteRows[0] && !form.getFieldValue('siteId')) form.setFieldValue('siteId', siteRows[0].id)

      const logResults = await Promise.allSettled(deviceRows.map(async (device) => [device.id, (await buildTrackBackendApi.getDeviceLogs(device.id))[0]] as const))
      const latestLogs: Record<string, DeviceConnectionLog | undefined> = {}
      logResults.forEach((result) => {
        if (result.status === 'fulfilled') latestLogs[result.value[0]] = result.value[1]
      })
      setConnectionLogByDeviceId(latestLogs)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Backend ilə əlaqə alınmadı')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void loadData()
  }, [])

  const createDemoSite = async () => {
    setLoading(true)
    try {
      const site = await buildTrackBackendApi.createSite({ name: 'Villa tikintisi', address: 'Bakı, tikinti sahəsi', timeZone: 'Asia/Baku' })
      await buildTrackBackendApi.createWorker({ siteId: site.id, externalWorkerCode: '1', fullName: 'İlham Əliyev', status: 'Active' })
      message.success('Test obyekti və işçi yaradıldı')
      await loadData()
    } catch (err) {
      message.error(getActionErrorMessage(err))
    } finally {
      setLoading(false)
    }
  }

  const createDevice = async (values: DeviceFormValues) => {
    setLoading(true)
    try {
      await buildTrackBackendApi.createDevice({ ...values, vendor: 'dahua' })
      form.setFieldsValue({ registerDeviceId: `BT-${Date.now()}`, password: '' })
      message.success('Dahua cihazı yaradıldı')
      await loadData()
    } catch (err) {
      message.error(getActionErrorMessage(err))
    } finally {
      setLoading(false)
    }
  }

  const runDeviceAction = async (deviceId: string, action: 'ready' | 'register' | 'event') => {
    setLoading(true)
    try {
      if (action === 'ready') await buildTrackBackendApi.markReady(deviceId)
      if (action === 'register') await buildTrackBackendApi.simulateRegister(deviceId)
      if (action === 'event') {
        const event = await buildTrackBackendApi.simulateEvent(deviceId)
        message.success(`Test davamiyyət hadisəsi yaradıldı: ${event.workerName ?? 'test işçi'}`)
        window.dispatchEvent(new CustomEvent('buildtrack:attendance-event-created', { detail: { siteId: event.siteId, deviceId } }))
      } else {
        message.success('Cihaz statusu yeniləndi')
      }
      await loadData()
    } catch (err) {
      if (err instanceof BackendApiError) console.error('Device action failed', { url: err.url, status: err.status, details: err.details })
      message.error(getActionErrorMessage(err))
    } finally {
      setLoading(false)
    }
  }

  const siteNameById = useMemo(() => new Map(sites.map((site) => [site.id, site.name])), [sites])

  const columns: TableColumnsType<BackendDevice> = [
    { title: 'Cihaz', dataIndex: 'name', sorter: (a, b) => a.name.localeCompare(b.name) },
    { title: 'Obyekt', dataIndex: 'siteId', render: (value) => siteNameById.get(value) ?? value },
    { title: 'Model', dataIndex: 'model' },
    { title: 'Rejim', dataIndex: 'mode', render: (value: DeviceMode) => <Tag color="blue">{modeLabel[value] ?? value}</Tag> },
    { title: 'Device ID', dataIndex: 'registerDeviceId' },
    { title: 'Register port', dataIndex: 'registerPort' },
    { title: 'Status', dataIndex: 'status', render: (value: DeviceStatus) => <Tag color={statusColor[value] ?? 'default'}>{statusLabel[value] ?? value}</Tag> },
    { title: 'NetSDK decode', dataIndex: 'netSdkDecodeStatus', render: (value) => <Tag color={value === 'Active' ? 'green' : value === 'Error' ? 'red' : 'orange'}>{value ?? 'Unknown'}</Tag> },
    { title: 'Last seen', dataIndex: 'lastSeenAt', render: formatDateTime },
    { title: 'Last known IP', dataIndex: 'lastKnownIp', render: (value) => value ?? '-' },
    {
      title: 'Last connection log event type',
      key: 'lastConnectionLogEventType',
      render: (_, row) => connectionLogByDeviceId[row.id]?.eventType ?? '-',
    },
    { title: 'Son RecNo', dataIndex: 'lastRecNo', render: (value) => value ?? '-' },
    { title: 'Son hadisə', dataIndex: 'lastEventWorkerName', render: (_, row) => row.lastEventWorkerName ? `${row.lastEventWorkerName} / ${formatDateTime(row.lastEventAt)}` : '-' },
    {
      title: 'Əməliyyat',
      key: 'actions',
      fixed: 'right',
      render: (_, row) => (
        <Space direction="vertical" size={6}>
          <Space wrap>
            <Tooltip title="Cihazı Pending vəziyyətinə salır və real Dahua terminal connection gözləyir.">
              <Button size="small" onClick={() => runDeviceAction(row.id, 'ready')}>Active Register hazır et</Button>
            </Tooltip>
            {showDevSimulator && (
              <>
                <Button size="small" icon={<PlayCircleOutlined />} onClick={() => runDeviceAction(row.id, 'register')}>DEV: Online sim</Button>
                <Button size="small" icon={<CheckCircleOutlined />} onClick={() => runDeviceAction(row.id, 'event')}>DEV: Event sim</Button>
              </>
            )}
          </Space>
          {showDevSimulator && <span className="dev-simulator-note">Bu düymələr real kamera olmadan test üçündür.</span>}
        </Space>
      ),
    },
  ]

  return (
    <div className="page-stack">
      <PageTitle title="Dahua Cihazları və Active Register" subtitle="DHI-ASI6213J-MW terminalını BuildTrack backend-ə qoşmaq üçün cihaz konfiqurasiyası" />

      {error && (
        <Alert
          type="warning"
          showIcon
          message="Backend bağlantısı yoxdur"
        description="Cihaz modulu backend bağlantısı tələb edir. Bağlantı bərpa olunanda siyahı avtomatik yenilənə bilər."
        />
      )}

      {listenerStatus?.warning && <Alert type="info" showIcon message="Dahua NetSDK xəbərdarlığı" description={listenerStatus.warning} />}

      {showDevSimulator && <Alert type="warning" showIcon message="Development simulator enabled" description="DEV simulator düymələri real kamera olmadan test üçündür və yalnız explicit VITE_SHOW_DEV_SIMULATOR=true olduqda görünür." />}

      <section className="integration-grid">
        <section className="table-card">
          <div className="card-heading">
            <h2>Cihaz siyahısı</h2>
            <Space>
              <ToolbarButton icon={<PlusOutlined />} tone="green" onClick={createDemoSite}>Test obyekt yarat</ToolbarButton>
              <ToolbarButton icon={<ReloadOutlined />} onClick={loadData}>Yenilə</ToolbarButton>
            </Space>
          </div>
          <Table<BackendDevice>
            columns={columns}
            dataSource={devices}
            loading={loading}
            rowKey="id"
            pagination={{ pageSize: 8 }}
            scroll={{ x: 'max-content' }}
            locale={{ emptyText: 'Cihaz tapılmadı. Əvvəl test obyekti yaradın və Dahua terminal əlavə edin.' }}
          />
        </section>

        <aside className="panel-card builder-panel">
          <h2>Yeni Dahua cihazı</h2>
          <Form<DeviceFormValues>
            form={form}
            layout="vertical"
            initialValues={{ model: 'DHI-ASI6213J-MW', mode: 'ActiveRegister', registerPort: 9500, username: 'admin', registerDeviceId: `BT-${Date.now()}` }}
            onFinish={createDevice}
          >
            <Form.Item label="Obyekt" name="siteId" rules={[{ required: true, message: 'Obyekt seçin' }]}>
              <Select options={sites.map((site) => ({ label: site.name, value: site.id }))} placeholder="Obyekt seçin" />
            </Form.Item>
            <Form.Item label="Cihaz adı" name="name" rules={[{ required: true, message: 'Cihaz adı yazın' }]}>
              <Input placeholder="Giriş terminalı" />
            </Form.Item>
            <Form.Item label="Model" name="model">
              <Input />
            </Form.Item>
            <Form.Item label="Rejim" name="mode">
              <Select options={modeOptions} />
            </Form.Item>
            <Form.Item label="Register Device ID" name="registerDeviceId" rules={[{ required: true, message: 'Device ID yazın' }]}>
              <Input placeholder="BT-SITE001-ENTRANCE1" />
            </Form.Item>
            <Form.Item label="Register port" name="registerPort">
              <InputNumber min={1} max={65535} style={{ width: '100%' }} />
            </Form.Item>
            <Form.Item label="Admin istifadəçi" name="username" rules={[{ required: true, message: 'İstifadəçi adı yazın' }]}>
              <Input />
            </Form.Item>
            <Form.Item label="Admin şifrə" name="password" rules={[{ required: true, message: 'Şifrə yazın' }]}>
              <Input.Password />
            </Form.Item>
            <Button icon={<ApiOutlined />} type="primary" htmlType="submit" className="toolbar-button toolbar-green" block>Cihaz yarat</Button>
          </Form>
        </aside>
      </section>

      <section className="explanation-grid">
        <aside className="panel-card instruction-panel">
          <h2>Terminalda Active Register</h2>
          <ol>
            <li>Dahua terminalında Connection &gt; Network &gt; Active Register bölməsinə keçin.</li>
            <li>Active Register Enable = ON edin.</li>
            <li>Server IP: BuildTrack backend public IP.</li>
            <li>Port: cihazda göstərilən register port, adətən 9500.</li>
            <li>Device ID: BuildTrack-də yaradılan Register Device ID.</li>
          </ol>
        </aside>
        <aside className="panel-card instruction-panel">
          <h2>Active Register diagnostics</h2>
          <div className="summary-metric"><span>Backend</span><strong>Konfiqurasiya edilib</strong></div>
          <div className="summary-metric"><span>TCP portlar</span><strong>{activeRegisterStatus?.ports?.join(', ') ?? listenerStatus?.ports?.join(', ') ?? '7000, 9500'}</strong></div>
          <div className="summary-metric"><span>Listener</span><strong>{activeRegisterStatus?.listenerActive ? 'Aktiv' : activeRegisterStatus?.enabled ? 'Gözləyir' : 'Söndürülüb'}</strong></div>
          <div className="summary-metric"><span>Son callback</span><strong>{formatDateTime(activeRegisterStatus?.lastCallbackTime)}</strong></div>
          <div className="summary-metric"><span>Son command</span><strong>{activeRegisterStatus?.lastCommand ?? '-'}</strong></div>
          <div className="summary-metric"><span>Payload bytes</span><strong>{activeRegisterStatus?.lastPayloadBytes ?? 0}</strong></div>
          <div className="summary-metric"><span>Raw / decoded / ingested</span><strong>{activeRegisterStatus ? `${activeRegisterStatus.rawEventCount} / ${activeRegisterStatus.decodedEventCount} / ${activeRegisterStatus.ingestedEventCount}` : '-'}</strong></div>
          <div className="summary-metric"><span>Ingestion</span><strong>{activeRegisterStatus?.ingestionEnabled ? 'Aktiv' : 'Yalnız diaqnostika'}</strong></div>
          {!activeRegisterStatus?.ingestionEnabled && <Alert type="warning" showIcon message="Active Register ingestion söndürülüb" description="Callback-lər raw diagnostics kimi saxlanır. Attendance/security yaratmaq üçün backend-də DAHUA_ACTIVE_REGISTER_INGESTION_ENABLED=true edin." />}
        </aside>
      </section>
    </div>
  )
}






