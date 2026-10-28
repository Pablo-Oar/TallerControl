import type { ReactNode } from 'react'
import { lazy, Suspense } from 'react'
import { Navigate, Route, Routes } from 'react-router'
import { RequireAuth } from '../../lib/auth/RequireAuth'
import { TallerLayout, firstAllowedPath } from './TallerLayout'
import { useAuth } from '../../lib/auth/AuthContext'
import { hasPermissions } from '../../lib/auth/permissions'

const DashboardPage = lazy(() => import('./DashboardPage').then(m => ({ default: m.DashboardPage })))
const CustomerDetailPage = lazy(() => import('./customers/CustomerDetailPage').then(m => ({ default: m.CustomerDetailPage })))
const CustomersPage = lazy(() => import('./customers/CustomersPage').then(m => ({ default: m.CustomersPage })))
const VehicleDetailPage = lazy(() => import('./vehicles/VehicleDetailPage').then(m => ({ default: m.VehicleDetailPage })))
const VehiclesPage = lazy(() => import('./vehicles/VehiclesPage').then(m => ({ default: m.VehiclesPage })))
const CatalogPage = lazy(() => import('./catalog/CatalogPage').then(m => ({ default: m.CatalogPage })))
const WorkOrdersPage = lazy(() => import('./work-orders/WorkOrdersPage').then(m => ({ default: m.WorkOrdersPage })))
const WorkOrderDetailPage = lazy(() => import('./work-orders/WorkOrderDetailPage').then(m => ({ default: m.WorkOrderDetailPage })))
const QuotesPage = lazy(() => import('./quotes/QuotesPage').then(m => ({ default: m.QuotesPage })))
const QuoteDetailPage = lazy(() => import('./quotes/QuoteDetailPage').then(m => ({ default: m.QuoteDetailPage })))
const AppointmentCalendarPage = lazy(() => import('./appointments/AppointmentCalendarPage').then(m => ({ default: m.AppointmentCalendarPage })))
const AvailabilityPage = lazy(() => import('./appointments/AvailabilityPage').then(m => ({ default: m.AvailabilityPage })))
const AppointmentsPage = lazy(() => import('./appointments/AppointmentsPage').then(m => ({ default: m.AppointmentsPage })))
const RemindersPage = lazy(() => import('./reminders/RemindersPage').then(m => ({ default: m.RemindersPage })))
const QrCardsPage = lazy(() => import('./qr/QrCardsPage').then(m => ({ default: m.QrCardsPage })))
const CashPage = lazy(() => import('./cash/CashPage').then(m => ({ default: m.CashPage })))
const SuppliersPage = lazy(() => import('./suppliers/SuppliersPage').then(m => ({ default: m.SuppliersPage })))
const PurchasesPage = lazy(() => import('./purchases/PurchasesPage').then(m => ({ default: m.PurchasesPage })))
const CounterSalesPage = lazy(() => import('./counter-sales/CounterSalesPage').then(m => ({ default: m.CounterSalesPage })))
const ReportsPage = lazy(() => import('./reports/ReportsPage').then(m => ({ default: m.ReportsPage })))
const InvoicingPage = lazy(() => import('./invoicing/InvoicingPage').then(m => ({ default: m.InvoicingPage })))
const InvoicingSetupPage = lazy(() => import('./invoicing/InvoicingSetupPage').then(m => ({ default: m.InvoicingSetupPage })))
const InvoiceDetailPage = lazy(() => import('./invoicing/InvoiceDetailPage').then(m => ({ default: m.InvoiceDetailPage })))
const IvaBookPage = lazy(() => import('./invoicing/IvaBookPage').then(m => ({ default: m.IvaBookPage })))
const GeneralSettingsPage = lazy(() => import('./settings/general/GeneralSettingsPage').then(m => ({ default: m.GeneralSettingsPage })))
const ProfilePage = lazy(() => import('./settings/profile/ProfilePage').then(m => ({ default: m.ProfilePage })))
const UsersSettingsPage = lazy(() => import('./settings/users/UsersSettingsPage').then(m => ({ default: m.UsersSettingsPage })))

