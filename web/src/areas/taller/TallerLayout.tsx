import type { ReactNode } from 'react'
import { NavLink, useNavigate } from 'react-router'
import { useAuth } from '../../lib/auth/AuthContext'
import { hasPermissions } from '../../lib/auth/permissions'
import { NotificationBell } from '../../components/NotificationBell'
import { PlateSearchBar } from '../../components/PlateSearchBar'
import { useState } from 'react'

interface NavItem {
  to: string
  label: string
  end?: boolean
  /** Permisos que hacen falta (todos) para ver la opción. */
  perms?: string[]
}

interface NavGroup {
  label: string
  items: NavItem[]
}

const NAV_GROUPS: NavGroup[] = [
  {
    label: 'Operaciones',
    items: [
      { to: '/app', label: 'Inicio', end: true, perms: ['workorders.read', 'payments.read'] },
      { to: '/app/customers', label: 'Clientes', perms: ['customers.read'] },
      { to: '/app/vehicles', label: 'Vehículos', perms: ['vehicles.read'] },
      { to: '/app/work-orders', label: 'Órdenes', perms: ['workorders.read'] },
      { to: '/app/quotes', label: 'Presupuestos', perms: ['quotes.read'] },
      { to: '/app/appointments', label: 'Agenda', perms: ['appointments.read'] },
    ],
  },
  {
    label: 'Taller',
    items: [
      { to: '/app/catalog', label: 'Catálogo', perms: ['catalog.read'] },
      { to: '/app/cash', label: 'Caja', perms: ['cash.read'] },
      { to: '/app/purchases', label: 'Compras', perms: ['purchases.read'] },
      { to: '/app/counter-sales', label: 'Mostrador', perms: ['counter_sales.read'] },
      { to: '/app/suppliers', label: 'Proveedores', perms: ['suppliers.read'] },
    ],
  },
  {
    label: 'Gestión',
    items: [
      { to: '/app/reports', label: 'Reportes', perms: ['workorders.read', 'payments.read'] },
      { to: '/app/invoicing', label: 'Facturación', perms: ['invoicing.read'] },
      { to: '/app/qr', label: 'Tarjetas QR', perms: ['qr.manage'] },
      { to: '/app/reminders', label: 'Recordatorios', perms: ['reminders.manage'] },
    ],
  },
  {
    label: 'Configuración',
    items: [
      { to: '/app/settings/profile', label: 'Mi perfil' },
      { to: '/app/settings/general', label: 'General', perms: ['settings.manage'] },
      { to: '/app/settings/users', label: 'Usuarios', perms: ['users.manage'] },
    ],
  },
]

function SidebarLink({ to, label, end }: NavItem) {
  return (
    <NavLink
      to={to}
      end={end}
      className={({ isActive }) =>
        isActive
          ? 'flex items-center border-l-2 border-tc-purple pl-4 py-2.5 text-sm font-medium text-white lg:py-1.5'
          : 'flex items-center border-l-2 border-transparent pl-4 py-2.5 text-sm text-tc-navy-text lg:py-1.5 hover:text-white transition-colors'
      }
    >
      {label}
    </NavLink>
  )
}

/** Primera pantalla del panel a la que la sesión tiene acceso (p. ej. Facturación para un Contador). */
export function firstAllowedPath(user: { permissions?: string[] } | null): string {
  for (const group of NAV_GROUPS) {
    const item = group.items.find(i => hasPermissions(user, ...(i.perms ?? [])))
    if (item) return item.to
  }
  return '/login'
}

