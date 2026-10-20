import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { useInfiniteQuery, useQuery, useQueryClient } from '@tanstack/react-query'
import { vehiclesApi, type VehicleHistoryEntryDto } from '../../../lib/api/vehicles'
import { STATUS_LABELS as ORDER_STATUS_LABELS, type WorkOrderStatus } from '../../../lib/api/workOrders'
import { QUOTE_STATUS_LABELS, type QuoteStatus } from '../../../lib/api/quotes'
import { Button } from '../../../components/ui/Button'
import { StatusBadge } from '../../../components/ui/StatusBadge'
import { Timeline, type TimelineItem } from '../../../components/ui/Timeline'
import { VehicleFormModal } from './VehicleFormModal'

const fmtARS = (n: number) =>
  new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 2 }).format(n)

function toItem(e: VehicleHistoryEntryDto): TimelineItem {
  const isOrder = e.type === 'work_order'
  const parts = [
    e.odometerKm != null ? `${e.odometerKm.toLocaleString('es-AR')} km` : null,
    e.total != null ? `Total ${fmtARS(e.total)}` : null,
  ].filter(Boolean)
  return {
    id: `${e.type}-${e.id}`,
    date: e.date,
    kind: isOrder ? 'order' : 'quote',
    title: `${isOrder ? 'Orden' : 'Presupuesto'} ${e.number}`,
    href: isOrder ? `/app/work-orders/${e.id}` : `/app/quotes/${e.id}`,
    badge: (
      <StatusBadge
        status={e.status}
        label={(isOrder ? ORDER_STATUS_LABELS[e.status as WorkOrderStatus] : QUOTE_STATUS_LABELS[e.status as QuoteStatus]) ?? e.status}
      />
    ),
    detail: parts.length ? parts.join(' · ') : null,
  }
}

/** Ficha del vehículo (doc 06 /app/vehiculos/:id): datos y su historial de órdenes y presupuestos (REQ-07). */
export function VehicleDetailPage() {
  const { id } = useParams<{ id: string }>()
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState(false)

  const { data: vehicle, isLoading, error } = useQuery({
    queryKey: ['vehicle', id],
    queryFn: () => vehiclesApi.get(id!),
    enabled: !!id,
  })

  const history = useInfiniteQuery({
    queryKey: ['vehicle-history', id],
    queryFn: ({ pageParam }) => vehiclesApi.history(id!, pageParam, 25),
    initialPageParam: 1,
    getNextPageParam: last => (last.pageNumber * last.pageSize < last.total ? last.pageNumber + 1 : undefined),
    enabled: !!id,
  })

  if (isLoading) {
    return (
      <div className="flex justify-center py-16">
        <div className="h-6 w-6 animate-spin border-2 border-tc-purple border-t-transparent" />
      </div>
    )
  }
  if (error || !vehicle) {
    return <p className="border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">No se pudo cargar el vehículo.</p>
  }

  const items = history.data?.pages.flatMap(p => p.items).map(toItem) ?? []
  const total = history.data?.pages[0]?.total ?? 0

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="font-mono text-xl font-bold tracking-widest text-tc-text">{vehicle.plate}</h1>
          <p className="mt-1 text-sm text-tc-text-secondary">
            {[vehicle.brand, vehicle.model, vehicle.versionName].filter(Boolean).join(' ') || 'Sin marca ni modelo'}
          </p>
        </div>
        <div className="flex gap-2">
          <Link to="/app/vehicles">
            <Button variant="secondary" size="sm">← Volver</Button>
          </Link>
          <Button size="sm" onClick={() => setEditing(true)}>Editar</Button>
        </div>
      </div>

      <section className="border border-tc-border bg-white">
        <div className="bg-tc-stub-bg border-b border-tc-border px-4 py-2">
          <span className="text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Datos</span>
        </div>
        <dl className="grid grid-cols-2 gap-4 p-4 text-sm md:grid-cols-4">
          <div>
            <dt className="mb-1 text-[10px] font-bold uppercase tracking-wider text-tc-text-muted">Cliente</dt>
            <dd className="text-tc-text">{vehicle.customerName}</dd>
          </div>
          <div>
            <dt className="mb-1 text-[10px] font-bold uppercase tracking-wider text-tc-text-muted">Kilometraje</dt>
            <dd className="tabular-nums text-tc-text">{vehicle.odometerKm.toLocaleString('es-AR')} km</dd>
          </div>
          <div>
            <dt className="mb-1 text-[10px] font-bold uppercase tracking-wider text-tc-text-muted">Año</dt>
            <dd className="tabular-nums text-tc-text">{vehicle.year ?? '—'}</dd>
          </div>
          <div>
            <dt className="mb-1 text-[10px] font-bold uppercase tracking-wider text-tc-text-muted">Color</dt>
            <dd className="text-tc-text">{vehicle.color ?? '—'}</dd>
          </div>
        </dl>
      </section>

      <section className="border border-tc-border bg-white">
        <div className="bg-tc-stub-bg border-b border-tc-border px-4 py-2">
          <span className="text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">
            Historial{total > 0 ? ` (${total})` : ''}
          </span>
        </div>
        <div className="p-4">
          {history.isLoading ? (
            <p className="text-sm text-tc-text-muted">Cargando historial…</p>
          ) : (
            <Timeline items={items} label="Historial del vehículo" empty="Este vehículo todavía no tiene órdenes ni presupuestos." />
          )}
          {history.hasNextPage && (
            <div className="mt-4">
              <Button variant="secondary" size="sm" loading={history.isFetchingNextPage} onClick={() => history.fetchNextPage()}>
                Cargar más
              </Button>
            </div>
          )}
        </div>
      </section>

      {editing && (
        <VehicleFormModal
          vehicle={vehicle}
          onSuccess={() => {
            setEditing(false)
            queryClient.invalidateQueries({ queryKey: ['vehicle', id] })
            queryClient.invalidateQueries({ queryKey: ['vehicles'] })
          }}
          onClose={() => setEditing(false)}
        />
      )}
    </div>
  )
}
