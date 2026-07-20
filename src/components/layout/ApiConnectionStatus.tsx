import { CheckCircleOutlined, SyncOutlined } from '@ant-design/icons'
import { Tag, Tooltip } from 'antd'
import { useEffect, useState } from 'react'
import { API_BASE_URL_SOURCE, tryApiRequest } from '../../shared/api/client'

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

  if (status === 'connected') {
    return (
      <div className="api-status-row connected">
        <Tag color="success" icon={<CheckCircleOutlined />}>Backend bağlantısı aktivdir</Tag>
      </div>
    )
  }

  return (
    <div className="api-status-row connected">
      <Tooltip title={API_BASE_URL_SOURCE === 'VITE_API_BASE_URL' ? 'Backend cavabı gözlənilir' : 'Backend ünvanı Vercel mühit dəyişəni ilə verilə bilər'}>
        <Tag color={status === 'checking' ? 'processing' : 'default'} icon={<SyncOutlined spin={status === 'checking'} />}>
          {status === 'checking' ? 'Sinxronizasiya yoxlanılır' : 'Yaddaş aktivdir'}
        </Tag>
      </Tooltip>
    </div>
  )
}
