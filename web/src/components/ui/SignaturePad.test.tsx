import { createRef } from 'react'
import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { SignaturePad, type SignaturePadHandle } from './SignaturePad'

describe('SignaturePad', () => {
  it('empieza vacío, con "Borrar" deshabilitado y toBlob() nulo', async () => {
    const ref = createRef<SignaturePadHandle>()
    render(<SignaturePad ref={ref} />)
    expect(screen.getByRole('img', { name: 'Área de firma vacía' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Borrar firma' })).toBeDisabled()
    expect(await ref.current!.toBlob()).toBeNull()
  })

  it('un trazo marca la firma, avisa por onChange y "Borrar" la limpia', () => {
    const onChange = vi.fn()
    render(<SignaturePad onChange={onChange} />)
    const canvas = screen.getByRole('img')
    fireEvent.pointerDown(canvas, { pointerId: 1, clientX: 10, clientY: 10 })
    fireEvent.pointerMove(canvas, { pointerId: 1, clientX: 40, clientY: 30 })
    fireEvent.pointerUp(canvas, { pointerId: 1 })
    expect(onChange).toHaveBeenLastCalledWith(true)
    expect(screen.getByRole('img', { name: 'Firma dibujada' })).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Borrar firma' }))
    expect(onChange).toHaveBeenLastCalledWith(false)
  })

  it('deshabilitado no registra trazos', () => {
    const onChange = vi.fn()
    render(<SignaturePad disabled onChange={onChange} />)
    fireEvent.pointerDown(screen.getByRole('img'), { pointerId: 1, clientX: 5, clientY: 5 })
    expect(onChange).not.toHaveBeenCalled()
  })
})
