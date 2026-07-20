import {
  ApiOutlined,
  AuditOutlined,
  CalendarOutlined,
  ClockCircleOutlined,
  DashboardOutlined,
  DollarCircleOutlined,
  ExportOutlined,
  EyeInvisibleOutlined,
  FieldTimeOutlined,
  FileSearchOutlined,
  SettingOutlined,
  TeamOutlined,
  ThunderboltOutlined,
  ToolOutlined,
  UnorderedListOutlined,
  UserOutlined,
} from '@ant-design/icons'
import type { ReactNode } from 'react'
import { NavLink } from 'react-router-dom'

interface SidebarItem {
  label: string
  path: string
  icon: ReactNode
  end?: boolean
}

const menuItems: SidebarItem[] = [
  { label: 'Dashboard', path: '/', icon: <DashboardOutlined /> },
  { label: 'Smeta', path: '/estimate', icon: <FileSearchOutlined /> },
  { label: 'Briqadalar', path: '/crews', icon: <TeamOutlined /> },
  { label: 'İşçilər', path: '/workers', icon: <UserOutlined /> },
  { label: 'Təqvim / Gedişat', path: '/timeline', icon: <UnorderedListOutlined /> },
  { label: 'Gündəlik hesabatlar', path: '/daily-reports', icon: <CalendarOutlined /> },
  { label: 'Materiallar', path: '/materials', icon: <ToolOutlined /> },
  { label: 'Davamiyyət / Saatlar', path: '/daily-attendance', icon: <ClockCircleOutlined /> },
  { label: 'Risk və gecikmələr', path: '/delays-permissions', icon: <FieldTimeOutlined /> },
  { label: 'Maaş Hesabatı', path: '/payroll', icon: <DollarCircleOutlined /> },
  { label: 'Prorab Audit', path: '/supervisor-audit', icon: <AuditOutlined /> },
  { label: 'Export / 1C', path: '/export', icon: <ExportOutlined /> },
  { label: 'Dahua Cihazları', path: '/devices', icon: <ApiOutlined /> },
  { label: 'Canlı Davamiyyət', path: '/attendance-live', icon: <ThunderboltOutlined /> },
  { label: 'Tanınmayan üzlər', path: '/security-events', icon: <EyeInvisibleOutlined /> },
  { label: 'Ayarlar', path: '/settings', icon: <SettingOutlined /> },
]

export const Sidebar = () => (
  <aside className="app-sidebar">
    <div className="sidebar-brand">
      <div className="sidebar-logo">BT</div>
      <div>
        <strong>BuildTrack</strong>
        <span>Tikinti nəzarət platforması</span>
      </div>
    </div>

    <nav className="sidebar-nav">
      {menuItems.map((item) => (
        <NavLink
          key={item.path}
          to={item.path}
          end={item.end ?? item.path === '/'}
          className={({ isActive }) => `sidebar-link${isActive ? ' active' : ''}`}
        >
          <span className="sidebar-icon">{item.icon}</span>
          <span>{item.label}</span>
        </NavLink>
      ))}
    </nav>
  </aside>
)
