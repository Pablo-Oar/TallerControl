import { MemoryRouter, Route, Routes } from 'react-router'
import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { RequireAuth } from './RequireAuth'

const auth = vi.hoisted(() => ({ value: { user: null as unknown, isLoading: false } }))
vi.mock('./AuthContext', () => ({ useAuth: () => auth.value }))

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/login" element={<p>login</p>} />
        <Route path="/m" element={<p>area mecanico</p>} />
        <Route
          path="/app/*"
          element={
            <RequireAuth area="app">
              <p>panel del taller</p>
            </RequireAuth>
          }
        />
      </Routes>
    </MemoryRouter>,
  )
}

describe('RequireAuth area="app" (E2E-08)', () => {
  it('el mecánico (home=/m) que fuerza /app/* va a /m', () => {
    auth.value = { user: { role: 'mechanic', home: '/m' }, isLoading: false }
    renderAt('/app/work-orders')
    expect(screen.getByText('area mecanico')).toBeInTheDocument()
  })

  it('el dueño (home=/app) entra al panel', () => {
    auth.value = { user: { role: 'owner', home: '/app' }, isLoading: false }
    renderAt('/app/work-orders')
    expect(screen.getByText('panel del taller')).toBeInTheDocument()
  })

  it('sin sesión va a /login', () => {
    auth.value = { user: null, isLoading: false }
    renderAt('/app')
    expect(screen.getByText('login')).toBeInTheDocument()
  })
})
