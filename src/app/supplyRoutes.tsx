import { createBrowserRouter } from 'react-router-dom'
import { PublicAuthPage, RequireAuth } from '../features/auth/AuthGate'
import { LoginPage } from '../features/auth/LoginPage'
import { SupplyDashboardPage } from '../features/supplyPortal/SupplyDashboardPage'
import { SupplyHistoryPage } from '../features/supplyPortal/SupplyHistoryPage'
import { SupplyLayout } from '../features/supplyPortal/SupplyLayout'
import { SupplyNotificationsPage } from '../features/supplyPortal/SupplyNotificationsPage'
import { SupplySettingsPage } from '../features/supplyPortal/SupplySettingsPage'
import { SupplyTaskDetailPage } from '../features/supplyPortal/SupplyTaskDetailPage'
import { SupplyTasksPage } from '../features/supplyPortal/SupplyTasksPage'

export const supplyRouter = createBrowserRouter([
  {
    path: '/login',
    element: <PublicAuthPage><LoginPage /></PublicAuthPage>,
  },
  {
    path: '/',
    element: <RequireAuth><SupplyLayout /></RequireAuth>,
    children: [
      { index: true, element: <SupplyDashboardPage /> },
      { path: 'tasks', element: <SupplyTasksPage /> },
      { path: 'tasks/:id', element: <SupplyTaskDetailPage /> },
      { path: 'history', element: <SupplyHistoryPage /> },
      { path: 'notifications', element: <SupplyNotificationsPage /> },
      { path: 'settings', element: <SupplySettingsPage /> },
    ],
  },
])
