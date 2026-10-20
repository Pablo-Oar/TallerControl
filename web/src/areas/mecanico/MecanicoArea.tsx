import { lazy, Suspense } from 'react'
import { Route, Routes, useNavigate } from 'react-router'
import { PlateSearchBar } from '../../components/PlateSearchBar'
import { PwaInstallBanner } from './PwaInstallBanner'
import { Button } from '../../components/ui/Button'
import { useAuth } from '../../lib/auth/AuthContext'

const MechanicWorkOrdersPage = lazy(() => import('./MechanicWorkOrdersPage').then(m => ({ default: m.MechanicWorkOrdersPage })))
const MechanicWorkOrderDetailPage = lazy(() => import('./MechanicWorkOrderDetailPage').then(m => ({ default: m.MechanicWorkOrderDetailPage })))

/** Área del mecánico (`/m/*`): celular primero. */
export function Component() {
  const navigate = useNavigate()
  const { logout } = useAuth()

  async function handleLogout() {
    try {
      await logout()
    } finally {
      navigate('/login')
    }
  }
  return (
    <div className="min-h-screen bg-tc-surface text-tc-text">
      <header className="bg-tc-navy px-4 h-[52px] flex items-center justify-between border-b border-tc-navy-mid">
        <span className="select-none text-sm font-bold tracking-widest text-white uppercase">
          TALLER<span className="text-tc-purple">CONTROL</span>
        </span>
        <PlateSearchBar onNavigate={navigate} target="/m" />
      </header>
      <PwaInstallBanner />
      <main className="mx-auto max-w-lg p-4">
        <Suspense fallback={
          <div className="flex items-center justify-center py-16">
            <div className="h-5 w-5 animate-spin border-2 border-tc-purple border-t-transparent" />
          </div>
        }>
          <Routes>
            <Route index element={<MechanicWorkOrdersPage />} />
            <Route path="work-orders/:id" element={<MechanicWorkOrderDetailPage />} />
          </Routes>
        </Suspense>
        <div className="mt-8 border-t border-tc-border pt-4">
          <Button variant="secondary" size="sm" onClick={handleLogout}>
            Cerrar sesión
          </Button>
        </div>
      </main>
    </div>
  )
}
