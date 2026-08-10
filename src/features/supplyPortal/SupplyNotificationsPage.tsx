import { BellOutlined } from '@ant-design/icons'
import { Card, List, Tag } from 'antd'
import { useSupplyPortalStore } from './supplyPortalStore'

export const SupplyNotificationsPage = () => {
  const { notifications } = useSupplyPortalStore()
  return (
    <div className="field-page supply-page">
      <div className="field-toolbar">
        <div>
          <span className="field-eyebrow">Supply Portal</span>
          <h2>Bildirişlər</h2>
        </div>
      </div>
      <Card className="soft-card">
        <List
          dataSource={notifications}
          locale={{ emptyText: 'Bildiriş yoxdur' }}
          renderItem={(item) => (
            <List.Item>
              <List.Item.Meta
                avatar={<BellOutlined />}
                title={<span>{item.title} <Tag color={item.status === 'Unread' ? 'blue' : 'default'}>{item.status === 'Unread' ? 'Yeni' : 'Oxunub'}</Tag></span>}
                description={`${item.message} • ${new Date(item.createdAt).toLocaleString('az-AZ')}`}
              />
            </List.Item>
          )}
        />
      </Card>
    </div>
  )
}
