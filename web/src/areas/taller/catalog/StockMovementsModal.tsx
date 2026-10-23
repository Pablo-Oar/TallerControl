import { useQuery } from '@tanstack/react-query'
import { catalogApi, STOCK_REASON_LABELS, type CatalogItemListDto } from '../../../lib/api/catalog'
import { Modal } from '../../../components/ui/Modal'

/** Libro de movimientos de stock de un producto (entradas y salidas, con su motivo y nota). */
export function StockMovementsModal({ item, onClose }: { item: CatalogItemListDto; onClose: () => void }) {
  const { data, isLoading, error } = useQuery({
    queryKey: ['catalog', item.id, 'movements'],
    queryFn: () => catalogApi.getMovements(item.id, 1, 100),
  })

  return (
    <Modal title={`Movimientos · ${item.name}`} onClose={onClose} className="max-w-xl">
      <div className="space-y-3">
        <p className="text-sm text-tc-text-secondary">
          Stock actual: <b className="tabular-nums text-tc-text">{item.stockQty}</b>
        </p>

        {isLoading && <p className="text-sm text-tc-text-muted">Cargando…</p>}
        {error && <p role="alert" className="text-sm text-tc-stamp-red">No se pudieron cargar los movimientos.</p>}
        {data && data.length === 0 && <p className="text-sm text-tc-text-muted">Todavía no hay movimientos.</p>}

        {data && data.length > 0 && (
          <ul className="max-h-[60vh] divide-y divide-tc-border overflow-auto border border-tc-border">
            {data.map(m => (
              <li key={m.id} className="flex items-start gap-3 px-3 py-2 text-sm">
                <span className={`w-16 shrink-0 text-right font-bold tabular-nums ${m.quantity < 0 ? 'text-tc-stamp-red' : 'text-green-700'}`}>
                  {m.quantity > 0 ? '+' : ''}{m.quantity}
                </span>
                <span className="min-w-0 flex-1">
                  <span className="block text-tc-text">{STOCK_REASON_LABELS[m.reason] ?? m.reason}</span>
                  {m.note && <span className="block text-xs text-tc-text-secondary">{m.note}</span>}
                </span>
                <span className="shrink-0 text-xs tabular-nums text-tc-text-muted">
                  {new Date(m.createdAt).toLocaleString('es-AR', { dateStyle: 'short', timeStyle: 'short' })}
                </span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </Modal>
  )
}
