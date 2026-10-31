import { lazy, Suspense } from 'react'
import { useQuery } from '@tanstack/react-query'
import { getDashboard } from '../../lib/api/dashboard'
import { useAuth } from '../../lib/auth/AuthContext'
import { onboardingApi } from '../../lib/api/onboarding'
import { LoadError } from '../../components/LoadError'
import { fmtARS } from './dashboard/format'
import { KpiCard } from './dashboard/Widgets'
import { QuickActions } from './dashboard/QuickActions'
import { LiveOrders, TodayAppointmentsWidget } from './dashboard/OperationsWidgets'
import { CashWidget, HotQuotesWidget, LowStockWidget } from './dashboard/CommercialWidgets'
import { OnboardingCard } from './dashboard/OnboardingCard'
import { AlertIcon, BanknoteIcon, DocClockIcon, WalletIcon, WrenchIcon } from './dashboard/icons'

// recharts pesa: vive en su propio chunk y solo se baja al entrar al Inicio (D-41).
const RevenueChart = lazy(() => import('./dashboard/RevenueChart'))

/** El Inicio se refresca solo cada minuto y al volver a la pestaña («tiempo real» sin WebSockets). */
const REFRESH_MS = 60_000

function DashboardSkeleton() {
  return (
    <div className="space-y-6" role="status" aria-label="Cargando el Inicio">
      <div className="h-6 w-32 animate-pulse bg-slate-100" />
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        {[0, 1, 2, 3].map(i => (
          <div key={i} className="h-28 animate-pulse border border-tc-border bg-slate-50" />
        ))}
      </div>
      <div className="h-11 animate-pulse border border-tc-border bg-slate-50" />
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-5">
        <div className="h-64 animate-pulse border border-tc-border bg-slate-50 lg:col-span-3" />
        <div className="h-64 animate-pulse border border-tc-border bg-slate-50 lg:col-span-2" />
      </div>
    </div>
  )
}

export function DashboardPage() {
  const { user } = useAuth()
  const isOwner = user?.role === 'owner'

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['dashboard'],
    queryFn: getDashboard,
    refetchInterval: REFRESH_MS,
    refetchOnWindowFocus: true,
  })

  const { data: onboarding } = useQuery({
    queryKey: ['onboarding'],
    queryFn: onboardingApi.get,
    enabled: isOwner,
  })

  if (isLoading) return <DashboardSkeleton />

  if (error || !data) {
    return <LoadError what="el resumen del taller" onRetry={() => refetch()} />
  }

  const { thresholds, ordersByStatus: byStatus, agedReceivable: aged } = data
  const activeOrders = byStatus.pending + byStatus.inProgress + byStatus.waitingParts + byStatus.readyToDeliver
  const hotCount = data.hotQuotes?.total ?? 0

  return (
    <div className="space-y-6">
      <h1 className="text-lg font-bold tracking-wide text-tc-text uppercase">Inicio</h1>

      {/* Fila 1: los cuatro indicadores */}
      <div className="grid grid-cols-1 gap-3 min-[480px]:grid-cols-2 lg:grid-cols-4">
        <KpiCard
          label="Ingresos de hoy"
          value={fmtARS(data.revenueToday)}
          accent="green"
          icon={<BanknoteIcon />}
          stats={[
            { label: 'Cobros de hoy', value: data.paymentsToday },
            { label: 'Últimos 7 días', value: fmtARS(data.revenueLast7Days.reduce((sum, d) => sum + d.amount, 0)), tone: 'green' },
          ]}
        />
        <KpiCard
          label="Por cobrar"
          value={fmtARS(data.totalReceivable)}
          to="/app/work-orders?pendientes=1"
          accent={aged.count > 0 ? 'red' : 'slate'}
          icon={aged.count > 0 ? <AlertIcon /> : <WalletIcon />}
          bar={
            data.totalReceivable > 0
              ? [
                  { value: aged.amount, tone: 'red', label: 'Saldo atrasado' },
                  { value: Math.max(0, data.totalReceivable - aged.amount), tone: 'slate', label: 'Al día' },
                ]
              : undefined
          }
          stats={[
            { label: `Atrasado (+${thresholds.agedReceivableDays} días)`, value: fmtARS(aged.amount), tone: aged.count > 0 ? 'red' : 'slate' },
            { label: 'Órdenes atrasadas', value: aged.count, tone: aged.count > 0 ? 'red' : 'slate' },
          ]}
        />
        <KpiCard
          label="Órdenes activas"
          value={activeOrders}
          to="/app/work-orders"
          accent="blue"
          icon={<WrenchIcon />}
          bar={[
            { value: byStatus.pending, tone: 'slate', label: 'Pendientes' },
            { value: byStatus.inProgress, tone: 'blue', label: 'En proceso' },
            { value: byStatus.waitingParts, tone: 'amber', label: 'Esperando repuestos' },
            { value: byStatus.readyToDeliver, tone: 'green', label: 'Listas' },
          ]}
          stats={[
            { label: byStatus.waitingParts > 0 ? `En proceso · ${byStatus.waitingParts} esperando repuestos` : 'En proceso', value: byStatus.inProgress, tone: 'blue' },
            { label: 'Listas para entregar', value: byStatus.readyToDeliver, tone: 'green' },
          ]}
        />
        <KpiCard
          label="Presupuestos pendientes"
          value={data.sentQuotes}
          to="/app/quotes"
          accent="amber"
          icon={<DocClockIcon />}
          bar={[
            { value: hotCount, tone: 'amber', label: 'Sin respuesta' },
            { value: Math.max(0, data.sentQuotes - hotCount), tone: 'slate', label: 'Recientes' },
          ]}
          stats={[
            { label: 'En juego', value: fmtARS(data.sentQuotesTotal) },
            { label: `Sin respuesta +${thresholds.hotQuoteHours} h`, value: hotCount, tone: hotCount > 0 ? 'amber' : 'slate' },
          ]}
        />
      </div>

      {/* Fila 2: acciones rápidas */}
      <QuickActions />

      {/* Cuerpo: pulso operativo a la izquierda, comercial y alertas a la derecha */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-5">
        <div className="space-y-6 lg:col-span-3">
          <LiveOrders orders={data.liveOrders} stuckCount={byStatus.stuck} thresholds={thresholds} />
          {data.todayAppointments && <TodayAppointmentsWidget data={data.todayAppointments} />}
          {data.lowStock && <LowStockWidget items={data.lowStock} total={data.lowStockItems} />}
        </div>
        <div className="space-y-6 lg:col-span-2">
          {data.hotQuotes && <HotQuotesWidget data={data.hotQuotes} thresholds={thresholds} />}
          {data.cash && <CashWidget data={data.cash} />}
        </div>
      </div>

      {/* Pie: ingresos de la semana */}
      <Suspense fallback={<div className="h-72 animate-pulse border border-tc-border bg-slate-50" />}>
        <RevenueChart days={data.revenueLast7Days} />
      </Suspense>

      {onboarding && !onboarding.isComplete && <OnboardingCard onboarding={onboarding} />}
    </div>
  )
}
