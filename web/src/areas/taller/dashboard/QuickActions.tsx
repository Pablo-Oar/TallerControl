import { Link } from 'react-router'
import { useCan } from '../../../lib/auth/permissions'

const ACTIONS = [
  { label: 'Nueva orden', to: '/app/work-orders?nueva=1', perm: 'workorders.write', primary: true },
  { label: 'Nuevo presupuesto', to: '/app/quotes?nueva=1', perm: 'quotes.write', primary: false },
  { label: 'Registrar cobro', to: '/app/work-orders?pendientes=1', perm: 'payments.write', primary: false },
  { label: 'Venta de mostrador', to: '/app/counter-sales?nueva=1', perm: 'counter_sales.write', primary: false },
] as const

/** Accesos directos a lo que más se hace en el día; cada uno aparece solo si el rol puede hacerlo. */
export function QuickActions() {
  const can = useCan()
  const actions = ACTIONS.filter(a => can(a.perm))
  if (actions.length === 0) return null

  return (
    <nav aria-label="Acciones rápidas" className="grid grid-cols-2 gap-2 md:grid-cols-4">
      {actions.map(a => (
        <Link
          key={a.to}
          to={a.to}
          className={[
            'flex min-h-11 items-center justify-center gap-2 border px-2 py-2 text-center text-[13px] font-medium leading-tight tracking-wide transition-colors sm:px-3 sm:text-sm',
            a.primary
              ? 'border-tc-purple bg-tc-purple text-white hover:bg-tc-purple-mid'
              : 'border-tc-border bg-white text-tc-text hover:border-tc-purple hover:text-tc-purple',
          ].join(' ')}
        >
          <svg className="hidden h-3.5 w-3.5 flex-none sm:block" viewBox="0 0 12 12" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden>
            <path d="M6 1v10M1 6h10" strokeLinecap="round" />
          </svg>
          {a.label}
        </Link>
      ))}
    </nav>
  )
}
