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
  InboxOutlined,
  KeyOutlined,
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
import { useI18n } from '../../i18n'
import { useAuthStore } from '../../features/auth/authStore'

interface SidebarItem {
  labelKey: string
  path: string
  icon: ReactNode
  end?: boolean
}

interface SidebarGroup {
  id: string
  labelKey: string
  icon: ReactNode
  children: SidebarItem[]
}

interface SidebarProps {
  embedded?: boolean
  onNavigate?: () => void
}

const mainItems: SidebarItem[] = [
  { labelKey: 'nav.dashboard', path: '/', icon: <DashboardOutlined /> },
  { labelKey: 'nav.estimate', path: '/estimate', icon: <FileSearchOutlined /> },
  { labelKey: 'nav.crews', path: '/crews', icon: <TeamOutlined /> },
  { labelKey: 'nav.workers', path: '/workers', icon: <UserOutlined /> },
  { labelKey: 'nav.dailyReports', path: '/daily-reports', icon: <CalendarOutlined /> },
]

const footerItems: SidebarItem[] = [
  { labelKey: 'nav.supervisorAudit', path: '/supervisor-audit', icon: <AuditOutlined /> },
  { labelKey: 'nav.export', path: '/export', icon: <ExportOutlined /> },
  { labelKey: 'nav.settings', path: '/settings', icon: <SettingOutlined /> },
]

const groups: SidebarGroup[] = [
  {
    id: 'materials',
    labelKey: 'nav.materialsGroup',
    icon: <ToolOutlined />,
    children: [
      { labelKey: 'nav.materials', path: '/materials', icon: <ToolOutlined /> },
      { labelKey: 'nav.warehouse', path: '/warehouse', icon: <InboxOutlined /> },
    ],
  },
  {
    id: 'attendance',
    labelKey: 'nav.attendanceGroup',
    icon: <ClockCircleOutlined />,
    children: [
      { labelKey: 'nav.attendanceHours', path: '/daily-attendance', icon: <ClockCircleOutlined /> },
      { labelKey: 'nav.risksDelays', path: '/delays-permissions', icon: <FieldTimeOutlined /> },
      { labelKey: 'nav.payroll', path: '/payroll', icon: <DollarCircleOutlined /> },
    ],
  },
  {
    id: 'camera',
    labelKey: 'nav.cameraGroup',
    icon: <ApiOutlined />,
    children: [
      { labelKey: 'nav.cameraDevices', path: '/devices', icon: <ApiOutlined /> },
      { labelKey: 'nav.liveAttendance', path: '/attendance-live', icon: <ThunderboltOutlined /> },
      { labelKey: 'nav.unknownFaces', path: '/security-events', icon: <EyeInvisibleOutlined /> },
    ],
  },
]

const isActivePath = (pathname: string, item: SidebarItem) => {
  if (item.path === '/') return pathname === '/'
  return pathname === item.path || pathname.startsWith(`${item.path}/`)
}

const SidebarLink = ({ item, child = false, onNavigate }: { item: SidebarItem; child?: boolean; onNavigate?: () => void }) => {
  const { t } = useI18n()
  return (
  <NavLink
    to={item.path}
    end={item.end ?? item.path === '/'}
    className={({ isActive }) => `sidebar-link${child ? ' sidebar-child' : ''}${isActive ? ' active' : ''}`}
    onClick={onNavigate}
  >
    <span className="sidebar-icon">{item.icon}</span>
    <span>{t(item.labelKey)}</span>
  </NavLink>
  )
}

export const Sidebar = ({ embedded = false, onNavigate }: SidebarProps) => {
  const { t } = useI18n()
  const { tenant, user } = useAuthStore()
  const location = useLocation()
  const showAdminLicenses = tenant?.code === 'DEMO' && (user?.role === 'Owner' || user?.role === 'Admin')
  const activeGroupIds = useMemo(
    () => new Set(groups.filter((group) => group.children.some((item) => isActivePath(location.pathname, item))).map((group) => group.id)),
    [location.pathname],
  )
  const [openGroups, setOpenGroups] = useState<Record<string, boolean>>({
    materials: true,
    attendance: true,
    camera: true,
  })

  const toggleGroup = (groupId: string) => {
    setOpenGroups((current) => ({ ...current, [groupId]: !(current[groupId] ?? activeGroupIds.has(groupId)) }))
  }

  const materialsOpen = openGroups.materials || activeGroupIds.has('materials')
  const attendanceOpen = openGroups.attendance || activeGroupIds.has('attendance')
  const cameraOpen = openGroups.camera || activeGroupIds.has('camera')

  return (
    <aside className={`app-sidebar${embedded ? ' sidebar-embedded' : ''}`}>
      <div className="sidebar-brand">
        <div className="sidebar-logo">BT</div>
        <div>
          <strong>BuildTrack</strong>
          <span>{t('brand.subtitle')}</span>
        </div>
      </div>

      <nav className="sidebar-nav">
        {mainItems.map((item) => <SidebarLink key={item.path} item={item} onNavigate={onNavigate} />)}

        <div className="sidebar-group">
          <button
            type="button"
            className={`sidebar-link sidebar-group-toggle${activeGroupIds.has('materials') ? ' active' : ''}`}
            onClick={() => toggleGroup('materials')}
          >
            <span className="sidebar-icon">{groups[0].icon}</span>
            <span>{t(groups[0].labelKey)}</span>
            <span className="sidebar-caret">{materialsOpen ? <DownOutlined /> : <RightOutlined />}</span>
          </button>
          {materialsOpen && groups[0].children.map((item) => <SidebarLink key={item.path} item={item} child onNavigate={onNavigate} />)}
        </div>

        <div className="sidebar-group">
          <button
            type="button"
            className={`sidebar-link sidebar-group-toggle${activeGroupIds.has('attendance') ? ' active' : ''}`}
            onClick={() => toggleGroup('attendance')}
          >
            <span className="sidebar-icon">{groups[1].icon}</span>
            <span>{t(groups[1].labelKey)}</span>
            <span className="sidebar-caret">{attendanceOpen ? <DownOutlined /> : <RightOutlined />}</span>
          </button>
          {attendanceOpen && groups[1].children.map((item) => <SidebarLink key={item.path} item={item} child onNavigate={onNavigate} />)}
        </div>

        {footerItems.slice(0, 2).map((item) => <SidebarLink key={item.path} item={item} onNavigate={onNavigate} />)}

        <div className="sidebar-group">
          <button
            type="button"
            className={`sidebar-link sidebar-group-toggle${activeGroupIds.has('camera') ? ' active' : ''}`}
            onClick={() => toggleGroup('camera')}
          >
            <span className="sidebar-icon">{groups[2].icon}</span>
            <span>{t(groups[2].labelKey)}</span>
            <span className="sidebar-caret">{cameraOpen ? <DownOutlined /> : <RightOutlined />}</span>
          </button>
          {cameraOpen && groups[2].children.map((item) => <SidebarLink key={item.path} item={item} child onNavigate={onNavigate} />)}
        </div>

        {showAdminLicenses && <SidebarLink item={{ labelKey: 'nav.adminLicenses', path: '/admin/licenses', icon: <KeyOutlined /> }} onNavigate={onNavigate} />}
        {footerItems.slice(2).map((item) => <SidebarLink key={item.path} item={item} onNavigate={onNavigate} />)}
      </nav>
    </aside>
  )
}
