import { useQuery } from '@tanstack/react-query'
import { getDashboard } from '../../lib/api/dashboard'
import { Link } from 'react-router'
import { useAuth } from '../../lib/auth/AuthContext'
import { onboardingApi } from '../../lib/api/onboarding'

function KpiCard({
  label,
  value,
  to,
  alert,
}: {
  label: string
  value: string | number
  to?: string
  alert?: boolean
}) {
  const inner = (
    <div
      className={`rounded-xl border bg-white p-5 shadow-sm flex flex-col gap-1 ${alert ? 'border-amber-400' : 'border-slate-200'}`}
    >
      <span className="text-xs font-medium uppercase tracking-wide text-slate-500">{label}</span>
      <span className={`text-3xl font-bold ${alert ? 'text-amber-600' : 'text-slate-800'}`}>{value}</span>
    </div>
  )
  return to ? (
    <Link to={to} className="hover:opacity-80 transition-opacity">
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
    return <p className="text-slate-500 text-sm">Cargando resumen…</p>
  }

  if (error) {
    return <p className="text-red-600 text-sm">No se pudo cargar el resumen del taller.</p>
  }

  const totalFormatted = new Intl.NumberFormat('es-AR', {
    style: 'currency',
    currency: 'ARS',
    maximumFractionDigits: 0,
  }).format(data!.totalReceivable)

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold text-slate-800">Inicio</h1>

      <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
        <KpiCard label="Por cobrar" value={totalFormatted} />
        <KpiCard label="OTs abiertas" value={data!.openWorkOrders} to="/app/work-orders" />
        <KpiCard label="Presupuestos pendientes" value={data!.pendingQuotes} to="/app/quotes" />
        <KpiCard
          label="Stock bajo"
          value={data!.lowStockItems}
          to="/app/catalog"
          alert={data!.lowStockItems > 0}
        />
      </div>

      {/* Guía de inicio — solo para Owner cuando no está completa */}
      {onboarding && !onboarding.isComplete && (
        <div className="rounded-xl border border-blue-200 bg-blue-50 p-5 space-y-3">
          <div className="flex items-center justify-between">
            <h2 className="text-sm font-semibold text-blue-800">
              Guía de inicio — {onboarding.completedCount}/{onboarding.totalCount} completados
            </h2>
            <div className="text-xs text-blue-600 font-medium">
              {Math.round((onboarding.completedCount / onboarding.totalCount) * 100)}%
            </div>
          </div>
          <div className="h-1.5 w-full rounded-full bg-blue-200">
            <div
              className="h-1.5 rounded-full bg-blue-600 transition-all"
              style={{ width: `${(onboarding.completedCount / onboarding.totalCount) * 100}%` }}
            />
          </div>
          <ul className="space-y-1.5">
            {onboarding.steps.map(step => (
              <li key={step.key} className="flex items-center gap-2 text-sm">
                {step.completed
                  ? <span className="text-green-600">✓</span>
                  : <span className="text-blue-300">○</span>}
                <span className={step.completed ? 'text-slate-500 line-through' : 'text-slate-700'}>
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
