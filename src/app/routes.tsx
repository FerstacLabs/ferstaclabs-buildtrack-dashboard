import { createBrowserRouter } from 'react-router-dom'
import { AppLayout } from '../components/layout/AppLayout'
import { AttendanceLivePage } from '../features/attendanceLive/AttendanceLivePage'
import { CostCodePage } from '../features/costCode/CostCodePage'
import { CustomReportsPage } from '../features/customReports/CustomReportsPage'
import { DailyAttendancePage } from '../features/dailyAttendance/DailyAttendancePage'
import { DashboardPage } from '../features/dashboard/DashboardPage'
import { DelaysPermissionsPage } from '../features/delaysPermissions/DelaysPermissionsPage'
import { DevicesPage } from '../features/devices/DevicesPage'
import { ExportPage } from '../features/export/ExportPage'
import { ImportPage } from '../features/import/ImportPage'
import { PayrollPage } from '../features/payroll/PayrollPage'
import { PerformancePage } from '../features/performance/PerformancePage'
import { RiskWorkersPage } from '../features/riskWorkers/RiskWorkersPage'
import { SecurityEventsPage } from '../features/securityEvents/SecurityEventsPage'
import { SettingsPage } from '../features/settings/SettingsPage'
import { SiteHoursPage } from '../features/siteHours/SiteHoursPage'
import { SupervisorAuditPage } from '../features/supervisorAudit/SupervisorAuditPage'

export const router = createBrowserRouter([
  {
    path: '/',
    element: <AppLayout />,
    children: [
      { index: true, element: <DashboardPage /> },
      { path: 'daily-attendance', element: <DailyAttendancePage /> },
      { path: 'site-hours', element: <SiteHoursPage /> },
      { path: 'risk-workers', element: <RiskWorkersPage /> },
      { path: 'delays-permissions', element: <DelaysPermissionsPage /> },
      { path: 'payroll', element: <PayrollPage /> },
      { path: 'performance', element: <PerformancePage /> },
      { path: 'supervisor-audit', element: <SupervisorAuditPage /> },
      { path: 'cost-code', element: <CostCodePage /> },
      { path: 'custom-reports', element: <CustomReportsPage /> },
      { path: 'export', element: <ExportPage /> },
      { path: 'devices', element: <DevicesPage /> },
      { path: 'attendance-live', element: <AttendanceLivePage /> },
      { path: 'security-events', element: <SecurityEventsPage /> },
      { path: 'settings', element: <SettingsPage /> },
      { path: 'import', element: <ImportPage /> },
    ],
  },
])



