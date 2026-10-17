import { Route, Routes } from 'react-router'
import { MechanicWorkOrdersPage } from './MechanicWorkOrdersPage'
import { MechanicWorkOrderDetailPage } from './MechanicWorkOrderDetailPage'
import { PlateSearchBar } from '../../components/PlateSearchBar'
import { PwaInstallBanner } from './PwaInstallBanner'

/** Área del mecánico (`/m/*`): celular primero. */
export function Component() {
  return (
    <div className="min-h-screen bg-tc-surface text-slate-900">
      <header className="bg-tc-navy px-4 h-[52px] flex items-center justify-between border-b border-tc-navy-mid">
        <span className="select-none text-sm font-bold tracking-widest text-white uppercase">
          TALLER<span className="text-tc-purple">CONTROL</span>
        </span>
        <PlateSearchBar />
      </header>
      <PwaInstallBanner />
      <main className="mx-auto max-w-lg p-4">
        <Routes>
          <Route index element={<MechanicWorkOrdersPage />} />
          <Route path="work-orders/:id" element={<MechanicWorkOrderDetailPage />} />
        </Routes>
      </main>
    </div>
  )
}