export function TallerLayout({ children }: { children: ReactNode }) {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const [loggingOut, setLoggingOut] = useState(false)
  const [sidebarOpen, setSidebarOpen] = useState(false)

  const role = user?.role ?? ''

  async function handleLogout() {
    setLoggingOut(true)
    try {
      await logout()
    } finally {
      setLoggingOut(false)
      navigate('/login', { replace: true })
    }
  }

  function itemVisible(item: NavItem): boolean {
    return hasPermissions(user, ...(item.perms ?? []))
  }

  return (
    <div className="flex h-dvh flex-col bg-tc-navy overflow-hidden">
      {/* ── Carrier bar ────────────────────────────────────── */}
      <header className="flex h-[52px] flex-none items-center gap-4 border-b border-tc-navy-mid px-4">
        {/* Hamburger — mobile only */}
        <button
          className="-ml-2 flex h-11 w-11 shrink-0 items-center justify-center text-tc-navy-text hover:text-white transition-colors lg:hidden"
          onClick={() => setSidebarOpen(v => !v)}
          aria-label="Menú"
        >
          <svg className="h-5 w-5" viewBox="0 0 20 20" fill="currentColor" aria-hidden>
            <path fillRule="evenodd" d="M3 5h14a1 1 0 010 2H3a1 1 0 010-2zm0 4h14a1 1 0 010 2H3a1 1 0 010-2zm0 4h14a1 1 0 010 2H3a1 1 0 010-2z" clipRule="evenodd" />
          </svg>
        </button>

        {/* Wordmark */}
        <span className="select-none text-sm font-bold tracking-widest text-white uppercase shrink-0">
          TALLER<span className="text-tc-purple">CONTROL</span>
        </span>

        {/* Search — hidden on mobile, visible from md */}
        <div className="hidden md:flex flex-1 max-w-sm">
          <PlateSearchBar onNavigate={navigate} />
        </div>

        {/* User + logout */}
        <div className="ml-auto flex items-center gap-1 sm:gap-3">
          {/* Search icon — mobile only, goes to vehicles search */}
          <button
            className="flex h-11 w-11 items-center justify-center text-tc-navy-text hover:text-white transition-colors md:hidden"
            onClick={() => navigate('/app/vehicles')}
            aria-label="Buscar"
          >
            <svg className="h-4 w-4" viewBox="0 0 20 20" fill="currentColor" aria-hidden>
              <path fillRule="evenodd" d="M8 4a4 4 0 100 8 4 4 0 000-8zM2 8a6 6 0 1110.89 3.476l4.817 4.817a1 1 0 01-1.414 1.414l-4.816-4.816A6 6 0 012 8z" clipRule="evenodd" />
            </svg>
          </button>
          <NotificationBell />
          <span className="hidden text-xs text-tc-navy-text lg:block truncate max-w-[160px]">
            {user?.user.displayName}
          </span>
          <button
            disabled={loggingOut}
            onClick={handleLogout}
            className="flex h-11 items-center gap-1.5 border border-tc-navy-mid px-3 text-xs font-medium lg:h-7 text-tc-navy-text hover:border-tc-navy-text hover:text-white transition-colors disabled:opacity-50"
          >
            {loggingOut ? (
              <svg className="h-3 w-3 animate-spin" viewBox="0 0 24 24" fill="none" aria-hidden>
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z" />
              </svg>
            ) : (
              <svg className="h-3 w-3" viewBox="0 0 20 20" fill="currentColor" aria-hidden>
                <path fillRule="evenodd" d="M3 3a1 1 0 011 1v12a1 1 0 11-2 0V4a1 1 0 011-1zm10.293 9.293a1 1 0 001.414 1.414l3-3a1 1 0 000-1.414l-3-3a1 1 0 10-1.414 1.414L14.586 9H7a1 1 0 100 2h7.586l-1.293 1.293z" clipRule="evenodd" />
              </svg>
            )}
            Salir
          </button>
        </div>
      </header>

      {/* ── Body row: sidebar + content ────────────────────── */}
      <div className="flex flex-1 overflow-hidden">
        {/* Sidebar overlay backdrop — mobile */}
        {sidebarOpen && (
          <div
            className="fixed inset-0 z-20 bg-black/50 lg:hidden"
            onClick={() => setSidebarOpen(false)}
          />
        )}

        {/* Sidebar */}
        <aside
          className={[
            'flex w-[220px] flex-none flex-col bg-tc-navy-deep overflow-y-auto',
            'fixed bottom-0 top-[52px] left-0 z-30 transition-transform duration-200',
            'lg:relative lg:inset-auto lg:translate-x-0',
            sidebarOpen ? 'translate-x-0' : '-translate-x-full',
          ].join(' ')}
        >
          <nav className="flex flex-col gap-6 py-5 px-2" onClick={() => setSidebarOpen(false)}>
            {NAV_GROUPS.map(group => {
              const visibleItems = group.items.filter(item => itemVisible(item))
              if (visibleItems.length === 0) return null

              return (
                <div key={group.label}>
                  <p className="mb-1.5 pl-4 text-[11px] font-bold tracking-[0.15em] text-tc-navy-text-dim uppercase">
                    {group.label}
                  </p>
                  {visibleItems.map(item => (
                    <SidebarLink key={item.to} {...item} />
                  ))}
                </div>
              )
            })}
          </nav>

          {/* Role chip at bottom of sidebar */}
          <div className="mt-auto px-4 pb-5">
            <span className="inline-flex items-center border border-tc-navy-mid px-2 py-0.5 text-[11px] font-bold tracking-widest text-tc-navy-text uppercase">
              {({ owner: 'Dueño', admin_staff: 'Administrativo', mechanic: 'Mecánico', accountant: 'Contador' } as Record<string, string>)[role] ?? role}
            </span>
          </div>
        </aside>

        {/* Content surface */}
        <main className="flex-1 overflow-y-auto bg-white">
          <div className="min-h-full p-4 pb-6 md:p-6">
            {children}
          </div>
        </main>
      </div>
    </div>
  )
}
