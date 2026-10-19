import { useState } from 'react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { SearchPicker } from './SearchPicker'

interface Item { id: string; name: string }
const DATA: Item[] = [
  { id: 'c1', name: 'Juan García' },
  { id: 'c2', name: 'Martín Rodríguez' },
]

function Harness({ search, onPick }: { search: (q: string) => Promise<Item[]>; onPick?: (id: string) => void }) {
  const [value, setValue] = useState('')
  return (
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <SearchPicker<Item>
        label="Cliente"
        searchKey="test"
        value={value}
        onChange={id => { setValue(id); onPick?.(id) }}
        search={search}
        getId={i => i.id}
        getLabel={i => i.name}
      />
    </QueryClientProvider>
  )
}

describe('SearchPicker', () => {
  it('busca, elige con el mouse y muestra el valor elegido con "Cambiar"', async () => {
    const search = vi.fn(async (q: string) => DATA.filter(d => d.name.toLowerCase().includes(q.toLowerCase())))
    const onPick = vi.fn()
    render(<Harness search={search} onPick={onPick} />)
    const input = screen.getByRole('combobox', { name: /cliente/i })
    await userEvent.type(input, 'mart')
    const opt = await screen.findByRole('option', { name: /martín rodríguez/i })
    await userEvent.click(opt)
    expect(onPick).toHaveBeenCalledWith('c2')
    expect(screen.getByDisplayValue('Martín Rodríguez')).toHaveAttribute('readonly')
    await userEvent.click(screen.getByRole('button', { name: /cambiar/i }))
    expect(onPick).toHaveBeenLastCalledWith('')
    expect(screen.getByRole('combobox')).toBeInTheDocument()
  })

  it('navega con el teclado y elige con Enter', async () => {
    const onPick = vi.fn()
    render(<Harness search={async () => DATA} onPick={onPick} />)
    await userEvent.click(screen.getByRole('combobox'))
    await screen.findAllByRole('option')
    await userEvent.keyboard('{ArrowDown}{Enter}')
    expect(onPick).toHaveBeenCalledWith('c2')
  })

  it('muestra "Sin resultados" cuando la búsqueda no devuelve nada', async () => {
    render(<Harness search={async () => []} />)
    await userEvent.type(screen.getByRole('combobox'), 'zzz')
    expect(await screen.findByText(/sin resultados/i)).toBeInTheDocument()
  })
})
