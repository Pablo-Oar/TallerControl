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
const confirm = vi.hoisted(() => vi.fn())
vi.mock('./ui/dialogs', () => ({ confirmDialog: confirm }))

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
    // jsdom no implementa las URL de vista previa local.
    URL.createObjectURL = vi.fn(() => 'blob:preview')
    URL.revokeObjectURL = vi.fn()
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
    confirm.mockResolvedValue(true)
    await userEvent.click(screen.getByRole('button', { name: /quitar foto 1/i }))
    await waitFor(() => expect(api.delete).toHaveBeenCalledWith('a1'))
  })

  it('pide confirmación y no quita la foto si se cancela', async () => {
    api.listForWorkOrder.mockResolvedValue([photo])
    renderIt(true)
    await screen.findByRole('img')
    confirm.mockResolvedValue(false)
    await userEvent.click(screen.getByRole('button', { name: /quitar foto 1/i }))
    await waitFor(() => expect(confirm).toHaveBeenCalled())
    expect(api.delete).not.toHaveBeenCalled()
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

  it('muestra la foto elegida al instante con «Subiendo…» y la reemplaza por la del servidor', async () => {
    let finish!: () => void
    api.upload.mockReturnValue(new Promise<void>(resolve => { finish = resolve }))
    renderIt()
    await screen.findByText(/sin fotos todavía/i)

    await userEvent.upload(screen.getByLabelText('Sacar foto'), new File(['x'], 'foto.jpg', { type: 'image/jpeg' }))
    // Antes de que el servidor responda ya está la vista previa local con el aviso.
    expect(await screen.findByText(/subiendo…/i)).toBeInTheDocument()
    expect(screen.getByAltText(/foto que se está subiendo/i)).toHaveAttribute('src', 'blob:preview')

    api.listForWorkOrder.mockResolvedValue([photo])
    finish()
    await waitFor(() => expect(screen.queryByText(/subiendo…/i)).toBeNull())
    expect(await screen.findByAltText(/foto 1 de la orden/i)).toBeInTheDocument()
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:preview')
  })

  it('si la subida falla deja la foto con «No se subió» y permite reintentar o descartar', async () => {
    api.upload.mockRejectedValueOnce(new Error('sin red'))
    renderIt()
    await screen.findByText(/sin fotos todavía/i)

    await userEvent.upload(screen.getByLabelText('Sacar foto'), new File(['x'], 'foto.jpg', { type: 'image/jpeg' }))
    expect(await screen.findByText(/no se subió/i)).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: /reintentar/i }))
    await waitFor(() => expect(api.upload).toHaveBeenCalledTimes(2))
    await waitFor(() => expect(screen.queryByText(/no se subió/i)).toBeNull())
  })

  it('en escritorio (puntero fino) ofrece un solo botón "Adjuntar imágenes" y sube varias', async () => {
    vi.stubGlobal('matchMedia', (q: string) => ({ matches: false, media: q, addEventListener() {}, removeEventListener() {} }))
    try {
      renderIt()
      await screen.findByText(/sin fotos todavía/i)
      expect(screen.getByRole('button', { name: /adjuntar imágenes/i })).toBeInTheDocument()
      expect(screen.queryByRole('button', { name: /sacar foto/i })).toBeNull()

      const input = screen.getByLabelText('Adjuntar imágenes')
      expect(input).toHaveAttribute('multiple')
      expect(input).not.toHaveAttribute('capture')
      await userEvent.upload(input, [
        new File(['a'], 'a.jpg', { type: 'image/jpeg' }),
        new File(['b'], 'b.png', { type: 'image/png' }),
      ])
      await waitFor(() => expect(api.upload).toHaveBeenCalledTimes(2))
    } finally {
      vi.unstubAllGlobals()
    }
  })
})
