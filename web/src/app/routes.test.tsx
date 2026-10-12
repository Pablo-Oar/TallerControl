import { render, screen } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { describe, expect, it } from 'vitest'
import { appRoutes } from './routes.tsx'

function renderEn(ruta: string) {
  const router = createMemoryRouter(appRoutes, { initialEntries: [ruta] })
  render(<RouterProvider router={router} />)
}

describe('shell de la SPA', () => {
  it('la raíz redirige al área del taller', async () => {
    renderEn('/')
    expect(await screen.findByRole('heading', { name: 'Inicio' })).toBeInTheDocument()
  })

  it('/m muestra el área del mecánico', async () => {
    renderEn('/m')
    expect(await screen.findByRole('heading', { name: 'Mis trabajos' })).toBeInTheDocument()
  })

  it('/login muestra el área pública', async () => {
    renderEn('/login')
    expect(await screen.findByRole('heading', { name: 'Ingresar' })).toBeInTheDocument()
  })
})