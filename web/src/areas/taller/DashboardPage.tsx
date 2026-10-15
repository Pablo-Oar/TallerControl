import { useQuery } from '@tanstack/react-query'
import { getDashboard } from '../../lib/api/dashboard'
import { Link } from 'react-router'

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
  const { data, isLoading, error } = useQuery({
    queryKey: ['dashboard'],
    queryFn: getDashboard,
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
    </div>
  )
}
