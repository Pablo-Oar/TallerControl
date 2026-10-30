import { useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { StatusBadge } from '../../../components/ui/StatusBadge'
import { appointmentsApi } from '../../../lib/api/appointments'
import type { DashboardThresholds, LiveOrder, TodayAppointment, TodayAppointments } from '../../../lib/api/dashboard'
import { useCan } from '../../../lib/auth/permissions'
import { ConvertAppointmentModal } from '../appointments/ConvertAppointmentModal'
import { daysLabel, timeLabel } from './format'
import { Pill, WidgetEmpty, WidgetShell } from './Widgets'

/** «Taller en vivo»: las órdenes en curso, las trabadas primero (llevan más de N días en el mismo estado). */
export function LiveOrders({
  orders,
  stuckCount,
  thresholds,
}: {
  orders: LiveOrder[]
  stuckCount: number
  thresholds: DashboardThresholds
}) {
  return (
    <WidgetShell title="Taller en vivo" linkTo="/app/work-orders" linkLabel="Ver órdenes">
      {orders.length === 0 ? (
        <WidgetEmpty>No hay órdenes en curso.</WidgetEmpty>
      ) : (
        <ul className="divide-y divide-tc-border">
          {orders.map(o => (
            <li key={o.id}>
              <Link to={`/app/work-orders/${o.id}`} className="flex min-h-11 items-center justify-between gap-3 px-4 py-2.5 hover:bg-tc-surface">
                <span className="min-w-0">
                  <span className="block truncate text-sm font-medium text-tc-text">#{o.number} · {o.customerName}</span>
                  <span className="block truncate text-xs text-tc-text-muted">
                    <span className="font-mono tracking-widest">{o.vehiclePlate}</span>
                    {o.vehicleLabel ? ` · ${o.vehicleLabel}` : ''}
                    {' · '}
                    {o.mechanicName ?? <span className="font-semibold text-amber-700">Sin mecánico</span>}
                  </span>
                </span>
                <span className="flex flex-none flex-col items-end gap-1">
                  <StatusBadge status={o.status} />
                  {o.isStuck ? (
                    <Pill tone="danger">hace {daysLabel(o.daysInStatus)}</Pill>
                  ) : (
                    <span className="text-[11px] text-tc-text-muted">{o.daysInStatus === 0 ? 'hoy' : `hace ${daysLabel(o.daysInStatus)}`}</span>
                  )}
                </span>
              </Link>
            </li>
          ))}
        </ul>
      )}
      {stuckCount > 0 && (
        <p className="border-t border-tc-border bg-red-50 px-4 py-2.5 text-xs text-red-700">
          {stuckCount === 1
            ? `1 orden lleva más de ${thresholds.stuckOrderDays} días en el mismo estado.`
            : `${stuckCount} órdenes llevan más de ${thresholds.stuckOrderDays} días en el mismo estado.`}
        </p>
      )}
    </WidgetShell>
  )
}

/** «Turnos de hoy», con «Convertir en orden» (pide el kilometraje, igual que en la Agenda). */
export function TodayAppointmentsWidget({ data }: { data: TodayAppointments }) {
  const can = useCan()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [converting, setConverting] = useState<TodayAppointment | null>(null)
  const [error, setError] = useState<string | null>(null)

  const convertMut = useMutation({
    mutationFn: ({ id, odometerKm }: { id: string; odometerKm: number }) =>
      appointmentsApi.convertToWorkOrder(id, { odometerKm }),
    onSuccess: order => {
      void queryClient.invalidateQueries({ queryKey: ['dashboard'] })
      setConverting(null)
      navigate(`/app/work-orders/${order.id}`)
    },
    onError: (e: Error) => setError(e.message),
  })

  return (
    <WidgetShell title="Turnos de hoy" count={data.total} linkTo="/app/appointments" linkLabel="Ver agenda">
      {data.items.length === 0 ? (
        <WidgetEmpty>No hay turnos para hoy.</WidgetEmpty>
      ) : (
        <ul className="divide-y divide-tc-border">
          {data.items.map(a => (
            <li key={a.id} className="flex min-h-11 items-center justify-between gap-3 px-4 py-2.5">
              <span className="flex min-w-0 items-center gap-3">
                <span className="flex-none text-sm font-bold tabular-nums text-tc-text">{timeLabel(a.scheduledAt)}</span>
                <span className="min-w-0">
                  <span className="block truncate text-sm font-medium text-tc-text">{a.customerName}</span>
                  <span className="block truncate text-xs text-tc-text-muted">
                    <span className="font-mono tracking-widest">{a.vehiclePlate}</span>
                    {' · '}
                    {a.mechanicName ?? <span className="font-semibold text-amber-700">Sin mecánico asignado</span>}
                  </span>
                </span>
              </span>
              {can('workorders.write') && (
                <button
                  type="button"
                  onClick={() => { setError(null); setConverting(a) }}
                  className="min-h-11 flex-none px-2 text-[11px] font-bold tracking-wider uppercase text-tc-purple hover:underline"
                >
                  Convertir en orden
                </button>
              )}
            </li>
          ))}
        </ul>
      )}
      {data.unassigned > 0 && (
        <p className="border-t border-tc-border bg-amber-50 px-4 py-2.5 text-xs text-amber-800">
          {data.unassigned === 1
            ? '1 turno sin mecánico asignado: asignalo desde la Agenda.'
            : `${data.unassigned} turnos sin mecánico asignado: asignalos desde la Agenda.`}
        </p>
      )}

      {converting && (
        <ConvertAppointmentModal
          vehicleId={converting.vehicleId}
          vehiclePlate={converting.vehiclePlate}
          busy={convertMut.isPending}
          error={error}
          onConfirm={odometerKm => convertMut.mutate({ id: converting.id, odometerKm })}
          onClose={() => setConverting(null)}
        />
      )}
    </WidgetShell>
  )
}
