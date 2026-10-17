import type { ReactNode } from 'react'
import { NavLink, useNavigate } from 'react-router'
import { useAuth } from '../../lib/auth/AuthContext'
import { PlateSearchBar } from '../../components/PlateSearchBar'
import { useState } from 'react'

interface NavItem {
  to: string
  label: string
  end?: boolean
  roles?: string[]
}

interface NavGroup {
  label: string
  items: NavItem[]
  roles?: string[]
}

const NAV_GROUPS: NavGroup[] = [
  {
    label: 'Operaciones',
    items: [
      { to: '/app', label: 'Inicio', end: true },
      { to: '/app/customers', label: 'Clientes' },
      { to: '/app/vehicles', label: 'Vehículos' },
      { to: '/app/work-orders', label: 'Órdenes' },
      { to: '/app/quotes', label: 'Presupuestos' },
      { to: '/app/appointments', label: 'Agenda' },
    ],
  },
  {
    label: 'Taller',
    roles: ['owner', 'admin_staff'],
    items: [
      { to: '/app/catalog', label: 'Catálogo' },
      { to: '/app/cash', label: 'Caja' },
      { to: '/app/purchases', label: 'Compras' },
      { to: '/app/counter-sales', label: 'Mostrador' },
      { to: '/app/suppliers', label: 'Proveedores' },
    ],
  },
  {
    label: 'Gestión',
    items: [
      { to: '/app/reports', label: 'Reportes', roles: ['owner', 'admin_staff'] },
      { to: '/app/invoicing', label: 'Facturación', roles: ['owner', 'admin_staff', 'accountant'] },
      { to: '/app/qr', label: 'Tarjetas QR', roles: ['owner', 'admin_staff'] },
      { to: '/app/reminders', label: 'Recordatorios', roles: ['owner', 'admin_staff'] },
    ],
  },
  {
    label: 'Configuración',
    roles: ['owner'],
    items: [
      { to: '/app/settings/general', label: 'General' },
      { to: '/app/settings/users', label: 'Usuarios' },
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
          ? 'flex items-center border-l-2 border-tc-purple pl-4 py-1.5 text-sm font-medium text-white'
          : 'flex items-center border-l-2 border-transparent pl-4 py-1.5 text-sm text-tc-navy-text hover:text-white transition-colors'
      }
    >
      {label}
    </NavLink>
  )
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

  function groupVisible(group: NavGroup): boolean {
    if (!group.roles) return true
    return group.roles.includes(role)
  }

  function itemVisible(item: NavItem): boolean {
    if (!item.roles) return true
    return item.roles.includes(role)
  }

  return (
    <div className="flex h-screen flex-col bg-tc-navy overflow-hidden">
      {/* ── Carrier bar ────────────────────────────────────── */}
      <header className="flex h-[52px] flex-none items-center gap-4 border-b border-tc-navy-mid px-4">
        {/* Hamburger — mobile only */}
        <button
          className="flex h-8 w-8 items-center justify-center text-tc-navy-text hover:text-white transition-colors lg:hidden"
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

        {/* Search */}
        <div className="flex-1 max-w-sm">
          <PlateSearchBar />
        </div>

        {/* User + logout */}
        <div className="ml-auto flex items-center gap-3">
          <span className="hidden text-xs text-tc-navy-text sm:block truncate max-w-[160px]">
            {user?.user.displayName}
          </span>
          <button
            disabled={loggingOut}
            onClick={handleLogout}
            className="flex h-7 items-center gap-1.5 border border-tc-navy-mid px-3 text-xs font-medium text-tc-navy-text hover:border-tc-navy-text hover:text-white transition-colors disabled:opacity-50"
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
            'fixed inset-y-[52px] left-0 z-30 transition-transform duration-200',
            'lg:relative lg:inset-auto lg:translate-x-0',
            sidebarOpen ? 'translate-x-0' : '-translate-x-full',
          ].join(' ')}
        >
          <nav className="flex flex-col gap-6 py-5 px-2">
            {NAV_GROUPS.map(group => {
              if (!groupVisible(group)) return null

              const visibleItems = group.items.filter(item => itemVisible(item))
              if (visibleItems.length === 0) return null

              return (
                <div key={group.label}>
                  <p className="mb-1.5 pl-4 text-[10px] font-bold tracking-[0.15em] text-tc-navy-text-dim uppercase">
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
            <span className="inline-flex items-center border border-tc-navy-mid px-2 py-0.5 text-[10px] font-bold tracking-widest text-tc-navy-text uppercase">
              {role.replace('_', ' ')}
            </span>
          </div>
        </aside>

        {/* Content surface */}
        <main className="flex-1 overflow-y-auto bg-white">
          <div className="min-h-full p-6">
            {children}
          </div>
        </main>
      </div>
    </div>
  )
}
