import { LockOutlined, MailOutlined } from '@ant-design/icons'
import { Alert, Button, Card, Form, Input, Typography, message } from 'antd'
import { Link, useNavigate } from 'react-router-dom'
import { fieldPortalUrl, getHostMode } from '../../app/hostMode'
import { AuthLandingLink } from './AuthLandingLink'
import { LOGIN_FAILED_MESSAGE, useAuthStore } from './authStore'

export const LoginPage = () => {
  const navigate = useNavigate()
  const { login, loading, error, hasActiveLicense } = useAuthStore()

  const onFinish = async (values: { email: string; password: string }) => {
    try {
      await login(values.email, values.password)
      message.success('Giriş uğurludur')
      const state = useAuthStore.getState()
      if (getHostMode() === 'ManagementApp' && state.user?.role === 'Supervisor') {
        window.location.assign(fieldPortalUrl())
        return
      }
      navigate(state.hasActiveLicense || hasActiveLicense ? '/' : '/license', { replace: true })
    } catch {
      message.error(LOGIN_FAILED_MESSAGE)
    }
  }

  return (
    <div className="auth-page">
      <Card className="auth-card">
        <AuthLandingLink />
        <Typography.Title level={2}>BuildTrack giriş</Typography.Title>
        <Typography.Paragraph>Şirkət hesabınıza daxil olun və layihələrinizi idarə edin.</Typography.Paragraph>
        {error && <Alert type="error" showIcon message={error} className="auth-alert" />}
        <Form layout="vertical" onFinish={onFinish}>
          <Form.Item name="email" label="Email" rules={[{ required: true, message: 'Email daxil edin' }]}>
            <Input prefix={<MailOutlined />} placeholder="admin@company.az" />
          </Form.Item>
          <Form.Item name="password" label="Şifrə" rules={[{ required: true, message: 'Şifrə daxil edin' }]}>
            <Input.Password prefix={<LockOutlined />} placeholder="Şifrə" />
          </Form.Item>
          <Button type="primary" htmlType="submit" loading={loading} block>
            Giriş
          </Button>
        </Form>
        <div className="auth-link-row">
          Hesabınız yoxdur? <Link to="/register">Qeydiyyatdan keçin</Link>
        </div>
      </Card>
    </div>
  )
}
