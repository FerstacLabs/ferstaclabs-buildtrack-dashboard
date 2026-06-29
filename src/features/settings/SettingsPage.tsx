import { ClockCircleOutlined, ExportOutlined, SafetyCertificateOutlined, SettingOutlined, TeamOutlined } from '@ant-design/icons'
import { Button, Form, Input, InputNumber, Select, message } from 'antd'
import { useEffect } from 'react'
import { PageTitle } from '../../components/ui/PageTitle'
import { useBuildTrackStore } from '../../services/data/dataService'

interface DemoSettings {
  companyDisplayName: string
  defaultWorkStart: string
  defaultWorkEnd: string
  defaultGeofenceRadius: number
  lowRiskThreshold: number
  mediumRiskThreshold: number
  highRiskThreshold: number
  exportFormatPreference: string
}

const settingsStorageKey = 'buildtrack-demo-settings'

const settingCards = [
  ['Şirkət məlumatları', 'Şirkət adı və demo görünüş adı.', <SettingOutlined />],
  ['İş saatları', 'Standart giriş və çıxış vaxtı qaydaları.', <ClockCircleOutlined />],
  ['Risk qaydaları', 'Risk balı limitləri və nəzarət səviyyələri.', <SafetyCertificateOutlined />],
  ['Export formatları', 'Excel, CSV və 1C üçün demo seçimləri.', <ExportOutlined />],
  ['İstifadəçi rolları', 'HR, layihə rəhbəri, mühasibatlıq və prorab rolları.', <TeamOutlined />],
]

const loadSettings = (fallbackName: string): DemoSettings => {
  const fallback: DemoSettings = {
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
    return raw ? { ...fallback, ...(JSON.parse(raw) as Partial<DemoSettings>) } : fallback
  } catch {
    return fallback
  }
}

export const SettingsPage = () => {
  const { data, resetDemoData } = useBuildTrackStore()
  const [form] = Form.useForm<DemoSettings>()
  const companyName = data?.company[0]?.company_name ?? 'BuildTrack Demo'

  useEffect(() => {
    form.setFieldsValue(loadSettings(companyName))
  }, [companyName, form])

  if (!data) return null

  const saveSettings = (values: DemoSettings) => {
    window.localStorage.setItem(settingsStorageKey, JSON.stringify(values))
    void message.success('Demo ayarları yadda saxlanıldı')
  }

  return (
    <div className="page-stack">
      <PageTitle title="Ayarlar" subtitle="Demo parametrləri və gələcək backend üçün hazır struktur" />

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
          <h2>Demo ayarları</h2>
          <Form form={form} layout="vertical" onFinish={saveSettings}>
            <Form.Item label="Şirkət görünüş adı (Demo ayarı)" name="companyDisplayName">
              <Input />
            </Form.Item>
            <Form.Item label="Standart iş başlama vaxtı (Demo ayarı)" name="defaultWorkStart">
              <Select options={[{ label: '07:00', value: '07:00' }, { label: '08:00', value: '08:00' }, { label: '09:00', value: '09:00' }]} />
            </Form.Item>
            <Form.Item label="Standart iş bitmə vaxtı (Demo ayarı)" name="defaultWorkEnd">
              <Select options={[{ label: '16:00', value: '16:00' }, { label: '17:00', value: '17:00' }, { label: '18:00', value: '18:00' }]} />
            </Form.Item>
            <Form.Item label="Default geofence radiusu (Demo ayarı)" name="defaultGeofenceRadius">
              <InputNumber min={50} max={1000} addonAfter="m" />
            </Form.Item>
            <Button type="primary" htmlType="submit">Yadda saxla</Button>
          </Form>
        </section>

        <section className="panel-card">
          <h2>Risk və export qaydaları</h2>
          <Form form={form} layout="vertical" onFinish={saveSettings}>
            <Form.Item label="Orta risk başlangıcı (Demo ayarı)" name="lowRiskThreshold">
              <InputNumber min={0} max={100} />
            </Form.Item>
            <Form.Item label="Yüksək risk başlangıcı (Demo ayarı)" name="mediumRiskThreshold">
              <InputNumber min={0} max={100} />
            </Form.Item>
            <Form.Item label="Kritik risk başlangıcı (Demo ayarı)" name="highRiskThreshold">
              <InputNumber min={0} max={100} />
            </Form.Item>
            <Form.Item label="Export formatı seçimi (Demo ayarı)" name="exportFormatPreference">
              <Select options={[{ label: 'Excel', value: 'Excel' }, { label: 'CSV', value: 'CSV' }, { label: '1C XML', value: '1C XML' }]} />
            </Form.Item>
            <Button type="primary" htmlType="submit">Yadda saxla</Button>
          </Form>
          <div className="settings-reset">
            <Button danger onClick={() => void resetDemoData().then(() => message.success('Demo data yeniləndi'))}>Reset Demo Data</Button>
          </div>
        </section>
      </section>
    </div>
  )
}
