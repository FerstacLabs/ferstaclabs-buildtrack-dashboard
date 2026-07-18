import {
  ApiOutlined,
  AuditOutlined,
  BarChartOutlined,
  CalendarOutlined,
  ClockCircleOutlined,
  DashboardOutlined,
  DollarCircleOutlined,
  ExportOutlined,
  EyeInvisibleOutlined,
  FieldTimeOutlined,
  FileSearchOutlined,
  LineChartOutlined,
  ProfileOutlined,
  SafetyCertificateOutlined,
  SettingOutlined,
  TeamOutlined,
  ThunderboltOutlined,
  UnorderedListOutlined,
} from '@ant-design/icons'
import type { ReactNode } from 'react'
import { NavLink } from 'react-router-dom'

interface SidebarItem {
  label: string
  path: string
  icon: ReactNode
  end?: boolean
  children?: SidebarItem[]
}

const menuItems: SidebarItem[] = [
  { label: 'Dashboard', path: '/', icon: <DashboardOutlined /> },
  { label: 'Günlük Davamiyyət', path: '/daily-attendance', icon: <CalendarOutlined /> },
  { label: 'Obyekt Saatları', path: '/site-hours', icon: <ClockCircleOutlined /> },
  { label: 'Riskli İşçilər', path: '/risk-workers', icon: <SafetyCertificateOutlined /> },
  { label: 'Gecikmələr və İcazələr', path: '/delays-permissions', icon: <FieldTimeOutlined /> },
  { label: 'Maaş Hesabatı', path: '/payroll', icon: <DollarCircleOutlined /> },
  { label: 'Performans Trendi', path: '/performance', icon: <LineChartOutlined /> },
  { label: 'Prorab Audit', path: '/supervisor-audit', icon: <AuditOutlined /> },
  { label: 'İş Fazası & Cost Code', path: '/cost-code', icon: <BarChartOutlined /> },
  { label: 'Custom Reports', path: '/custom-reports', icon: <FileSearchOutlined /> },
  { label: '1C / Export', path: '/export', icon: <ExportOutlined /> },
  { label: 'Dahua Cihazları', path: '/devices', icon: <ApiOutlined /> },
  { label: 'Canlı Davamiyyət', path: '/attendance-live', icon: <ThunderboltOutlined /> },
  { label: 'Tanınmayan üzlər', path: '/security-events', icon: <EyeInvisibleOutlined /> },
  {
    label: 'Layihə Gedişatı',
    path: '/project-progress',
    icon: <ProfileOutlined />,
    children: [
      { label: 'Dashboard', path: '/project-progress', icon: <DashboardOutlined />, end: true },
      { label: 'Smeta', path: '/project-progress/estimate', icon: <FileSearchOutlined /> },
      { label: 'Briqadalar', path: '/project-progress/crews', icon: <TeamOutlined /> },
      { label: 'Təqvim / Gedişat', path: '/project-progress/timeline', icon: <UnorderedListOutlined /> },
    ],
  },
  { label: 'Ayarlar', path: '/settings', icon: <SettingOutlined /> },
]

export const Sidebar = () => (
  <aside className="app-sidebar">
    <div className="sidebar-brand">
      <div className="sidebar-logo">BT</div>
      <div>
        <strong>BuildTrack</strong>
        <span>Demo panel</span>
      </div>
    </div>

    <nav className="sidebar-nav">
      {menuItems.map((item) => (
        <div className="sidebar-group" key={item.path}>
          <NavLink
            to={item.path}
            end={item.end ?? item.path === '/'}
            className={({ isActive }) => `sidebar-link${isActive ? ' active' : ''}`}
          >
            <span className="sidebar-icon">{item.icon}</span>
            <span>{item.label}</span>
          </NavLink>
          {item.children?.map((child) => (
            <NavLink
              key={child.path}
              to={child.path}
              end={child.end}
              className={({ isActive }) => `sidebar-link sidebar-child${isActive ? ' active' : ''}`}
            >
              <span className="sidebar-icon">{child.icon}</span>
              <span>{child.label}</span>
            </NavLink>
          ))}
        </div>
      ))}
    </nav>
  </aside>
)


