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
    <ol aria-label={label} className={cn('relative space-y-4 border-l border-tc-border pl-5', className)}>
      {items.map(item => {
        const kind = item.kind ?? 'info'
        return (
          <li key={item.id} className={cn('relative', item.emphasis && 'bg-tc-done-bg/60 -ml-2 pl-2 py-1')}>
            <span
              aria-hidden
              className={cn('absolute top-1.5 h-2.5 w-2.5', MARKER[kind], item.emphasis ? '-left-[29px]' : '-left-[25px]')}
            />
            <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-0.5">
              <div className="flex flex-wrap items-baseline gap-x-2 text-sm text-tc-text">
                {item.href ? (
                  <Link to={item.href} className="font-semibold text-tc-purple hover:text-tc-purple-mid">
                    {item.title}
                  </Link>
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
          </li>
        )
      })}
    </ol>
  )
}
