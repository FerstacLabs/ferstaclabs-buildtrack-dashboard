import { Spin } from 'antd'
import { useEffect } from 'react'
import { Outlet, useLocation } from 'react-router-dom'
import { AiAssistant } from '../../features/aiAssistant/AiAssistant'
import { useProjectProgressStore } from '../../features/projectProgress/projectProgressStore'
import { useBuildTrackStore } from '../../services/data/dataService'
import { ALL_PROJECTS_ID, useProjectSelectionStore } from '../../stores/projectSelectionStore'
import { ApiConnectionStatus } from './ApiConnectionStatus'
import { Sidebar } from './Sidebar'

export const AppLayout = () => {
  const { data, initialized, loadData, loading } = useBuildTrackStore()
  const location = useLocation()
  const objects = useProjectProgressStore((state) => state.objects)
  const selectedProjectId = useProjectSelectionStore((state) => state.selectedProjectId)
  const lastChangedAt = useProjectSelectionStore((state) => state.lastChangedAt)
  const projectContentKey = `${selectedProjectId}:${lastChangedAt}`
  const selectedProjectName = selectedProjectId === ALL_PROJECTS_ID
    ? 'Bütün obyektlər'
    : objects.find((object) => object.id === selectedProjectId)?.name ?? 'Naməlum obyekt'
  const showProjectDebug = import.meta.env.DEV || import.meta.env.VITE_PROJECT_STATE_DEBUG === 'true'

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
        {showProjectDebug && (
          <div
            style={{
              marginBottom: 8,
              padding: '6px 10px',
              border: '1px dashed #8fb8a8',
              borderRadius: 8,
              background: '#f3fbf8',
              color: '#275346',
              fontSize: 12,
            }}
          >
            Project debug: selectedProjectId={selectedProjectId}, name={selectedProjectName}, route={location.pathname}, changedAt={new Date(lastChangedAt).toLocaleTimeString()}
          </div>
        )}
        {data ? (
          <div key={projectContentKey} className="project-content-remount-boundary">
            <Outlet />
          </div>
        ) : <Spin size="large" />}
      </main>
      <AiAssistant />
    </div>
  )
}
