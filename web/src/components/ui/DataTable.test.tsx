import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { describe, expect, it, vi } from 'vitest'
import { DataTable, type DataTableColumn } from './DataTable'

interface Row {
  id: string
  name: string
  total: number
}

const rows: Row[] = [
  { id: 'a', name: 'Bravo', total: 300 },
  { id: 'b', name: 'Alfa', total: 100 },
  { id: 'c', name: 'Charly', total: 200 },
]

const columns: DataTableColumn<Row>[] = [
  { key: 'name', header: 'Nombre', cell: r => r.name, sortable: true, sortValue: r => r.name },
  { key: 'total', header: 'Total', align: 'right', cell: r => `$${r.total}`, sortable: true, sortValue: r => r.total },
  { key: 'plain', header: 'Fijo', cell: () => 'x' },
]

function renderTable(extra: Partial<React.ComponentProps<typeof DataTable<Row>>> = {}) {
  return render(
    <MemoryRouter>
      <DataTable<Row>
        label="Filas"
        columns={columns}
        rows={rows}
        getRowId={r => r.id}
        rowHref={r => `/x/${r.id}`}
        rowLabel={r => `Ver ${r.name}`}
        renderCard={r => r.name}
        {...extra}
      />
    </MemoryRouter>,
  )
}

const bodyNames = () =>
  within(screen.getAllByRole('table')[0])
    .getAllByRole('row')
    .slice(1)
    .map(r => within(r).getAllByRole('cell')[0].textContent)

describe('DataTable', () => {
  it('modo navegador: ordena asc → desc → sin orden y marca aria-sort', async () => {
    renderTable()
    const header = screen.getAllByRole('columnheader', { name: /nombre/i })[0]
    expect(header).toHaveAttribute('aria-sort', 'none')
    expect(bodyNames()).toEqual(['Bravo', 'Alfa', 'Charly'])

    await userEvent.click(screen.getByRole('button', { name: 'Ordenar por Nombre' }))
    expect(header).toHaveAttribute('aria-sort', 'ascending')
    expect(bodyNames()).toEqual(['Alfa', 'Bravo', 'Charly'])

    await userEvent.click(screen.getByRole('button', { name: 'Ordenar por Nombre' }))
    expect(header).toHaveAttribute('aria-sort', 'descending')
    expect(bodyNames()).toEqual(['Charly', 'Bravo', 'Alfa'])

    await userEvent.click(screen.getByRole('button', { name: 'Ordenar por Nombre' }))
    expect(header).toHaveAttribute('aria-sort', 'none')
    expect(bodyNames()).toEqual(['Bravo', 'Alfa', 'Charly'])
  })

  it('modo servidor: no reordena las filas, avisa con onSortChange', async () => {
    const onSortChange = vi.fn()
    renderTable({ onSortChange, sort: null })
    await userEvent.click(screen.getByRole('button', { name: 'Ordenar por Total' }))
    expect(onSortChange).toHaveBeenCalledWith({ key: 'total', dir: 'asc' })
    expect(bodyNames()).toEqual(['Bravo', 'Alfa', 'Charly']) // el servidor es quien manda las filas ordenadas
  })

  it('las columnas sin sortable no tienen botón de orden', () => {
    renderTable()
    expect(screen.queryByRole('button', { name: /ordenar por fijo/i })).toBeNull()
  })

  it('cada fila tiene un enlace de entrada con nombre accesible', () => {
    renderTable()
    expect(screen.getAllByRole('link', { name: 'Ver Alfa' }).length).toBeGreaterThan(0)
  })

  it('muestra el estado vacío y el de error con reintento', async () => {
    const { unmount } = renderTable({ rows: [], emptyMessage: 'No hay filas.' })
    expect(screen.getByText('No hay filas.')).toBeInTheDocument()
    unmount()

    const onRetry = vi.fn()
    renderTable({ error: true, onRetry })
    expect(screen.getByRole('alert')).toHaveTextContent(/no se pudo cargar/i)
    await userEvent.click(screen.getByRole('button', { name: /reintentar/i }))
    expect(onRetry).toHaveBeenCalled()
  })
})
