import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useForm } from 'react-hook-form'
import { describe, expect, it } from 'vitest'
import { FormField, FormSelect, FormTextarea } from './FormField'

function Harness({ error }: { error?: string }) {
  const { register, reset } = useForm({ defaultValues: { name: 'Juan' } })
  return (
    <form>
      <FormField label="Nombre" requiredMark hint="Como figura en el DNI" error={error} {...register('name')} />
      <FormTextarea label="Notas" {...register('notes' as 'name')} />
      <FormSelect label="IVA" defaultValue="a">
        <option value="a">A</option>
      </FormSelect>
      <button type="button" onClick={() => reset({ name: 'Ana' })}>reset</button>
    </form>
  )
}

describe('FormField', () => {
  it('asocia la etiqueta al control (a11y) y refleja el valor inicial de RHF', () => {
    render(<Harness />)
    expect(screen.getByLabelText(/nombre/i)).toHaveValue('Juan')
    expect(screen.getByLabelText('Notas')).toBeInTheDocument()
    expect(screen.getByLabelText('IVA')).toBeInTheDocument()
  })

  it('refleja reset() sin depender de estado interno', async () => {
    render(<Harness />)
    await userEvent.click(screen.getByText('reset'))
    expect(screen.getByLabelText(/nombre/i)).toHaveValue('Ana')
  })

  it('muestra el hint y, si hay error, lo anuncia y marca aria-invalid', () => {
    const { rerender } = render(<Harness />)
    const input = screen.getByLabelText(/nombre/i)
    expect(input).toHaveAccessibleDescription('Como figura en el DNI')
    expect(input).not.toHaveAttribute('aria-invalid')
    rerender(<Harness error="Requerido" />)
    expect(screen.getByRole('alert')).toHaveTextContent('Requerido')
    expect(screen.getByLabelText(/nombre/i)).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByLabelText(/nombre/i)).toHaveAccessibleDescription('Requerido')
  })
})
