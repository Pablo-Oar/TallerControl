import { Link } from 'react-router'
import type { ReactNode } from 'react'

/** Color de acento de un indicador o bloque: define el tinte del ícono y, en los KPIs, el del número. */
export type Accent = 'green' | 'red' | 'blue' | 'amber' | 'purple' | 'slate'

const TILE: Record<Accent, string> = {
  green: 'bg-tc-done-bg text-tc-done-text',
  red: 'bg-red-50 text-tc-stamp-red',
  blue: 'bg-tc-inprog-bg text-tc-inprog-text',
  amber: 'bg-amber-50 text-amber-600',
  purple: 'bg-tc-purple-light text-tc-purple-mid',
  slate: 'bg-tc-stub-bg text-tc-navy-text-dim',
}

const VALUE: Record<Accent, string> = {
  green: 'text-tc-done-text',
  red: 'text-tc-stamp-red',
  blue: 'text-tc-text',
  amber: 'text-tc-text',
  purple: 'text-tc-text',
  slate: 'text-tc-text',
}

/** Cuadro con el ícono, tintado según el acento. */
export function IconTile({ accent, children, small = false }: { accent: Accent; children: ReactNode; small?: boolean }) {
  return (
    <span
      aria-hidden
      className={[
        'flex flex-none items-center justify-center',
        small ? 'h-7 w-7 rounded-lg [&>svg]:h-4 [&>svg]:w-4' : 'h-10 w-10 rounded-xl',
        TILE[accent],
      ].join(' ')}
    >
      {children}
    </span>
  )
}

/** Color de cada tramo de la barra y de cada cifra del pie. */
export type BarTone = 'green' | 'red' | 'blue' | 'amber' | 'purple' | 'slate'

const BAR: Record<BarTone, string> = {
  green: 'bg-emerald-500',
  red: 'bg-tc-stamp-red',
  blue: 'bg-blue-500',
  amber: 'bg-amber-400',
  purple: 'bg-tc-purple',
  slate: 'bg-slate-300',
}

const STAT_TEXT: Record<BarTone, string> = {
  green: 'text-tc-done-text',
  red: 'text-tc-stamp-red',
  blue: 'text-tc-inprog-text',
  amber: 'text-amber-700',
  purple: 'text-tc-purple-mid',
  slate: 'text-tc-text',
}

export interface KpiBarSegment {
  value: number
  tone: BarTone
  label: string
}

export interface KpiStat {
  label: string
  value: ReactNode
  tone?: BarTone
}

interface KpiCardProps {
  label: string
  value: string | number
  accent?: Accent
  icon: ReactNode
  /** Barra de progreso segmentada bajo el valor. */
  bar?: KpiBarSegment[]
  /** Hasta dos cifras al pie: la primera a la izquierda y la segunda a la derecha. */
  stats?: KpiStat[]
  /** Línea de detalle bajo el valor cuando no hay barra ni cifras. */
  sub?: ReactNode
  to?: string
}

/**
 * Indicador del Inicio: marco redondeado con la tarjeta adentro, sello de color arriba a la derecha, valor grande,
 * barra de progreso y dos cifras al pie (estilo «bisel» de las pantallas de acceso).
 */
