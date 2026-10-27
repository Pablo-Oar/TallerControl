import { formatDate } from '../../../lib/dates'
import { LineItemsList } from '../../../components/ui/LineItemsList'
import { useState } from 'react'
import { useParams, Link } from 'react-router'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  workOrdersApi,
  type WorkOrderStatus,
  STATUS_LABELS,
  PRIORITY_LABELS,
  WORK_ORDER_TRANSITIONS,
} from '../../../lib/api/workOrders'
import {
  getPayments,
  voidPayment,
  getPaymentPdfUrl,
  createPaymentPublicLink,
  METHOD_LABELS,
  STATUS_LABELS as PAYMENT_STATUS_LABELS,
} from '../../../lib/api/payments'
import { Button } from '../../../components/ui/Button'
import { WorkOrderFormModal } from './WorkOrderFormModal'
import { quotesApi } from '../../../lib/api/quotes'
import { WorkOrderPhotos } from '../../../components/WorkOrderPhotos'
import { FormSelect, FormTextarea } from '../../../components/ui/FormField'
import { ApiError } from '../../../lib/api/client'
import { notify } from '../../../components/ui/dialogs'
import { EditableStatusBadge } from '../../../components/ui/EditableStatusBadge'
import { StatusBadge } from '../../../components/ui/StatusBadge'
import { Timeline } from '../../../components/ui/Timeline'
import { Modal } from '../../../components/ui/Modal'
import { ChangeStatusModal } from './ChangeStatusModal'
import { CreatePaymentModal } from './CreatePaymentModal'
import { InvoiceSection } from '../invoicing/InvoiceSection'
import { ShareLink } from '../../../components/ShareLink'
import { useCan } from '../../../lib/auth/permissions'

const fmtARS = (n: number) =>
  new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 2 }).format(n)

