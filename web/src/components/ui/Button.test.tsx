import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { Button } from './Button'

describe('Button', () => {
  it('loading deshabilita el botón y lo marca como ocupado, sin perder el texto', () => {
    render(<Button loading>Guardar</Button>)
    const btn = screen.getByRole('button', { name: 'Guardar' })
    expect(btn).toBeDisabled()
    expect(btn).toHaveAttribute('aria-busy', 'true')
  })

  it('success muestra el check y mantiene el nombre accesible', () => {
    const { container } = render(<Button success>Guardado</Button>)
    expect(screen.getByRole('button', { name: 'Guardado' })).toBeEnabled()
    expect(container.querySelector('.tc-check')).not.toBeNull()
  })

  it('el brillo solo existe en variantes rellenas y es decorativo', () => {
    const { container, rerender } = render(<Button>Ok</Button>)
    expect(container.querySelector('span[aria-hidden]')).not.toBeNull()
    rerender(<Button variant="secondary">Ok</Button>)
    expect(container.querySelector('span[aria-hidden]')).toBeNull()
  })
})
