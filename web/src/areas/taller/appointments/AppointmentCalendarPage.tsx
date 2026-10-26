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

const hhmm = (iso: string) => new Date(iso).toLocaleTimeString('es-AR', { hour: '2-digit', minute: '2-digit', hour12: false })

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
        <Button variant="secondary" size="sm" className="min-h-11 min-w-11 sm:min-h-0 sm:min-w-0" onClick={() => setMonday(addDaysISO(monday, -7))} aria-label="Semana anterior">←</Button>
        <span className="min-w-[11rem] text-center text-sm font-bold tabular-nums text-tc-text">
          {formatDateOnly(monday)} al {formatDateOnly(sunday)}
        </span>
        <Button variant="secondary" size="sm" className="min-h-11 min-w-11 sm:min-h-0 sm:min-w-0" onClick={() => setMonday(addDaysISO(monday, 7))} aria-label="Semana siguiente">→</Button>
        <Button variant="secondary" size="sm" className="min-h-11 sm:min-h-0" onClick={() => setMonday(mondayOf(localDateISO()))}>Hoy</Button>
        {mechanics && (
          <select value={mechanicId} onChange={e => setMechanicId(e.target.value)} className={`${inputCls} min-h-11 w-full sm:min-h-0 sm:w-auto`} aria-label="Mecánico">
            <option value="">Todos los mecánicos</option>
            {mechanics.map(m => <option key={m.membershipId} value={m.membershipId}>{m.displayName}</option>)}
          </select>
        )}
      </div>

      <div className="grid items-start gap-3 sm:grid-cols-2 lg:grid-cols-4 2xl:grid-cols-7" aria-busy={isLoading}>
        {days.map((d, i) => {
          const list = (byDay.get(d.date) ?? []).sort((a, b) => a.scheduledAt.localeCompare(b.scheduledAt))
          const slots = freeSlots[i]?.data
          const mine = mechanicId ? slots?.filter(s => s.mechanicMembershipId === mechanicId) : slots
          const free = mine?.length
          const range = mine && mine.length > 0
            ? `${hhmm(mine.reduce((m, s) => (s.start < m ? s.start : m), mine[0].start))}–${hhmm(mine.reduce((m, s) => (s.end > m ? s.end : m), mine[0].end))}`
            : ''
          const isToday = d.date === today
          let summary: string
          if (free === undefined) summary = 'Cargando…'
          else if (free === 0) summary = list.length > 0 ? 'Completo' : 'Cerrado'
          else if (list.length === 0) summary = `Libre ${range}`
          else summary = `${free} horario${free === 1 ? '' : 's'} libre${free === 1 ? '' : 's'}`
          return (
            <section
              key={d.date}
              aria-label={`${d.label} ${formatDateOnly(d.date)}`}
              className={`border bg-white ${isToday ? 'border-tc-purple' : 'border-tc-border'}`}
            >
              <header className={`flex min-h-12 flex-wrap items-center justify-between gap-x-2 bg-tc-stub-bg px-3 py-2 ${list.length > 0 ? 'border-b border-tc-border' : ''}`}>
                <span className="text-xs font-bold tracking-wider uppercase text-tc-text">
                  {d.short} {formatDateOnly(d.date).slice(0, -5)}
                  {isToday && <span className="ml-2 bg-tc-purple px-1.5 py-0.5 text-[10px] text-white">Hoy</span>}
                </span>
                <span className={`text-xs ${free === 0 && list.length === 0 ? 'text-tc-text-muted' : 'text-tc-text-secondary'}`}>
                  {summary}
                </span>
              </header>
              {list.length > 0 && (
                <ul className="divide-y divide-tc-border">
                  {list.map(a => (
                    <li key={a.id}>
                      <button
                        type="button"
                        onClick={() => open(a.id)}
                        className="block min-h-11 w-full px-3 py-2 text-left hover:bg-tc-surface"
                      >
                        <span className="block text-sm font-bold tabular-nums text-tc-text">
                          {hhmm(a.scheduledAt)} <span className="font-normal text-tc-text-muted">· {a.totalDurationMinutes} min</span>
                        </span>
                        <span className="block truncate text-xs text-tc-text-secondary">{a.customerName}</span>
                        <span className="block font-mono text-xs tracking-widest text-tc-text-muted">{a.vehiclePlate}</span>
                        <span className="mt-1 flex flex-wrap items-center gap-1.5">
                          <StatusBadge status={a.status} label={getAppointmentStatusLabel(a.status)} />
                          {a.mechanicName && <span className="text-xs text-tc-text-muted">{a.mechanicName}</span>}
                        </span>
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </section>
          )
        })}
      </div>

      {creating && <AppointmentFormModal onClose={() => { setCreating(false); refresh() }} onCreated={() => { setCreating(false); refresh() }} />}
      {editing && <AppointmentFormModal editData={editing} onClose={() => { setEditing(null); refresh() }} />}
    </div>
  )
}
