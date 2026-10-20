import { useParams, useNavigate } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import { getOwnerVehicle, type OwnerWorkOrderDto } from '../../../lib/api/qrCards'
import { StatusBadge } from '../../../components/ui/StatusBadge'
import { Timeline, type TimelineItem } from '../../../components/ui/Timeline'

const WO_STATUS_LABELS: Record<string, string> = {
  pending: 'Pendiente',
  in_progress: 'En proceso',
  waiting_parts: 'Esperando repuestos',
  completed: 'Completada',
  delivered: 'Entregada',
  cancelled: 'Cancelada',
}

const fmtARS = (n: number) =>
  new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 2 }).format(n)

/** Solo los campos que ya expone el DTO público (número, estado, fechas y total): el Timeline no recibe nada más. */
function toTimelineItem(wo: OwnerWorkOrderDto): TimelineItem {
  const parts = [
    wo.completedAt ? `Entrega: ${new Date(wo.completedAt).toLocaleDateString('es-AR')}` : null,
    wo.total != null ? `Total: ${fmtARS(wo.total)}` : null,
  ].filter(Boolean)
  return {
    id: String(wo.number),
    date: wo.createdAt,
    title: `OT #${wo.number}`,
    kind: 'order',
    badge: <StatusBadge status={wo.status} label={WO_STATUS_LABELS[wo.status] ?? wo.status} />,
    detail: parts.length ? parts.join(' · ') : null,
  }
}

export function OwnerVehiclePage() {
  const { code } = useParams<{ code: string }>()
  const navigate = useNavigate()

  const { data, isLoading, isError } = useQuery({
    queryKey: ['owner-vehicle', code],
    queryFn: () => getOwnerVehicle(code!),
    retry: false,
  })

  if (isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-tc-navy">
        <div className="space-y-2 w-48">
          <div className="h-2 animate-pulse bg-tc-navy-mid" />
          <div className="h-2 w-3/4 animate-pulse bg-tc-navy-mid" />
        </div>
      </div>
    )
  }

  if (isError || !data) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-tc-navy px-4 text-center">
        <div className="border border-tc-border bg-white p-8 max-w-sm w-full space-y-3">
          <p className="text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Sesión expirada</p>
          <p className="font-bold text-slate-900">Tu sesión venció</p>
          <p className="text-sm text-slate-500">
            Escaneá el código QR nuevamente para acceder.
          </p>
          <button
            className="mt-2 w-full border border-tc-navy bg-tc-navy py-2.5 text-xs font-bold tracking-wider uppercase text-white hover:bg-transparent hover:text-tc-navy transition-colors"
            onClick={() => navigate(`/q/${code}`, { replace: true })}
          >
            Volver al inicio
          </button>
        </div>
      </div>
    )
  }

  const vehicleTitle = [data.brand, data.model].filter(Boolean).join(' ') || 'Vehículo'

  return (
    <div className="min-h-screen bg-tc-surface pb-8">
      {/* Header */}
      <div className="bg-tc-navy border-b border-tc-border px-4 py-5">
        <div className="mx-auto max-w-md">
          <p className="text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">
            Portal del dueño
          </p>
          <h1 className="mt-1 text-xl font-bold text-white">{vehicleTitle}</h1>
          <p className="mt-0.5 font-mono font-bold text-tc-purple tracking-widest">
            {data.plate}
          </p>
          <p className="mt-1 text-sm text-slate-400">
            Odómetro: {data.odometerKm.toLocaleString('es-AR')} km
          </p>
        </div>
      </div>

      <div className="mx-auto max-w-md px-4 space-y-5 pt-5">
        {/* Balance pendiente */}
        {data.pendingBalance > 0 && (
          <div className="border border-amber-300 bg-amber-50 p-4">
            <p className="text-[10px] font-bold tracking-wider uppercase text-amber-600 mb-1">Saldo pendiente</p>
            <p className="text-2xl font-bold text-amber-700 tabular-nums">
              {fmtARS(data.pendingBalance)}
            </p>
          </div>
        )}

        {/* Presupuestos activos */}
        {data.activeQuotes.length > 0 && (
          <div className="border border-tc-border bg-white overflow-hidden">
            <div className="bg-tc-stub-bg border-b border-tc-border px-4 py-2">
              <span className="text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Presupuestos activos</span>
            </div>
            <div className="divide-y divide-tc-border">
              {data.activeQuotes.map((q) => (
                <div key={q.number} className="px-4 py-3">
                  <div className="flex items-start justify-between">
                    <div>
                      <span className="text-[10px] font-bold tracking-wider uppercase text-slate-400">Presupuesto #{q.number}</span>
                      <p className="mt-0.5 text-sm text-slate-700">{q.status.replace(/_/g, ' ')}</p>
                    </div>
                    {q.validUntil && (
                      <span className="text-xs text-slate-400 shrink-0">
                        Vence {new Date(q.validUntil).toLocaleDateString('es-AR')}
                      </span>
                    )}
                  </div>
                  {q.publicLink && (
                    <a
                      href={q.publicLink}
                      className="mt-2 block text-xs font-bold tracking-wider uppercase text-tc-purple hover:opacity-70"
                    >
                      Ver presupuesto →
                    </a>
                  )}
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Historial de OTs */}
        <div className="border border-tc-border bg-white overflow-hidden">
          <div className="bg-tc-stub-bg border-b border-tc-border px-4 py-2">
            <span className="text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Historial de órdenes de trabajo</span>
          </div>
          {data.workOrders.length === 0 ? (
            <p className="px-4 py-6 text-sm text-slate-400">No hay órdenes registradas.</p>
          ) : (
            <div className="px-4 py-4">
              <Timeline items={data.workOrders.map(toTimelineItem)} showTime={false} label="Historial de órdenes de trabajo" />
            </div>
          )}
        </div>

        <p className="text-center text-xs text-slate-400">
          Esta sesión es válida por 30 minutos.
        </p>
      </div>
    </div>
  )
}
