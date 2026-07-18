import { Alert, Spin } from 'antd'
import { useEffect } from 'react'
import { Outlet } from 'react-router-dom'
import { useBuildTrackStore } from '../../services/data/dataService'
import { ApiConnectionStatus } from './ApiConnectionStatus'
import { Sidebar } from './Sidebar'

export const AppLayout = () => {
  const { data, error, initialized, loadData, loading } = useBuildTrackStore()

  useEffect(() => {
    void loadData()
  }, [loadData])

  if (!initialized && loading) {
    return (
      <div className="initial-loader">
        <Spin size="large" />
        <span>BuildTrack demo məlumatları hazırlanır...</span>
      </div>
    )
  }

  return (
    <div className="app-shell">
      <Sidebar />
      <main className="app-main">
        <ApiConnectionStatus />
        {error ? <Alert type="warning" showIcon message="Demo məlumatları lokal rejimdə açıldı" description={error} /> : null}
        {data ? <Outlet /> : <Spin size="large" />}
      </main>
    </div>
  )
}
