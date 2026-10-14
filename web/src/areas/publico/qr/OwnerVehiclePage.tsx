import { useParams, useNavigate } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import { getOwnerVehicle, type OwnerWorkOrderDto } from '../../../lib/api/qrCards'

const WO_STATUS_LABELS: Record<string, string> = {
  pending: 'Pendiente',
  in_progress: 'En proceso',
  waiting_parts: 'Esperando repuestos',
  completed: 'Completada',
  delivered: 'Entregada',
  cancelled: 'Cancelada',
}

function WorkOrderCard({ wo }: { wo: OwnerWorkOrderDto }) {
  const status = WO_STATUS_LABELS[wo.status] ?? wo.status
  const date = new Date(wo.createdAt).toLocaleDateString('es-AR')
  return (
    <div className="rounded-lg border border-slate-200 bg-white p-4">
      <div className="flex items-start justify-between gap-2">
        <div>
          <span className="text-xs font-semibold text-slate-500 uppercase">
            OT #{wo.number}
          </span>
          <p className="mt-0.5 text-sm font-medium text-slate-800">{status}</p>
        </div>
        {wo.total != null && (
          <span className="text-sm font-semibold text-slate-800">
            ${wo.total.toLocaleString('es-AR', { minimumFractionDigits: 2 })}
          </span>
        )}
      </div>
      <div className="mt-1 flex items-center gap-3 text-xs text-slate-400">
        <span>Ingreso: {date}</span>
        {wo.completedAt && (
          <span>
            Entrega: {new Date(wo.completedAt).toLocaleDateString('es-AR')}
          </span>
        )}
      </div>
    </div>
  )
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
      <div className="flex min-h-screen items-center justify-center bg-slate-50">
        <p className="text-slate-500">Cargando historial…</p>
      </div>
    )
  }

  if (isError || !data) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-slate-50 px-4 text-center">
        <p className="text-lg font-semibold text-slate-800">Sesión expirada</p>
        <p className="text-sm text-slate-500">
          Tu sesión venció. Escaneá el código QR nuevamente para acceder.
        </p>
        <button
          className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
          onClick={() => navigate(`/q/${code}`, { replace: true })}
        >
          Volver al inicio
        </button>
      </div>
    )
  }

  const vehicleTitle = [data.brand, data.model].filter(Boolean).join(' ') || 'Vehículo'

  return (
    <div className="min-h-screen bg-slate-50 pb-8">
      {/* Header */}
      <div className="bg-white border-b border-slate-200 px-4 py-5">
        <div className="mx-auto max-w-md">
          <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
            Portal del dueño
          </p>
          <h1 className="mt-1 text-2xl font-bold text-slate-900">{vehicleTitle}</h1>
          <p className="mt-0.5 text-base font-mono font-semibold text-blue-600">
            {data.plate}
          </p>
          <p className="mt-1 text-sm text-slate-500">
            Odómetro: {data.odometerKm.toLocaleString('es-AR')} km
          </p>
        </div>
      </div>

      <div className="mx-auto max-w-md px-4 space-y-6 pt-6">
        {/* Balance pendiente */}
        {data.pendingBalance > 0 && (
          <div className="rounded-lg border border-amber-200 bg-amber-50 p-4">
            <p className="text-sm font-semibold text-amber-800">Saldo pendiente</p>
            <p className="mt-0.5 text-2xl font-bold text-amber-700">
              ${data.pendingBalance.toLocaleString('es-AR', { minimumFractionDigits: 2 })}
            </p>
          </div>
        )}

        {/* Presupuestos activos */}
        {data.activeQuotes.length > 0 && (
          <section>
            <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-500">
              Presupuestos activos
            </h2>
            <div className="space-y-2">
              {data.activeQuotes.map((q) => (
                <div
                  key={q.number}
                  className="rounded-lg border border-slate-200 bg-white p-4"
                >
                  <div className="flex items-start justify-between">
                    <div>
                      <span className="text-xs font-semibold text-slate-500 uppercase">
                        Presupuesto #{q.number}
                      </span>
                      <p className="mt-0.5 text-sm font-medium capitalize text-slate-700">
                        {q.status.replace(/_/g, ' ')}
                      </p>
                    </div>
                    {q.validUntil && (
                      <span className="text-xs text-slate-400">
                        Vence {new Date(q.validUntil).toLocaleDateString('es-AR')}
                      </span>
                    )}
                  </div>
                  {q.publicLink && (
                    <a
                      href={q.publicLink}
                      className="mt-2 block text-xs text-blue-600 hover:underline"
                    >
                      Ver presupuesto →
                    </a>
                  )}
                </div>
              ))}
            </div>
          </section>
        )}

        {/* Historial de OTs */}
        <section>
          <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-500">
            Historial de órdenes de trabajo
          </h2>
          {data.workOrders.length === 0 ? (
            <p className="text-sm text-slate-400">No hay órdenes registradas.</p>
          ) : (
            <div className="space-y-2">
              {data.workOrders.map((wo) => (
                <WorkOrderCard key={wo.number} wo={wo} />
              ))}
            </div>
          )}
        </section>

        <p className="text-center text-xs text-slate-400">
          Esta sesión es válida por 30 minutos.
        </p>
      </div>
    </div>
  )
}
