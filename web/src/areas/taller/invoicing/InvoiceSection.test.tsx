import { MemoryRouter } from 'react-router'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { InvoiceSection } from './InvoiceSection'

const auth = vi.hoisted(() => ({ value: { user: null as unknown } }))
vi.mock('../../../lib/auth/AuthContext', () => ({ useAuth: () => auth.value }))

const api = vi.hoisted(() => ({
  searchInvoices: vi.fn(),
  listCertificates: vi.fn(),
  listPointsOfSale: vi.fn(),
  emitInvoiceFromWorkOrder: vi.fn(),
  emitInvoiceFromCounterSale: vi.fn(),
}))
vi.mock('../../../lib/api/invoicing', () => api)

function renderSection(props: Partial<React.ComponentProps<typeof InvoiceSection>> = {}) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <InvoiceSection sourceType="work_order" sourceId="o1" eligible {...props} />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

// T-UI-10 (F-01): el botón «Facturar» solo aparece con permiso, ARCA configurado y el documento facturable.
describe('InvoiceSection', () => {
  beforeEach(() => {
    api.searchInvoices.mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 50 })
    api.listCertificates.mockResolvedValue([{ id: 'c1', isActive: true }])
    api.listPointsOfSale.mockResolvedValue([{ id: 'p1', number: 1, name: 'Taller', isActive: true }])
  })

  it('ofrece Facturar a quien puede emitir cuando ARCA está configurado', async () => {
    auth.value = { user: { permissions: ['invoicing.read', 'invoicing.emit'] } }
    renderSection()
    expect(await screen.findByRole('button', { name: 'Facturar' })).toBeInTheDocument()
  })

  it('no ofrece Facturar sin el permiso invoicing.emit', async () => {
    auth.value = { user: { permissions: ['invoicing.read'] } }
    renderSection()
    expect(await screen.findByText('Todavía no se emitió ninguna factura.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Facturar' })).not.toBeInTheDocument()
  })

  it('invita a configurar ARCA si no hay certificado activo', async () => {
    auth.value = { user: { permissions: ['invoicing.read', 'invoicing.emit'] } }
    api.listCertificates.mockResolvedValue([])
    renderSection()
    expect(await screen.findByRole('link', { name: 'Configurar ARCA' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Facturar' })).not.toBeInTheDocument()
  })

  it('explica por qué no se puede facturar un documento no elegible', async () => {
    auth.value = { user: { permissions: ['invoicing.read', 'invoicing.emit'] } }
    renderSection({ eligible: false, ineligibleReason: 'Una orden cancelada no se factura.' })
    expect(await screen.findByText('Una orden cancelada no se factura.')).toBeInTheDocument()
  })

  it('no muestra nada a quien no puede leer facturación', () => {
    auth.value = { user: { permissions: ['workorders.read'] } }
    const { container } = renderSection()
    expect(container).toBeEmptyDOMElement()
  })
})
