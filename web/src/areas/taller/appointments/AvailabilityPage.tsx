import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  availabilityApi,
  hhmm,
  WEEK_DAYS,
  type AvailabilityExceptionDto,
  type AvailabilityRuleDto,
  type WeekDay,
} from '../../../lib/api/appointments'
import { workOrdersApi } from '../../../lib/api/workOrders'
import { ApiError } from '../../../lib/api/client'
import { addDaysISO, formatDateOnly, localDateISO } from '../../../lib/dates'
import { Button } from '../../../components/ui/Button'
import { confirmDialog } from '../../../components/ui/dialogs'
import { FormField, FormSelect } from '../../../components/ui/FormField'
import { AppointmentsTabs } from './AppointmentsTabs'

const errorText = (e: unknown) => (e instanceof ApiError ? e.detail : 'No se pudo guardar. Probá de nuevo.')

/** Horario semanal de cada mecánico y días bloqueados / con horario especial (feriados, vacaciones). */
export function AvailabilityPage() {
  const qc = useQueryClient()
  const today = localDateISO()

  const { data: mechanics } = useQuery({ queryKey: ['assignable-mechanics'], queryFn: () => workOrdersApi.listMechanics() })
  const { data: rules, isLoading: rulesLoading } = useQuery({ queryKey: ['availability-rules'], queryFn: () => availabilityApi.rules() })
  const { data: exceptions, isLoading: exLoading } = useQuery({
    queryKey: ['availability-exceptions', today],
    queryFn: () => availabilityApi.exceptions(today, addDaysISO(today, 365)),
  })

  const [error, setError] = useState<string | null>(null)

  // ── Regla semanal ──────────────────────────────────────────────────────────
  const [ruleMechanic, setRuleMechanic] = useState('')
  const [ruleDays, setRuleDays] = useState<WeekDay[]>(['monday', 'tuesday', 'wednesday', 'thursday', 'friday'])
  const [ruleStart, setRuleStart] = useState('09:00')
  const [ruleEnd, setRuleEnd] = useState('18:00')

  const addRules = useMutation({
    mutationFn: async () => {
      for (const day of ruleDays) {
        await availabilityApi.createRule({
          mechanicMembershipId: ruleMechanic,
          dayOfWeek: day,
          startTime: `${ruleStart}:00`,
          endTime: `${ruleEnd}:00`,
        })
      }
    },
    onSuccess: () => { setError(null); qc.invalidateQueries({ queryKey: ['availability-rules'] }); qc.invalidateQueries({ queryKey: ['availability'] }) },
    onError: e => setError(errorText(e)),
  })

  async function removeRules(rules: AvailabilityRuleDto[], who: string, label: string) {
    if (!(await confirmDialog(`¿Quitar el horario de ${label} de ${who}?`, { danger: true }))) return
    try {
      for (const r of rules) await availabilityApi.deleteRule(r.id)
      setError(null)
    } catch (e) {
      setError(errorText(e))
    } finally {
      qc.invalidateQueries({ queryKey: ['availability-rules'] })
      qc.invalidateQueries({ queryKey: ['availability'] })
    }
  }

  // ── Excepción ──────────────────────────────────────────────────────────────
  const [exDate, setExDate] = useState(today)
  const [exMechanic, setExMechanic] = useState('')
  const [exKind, setExKind] = useState<'blocked' | 'special'>('blocked')
  const [exStart, setExStart] = useState('09:00')
  const [exEnd, setExEnd] = useState('13:00')
  const [exReason, setExReason] = useState('')

  const addException = useMutation({
    mutationFn: () =>
      availabilityApi.createException({
        mechanicMembershipId: exMechanic || null,
        date: exDate,
        isBlocked: exKind === 'blocked',
        startTime: exKind === 'special' ? `${exStart}:00` : null,
        endTime: exKind === 'special' ? `${exEnd}:00` : null,
        reason: exReason.trim() || null,
      }),
    onSuccess: () => {
      setError(null)
      setExReason('')
      qc.invalidateQueries({ queryKey: ['availability-exceptions'] })
      qc.invalidateQueries({ queryKey: ['availability'] })
    },
    onError: e => setError(errorText(e)),
  })

  async function removeException(e: AvailabilityExceptionDto) {
    if (!(await confirmDialog(`¿Quitar el ${e.isBlocked ? 'bloqueo' : 'horario especial'} del ${formatDateOnly(e.date)}?`, { danger: true }))) return
    try {
      await availabilityApi.deleteException(e.id)
      qc.invalidateQueries({ queryKey: ['availability-exceptions'] })
      qc.invalidateQueries({ queryKey: ['availability'] })
    } catch (err) {
      setError(errorText(err))
    }
  }

  const byMechanic = new Map<string, AvailabilityRuleDto[]>()
  for (const r of rules ?? []) byMechanic.set(r.mechanicMembershipId, [...(byMechanic.get(r.mechanicMembershipId) ?? []), r])
  const weekOrder = (d: WeekDay) => WEEK_DAYS.findIndex(x => x.value === d)
  const nameOf = (id: string, fallback: string) => mechanics?.find(m => m.membershipId === id)?.displayName ?? fallback

  /** Agrupa los días con el mismo rango: "Lun–Vie 09:00–18:00" en un solo chip. */
  function groupRules(list: AvailabilityRuleDto[]) {
    const byRange = new Map<string, AvailabilityRuleDto[]>()
    for (const r of [...list].sort((x, y) => weekOrder(x.dayOfWeek) - weekOrder(y.dayOfWeek))) {
      const key = `${hhmm(r.startTime)}–${hhmm(r.endTime)}`
      byRange.set(key, [...(byRange.get(key) ?? []), r])
    }
    return [...byRange.entries()].map(([range, rs]) => {
      const idx = rs.map(r => weekOrder(r.dayOfWeek))
      const consecutive = rs.length > 2 && idx.every((v, i) => i === 0 || v === idx[i - 1] + 1)
      const short = (r: AvailabilityRuleDto) => WEEK_DAYS[weekOrder(r.dayOfWeek)].short
      const days = consecutive ? `${short(rs[0])}–${short(rs[rs.length - 1])}` : rs.map(short).join(', ')
      return { range, rules: rs, days }
    })
  }

  return (
    <div className="space-y-6">
      <h1 className="text-xl font-bold tracking-widest text-tc-text uppercase">Agenda</h1>
      <AppointmentsTabs />

      {error && (
        <p role="alert" className="flex items-center justify-between border border-red-200 bg-red-50 px-4 py-2 text-sm text-red-700">
          <span>{error}</span>
          <button className="text-xs underline" onClick={() => setError(null)}>Cerrar</button>
        </p>
      )}

      <p className="border border-tc-border bg-tc-surface px-3 py-2 text-sm text-tc-text-secondary">
        Los horarios son hora del taller. Un mecánico <b>sin horario cargado</b> no tiene restricciones al agendar; en cuanto le cargás
        uno, el sistema solo le permite turnos dentro de él y sin encimarse.
      </p>

      {/* ── Horario semanal ───────────────────────────────────────────────── */}
      <section className="overflow-hidden border border-tc-border bg-white">
        <h2 className="border-b border-tc-border bg-tc-stub-bg px-4 py-2 text-[11px] font-bold tracking-[0.14em] text-tc-text-muted uppercase">
          Horario semanal de cada mecánico
        </h2>

        <div className="divide-y divide-tc-border">
          {rulesLoading && <p className="px-4 py-3 text-sm text-tc-text-muted">Cargando…</p>}
          {!rulesLoading && byMechanic.size === 0 && (
            <p className="px-4 py-3 text-sm text-tc-text-muted">Todavía no hay horarios cargados.</p>
          )}
          {[...byMechanic.entries()].map(([id, list]) => {
            const who = nameOf(id, list[0].mechanicName)
            return (
              <div key={id} className="px-4 py-3">
                <p className="mb-2 text-sm font-bold text-tc-text">{who}</p>
                <ul className="flex flex-wrap gap-2">
                  {groupRules(list).map(g => (
                    <li key={g.range} className="flex items-center border border-tc-border bg-white pl-3 text-sm">
                      <span className="font-bold text-tc-text">{g.days}</span>
                      <span className="ml-2 tabular-nums text-tc-text-secondary">{g.range}</span>
                      <button
                        type="button"
                        onClick={() => removeRules(g.rules, who, g.days)}
                        aria-label={`Quitar ${g.days} ${g.range} de ${who}`}
                        className="ml-1 flex h-11 w-11 items-center justify-center text-lg leading-none text-tc-text-muted transition-colors hover:text-tc-stamp-red"
                      >
                        ×
                      </button>
                    </li>
                  ))}
                </ul>
              </div>
            )
          })}
        </div>

        <form
          className="space-y-3 border-t border-tc-border p-4 [&>*]:max-w-xl"
          onSubmit={e => {
            e.preventDefault()
            if (ruleMechanic && ruleDays.length > 0) addRules.mutate()
          }}
        >
          <p className="text-xs font-bold tracking-wider text-tc-text-muted uppercase">Agregar horario</p>
          <FormSelect label="Mecánico" requiredMark value={ruleMechanic} onChange={e => setRuleMechanic(e.target.value)}>
            <option value="">Elegí un mecánico</option>
            {mechanics?.map(m => <option key={m.membershipId} value={m.membershipId}>{m.displayName}</option>)}
          </FormSelect>
          <fieldset>
            <legend className="mb-1 text-xs font-bold tracking-wider text-tc-text-muted uppercase">Días</legend>
            <div className="flex flex-wrap gap-2">
              {WEEK_DAYS.map(d => (
                <label key={d.value} className="flex min-h-11 cursor-pointer items-center gap-2 border border-tc-border px-3 text-sm">
                  <input
                    type="checkbox"
                    className="accent-tc-purple"
                    checked={ruleDays.includes(d.value)}
                    onChange={e => setRuleDays(cur => (e.target.checked ? [...cur, d.value] : cur.filter(x => x !== d.value)))}
                  />
                  {d.label}
                </label>
              ))}
            </div>
          </fieldset>
          <div className="grid grid-cols-2 gap-3">
            <FormField label="Desde" type="time" value={ruleStart} onChange={e => setRuleStart(e.target.value)} />
            <FormField label="Hasta" type="time" value={ruleEnd} onChange={e => setRuleEnd(e.target.value)} />
          </div>
          <Button type="submit" size="sm" className="disabled:bg-tc-surface disabled:text-tc-text-muted disabled:opacity-100 disabled:border disabled:border-tc-border" loading={addRules.isPending} disabled={!ruleMechanic || ruleDays.length === 0 || ruleStart >= ruleEnd}>
            Agregar horario
          </Button>
        </form>
      </section>

      {/* ── Días bloqueados / horario especial ────────────────────────────── */}
      <section className="overflow-hidden border border-tc-border bg-white">
        <h2 className="border-b border-tc-border bg-tc-stub-bg px-4 py-2 text-[11px] font-bold tracking-[0.14em] text-tc-text-muted uppercase">
          Feriados, vacaciones y horarios especiales (próximos 12 meses)
        </h2>

        <ul className="divide-y divide-tc-border">
          {exLoading && <li className="px-4 py-3 text-sm text-tc-text-muted">Cargando…</li>}
          {!exLoading && (exceptions?.length ?? 0) === 0 && <li className="px-4 py-3 text-sm text-tc-text-muted">No hay días especiales cargados.</li>}
          {exceptions?.map(e => (
            <li key={e.id} className="flex flex-wrap items-center gap-x-4 gap-y-1 px-4 py-2 text-sm">
              <span className="font-bold tabular-nums text-tc-text">{formatDateOnly(e.date)}</span>
              <span className="text-tc-text-secondary">{e.mechanicMembershipId ? nameOf(e.mechanicMembershipId, e.mechanicName ?? '') : 'Todo el taller'}</span>
              <span className={e.isBlocked ? 'font-bold text-tc-stamp-red' : 'text-amber-700'}>
                {e.isBlocked ? 'Bloqueado' : `Horario ${hhmm(e.startTime)}–${hhmm(e.endTime)}`}
              </span>
              {e.reason && <span className="text-tc-text-muted">{e.reason}</span>}
              <button type="button" onClick={() => removeException(e)} className="ml-auto min-h-11 px-2 text-xs font-bold text-tc-text-muted uppercase transition-colors hover:text-tc-stamp-red">
                Quitar
              </button>
            </li>
          ))}
        </ul>

        <form
          className="space-y-3 border-t border-tc-border p-4 [&>*]:max-w-xl"
          onSubmit={ev => {
            ev.preventDefault()
            addException.mutate()
          }}
        >
          <p className="text-xs font-bold tracking-wider text-tc-text-muted uppercase">Agregar día</p>
          <div className="grid gap-3 sm:grid-cols-2">
            <FormField label="Fecha" requiredMark type="date" value={exDate} min={today} onChange={e => setExDate(e.target.value)} />
            <FormSelect label="Aplica a" value={exMechanic} onChange={e => setExMechanic(e.target.value)}>
              <option value="">Todo el taller</option>
              {mechanics?.map(m => <option key={m.membershipId} value={m.membershipId}>{m.displayName}</option>)}
            </FormSelect>
          </div>
          <fieldset className="flex flex-wrap gap-4 text-sm text-tc-text-secondary">
            <legend className="sr-only">Tipo</legend>
            <label className="flex min-h-11 cursor-pointer items-center gap-2">
              <input type="radio" name="ex-kind" className="accent-tc-purple" checked={exKind === 'blocked'} onChange={() => setExKind('blocked')} />
              Día bloqueado (no se agenda)
            </label>
            <label className="flex min-h-11 cursor-pointer items-center gap-2">
              <input type="radio" name="ex-kind" className="accent-tc-purple" checked={exKind === 'special'} onChange={() => setExKind('special')} />
              Horario especial
            </label>
          </fieldset>
          {exKind === 'special' && (
            <div className="grid grid-cols-2 gap-3">
              <FormField label="Desde" type="time" value={exStart} onChange={e => setExStart(e.target.value)} />
              <FormField label="Hasta" type="time" value={exEnd} onChange={e => setExEnd(e.target.value)} />
            </div>
          )}
          <FormField label="Motivo (opcional)" value={exReason} onChange={e => setExReason(e.target.value)} placeholder="Feriado, vacaciones, capacitación…" />
          <Button type="submit" size="sm" className="disabled:bg-tc-surface disabled:text-tc-text-muted disabled:opacity-100 disabled:border disabled:border-tc-border" loading={addException.isPending} disabled={!exDate || (exKind === 'special' && exStart >= exEnd)}>
            Agregar
          </Button>
        </form>
      </section>
    </div>
  )
}
