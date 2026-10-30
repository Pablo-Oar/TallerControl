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
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <KpiCard
          label="Ingresos de hoy"
          value={fmtARS(data.revenueToday)}
          tone={data.revenueToday > 0 ? 'good' : 'default'}
          sub={data.paymentsToday === 1 ? '1 cobro' : `${data.paymentsToday} cobros`}
        />
        <KpiCard
          label="Por cobrar"
          value={fmtARS(data.totalReceivable)}
          to="/app/work-orders?pendientes=1"
          tone={aged.count > 0 ? 'danger' : 'default'}
          sub={
            aged.count > 0 ? (
              <span className="font-semibold text-tc-stamp-red">
                {aged.count === 1 ? '1 orden' : `${aged.count} órdenes`} con saldo hace más de {thresholds.agedReceivableDays} días ({fmtARS(aged.amount)})
              </span>
            ) : (
              'Sin saldos atrasados'
            )
          }
        />
        <KpiCard
          label="Órdenes activas"
          value={activeOrders}
          to="/app/work-orders"
          sub={
            <>
              {byStatus.inProgress} en proceso ·{' '}
              <span className={byStatus.waitingParts > 0 ? 'font-semibold text-amber-700' : ''}>{byStatus.waitingParts} esperando repuestos</span>{' '}
              · {byStatus.readyToDeliver} listas
            </>
          }
        />
        <KpiCard
          label="Presupuestos pendientes"
          value={data.sentQuotes}
          to="/app/quotes"
          sub={
            <>
              {fmtARS(data.sentQuotesTotal)} en juego
              {hotCount > 0 && (
                <>
                  {' · '}
                  <span className="font-semibold text-amber-700">{hotCount} sin respuesta hace +{thresholds.hotQuoteHours} h</span>
                </>
              )}
            </>
          }
        />
      </div>

      {/* Fila 2: acciones rápidas */}
      <QuickActions />

      {/* Cuerpo: pulso operativo a la izquierda, comercial y alertas a la derecha */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-5">
        <div className="space-y-6 lg:col-span-3">
          <LiveOrders orders={data.liveOrders} stuckCount={byStatus.stuck} thresholds={thresholds} />
          {data.todayAppointments && <TodayAppointmentsWidget data={data.todayAppointments} />}
        </div>
        <div className="space-y-6 lg:col-span-2">
          {data.hotQuotes && <HotQuotesWidget data={data.hotQuotes} thresholds={thresholds} />}
          {data.cash && <CashWidget data={data.cash} />}
          {data.lowStock && <LowStockWidget items={data.lowStock} total={data.lowStockItems} />}
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
