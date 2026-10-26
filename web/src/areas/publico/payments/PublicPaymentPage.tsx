import { useParams } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import { getPublicPayment, METHOD_LABELS } from '../../../lib/api/payments'

const fmtARS = (n: number) =>
  new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 2 }).format(n)

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
      <div className="flex min-h-screen items-center justify-center bg-tc-navy">
        <div className="space-y-2 w-48">
          <div className="h-2 animate-pulse bg-tc-navy-mid" />
          <div className="h-2 w-3/4 animate-pulse bg-tc-navy-mid" />
        </div>
      </div>
    )
  }

  if (error || !data) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center bg-tc-navy px-4 text-center">
        <div className="border border-tc-border bg-white p-8 max-w-sm w-full">
          <p className="text-[11px] font-bold tracking-[0.14em] uppercase text-tc-text-muted mb-2">Error</p>
          <p className="font-bold text-tc-text">Comprobante no encontrado</p>
          <p className="mt-2 text-sm text-tc-text-secondary">El enlace puede haber expirado o ser incorrecto.</p>
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
    <div className="min-h-screen bg-tc-surface">
      <div className="mx-auto max-w-md px-4 py-10">
        <div className="border border-tc-border bg-white overflow-hidden">
          <div className="bg-tc-stub-bg border-b border-tc-border px-6 py-3 flex items-center justify-between">
            <span className="text-[11px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Comprobante de Pago</span>
            {isVoided && (
              <span className="border border-tc-stamp-red px-1.5 py-0.5 text-[11px] font-bold tracking-wider uppercase text-tc-stamp-red">
                Anulado
              </span>
            )}
          </div>

          <div className="px-6 py-5">
            <p className="font-mono font-bold text-2xl text-tc-text mb-4">
              #{String(data.number).padStart(4, '0')}
            </p>

            {isVoided && (
              <div className="mb-4 border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
                Este comprobante ha sido anulado.
              </div>
            )}

            <dl className="space-y-3 text-sm">
              <div className="flex justify-between border-b border-tc-border pb-2">
                <dt className="text-[11px] font-bold tracking-wider uppercase text-tc-text-muted">Importe</dt>
                <dd className="font-bold text-tc-purple tabular-nums text-base">{fmtARS(data.amount)}</dd>
              </div>
              <div className="flex justify-between border-b border-tc-border pb-2">
                <dt className="text-[11px] font-bold tracking-wider uppercase text-tc-text-muted">Método</dt>
                <dd className="text-tc-text-secondary">{METHOD_LABELS[data.method]}</dd>
              </div>
              <div className="flex justify-between border-b border-tc-border pb-2">
                <dt className="text-[11px] font-bold tracking-wider uppercase text-tc-text-muted">Fecha</dt>
                <dd className="text-tc-text-secondary tabular-nums">{paidAt}</dd>
              </div>
              {data.reference && (
                <div className="flex justify-between">
                  <dt className="text-[11px] font-bold tracking-wider uppercase text-tc-text-muted">Referencia</dt>
                  <dd className="text-tc-text-secondary">{data.reference}</dd>
                </div>
              )}
            </dl>
          </div>

          <div className="border-t border-tc-border bg-tc-stub-bg px-6 py-2 text-center">
            <span className="text-xs text-tc-text-muted">TallerControl</span>
          </div>
        </div>
      </div>
    </div>
  )
}
