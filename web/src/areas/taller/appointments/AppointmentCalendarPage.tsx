import { useMemo, useState } from 'react'
import { useQueries, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  appointmentsApi,
  getAppointmentStatusLabel,
  WEEK_DAYS,
  type AppointmentDto,
  type AppointmentListDto,
} from '../../../lib/api/appointments'
import { workOrdersApi } from '../../../lib/api/workOrders'
import { useCan } from '../../../lib/auth/permissions'
import { addDaysISO, formatDateOnly, localDateISO, mondayOf } from '../../../lib/dates'
import { Button } from '../../../components/ui/Button'
import { StatusBadge } from '../../../components/ui/StatusBadge'
import { inputCls } from '../../../lib/styles'
import { AppointmentFormModal } from './AppointmentFormModal'
import { AppointmentsTabs } from './AppointmentsTabs'

const SLOT_MINUTES = 60

const hhmm = (iso: string) => new Date(iso).toLocaleTimeString('es-AR', { hour: '2-digit', minute: '2-digit' })

/** Calendario semanal (lunes a domingo): los turnos de cada día y cuántos huecos libres quedan. */
export function AppointmentCalendarPage() {
  const qc = useQueryClient()
  const can = useCan()
  const [monday, setMonday] = useState(() => mondayOf(localDateISO()))
  const [mechanicId, setMechanicId] = useState('')
  const [editing, setEditing] = useState<AppointmentDto | null>(null)
  const [creating, setCreating] = useState(false)

  const days = useMemo(() => WEEK_DAYS.map((d, i) => ({ ...d, date: addDaysISO(monday, i) })), [monday])
  const sunday = days[6].date

  const { data: mechanics } = useQuery({
    queryKey: ['assignable-mechanics'],
    queryFn: () => workOrdersApi.listMechanics(),
    enabled: can('workorders.write'),
  })

  const { data, isLoading } = useQuery({
    queryKey: ['appointments', 'week', monday, mechanicId],
    queryFn: () =>
      appointmentsApi.search({ from: monday, to: sunday, mechanicMembershipId: mechanicId || undefined, pageSize: 100 }),
  })

  const freeSlots = useQueries({
    queries: days.map(d => ({
      queryKey: ['availability', d.date, SLOT_MINUTES],
      queryFn: () => appointmentsApi.getAvailabilitySlots(d.date, SLOT_MINUTES),
    })),
  })

  const byDay = useMemo(() => {
    const map = new Map<string, AppointmentListDto[]>()
    for (const a of data?.items ?? []) {
      if (a.status === 'cancelled') continue
      // El día es el de la hora local del dispositivo (igual que el del taller).
      const d = new Date(a.scheduledAt)
      const key = localDateISO(Math.round((new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime() - new Date().setHours(0, 0, 0, 0)) / 86_400_000))
      map.set(key, [...(map.get(key) ?? []), a])
    }
    return map
  }, [data])

  const today = localDateISO()
  const refresh = () => {
    qc.invalidateQueries({ queryKey: ['appointments'] })
    qc.invalidateQueries({ queryKey: ['availability'] })
  }

  async function open(id: string) {
    setEditing(await appointmentsApi.get(id))
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-bold tracking-widest text-tc-text uppercase">Agenda</h1>
        <Button size="sm" onClick={() => setCreating(true)}>Nuevo turno</Button>
      </div>

      <AppointmentsTabs />

      <div className="flex flex-wrap items-center gap-2">
        <Button variant="secondary" size="sm" onClick={() => setMonday(addDaysISO(monday, -7))} aria-label="Semana anterior">←</Button>
        <span className="min-w-[11rem] text-center text-sm font-bold tabular-nums text-tc-text">
          {formatDateOnly(monday)} al {formatDateOnly(sunday)}
        </span>
        <Button variant="secondary" size="sm" onClick={() => setMonday(addDaysISO(monday, 7))} aria-label="Semana siguiente">→</Button>
        <Button variant="secondary" size="sm" onClick={() => setMonday(mondayOf(localDateISO()))}>Hoy</Button>
        {mechanics && (
          <select value={mechanicId} onChange={e => setMechanicId(e.target.value)} className={inputCls} aria-label="Mecánico">
            <option value="">Todos los mecánicos</option>
            {mechanics.map(m => <option key={m.membershipId} value={m.membershipId}>{m.displayName}</option>)}
          </select>
        )}
      </div>

      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4 2xl:grid-cols-7" aria-busy={isLoading}>
        {days.map((d, i) => {
          const list = (byDay.get(d.date) ?? []).sort((a, b) => a.scheduledAt.localeCompare(b.scheduledAt))
          const slots = freeSlots[i]?.data
          const free = mechanicId ? slots?.filter(s => s.mechanicMembershipId === mechanicId).length : slots?.length
          return (
            <section
              key={d.date}
              aria-label={`${d.label} ${formatDateOnly(d.date)}`}
              className={`border bg-white ${d.date === today ? 'border-tc-purple' : 'border-tc-border'}`}
            >
              <header className="border-b border-tc-border bg-tc-stub-bg px-3 py-2">
                <span className="block text-xs font-bold tracking-wider uppercase text-tc-text">
                  {d.short} {formatDateOnly(d.date).slice(0, -5)}
                </span>
                <span className="block text-[10px] font-bold tracking-wider uppercase text-tc-text-muted">
                  {free === undefined ? 'Cargando…' : free === 0 ? 'Sin huecos' : `${free} hueco${free === 1 ? '' : 's'} libres`}
                </span>
              </header>
              <ul className="min-h-[3rem] divide-y divide-tc-border">
                {list.length === 0 && <li className="px-3 py-3 text-xs text-tc-text-muted">Sin turnos</li>}
                {list.map(a => (
                  <li key={a.id}>
                    <button
                      type="button"
                      onClick={() => open(a.id)}
                      className="block w-full px-3 py-2 text-left hover:bg-tc-surface"
                    >
                      <span className="block text-sm font-bold tabular-nums text-tc-text">
                        {hhmm(a.scheduledAt)} <span className="font-normal text-tc-text-muted">· {a.totalDurationMinutes} min</span>
                      </span>
                      <span className="block truncate text-xs text-tc-text-secondary">{a.customerName}</span>
                      <span className="block font-mono text-xs tracking-widest text-tc-text-muted">{a.vehiclePlate}</span>
                      <span className="mt-1 flex flex-wrap items-center gap-1.5">
                        <StatusBadge status={a.status} label={getAppointmentStatusLabel(a.status)} />
                        {a.mechanicName && <span className="text-[11px] text-tc-text-muted">{a.mechanicName}</span>}
                      </span>
                    </button>
                  </li>
                ))}
              </ul>
            </section>
          )
        })}
      </div>

      {creating && <AppointmentFormModal onClose={() => { setCreating(false); refresh() }} onCreated={() => { setCreating(false); refresh() }} />}
      {editing && <AppointmentFormModal editData={editing} onClose={() => { setEditing(null); refresh() }} />}
    </div>
  )
}
