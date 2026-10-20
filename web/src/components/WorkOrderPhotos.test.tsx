import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { WorkOrderPhotos } from './WorkOrderPhotos'

const api = vi.hoisted(() => ({
  listForWorkOrder: vi.fn(),
  upload: vi.fn(),
  delete: vi.fn(),
}))
vi.mock('../lib/api/attachments', () => ({ attachmentsApi: api }))

const photo = { id: 'a1', status: 'ready', contentType: 'image/jpeg', downloadUrl: 'https://r2.test/a1.jpg' }

function renderIt(canDelete = false) {
  return render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <WorkOrderPhotos workOrderId="wo1" canDelete={canDelete} />
    </QueryClientProvider>,
  )
}

describe('WorkOrderPhotos', () => {
  beforeEach(() => {
    api.listForWorkOrder.mockReset().mockResolvedValue([])
    api.upload.mockReset().mockResolvedValue({})
    api.delete.mockReset().mockResolvedValue(undefined)
  })

  it('sin fotos muestra el estado vacío y el botón para agregar', async () => {
    renderIt()
    expect(await screen.findByText(/sin fotos todavía/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /sacar foto/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /desde la galería/i })).toBeInTheDocument()
  })

  it('lista solo las fotos listas y el dueño puede quitarlas', async () => {
    api.listForWorkOrder.mockResolvedValue([photo, { ...photo, id: 'a2', status: 'pending_upload' }])
    renderIt(true)
    expect(await screen.findAllByRole('img')).toHaveLength(1)
    await userEvent.click(screen.getByRole('button', { name: /quitar foto 1/i }))
    await waitFor(() => expect(api.delete).toHaveBeenCalledWith('a1'))
  })

  it('el mecánico no ve el botón de quitar', async () => {
    api.listForWorkOrder.mockResolvedValue([photo])
    renderIt(false)
    await screen.findByRole('img')
    expect(screen.queryByRole('button', { name: /quitar foto/i })).toBeNull()
  })

  it('sube la foto elegida y rechaza formatos no admitidos', async () => {
    renderIt()
    await screen.findByText(/sin fotos todavía/i)
    const input = screen.getByLabelText('Sacar foto')
    expect(input).toHaveAttribute('capture', 'environment') // abre la cámara trasera en el celular

    await userEvent.upload(input, new File(['x'], 'doc.gif', { type: 'image/gif' }), { applyAccept: false })
    expect(await screen.findByRole('alert')).toHaveTextContent(/jpg, png o webp/i)
    expect(api.upload).not.toHaveBeenCalled()

    await userEvent.upload(input, new File(['x'], 'foto.jpg', { type: 'image/jpeg' }))
    await waitFor(() => expect(api.upload).toHaveBeenCalledTimes(1))
    expect(api.upload.mock.calls[0].slice(0, 2)).toEqual(['work_order', 'wo1'])
  })
})
