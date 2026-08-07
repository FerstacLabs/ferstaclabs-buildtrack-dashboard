import { createBrowserRouter } from 'react-router-dom'
import { PublicAuthPage, RequireAuth } from '../features/auth/AuthGate'
import { LoginPage } from '../features/auth/LoginPage'
import { FieldDashboardPage } from '../features/fieldPortal/FieldDashboardPage'
import { FieldDailyReportsPage } from '../features/fieldPortal/FieldDailyReportsPage'
import { FieldLayout } from '../features/fieldPortal/FieldLayout'
import { FieldNotificationsPage } from '../features/fieldPortal/FieldNotificationsPage'
import { FieldSettingsPage } from '../features/fieldPortal/FieldSettingsPage'
import { FieldSiteNotesPage } from '../features/fieldPortal/FieldSiteNotesPage'
import { FieldWarehouseRequestsPage } from '../features/fieldPortal/FieldWarehouseRequestsPage'
import { FieldWorkersPage } from '../features/fieldPortal/FieldWorkersPage'

export const fieldRouter = createBrowserRouter([
  {
    path: '/login',
    element: <PublicAuthPage><LoginPage /></PublicAuthPage>,
  },
  {
    path: '/',
    element: <RequireAuth><FieldLayout /></RequireAuth>,
    children: [
      { index: true, element: <FieldDashboardPage /> },
      { path: 'reports', element: <FieldDailyReportsPage /> },
      { path: 'workers', element: <FieldWorkersPage /> },
      { path: 'warehouse', element: <FieldWarehouseRequestsPage /> },
      { path: 'notes', element: <FieldSiteNotesPage /> },
      { path: 'notifications', element: <FieldNotificationsPage /> },
      { path: 'settings', element: <FieldSettingsPage /> },
    ],
  },
])
