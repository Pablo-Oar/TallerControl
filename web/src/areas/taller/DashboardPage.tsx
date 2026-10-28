import { useQuery } from '@tanstack/react-query'
import { getDashboard } from '../../lib/api/dashboard'
import { Link } from 'react-router'
import { useAuth } from '../../lib/auth/AuthContext'
import { onboardingApi } from '../../lib/api/onboarding'
import { workOrdersApi, STATUS_LABELS } from '../../lib/api/workOrders'
import { appointmentsApi } from '../../lib/api/appointments'
import { StatusBadge } from '../../components/ui/StatusBadge'
import { useCan } from '../../lib/auth/permissions'
import { formatDateTime, localDateISO } from '../../lib/dates'

interface KpiStubProps {
  label: string
  value: string | number
  to?: string
  alert?: boolean
}

function KpiStub({ label, value, to, alert }: KpiStubProps) {
  const inner = (
    <div className="flex h-full overflow-hidden border border-tc-border bg-white hover:border-tc-border-mid transition-colors group">
      {/* Stub column */}
      <div className="w-2 flex-none bg-tc-stub-bg" />
      {/* Perforation */}
      <div className="perf-line flex-none self-stretch" />
      {/* Body */}
      <div className="flex flex-1 flex-col gap-2 px-4 py-4">
        <span className="text-[11px] font-bold leading-tight tracking-[0.12em] uppercase text-tc-text-secondary">
          {label}
        </span>
        <span
          className={[
            'tabular-nums text-2xl font-bold leading-none',
            alert ? 'text-amber-600' : 'text-tc-text',
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
    <Link to={to} className="block h-full focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-tc-purple focus-visible:ring-offset-1">
      {inner}
    </Link>
  ) : (
    inner
  )
}

function Panel({ title, to, children }: { title: string; to: string; children: React.ReactNode }) {
  return (
    <section className="border border-tc-border bg-white">
      <div className="flex items-center justify-between border-b border-tc-border bg-tc-stub-bg px-4 py-3">
        <h2 className="text-[11px] font-bold tracking-[0.14em] uppercase text-tc-text-secondary">{title}</h2>
        <Link to={to} className="text-[11px] font-bold tracking-wider uppercase text-tc-purple hover:underline">Ver todas</Link>
      </div>
      {children}
    </section>
  )
}

function RecentOrders() {
  const { data } = useQuery({
    queryKey: ['dashboard-recent-orders'],
    queryFn: () => workOrdersApi.search(null, null, null, null, null, null, 1, 6, 'all', { key: 'createdat', dir: 'desc' }),
  })
  return (
    <Panel title="Últimas órdenes" to="/app/work-orders">
      {!data?.items.length ? (
        <p className="px-4 py-6 text-sm text-tc-text-muted">Todavía no hay órdenes.</p>
      ) : (
        <ul className="divide-y divide-tc-border">
          {data.items.map(o => (
            <li key={o.id}>
              <Link to={`/app/work-orders/${o.id}`} className="flex min-h-11 items-center justify-between gap-3 px-4 py-2.5 hover:bg-tc-surface">
                <span className="min-w-0">
                  <span className="block truncate text-sm font-medium text-tc-text">#{o.number} · {o.customerName}</span>
                  <span className="block truncate font-mono text-xs tracking-widest text-tc-text-muted">{o.vehiclePlate}</span>
                </span>
                <StatusBadge status={o.status} label={STATUS_LABELS[o.status]} />
              </Link>
            </li>
          ))}
        </ul>
      )}
    </Panel>
  )
}

function UpcomingAppointments() {
  const { data } = useQuery({
    queryKey: ['dashboard-upcoming-appointments'],
    queryFn: () => appointmentsApi.search({ from: localDateISO(), to: localDateISO(14), pageSize: 6 }),
  })
  const items = (data?.items ?? []).filter(a => a.status === 'pending' || a.status === 'confirmed')
  return (
    <Panel title="Próximos turnos" to="/app/appointments">
      {!items.length ? (
        <p className="px-4 py-6 text-sm text-tc-text-muted">No hay turnos en los próximos días.</p>
      ) : (
        <ul className="divide-y divide-tc-border">
          {items.map(a => (
            <li key={a.id} className="flex min-h-11 items-center justify-between gap-3 px-4 py-2.5">
              <span className="min-w-0">
                <span className="block truncate text-sm font-medium text-tc-text">{a.customerName}</span>
                <span className="block truncate text-xs text-tc-text-muted">
                  {formatDateTime(a.scheduledAt)} · <span className="font-mono tracking-widest">{a.vehiclePlate}</span>
                </span>
              </span>
              <StatusBadge status={a.status} label={a.status === 'confirmed' ? 'Confirmado' : 'Pendiente'} />
            </li>
          ))}
        </ul>
      )}
    </Panel>
  )
}

export function DashboardPage() {
  const { user } = useAuth()
  const can = useCan()
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
        <KpiStub label="Por cobrar" value={totalFormatted} />
        <KpiStub label="Órdenes abiertas" value={data!.openWorkOrders} to="/app/work-orders" />
        <KpiStub label="Presupuestos pendientes" value={data!.pendingQuotes} to="/app/quotes" />
        <KpiStub
          label="Stock bajo"
          value={data!.lowStockItems}
          to="/app/catalog"
          alert={data!.lowStockItems > 0}
        />
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        <RecentOrders />
        {can('appointments.read') && <UpcomingAppointments />}
      </div>

      {/* Onboarding checklist — owner only, while incomplete */}
      {onboarding && !onboarding.isComplete && (
        <div className="border border-tc-border bg-white overflow-hidden">
          {/* Header stub */}
          <div className="flex items-center justify-between border-b border-tc-border bg-tc-stub-bg px-4 py-3">
            <span className="text-[11px] font-bold tracking-[0.14em] uppercase text-tc-text-secondary">
              Guía de inicio
            </span>
            <span className="text-[11px] font-bold tracking-wider text-tc-purple tabular-nums">
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
            {onboarding.steps.filter(step => !step.completed).map(step => (
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
          {onboarding.completedCount > 0 && (
            <p className="flex items-center gap-2 border-t border-tc-border px-4 py-2.5 text-xs text-tc-text-secondary">
              <svg className="h-4 w-4 flex-none text-tc-done-text" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden>
                <path d="M3 8l3.5 3.5L13 5" strokeLinecap="round" strokeLinejoin="round" />
              </svg>
              {onboarding.completedCount === 1 ? '1 paso ya completo' : `${onboarding.completedCount} pasos ya completos`}
            </p>
          )}
        </div>
      )}
    </div>
  )
}
