import { Link } from 'react-router'
import type { ReactNode } from 'react'

export type Tone = 'default' | 'danger' | 'warn' | 'good'

const TONE_TEXT: Record<Tone, string> = {
  default: 'text-tc-text',
  danger: 'text-tc-stamp-red',
  warn: 'text-amber-600',
  good: 'text-tc-done-text',
}

interface KpiCardProps {
  label: string
  value: string | number
  tone?: Tone
  /** Línea de detalle bajo el valor (puede llevar énfasis propio). */
  sub?: ReactNode
  to?: string
}

/** Cupón de indicador: talón, línea perforada y cuerpo (identidad «Boarding Pass»). */
export function KpiCard({ label, value, tone = 'default', sub, to }: KpiCardProps) {
  const inner = (
    <div className="group flex h-full overflow-hidden border border-tc-border bg-white transition-colors hover:border-tc-border-mid">
      <div className="w-2 flex-none bg-tc-stub-bg" />
      <div className="perf-line flex-none self-stretch" />
      <div className="flex min-w-0 flex-1 flex-col gap-2 px-4 py-4">
        <span className="text-[11px] font-bold leading-tight tracking-[0.12em] uppercase text-tc-text-secondary">{label}</span>
        <span className={['tabular-nums text-xl font-bold leading-none sm:text-2xl', TONE_TEXT[tone]].join(' ')}>{value}</span>
        {sub && <div className="text-xs leading-snug text-tc-text-secondary">{sub}</div>}
      </div>
      {to && (
        <div className="hidden w-8 flex-none items-center justify-center self-stretch border-l sm:flex border-tc-border bg-tc-stub-bg text-tc-navy-text-dim transition-colors group-hover:text-tc-navy-text">
          <svg className="h-3 w-3" viewBox="0 0 12 12" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden>
            <path d="M2 6h8M6 2l4 4-4 4" strokeLinecap="round" strokeLinejoin="round" />
          </svg>
        </div>
      )}
    </div>
  )

  return to ? (
    <Link to={to} className="block h-full focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-tc-purple focus-visible:ring-offset-1">
      {inner}
    </Link>
  ) : (
    inner
  )
}

interface WidgetShellProps {
  title: string
  /** Número junto al título (p. ej. cuántos hay en total). */
  count?: number
  linkTo?: string
  linkLabel?: string
  children: ReactNode
}

/** Marco común de los bloques del Inicio: encabezado con título y enlace «Ver todas». */
export function WidgetShell({ title, count, linkTo, linkLabel = 'Ver todas', children }: WidgetShellProps) {
  return (
    <section className="min-w-0 border border-tc-border bg-white" aria-label={title}>
      <div className="flex items-center justify-between gap-3 border-b border-tc-border bg-tc-stub-bg px-4 py-1">
        <h2 className="flex items-center gap-2 text-[11px] font-bold tracking-[0.14em] uppercase text-tc-text-secondary">
          {title}
          {count !== undefined && count > 0 && (
            <span className="bg-tc-purple px-1.5 py-0.5 text-[10px] font-bold tabular-nums text-white">{count}</span>
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

/** Etiqueta corta de énfasis (p. ej. «hace 12 días») para filas de lista. */
export function Pill({ tone, children }: { tone: Exclude<Tone, 'default' | 'good'>; children: ReactNode }) {
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
