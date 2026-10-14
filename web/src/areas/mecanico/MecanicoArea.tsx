import { Route, Routes } from 'react-router'
import { MechanicWorkOrdersPage } from './MechanicWorkOrdersPage'
import { MechanicWorkOrderDetailPage } from './MechanicWorkOrderDetailPage'

/** Área del mecánico (`/m/*`): celular primero. */
export function Component() {
  return (
    <div className="min-h-screen bg-slate-50 text-slate-900">
      <main className="mx-auto max-w-lg p-4">
        <Routes>
          <Route index element={<MechanicWorkOrdersPage />} />
          <Route path="work-orders/:id" element={<MechanicWorkOrderDetailPage />} />
        </Routes>
      </main>
    </div>
  )
}
