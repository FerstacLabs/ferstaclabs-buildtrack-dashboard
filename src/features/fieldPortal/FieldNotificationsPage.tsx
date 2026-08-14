import { BellOutlined } from '@ant-design/icons'
import { Alert, Card, List, Skeleton, Tag, Typography } from 'antd'
import { useEffect, useState } from 'react'
import { buildTrackBackendApi, type FieldDashboard } from '../../services/api/buildTrackBackendApi'
import { fieldStatusColor, fieldStatusLabel, useFieldPortalStore } from './fieldPortalStore'

export const FieldNotificationsPage = () => {
  const selectedSiteId = useFieldPortalStore((state) => state.selectedSiteId)
  const [dashboard, setDashboard] = useState<FieldDashboard>()
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    if (!selectedSiteId) return
    setLoading(true)
    buildTrackBackendApi.getFieldDashboard(selectedSiteId)
      .then(setDashboard)
      .finally(() => setLoading(false))
  }, [selectedSiteId])

  if (!selectedSiteId) return <Alert type="info" showIcon message="Layihə seçin" />
  if (loading && !dashboard) return <Skeleton active />

  return (
    <div className="field-page">
      <div className="field-page-title">
        <span>Rəhbərlik və sistem bildirişləri</span>
        <Typography.Title level={2}>Bildirişlər</Typography.Title>
      </div>
      <Card className="soft-card">
        <List
          dataSource={dashboard?.recentActivity ?? []}
          locale={{ emptyText: 'Yeni bildiriş yoxdur' }}
          renderItem={(item) => (
            <List.Item>
              <List.Item.Meta
                avatar={<BellOutlined className="field-list-icon" />}
                title={item.title}
                description={new Date(item.timestamp).toLocaleString('az-AZ')}
              />
              <Tag color={fieldStatusColor(item.status)}>{fieldStatusLabel(item.status)}</Tag>
            </List.Item>
          )}
        />
      </Card>
    </div>
  )
}
