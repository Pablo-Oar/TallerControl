import { useQuery } from '@tanstack/react-query'
import { getDashboard } from '../../lib/api/dashboard'
import { Link } from 'react-router'
import { useAuth } from '../../lib/auth/AuthContext'
import { onboardingApi } from '../../lib/api/onboarding'

interface KpiStubProps {
  label: string
  value: string | number
  to?: string
  alert?: boolean
  money?: boolean
}

function KpiStub({ label, value, to, alert, money }: KpiStubProps) {
  const inner = (
    <div className="flex overflow-hidden border border-tc-border bg-white hover:border-tc-border-mid transition-colors group">
      {/* Stub column */}
      <div className="w-2 flex-none bg-tc-stub-bg" />
      {/* Perforation */}
      <div className="perf-line flex-none self-stretch" />
      {/* Body */}
      <div className="flex flex-1 flex-col gap-2 px-4 py-4">
        <span className="text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">
          {label}
        </span>
        <span
          className={[
            'tabular-nums text-2xl font-bold leading-none',
            money ? 'text-tc-purple' : alert ? 'text-amber-600' : 'text-tc-text',
          ].join(' ')}
        >
          {value}
        </span>
      </div>
      {/* Right corner tab */}
      {to && (
        <div className="flex w-8 flex-none items-center justify-center self-stretch bg-tc-stub-bg border-l border-tc-border text-tc-navy-text-dim group-hover:text-tc-navy-text transition-colors">
          <svg className="h-3 w-3" viewBox="0 0 12 12" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden>
            <path d="M2 6h8M6 2l4 4-4 4" strokeLinecap="round" strokeLinejoin="round" />
          </svg>
        </div>
      )}
    </div>
  )

  return to ? (
    <Link to={to} className="block focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-tc-purple focus-visible:ring-offset-1">
      {inner}
    </Link>
  ) : (
    inner
  )
}

export function DashboardPage() {
  const { user } = useAuth()
  const isOwner = user?.role === 'owner'

  const { data, isLoading, error } = useQuery({
    queryKey: ['dashboard'],
    queryFn: getDashboard,
  })

  const { data: onboarding } = useQuery({
    queryKey: ['onboarding'],
    queryFn: onboardingApi.get,
    enabled: isOwner,
  })

  if (isLoading) {
    return (
      <div className="space-y-6">
        <div className="h-6 w-32 animate-pulse bg-slate-100" />
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          {[0, 1, 2, 3].map(i => (
            <div key={i} className="h-24 animate-pulse bg-slate-50 border border-tc-border" />
          ))}
        </div>
      </div>
    )
  }

  if (error) {
    return (
      <p className="border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
        No se pudo cargar el resumen del taller.
      </p>
    )
  }

  const totalFormatted = new Intl.NumberFormat('es-AR', {
    style: 'currency',
    currency: 'ARS',
    maximumFractionDigits: 0,
  }).format(data!.totalReceivable)

  const pct = onboarding
    ? Math.round((onboarding.completedCount / onboarding.totalCount) * 100)
    : 0

  return (
    <div className="space-y-8">
      <h1 className="text-lg font-bold tracking-wide text-tc-text uppercase">Inicio</h1>

      {/* KPI row — coupon stubs */}
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <KpiStub label="Por cobrar" value={totalFormatted} money />
        <KpiStub label="OTs abiertas" value={data!.openWorkOrders} to="/app/work-orders" />
        <KpiStub label="Presupuestos pendientes" value={data!.pendingQuotes} to="/app/quotes" />
        <KpiStub
          label="Stock bajo"
          value={data!.lowStockItems}
          to="/app/catalog"
          alert={data!.lowStockItems > 0}
        />
      </div>

      {/* Onboarding checklist — owner only, while incomplete */}
      {onboarding && !onboarding.isComplete && (
        <div className="border border-tc-border bg-white overflow-hidden">
          {/* Header stub */}
          <div className="flex items-center justify-between border-b border-tc-border bg-tc-stub-bg px-4 py-3">
            <span className="text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-secondary">
              Guía de inicio
            </span>
            <span className="text-[10px] font-bold tracking-wider text-tc-purple tabular-nums">
              {onboarding.completedCount}/{onboarding.totalCount} · {pct}%
            </span>
          </div>

          {/* Progress bar */}
          <div className="h-0.5 w-full bg-tc-border">
            <div
              className="h-0.5 bg-tc-purple transition-all"
              style={{ width: `${pct}%` }}
            />
          </div>

          {/* Steps */}
          <ul className="divide-y divide-tc-border">
            {onboarding.steps.map(step => (
              <li key={step.key} className="flex items-center gap-3 px-4 py-2.5">
                {step.completed ? (
                  <svg className="h-4 w-4 flex-none text-tc-done-text" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden>
                    <path d="M3 8l3.5 3.5L13 5" strokeLinecap="round" strokeLinejoin="round" />
                  </svg>
                ) : (
                  <span className="h-4 w-4 flex-none rounded-full border-2 border-tc-border-mid" aria-hidden />
                )}
                <span className={[
                  'text-sm',
                  step.completed ? 'text-tc-text-muted line-through' : 'text-tc-text-secondary',
                ].join(' ')}>
                  {step.label}
                </span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  )
}
