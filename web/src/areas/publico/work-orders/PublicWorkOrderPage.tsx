import { useParams } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import { publicApi } from '../../../lib/api/quotes'
import { StatusBadge } from '../../../components/ui/StatusBadge'

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
      <div className="flex min-h-screen items-center justify-center bg-tc-navy">
        <div className="space-y-2 w-48">
          <div className="h-2 animate-pulse bg-tc-navy-mid" />
          <div className="h-2 w-3/4 animate-pulse bg-tc-navy-mid" />
        </div>
      </div>
    )
  }

  if (error || !data) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center bg-tc-navy px-4 text-center">
        <div className="border border-tc-border bg-white p-8 max-w-sm w-full">
          <p className="text-[11px] font-bold tracking-[0.14em] uppercase text-tc-text-muted mb-2">Error</p>
          <p className="font-bold text-tc-text">Orden no encontrada</p>
          <p className="mt-2 text-sm text-tc-text-secondary">El enlace puede haber expirado o ser incorrecto.</p>
        </div>
      </div>
    )
  }

  const statusLabel = WO_STATUS_LABELS[data.status] ?? data.status

  return (
    <div className="min-h-screen bg-tc-surface">
      <div className="mx-auto max-w-2xl px-4 py-10">
        <div className="border border-tc-border bg-white overflow-hidden">
          <div className="bg-tc-stub-bg border-b border-tc-border px-6 py-3 flex items-center justify-between">
            <span className="text-[11px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Orden de trabajo</span>
            <StatusBadge status={data.status} label={statusLabel} />
          </div>
          <div className="px-6 py-5">
            <p className="font-mono font-bold text-2xl text-tc-text mb-4">#{data.number}</p>
            <dl className="grid grid-cols-2 gap-4 text-sm">
              <div>
                <dt className="text-[11px] font-bold tracking-wider uppercase text-tc-text-muted mb-1">Cliente</dt>
                <dd className="text-tc-text">{data.customerName}</dd>
              </div>
              <div>
                <dt className="text-[11px] font-bold tracking-wider uppercase text-tc-text-muted mb-1">Vehículo</dt>
                <dd className="text-tc-text font-mono">
                  {[data.vehicleBrand, data.vehicleModel, data.vehiclePlate]
                    .filter(Boolean)
                    .join(' · ')}
                </dd>
              </div>
              {data.assignedMechanicName && (
                <div>
                  <dt className="text-[11px] font-bold tracking-wider uppercase text-tc-text-muted mb-1">Mecánico asignado</dt>
                  <dd className="text-tc-text">{data.assignedMechanicName}</dd>
                </div>
              )}
              {data.reportedIssue && (
                <div className="col-span-2">
                  <dt className="text-[11px] font-bold tracking-wider uppercase text-tc-text-muted mb-1">Problema reportado</dt>
                  <dd className="text-tc-text">{data.reportedIssue}</dd>
                </div>
              )}
              {data.mechanicNotes && (
                <div className="col-span-2">
                  <dt className="text-[11px] font-bold tracking-wider uppercase text-tc-text-muted mb-1">Notas del mecánico</dt>
                  <dd className="text-tc-text">{data.mechanicNotes}</dd>
                </div>
              )}
            </dl>
          </div>
          <div className="border-t border-tc-border bg-tc-stub-bg px-6 py-2 text-center">
            <span className="text-xs text-tc-text-muted">TallerControl</span>
          </div>
        </div>
      </div>
    </div>
  )
}
