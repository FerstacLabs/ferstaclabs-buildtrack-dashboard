import { Spin } from 'antd'
import type { ReactNode } from 'react'
import { useEffect } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { fieldPortalUrl, getHostMode, managementAppUrl } from '../../app/hostMode'
import { useAuthStore } from './authStore'

const AuthLoader = () => (
  <div className="auth-loader">
    <Spin size="large" />
    <span>BuildTrack hesabı yoxlanılır...</span>
  </div>
)

export const RequireAuth = ({ children }: { children: ReactNode }) => {
  const location = useLocation()
  const { initialized, loading, isAuthenticated, hasActiveLicense, loadMe, user } = useAuthStore()
  const hostMode = getHostMode()

  useEffect(() => {
    if (!initialized && !loading) void loadMe()
  }, [initialized, loading, loadMe])

  if (!initialized || loading) return <AuthLoader />
  if (!isAuthenticated) return <Navigate to="/login" replace state={{ from: location.pathname }} />
  if (!hasActiveLicense) return <Navigate to="/license" replace />
  if (hostMode === 'ManagementApp' && user?.role === 'Supervisor') {
    window.location.assign(fieldPortalUrl())
    return <AuthLoader />
  }
  if (hostMode === 'FieldPortal' && user && !['Supervisor', 'Owner', 'Admin', 'Manager'].includes(user.role)) {
    window.location.assign(managementAppUrl())
    return <AuthLoader />
  }

  return children
}

export const RequireLogin = ({ children }: { children: ReactNode }) => {
  const { initialized, loading, isAuthenticated, loadMe } = useAuthStore()

  useEffect(() => {
    if (!initialized && !loading) void loadMe()
  }, [initialized, loading, loadMe])

  if (!initialized || loading) return <AuthLoader />
  if (!isAuthenticated) return <Navigate to="/login" replace />

  return children
}

export const PublicAuthPage = ({ children }: { children: ReactNode }) => {
  const { initialized, loading, isAuthenticated, hasActiveLicense, loadMe, user } = useAuthStore()
  const hostMode = getHostMode()

  useEffect(() => {
    if (!initialized && !loading) void loadMe()
  }, [initialized, loading, loadMe])

  if (!initialized || loading) return <AuthLoader />
  if (isAuthenticated) {
    if (hostMode === 'ManagementApp' && user?.role === 'Supervisor') {
      window.location.assign(fieldPortalUrl())
      return <AuthLoader />
    }
    return <Navigate to={hasActiveLicense ? '/' : '/license'} replace />
  }

  return children
}
