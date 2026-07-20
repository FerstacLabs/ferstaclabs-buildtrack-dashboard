import { ClockCircleOutlined, ExportOutlined, SafetyCertificateOutlined, SettingOutlined, TeamOutlined } from '@ant-design/icons'
import { Button, Form, Input, InputNumber, Select, message } from 'antd'
import { useEffect } from 'react'
import { PageTitle } from '../../components/ui/PageTitle'
import { useBuildTrackStore } from '../../services/data/dataService'
import { useProjectProgressStore } from '../projectProgress/projectProgressStore'

interface AppSettings {
  companyDisplayName: string
  defaultWorkStart: string
  defaultWorkEnd: string
  defaultGeofenceRadius: number
  lowRiskThreshold: number
  mediumRiskThreshold: number
  highRiskThreshold: number
  exportFormatPreference: string
}

const settingsStorageKey = 'buildtrack-app-settings'

const settingCards = [
  ['Şirkət məlumatları', 'Şirkət adı və platformada görünən məlumatlar.', <SettingOutlined />],
  ['İş saatları', 'Standart giriş və çıxış vaxtı qaydaları.', <ClockCircleOutlined />],
  ['Risk qaydaları', 'Risk balı limitləri və nəzarət səviyyələri.', <SafetyCertificateOutlined />],
  ['Export formatları', 'Excel, CSV və 1C üçün əsas seçimlər.', <ExportOutlined />],
  ['İstifadəçi rolları', 'Layihə rəhbəri, mühasibatlıq, prorab və operator rolları.', <TeamOutlined />],
]

const loadSettings = (fallbackName: string): AppSettings => {
  const fallback: AppSettings = {
    companyDisplayName: fallbackName,
    defaultWorkStart: '08:00',
    defaultWorkEnd: '17:00',
    defaultGeofenceRadius: 200,
    lowRiskThreshold: 40,
    mediumRiskThreshold: 60,
    highRiskThreshold: 80,
    exportFormatPreference: 'Excel',
  }

  try {
    const raw = window.localStorage.getItem(settingsStorageKey)
    return raw ? { ...fallback, ...(JSON.parse(raw) as Partial<AppSettings>) } : fallback
  } catch {
    return fallback
  }
}

export const SettingsPage = () => {
  const { data, resetDemoData } = useBuildTrackStore()
  const project = useProjectProgressStore((state) => state.project)
  const refreshSeedData = useProjectProgressStore((state) => state.refreshSeedData)
  const [form] = Form.useForm<AppSettings>()
  const companyName = data?.company[0]?.company_name ?? project.name

  useEffect(() => {
    form.setFieldsValue(loadSettings(companyName))
  }, [companyName, form])

  if (!data) return null

  const saveSettings = (values: AppSettings) => {
    window.localStorage.setItem(settingsStorageKey, JSON.stringify(values))
    void message.success('Ayarlar yadda saxlandı')
  }

  const refreshSampleData = async () => {
    refreshSeedData()
    await resetDemoData()
    void message.success('Nümunə məlumatları yeniləndi')
  }

  return (
    <div className="page-stack">
      <PageTitle title="Ayarlar" subtitle="Şirkət, iş saatı, risk və export parametrləri" />

      <section className="settings-grid">
        {settingCards.map(([title, text, icon]) => (
          <section className="panel-card" key={String(title)}>
            <div className="kpi-icon kpi-blue">{icon}</div>
            <h2>{title}</h2>
            <p>{text}</p>
          </section>
        ))}
      </section>

      <section className="content-grid">
        <section className="panel-card">
          <h2>Əsas ayarlar</h2>
          <Form form={form} layout="vertical" onFinish={saveSettings}>
            <Form.Item label="Şirkət görünüş adı" name="companyDisplayName">
              <Input />
            </Form.Item>
            <Form.Item label="Standart iş başlama vaxtı" name="defaultWorkStart">
              <Select options={[{ label: '07:00', value: '07:00' }, { label: '08:00', value: '08:00' }, { label: '09:00', value: '09:00' }]} />
            </Form.Item>
            <Form.Item label="Standart iş bitmə vaxtı" name="defaultWorkEnd">
              <Select options={[{ label: '16:00', value: '16:00' }, { label: '17:00', value: '17:00' }, { label: '18:00', value: '18:00' }]} />
            </Form.Item>
            <Form.Item label="Default geofence radiusu" name="defaultGeofenceRadius">
              <InputNumber min={50} max={1000} addonAfter="m" />
            </Form.Item>
            <Button type="primary" htmlType="submit">Yadda saxla</Button>
          </Form>
        </section>

        <section className="panel-card">
          <h2>Risk və export qaydaları</h2>
          <Form form={form} layout="vertical" onFinish={saveSettings}>
            <Form.Item label="Orta risk başlanğıcı" name="lowRiskThreshold">
              <InputNumber min={0} max={100} />
            </Form.Item>
            <Form.Item label="Yüksək risk başlanğıcı" name="mediumRiskThreshold">
              <InputNumber min={0} max={100} />
            </Form.Item>
            <Form.Item label="Kritik risk başlanğıcı" name="highRiskThreshold">
              <InputNumber min={0} max={100} />
            </Form.Item>
            <Form.Item label="Export formatı seçimi" name="exportFormatPreference">
              <Select options={[{ label: 'Excel', value: 'Excel' }, { label: 'CSV', value: 'CSV' }, { label: '1C XML', value: '1C XML' }]} />
            </Form.Item>
            <Button type="primary" htmlType="submit">Yadda saxla</Button>
          </Form>
          <div className="settings-reset">
            <Button danger onClick={() => void refreshSampleData()}>Nümunə məlumatları yenilə</Button>
          </div>
        </section>
      </section>
    </div>
  )
}
