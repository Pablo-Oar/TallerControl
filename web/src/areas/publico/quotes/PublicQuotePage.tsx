import { useState, useRef, useEffect } from 'react'
import { useParams } from 'react-router'
import { useQuery, useMutation } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { publicApi, type SubmitPublicResponseRequest, QUOTE_STATUS_LABELS } from '../../../lib/api/quotes'
import { ApiError } from '../../../lib/api/client'
import { Button } from '../../../components/ui/Button'

const inputCls =
  'w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-blue-500'

const schema = z.object({
  decision: z.enum(['approved', 'rejected']),
  signerName: z.string().trim().min(2, 'Ingrese su nombre completo').max(200),
  comment: z.string().trim().max(500).nullable(),
})

type FormData = z.infer<typeof schema>

export function PublicQuotePage() {
  const { token } = useParams<{ token: string }>()
  const [submitted, setSubmitted] = useState(false)
  const [serverError, setServerError] = useState<string | null>(null)
  const [signerDocNumber, setSignerDocNumber] = useState('')
  const canvasRef = useRef<HTMLCanvasElement>(null)
  const [isDrawing, setIsDrawing] = useState(false)
  const [hasSig, setHasSig] = useState(false)

  // Inicializa el canvas con fondo blanco al montar.
  useEffect(() => {
    const canvas = canvasRef.current
    if (!canvas) return
    const ctx = canvas.getContext('2d')
    if (!ctx) return
    ctx.fillStyle = '#fff'
    ctx.fillRect(0, 0, canvas.width, canvas.height)
  }, [])

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
    onSuccess: () => setSubmitted(true),
    onError: (err) => {
      setServerError(err instanceof ApiError ? err.detail : 'Error al enviar la respuesta.')
    },
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
          <p className="text-lg font-medium text-slate-800">Presupuesto no encontrado</p>
          <p className="mt-1 text-sm text-slate-500">
            El enlace puede haber expirado o ser incorrecto.
          </p>
        </div>
      </div>
    )
  }

  const canRespond = data.status === 'sent'
  const isExpired = new Date(data.validUntil) < new Date()

  if (submitted) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-white px-4">
        <div className="max-w-sm text-center">
          <div className="mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-full bg-green-100">
            <span className="text-2xl">✓</span>
          </div>
          <p className="text-lg font-semibold text-slate-800">Respuesta enviada</p>
          <p className="mt-1 text-sm text-slate-500">
            Tu respuesta fue registrada correctamente.
          </p>
        </div>
      </div>
    )
  }

  function getCanvasPos(canvas: HTMLCanvasElement, e: React.MouseEvent | React.TouchEvent) {
    const rect = canvas.getBoundingClientRect()
    const scaleX = canvas.width / rect.width
    const scaleY = canvas.height / rect.height
    if ('touches' in e) {
      const t = e.touches[0]
      return { x: (t.clientX - rect.left) * scaleX, y: (t.clientY - rect.top) * scaleY }
    }
    return { x: ((e as React.MouseEvent).clientX - rect.left) * scaleX, y: ((e as React.MouseEvent).clientY - rect.top) * scaleY }
  }

  function startDraw(e: React.MouseEvent<HTMLCanvasElement> | React.TouchEvent<HTMLCanvasElement>) {
    e.preventDefault()
    const canvas = canvasRef.current
    if (!canvas) return
    const ctx = canvas.getContext('2d')
    if (!ctx) return
    const { x, y } = getCanvasPos(canvas, e)
    ctx.beginPath()
    ctx.moveTo(x, y)
    setIsDrawing(true)
    setHasSig(true)
  }

  function draw(e: React.MouseEvent<HTMLCanvasElement> | React.TouchEvent<HTMLCanvasElement>) {
    e.preventDefault()
    if (!isDrawing) return
    const canvas = canvasRef.current
    if (!canvas) return
    const ctx = canvas.getContext('2d')
    if (!ctx) return
    const { x, y } = getCanvasPos(canvas, e)
    ctx.lineWidth = 2
    ctx.lineCap = 'round'
    ctx.strokeStyle = '#1e293b'
    ctx.lineTo(x, y)
    ctx.stroke()
  }

  function endDraw() {
    setIsDrawing(false)
  }

  function clearCanvas() {
    const canvas = canvasRef.current
    if (!canvas) return
    const ctx = canvas.getContext('2d')
    if (!ctx) return
    ctx.fillStyle = '#fff'
    ctx.fillRect(0, 0, canvas.width, canvas.height)
    setHasSig(false)
  }

  async function onSubmit(values: FormData) {
    if (!data) return

    let sigKey: string | null = null
    if (hasSig) {
      const canvas = canvasRef.current
      if (canvas) {
        try {
          const blob = await new Promise<Blob>((resolve, reject) => {
            canvas.toBlob(b => b ? resolve(b) : reject(new Error('canvas vacío')), 'image/png')
          })
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
    }

    const body: SubmitPublicResponseRequest = {
      decision: values.decision,
      signerName: values.signerName,
      contentHash: data.contentHash,
      comment: values.comment || null,
      items: data.items.map(item => ({ quoteItemId: item.id, approved: values.decision === 'approved' })),
      signatureFileKey: sigKey,
      signerDocNumber: signerDocNumber.trim() || null,
    }
    submitMutation.mutate(body)
  }

  return (
    <div className="min-h-screen bg-slate-50">
      <div className="mx-auto max-w-2xl px-4 py-10 space-y-6">

        {/* Encabezado del presupuesto */}
        <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
          <div className="mb-4 flex items-start justify-between">
            <div>
              <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Presupuesto</p>
              <h1 className="text-2xl font-bold text-slate-800">#{data.number}</h1>
            </div>
            <span className="rounded-full bg-blue-100 px-3 py-1 text-sm font-medium text-blue-700">
              {QUOTE_STATUS_LABELS[data.status]}
            </span>
          </div>

          <dl className="grid grid-cols-2 gap-4 text-sm">
            <div>
              <dt className="font-medium text-slate-500">Cliente</dt>
              <dd className="mt-1 text-slate-800">{data.customerName}</dd>
            </div>
            <div>
              <dt className="font-medium text-slate-500">Vehículo</dt>
              <dd className="mt-1 text-slate-800">
                {[data.vehicleBrand, data.vehicleModel, data.vehiclePlate].filter(Boolean).join(' · ')}
              </dd>
            </div>
            <div>
              <dt className="font-medium text-slate-500">Válido hasta</dt>
              <dd className={`mt-1 ${isExpired ? 'text-red-600' : 'text-slate-800'}`}>
                {new Date(data.validUntil).toLocaleDateString('es-AR')}
                {isExpired && ' (vencido)'}
              </dd>
            </div>
          </dl>

          {data.customerNotes && (
            <div className="mt-4 rounded-md bg-slate-50 p-3 text-sm text-slate-700">
              <p className="mb-1 font-medium text-slate-500">Notas</p>
              {data.customerNotes}
            </div>
          )}
        </div>

        {/* Ítems del presupuesto */}
        <div className="rounded-xl border border-slate-200 bg-white shadow-sm overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-slate-50 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">
              <tr>
                <th className="px-4 py-3">Descripción</th>
                <th className="px-4 py-3 text-right">Cantidad</th>
                <th className="px-4 py-3 text-right">Precio unit.</th>
                <th className="px-4 py-3 text-right">Subtotal</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {data.items.map(item => (
                <tr key={item.id}>
                  <td className="px-4 py-3 text-slate-800">{item.description}</td>
                  <td className="px-4 py-3 text-right text-slate-600">{item.quantity}</td>
                  <td className="px-4 py-3 text-right text-slate-600">
                    ${item.unitNetPrice.toLocaleString('es-AR', { minimumFractionDigits: 2 })}
                  </td>
                  <td className="px-4 py-3 text-right font-medium text-slate-800">
                    ${item.lineTotal.toLocaleString('es-AR', { minimumFractionDigits: 2 })}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          <div className="border-t border-slate-100 px-4 py-3 space-y-1 text-sm">
            {data.globalDiscountPct > 0 && (
              <>
                <div className="flex justify-between text-slate-600">
                  <span>Subtotal</span>
                  <span>${data.subtotalNet.toLocaleString('es-AR', { minimumFractionDigits: 2 })}</span>
                </div>
                <div className="flex justify-between text-slate-600">
                  <span>Descuento ({data.globalDiscountPct}%)</span>
                  <span>−${data.discountAmount.toLocaleString('es-AR', { minimumFractionDigits: 2 })}</span>
                </div>
              </>
            )}
            <div className="flex justify-between text-slate-600">
              <span>IVA</span>
              <span>${data.vatAmount.toLocaleString('es-AR', { minimumFractionDigits: 2 })}</span>
            </div>
            <div className="flex justify-between text-base font-bold text-slate-800 pt-1 border-t border-slate-200">
              <span>Total</span>
              <span>${data.total.toLocaleString('es-AR', { minimumFractionDigits: 2 })}</span>
            </div>
          </div>
        </div>

        {/* Formulario de respuesta */}
        {canRespond && !isExpired && (
          <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
            <h2 className="text-base font-semibold text-slate-800 mb-4">Su respuesta</h2>
            <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
              <div>
                <label className="mb-1 block text-sm font-medium text-slate-700">
                  Nombre completo <span className="text-red-500">*</span>
                </label>
                <input {...register('signerName')} className={inputCls} placeholder="Juan García" />
                {errors.signerName && (
                  <p className="mt-1 text-xs text-red-600">{errors.signerName.message}</p>
                )}
              </div>

              <div>
                <label className="mb-1 block text-sm font-medium text-slate-700">Decisión</label>
                <div className="flex gap-4">
                  <label className="flex items-center gap-2 text-sm">
                    <input type="radio" value="approved" {...register('decision')} />
                    <span className="text-green-700 font-medium">Apruebo</span>
                  </label>
                  <label className="flex items-center gap-2 text-sm">
                    <input type="radio" value="rejected" {...register('decision')} />
                    <span className="text-red-700 font-medium">Rechazo</span>
                  </label>
                </div>
              </div>

              <div>
                <label className="mb-1 block text-sm font-medium text-slate-700">
                  Comentario (opcional)
                </label>
                <textarea
                  {...register('comment')}
                  rows={3}
                  className={inputCls}
                  placeholder="Puede agregar observaciones..."
                />
              </div>

              {/* Firma digital */}
              <div>
                <label className="mb-1 block text-sm font-medium text-slate-700">
                  Firma (opcional)
                </label>
                <div className="rounded-md border border-slate-300 bg-white overflow-hidden touch-none select-none">
                  <canvas
                    ref={canvasRef}
                    width={600}
                    height={160}
                    className="w-full"
                    style={{ cursor: 'crosshair' }}
                    onMouseDown={startDraw}
                    onMouseMove={draw}
                    onMouseUp={endDraw}
                    onMouseLeave={endDraw}
                    onTouchStart={startDraw}
                    onTouchMove={draw}
                    onTouchEnd={endDraw}
                  />
                </div>
                <div className="mt-1 flex items-center justify-between">
                  <p className="text-xs text-slate-400">Dibuje su firma con el dedo o el mouse</p>
                  <button
                    type="button"
                    onClick={clearCanvas}
                    className="text-xs text-slate-500 underline hover:text-slate-700"
                  >
                    Borrar firma
                  </button>
                </div>
              </div>

              {/* DNI */}
              <div>
                <label className="mb-1 block text-sm font-medium text-slate-700">
                  DNI / Documento (opcional)
                </label>
                <input
                  type="text"
                  value={signerDocNumber}
                  onChange={e => setSignerDocNumber(e.target.value)}
                  className={inputCls}
                  placeholder="Ej: 30123456"
                  maxLength={20}
                />
                <p className="mt-1 text-xs text-slate-400">
                  Su documento se almacena cifrado y no es visible para terceros.
                </p>
              </div>

              {serverError && (
                <p className="text-sm text-red-600">{serverError}</p>
              )}

              <Button type="submit" loading={isSubmitting} className="w-full">
                Enviar respuesta
              </Button>
            </form>
          </div>
        )}

        {canRespond && isExpired && (
          <div className="rounded-xl border border-orange-200 bg-orange-50 p-4 text-sm text-orange-800">
            Este presupuesto venció el {new Date(data.validUntil).toLocaleDateString('es-AR')}.
            Contacte al taller para obtener un nuevo presupuesto.
          </div>
        )}

        {!canRespond && data.status !== 'sent' && (
          <div className="rounded-xl border border-slate-200 bg-white p-4 text-center text-sm text-slate-500">
            Este presupuesto está en estado{' '}
            <span className="font-medium text-slate-700">{QUOTE_STATUS_LABELS[data.status]}</span>{' '}
            y ya no acepta respuestas.
          </div>
        )}
      </div>
    </div>
  )
}