/** Inicio del panel: el resumen si la sesión puede verlo; si no (p. ej. Contador), su primera pantalla permitida. */
function AppHome() {
  const { user } = useAuth()
  if (!hasPermissions(user, 'workorders.read', 'payments.read')) {
    return <Navigate to={firstAllowedPath(user)} replace />
  }
  return <DashboardPage />
}

/** Ruta del panel visible solo con los permisos de la sección (la API igual responde 403). */
function Need({ perms, children }: { perms: string[]; children: ReactNode }) {
  const { user } = useAuth()
  if (!hasPermissions(user, ...perms)) return <Navigate to="/app" replace />
  return <>{children}</>
}

function PageFallback() {
  return (
    <div className="flex items-center justify-center py-16">
      <div className="h-5 w-5 animate-spin border-2 border-tc-purple border-t-transparent" />
    </div>
  )
}

/** Área del taller (`/app/*`): dueño y administrativo. Escritorio primero. */
export function Component() {
  return (
    <RequireAuth area="app">
      <TallerLayout>
        <Suspense fallback={<PageFallback />}>
          <Routes>
            <Route index element={<AppHome />} />
            <Route path="customers" element={<Need perms={['customers.read']}><CustomersPage /></Need>} />
            <Route path="customers/:id" element={<Need perms={['customers.read']}><CustomerDetailPage /></Need>} />
            <Route path="vehicles" element={<Need perms={['vehicles.read']}><VehiclesPage /></Need>} />
            <Route path="vehicles/:id" element={<Need perms={['vehicles.read']}><VehicleDetailPage /></Need>} />
            <Route path="catalog" element={<Need perms={['catalog.read']}><CatalogPage /></Need>} />
            <Route path="quotes" element={<Need perms={['quotes.read']}><QuotesPage /></Need>} />
            <Route path="quotes/:id" element={<Need perms={['quotes.read']}><QuoteDetailPage /></Need>} />
            <Route path="work-orders" element={<Need perms={['workorders.read']}><WorkOrdersPage /></Need>} />
            <Route path="work-orders/:id" element={<Need perms={['workorders.read']}><WorkOrderDetailPage /></Need>} />
            <Route path="appointments" element={<Need perms={['appointments.read']}><AppointmentsPage /></Need>} />
            <Route path="appointments/calendar" element={<Need perms={['appointments.read']}><AppointmentCalendarPage /></Need>} />
            <Route path="appointments/schedule" element={<Need perms={['appointments.read']}><AvailabilityPage /></Need>} />
            <Route path="reminders" element={<Need perms={['reminders.manage']}><RemindersPage /></Need>} />
            <Route path="qr" element={<Need perms={['qr.manage']}><QrCardsPage /></Need>} />
            <Route path="cash" element={<Need perms={['cash.read']}><CashPage /></Need>} />
            <Route path="suppliers" element={<Need perms={['suppliers.read']}><SuppliersPage /></Need>} />
            <Route path="purchases" element={<Need perms={['purchases.read']}><PurchasesPage /></Need>} />
            <Route path="counter-sales" element={<Need perms={['counter_sales.read']}><CounterSalesPage /></Need>} />
            <Route path="reports" element={<Need perms={['workorders.read', 'payments.read']}><ReportsPage /></Need>} />
            <Route path="invoicing" element={<Need perms={['invoicing.read']}><InvoicingPage /></Need>} />
            <Route path="invoicing/setup" element={<Need perms={['invoicing.read']}><InvoicingSetupPage /></Need>} />
            <Route path="invoicing/invoices/:id" element={<Need perms={['invoicing.read']}><InvoiceDetailPage /></Need>} />
            <Route path="invoicing/iva-book" element={<Need perms={['invoicing.read']}><IvaBookPage /></Need>} />
            <Route
              path="settings/general"
              element={
                <RequireAuth role="owner">
                  <GeneralSettingsPage />
                </RequireAuth>
              }
            />
            <Route path="settings/profile" element={<ProfilePage />} />
            <Route path="*" element={<Navigate to="/app" replace />} />
            <Route
              path="settings/users"
              element={
                <RequireAuth role="owner">
                  <UsersSettingsPage />
                </RequireAuth>
              }
            />
          </Routes>
        </Suspense>
      </TallerLayout>
    </RequireAuth>
  )
}
