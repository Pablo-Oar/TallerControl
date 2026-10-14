import { useState } from 'react'
import { useParams, Link } from 'react-router'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  workOrdersApi,
  type WorkOrderStatus,
  STATUS_LABELS,
  STATUS_COLORS,
  PRIORITY_LABELS,
} from '../../../lib/api/workOrders'
import { Button } from '../../../components/ui/Button'
import { ChangeStatusModal } from './ChangeStatusModal'

export function WorkOrderDetailPage() {
  const { id } = useParams<{ id: string }>()
  const queryClient = useQueryClient()
  const [changingStatus, setChangingStatus] = useState(false)

  const { data: order, isLoading, error } = useQuery({
    queryKey: ['work-order', id],
    queryFn: () => workOrdersApi.get(id!),
    enabled: !!id,
  })

  const { data: history } = useQuery({
    queryKey: ['work-order-history', id],
    queryFn: () => workOrdersApi.getHistory(id!),
    enabled: !!id,
  })

  function handleStatusChange() {
    setChangingStatus(false)
    queryClient.invalidateQueries({ queryKey: ['work-order', id] })
    queryClient.invalidateQueries({ queryKey: ['work-order-history', id] })
    queryClient.invalidateQueries({ queryKey: ['work-orders'] })
  }

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <div className="h-6 w-6 animate-spin rounded-full border-2 border-blue-600 border-t-transparent" />
      </div>
    )
  }

  if (error || !order) {
    return <p className="text-sm text-red-600">No se pudo cargar la orden.</p>
  }

  const vehicleLabel = [order.vehicleBrand, order.vehicleModel].filter(Boolean).join(' ')

  return (
    <div className="space-y-6">
      {/* Encabezado */}
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-2xl font-semibold">Orden #{order.number}</h1>
            <span className={`rounded-full px-2.5 py-0.5 text-sm font-medium ${STATUS_COLORS[order.status]}`}>
              {STATUS_LABELS[order.status]}
            </span>
          </div>
          <p className="mt-1 text-sm text-slate-500">
            Prioridad: {PRIORITY_LABELS[order.priority]}
            {' · '}Creada: {new Date(order.createdAt ?? '').toLocaleDateString('es-AR')}
          </p>
        </div>
        <div className="flex gap-2">
          <Link to="/app/work-orders">
            <Button variant="secondary">Volver</Button>
          </Link>
          {order.status !== 'cancelled' && order.status !== 'delivered' && (
            <Button onClick={() => setChangingStatus(true)}>Cambiar estado</Button>
          )}
        </div>
      </div>

      {/* Grid de info */}
      <div className="grid gap-4 md:grid-cols-2">
        {/* Cliente y vehículo */}
        <InfoCard title="Cliente y vehículo">
          <Row label="Cliente">
            <Link to={`/app/customers`} className="text-blue-600 hover:underline">
              {order.customerName}
            </Link>
          </Row>
          <Row label="Vehículo">
            <span className="font-mono">{order.vehiclePlate}</span>
            {vehicleLabel && <span className="ml-1 text-slate-500">— {vehicleLabel}</span>}
          </Row>
          <Row label="Odómetro">{order.odometerKm.toLocaleString('es-AR')} km</Row>
          {order.fuelLevel != null && (
            <Row label="Combustible">{order.fuelLevel}%</Row>
          )}
        </InfoCard>

        {/* Estado y mecánico */}
        <InfoCard title="Asignación">
          <Row label="Mecánico">{order.assignedMechanicName ?? <span className="text-slate-400">Sin asignar</span>}</Row>
          {order.completedAt && <Row label="Completada">{new Date(order.completedAt).toLocaleDateString('es-AR')}</Row>}
          {order.deliveredAt && <Row label="Entregada">{new Date(order.deliveredAt).toLocaleDateString('es-AR')}</Row>}
          {order.cancelledAt && (
            <>
              <Row label="Cancelada">{new Date(order.cancelledAt).toLocaleDateString('es-AR')}</Row>
              {order.cancelReason && <Row label="Motivo">{order.cancelReason}</Row>}
            </>
          )}
        </InfoCard>
      </div>

      {/* Problema reportado */}
      {order.reportedIssue && (
        <InfoCard title="Problema reportado">
          <p className="text-sm">{order.reportedIssue}</p>
        </InfoCard>
      )}

      {/* Renglones */}
      {order.items.length > 0 && (
        <InfoCard title="Renglones">
          <table className="w-full text-sm">
            <thead className="text-xs font-semibold uppercase tracking-wide text-slate-400">
              <tr>
                <th className="pb-2 text-left">Descripción</th>
                <th className="pb-2 text-right">Cant.</th>
                <th className="pb-2 text-right">P. neto</th>
                <th className="pb-2 text-right">IVA %</th>
                <th className="pb-2 text-right">Total línea</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {order.items.map(item => (
                <tr key={item.id}>
                  <td className="py-2">{item.description}</td>
                  <td className="py-2 text-right tabular-nums">{item.quantity}</td>
                  <td className="py-2 text-right tabular-nums">
                    {order.subtotalNet != null ? `$${item.unitNetPrice.toFixed(2)}` : '—'}
                  </td>
                  <td className="py-2 text-right">{item.vatRate}%</td>
                  <td className="py-2 text-right tabular-nums font-medium">
                    {order.total != null ? `$${item.lineTotal.toFixed(2)}` : '—'}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {order.total != null && (
            <div className="mt-3 border-t border-slate-100 pt-3 text-right text-sm">
              <span className="text-slate-500">Subtotal neto: </span>
              <span className="tabular-nums">${order.subtotalNet?.toFixed(2)}</span>
              <span className="ml-4 text-slate-500">IVA: </span>
              <span className="tabular-nums">${order.vatAmount?.toFixed(2)}</span>
              <span className="ml-4 font-semibold">Total: ${order.total.toFixed(2)}</span>
              {order.balance != null && order.balance > 0 && (
                <span className="ml-4 text-amber-600 font-medium">Saldo: ${order.balance.toFixed(2)}</span>
              )}
            </div>
          )}
        </InfoCard>
      )}

      {/* Notas */}
      {(order.internalNotes || order.mechanicNotes) && (
        <InfoCard title="Notas">
          {order.internalNotes && (
            <div>
              <p className="mb-1 text-xs font-medium text-slate-400 uppercase tracking-wide">Internas</p>
              <p className="text-sm">{order.internalNotes}</p>
            </div>
          )}
          {order.mechanicNotes && (
            <div className={order.internalNotes ? 'mt-3' : ''}>
              <p className="mb-1 text-xs font-medium text-slate-400 uppercase tracking-wide">Del mecánico</p>
              <p className="text-sm">{order.mechanicNotes}</p>
            </div>
          )}
        </InfoCard>
      )}

      {/* Historial */}
      {history && history.length > 0 && (
        <InfoCard title="Historial de estados">
          <ol className="space-y-2">
            {history.map(change => (
              <li key={change.id} className="flex items-start gap-3 text-sm">
                <span className="mt-0.5 text-xs text-slate-400 tabular-nums whitespace-nowrap">
                  {new Date(change.changedAt).toLocaleString('es-AR')}
                </span>
                <span>
                  {change.fromStatus
                    ? <>{STATUS_LABELS[change.fromStatus as WorkOrderStatus]} → </>
                    : null}
                  <strong>{STATUS_LABELS[change.toStatus as WorkOrderStatus]}</strong>
                  {change.note && <span className="ml-1 text-slate-500">— {change.note}</span>}
                  {change.changedByName && (
                    <span className="ml-1 text-slate-400 text-xs">por {change.changedByName}</span>
                  )}
                </span>
              </li>
            ))}
          </ol>
        </InfoCard>
      )}

      {changingStatus && (
        <ChangeStatusModal
          orderId={order.id}
          currentStatus={order.status}
          onSuccess={handleStatusChange}
          onClose={() => setChangingStatus(false)}
        />
      )}
    </div>
  )
}

// ── Helpers de layout ──────────────────────────────────────────────────────────

function InfoCard({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="rounded-lg border border-slate-200 bg-white p-4">
      <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-400">{title}</h2>
      {children}
    </div>
  )
}

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex items-baseline justify-between py-1 text-sm">
      <span className="text-slate-500">{label}</span>
      <span>{children}</span>
    </div>
  )
}
