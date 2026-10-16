import type { ReactNode } from 'react'
import { NavLink, useNavigate } from 'react-router'
import { useAuth } from '../../lib/auth/AuthContext'
import { Button } from '../../components/ui/Button'
import { PlateSearchBar } from '../../components/PlateSearchBar'
import { useState } from 'react'

const navCls = ({ isActive }: { isActive: boolean }) =>
  isActive ? 'text-blue-600 hover:underline' : 'text-slate-600 hover:text-slate-900'

export function TallerLayout({ children }: { children: ReactNode }) {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const [loggingOut, setLoggingOut] = useState(false)

  async function handleLogout() {
    setLoggingOut(true)
    try {
      await logout()
    } finally {
      setLoggingOut(false)
      navigate('/login', { replace: true })
    }
  }

  return (
    <div className="flex min-h-screen flex-col bg-slate-50 text-slate-900">
      <header className="border-b border-slate-200 bg-white px-6 py-3">
        <div className="mx-auto flex max-w-7xl items-center justify-between">
          <nav className="flex items-center gap-6 text-sm font-medium">
            <NavLink to="/app" end className={navCls}>
              Inicio
            </NavLink>
            <NavLink to="/app/customers" className={navCls}>
              Clientes
            </NavLink>
            <NavLink to="/app/vehicles" className={navCls}>
              Vehículos
            </NavLink>
            <NavLink to="/app/catalog" className={navCls}>
              Catálogo
            </NavLink>
            <NavLink to="/app/quotes" className={navCls}>
              Presupuestos
            </NavLink>
            <NavLink to="/app/work-orders" className={navCls}>
              Órdenes
            </NavLink>
            {(user?.role === 'Owner' || user?.role === 'AdminStaff') && (
              <NavLink to="/app/reminders" className={navCls}>
                Recordatorios
              </NavLink>
            )}
            {(user?.role === 'Owner' || user?.role === 'AdminStaff') && (
              <NavLink to="/app/qr" className={navCls}>
                Tarjetas QR
              </NavLink>
            )}
            {(user?.role === 'Owner' || user?.role === 'AdminStaff') && (
              <NavLink to="/app/cash" className={navCls}>
                Caja
              </NavLink>
            )}
            {(user?.role === 'Owner' || user?.role === 'AdminStaff') && (
              <NavLink to="/app/suppliers" className={navCls}>
                Proveedores
              </NavLink>
            )}
            {(user?.role === 'Owner' || user?.role === 'AdminStaff') && (
              <NavLink to="/app/purchases" className={navCls}>
                Compras
              </NavLink>
            )}
            {(user?.role === 'Owner' || user?.role === 'AdminStaff') && (
              <NavLink to="/app/counter-sales" className={navCls}>
                Mostrador
              </NavLink>
            )}
            {(user?.role === 'Owner' || user?.role === 'AdminStaff' || user?.role === 'Accountant') && (
              <NavLink to="/app/invoicing" className={navCls}>
                Facturación
              </NavLink>
            )}
            {user?.role === 'Owner' && (
              <NavLink to="/app/settings/general" className={navCls}>
                General
              </NavLink>
            )}
            {user?.role === 'Owner' && (
              <NavLink to="/app/settings/users" className={navCls}>
                Usuarios
              </NavLink>
            )}
          </nav>
          <div className="flex items-center gap-3 text-sm text-slate-500">
            <PlateSearchBar />
            <span>{user?.user.displayName}</span>
            <Button variant="secondary" loading={loggingOut} onClick={handleLogout}>
              Salir
            </Button>
          </div>
        </div>
      </header>
      <main className="mx-auto w-full max-w-7xl flex-1 p-6">{children}</main>
    </div>
  )
}
