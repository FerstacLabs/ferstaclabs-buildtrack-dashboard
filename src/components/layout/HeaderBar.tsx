import { BellOutlined, LogoutOutlined, UserOutlined } from '@ant-design/icons'
import { Avatar, Button, Dropdown, Tag } from 'antd'
import { useNavigate } from 'react-router-dom'
import { useAuthStore } from '../../features/auth/authStore'

export const HeaderBar = () => {
  const navigate = useNavigate()
  const { tenant, user, logout } = useAuthStore()

  return (
    <div className="header-actions">
      {tenant && <Tag color="cyan">{tenant.companyName}</Tag>}
      <Button aria-label="Bildirişlər" className="icon-button" icon={<BellOutlined />} />
      <Dropdown
        menu={{
          items: [
            { key: 'user', label: user?.fullName ?? 'İstifadəçi', disabled: true },
            { type: 'divider' },
            { key: 'logout', icon: <LogoutOutlined />, label: 'Çıxış' },
          ],
          onClick: async ({ key }) => {
            if (key !== 'logout') return
            await logout()
            navigate('/login', { replace: true })
          },
        }}
      >
        <Avatar size={42} icon={<UserOutlined />} className="header-avatar" />
      </Dropdown>
    </div>
  )
}
