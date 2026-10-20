import { useState, useRef } from 'react'
import { useParams } from 'react-router'
import { useQuery, useMutation } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { publicApi, type SubmitPublicResponseRequest, type SubmitPublicResponseResult, QUOTE_STATUS_LABELS } from '../../../lib/api/quotes'
import { ApiError } from '../../../lib/api/client'
import { Button } from '../../../components/ui/Button'
import { FormField, FormTextarea } from '../../../components/ui/FormField'
import { SignaturePad, type SignaturePadHandle } from '../../../components/ui/SignaturePad'
import { StatusBadge } from '../../../components/ui/StatusBadge'
import { labelCls } from '../../../lib/styles'


const fmtARS = (n: number) =>
  new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 2 }).format(n)

const schema = z.object({
  decision: z.enum(['approved', 'rejected']),
  signerName: z.string().trim().min(2, 'Ingrese su nombre completo').max(200),
  comment: z.string().trim().max(500).nullable(),
})

type FormData = z.infer<typeof schema>

export function PublicQuotePage() {
  const { token } = useParams<{ token: string }>()
  const [submitted, setSubmitted] = useState(false)
  const [result, setResult] = useState<SubmitPublicResponseResult | null>(null)
  // Renglones opcionales que el cliente destildó (REQ-10); los obligatorios no se pueden quitar.
  const [excluded, setExcluded] = useState<Set<string>>(new Set())
  const [serverError, setServerError] = useState<string | null>(null)
  const [signerDocNumber, setSignerDocNumber] = useState('')
  const sigRef = useRef<SignaturePadHandle>(null)

  const { data, isLoading, error } = useQuery({
    queryKey: ['public-quote', token],
    queryFn: () => publicApi.getQuote(token!),
    enabled: !!token,
    retry: false,
  })

  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { decision: 'approved', signerName: '', comment: null },
  })

  const submitMutation = useMutation({
    mutationFn: (body: SubmitPublicResponseRequest) => publicApi.submitResponse(token!, body),
    onSuccess: (r) => { setResult(r); setSubmitted(true) },
    onError: (err) => {
      setServerError(err instanceof ApiError ? err.detail : 'Error al enviar la respuesta.')
    },
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
          <p className="text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted mb-2">Error</p>
          <p className="font-bold text-tc-text">Presupuesto no encontrado</p>
          <p className="mt-2 text-sm text-tc-text-secondary">El enlace puede haber expirado o ser incorrecto.</p>
        </div>
      </div>
    )
  }

  const canRespond = data.status === 'sent'
  const isExpired = new Date(data.validUntil) < new Date()

  if (submitted) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-tc-surface px-4">
        <div className="border border-tc-border bg-white p-10 max-w-sm w-full text-center">
          <div className="bg-tc-stub-bg border border-tc-border w-10 h-10 flex items-center justify-center mx-auto mb-4 text-lg font-bold text-tc-text-secondary">
            ✓
          </div>
          <p className="font-bold text-tc-text">Respuesta enviada</p>
          <p className="mt-2 text-sm text-tc-text-secondary">Tu respuesta fue registrada correctamente.</p>
          {result?.decision === 'approved' && (
            <p className="mt-3 text-sm text-tc-text-secondary">
              Total aprobado: <strong className="tabular-nums">{fmtARS(result.approvedTotal)}</strong>
            </p>
          )}
        </div>
      </div>
    )
  }

  async function onSubmit(values: FormData) {
    if (!data) return
    let sigKey: string | null = null
    const blob = await sigRef.current?.toBlob()
    if (blob) {
      try {
        const urlResp = await publicApi.getSignatureUploadUrl(token!, {
          contentType: 'image/png',
          size: blob.size,
        })
        await fetch(urlResp.uploadUrl, {
          method: 'PUT',
          body: blob,
          headers: { 'Content-Type': 'image/png' },
        })
        sigKey = urlResp.fileKey
      } catch {
        // Si falla la subida de firma, continuar sin ella
      }
    }
    const body: SubmitPublicResponseRequest = {
      decision: values.decision,
      signerName: values.signerName,
      contentHash: data.contentHash,
      comment: values.comment || null,
      items: data.items.map(item => ({
        quoteItemId: item.id,
        approved: values.decision === 'approved' && !excluded.has(item.id),
      })),
      signatureFileKey: sigKey,
      signerDocNumber: signerDocNumber.trim() || null,
    }
    submitMutation.mutate(body)
  }

  return (
    <div className="min-h-screen bg-tc-surface">
      <div className="mx-auto max-w-2xl px-4 py-10 space-y-5">

        {/* Encabezado del presupuesto */}
        <div className="border border-tc-border bg-white overflow-hidden">
          <div className="bg-tc-stub-bg border-b border-tc-border px-6 py-3 flex items-center justify-between">
            <span className="text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Presupuesto</span>
            <StatusBadge status={data.status} label={QUOTE_STATUS_LABELS[data.status] ?? data.status} />
          </div>
          <div className="px-6 py-5">
            <p className="font-mono font-bold text-2xl text-tc-text mb-4">#{data.number}</p>
            <dl className="grid grid-cols-2 gap-4 text-sm">
              <div>
                <dt className="text-[10px] font-bold tracking-wider uppercase text-tc-text-muted mb-1">Cliente</dt>
                <dd className="text-tc-text">{data.customerName}</dd>
              </div>
              <div>
                <dt className="text-[10px] font-bold tracking-wider uppercase text-tc-text-muted mb-1">Vehículo</dt>
                <dd className="text-tc-text font-mono">
                  {[data.vehicleBrand, data.vehicleModel, data.vehiclePlate].filter(Boolean).join(' · ')}
                </dd>
              </div>
              <div>
                <dt className="text-[10px] font-bold tracking-wider uppercase text-tc-text-muted mb-1">Válido hasta</dt>
                <dd className={isExpired ? 'text-tc-stamp-red font-bold' : 'text-tc-text'}>
                  {new Date(data.validUntil).toLocaleDateString('es-AR')}
                  {isExpired && ' — vencido'}
                </dd>
              </div>
            </dl>
            {data.customerNotes && (
              <div className="mt-4 border-t border-tc-border pt-3 text-sm text-tc-text-secondary">
                <p className="text-[10px] font-bold tracking-wider uppercase text-tc-text-muted mb-1">Notas</p>
                {data.customerNotes}
              </div>
            )}
          </div>
        </div>

        {/* Ítems del presupuesto */}
        <div className="border border-tc-border bg-white overflow-hidden">
          <div className="bg-tc-stub-bg border-b border-tc-border px-4 py-2">
            <span className="text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Ítems</span>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-tc-border">
                  <th className="px-3 py-2 sm:px-4 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Descripción</th>
                  <th className="hidden px-4 py-2 text-right text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted sm:table-cell">Cant.</th>
                  <th className="hidden px-4 py-2 text-right text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted sm:table-cell">Precio unit.</th>
                  <th className="px-3 py-2 sm:px-4 text-right text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Subtotal</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-tc-border">
                {data.items.map(item => (
                  <tr key={item.id} className="hover:bg-tc-surface transition-colors">
                    <td className="px-3 py-3 sm:px-4 text-tc-text">
                      {item.description}
                      {canRespond && !isExpired && (
                        item.requiredForApproval ? (
                          <span className="ml-2 text-[10px] font-bold uppercase tracking-wider text-tc-text-muted">Obligatorio</span>
                        ) : (
                          <label className="mt-1 flex min-h-[44px] cursor-pointer items-center gap-2 text-xs text-tc-text-secondary">
                            <input
                              type="checkbox"
                              className="h-4 w-4 accent-tc-purple"
                              checked={!excluded.has(item.id)}
                              onChange={e =>
                                setExcluded(prev => {
                                  const next = new Set(prev)
                                  if (e.target.checked) next.delete(item.id)
                                  else next.add(item.id)
                                  return next
                                })
                              }
                            />
                            Incluir en la aprobación
                          </label>
                        )
                      )}
                      <span className="block text-xs tabular-nums text-tc-text-secondary sm:hidden">
                        {item.quantity} × {fmtARS(item.unitNetPrice)}
                      </span>
                    </td>
                    <td className="hidden px-4 py-3 text-right tabular-nums text-tc-text-secondary sm:table-cell">{item.quantity}</td>
                    <td className="hidden px-4 py-3 text-right tabular-nums text-tc-text-secondary sm:table-cell">{fmtARS(item.unitNetPrice)}</td>
                    <td className="px-3 py-3 sm:px-4 text-right tabular-nums font-bold text-tc-text">{fmtARS(item.lineTotal)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="border-t border-tc-border px-4 py-3 space-y-1 text-sm bg-tc-stub-bg">
            {data.globalDiscountPct > 0 && (
              <>
                <div className="flex justify-between text-tc-text-secondary">
                  <span className="text-xs font-bold tracking-wider uppercase">Subtotal</span>
                  <span className="tabular-nums">{fmtARS(data.subtotalNet)}</span>
                </div>
                <div className="flex justify-between text-tc-text-secondary">
                  <span className="text-xs font-bold tracking-wider uppercase">Descuento ({data.globalDiscountPct}%)</span>
                  <span className="tabular-nums text-tc-stamp-red">−{fmtARS(data.discountAmount)}</span>
                </div>
              </>
            )}
            <div className="flex justify-between text-tc-text-secondary">
              <span className="text-xs font-bold tracking-wider uppercase">IVA</span>
              <span className="tabular-nums">{fmtARS(data.vatAmount)}</span>
            </div>
            <div className="flex justify-between border-t border-tc-border pt-2 text-base font-bold text-tc-text">
              <span className="text-xs font-bold tracking-wider uppercase">Total</span>
              <span className="tabular-nums text-tc-purple">{fmtARS(data.total)}</span>
            </div>
          </div>
        </div>

        {/* Formulario de respuesta */}
        {canRespond && !isExpired && (
          <div className="border border-tc-border bg-white overflow-hidden">
            <div className="bg-tc-stub-bg border-b border-tc-border px-6 py-3">
              <span className="text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Su respuesta</span>
            </div>
            <div className="px-6 py-5">
              <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
                <FormField
                  label="Nombre completo"
                  requiredMark
                  placeholder="Juan García"
                  error={errors.signerName?.message}
                  {...register('signerName')}
                />

                <fieldset>
                  <legend className={labelCls}>Decisión</legend>
                  <div className="flex gap-6 mt-1">
                    <label className="flex items-center gap-2 cursor-pointer">
                      <input type="radio" value="approved" {...register('decision')} className="accent-tc-purple" />
                      <span className="text-sm font-bold tracking-wide text-green-700">Apruebo</span>
                    </label>
                    <label className="flex items-center gap-2 cursor-pointer">
                      <input type="radio" value="rejected" {...register('decision')} className="accent-tc-purple" />
                      <span className="text-sm font-bold tracking-wide text-tc-stamp-red">Rechazo</span>
                    </label>
                  </div>
                </fieldset>

                <FormTextarea
                  label="Comentario (opcional)"
                  rows={3}
                  placeholder="Puede agregar observaciones..."
                  {...register('comment')}
                />

                {/* Firma digital */}
                <div>
                  <p className={labelCls}>Firma <span className="font-normal normal-case text-tc-text-muted">(opcional)</span></p>
                  <SignaturePad ref={sigRef} />
                </div>

                {/* DNI */}
                <FormField
                  label="DNI / Documento (opcional)"
                  type="text"
                  value={signerDocNumber}
                  onChange={e => setSignerDocNumber(e.target.value)}
                  placeholder="Ej: 30123456"
                  maxLength={20}
                  inputClassName="tabular-nums"
                  hint="Su documento se almacena cifrado y no es visible para terceros."
                />

                {serverError && (
                  <div className="border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
                    {serverError}
                  </div>
                )}

                <Button type="submit" loading={isSubmitting} className="w-full">
                  Enviar respuesta
                </Button>
              </form>
            </div>
          </div>
        )}

        {canRespond && isExpired && (
          <div className="border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
            Este presupuesto venció el {new Date(data.validUntil).toLocaleDateString('es-AR')}.
            Contacte al taller para obtener un nuevo presupuesto.
          </div>
        )}

        {!canRespond && data.status !== 'sent' && (
          <div className="border border-tc-border bg-white px-4 py-4 text-center text-sm text-tc-text-secondary">
            Este presupuesto está en estado{' '}
            <span className="font-bold text-tc-text-secondary">{QUOTE_STATUS_LABELS[data.status]}</span>{' '}
            y ya no acepta respuestas.
          </div>
        )}
      </div>
    </div>
  )
}
