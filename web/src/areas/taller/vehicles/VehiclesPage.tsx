import { useState } from 'react'
import { useSearchParams } from 'react-router'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { vehiclesApi, type VehicleDto, type VehicleListDto } from '../../../lib/api/vehicles'
import { Button } from '../../../components/ui/Button'
import { VehicleFormModal } from './VehicleFormModal'

export function VehiclesPage() {
  const queryClient = useQueryClient()
  const [searchParams] = useSearchParams()
  const initialQ = searchParams.get('q') ?? ''
  const [q, setQ] = useState(initialQ)
  const [search, setSearch] = useState(initialQ)
  const [page, setPage] = useState(1)
  const [creating, setCreating] = useState(false)
  const [editing, setEditing] = useState<VehicleListDto | null>(null)

  const { data, isLoading, error } = useQuery({
    queryKey: ['vehicles', search, page],
    queryFn: () => vehiclesApi.search(search || null, page),
  })

  const { data: editVehicle } = useQuery({
    queryKey: ['vehicles', editing?.id],
    queryFn: () => vehiclesApi.get(editing!.id),
    enabled: !!editing,
  })

  function handleSearch(e: React.FormEvent) {
    e.preventDefault()
    setSearch(q)
    setPage(1)
  }

  function handleSuccess(dto: VehicleDto) {
    setCreating(false)
    setEditing(null)
    queryClient.invalidateQueries({ queryKey: ['vehicles'] })
    queryClient.setQueryData(['vehicles', dto.id], dto)
  }

  return (
    <div>
      <div className="mb-6 flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Vehículos</h1>
        <Button onClick={() => setCreating(true)}>Nuevo vehículo</Button>
      </div>

      <form onSubmit={handleSearch} className="mb-4 flex gap-2">
        <input
          type="search"
          value={q}
          onChange={e => setQ(e.target.value)}
          placeholder="Buscar por patente, marca o modelo…"
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
        <p className="text-sm text-red-600">No se pudo cargar la lista de vehículos.</p>
      )}

      {data && (
        <>
          <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
            <table className="w-full text-sm">
              <thead className="bg-slate-50 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">
                <tr>
                  <th className="px-4 py-3">Patente</th>
                  <th className="px-4 py-3">Marca / Modelo</th>
                  <th className="px-4 py-3">Año</th>
                  <th className="px-4 py-3">Cliente</th>
                  <th className="px-4 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {data.items.length === 0 && (
                  <tr>
                    <td colSpan={5} className="px-4 py-8 text-center text-slate-400">
                      No hay vehículos{search ? ' para esa búsqueda' : ''}.
                    </td>
                  </tr>
                )}
                {data.items.map((v) => (
                  <tr key={v.id} className="hover:bg-slate-50">
                    <td className="px-4 py-3 font-mono font-medium tracking-wide">{v.plate}</td>
                    <td className="px-4 py-3">
                      {[v.brand, v.model].filter(Boolean).join(' ') || <span className="text-slate-400">—</span>}
                    </td>
                    <td className="px-4 py-3 text-slate-500">{v.year ?? '—'}</td>
                    <td className="px-4 py-3 text-slate-500">{v.customerName}</td>
                    <td className="px-4 py-3 text-right">
                      <button
                        onClick={() => setEditing(v)}
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
              <span>{data.total} vehículos</span>
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
        <VehicleFormModal
          onSuccess={handleSuccess}
          onClose={() => setCreating(false)}
        />
      )}

      {editing && editVehicle && (
        <VehicleFormModal
          vehicle={editVehicle}
          onSuccess={handleSuccess}
          onClose={() => setEditing(null)}
        />
      )}
    </div>
  )
}
