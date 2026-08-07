import { Card, Descriptions, List, Typography } from 'antd'
import { useFieldPortalStore } from './fieldPortalStore'

export const FieldSettingsPage = () => {
  const me = useFieldPortalStore((state) => state.me)
  const assignments = useFieldPortalStore((state) => state.assignments)

  return (
    <div className="field-page">
      <div className="field-page-title">
        <span>Prorab hesabı</span>
        <Typography.Title level={2}>Ayarlar</Typography.Title>
      </div>
      <Card className="soft-card" title="Hesab məlumatları">
        <Descriptions column={1}>
          <Descriptions.Item label="Ad Soyad">{me?.fullName}</Descriptions.Item>
          <Descriptions.Item label="Email">{me?.email}</Descriptions.Item>
          <Descriptions.Item label="Rol">{me?.role}</Descriptions.Item>
          <Descriptions.Item label="Şirkət">{me?.tenantName}</Descriptions.Item>
        </Descriptions>
      </Card>
      <Card className="soft-card" title="Təyin olunmuş obyektlər">
        <List
          dataSource={assignments}
          renderItem={(assignment) => (
            <List.Item>
              <List.Item.Meta title={assignment.siteName} description={assignment.address || 'Ünvan qeyd edilməyib'} />
              <span>{new Date(assignment.assignedAt).toLocaleDateString('az-AZ')}</span>
            </List.Item>
          )}
        />
      </Card>
    </div>
  )
}
