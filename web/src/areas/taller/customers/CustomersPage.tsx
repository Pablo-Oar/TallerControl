import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { customersApi, type CustomerDto, type CustomerListDto } from '../../../lib/api/customers'
import { Button } from '../../../components/ui/Button'
import { CustomerFormModal } from './CustomerFormModal'

const fmtARS = (n: number) =>
  new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', maximumFractionDigits: 0 }).format(n)

export function CustomersPage() {
  const queryClient = useQueryClient()
  const [q, setQ] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [creating, setCreating] = useState(false)
  const [editing, setEditing] = useState<CustomerListDto | null>(null)
  const [showDeleted, setShowDeleted] = useState(false)

  const { data, isLoading, error } = useQuery({
    queryKey: ['customers', search, page, showDeleted],
    queryFn: () => customersApi.search(search || null, page, 25, showDeleted),
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
      alert('No se pudo restaurar el cliente')
    }
  }

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-bold tracking-wide text-slate-800 uppercase">Clientes</h1>
        <Button onClick={() => setCreating(true)} size="sm">Nuevo cliente</Button>
      </div>

      <div className="flex items-center gap-2">
        <form onSubmit={handleSearch} className="flex flex-1 gap-2">
          <input
            type="search"
            value={q}
            onChange={e => setQ(e.target.value)}
            placeholder="Buscar por nombre, teléfono o documento…"
            className="min-w-0 flex-1 border border-tc-border bg-white px-3 py-1.5 text-sm text-slate-700 outline-none focus:border-tc-border-mid"
          />
          <Button type="submit" variant="secondary" size="sm">Buscar</Button>
        </form>
        <button
          onClick={() => { setShowDeleted(v => !v); setPage(1) }}
          className={`border px-3 py-1.5 text-xs font-bold tracking-wider uppercase transition-colors ${showDeleted ? 'border-red-300 bg-red-50 text-red-700' : 'border-tc-border bg-white text-slate-500 hover:bg-tc-surface'}`}
        >
          {showDeleted ? 'Ocultar eliminados' : 'Ver eliminados'}
        </button>
      </div>

      {isLoading && (
        <div className="space-y-1">
          {[0, 1, 2, 3].map(i => (
            <div key={i} className="h-14 animate-pulse bg-slate-50 border border-tc-border" />
          ))}
        </div>
      )}

      {error && (
        <p className="border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          No se pudo cargar la lista de clientes.
        </p>
      )}

      {data && (
        <>
          <div className="border border-tc-border bg-white overflow-hidden">
            {/* Header */}
            <div className="grid grid-cols-[1fr_7rem_8rem_6rem_3.5rem] border-b border-tc-border bg-tc-stub-bg">
              {['Nombre', 'Teléfono', 'Documento', 'Saldo', ''].map((h, i) => (
                <div key={i} className={`px-4 py-2 text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400 ${i === 3 ? 'text-right' : ''}`}>
                  {h}
                </div>
              ))}
            </div>

            {data.items.length === 0 ? (
              <p className="px-4 py-8 text-center text-sm text-slate-400">
                No hay clientes{search ? ' para esa búsqueda' : ''}.
              </p>
            ) : (
              <ul className="divide-y divide-tc-border">
                {data.items.map(c => (
                  <li
                    key={c.id}
                    className={`grid grid-cols-[1fr_7rem_8rem_6rem_3.5rem] items-center transition-colors ${c.isDeleted ? 'bg-red-50 opacity-60' : 'hover:bg-tc-surface'}`}
                  >
                    <div className="px-4 py-3">
                      <span className="text-sm font-medium text-slate-800">{c.name}</span>
                    </div>
                    <div className="px-4 py-3">
                      <span className="text-sm text-slate-500">{c.phone ?? '—'}</span>
                    </div>
                    <div className="px-4 py-3">
                      <span className="text-sm text-slate-500">{c.docNumber ?? '—'}</span>
                    </div>
                    <div className="px-4 py-3 text-right">
                      {c.balance > 0 ? (
                        <span className="tabular-nums text-sm font-semibold text-tc-stamp-red">
                          {fmtARS(c.balance)}
                        </span>
                      ) : (
                        <span className="text-sm text-slate-300">—</span>
                      )}
                    </div>
                    <div className="flex h-full items-center justify-center border-l border-tc-border bg-tc-stub-bg">
                      {c.isDeleted ? (
                        <button
                          onClick={() => handleRestore(c)}
                          className="px-3 py-3 text-[10px] font-bold tracking-wider text-green-700 hover:text-green-900 transition-colors uppercase"
                        >
                          Restaurar
                        </button>
                      ) : (
                        <button
                          onClick={() => setEditing(c)}
                          className="px-3 py-3 text-[10px] font-bold tracking-wider text-tc-navy-text-dim hover:text-tc-purple transition-colors uppercase"
                        >
                          Edit
                        </button>
                      )}
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </div>

          {data.total > data.pageSize && (
            <div className="flex items-center justify-between text-sm text-slate-500">
              <span className="tabular-nums">{data.total} clientes</span>
              <div className="flex items-center gap-1">
                <button
                  onClick={() => setPage(p => p - 1)}
                  disabled={page === 1}
                  className="border border-tc-border px-3 py-1 text-xs hover:bg-tc-surface disabled:opacity-40 transition-colors"
                >
                  Anterior
                </button>
                <span className="px-2 text-xs tabular-nums">Pág. {page}</span>
                <button
                  onClick={() => setPage(p => p + 1)}
                  disabled={page * data.pageSize >= data.total}
                  className="border border-tc-border px-3 py-1 text-xs hover:bg-tc-surface disabled:opacity-40 transition-colors"
                >
                  Siguiente
                </button>
              </div>
            </div>
          )}
        </>
      )}

      {creating && (
        <CustomerFormModal onSuccess={handleSuccess} onClose={() => setCreating(false)} />
      )}
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
