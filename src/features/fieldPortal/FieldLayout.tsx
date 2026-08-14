import {
  AuditOutlined,
  BellOutlined,
  CalendarOutlined,
  FileDoneOutlined,
  HomeOutlined,
  InboxOutlined,
  LogoutOutlined,
  MenuOutlined,
  SettingOutlined,
  TeamOutlined,
} from '@ant-design/icons'
import { Alert, Button, Drawer, Select, Spin } from 'antd'
import { useEffect, useState } from 'react'
import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { useAuthStore } from '../auth/authStore'
import { useFieldPortalStore } from './fieldPortalStore'

const navItems = [
  { path: '/', label: 'İcmal', icon: <HomeOutlined />, end: true },
  { path: '/reports', label: 'Gündəlik hesabat', icon: <FileDoneOutlined /> },
  { path: '/workers', label: 'İşçi qeydləri', icon: <TeamOutlined /> },
  { path: '/warehouse', label: 'Anbar sorğuları', icon: <InboxOutlined /> },
  { path: '/notes', label: 'Sahə qeydləri', icon: <CalendarOutlined /> },
  { path: '/notifications', label: 'Bildirişlər', icon: <BellOutlined /> },
  { path: '/settings', label: 'Ayarlar', icon: <SettingOutlined /> },
]

const FieldSidebar = ({ onNavigate }: { onNavigate?: () => void }) => (
  <aside className="field-sidebar">
    <div className="field-brand">
      <div className="field-logo">BT</div>
      <div>
        <strong>BuildTrack Field</strong>
        <span>Prorab portalı</span>
      </div>
    </div>
    <nav className="field-nav">
      {navItems.map((item) => (
        <NavLink
          key={item.path}
          to={item.path}
          end={item.end}
          className={({ isActive }) => `field-nav-link${isActive ? ' active' : ''}`}
          onClick={onNavigate}
        >
          <span>{item.icon}</span>
          {item.label}
        </NavLink>
      ))}
    </nav>
  </aside>
)

export const FieldLayout = () => {
  const navigate = useNavigate()
  const [drawerOpen, setDrawerOpen] = useState(false)
  const { logout } = useAuthStore()
  const { me, assignments, selectedSiteId, loading, error, load, setSelectedSiteId } = useFieldPortalStore()

  useEffect(() => {
    void load()
  }, [load])

  const handleLogout = async () => {
    await logout()
    navigate('/login', { replace: true })
  }

  if (loading && !me) {
    return (
      <div className="field-loader">
        <Spin size="large" />
        <span>Prorab portalı açılır...</span>
      </div>
    )
  }

  return (
    <div className="field-shell">
      <FieldSidebar />
      <main className="field-main">
        <header className="field-header">
          <Button className="field-mobile-menu" icon={<MenuOutlined />} onClick={() => setDrawerOpen(true)}>
            Menyu
          </Button>
          <div>
            <strong>{me?.fullName ?? 'Prorab'}</strong>
            <span>{me?.tenantName ?? 'BuildTrack'}</span>
          </div>
          <Select
            className="field-site-select"
            value={selectedSiteId}
            placeholder="Layihə seçin"
            options={assignments.map((assignment) => ({ value: assignment.siteId, label: assignment.siteName }))}
            onChange={setSelectedSiteId}
          />
          <Button icon={<LogoutOutlined />} onClick={handleLogout}>Çıxış</Button>
        </header>
        {error && <Alert type="error" showIcon message={error} className="field-alert" />}
        {!assignments.length && !loading ? (
          <Alert
            type="warning"
            showIcon
            message="Sizə hələ layihə təyin edilməyib"
            description="Admin panelindən prorab üçün aktiv layihə təyinatı yaradılmalıdır."
          />
        ) : <Outlet />}
      </main>
      <Drawer open={drawerOpen} placement="left" width={290} closable={false} onClose={() => setDrawerOpen(false)}>
        <FieldSidebar onNavigate={() => setDrawerOpen(false)} />
      </Drawer>
      <div className="field-floating-audit">
        <AuditOutlined />
      </div>
    </div>
  )
}
