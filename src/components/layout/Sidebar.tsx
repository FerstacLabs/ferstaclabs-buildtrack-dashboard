import {
  ApiOutlined,
  AuditOutlined,
  CalendarOutlined,
  ClockCircleOutlined,
  DashboardOutlined,
  DollarCircleOutlined,
  DownOutlined,
  ExportOutlined,
  EyeInvisibleOutlined,
  FieldTimeOutlined,
  FileSearchOutlined,
  RightOutlined,
  SettingOutlined,
  TeamOutlined,
  ThunderboltOutlined,
  ToolOutlined,
  UserOutlined,
} from '@ant-design/icons'
import type { ReactNode } from 'react'
import { useMemo, useState } from 'react'
import { NavLink, useLocation } from 'react-router-dom'

interface SidebarItem {
  label: string
  path: string
  icon: ReactNode
  end?: boolean
}

interface SidebarGroup {
  id: string
  label: string
  icon: ReactNode
  children: SidebarItem[]
}

const mainItems: SidebarItem[] = [
  { label: 'Dashboard', path: '/', icon: <DashboardOutlined /> },
  { label: 'Smeta', path: '/estimate', icon: <FileSearchOutlined /> },
  { label: 'Briqadalar', path: '/crews', icon: <TeamOutlined /> },
  { label: 'İşçilər', path: '/workers', icon: <UserOutlined /> },
  { label: 'Gündəlik hesabatlar', path: '/daily-reports', icon: <CalendarOutlined /> },
  { label: 'Materiallar', path: '/materials', icon: <ToolOutlined /> },
]

const footerItems: SidebarItem[] = [
  { label: 'Prorab Audit', path: '/supervisor-audit', icon: <AuditOutlined /> },
  { label: 'Export / 1C', path: '/export', icon: <ExportOutlined /> },
  { label: 'Ayarlar', path: '/settings', icon: <SettingOutlined /> },
]

const groups: SidebarGroup[] = [
  {
    id: 'attendance',
    label: 'Davamiyyət / Saatlar',
    icon: <ClockCircleOutlined />,
    children: [
      { label: 'Davamiyyət / Saatlar', path: '/daily-attendance', icon: <ClockCircleOutlined /> },
      { label: 'Risk və gecikmələr', path: '/delays-permissions', icon: <FieldTimeOutlined /> },
      { label: 'Maaş Hesabatı', path: '/payroll', icon: <DollarCircleOutlined /> },
    ],
  },
  {
    id: 'camera',
    label: 'Kamera idarəetmə sistemi',
    icon: <ApiOutlined />,
    children: [
      { label: 'Kamera cihazları', path: '/devices', icon: <ApiOutlined /> },
      { label: 'Canlı Davamiyyət', path: '/attendance-live', icon: <ThunderboltOutlined /> },
      { label: 'Tanınmayan üzlər', path: '/security-events', icon: <EyeInvisibleOutlined /> },
    ],
  },
]

const isActivePath = (pathname: string, item: SidebarItem) => {
  if (item.path === '/') return pathname === '/'
  return pathname === item.path || pathname.startsWith(`${item.path}/`)
}

const SidebarLink = ({ item, child = false }: { item: SidebarItem; child?: boolean }) => (
  <NavLink
    to={item.path}
    end={item.end ?? item.path === '/'}
    className={({ isActive }) => `sidebar-link${child ? ' sidebar-child' : ''}${isActive ? ' active' : ''}`}
  >
    <span className="sidebar-icon">{item.icon}</span>
    <span>{item.label}</span>
  </NavLink>
)

export const Sidebar = () => {
  const location = useLocation()
  const activeGroupIds = useMemo(
    () => new Set(groups.filter((group) => group.children.some((item) => isActivePath(location.pathname, item))).map((group) => group.id)),
    [location.pathname],
  )
  const [openGroups, setOpenGroups] = useState<Record<string, boolean>>({
    attendance: true,
    camera: true,
  })

  const toggleGroup = (groupId: string) => {
    setOpenGroups((current) => ({ ...current, [groupId]: !(current[groupId] ?? activeGroupIds.has(groupId)) }))
  }

  const attendanceOpen = openGroups.attendance || activeGroupIds.has('attendance')
  const cameraOpen = openGroups.camera || activeGroupIds.has('camera')

  return (
    <aside className="app-sidebar">
      <div className="sidebar-brand">
        <div className="sidebar-logo">BT</div>
        <div>
          <strong>BuildTrack</strong>
          <span>Tikinti nəzarət platforması</span>
        </div>
      </div>

      <nav className="sidebar-nav">
        {mainItems.map((item) => <SidebarLink key={item.path} item={item} />)}

        <div className="sidebar-group">
          <button
            type="button"
            className={`sidebar-link sidebar-group-toggle${activeGroupIds.has('attendance') ? ' active' : ''}`}
            onClick={() => toggleGroup('attendance')}
          >
            <span className="sidebar-icon">{groups[0].icon}</span>
            <span>{groups[0].label}</span>
            <span className="sidebar-caret">{attendanceOpen ? <DownOutlined /> : <RightOutlined />}</span>
          </button>
          {attendanceOpen && groups[0].children.map((item) => <SidebarLink key={item.path} item={item} child />)}
        </div>

        {footerItems.slice(0, 2).map((item) => <SidebarLink key={item.path} item={item} />)}

        <div className="sidebar-group">
          <button
            type="button"
            className={`sidebar-link sidebar-group-toggle${activeGroupIds.has('camera') ? ' active' : ''}`}
            onClick={() => toggleGroup('camera')}
          >
            <span className="sidebar-icon">{groups[1].icon}</span>
            <span>{groups[1].label}</span>
            <span className="sidebar-caret">{cameraOpen ? <DownOutlined /> : <RightOutlined />}</span>
          </button>
          {cameraOpen && groups[1].children.map((item) => <SidebarLink key={item.path} item={item} child />)}
        </div>

        {footerItems.slice(2).map((item) => <SidebarLink key={item.path} item={item} />)}
      </nav>
    </aside>
  )
}
