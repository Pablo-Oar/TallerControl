import { useState } from 'react'
import { useParams, useNavigate, Link } from 'react-router'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  quotesApi,
  type ConvertQuoteRequest,
  QUOTE_STATUS_LABELS,
} from '../../../lib/api/quotes'
import { Button } from '../../../components/ui/Button'
import { FormField } from '../../../components/ui/FormField'
import { StatusBadge } from '../../../components/ui/StatusBadge'
import { Timeline } from '../../../components/ui/Timeline'
import { ApiError } from '../../../lib/api/client'
import { QuoteFormModal } from './QuoteFormModal'
import { ConvertQuoteModal } from './ConvertQuoteModal'
import { formatDateOnly } from '../../../lib/dates'

const fmtARS = (n: number) =>
  new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 2 }).format(n)

const KIND_LABELS: Record<string, string> = {
  service: 'Servicio',
  product: 'Producto',
}

const DECISION_LABEL: Record<string, string> = {
  approved: 'Aprobado',
  partially_approved: 'Parcial',
  rejected: 'Rechazado',
}

export function QuoteDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState(false)
  const [confirmCancel, setConfirmCancel] = useState(false)
  const [cancelReason, setCancelReason] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [sendingLink, setSendingLink] = useState(false)
  const [publicLink, setPublicLink] = useState<{ url: string; label: string } | null>(null)

  const { data: quote, isLoading } = useQuery({
    queryKey: ['quote', id],
    queryFn: () => quotesApi.get(id!),
    enabled: !!id,
  })

  const { data: responses } = useQuery({
    queryKey: ['quote-responses', id],
    queryFn: () => quotesApi.getResponses(id!),
    enabled: !!id,
  })

  const qc = queryClient

  const sendMutation = useMutation({
    mutationFn: () => quotesApi.send(id!),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['quote', id] })
      qc.invalidateQueries({ queryKey: ['quotes'] })
      // Doc 05 §2.6: enviar entrega el enlace para compartirlo con el cliente (copiar / WhatsApp).
      void handleQuoteLink()
    },
    onError: (err) => setError(err instanceof ApiError ? err.detail : 'Error al enviar.'),
  })

  const reopenMutation = useMutation({
    mutationFn: () => quotesApi.reopen(id!),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['quote', id] }),
    onError: (err) => setError(err instanceof ApiError ? err.detail : 'Error al reabrir.'),
  })

  const cancelMutation = useMutation({
    mutationFn: (reason: string) => quotesApi.cancel(id!, reason),
    onSuccess: () => {
      setConfirmCancel(false)
      qc.invalidateQueries({ queryKey: ['quote', id] })
      qc.invalidateQueries({ queryKey: ['quotes'] })
    },
    onError: (err) => setError(err instanceof ApiError ? err.detail : 'Error al cancelar.'),
  })

  const deleteMutation = useMutation({
    mutationFn: () => quotesApi.delete(id!),
    onSuccess: () => navigate('/app/quotes'),
    onError: (err) => setError(err instanceof ApiError ? err.detail : 'Error al eliminar.'),
  })

  const [converting, setConverting] = useState(false)
  const convertMutation = useMutation({
    mutationFn: (body: ConvertQuoteRequest) => quotesApi.convert(id!, body),
    onSuccess: order => {
      qc.invalidateQueries({ queryKey: ['quote', id] })
      qc.invalidateQueries({ queryKey: ['quotes'] })
      const orderId = (order as { id?: string } | undefined)?.id
      navigate(orderId ? `/app/work-orders/${orderId}` : '/app/work-orders')
    },
    onError: (err) => setError(err instanceof ApiError ? err.detail : 'Error al convertir.'),
  })

  async function handleQuoteLink() {
    setSendingLink(true)
    try {
      const result = await quotesApi.createPublicLink(id!)
      setPublicLink({ url: `${window.location.origin}/p/${result.tokenBase64Url}`, label: 'Enlace del presupuesto para el cliente' })
    } catch (err) {
      setError(err instanceof ApiError ? err.detail : 'Error al generar el enlace.')
    } finally {
      setSendingLink(false)
    }
  }

  async function handleGetPublicLink() {
    if (!quote?.workOrderId) return
    setSendingLink(true)
    try {
      const result = await quotesApi.getPublicLink(quote.workOrderId)
      setPublicLink({ url: `${window.location.origin}/o/${result.tokenBase64Url}`, label: 'Enlace público de la OT' })
    } catch (err) {
      setError(err instanceof ApiError ? err.detail : 'Error al generar el enlace.')
    } finally {
      setSendingLink(false)
    }
  }

  function handleEditSuccess() {
    setEditing(false)
    qc.invalidateQueries({ queryKey: ['quote', id] })
    qc.invalidateQueries({ queryKey: ['quotes'] })
  }

  if (isLoading) {
    return (
      <div className="flex justify-center py-16">
        <div className="h-6 w-6 animate-spin border-2 border-tc-purple border-t-transparent" />
      </div>
    )
  }

  if (!quote) {
    return <p className="border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">Presupuesto no encontrado.</p>
  }

  const canEdit = quote.status === 'draft'
  const canSend = quote.status === 'draft' && quote.items.length > 0
  const canReopen = ['sent', 'rejected'].includes(quote.status)
  const canConvert = ['approved', 'partially_approved'].includes(quote.status)
  const canCancel = !['converted', 'cancelled'].includes(quote.status)
  const canDelete = quote.status === 'draft'
  const canShare = ['sent', 'approved', 'partially_approved', 'rejected'].includes(quote.status)

  return (
    <div className="space-y-5">
      {/* Encabezado */}
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex flex-wrap items-center gap-3">
          <Link to="/app/quotes" className="text-xs font-bold tracking-wider uppercase text-tc-navy-text hover:text-tc-purple transition-colors">
            ← Presupuestos
          </Link>
          <h1 className="text-lg font-bold tracking-wide text-tc-text uppercase">
            Presupuesto #{quote.number}
          </h1>
          <StatusBadge status={quote.status} label={QUOTE_STATUS_LABELS[quote.status]} />
        </div>
      </div>

      {error && (
        <div className="border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 flex items-center justify-between">
          {error}
          <button onClick={() => setError(null)} className="ml-3 text-xs underline">Cerrar</button>
        </div>
      )}

      {/* Datos principales */}
      <div className="border border-tc-border bg-white overflow-hidden">
        <div className="bg-tc-stub-bg border-b border-tc-border px-4 py-2">
          <span className="text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Datos del presupuesto</span>
        </div>
        <div className="grid grid-cols-2 gap-0 sm:grid-cols-3 divide-x divide-tc-border">
          <DataCell label="Cliente">{quote.customerName}</DataCell>
          <DataCell label="Vehículo">
            <span className="font-mono font-bold tracking-wider">{quote.vehiclePlate}</span>
            {(quote.vehicleBrand || quote.vehicleModel) && (
              <span className="ml-1 text-tc-text-secondary">— {[quote.vehicleBrand, quote.vehicleModel].filter(Boolean).join(' ')}</span>
            )}
          </DataCell>
          <DataCell label="Válido hasta">
            {formatDateOnly(quote.validUntil)}
          </DataCell>
          {quote.globalDiscountPct > 0 && (
            <DataCell label="Descuento">{quote.globalDiscountPct}%</DataCell>
          )}
          {quote.workOrderId && (
            <DataCell label="OT generada">
              <Link to={`/app/work-orders/${quote.workOrderId}`} className="text-tc-purple hover:text-tc-purple-mid font-medium transition-colors">
                Ver OT →
              </Link>
            </DataCell>
          )}
        </div>
        {(quote.customerNotes || quote.internalNotes) && (
          <div className="border-t border-tc-perf p-4 space-y-3">
            {quote.customerNotes && (
              <div>
                <p className="mb-1 text-[10px] font-bold tracking-wider uppercase text-tc-text-muted">Notas para el cliente</p>
                <p className="text-sm text-tc-text-secondary">{quote.customerNotes}</p>
              </div>
            )}
            {quote.internalNotes && (
              <div>
                <p className="mb-1 text-[10px] font-bold tracking-wider uppercase text-tc-text-muted">Notas internas</p>
                <p className="text-sm text-tc-text-secondary italic">{quote.internalNotes}</p>
              </div>
            )}
          </div>
        )}
      </div>

      {/* Ítems */}
      <div className="border border-tc-border bg-white overflow-hidden">
        <div className="bg-tc-stub-bg border-b border-tc-border px-4 py-2">
          <span className="text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Ítems</span>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-tc-border">
                {['Descripción', 'Tipo', 'Cant.', 'Precio neto', 'IVA%', 'Total'].map((h, i) => (
                  <th key={i} className={`px-4 py-2 text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted ${i > 1 ? 'text-right' : 'text-left'}`}>
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-tc-border">
              {quote.items.length === 0 ? (
                <tr>
                  <td colSpan={6} className="px-4 py-6 text-center text-sm text-tc-text-muted">Sin ítems</td>
                </tr>
              ) : (
                quote.items.map(item => (
                  <tr key={item.id}>
                    <td className="px-4 py-2.5 text-tc-text">{item.description}</td>
                    <td className="px-4 py-2.5 text-xs text-tc-text-secondary">{KIND_LABELS[item.kind] ?? item.kind}</td>
                    <td className="px-4 py-2.5 text-right tabular-nums text-tc-text-secondary">{item.quantity}</td>
                    <td className="px-4 py-2.5 text-right tabular-nums text-tc-purple">{fmtARS(item.unitNetPrice)}</td>
                    <td className="px-4 py-2.5 text-right text-tc-text-secondary">{item.vatRate}%</td>
                    <td className="px-4 py-2.5 text-right tabular-nums font-semibold text-tc-purple">{fmtARS(item.lineTotal)}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
        {/* Totales */}
        <div className="border-t border-tc-perf px-4 py-3 space-y-1 text-sm">
          {quote.globalDiscountPct > 0 && (
            <>
              <TotalRow label="Subtotal">{fmtARS(quote.subtotalNet)}</TotalRow>
              <TotalRow label={`Descuento (${quote.globalDiscountPct}%)`}>
                <span className="text-tc-stamp-red">−{fmtARS(quote.discountAmount)}</span>
              </TotalRow>
            </>
          )}
          <TotalRow label="IVA">{fmtARS(quote.vatAmount)}</TotalRow>
          <TotalRow label="Total" bold>
            <span className="text-tc-purple">{fmtARS(quote.total)}</span>
          </TotalRow>
          {quote.approvedTotal !== null && (
            <TotalRow label="Total aprobado">
              <span className="text-tc-done-text font-bold">{fmtARS(quote.approvedTotal)}</span>
            </TotalRow>
          )}
        </div>
      </div>

      {/* Acciones */}
      <div className="flex flex-wrap gap-2">
        {canEdit && (
          <Button variant="secondary" size="sm" onClick={() => setEditing(true)}>Editar</Button>
        )}
        {canSend && (
          <Button size="sm" onClick={() => sendMutation.mutate()} loading={sendMutation.isPending}>
            Enviar al cliente
          </Button>
        )}
        {canReopen && (
          <Button variant="secondary" size="sm" onClick={() => reopenMutation.mutate()} loading={reopenMutation.isPending}>
            Reabrir
          </Button>
        )}
        {canConvert && (
          <Button size="sm" onClick={() => { setError(null); setConverting(true) }}>
            Convertir a OT
          </Button>
        )}
        {canShare && (
          <Button variant="secondary" size="sm" onClick={handleQuoteLink} loading={sendingLink}>
            Enlace para el cliente
          </Button>
        )}
        {quote.workOrderId && (
          <Button variant="secondary" size="sm" onClick={handleGetPublicLink} loading={sendingLink}>
            Enlace OT
          </Button>
        )}
        <a
          href={quotesApi.getPdfUrl(quote.id)}
          target="_blank"
          rel="noreferrer"
          className="inline-flex items-center border border-tc-border bg-white px-3 py-1.5 text-xs font-bold tracking-wider uppercase text-tc-text-secondary hover:bg-tc-surface transition-colors"
        >
          PDF
        </a>
        {canCancel && !confirmCancel && (
          <Button variant="ghost" size="sm" onClick={() => setConfirmCancel(true)}>Cancelar</Button>
        )}
        {canDelete && (
          <Button variant="danger" size="sm" onClick={() => deleteMutation.mutate()} loading={deleteMutation.isPending}>
            Eliminar
          </Button>
        )}
      </div>

      {/* Enlace público (presupuesto para el cliente, u OT) */}
      {publicLink && (
        <div className="border border-tc-border bg-tc-stub-bg px-4 py-3">
          <p className="mb-2 text-[10px] font-bold tracking-wider uppercase text-tc-text-muted">{publicLink.label}</p>
          <div className="flex flex-wrap items-end gap-2">
            <FormField label="Enlace" size="sm" readOnly value={publicLink.url} className="min-w-0 flex-1" />
            <Button variant="secondary" size="sm" onClick={() => navigator.clipboard.writeText(publicLink.url)}>
              Copiar
            </Button>
            <a
              href={`https://wa.me/?text=${encodeURIComponent(`Presupuesto #${quote.number}: ${publicLink.url}`)}`}
              target="_blank"
              rel="noreferrer"
              className="inline-flex items-center border border-tc-border bg-white px-3 py-1.5 text-xs font-bold tracking-wider uppercase text-tc-text-secondary hover:bg-tc-surface transition-colors"
            >
              WhatsApp
            </a>
          </div>
        </div>
      )}

      {/* Confirmación de cancelación */}
      {confirmCancel && (
        <div className="border border-tc-border bg-white overflow-hidden">
          <div className="bg-tc-stub-bg border-b border-tc-border px-4 py-2">
            <span className="text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Cancelar presupuesto</span>
          </div>
          <div className="p-4 space-y-3">
            <input
              value={cancelReason}
              onChange={e => setCancelReason(e.target.value)}
              className="w-full border border-tc-border bg-white px-3 py-2 text-sm outline-none focus:border-tc-border-mid transition-colors"
              placeholder="Motivo de cancelación (obligatorio)"
            />
            <div className="flex gap-2">
              <Button
                size="sm"
                variant="danger"
                onClick={() => cancelMutation.mutate(cancelReason)}
                loading={cancelMutation.isPending}
                disabled={!cancelReason.trim()}
              >
                Confirmar cancelación
              </Button>
              <Button size="sm" variant="secondary" onClick={() => setConfirmCancel(false)}>
                No cancelar
              </Button>
            </div>
          </div>
        </div>
      )}

      {/* Respuestas del cliente */}
      {responses && responses.length > 0 && (
        <div className="border border-tc-border bg-white overflow-hidden">
          <div className="bg-tc-stub-bg border-b border-tc-border px-4 py-2">
            <span className="text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Respuestas del cliente</span>
          </div>
          <div className="px-4 py-4">
            <Timeline
              label="Respuestas del cliente"
              items={responses.map(r => ({
                id: r.id,
                date: r.createdAt,
                title: r.signerName,
                kind: 'response' as const,
                emphasis: r.isCurrent,
                detail: r.comment,
                badge: (
                  <>
                    <StatusBadge status={r.decision} label={DECISION_LABEL[r.decision] ?? r.decision} />
                    {r.isCurrent && (
                      <span className="text-[10px] font-bold tracking-wider uppercase text-tc-done-text">Vigente</span>
                    )}
                  </>
                ),
              }))}
            />
          </div>
        </div>
      )}

      {editing && quote && (
        <QuoteFormModal quote={quote} onSuccess={handleEditSuccess} onClose={() => setEditing(false)} />
      )}

      {converting && quote && (
        <ConvertQuoteModal
          vehicleId={quote.vehicleId}
          vehiclePlate={quote.vehiclePlate}
          busy={convertMutation.isPending}
          error={error}
          onConfirm={body => convertMutation.mutate(body)}
          onClose={() => setConverting(false)}
        />
      )}
    </div>
  )
}

// ── Helpers ────────────────────────────────────────────────────────────────────

function DataCell({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="px-4 py-3">
      <p className="mb-0.5 text-[10px] font-bold tracking-wider uppercase text-tc-text-muted">{label}</p>
      <p className="text-sm text-tc-text">{children}</p>
    </div>
  )
}

function TotalRow({ label, children, bold }: { label: string; children: React.ReactNode; bold?: boolean }) {
  return (
    <div className={`flex items-baseline justify-end gap-8 ${bold ? 'font-bold text-base' : 'text-tc-text-secondary'}`}>
      <span className={bold ? 'text-tc-text' : 'text-tc-text-secondary'}>{label}</span>
      <span className="tabular-nums">{children}</span>
    </div>
  )
}
