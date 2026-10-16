import { Route, Routes } from 'react-router'
import { RequireAuth } from '../../lib/auth/RequireAuth'
import { TallerLayout } from './TallerLayout'
import { UsersSettingsPage } from './settings/users/UsersSettingsPage'
import { GeneralSettingsPage } from './settings/general/GeneralSettingsPage'
import { CustomersPage } from './customers/CustomersPage'
import { VehiclesPage } from './vehicles/VehiclesPage'
import { CatalogPage } from './catalog/CatalogPage'
import { WorkOrdersPage } from './work-orders/WorkOrdersPage'
import { WorkOrderDetailPage } from './work-orders/WorkOrderDetailPage'
import { QuotesPage } from './quotes/QuotesPage'
import { QuoteDetailPage } from './quotes/QuoteDetailPage'
import { QrCardsPage } from './qr/QrCardsPage'
import { DashboardPage } from './DashboardPage'
import { CashPage } from './cash/CashPage'
import { SuppliersPage } from './suppliers/SuppliersPage'
import { PurchasesPage } from './purchases/PurchasesPage'
import { CounterSalesPage } from './counter-sales/CounterSalesPage'
import { InvoicingPage } from './invoicing/InvoicingPage'
import { InvoicingSetupPage } from './invoicing/InvoicingSetupPage'
import { IvaBookPage } from './invoicing/IvaBookPage'
import { RemindersPage } from './reminders/RemindersPage'

/** Área del taller (`/app/*`): dueño y administrativo. Escritorio primero. */
export function Component() {
  return (
    <RequireAuth>
      <TallerLayout>
        <Routes>
          <Route index element={<DashboardPage />} />
          <Route path="customers" element={<CustomersPage />} />
          <Route path="vehicles" element={<VehiclesPage />} />
          <Route path="catalog" element={<CatalogPage />} />
          <Route path="quotes" element={<QuotesPage />} />
          <Route path="quotes/:id" element={<QuoteDetailPage />} />
          <Route path="work-orders" element={<WorkOrdersPage />} />
          <Route path="work-orders/:id" element={<WorkOrderDetailPage />} />
          <Route path="reminders" element={<RemindersPage />} />
          <Route path="qr" element={<QrCardsPage />} />
          <Route path="cash" element={<CashPage />} />
          <Route path="suppliers" element={<SuppliersPage />} />
          <Route path="purchases" element={<PurchasesPage />} />
          <Route path="counter-sales" element={<CounterSalesPage />} />
          <Route path="invoicing" element={<InvoicingPage />} />
          <Route path="invoicing/setup" element={<InvoicingSetupPage />} />
          <Route path="invoicing/iva-book" element={<IvaBookPage />} />
          <Route
            path="settings/general"
            element={
              <RequireAuth role="Owner">
                <GeneralSettingsPage />
              </RequireAuth>
            }
          />
          <Route
            path="settings/users"
            element={
              <RequireAuth role="Owner">
                <UsersSettingsPage />
              </RequireAuth>
            }
          />
        </Routes>
      </TallerLayout>
    </RequireAuth>
  )
}
