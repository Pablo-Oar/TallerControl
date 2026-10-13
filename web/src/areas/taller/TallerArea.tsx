import { Route, Routes } from 'react-router'
import { RequireAuth } from '../../lib/auth/RequireAuth'
import { TallerLayout } from './TallerLayout'
import { UsersSettingsPage } from './settings/users/UsersSettingsPage'

/** Área del taller (`/app/*`): dueño y administrativo. Escritorio primero. */
export function Component() {
  return (
    <RequireAuth>
      <TallerLayout>
        <Routes>
          <Route index element={<h1 className="text-2xl font-semibold">Inicio</h1>} />
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