export function WorkOrderDetailPage() {
  const { id } = useParams<{ id: string }>()
  const can = useCan()
  const [editing, setEditing] = useState(false)
  const [orderLink, setOrderLink] = useState<string | null>(null)
  const [receipt, setReceipt] = useState<{ number: number; url: string } | null>(null)

  async function shareOrderLink() {
    try {
      const result = await quotesApi.getPublicLink(id!)
      setOrderLink(`${window.location.origin}/o/${result.tokenBase64Url}`)
    } catch (err) {
      notify(err instanceof ApiError ? err.detail : 'No se pudo generar el enlace de la orden')
    }
  }

  async function shareReceipt(paymentId: string, number: number) {
    try {
      setReceipt({ number, url: await createPaymentPublicLink(id!, paymentId) })
    } catch (err) {
      notify(err instanceof ApiError ? err.detail : 'No se pudo generar el enlace del comprobante')
    }
  }
  const queryClient = useQueryClient()
  const [changingStatus, setChangingStatus] = useState(false)
  const [creatingPayment, setCreatingPayment] = useState(false)
  const [voidingPaymentId, setVoidingPaymentId] = useState<string | null>(null)

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

  const { data: payments, refetch: refetchPayments } = useQuery({
    queryKey: ['work-order-payments', id],
    queryFn: () => getPayments(id!),
    enabled: !!id,
  })

  const [initialStatus, setInitialStatus] = useState<WorkOrderStatus | undefined>(undefined)
  const [assigning, setAssigning] = useState(false)
  const [assignError, setAssignError] = useState<string | null>(null)

  const { data: mechanics } = useQuery({
    queryKey: ['work-order-mechanics'],
    queryFn: () => workOrdersApi.listMechanics(),
    staleTime: 60_000,
  })

  async function handleAssign(mechanicId: string) {
    if (!id || !mechanicId) return
    setAssigning(true)
    setAssignError(null)
    try {
      await workOrdersApi.assign(id, mechanicId)
      queryClient.invalidateQueries({ queryKey: ['work-order', id] })
      queryClient.invalidateQueries({ queryKey: ['work-orders'] })
    } catch (err) {
      setAssignError(err instanceof ApiError ? err.detail : 'No se pudo asignar el mecánico.')
    } finally {
      setAssigning(false)
    }
  }

  // Badge editable: aplica la transición directa; "Anulada" exige motivo, así que delega en el diálogo.
  async function handleBadgeSelect(value: string) {
    if (!id) return
    if (value === 'cancelled') {
      setInitialStatus('cancelled')
      setChangingStatus(true)
      return
    }
    await workOrdersApi.changeStatus(id, { toStatus: value as WorkOrderStatus })
    handleStatusChange()
  }

  function handleStatusChange() {
    setChangingStatus(false)
    queryClient.invalidateQueries({ queryKey: ['work-order', id] })
    queryClient.invalidateQueries({ queryKey: ['work-order-history', id] })
    // Cancelar deja el saldo en 0: los cobros (total/cobrado/saldo) también se recalculan.
    queryClient.invalidateQueries({ queryKey: ['work-order-payments', id] })
    queryClient.invalidateQueries({ queryKey: ['work-orders'] })
  }

  function handlePaymentSuccess() {
    setCreatingPayment(false)
    setVoidingPaymentId(null)
    void refetchPayments()
    queryClient.invalidateQueries({ queryKey: ['work-order', id] })
    queryClient.invalidateQueries({ queryKey: ['work-orders'] })
  }

  async function handleVoidPayment(paymentId: string, reason: string) {
    if (!id) return
    await voidPayment(id, paymentId, reason)
    setVoidingPaymentId(null)
    void refetchPayments()
    queryClient.invalidateQueries({ queryKey: ['work-order', id] })
  }

  if (isLoading) {
    return (
      <div className="flex justify-center py-16">
        <div className="h-6 w-6 animate-spin border-2 border-tc-purple border-t-transparent" />
      </div>
    )
  }

  if (error || !order) {
    return <p className="border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">No se pudo cargar la orden.</p>
  }

  const vehicleLabel = [order.vehicleBrand, order.vehicleModel].filter(Boolean).join(' ')
  const canAct = order.status !== 'cancelled' && order.status !== 'delivered'

  return (
    <div className="space-y-5">
      {/* Encabezado */}
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex flex-wrap items-center gap-3">
          <h1 className="text-lg font-bold tracking-wide text-tc-text uppercase">
            Orden #{order.number}
          </h1>
          <EditableStatusBadge
            status={order.status}
            statusText={STATUS_LABELS[order.status]}
            options={canAct ? WORK_ORDER_TRANSITIONS[order.status].map(s => ({ value: s, label: STATUS_LABELS[s] })) : []}
            onSelect={handleBadgeSelect}
          />
          <span className="text-xs text-tc-text-muted uppercase tracking-wider">
            {PRIORITY_LABELS[order.priority]}
          </span>
        </div>
        <div className="grid w-full grid-cols-3 gap-2 sm:flex sm:w-auto">
          <Link to="/app/work-orders" className="contents">
            <Button variant="secondary" size="sm" className="whitespace-nowrap">← Volver</Button>
          </Link>
          <a
            href={`/api/v1/work-orders/${order.id}/pdf`}
            target="_blank"
            rel="noreferrer"
            className="inline-flex items-center justify-center border border-tc-border bg-white px-3 py-1.5 text-xs font-bold tracking-wider uppercase text-tc-text-secondary transition-colors hover:bg-tc-surface max-md:min-h-11"
          >
            PDF
          </a>
          {canAct && can('workorders.write') && (
            <Button variant="secondary" size="sm" onClick={() => setEditing(true)}>Editar</Button>
          )}
          {canAct && (
            <Button variant="secondary" size="sm" className="col-span-3 sm:col-auto" onClick={shareOrderLink}>Enlace para el cliente</Button>
          )}
          {canAct && (
            <Button size="sm" className="col-span-3 sm:col-auto" onClick={() => { setInitialStatus(undefined); setChangingStatus(true) }}>Cambiar estado</Button>
          )}
        </div>
      </div>

      {/* Info principal: coupon anatomy */}
      <div className="grid gap-4 md:grid-cols-2">
        <Section title="Cliente y vehículo">
          <Row label="Cliente">{order.customerName}</Row>
          <Row label="Vehículo">
            <span className="font-mono font-bold tracking-widest">{order.vehiclePlate}</span>
            {vehicleLabel && <span className="ml-1 text-tc-text-secondary">— {vehicleLabel}</span>}
          </Row>
          <Row label="Odómetro">
            <span className="tabular-nums">{order.odometerKm.toLocaleString('es-AR')} km</span>
          </Row>
          {order.fuelLevel != null && (
            <Row label="Combustible"><span className="tabular-nums">{order.fuelLevel}%</span></Row>
          )}
        </Section>

        <Section title="Asignación">
          <Row label="Mecánico">
            {order.assignedMechanicName ?? <span className="text-tc-text-muted">Sin asignar</span>}
            {canAct && (mechanics?.length ?? 0) > 0 && (
              <FormSelect
                label={order.assignedMechanicId ? 'Reasignar a' : 'Asignar a'}
                size="sm"
                className="mt-2 w-56"
                disabled={assigning}
                value=""
                onChange={e => void handleAssign(e.target.value)}
                error={assignError ?? undefined}
              >
                <option value="">Seleccionar mecánico…</option>
                {mechanics!.filter(m => m.membershipId !== order.assignedMechanicId).map(m => (
                  <option key={m.membershipId} value={m.membershipId}>{m.displayName}</option>
                ))}
              </FormSelect>
            )}
          </Row>
          <Row label="Creada">
            {formatDate(order.createdAt ?? '')}
          </Row>
          {order.completedAt && (
            <Row label="Completada">{formatDate(order.completedAt)}</Row>
          )}
          {order.deliveredAt && (
            <Row label="Entregada">{formatDate(order.deliveredAt)}</Row>
          )}
          {order.cancelledAt && (
            <>
              <Row label="Cancelada">{formatDate(order.cancelledAt)}</Row>
              {order.cancelReason && <Row label="Motivo">{order.cancelReason}</Row>}
            </>
          )}
        </Section>
      </div>

      {/* Problema reportado */}
      {order.reportedIssue && (
        <Section title="Problema reportado">
          <p className="text-sm text-tc-text-secondary">{order.reportedIssue}</p>
        </Section>
      )}

      {/* Renglones */}
      {order.items.length > 0 && (
        <Section title="Renglones">
          {/* Celular: una fila por renglón (la tabla de 5 columnas cortaba los importes). */}
          <div className="md:hidden">
            <LineItemsList
              label="Renglones de la orden"
              items={order.items.map(item => ({
                id: item.id,
                title: item.description,
                detail: `${item.quantity} × ${item.unitNetPrice != null ? fmtARS(item.unitNetPrice) : '—'} · IVA ${item.vatRate}%`,
                amount: item.lineTotal != null ? fmtARS(item.lineTotal) : '—',
              }))}
            />
          </div>
          <div className="hidden overflow-x-auto md:block">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-tc-border bg-tc-stub-bg">
                  {['Descripción', 'Cant.', 'P. neto', 'IVA', 'Total línea'].map((h, i) => (
                    <th key={i} className={`px-3 py-2 text-[11px] font-bold tracking-[0.14em] uppercase text-tc-text-muted ${i > 0 ? 'text-right' : 'text-left'}`}>
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-tc-border">
                {order.items.map(item => (
                  <tr key={item.id}>
                    <td className="px-3 py-2.5">{item.description}</td>
                    <td className="px-3 py-2.5 text-right tabular-nums">{item.quantity}</td>
                    <td className="px-3 py-2.5 text-right tabular-nums text-tc-purple">
                      {item.unitNetPrice != null ? fmtARS(item.unitNetPrice) : '—'}
                    </td>
                    <td className="px-3 py-2.5 text-right text-tc-text-secondary">{item.vatRate}%</td>
                    <td className="px-3 py-2.5 text-right tabular-nums font-semibold text-tc-purple">
                      {item.lineTotal != null ? fmtARS(item.lineTotal) : '—'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {order.total != null && (
            <div className="mt-3 flex flex-wrap justify-end gap-x-6 gap-y-1 border-t border-tc-perf px-4 pt-3 text-sm md:px-0">
              <span className="text-tc-text-secondary">
                Subtotal neto <span className="tabular-nums text-tc-text">{fmtARS(order.subtotalNet ?? 0)}</span>
              </span>
              <span className="text-tc-text-secondary">
                IVA <span className="tabular-nums text-tc-text">{fmtARS(order.vatAmount ?? 0)}</span>
              </span>
              <span className="font-bold text-tc-purple tabular-nums">
                Total {fmtARS(order.total)}
              </span>
              {(order.balance ?? 0) > 0 && (
                <span className="font-semibold text-amber-600 tabular-nums">
                  Saldo {fmtARS(order.balance!)}
                </span>
              )}
            </div>
          )}
        </Section>
      )}

      {/* Notas */}
      {(order.internalNotes || order.mechanicNotes) && (
        <Section title="Notas">
          {order.internalNotes && (
            <div>
              <p className="mb-1 text-[11px] font-bold tracking-wider uppercase text-tc-text-muted">Internas</p>
              <p className="text-sm text-tc-text-secondary">{order.internalNotes}</p>
            </div>
          )}
          {order.mechanicNotes && (
            <div className={order.internalNotes ? 'mt-3' : ''}>
              <p className="mb-1 text-[11px] font-bold tracking-wider uppercase text-tc-text-muted">Del mecánico</p>
              <p className="text-sm text-tc-text-secondary">{order.mechanicNotes}</p>
            </div>
          )}
        </Section>
      )}

      {/* Cobros */}
      <Section title="Fotos">
        <WorkOrderPhotos workOrderId={order.id} canDelete />
      </Section>

      <Section title="Cobros">
        {payments && (
          <div className="mb-3 flex flex-wrap gap-6 text-sm border-b border-tc-perf pb-3">
            <span className="text-tc-text-secondary">
              Total <span className="tabular-nums font-semibold text-tc-text">{fmtARS(payments.total)}</span>
            </span>
            <span className="text-tc-text-secondary">
              Cobrado <span className="tabular-nums font-semibold text-tc-done-text">{fmtARS(payments.paid)}</span>
            </span>
            <span className={payments.balance > 0 ? 'text-amber-600' : 'text-tc-done-text'}>
              Saldo <span className="tabular-nums font-bold">{fmtARS(payments.balance)}</span>
            </span>
          </div>
        )}

        {payments && payments.payments.length > 0 && (
          <div className="mb-3 overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-tc-border">
                {['#', 'Método', 'Monto', 'Fecha', 'Estado', ''].map((h, i) => (
                  <th key={i} className={`pb-2 text-[11px] font-bold tracking-[0.14em] uppercase text-tc-text-muted ${i === 2 ? 'text-right px-3' : i > 0 ? 'px-3 text-left' : 'text-left'}`}>
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-tc-border">
              {payments.payments.map(pay => (
                <tr key={pay.id} className={pay.status === 'voided' ? 'opacity-60' : ''}>
                  <td className="py-2 text-tc-text-muted tabular-nums">{String(pay.number).padStart(4, '0')}</td>
                  <td className="py-2 px-3">{METHOD_LABELS[pay.method]}</td>
                  <td className={`py-2 px-3 text-right tabular-nums font-semibold ${pay.status === 'voided' ? 'line-through text-tc-text-muted' : 'text-tc-purple'}`}>
                    {fmtARS(pay.amount)}
                  </td>
                  <td className="py-2 px-3 text-tc-text-secondary text-xs">
                    {formatDate(pay.paidAt)}
                  </td>
                  <td className="py-2 px-3">
                    <StatusBadge status={pay.status} label={PAYMENT_STATUS_LABELS[pay.status]} />
                  </td>
                  <td className="py-2 text-right">
                    <div className="flex justify-end gap-3">
                      <a
                        href={getPaymentPdfUrl(id!, pay.id)}
                        target="_blank"
                        rel="noreferrer"
                        className="text-[11px] font-bold tracking-wider uppercase text-tc-navy-text-dim hover:text-tc-purple transition-colors"
                      >
                        PDF
                      </a>
                      <button
                        onClick={() => shareReceipt(pay.id, pay.number)}
                        className="text-[11px] font-bold tracking-wider uppercase text-tc-navy-text-dim hover:text-tc-purple transition-colors"
                      >
                        Enlace
                      </button>
                      {pay.status === 'valid' && can('payments.void') && (
                        <button
                          onClick={() => setVoidingPaymentId(pay.id)}
                          className="text-[11px] font-bold tracking-wider uppercase text-tc-stamp-red hover:text-red-700 transition-colors"
                        >
                          Anular
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          </div>
        )}

        {!payments?.payments.length && (
          <p className="mb-3 text-sm text-tc-text-muted">Sin cobros registrados.</p>
        )}

        {/* Se puede cobrar una orden ya entregada (saldo pendiente); solo una anulada no admite cobros. */}
        {order.status !== 'cancelled' && (payments?.balance ?? 0) > 0 && (
          <Button variant="secondary" size="sm" onClick={() => setCreatingPayment(true)}>
            + Registrar cobro
          </Button>
        )}
      </Section>

      {/* Historial */}
      {history && history.length > 0 && (
        <Section title="Historial de estados">
          <Timeline
            label="Historial de estados"
            items={history.map(change => ({
              id: change.id,
              date: change.changedAt,
              kind: 'status' as const,
              title: (
                <>
                  {change.fromStatus && <>{STATUS_LABELS[change.fromStatus as WorkOrderStatus]} → </>}
                  {STATUS_LABELS[change.toStatus as WorkOrderStatus]}
                </>
              ),
              detail: change.note,
              author: change.changedByName,
            }))}
          />
        </Section>
      )}

      <Section title="Facturación">
        <InvoiceSection
          sourceType="work_order"
          sourceId={order.id}
          eligible={order.status !== 'cancelled'}
          ineligibleReason="Una orden cancelada no se factura."
        />
      </Section>

      {/* Modales */}
      {changingStatus && (
        <ChangeStatusModal
          orderId={order.id}
          currentStatus={order.status}
          initialStatus={initialStatus}
          onSuccess={handleStatusChange}
          onClose={() => setChangingStatus(false)}
        />
      )}

      {creatingPayment && payments && (
        <CreatePaymentModal
          workOrderId={id!}
          balance={payments.balance}
          onSuccess={handlePaymentSuccess}
          onClose={() => setCreatingPayment(false)}
        />
      )}

      {editing && (
        <WorkOrderFormModal
          order={order}
          onSuccess={() => {
            setEditing(false)
            queryClient.invalidateQueries({ queryKey: ['work-order', id] })
            queryClient.invalidateQueries({ queryKey: ['work-orders'] })
            queryClient.invalidateQueries({ queryKey: ['catalog'] })
            void refetchPayments()
          }}
          onClose={() => setEditing(false)}
        />
      )}

      {orderLink && (
        <Modal title="Enlace para el cliente" onClose={() => setOrderLink(null)} className="max-w-md">
          <div className="space-y-4">
            <p className="text-sm text-tc-text-secondary">
              Con este enlace el cliente sigue el estado de su orden (sin precios). Dura 180 días y reemplaza al enlace anterior.
            </p>
            <ShareLink url={orderLink} whatsappText={`Estado de tu orden #${order.number}:`} />
            <div className="flex justify-end">
              <Button size="sm" onClick={() => setOrderLink(null)}>Cerrar</Button>
            </div>
          </div>
        </Modal>
      )}

      {receipt && (
        <Modal title={`Comprobante #${String(receipt.number).padStart(4, '0')}`} onClose={() => setReceipt(null)} className="max-w-md">
          <div className="space-y-4">
            <p className="text-sm text-tc-text-secondary">Este enlace reemplaza al anterior del comprobante.</p>
            <ShareLink url={receipt.url} whatsappText="Comprobante de pago:" />
            <div className="flex justify-end">
              <Button size="sm" onClick={() => setReceipt(null)}>Cerrar</Button>
            </div>
          </div>
        </Modal>
      )}

      {voidingPaymentId && (
        <VoidPaymentDialog
          onConfirm={(reason) => handleVoidPayment(voidingPaymentId, reason)}
          onClose={() => setVoidingPaymentId(null)}
        />
      )}
    </div>
  )
}

// ── Helpers ────────────────────────────────────────────────────────────────────

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="border border-tc-border bg-white overflow-hidden">
      <div className="bg-tc-stub-bg border-b border-tc-border px-4 py-2">
        <span className="text-[11px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">{title}</span>
      </div>
      <div className="p-4">{children}</div>
    </div>
  )
}

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex items-baseline justify-between py-1.5 text-sm border-b border-tc-border last:border-0">
      <span className="text-xs font-bold tracking-wider uppercase text-tc-text-muted">{label}</span>
      <span className="text-tc-text">{children}</span>
    </div>
  )
}

function VoidPaymentDialog({ onConfirm, onClose }: { onConfirm: (r: string) => void; onClose: () => void }) {
  const [reason, setReason] = useState('')
  const [loading, setLoading] = useState(false)

  async function handleConfirm() {
    if (!reason.trim()) return
    setLoading(true)
    try { await onConfirm(reason.trim()) } finally { setLoading(false) }
  }

  return (
    <Modal title="Anular cobro" onClose={onClose} className="max-w-sm">
      <div className="space-y-4">
        <p className="text-sm text-tc-text-secondary">Ingresá el motivo de la anulación:</p>
        <FormTextarea
          label="Motivo"
          value={reason}
          onChange={e => setReason(e.target.value)}
          rows={3}
          placeholder="Motivo…"
          autoFocus
        />
        <div className="flex justify-end gap-2">
          <Button variant="secondary" size="sm" onClick={onClose} disabled={loading}>Cancelar</Button>
          <Button variant="danger" size="sm" onClick={handleConfirm} disabled={loading || !reason.trim()} loading={loading}>
            Anular cobro
          </Button>
        </div>
      </div>
    </Modal>
  )
}
