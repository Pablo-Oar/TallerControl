import { useState } from 'react'
import { useParams, useNavigate, Link } from 'react-router'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  quotesApi,
  type ConvertQuoteRequest,
  QUOTE_STATUS_LABELS,
  QUOTE_STATUS_COLORS,
} from '../../../lib/api/quotes'
import { Button } from '../../../components/ui/Button'
import { ApiError } from '../../../lib/api/client'
import { QuoteFormModal } from './QuoteFormModal'

export function QuoteDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState(false)
  const [confirmCancel, setConfirmCancel] = useState(false)
  const [cancelReason, setCancelReason] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [sendingLink, setSendingLink] = useState(false)
  const [publicLink, setPublicLink] = useState<string | null>(null)

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
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['quote', id] }); qc.invalidateQueries({ queryKey: ['quotes'] }) },
    onError: (err) => setError(err instanceof ApiError ? err.detail : 'Error al enviar.'),
  })

  const reopenMutation = useMutation({
    mutationFn: () => quotesApi.reopen(id!),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['quote', id] }) },
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

  const convertMutation = useMutation({
    mutationFn: (body: ConvertQuoteRequest) => quotesApi.convert(id!, body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['quote', id] })
      qc.invalidateQueries({ queryKey: ['quotes'] })
      navigate('/app/work-orders')
    },
    onError: (err) => setError(err instanceof ApiError ? err.detail : 'Error al convertir.'),
  })

  async function handleGetPublicLink() {
    if (!quote?.workOrderId) return
    setSendingLink(true)
    try {
      const result = await quotesApi.getPublicLink(quote.workOrderId)
      const url = `${window.location.origin}/o/${result.tokenBase64Url}`
      setPublicLink(url)
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
        <div className="h-6 w-6 animate-spin rounded-full border-2 border-blue-600 border-t-transparent" />
      </div>
    )
  }

  if (!quote) {
    return <p className="text-sm text-red-600 py-8">Presupuesto no encontrado.</p>
  }

  const canEdit = quote.status === 'draft'
  const canSend = quote.status === 'draft' && quote.items.length > 0
  const canReopen = ['sent', 'rejected'].includes(quote.status)
  const canConvert = ['approved', 'partially_approved'].includes(quote.status)
  const canCancel = !['converted', 'cancelled'].includes(quote.status)
  const canDelete = quote.status === 'draft'

  return (
    <div className="space-y-6">
      {/* Encabezado */}
      <div className="flex items-start justify-between">
        <div>
          <Link to="/app/quotes" className="text-sm text-blue-600 hover:underline">
            ← Presupuestos
          </Link>
          <h1 className="mt-1 text-2xl font-semibold text-slate-800">
            Presupuesto #{quote.number}
          </h1>
        </div>
        <span className={`rounded-full px-3 py-1 text-sm font-medium ${QUOTE_STATUS_COLORS[quote.status]}`}>
          {QUOTE_STATUS_LABELS[quote.status]}
        </span>
      </div>

      {error && (
        <div className="rounded-md bg-red-50 px-4 py-3 text-sm text-red-700">
          {error}
          <button onClick={() => setError(null)} className="ml-2 underline">Cerrar</button>
        </div>
      )}

      {/* Datos principales */}
      <div className="rounded-lg border border-slate-200 bg-white p-5">
        <dl className="grid grid-cols-2 gap-4 text-sm sm:grid-cols-3">
          <div>
            <dt className="font-medium text-slate-500">Cliente</dt>
            <dd className="mt-1 text-slate-800">{quote.customerName}</dd>
          </div>
          <div>
            <dt className="font-medium text-slate-500">Vehículo</dt>
            <dd className="mt-1 text-slate-800">
              {[quote.vehicleBrand, quote.vehicleModel, quote.vehiclePlate].filter(Boolean).join(' · ')}
            </dd>
          </div>
          <div>
            <dt className="font-medium text-slate-500">Válido hasta</dt>
            <dd className="mt-1 text-slate-800">
              {new Date(quote.validUntil).toLocaleDateString('es-AR')}
            </dd>
          </div>
          {quote.globalDiscountPct > 0 && (
            <div>
              <dt className="font-medium text-slate-500">Descuento</dt>
              <dd className="mt-1 text-slate-800">{quote.globalDiscountPct}%</dd>
            </div>
          )}
          {quote.workOrderId && (
            <div>
              <dt className="font-medium text-slate-500">OT generada</dt>
              <dd className="mt-1">
                <Link to={`/app/work-orders/${quote.workOrderId}`} className="text-blue-600 hover:underline">
                  Ver OT
                </Link>
              </dd>
            </div>
          )}
        </dl>
        {quote.customerNotes && (
          <div className="mt-4 text-sm">
            <p className="font-medium text-slate-500">Notas para el cliente</p>
            <p className="mt-1 text-slate-700">{quote.customerNotes}</p>
          </div>
        )}
        {quote.internalNotes && (
          <div className="mt-3 text-sm">
            <p className="font-medium text-slate-500">Notas internas</p>
            <p className="mt-1 text-slate-600 italic">{quote.internalNotes}</p>
          </div>
        )}
      </div>

      {/* Ítems */}
      <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
        <table className="w-full text-sm">
          <thead className="bg-slate-50 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">
            <tr>
              <th className="px-4 py-3">Descripción</th>
              <th className="px-4 py-3">Tipo</th>
              <th className="px-4 py-3 text-right">Cant.</th>
              <th className="px-4 py-3 text-right">Precio neto</th>
              <th className="px-4 py-3 text-right">IVA%</th>
              <th className="px-4 py-3 text-right">Total</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {quote.items.length === 0 ? (
              <tr>
                <td colSpan={6} className="px-4 py-6 text-center text-slate-400">Sin ítems</td>
              </tr>
            ) : (
              quote.items.map(item => (
                <tr key={item.id}>
                  <td className="px-4 py-3 text-slate-800">{item.description}</td>
                  <td className="px-4 py-3 text-slate-500 capitalize">{item.kind}</td>
                  <td className="px-4 py-3 text-right text-slate-600">{item.quantity}</td>
                  <td className="px-4 py-3 text-right text-slate-600">
                    ${item.unitNetPrice.toLocaleString('es-AR', { minimumFractionDigits: 2 })}
                  </td>
                  <td className="px-4 py-3 text-right text-slate-500">{item.vatRate}%</td>
                  <td className="px-4 py-3 text-right font-medium text-slate-800">
                    ${item.lineTotal.toLocaleString('es-AR', { minimumFractionDigits: 2 })}
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>

        {/* Totales */}
        <div className="border-t border-slate-100 px-4 py-3 space-y-1 text-sm text-right">
          {quote.globalDiscountPct > 0 && (
            <>
              <div className="flex justify-end gap-8 text-slate-500">
                <span>Subtotal</span>
                <span>${quote.subtotalNet.toLocaleString('es-AR', { minimumFractionDigits: 2 })}</span>
              </div>
              <div className="flex justify-end gap-8 text-slate-500">
                <span>Descuento ({quote.globalDiscountPct}%)</span>
                <span>−${quote.discountAmount.toLocaleString('es-AR', { minimumFractionDigits: 2 })}</span>
              </div>
            </>
          )}
          <div className="flex justify-end gap-8 text-slate-500">
            <span>IVA</span>
            <span>${quote.vatAmount.toLocaleString('es-AR', { minimumFractionDigits: 2 })}</span>
          </div>
          <div className="flex justify-end gap-8 text-base font-bold text-slate-800">
            <span>Total</span>
            <span>${quote.total.toLocaleString('es-AR', { minimumFractionDigits: 2 })}</span>
          </div>
          {quote.approvedTotal !== null && (
            <div className="flex justify-end gap-8 text-green-700 font-semibold">
              <span>Total aprobado</span>
              <span>${quote.approvedTotal.toLocaleString('es-AR', { minimumFractionDigits: 2 })}</span>
            </div>
          )}
        </div>
      </div>

      {/* Acciones */}
      <div className="flex flex-wrap gap-2">
        {canEdit && (
          <Button variant="secondary" onClick={() => setEditing(true)}>Editar</Button>
        )}
        {canSend && (
          <Button onClick={() => sendMutation.mutate()} loading={sendMutation.isPending}>
            Enviar al cliente
          </Button>
        )}
        {canReopen && (
          <Button variant="secondary" onClick={() => reopenMutation.mutate()} loading={reopenMutation.isPending}>
            Reabrir
          </Button>
        )}
        {canConvert && (
          <Button
            onClick={() => convertMutation.mutate({ odometerKm: 0 })}
            loading={convertMutation.isPending}
          >
            Convertir a OT
          </Button>
        )}
        {quote.workOrderId && (
          <Button variant="secondary" onClick={handleGetPublicLink} loading={sendingLink}>
            Enlace público OT
          </Button>
        )}
        <a
          href={quotesApi.getPdfUrl(quote.id)}
          target="_blank"
          rel="noreferrer"
          className="inline-flex items-center rounded-md border border-slate-300 bg-white px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
        >
          Descargar PDF
        </a>
        {canCancel && !confirmCancel && (
          <Button variant="secondary" onClick={() => setConfirmCancel(true)}>
            Cancelar
          </Button>
        )}
        {canDelete && (
          <Button
            variant="secondary"
            onClick={() => deleteMutation.mutate()}
            loading={deleteMutation.isPending}
          >
            Eliminar
          </Button>
        )}
      </div>

      {/* Enlace público copiado */}
      {publicLink && (
        <div className="rounded-md border border-slate-200 bg-slate-50 px-4 py-3 text-sm">
          <p className="text-slate-600 mb-1">Enlace público de la OT:</p>
          <div className="flex gap-2">
            <input
              readOnly
              value={publicLink}
              className="flex-1 rounded border border-slate-300 px-2 py-1 text-xs text-slate-700 bg-white"
            />
            <button
              onClick={() => navigator.clipboard.writeText(publicLink)}
              className="rounded border border-slate-300 px-2 py-1 text-xs hover:bg-slate-100"
            >
              Copiar
            </button>
          </div>
        </div>
      )}

      {/* Confirmación de cancelación */}
      {confirmCancel && (
        <div className="rounded-lg border border-slate-200 bg-white p-4">
          <p className="text-sm font-medium text-slate-700 mb-2">Motivo de cancelación</p>
          <input
            value={cancelReason}
            onChange={e => setCancelReason(e.target.value)}
            className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-blue-500"
            placeholder="Motivo (obligatorio)"
          />
          <div className="mt-3 flex gap-2">
            <Button
              onClick={() => cancelMutation.mutate(cancelReason)}
              loading={cancelMutation.isPending}
              disabled={!cancelReason.trim()}
            >
              Confirmar cancelación
            </Button>
            <Button variant="secondary" onClick={() => setConfirmCancel(false)}>
              No cancelar
            </Button>
          </div>
        </div>
      )}

      {/* Respuestas del cliente */}
      {responses && responses.length > 0 && (
        <div className="rounded-lg border border-slate-200 bg-white p-5">
          <h2 className="text-sm font-semibold text-slate-700 mb-3">Respuestas del cliente</h2>
          <div className="space-y-3">
            {responses.map(r => (
              <div key={r.id} className={`rounded-md p-3 text-sm ${r.isCurrent ? 'bg-green-50 border border-green-200' : 'bg-slate-50'}`}>
                <div className="flex items-center justify-between">
                  <span className="font-medium text-slate-800">{r.signerName}</span>
                  <span className={`font-medium ${r.decision === 'approved' || r.decision === 'partially_approved' ? 'text-green-700' : 'text-red-600'}`}>
                    {r.decision === 'approved' ? 'Aprobado' : r.decision === 'partially_approved' ? 'Aprobado parcialmente' : 'Rechazado'}
                  </span>
                </div>
                {r.comment && <p className="mt-1 text-slate-600">{r.comment}</p>}
                <p className="mt-1 text-xs text-slate-400">
                  {new Date(r.createdAt).toLocaleString('es-AR')}
                  {r.isCurrent && <span className="ml-2 text-green-600 font-medium">· vigente</span>}
                </p>
              </div>
            ))}
          </div>
        </div>
      )}

      {editing && quote && (
        <QuoteFormModal
          quote={quote}
          onSuccess={handleEditSuccess}
          onClose={() => setEditing(false)}
        />
      )}
    </div>
  )
}
