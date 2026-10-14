import { useParams } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import { publicApi } from '../../../lib/api/quotes'

const WO_STATUS_LABELS: Record<string, string> = {
  pending: 'Pendiente',
  in_progress: 'En proceso',
  waiting_parts: 'Esperando repuestos',
  completed: 'Completada',
  delivered: 'Entregada',
  cancelled: 'Cancelada',
}

export function PublicWorkOrderPage() {
  const { token } = useParams<{ token: string }>()

  const { data, isLoading, error } = useQuery({
    queryKey: ['public-work-order', token],
    queryFn: () => publicApi.getWorkOrder(token!),
    enabled: !!token,
    retry: false,
  })

  if (isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-white">
        <div className="h-6 w-6 animate-spin rounded-full border-2 border-blue-600 border-t-transparent" />
      </div>
    )
  }

  if (error || !data) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-white px-4">
        <div className="max-w-sm text-center">
          <p className="text-lg font-medium text-slate-800">Orden no encontrada</p>
          <p className="mt-1 text-sm text-slate-500">
            El enlace puede haber expirado o ser incorrecto.
          </p>
        </div>
      </div>
    )
  }

  const statusLabel = WO_STATUS_LABELS[data.status] ?? data.status

  return (
    <div className="min-h-screen bg-slate-50">
      <div className="mx-auto max-w-2xl px-4 py-10">
        <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
          <div className="mb-6 flex items-start justify-between">
            <div>
              <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                Orden de trabajo
              </p>
              <h1 className="text-2xl font-bold text-slate-800">#{data.number}</h1>
            </div>
            <span className="rounded-full bg-blue-100 px-3 py-1 text-sm font-medium text-blue-700">
              {statusLabel}
            </span>
          </div>

          <dl className="grid grid-cols-2 gap-4 text-sm">
            <div>
              <dt className="font-medium text-slate-500">Cliente</dt>
              <dd className="mt-1 text-slate-800">{data.customerName}</dd>
            </div>
            <div>
              <dt className="font-medium text-slate-500">Vehículo</dt>
              <dd className="mt-1 text-slate-800">
                {[data.vehicleBrand, data.vehicleModel, data.vehiclePlate]
                  .filter(Boolean)
                  .join(' · ')}
              </dd>
            </div>
            {data.assignedMechanicName && (
              <div>
                <dt className="font-medium text-slate-500">Mecánico asignado</dt>
                <dd className="mt-1 text-slate-800">{data.assignedMechanicName}</dd>
              </div>
            )}
            {data.reportedIssue && (
              <div className="col-span-2">
                <dt className="font-medium text-slate-500">Problema reportado</dt>
                <dd className="mt-1 text-slate-800">{data.reportedIssue}</dd>
              </div>
            )}
            {data.mechanicNotes && (
              <div className="col-span-2">
                <dt className="font-medium text-slate-500">Notas del mecánico</dt>
                <dd className="mt-1 text-slate-800">{data.mechanicNotes}</dd>
              </div>
            )}
          </dl>
        </div>
      </div>
    </div>
  )
}