export function KpiCard({ label, value, accent = 'slate', icon, bar, stats, sub, to }: KpiCardProps) {
  const total = bar?.reduce((sum, seg) => sum + seg.value, 0) ?? 0

  const inner = (
    <div className="group h-full rounded-[22px] border border-slate-300 bg-slate-200 p-1.5 shadow-[0_18px_36px_-14px_rgba(15,23,42,0.45)] transition-all duration-300 hover:shadow-[0_28px_50px_-14px_rgba(15,23,42,0.55)] motion-safe:hover:-translate-y-1">
      <div className="flex h-full flex-col gap-3 rounded-2xl border border-slate-200 bg-white px-4 py-4 shadow-sm">
        <div className="flex items-start justify-between gap-2">
          <span className="text-[13px] font-medium leading-tight text-tc-text-secondary">{label}</span>
          <IconTile accent={accent}>{icon}</IconTile>
        </div>

        <span className={['tabular-nums text-2xl font-extrabold leading-none tracking-tight sm:text-[28px]', VALUE[accent]].join(' ')}>{value}</span>

        {bar && total > 0 && (
          <div
            role="img"
            aria-label={bar.map(seg => `${seg.label}: ${seg.value}`).join(', ')}
            className="flex h-2 w-full gap-0.5 overflow-hidden rounded-full bg-tc-border"
          >
            {bar.filter(seg => seg.value > 0).map(seg => (
              <span key={seg.label} className={['h-full', BAR[seg.tone]].join(' ')} style={{ width: `${(seg.value / total) * 100}%` }} />
            ))}
          </div>
        )}

        {stats && stats.length > 0 ? (
          <div className="mt-auto flex items-end justify-between gap-3 text-xs">
            {stats.slice(0, 2).map((st, i) => (
              <div key={st.label} className={i === 1 ? 'text-right' : ''}>
                <p className="text-tc-text-muted">{st.label}</p>
                <p className={['text-sm font-bold tabular-nums', STAT_TEXT[st.tone ?? 'slate']].join(' ')}>{st.value}</p>
              </div>
            ))}
          </div>
        ) : (
          sub && <div className="mt-auto text-xs leading-snug text-tc-text-secondary">{sub}</div>
        )}
      </div>
    </div>
  )

  return to ? (
    <Link to={to} className="block h-full rounded-[22px] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-tc-purple focus-visible:ring-offset-2">
      {inner}
    </Link>
  ) : (
    inner
  )
}

interface WidgetShellProps {
  title: string
  icon?: ReactNode
  accent?: Accent
  /** Número junto al título (p. ej. cuántos hay en total). */
  count?: number
  linkTo?: string
  linkLabel?: string
  children: ReactNode
}

/** Marco común de los bloques del Inicio: encabezado con ícono, título y enlace «Ver todas». */
export function WidgetShell({ title, icon, accent = 'slate', count, linkTo, linkLabel = 'Ver todas', children }: WidgetShellProps) {
  return (
    <section className="min-w-0 overflow-hidden rounded-2xl border border-tc-border bg-white shadow-sm" aria-label={title}>
      <div className="flex items-center justify-between gap-3 border-b border-tc-border bg-tc-stub-bg px-4 py-1.5">
        <h2 className="flex items-center gap-2.5 text-[11px] font-bold tracking-[0.14em] uppercase text-tc-text-secondary">
          {icon && <IconTile accent={accent} small>{icon}</IconTile>}
          {title}
          {count !== undefined && count > 0 && (
            <span className="bg-tc-purple px-1.5 py-0.5 text-[11px] font-bold tabular-nums text-white">{count}</span>
          )}
        </h2>
        {linkTo && (
          <Link to={linkTo} className="flex min-h-11 flex-none items-center whitespace-nowrap text-[11px] font-bold tracking-wider uppercase text-tc-purple hover:underline">
            {linkLabel}
          </Link>
        )}
      </div>
      {children}
    </section>
  )
}

export function WidgetEmpty({ children }: { children: ReactNode }) {
  return <p className="px-4 py-6 text-sm text-tc-text-muted">{children}</p>
}

/** Etiqueta corta de énfasis para filas de lista (p. ej. «−2 u.»). */
export function Pill({ tone, children }: { tone: 'danger' | 'warn'; children: ReactNode }) {
  const cls =
    tone === 'danger'
      ? 'border-red-300 bg-red-50 text-red-700'
      : 'border-amber-300 bg-amber-50 text-amber-700'
  return (
    <span className={['inline-block whitespace-nowrap border px-1.5 py-0.5 text-[11px] font-bold tracking-wider uppercase', cls].join(' ')}>
      {children}
    </span>
  )
}

/** Franja de estado a todo el ancho (verde = todo bien, ámbar = atención, rojo = problema). */
export function StatusStrip({ tone, icon, children }: { tone: 'green' | 'amber' | 'red'; icon?: ReactNode; children: ReactNode }) {
  const cls =
    tone === 'green'
      ? 'bg-tc-done-bg text-tc-done-text'
      : tone === 'amber'
        ? 'bg-amber-50 text-amber-800'
        : 'bg-red-50 text-red-700'
  return (
    <p className={['flex items-center gap-2 px-4 py-2.5 text-xs font-medium', cls].join(' ')}>
      {icon && <span aria-hidden className="flex-none [&>svg]:h-4 [&>svg]:w-4">{icon}</span>}
      <span>{children}</span>
    </p>
  )
}
