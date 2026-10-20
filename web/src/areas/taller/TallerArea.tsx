import { lazy, Suspense } from 'react'
import { Route, Routes } from 'react-router'
import { RequireAuth } from '../../lib/auth/RequireAuth'
import { TallerLayout } from './TallerLayout'

const DashboardPage = lazy(() => import('./DashboardPage').then(m => ({ default: m.DashboardPage })))
const CustomersPage = lazy(() => import('./customers/CustomersPage').then(m => ({ default: m.CustomersPage })))
const VehicleDetailPage = lazy(() => import('./vehicles/VehicleDetailPage').then(m => ({ default: m.VehicleDetailPage })))
const VehiclesPage = lazy(() => import('./vehicles/VehiclesPage').then(m => ({ default: m.VehiclesPage })))
const CatalogPage = lazy(() => import('./catalog/CatalogPage').then(m => ({ default: m.CatalogPage })))
const WorkOrdersPage = lazy(() => import('./work-orders/WorkOrdersPage').then(m => ({ default: m.WorkOrdersPage })))
const WorkOrderDetailPage = lazy(() => import('./work-orders/WorkOrderDetailPage').then(m => ({ default: m.WorkOrderDetailPage })))
const QuotesPage = lazy(() => import('./quotes/QuotesPage').then(m => ({ default: m.QuotesPage })))
const QuoteDetailPage = lazy(() => import('./quotes/QuoteDetailPage').then(m => ({ default: m.QuoteDetailPage })))
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
const IvaBookPage = lazy(() => import('./invoicing/IvaBookPage').then(m => ({ default: m.IvaBookPage })))
const GeneralSettingsPage = lazy(() => import('./settings/general/GeneralSettingsPage').then(m => ({ default: m.GeneralSettingsPage })))
const UsersSettingsPage = lazy(() => import('./settings/users/UsersSettingsPage').then(m => ({ default: m.UsersSettingsPage })))

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
            <Route index element={<DashboardPage />} />
            <Route path="customers" element={<CustomersPage />} />
            <Route path="vehicles" element={<VehiclesPage />} />
            <Route path="vehicles/:id" element={<VehicleDetailPage />} />
            <Route path="catalog" element={<CatalogPage />} />
            <Route path="quotes" element={<QuotesPage />} />
            <Route path="quotes/:id" element={<QuoteDetailPage />} />
            <Route path="work-orders" element={<WorkOrdersPage />} />
            <Route path="work-orders/:id" element={<WorkOrderDetailPage />} />
            <Route path="appointments" element={<AppointmentsPage />} />
            <Route path="reminders" element={<RemindersPage />} />
            <Route path="qr" element={<QrCardsPage />} />
            <Route path="cash" element={<CashPage />} />
            <Route path="suppliers" element={<SuppliersPage />} />
            <Route path="purchases" element={<PurchasesPage />} />
            <Route path="counter-sales" element={<CounterSalesPage />} />
            <Route path="reports" element={<ReportsPage />} />
            <Route path="invoicing" element={<InvoicingPage />} />
            <Route path="invoicing/setup" element={<InvoicingSetupPage />} />
            <Route path="invoicing/iva-book" element={<IvaBookPage />} />
            <Route
              path="settings/general"
              element={
                <RequireAuth role="owner">
                  <GeneralSettingsPage />
                </RequireAuth>
              }
            />
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
