import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { customersApi, type CustomerDto, type CustomerListDto } from '../../../lib/api/customers'
import { Button } from '../../../components/ui/Button'
import { CustomerFormModal } from './CustomerFormModal'

export function CustomersPage() {
  const queryClient = useQueryClient()
  const [q, setQ] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [creating, setCreating] = useState(false)
  const [editing, setEditing] = useState<CustomerListDto | null>(null)

  const { data, isLoading, error } = useQuery({
    queryKey: ['customers', search, page],
    queryFn: () => customersApi.search(search || null, page),
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

  return (
    <div>
      <div className="mb-6 flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Clientes</h1>
        <Button onClick={() => setCreating(true)}>Nuevo cliente</Button>
      </div>

      <form onSubmit={handleSearch} className="mb-4 flex gap-2">
        <input
          type="search"
          value={q}
          onChange={e => setQ(e.target.value)}
          placeholder="Buscar por nombre, teléfono o documento…"
          className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-blue-500"
        />
        <Button type="submit" variant="secondary">Buscar</Button>
      </form>

      {isLoading && (
        <div className="flex justify-center py-12">
          <div className="h-6 w-6 animate-spin rounded-full border-2 border-blue-600 border-t-transparent" />
        </div>
      )}

      {error && (
        <p className="text-sm text-red-600">No se pudo cargar la lista de clientes.</p>
      )}

      {data && (
        <>
          <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
            <table className="w-full text-sm">
              <thead className="bg-slate-50 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">
                <tr>
                  <th className="px-4 py-3">Nombre</th>
                  <th className="px-4 py-3">Teléfono</th>
                  <th className="px-4 py-3">Documento</th>
                  <th className="px-4 py-3 text-right">Saldo</th>
                  <th className="px-4 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {data.items.length === 0 && (
                  <tr>
                    <td colSpan={5} className="px-4 py-8 text-center text-slate-400">
                      No hay clientes{search ? ' para esa búsqueda' : ''}.
                    </td>
                  </tr>
                )}
                {data.items.map((c) => (
                  <tr key={c.id} className="hover:bg-slate-50">
                    <td className="px-4 py-3 font-medium">{c.name}</td>
                    <td className="px-4 py-3 text-slate-500">{c.phone ?? '—'}</td>
                    <td className="px-4 py-3 text-slate-500">{c.docNumber ?? '—'}</td>
                    <td className="px-4 py-3 text-right tabular-nums">
                      {c.balance > 0
                        ? <span className="font-medium text-red-600">${c.balance.toFixed(2)}</span>
                        : <span className="text-slate-400">—</span>
                      }
                    </td>
                    <td className="px-4 py-3 text-right">
                      <button
                        onClick={() => setEditing(c)}
                        className="text-xs text-blue-600 hover:underline"
                      >
                        Editar
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {data.total > data.pageSize && (
            <div className="mt-4 flex items-center justify-between text-sm text-slate-500">
              <span>{data.total} clientes</span>
              <div className="flex items-center gap-2">
                <button
                  onClick={() => setPage(p => p - 1)}
                  disabled={page === 1}
                  className="rounded px-3 py-1 hover:bg-slate-100 disabled:opacity-40"
                >
                  Anterior
                </button>
                <span>Pág. {page}</span>
                <button
                  onClick={() => setPage(p => p + 1)}
                  disabled={page * data.pageSize >= data.total}
                  className="rounded px-3 py-1 hover:bg-slate-100 disabled:opacity-40"
                >
                  Siguiente
                </button>
              </div>
            </div>
          )}
        </>
      )}

      {creating && (
        <CustomerFormModal
          onSuccess={handleSuccess}
          onClose={() => setCreating(false)}
        />
      )}

      {editing && editCustomer && (
        <CustomerFormModal
          customer={editCustomer}
          onSuccess={handleSuccess}
          onClose={() => setEditing(null)}
        />
      )}
    </div>
  )
}
