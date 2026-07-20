import { Spin } from 'antd'
import { useEffect } from 'react'
import { Outlet } from 'react-router-dom'
import { AiAssistant } from '../../features/aiAssistant/AiAssistant'
import { useBuildTrackStore } from '../../services/data/dataService'
import { ApiConnectionStatus } from './ApiConnectionStatus'
import { Sidebar } from './Sidebar'

export const AppLayout = () => {
  const { data, initialized, loadData, loading } = useBuildTrackStore()

  useEffect(() => {
    void loadData()
  }, [loadData])

  if (!initialized && loading) {
    return (
      <div className="initial-loader">
        <Spin size="large" />
        <span>BuildTrack məlumatları hazırlanır...</span>
      </div>
    )
  }

  return (
    <div className="app-shell">
      <Sidebar />
      <main className="app-main">
        <ApiConnectionStatus />
        {data ? <Outlet /> : <Spin size="large" />}
      </main>
      <AiAssistant />
    </div>
  )
}
