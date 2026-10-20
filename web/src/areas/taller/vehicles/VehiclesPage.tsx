import { useState } from 'react'
import { useSearchParams } from 'react-router'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { vehiclesApi, type VehicleDto, type VehicleListDto } from '../../../lib/api/vehicles'
import { Button } from '../../../components/ui/Button'
import { Link } from 'react-router'
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
  const [showDeleted, setShowDeleted] = useState(false)

  const { data, isLoading, error } = useQuery({
    queryKey: ['vehicles', search, page, showDeleted],
    queryFn: () => vehiclesApi.search(search || null, page, 25, showDeleted),
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

  async function handleRestore(v: VehicleListDto) {
    try {
      await vehiclesApi.restore(v.id)
      queryClient.invalidateQueries({ queryKey: ['vehicles'] })
    } catch {
      alert('No se pudo restaurar el vehículo')
    }
  }

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-bold tracking-wide text-tc-text uppercase">Vehículos</h1>
        <Button onClick={() => setCreating(true)} size="sm">Nuevo vehículo</Button>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <form onSubmit={handleSearch} className="flex min-w-[14rem] flex-1 gap-2">
          <input
            type="search"
            value={q}
            onChange={e => setQ(e.target.value)}
            placeholder="Buscar por patente, marca o modelo…"
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

      {isLoading && (
        <div className="space-y-1">
          {[0, 1, 2, 3].map(i => (
            <div key={i} className="h-14 animate-pulse bg-slate-50 border border-tc-border" />
          ))}
        </div>
      )}

      {error && (
        <p className="border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          No se pudo cargar la lista de vehículos.
        </p>
      )}

      {data && (
        <>
          <div className="border border-tc-border bg-white overflow-hidden">
            {/* Header */}
            <div className="grid grid-cols-[7rem_1fr_3.5rem] md:grid-cols-[7rem_1fr_3.5rem_1fr_3.5rem] border-b border-tc-border bg-tc-stub-bg">
              {[
                ['Patente', '', false],
                ['Marca / Modelo', '', false],
                ['Año', 'text-right', true],
                ['Cliente', '', true],
                ['', '', false],
              ].map(([h, cls, hideOnMobile], i) => (
                <div key={i} className={`px-4 py-2 text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted ${cls} ${hideOnMobile ? 'hidden md:block' : ''}`}>
                  {h}
                </div>
              ))}
            </div>

            {data.items.length === 0 ? (
              <p className="px-4 py-8 text-center text-sm text-tc-text-muted">
                No hay vehículos{search ? ' para esa búsqueda' : ''}.
              </p>
            ) : (
              <ul className="divide-y divide-tc-border">
                {data.items.map(v => (
                  <li
                    key={v.id}
                    className={`grid grid-cols-[7rem_1fr_3.5rem] md:grid-cols-[7rem_1fr_3.5rem_1fr_3.5rem] items-center transition-colors ${v.isDeleted ? 'bg-red-50 opacity-60' : 'hover:bg-tc-surface'}`}
                  >
                    <div className="px-4 py-3">
                      {v.isDeleted ? (
                        <span className="font-mono text-sm font-bold tracking-widest text-tc-text">{v.plate}</span>
                      ) : (
                        <Link
                          to={`/app/vehicles/${v.id}`}
                          className="font-mono text-sm font-bold tracking-widest text-tc-text hover:text-tc-purple"
                        >
                          {v.plate}
                        </Link>
                      )}
                    </div>
                    <div className="px-4 py-3">
                      <p className="text-sm text-tc-text-secondary truncate">
                        {[v.brand, v.model].filter(Boolean).join(' ') || <span className="text-tc-text-muted">—</span>}
                      </p>
                      <p className="mt-0.5 text-xs text-tc-text-muted truncate md:hidden">{v.customerName}</p>
                    </div>
                    <div className="hidden md:block px-4 py-3 text-right">
                      <span className="tabular-nums text-sm text-tc-text-secondary">{v.year ?? '—'}</span>
                    </div>
                    <div className="hidden md:block px-4 py-3">
                      <span className="text-sm text-tc-text-secondary truncate">{v.customerName}</span>
                    </div>
                    <div className="flex h-full items-center justify-center border-l border-tc-border bg-tc-stub-bg">
                      {v.isDeleted ? (
                        <button
                          onClick={() => handleRestore(v)}
                          className="px-3 py-3 text-[10px] font-bold tracking-wider uppercase text-green-700 hover:text-green-900 transition-colors"
                        >
                          Restaurar
                        </button>
                      ) : (
                        <button
                          onClick={() => setEditing(v)}
                          className="px-3 py-3 text-[10px] font-bold tracking-wider uppercase text-tc-navy-text-dim hover:text-tc-purple transition-colors"
                        >
                          Editar
                        </button>
                      )}
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </div>

          {data.total > data.pageSize && (
            <div className="flex items-center justify-between text-sm text-tc-text-secondary">
              <span className="tabular-nums">{data.total} vehículos</span>
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
        <VehicleFormModal onSuccess={handleSuccess} onClose={() => setCreating(false)} />
      )}
      {editing && editVehicle && (
        <VehicleFormModal
          vehicle={editVehicle}
          onSuccess={handleSuccess}
          onClose={() => setEditing(null)}
          onDelete={() => {
            setEditing(null)
            queryClient.invalidateQueries({ queryKey: ['vehicles'] })
          }}
        />
      )}
    </div>
  )
}
