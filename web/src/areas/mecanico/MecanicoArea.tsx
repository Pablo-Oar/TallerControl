import { Route, Routes } from 'react-router'
import { MechanicWorkOrdersPage } from './MechanicWorkOrdersPage'
import { MechanicWorkOrderDetailPage } from './MechanicWorkOrderDetailPage'
import { PlateSearchBar } from '../../components/PlateSearchBar'

/** Área del mecánico (`/m/*`): celular primero. */
export function Component() {
  return (
    <div className="min-h-screen bg-slate-50 text-slate-900">
      <header className="border-b border-slate-200 bg-white px-4 py-2 flex items-center justify-end">
        <PlateSearchBar />
      </header>
      <main className="mx-auto max-w-lg p-4">
        <Routes>
          <Route index element={<MechanicWorkOrdersPage />} />
          <Route path="work-orders/:id" element={<MechanicWorkOrderDetailPage />} />
        </Routes>
      </main>
    </div>
  )
}
