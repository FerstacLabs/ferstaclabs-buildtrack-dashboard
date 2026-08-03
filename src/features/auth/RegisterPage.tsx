import { BankOutlined, LockOutlined, MailOutlined, UserOutlined } from '@ant-design/icons'
import { Alert, Button, Card, Form, Input, Typography, message } from 'antd'
import { Link, useNavigate } from 'react-router-dom'
import { AuthLandingLink } from './AuthLandingLink'
import { useAuthStore } from './authStore'

export const RegisterPage = () => {
  const navigate = useNavigate()
  const { register, loading, error } = useAuthStore()

  const onFinish = async (values: { companyName: string; fullName: string; email: string; password: string }) => {
    try {
      await register(values)
      message.success('Qeydiyyat tamamlandı. Lisenziya aktivləşdirilməlidir.')
      navigate('/license', { replace: true })
    } catch {
      message.error('Qeydiyyat alınmadı')
    }
  }

  return (
    <div className="auth-page">
      <Card className="auth-card">
        <AuthLandingLink />
        <Typography.Title level={2}>Şirkət qeydiyyatı</Typography.Title>
        <Typography.Paragraph>Yeni tenant boş iş sahəsi ilə yaradılır. Demo məlumatlar yalnız DEMO hesabında qalır.</Typography.Paragraph>
        {error && <Alert type="error" showIcon message={error} className="auth-alert" />}
        <Form layout="vertical" onFinish={onFinish}>
          <Form.Item name="companyName" label="Şirkət adı" rules={[{ required: true, message: 'Şirkət adını daxil edin' }]}>
            <Input prefix={<BankOutlined />} placeholder="Şirkət adı" />
          </Form.Item>
          <Form.Item name="fullName" label="Ad soyad" rules={[{ required: true, message: 'Ad soyad daxil edin' }]}>
            <Input prefix={<UserOutlined />} placeholder="Ad soyad" />
          </Form.Item>
          <Form.Item name="email" label="Email" rules={[{ required: true, message: 'Email daxil edin' }]}>
            <Input prefix={<MailOutlined />} placeholder="owner@company.az" />
          </Form.Item>
          <Form.Item name="password" label="Şifrə" rules={[{ required: true, min: 8, message: 'Minimum 8 simvol' }]}>
            <Input.Password prefix={<LockOutlined />} placeholder="Minimum 8 simvol" />
          </Form.Item>
          <Button type="primary" htmlType="submit" loading={loading} block>
            Qeydiyyatdan keç
          </Button>
        </Form>
        <div className="auth-link-row">
          Artıq hesabınız var? <Link to="/login">Giriş edin</Link>
        </div>
      </Card>
    </div>
  )
}
