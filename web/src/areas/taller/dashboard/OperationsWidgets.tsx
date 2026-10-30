import { useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { StatusBadge } from '../../../components/ui/StatusBadge'
import { appointmentsApi } from '../../../lib/api/appointments'
import type { DashboardThresholds, LiveOrder, TodayAppointment, TodayAppointments } from '../../../lib/api/dashboard'
import { useCan } from '../../../lib/auth/permissions'
import { ConvertAppointmentModal } from '../appointments/ConvertAppointmentModal'
import { daysLabel, timeLabel } from './format'
import { CalendarIcon, ClockIcon, WrenchIcon } from './icons'
import { StatusStrip, WidgetEmpty, WidgetShell } from './Widgets'

/** Columnas de «Taller en vivo» en escritorio; en celular cada orden es una tarjeta. */
const LIVE_GRID = 'md:grid md:grid-cols-[3.5rem_minmax(0,1.5fr)_minmax(0,1fr)_7rem_7rem] md:items-center md:gap-3'

/** «hace 9 días» / «hoy»: el tiempo que lleva la orden en su estado, con el mismo sello que el estado. */
function TimeInStatus({ order }: { order: LiveOrder }) {
  return order.isStuck ? (
    <StatusBadge status="time_stuck" label={`HACE ${daysLabel(order.daysInStatus).toUpperCase()}`} />
  ) : (
    <StatusBadge status="time_ok" label={order.daysInStatus === 0 ? 'HOY' : `HACE ${daysLabel(order.daysInStatus).toUpperCase()}`} />
  )
}

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
    <WidgetShell title="Taller en vivo" icon={<WrenchIcon />} accent="blue" linkTo="/app/work-orders" linkLabel="Ver órdenes">
      {orders.length === 0 ? (
        <WidgetEmpty>No hay órdenes en curso.</WidgetEmpty>
      ) : (
        <>
          <div
            aria-hidden
            className={`${LIVE_GRID} hidden border-b border-tc-border bg-white px-4 py-2 text-[11px] font-bold tracking-[0.12em] uppercase text-tc-text-muted`}
          >
            <span>N°</span>
            <span>Cliente y vehículo</span>
            <span>Mecánico</span>
            <span className="text-center">Estado</span>
            <span className="text-center">En el estado</span>
          </div>
          <ul className="divide-y divide-tc-border">
            {orders.map(o => (
              <li key={o.id}>
                <Link
                  to={`/app/work-orders/${o.id}`}
                  className={`${LIVE_GRID} flex min-h-11 items-center justify-between gap-3 px-4 py-2.5 hover:bg-tc-surface`}
                >
                  <span className="hidden text-sm font-bold tabular-nums text-tc-text-secondary md:block">#{o.number}</span>
                  <span className="min-w-0">
                    <span className="block truncate text-sm font-medium text-tc-text">
                      <span className="md:hidden">#{o.number} · </span>
                      {o.customerName}
                    </span>
                    <span className="block truncate text-xs text-tc-text-muted">
                      <span className="font-mono tracking-widest">{o.vehiclePlate}</span>
                      {o.vehicleLabel ? ` · ${o.vehicleLabel}` : ''}
                      <span className="md:hidden">
                        {' · '}
                        {o.mechanicName ?? <span className="font-semibold text-amber-700">Sin mecánico</span>}
                      </span>
                    </span>
                  </span>
                  <span className="hidden min-w-0 truncate text-sm text-tc-text-secondary md:block">
                    {o.mechanicName ?? <span className="font-semibold text-amber-700">Sin mecánico</span>}
                  </span>
                  <span className="flex flex-none flex-col items-end gap-1 md:contents">
                    <span className="md:flex md:justify-center">
                      <StatusBadge status={o.status} />
                    </span>
                    <span className="md:flex md:justify-center">
                      <TimeInStatus order={o} />
                    </span>
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        </>
      )}
      {stuckCount > 0 && (
        <StatusStrip tone="red" icon={<ClockIcon />}>
          {stuckCount === 1
            ? `1 orden lleva más de ${thresholds.stuckOrderDays} días en el mismo estado.`
            : `${stuckCount} órdenes llevan más de ${thresholds.stuckOrderDays} días en el mismo estado.`}
        </StatusStrip>
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
    <WidgetShell title="Turnos de hoy" icon={<CalendarIcon />} accent="purple" count={data.total} linkTo="/app/appointments" linkLabel="Ver agenda">
      {data.items.length === 0 ? (
        <WidgetEmpty>No hay turnos para hoy.</WidgetEmpty>
      ) : (
        <ul className="divide-y divide-tc-border">
          {data.items.map(a => (
            <li key={a.id} className="flex min-h-11 items-center justify-between gap-3 px-4 py-2.5">
              <span className="flex min-w-0 items-center gap-3">
                <span className="w-12 flex-none bg-tc-purple-light py-1 text-center text-sm font-bold tabular-nums text-tc-purple-mid">
                  {timeLabel(a.scheduledAt)}
                </span>
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
                  className="min-h-11 flex-none border border-tc-purple px-2.5 text-[11px] font-bold tracking-wider uppercase text-tc-purple transition-colors hover:bg-tc-purple hover:text-white"
                >
                  Convertir en orden
                </button>
              )}
            </li>
          ))}
        </ul>
      )}
      {data.unassigned > 0 && (
        <StatusStrip tone="amber" icon={<UserOffIcon />}>
          {data.unassigned === 1
            ? '1 turno sin mecánico asignado: asignalo desde la Agenda.'
            : `${data.unassigned} turnos sin mecánico asignado: asignalos desde la Agenda.`}
        </StatusStrip>
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

function UserOffIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.8} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <circle cx="12" cy="8" r="3.5" />
      <path d="M5 20a7 7 0 0 1 14 0M4 4l16 16" />
    </svg>
  )
}
