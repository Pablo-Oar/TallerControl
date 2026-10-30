import { Link } from 'react-router'
import { useCan } from '../../../lib/auth/permissions'
import { CardIcon, PlusIcon, StoreIcon } from './icons'

const ACTIONS = [
  { label: 'Venta de mostrador', to: '/app/counter-sales?nueva=1', perm: 'counter_sales.write', color: 'bg-emerald-700 hover:bg-emerald-800', icon: <StoreIcon /> },
  { label: 'Registrar cobro', to: '/app/work-orders?pendientes=1', perm: 'payments.write', color: 'bg-blue-700 hover:bg-blue-800', icon: <CardIcon /> },
  { label: 'Nueva orden', to: '/app/work-orders?nueva=1', perm: 'workorders.write', color: 'bg-tc-purple hover:bg-tc-purple-mid', icon: <PlusIcon /> },
  { label: 'Nuevo presupuesto', to: '/app/quotes?nueva=1', perm: 'quotes.write', color: 'bg-indigo-800 hover:bg-indigo-900', icon: <PlusIcon /> },
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
            'flex min-h-12 items-center justify-center gap-2 rounded-2xl px-2 py-2 text-center text-[13px] font-semibold leading-tight tracking-wide text-white shadow-sm transition-colors sm:px-3 sm:text-sm',
            a.color,
          ].join(' ')}
        >
          <span aria-hidden className="hidden flex-none sm:block [&>svg]:h-[18px] [&>svg]:w-[18px]">{a.icon}</span>
          {a.label}
        </Link>
      ))}
    </nav>
  )
}
