import { formatDate, formatDateTime } from '../../lib/dates'
import { useState, type ReactNode } from 'react'
import { Link } from 'react-router'
import { cn } from '@/lib/utils'

/**
 * Línea de tiempo del sistema (UI-R2 F4, D-24/D-25 "Activities Card"). Cada pantalla mapea su propio DTO a
 * `TimelineItem`: el componente no conoce endpoints ni reglas de negocio y solo muestra lo que recibe (en la
 * superficie pública, únicamente los campos que ya expone su DTO). Los detalles largos se pliegan con un botón
 * operable con toque y teclado (`aria-expanded`). Propio: `activities-card` exige motion y react-icons.
 */

export type TimelineKind = 'status' | 'order' | 'quote' | 'response' | 'info'

export interface TimelineItem {
  id: string
  /** ISO 8601. */
  date: string
  title: ReactNode
  detail?: string | null
  kind?: TimelineKind
  /** Quién lo hizo (opcional). */
  author?: string | null
  /** Sello o dato a la derecha del título (p. ej. un `StatusBadge`). */
  badge?: ReactNode
  /** Si se indica, el título es un enlace. */
  href?: string
  /** Si se indica, el título es un botón que la ejecuta (p. ej. abrir el detalle). */
  onSelect?: () => void
  /** Clase de color del punto (pisa el del tipo): p. ej. el color del estado al que pasó la orden. */
  markerClass?: string
  /** Resalta el ítem (p. ej. la respuesta vigente). */
  emphasis?: boolean
}

const MARKER: Record<TimelineKind, string> = {
  status: 'bg-tc-purple',
  order: 'bg-tc-navy',
  quote: 'bg-tc-inprog-text',
  response: 'bg-tc-done-text',
  info: 'bg-tc-navy-text',
}

/** Color del punto de cada estado de la orden, a tono con su sello (`StatusBadge`). */
export const WORK_ORDER_MARKER: Record<string, string> = {
  pending: 'bg-tc-warning-solid',
  in_progress: 'bg-tc-info-solid',
  waiting_parts: 'bg-tc-warning-solid',
  completed: 'bg-tc-done-text',
  delivered: 'bg-tc-success-solid',
  cancelled: 'bg-tc-neutral-solid',
}

/** Ídem para los estados del presupuesto. */
export const QUOTE_MARKER: Record<string, string> = {
  draft: 'bg-tc-neutral-solid',
  sent: 'bg-tc-info-solid',
  approved: 'bg-tc-done-text',
  partially_approved: 'bg-tc-success-solid',
  rejected: 'bg-tc-danger-solid',
  expired: 'bg-tc-neutral-solid',
  converted: 'bg-tc-purple',
  cancelled: 'bg-tc-neutral-solid',
}

const COLLAPSE_OVER = 120

function formatWhen(iso: string, withTime: boolean) {
  const d = new Date(iso)
  return withTime
    ? formatDateTime(d)
    : formatDate(d)
}

function Detail({ text }: { text: string }) {
  const [open, setOpen] = useState(false)
  const long = text.length > COLLAPSE_OVER
  return (
    <div className="mt-1 text-sm text-tc-text-secondary">
      <p className={cn(long && !open && 'line-clamp-2')}>{text}</p>
      {long && (
        <button
          type="button"
          aria-expanded={open}
          onClick={() => setOpen(o => !o)}
          className="mt-0.5 min-h-[32px] text-xs font-bold uppercase tracking-wider text-tc-purple hover:text-tc-purple-mid"
        >
          {open ? 'Ver menos' : 'Ver más'}
        </button>
      )}
    </div>
  )
}

interface Props {
  items: TimelineItem[]
  /** Texto cuando no hay ítems. */
  empty?: string
  /** Mostrar la hora además de la fecha (por defecto sí). */
  showTime?: boolean
  /** Nombre accesible de la lista. */
  label?: string
  className?: string
}

export function Timeline({ items, empty = 'Sin movimientos todavía.', showTime = true, label, className }: Props) {
  if (items.length === 0) return <p className="text-sm text-tc-text-muted">{empty}</p>
  return (
    <ol
      aria-label={label}
      className={cn(
        'relative space-y-3 pl-9 before:absolute before:bottom-3 before:left-[11px] before:top-3 before:w-px before:bg-tc-border-mid',
        className,
      )}
    >
      {items.map(item => {
        const kind = item.kind ?? 'info'
        return (
          <li key={item.id} className="relative">
            <span
              aria-hidden
              className="absolute -left-9 top-3 flex h-6 w-6 items-center justify-center rounded-full bg-white ring-1 ring-tc-border-mid"
            >
              <span className={cn('h-2.5 w-2.5 rounded-full', item.markerClass ?? MARKER[kind])} />
            </span>
            <div
              className={cn(
                'rounded-xl border px-3.5 py-2.5 transition-shadow hover:shadow-raised',
                item.emphasis ? 'border-tc-success-border bg-tc-success-bg' : 'border-tc-border bg-white',
              )}
            >
              <div className="flex flex-wrap items-center justify-between gap-x-3 gap-y-1">
                <div className="flex flex-wrap items-baseline gap-x-2 text-sm text-tc-text">
                  {item.href ? (
                    <Link to={item.href} className="font-semibold text-tc-purple hover:text-tc-purple-mid">
                      {item.title}
                    </Link>
                  ) : item.onSelect ? (
                    <button
                      type="button"
                      onClick={item.onSelect}
                      className="min-h-[32px] font-semibold text-tc-purple hover:text-tc-purple-mid"
                    >
                      {item.title}
                    </button>
                  ) : (
                    <span className="font-semibold">{item.title}</span>
                  )}
                  {item.author && <span className="text-xs text-tc-text-muted">por {item.author}</span>}
                </div>
                <div className="flex items-center gap-2">
                  {item.badge}
                  <time dateTime={item.date} className="text-xs tabular-nums text-tc-text-muted">
                    {formatWhen(item.date, showTime)}
                  </time>
                </div>
              </div>
              {item.detail && <Detail text={item.detail} />}
              {item.onSelect && (
                <button
                  type="button"
                  onClick={item.onSelect}
                  className="mt-1 min-h-[36px] text-xs font-bold uppercase tracking-wider text-tc-purple hover:text-tc-purple-mid"
                >
                  Ver qué se hizo →
                </button>
              )}
            </div>
          </li>
        )
      })}
    </ol>
  )
}
