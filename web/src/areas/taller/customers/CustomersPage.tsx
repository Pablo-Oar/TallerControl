import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { customersApi, type CustomerDto, type CustomerListDto } from '../../../lib/api/customers'
import { Button } from '../../../components/ui/Button'
import { DataTable, RowAction, type DataTableColumn, type SortState } from '../../../components/ui/DataTable'
import { Pagination } from '../../../components/ui/Pagination'
import { CustomerFormModal } from './CustomerFormModal'
import { notify } from '../../../components/ui/dialogs'
import { useCan } from '../../../lib/auth/permissions'

const fmtARS = (n: number) =>
  new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', maximumFractionDigits: 0 }).format(n)

export function CustomersPage() {
  const can = useCan()
  const queryClient = useQueryClient()
  const [q, setQ] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [creating, setCreating] = useState(false)
  const [editing, setEditing] = useState<CustomerListDto | null>(null)
  const [showDeleted, setShowDeleted] = useState(false)
  const [sort, setSort] = useState<SortState | null>(null)

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['customers', search, page, showDeleted, sort],
    queryFn: () => customersApi.search(search || null, page, 25, showDeleted, sort),
  })

  const { data: editCustomer } = useQuery({
    queryKey: ['customers', editing?.id],
    queryFn: () => customersApi.get(editing!.id),
    enabled: !!editing,
  })

  function handleSearch(e: React.FormEvent) {
    e.preventDefault()
    setSearch(q)
    setPage(1)
  }

  function handleSuccess(dto: CustomerDto) {
    setCreating(false)
    setEditing(null)
    queryClient.invalidateQueries({ queryKey: ['customers'] })
    queryClient.setQueryData(['customers', dto.id], dto)
  }

  async function handleRestore(c: CustomerListDto) {
    try {
      await customersApi.restore(c.id)
      queryClient.invalidateQueries({ queryKey: ['customers'] })
    } catch {
      notify('No se pudo restaurar el cliente')
    }
  }

  const columns: DataTableColumn<CustomerListDto>[] = [
    { key: 'name', header: 'Nombre', sortable: true, cell: c => <span className="font-medium text-tc-text">{c.name}</span> },
    { key: 'phone', header: 'Teléfono', widthClass: 'w-44', sortable: true, cell: c => <span className="text-tc-text-secondary">{c.phone ?? '—'}</span> },
    { key: 'doc', header: 'Documento', widthClass: 'w-40', sortable: true, cell: c => <span className="text-tc-text-secondary">{c.docNumber ?? '—'}</span> },
    {
      key: 'balance',
      header: 'Saldo',
      align: 'right',
      widthClass: 'w-32',
      sortable: true,
      cell: c =>
        c.balance > 0 ? (
          <span className="tabular-nums font-semibold text-tc-stamp-red">{fmtARS(c.balance)}</span>
        ) : (
          <span className="text-tc-text-muted">—</span>
        ),
    },
  ]

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-bold tracking-wide text-tc-text uppercase">Clientes</h1>
        <Button onClick={() => setCreating(true)} size="sm">Nuevo cliente</Button>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <form onSubmit={handleSearch} className="flex min-w-[14rem] flex-1 gap-2">
          <input
            type="search"
            value={q}
            onChange={e => setQ(e.target.value)}
            placeholder="Buscar por nombre, teléfono o documento…"
            className="min-w-0 flex-1 border border-tc-border bg-white px-3 py-1.5 text-sm text-tc-text-secondary outline-none focus:border-tc-border-mid"
          />
          <Button type="submit" variant="secondary" size="sm">Buscar</Button>
        </form>
        <button
          onClick={() => { setShowDeleted(v => !v); setPage(1) }}
          className={`shrink-0 whitespace-nowrap border px-3 py-2 text-xs font-bold tracking-wider uppercase transition-colors ${showDeleted ? 'border-red-300 bg-red-50 text-red-700' : 'border-tc-border bg-white text-tc-text-secondary hover:bg-tc-surface'}`}
        >
          {showDeleted ? 'Ocultar eliminados' : 'Ver eliminados'}
        </button>
      </div>

      <DataTable<CustomerListDto>
        label="Clientes"
        columns={columns}
        sort={sort}
        onSortChange={s => { setSort(s); setPage(1) }}
        rows={data?.items ?? []}
        getRowId={c => c.id}
        rowHref={c => (c.isDeleted ? null : `/app/customers/${c.id}`)}
        rowClassName={c => (c.isDeleted ? 'opacity-60' : undefined)}
        actions={c =>
          c.isDeleted ? (
            can('customers.delete') ? <RowAction tone="success" onClick={() => handleRestore(c)}>Restaurar</RowAction> : null
          ) : (
            <RowAction onClick={() => setEditing(c)}>Editar</RowAction>
          )
        }
        renderCard={c => (
          <>
            <p className="truncate text-sm font-medium text-tc-text">{c.name}</p>
            <p className="mt-0.5 text-xs text-tc-text-muted">{[c.phone, c.docNumber].filter(Boolean).join(' · ') || '—'}</p>
            {c.balance > 0 && <p className="mt-1 text-sm font-semibold tabular-nums text-tc-stamp-red">Saldo {fmtARS(c.balance)}</p>}
          </>
        )}
        loading={isLoading}
        error={!!error}
        onRetry={() => refetch()}
        emptyMessage={`No hay clientes${search ? ' para esa búsqueda' : ''}.`}
      />

      {data && <Pagination page={page} pageSize={data.pageSize} total={data.total} noun="clientes" onPage={setPage} />}

      {creating && <CustomerFormModal onSuccess={handleSuccess} onClose={() => setCreating(false)} />}
      {editing && editCustomer && (
        <CustomerFormModal
          customer={editCustomer}
          onSuccess={handleSuccess}
          onClose={() => setEditing(null)}
          onDelete={() => {
            setEditing(null)
            queryClient.invalidateQueries({ queryKey: ['customers'] })
          }}
        />
      )}
    </div>
  )
}
