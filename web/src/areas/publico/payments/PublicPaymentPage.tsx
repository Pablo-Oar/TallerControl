import { useParams } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import { getPublicPayment, METHOD_LABELS, STATUS_LABELS } from '../../../lib/api/payments'

export function PublicPaymentPage() {
  const { token } = useParams<{ token: string }>()

  const { data, isLoading, error } = useQuery({
    queryKey: ['public-payment', token],
    queryFn: () => getPublicPayment(token!),
    enabled: !!token,
    retry: false,
  })

  if (isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-white">
        <div className="h-6 w-6 animate-spin rounded-full border-2 border-blue-600 border-t-transparent" />
      </div>
    )
  }

  if (error || !data) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-white px-4">
        <div className="max-w-sm text-center">
          <p className="text-lg font-medium text-slate-800">Comprobante no encontrado</p>
          <p className="mt-1 text-sm text-slate-500">
            El enlace puede haber expirado o ser incorrecto.
          </p>
        </div>
      </div>
    )
  }

  const isVoided = data.status === 'voided'
  const paidAt = new Date(data.paidAt).toLocaleString('es-AR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })

  return (
    <div className="min-h-screen bg-slate-50">
      <div className="mx-auto max-w-md px-4 py-10">
        <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
          <div className="mb-6 flex items-start justify-between">
            <div>
              <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                Comprobante de Pago
              </p>
              <h1 className="text-2xl font-bold text-slate-800">
                #{String(data.number).padStart(4, '0')}
              </h1>
            </div>
            <span
              className={`rounded-full px-3 py-1 text-sm font-medium ${
                isVoided
                  ? 'bg-red-100 text-red-700'
                  : 'bg-green-100 text-green-700'
              }`}
            >
              {STATUS_LABELS[data.status]}
            </span>
          </div>

          {isVoided && (
            <div className="mb-4 rounded-lg bg-red-50 p-3 text-sm text-red-700">
              Este comprobante ha sido anulado.
            </div>
          )}

          <dl className="space-y-3 text-sm">
            <div className="flex justify-between">
              <dt className="font-medium text-slate-500">Importe</dt>
              <dd className="font-semibold text-slate-800">
                ${data.amount.toLocaleString('es-AR', { minimumFractionDigits: 2 })}
              </dd>
            </div>
            <div className="flex justify-between">
              <dt className="font-medium text-slate-500">Método</dt>
              <dd className="text-slate-800">{METHOD_LABELS[data.method]}</dd>
            </div>
            <div className="flex justify-between">
              <dt className="font-medium text-slate-500">Fecha</dt>
              <dd className="text-slate-800">{paidAt}</dd>
            </div>
            {data.reference && (
              <div className="flex justify-between">
                <dt className="font-medium text-slate-500">Referencia</dt>
                <dd className="text-slate-800">{data.reference}</dd>
              </div>
            )}
          </dl>
        </div>

        <p className="mt-4 text-center text-xs text-slate-400">Generado con TallerControl</p>
      </div>
    </div>
  )
}
