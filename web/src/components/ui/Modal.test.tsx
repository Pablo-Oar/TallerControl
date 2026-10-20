import { useState } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { Modal } from './Modal'

/** T-UI-05: diálogos accesibles — Escape solo en el superior, foco atrapado y devuelto, fondo inerte y sin scroll. */

function Stack() {
  const [outer, setOuter] = useState(false)
  const [inner, setInner] = useState(false)
  return (
    <div id="root">
      <button onClick={() => setOuter(true)}>abrir externo</button>
      {outer && (
        <Modal title="Externo" onClose={() => setOuter(false)}>
          <input aria-label="campo externo" />
          <button onClick={() => setInner(true)}>abrir interno</button>
          {inner && (
            <Modal title="Interno" onClose={() => setInner(false)}>
              <input aria-label="campo interno" />
              <button>aceptar</button>
            </Modal>
          )}
        </Modal>
      )}
    </div>
  )
}

describe('Modal (T-UI-05)', () => {
  it('expone role=dialog, aria-modal y aria-labelledby apuntando al título', async () => {
    render(<Stack />)
    await userEvent.click(screen.getByText('abrir externo'))
    const dialog = screen.getByRole('dialog', { name: 'Externo' })
    expect(dialog).toHaveAttribute('aria-modal', 'true')
    expect(dialog).toHaveAccessibleName('Externo')
  })

  it('el foco entra al diálogo y vuelve al botón que lo abrió al cerrar con Escape', async () => {
    render(<Stack />)
    const opener = screen.getByText('abrir externo')
    await userEvent.click(opener)
    expect(screen.getByLabelText('campo externo')).toHaveFocus()
    await userEvent.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).toBeNull()
    expect(opener).toHaveFocus()
  })

  it('Escape cierra solo el diálogo superior de la pila', async () => {
    render(<Stack />)
    await userEvent.click(screen.getByText('abrir externo'))
    await userEvent.click(screen.getByText('abrir interno'))
    expect(screen.getAllByRole('dialog', { hidden: true })).toHaveLength(2)

    await userEvent.keyboard('{Escape}')
    expect(screen.getAllByRole('dialog')).toHaveLength(1)
    expect(screen.getByRole('dialog', { name: 'Externo' })).toBeInTheDocument()

    await userEvent.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).toBeNull()
  })

  it('el diálogo de abajo queda inerte mientras haya otro encima', async () => {
    render(<Stack />)
    await userEvent.click(screen.getByText('abrir externo'))
    await userEvent.click(screen.getByText('abrir interno'))
    const [outer] = screen.getAllByRole('dialog', { hidden: true })
    expect(outer).toHaveAttribute('inert')
    await userEvent.keyboard('{Escape}')
    expect(screen.getByRole('dialog', { name: 'Externo' })).not.toHaveAttribute('inert')
  })

  it('atrapa el foco: Tab desde el último pasa al primero y Shift+Tab desde el primero al último', async () => {
    render(<Stack />)
    await userEvent.click(screen.getByText('abrir externo'))
    const campo = screen.getByLabelText('campo externo')
    const cerrar = screen.getByRole('button', { name: 'Cerrar' })
    const abrirInterno = screen.getByText('abrir interno')

    // orden en el DOM: Cerrar, campo, abrir interno
    abrirInterno.focus()
    await userEvent.tab()
    expect(cerrar).toHaveFocus()
    await userEvent.tab({ shift: true })
    expect(abrirInterno).toHaveFocus()
    campo.focus()
    expect(campo).toHaveFocus()
  })

  it('bloquea el scroll y vuelve inerte el fondo mientras está abierto, y lo restaura al cerrar', async () => {
    render(<Stack />)
    await userEvent.click(screen.getByText('abrir externo'))
    expect(document.body.style.overflow).toBe('hidden')
    expect(document.getElementById('root')).toHaveAttribute('inert')

    await userEvent.keyboard('{Escape}')
    expect(document.body.style.overflow).not.toBe('hidden')
    expect(document.getElementById('root')).not.toHaveAttribute('inert')
  })

  it('el botón Cerrar y el clic en el fondo cierran el diálogo', async () => {
    render(<Stack />)
    await userEvent.click(screen.getByText('abrir externo'))
    await userEvent.click(screen.getByRole('button', { name: 'Cerrar' }))
    expect(screen.queryByRole('dialog')).toBeNull()

    await userEvent.click(screen.getByText('abrir externo'))
    const backdrop = screen.getByRole('dialog').parentElement!
    await userEvent.click(backdrop)
    expect(screen.queryByRole('dialog')).toBeNull()
  })
})

describe('Modal: foco perdido', () => {
  it('Escape sigue cerrando el diálogo aunque el foco haya caído al body', async () => {
    render(<Stack />)
    await userEvent.click(screen.getByText('abrir externo'))
    ;(document.activeElement as HTMLElement).blur()
    expect(document.body).toHaveFocus()
    await userEvent.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).toBeNull()
  })

  it('Tab con el foco en el body vuelve a entrar al diálogo', async () => {
    render(<Stack />)
    await userEvent.click(screen.getByText('abrir externo'))
    ;(document.activeElement as HTMLElement).blur()
    await userEvent.tab()
    expect(screen.getByRole('dialog').contains(document.activeElement)).toBe(true)
  })

  it('el tope de ancho del llamador rige en escritorio (clase literal) y en móvil es hoja a ancho completo', () => {
    render(
      <Modal title="Ancho" onClose={() => {}} className="max-w-sm">
        contenido
      </Modal>,
    )
    const panel = screen.getByRole('dialog')
    // `max-w-sm` tiene que llegar tal cual: Tailwind solo genera clases que aparecen literales en el código.
    expect(panel.className).toContain('max-w-sm')
    expect(panel.className).not.toContain('sm:max-w-sm')
    expect(panel.className).toContain('max-sm:max-w-none')
  })
})
