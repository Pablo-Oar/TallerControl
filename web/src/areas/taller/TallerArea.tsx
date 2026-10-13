import { Route, Routes } from 'react-router'
import { RequireAuth } from '../../lib/auth/RequireAuth'
import { TallerLayout } from './TallerLayout'
import { UsersSettingsPage } from './settings/users/UsersSettingsPage'
import { GeneralSettingsPage } from './settings/general/GeneralSettingsPage'
import { CustomersPage } from './customers/CustomersPage'
import { VehiclesPage } from './vehicles/VehiclesPage'
import { CatalogPage } from './catalog/CatalogPage'

/** Área del taller (`/app/*`): dueño y administrativo. Escritorio primero. */
export function Component() {
  return (
    <RequireAuth>
      <TallerLayout>
        <Routes>
          <Route index element={<h1 className="text-2xl font-semibold">Inicio</h1>} />
          <Route path="customers" element={<CustomersPage />} />
          <Route path="vehicles" element={<VehiclesPage />} />
          <Route path="catalog" element={<CatalogPage />} />
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
