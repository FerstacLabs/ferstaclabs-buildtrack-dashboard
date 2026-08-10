import { Navigate, createBrowserRouter } from 'react-router-dom'
import { AppLayout } from '../components/layout/AppLayout'
import { LicensePage } from '../features/auth/LicensePage'
import { AdminLicensesPage } from '../features/admin/AdminLicensesPage'
import { LoginPage } from '../features/auth/LoginPage'
import { PublicAuthPage, RequireAuth, RequireLogin } from '../features/auth/AuthGate'
import { RegisterPage } from '../features/auth/RegisterPage'
import { AttendanceLivePage } from '../features/attendanceLive/AttendanceLivePage'
import { CostCodePage } from '../features/costCode/CostCodePage'
import { CustomReportsPage } from '../features/customReports/CustomReportsPage'
import { DailyAttendancePage } from '../features/dailyAttendance/DailyAttendancePage'
import { DailyReportsPage } from '../features/dailyReports/DailyReportsPage'
import { DashboardPage } from '../features/dashboard/DashboardPage'
import { DelaysPermissionsPage } from '../features/delaysPermissions/DelaysPermissionsPage'
import { DevicesPage } from '../features/devices/DevicesPage'
import { ExportPage } from '../features/export/ExportPage'
import { ImportPage } from '../features/import/ImportPage'
import { MaterialsPage } from '../features/materials/MaterialsPage'
import { PayrollPage } from '../features/payroll/PayrollPage'
import { PerformancePage } from '../features/performance/PerformancePage'
import { ProcurementPage } from '../features/procurement/ProcurementPage'
import { ProjectCrewsPage } from '../features/projectProgress/ProjectCrewsPage'
import { ProjectEstimatePage } from '../features/projectProgress/ProjectEstimatePage'
import { ProjectTimelinePage } from '../features/projectProgress/ProjectTimelinePage'
import { RiskWorkersPage } from '../features/riskWorkers/RiskWorkersPage'
import { SecurityEventsPage } from '../features/securityEvents/SecurityEventsPage'
import { SettingsPage } from '../features/settings/SettingsPage'
import { SiteHoursPage } from '../features/siteHours/SiteHoursPage'
import { SupervisorAuditPage } from '../features/supervisorAudit/SupervisorAuditPage'
import { SupervisorsPage } from '../features/supervisors/SupervisorsPage'
import { WorkersPage } from '../features/workers/WorkersPage'
import { WarehousePage } from '../features/warehouse/WarehousePage'

export const router = createBrowserRouter([
  {
    path: '/login',
    element: <PublicAuthPage><LoginPage /></PublicAuthPage>,
  },
  {
    path: '/register',
    element: <PublicAuthPage><RegisterPage /></PublicAuthPage>,
  },
  {
    path: '/license',
    element: <RequireLogin><LicensePage /></RequireLogin>,
  },
  {
    path: '/',
    element: <RequireAuth><AppLayout /></RequireAuth>,
    children: [
      { index: true, element: <DashboardPage /> },
      { path: 'estimate', element: <ProjectEstimatePage /> },
      { path: 'crews', element: <ProjectCrewsPage /> },
      { path: 'workers', element: <WorkersPage /> },
      { path: 'timeline', element: <ProjectTimelinePage /> },
      { path: 'daily-reports', element: <DailyReportsPage /> },
      { path: 'materials', element: <MaterialsPage /> },
      { path: 'warehouse', element: <WarehousePage /> },
      { path: 'procurement', element: <ProcurementPage /> },
      { path: 'daily-attendance', element: <DailyAttendancePage /> },
      { path: 'attendance-live', element: <AttendanceLivePage /> },
      { path: 'site-hours', element: <SiteHoursPage /> },
      { path: 'risk-workers', element: <RiskWorkersPage /> },
      { path: 'delays-permissions', element: <DelaysPermissionsPage /> },
      { path: 'payroll', element: <PayrollPage /> },
      { path: 'performance', element: <PerformancePage /> },
      { path: 'supervisor-audit', element: <SupervisorAuditPage /> },
      { path: 'supervisors', element: <SupervisorsPage /> },
      { path: 'cost-code', element: <CostCodePage /> },
      { path: 'custom-reports', element: <CustomReportsPage /> },
      { path: 'export', element: <ExportPage /> },
      { path: 'devices', element: <DevicesPage /> },
      { path: 'admin/licenses', element: <AdminLicensesPage /> },
      { path: 'security-events', element: <SecurityEventsPage /> },
      { path: 'project-progress', element: <Navigate to="/" replace /> },
      { path: 'project-progress/estimate', element: <ProjectEstimatePage /> },
      { path: 'project-progress/crews', element: <ProjectCrewsPage /> },
      { path: 'project-progress/timeline', element: <ProjectTimelinePage /> },
      { path: 'settings', element: <SettingsPage /> },
      { path: 'import', element: <ImportPage /> },
    ],
  },
])
