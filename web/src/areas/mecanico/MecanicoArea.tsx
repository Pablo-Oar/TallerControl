import { lazy, Suspense } from 'react'
import { PageSkeleton } from '@/components/ui/Loading'
import { OfflineBanner } from '../../components/OfflineBanner'
import { Route, Routes, useNavigate, useSearchParams } from 'react-router'
import { PlateSearchBar } from '../../components/PlateSearchBar'
import { PwaInstallBanner } from './PwaInstallBanner'
import { PushTestButton } from './PushTestButton'
import { Button } from '../../components/ui/Button'
import { useAuth } from '../../lib/auth/AuthContext'
import { RequireAuth } from '../../lib/auth/RequireAuth'

const MechanicWorkOrdersPage = lazy(() => import('./MechanicWorkOrdersPage').then(m => ({ default: m.MechanicWorkOrdersPage })))
const MechanicWorkOrderDetailPage = lazy(() => import('./MechanicWorkOrderDetailPage').then(m => ({ default: m.MechanicWorkOrderDetailPage })))

/** Área del mecánico (`/m/*`): celular primero. */
export function Component() {
  const navigate = useNavigate()
  const { logout } = useAuth()
  // El aviso de prueba es una herramienta de diagnóstico (soporte): solo aparece con `?diag=1`, no en el uso diario.
  const [params] = useSearchParams()
  const showDiagnostics = params.get('diag') === '1'

  async function handleLogout() {
    try {
      await logout()
    } finally {
      navigate('/login')
    }
  }
  // Sin sesión (o vencida) el portal no se muestra: va al login, como el panel del taller.
  return (
    <RequireAuth>
    <div className="min-h-screen bg-tc-surface text-tc-text">
      <header className="border-b border-tc-navy-mid bg-tc-navy">
        <div className="mx-auto flex h-[52px] max-w-6xl items-center justify-between gap-3 px-4">
          <span className="select-none text-sm font-bold tracking-widest text-white uppercase">
            TALLER<span className="text-tc-purple">CONTROL</span>
          </span>
          <div className="flex items-center gap-3">
            <PlateSearchBar onNavigate={navigate} target="/m" />
            <button
              type="button"
              onClick={handleLogout}
              className="hidden min-h-[40px] items-center gap-2 whitespace-nowrap rounded-lg border border-white/20 px-3 text-xs font-bold tracking-wider text-white/80 uppercase transition-colors hover:border-white/40 hover:bg-white/10 hover:text-white sm:inline-flex"
            >
              <svg className="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4M16 17l5-5-5-5M21 12H9" />
              </svg>
              Cerrar sesión
            </button>
          </div>
        </div>
      </header>
      <div className="mx-auto max-w-6xl">
        <OfflineBanner />
        <PwaInstallBanner />
      </div>
      <main className="relative mx-auto max-w-6xl p-4 sm:py-6">
        <Suspense fallback={<PageSkeleton />}>
          <Routes>
            <Route index element={<MechanicWorkOrdersPage />} />
            <Route path="work-orders/:id" element={<MechanicWorkOrderDetailPage />} />
          </Routes>
        </Suspense>
        <div className="mt-6 border-t border-tc-border pt-3 pb-4 sm:hidden">
          <Button variant="secondary" className="min-h-[48px] w-full rounded-lg" onClick={handleLogout}>
            Cerrar sesión
          </Button>
          {showDiagnostics && <PushTestButton />}
        </div>
      </main>
    </div>
    </RequireAuth>
  )
}
