import { MenuOutlined } from '@ant-design/icons'
import { Alert, Button, Drawer, Space, Spin } from 'antd'
import { useEffect, useState } from 'react'
import { Outlet, useLocation } from 'react-router-dom'
import { AiAssistant } from '../../features/aiAssistant/AiAssistant'
import { useAuthStore } from '../../features/auth/authStore'
import { useProjectProgressStore } from '../../features/projectProgress/projectProgressStore'
import { buildTrackBackendApi } from '../../services/api/buildTrackBackendApi'
import { useBuildTrackStore } from '../../services/data/dataService'
import { ALL_PROJECTS_ID, useProjectSelectionStore } from '../../stores/projectSelectionStore'
import { ApiConnectionStatus } from './ApiConnectionStatus'
import { Sidebar } from './Sidebar'

export const AppLayout = () => {
  const { data, initialized, loadData, loading } = useBuildTrackStore()
  const [mobileSidebarOpen, setMobileSidebarOpen] = useState(false)
  const location = useLocation()
  const objects = useProjectProgressStore((state) => state.objects)
  const hydrateTenantSitesFromBackend = useProjectProgressStore((state) => state.hydrateTenantSitesFromBackend)
  const loadProjectWorkspace = useProjectProgressStore((state) => state.loadFromBackend)
  const legacyLocalDataAvailable = useProjectProgressStore((state) => state.legacyLocalDataAvailable)
  const legacyLocalSummary = useProjectProgressStore((state) => state.legacyLocalSummary)
  const importLegacyLocalData = useProjectProgressStore((state) => state.importLegacyLocalData)
  const dismissLegacyLocalData = useProjectProgressStore((state) => state.dismissLegacyLocalData)
  const projectServerSyncStatus = useProjectProgressStore((state) => state.serverSyncStatus)
  const projectServerSyncError = useProjectProgressStore((state) => state.serverSyncError)
  const projectServerPendingSave = useProjectProgressStore((state) => state.serverPendingSave)
  const saveProjectWorkspace = useProjectProgressStore((state) => state.saveToBackend)
  const tenant = useAuthStore((state) => state.tenant)
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated)
  const selectedProjectId = useProjectSelectionStore((state) => state.selectedProjectId)
  const lastChangedAt = useProjectSelectionStore((state) => state.lastChangedAt)
  const projectContentKey = `${selectedProjectId}:${lastChangedAt}`
  const selectedProjectName = selectedProjectId === ALL_PROJECTS_ID
    ? 'Bütün layihələr'
    : objects.find((object) => object.id === selectedProjectId)?.name ?? 'Naməlum layihə'
  const showProjectDebug = import.meta.env.DEV || import.meta.env.VITE_PROJECT_STATE_DEBUG === 'true'

  useEffect(() => {
    void loadData()
  }, [loadData])

  useEffect(() => {
    document.body.style.removeProperty('overflow')
    document.body.style.removeProperty('overflow-y')
    document.documentElement.style.removeProperty('overflow')
    document.documentElement.style.removeProperty('overflow-y')
    window.scrollTo({ top: 0, left: 0 })
  }, [location.pathname])

  useEffect(() => {
    if (!isAuthenticated || !tenant) return
    let cancelled = false

    const loadWorkspaceOrSites = async () => {
      const loaded = await loadProjectWorkspace()
      if (loaded || cancelled) return
      try {
        const sites = await buildTrackBackendApi.getSites()
        if (!cancelled) hydrateTenantSitesFromBackend(sites, 'replace')
      } catch (error) {
        if (import.meta.env.DEV) console.warn('Tenant site sync failed', error)
      }
    }

    void loadWorkspaceOrSites()
    return () => {
      cancelled = true
    }
  }, [isAuthenticated, tenant?.id, tenant?.code, loadProjectWorkspace, hydrateTenantSitesFromBackend])

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
        <Button
          className="mobile-menu-toggle"
          icon={<MenuOutlined />}
          onClick={() => setMobileSidebarOpen(true)}
        >
          Menyu
        </Button>
        <ApiConnectionStatus />
        {projectServerSyncStatus === 'error' && projectServerPendingSave && (
          <Alert
            style={{ marginBottom: 12 }}
            type="error"
            showIcon
            message="Layihə dəyişiklikləri serverdə saxlanmadı"
            description={projectServerSyncError ?? 'Bağlantını yoxlayın və yenidən cəhd edin.'}
            action={<Button size="small" danger onClick={() => void saveProjectWorkspace()}>Yenidən saxla</Button>}
          />
        )}
        {legacyLocalDataAvailable && (
          <Alert
            style={{ marginBottom: 12 }}
            type="warning"
            showIcon
            message="Brauzerdə köhnə lokal layihə datası tapıldı"
            description={(
              <Space direction="vertical" size={8}>
                <span>{legacyLocalSummary}. Bu məlumatı server workspace-ə bir dəfə köçürə bilərsiniz.</span>
                <Space>
                  <Button size="small" type="primary" onClick={() => void importLegacyLocalData()}>Serverə köçür</Button>
                  <Button size="small" onClick={dismissLegacyLocalData}>Gizlət</Button>
                </Space>
              </Space>
            )}
          />
        )}
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
      <Drawer
        className="mobile-sidebar-drawer"
        open={mobileSidebarOpen}
        placement="left"
        width={300}
        closable={false}
        onClose={() => setMobileSidebarOpen(false)}
      >
        <Sidebar embedded onNavigate={() => setMobileSidebarOpen(false)} />
      </Drawer>
      <AiAssistant />
    </div>
  )
}
