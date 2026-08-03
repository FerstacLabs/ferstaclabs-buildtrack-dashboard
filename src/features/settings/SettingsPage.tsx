import { ApiOutlined, GlobalOutlined, LockOutlined, LogoutOutlined, RobotOutlined, SafetyCertificateOutlined, SettingOutlined } from '@ant-design/icons'
import { Button, Descriptions, Select, Space, Tag, message } from 'antd'
import { useCallback, useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { PageTitle } from '../../components/ui/PageTitle'
import { languageOptions, type AppLanguage, useI18n } from '../../i18n'
import { buildTrackBackendApi, type ActiveRegisterStatus, type BackendDevice } from '../../services/api/buildTrackBackendApi'
import { tryApiRequest } from '../../shared/api/client'
import { useAuthStore } from '../auth/authStore'

interface AiAssistantStatus {
  enabled: boolean
  configured: boolean
}

const languageSavedMessages: Record<AppLanguage, string> = {
  az: 'Dil ayarı yadda saxlandı',
  en: 'Language setting saved',
  ru: 'Настройка языка сохранена',
}

const formatDate = (value?: string) => value ? new Date(value).toLocaleDateString('az-AZ') : '-'
const formatDateTime = (value?: string) => value ? new Date(value).toLocaleString('az-AZ') : '-'

export const SettingsPage = () => {
  const navigate = useNavigate()
  const { language, setLanguage, t } = useI18n()
  const { tenant, user, license, logout } = useAuthStore()
  const [aiStatus, setAiStatus] = useState<AiAssistantStatus | null>(null)
  const [devices, setDevices] = useState<BackendDevice[]>([])
  const [activeRegisterStatus, setActiveRegisterStatus] = useState<ActiveRegisterStatus | null>(null)

  const loadOperationalStatus = useCallback(async () => {
    const [ai, deviceRows, activeStatus] = await Promise.all([
      tryApiRequest<AiAssistantStatus>('/api/ai/project-assistant/status'),
      buildTrackBackendApi.getDevices().catch(() => []),
      buildTrackBackendApi.getActiveRegisterStatus().catch(() => null),
    ])
    setAiStatus(ai ?? { enabled: false, configured: false })
    setDevices(deviceRows)
    setActiveRegisterStatus(activeStatus)
  }, [])

  useEffect(() => {
    void loadOperationalStatus()
  }, [loadOperationalStatus])

  const changeLanguage = (nextLanguage: AppLanguage) => {
    setLanguage(nextLanguage)
    void message.success(languageSavedMessages[nextLanguage])
  }

  const handleLogout = async () => {
    await logout()
    navigate('/login', { replace: true })
  }

  const lastCameraEvent = devices
    .map((device) => device.lastEventAt ?? device.lastSeenAt)
    .filter(Boolean)
    .sort()
    .at(-1)

  return (
    <div className="page-stack">
      <PageTitle title={t('settings.title')} subtitle="Şirkət hesabı, lisenziya, dil və kamera inteqrasiyası" />

      <section className="settings-overview-grid">
        <section className="panel-card">
          <div className="settings-section-title">
            <SettingOutlined />
            <h2>Şirkət / hesab</h2>
          </div>
          <Descriptions column={1} size="small">
            <Descriptions.Item label="Şirkət adı">{tenant?.companyName ?? '-'}</Descriptions.Item>
            <Descriptions.Item label="Owner/Admin email">{user?.email ?? '-'}</Descriptions.Item>
            <Descriptions.Item label="Rol">{user?.role ?? '-'}</Descriptions.Item>
          </Descriptions>
          <p className="muted-text">Şirkət adını dəyişmək üçün administratorla əlaqə saxlayın.</p>
        </section>

        <section className="panel-card">
          <div className="settings-section-title">
            <SafetyCertificateOutlined />
            <h2>Lisenziya</h2>
          </div>
          <Descriptions column={1} size="small">
            <Descriptions.Item label="Plan">{license?.plan ?? '-'}</Descriptions.Item>
            <Descriptions.Item label="Status">
              <Tag color={license?.status === 'Active' ? 'green' : 'orange'}>{license?.status ?? 'Pending'}</Tag>
            </Descriptions.Item>
            <Descriptions.Item label="Max layihə">{license?.maxProjects ?? '-'}</Descriptions.Item>
            <Descriptions.Item label="Max istifadəçi">{license?.maxUsers ?? '-'}</Descriptions.Item>
            <Descriptions.Item label="Max kamera">{license?.maxCameras ?? '-'}</Descriptions.Item>
            <Descriptions.Item label="Bitmə tarixi">{formatDate(license?.expiresAt)}</Descriptions.Item>
          </Descriptions>
          {license?.status !== 'Active' && <Button onClick={() => navigate('/license')}>Lisenziyanı aktivləşdir</Button>}
        </section>

        <section className="panel-card compact-settings-card">
          <div className="settings-section-title">
            <GlobalOutlined />
            <h2>Dil</h2>
          </div>
          <Select
            value={language}
            options={languageOptions.map((option) => ({ value: option.value, label: t(option.labelKey) }))}
            onChange={changeLanguage}
            style={{ width: '100%' }}
          />
        </section>

        <section className="panel-card">
          <div className="settings-section-title">
            <RobotOutlined />
            <h2>AI köməkçi</h2>
          </div>
          <Tag color={aiStatus?.enabled && aiStatus.configured ? 'green' : 'default'}>
            {aiStatus?.enabled && aiStatus.configured ? 'AI köməkçi aktivdir' : 'AI köməkçi hazırda aktiv deyil'}
          </Tag>
          <p className="muted-text">Layihə məlumatları və hesabatlarla işləmək üçün köməkçi modul.</p>
        </section>

        <section className="panel-card">
          <div className="settings-section-title">
            <ApiOutlined />
            <h2>Kamera inteqrasiyası</h2>
          </div>
          <Descriptions column={1} size="small">
            <Descriptions.Item label="Server IP">46.101.182.202</Descriptions.Item>
            <Descriptions.Item label="Active Register port">7000</Descriptions.Item>
            <Descriptions.Item label="Qeydiyyatlı kamera">{devices.length}</Descriptions.Item>
            <Descriptions.Item label="Son kamera hadisəsi">{formatDateTime(lastCameraEvent)}</Descriptions.Item>
            <Descriptions.Item label="Listener">
              <Tag color={activeRegisterStatus?.listenerActive ? 'green' : 'orange'}>{activeRegisterStatus?.listenerActive ? 'Aktiv' : 'Gözləyir'}</Tag>
            </Descriptions.Item>
          </Descriptions>
          <Button onClick={() => navigate('/devices')}>Kamera cihazlarına keç</Button>
        </section>

        <section className="panel-card">
          <div className="settings-section-title">
            <LockOutlined />
            <h2>Təhlükəsizlik</h2>
          </div>
          <Space wrap>
            <Button disabled>Şifrəni dəyiş — Tezliklə əlavə olunacaq</Button>
            <Button danger icon={<LogoutOutlined />} onClick={() => void handleLogout()}>Çıxış</Button>
          </Space>
        </section>
      </section>
    </div>
  )
}
