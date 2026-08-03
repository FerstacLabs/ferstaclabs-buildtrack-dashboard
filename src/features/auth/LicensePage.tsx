import { SafetyCertificateOutlined } from '@ant-design/icons'
import { Alert, Button, Card, Form, Input, Result, Space, Tag, Typography, message } from 'antd'
import { useNavigate } from 'react-router-dom'
import { useAuthStore } from './authStore'

export const LicensePage = () => {
  const navigate = useNavigate()
  const { activateLicense, license, loading, error, logout, tenant } = useAuthStore()
  const active = license?.status === 'Active'

  const onFinish = async (values: { licenseKey: string }) => {
    try {
      await activateLicense(values.licenseKey)
      message.success('Lisenziya aktivləşdirildi')
      navigate('/', { replace: true })
    } catch {
      message.error('Lisenziya aktivləşdirilmədi')
    }
  }

  if (active) {
    return (
      <div className="auth-page">
        <Result
          status="success"
          title="Lisenziya aktivdir"
          subTitle={`${tenant?.companyName ?? 'Şirkət'} üçün giriş hazırdır.`}
          extra={<Button type="primary" onClick={() => navigate('/', { replace: true })}>Dashboard-a keç</Button>}
        />
      </div>
    )
  }

  return (
    <div className="auth-page">
      <Card className="auth-card license-card">
        <Space direction="vertical" size={14} className="full-width">
          <SafetyCertificateOutlined className="license-icon" />
          <Typography.Title level={2}>Lisenziya tələb olunur</Typography.Title>
          <Typography.Paragraph>
            Hesabınız yaradılıb, amma dashboard modulları üçün aktiv lisenziya lazımdır.
          </Typography.Paragraph>
          <Tag color="orange">Status: {license?.status ?? 'Pending'}</Tag>
          {error && <Alert type="error" showIcon message={error} className="auth-alert" />}
          <Form layout="vertical" onFinish={onFinish}>
            <Form.Item name="licenseKey" label="Lisenziya açarı" rules={[{ required: true, message: 'Lisenziya açarını daxil edin' }]}>
              <Input placeholder="BT-XXXX-XXXX-XXXX" />
            </Form.Item>
            <Button type="primary" htmlType="submit" loading={loading} block>
              Aktivləşdir
            </Button>
          </Form>
          <Button type="link" onClick={() => void logout()}>
            Başqa hesabla giriş et
          </Button>
        </Space>
      </Card>
    </div>
  )
}
