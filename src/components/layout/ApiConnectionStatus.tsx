import { CheckCircleOutlined, DisconnectOutlined } from '@ant-design/icons'
import { Alert, Tag } from 'antd'
import { useEffect, useState } from 'react'
import { API_BASE_URL, API_BASE_URL_SOURCE, tryApiRequest } from '../../shared/api/client'

type ApiStatus = 'checking' | 'connected' | 'unavailable'

export const ApiConnectionStatus = () => {
  const [status, setStatus] = useState<ApiStatus>('checking')

  useEffect(() => {
    let mounted = true
    const checkHealth = async () => {
      const result = await tryApiRequest<{ status?: string }>('/api/health')
      if (!mounted) return
      setStatus(result?.status === 'ok' ? 'connected' : 'unavailable')
    }

    void checkHealth()
    const timer = window.setInterval(() => void checkHealth(), 60_000)
    return () => {
      mounted = false
      window.clearInterval(timer)
    }
  }, [])

  if (status === 'checking') {
    return (
      <div className="api-status-row">
        <Tag color="processing">Backend yoxlanılır</Tag>
        <span>{API_BASE_URL}</span>
      </div>
    )
  }

  if (status === 'connected') {
    return (
      <div className="api-status-row connected">
        <Tag color="success" icon={<CheckCircleOutlined />}>Backend qoşulub</Tag>
        <span>{API_BASE_URL}</span>
      </div>
    )
  }

  return (
    <Alert
      className="api-status-alert"
      type="warning"
      showIcon
      icon={<DisconnectOutlined />}
      message="Backend əlçatan deyil, demo/lokal məlumatlar istifadə olunur"
      description={`API: ${API_BASE_URL}. Mənbə: ${API_BASE_URL_SOURCE}. Vercel HTTPS deploy üçün VITE_API_BASE_URL mütləq HTTPS API ünvanına verilməlidir.`}
    />
  )
}
