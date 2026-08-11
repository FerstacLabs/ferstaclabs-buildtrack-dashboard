import {
  BellOutlined,
  CheckSquareOutlined,
  HistoryOutlined,
  HomeOutlined,
  LogoutOutlined,
  MenuOutlined,
  SettingOutlined,
  ShoppingCartOutlined,
} from '@ant-design/icons'
import { Alert, Button, Drawer, Spin } from 'antd'
import { useEffect, useState } from 'react'
import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { useAuthStore } from '../auth/authStore'
import { useSupplyPortalStore } from './supplyPortalStore'

const navItems = [
  { path: '/', label: 'İcmal', icon: <HomeOutlined />, end: true },
  { path: '/tasks', label: 'Satınalma tapşırıqları', icon: <ShoppingCartOutlined /> },
  { path: '/history', label: 'Tarixçə', icon: <HistoryOutlined /> },
  { path: '/notifications', label: 'Bildirişlər', icon: <BellOutlined /> },
  { path: '/settings', label: 'Ayarlar', icon: <SettingOutlined /> },
]

const SupplySidebar = ({ onNavigate }: { onNavigate?: () => void }) => (
  <aside className="field-sidebar supply-sidebar">
    <div className="field-brand">
      <div className="field-logo">BT</div>
      <div>
        <strong>BuildTrack Supply</strong>
        <span>Satınalma portalı</span>
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

export const SupplyLayout = () => {
  const navigate = useNavigate()
  const [drawerOpen, setDrawerOpen] = useState(false)
  const { user, tenant, logout } = useAuthStore()
  const { loading, error, load } = useSupplyPortalStore()

  useEffect(() => {
    void load()
  }, [load])

  const handleLogout = async () => {
    await logout()
    navigate('/login', { replace: true })
  }

  if (loading && !user) {
    return (
      <div className="field-loader">
        <Spin size="large" />
        <span>Satınalma portalı açılır...</span>
      </div>
    )
  }

  return (
    <div className="field-shell supply-shell">
      <SupplySidebar />
      <main className="field-main">
        <header className="field-header">
          <Button className="field-mobile-menu" icon={<MenuOutlined />} onClick={() => setDrawerOpen(true)}>
            Menyu
          </Button>
          <div>
            <strong>{user?.fullName ?? 'Təchizatçı'}</strong>
            <span>{tenant?.companyName ?? 'BuildTrack'}</span>
          </div>
          <div className="supply-header-badge">
            <CheckSquareOutlined /> Sübutlu satınalma axını
          </div>
          <Button icon={<LogoutOutlined />} onClick={handleLogout}>Çıxış</Button>
        </header>
        {error && <Alert type="error" showIcon message={error} className="field-alert" />}
        <Outlet />
      </main>
      <Drawer open={drawerOpen} placement="left" width={290} closable={false} onClose={() => setDrawerOpen(false)}>
        <SupplySidebar onNavigate={() => setDrawerOpen(false)} />
      </Drawer>
    </div>
  )
}
