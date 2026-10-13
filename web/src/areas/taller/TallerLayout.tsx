import type { ReactNode } from 'react'
import { Link, useNavigate } from 'react-router'
import { useAuth } from '../../lib/auth/AuthContext'
import { Button } from '../../components/ui/Button'
import { useState } from 'react'

export function TallerLayout({ children }: { children: ReactNode }) {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const [loggingOut, setLoggingOut] = useState(false)

  async function handleLogout() {
    setLoggingOut(true)
    try {
      await logout()
      navigate('/login', { replace: true })
    } finally {
      setLoggingOut(false)
    }
  }

  return (
    <div className="flex min-h-screen flex-col bg-slate-50 text-slate-900">
      <header className="border-b border-slate-200 bg-white px-6 py-3">
        <div className="mx-auto flex max-w-7xl items-center justify-between">
          <nav className="flex items-center gap-6 text-sm font-medium">
            <Link to="/app" className="text-blue-600 hover:underline">
              Inicio
            </Link>
            {user?.role === 'Owner' && (
              <Link to="/app/settings/users" className="text-slate-600 hover:text-slate-900">
                Usuarios
              </Link>
            )}
          </nav>
          <div className="flex items-center gap-3 text-sm text-slate-500">
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
