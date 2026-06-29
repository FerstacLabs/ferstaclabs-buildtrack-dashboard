import { BellOutlined, UserOutlined } from '@ant-design/icons'
import { Avatar, Button } from 'antd'

export const HeaderBar = () => (
  <div className="header-actions">
    <Button aria-label="Bildirişlər" className="icon-button" icon={<BellOutlined />} />
    <Avatar size={42} icon={<UserOutlined />} className="header-avatar" />
  </div>
)
