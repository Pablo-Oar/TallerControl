import { useState } from 'react'
import { Link } from 'react-router'
import { useParams, useNavigate } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import {
  scanQrCode,
  startOwnerChallenge,
  validateOwnerOtp,
  reportFoundKeys,
} from '../../../lib/api/qrCards'

type Step = 'scan' | 'challenge' | 'otp' | 'found-keys' | 'found-keys-done'

export function PublicQrPage() {
  const { code } = useParams<{ code: string }>()
  const navigate = useNavigate()
  const [step, setStep] = useState<Step>('scan')
  const [plate, setPlate] = useState('')
  const [customerCode, setCustomerCode] = useState('')
  const [challengeId, setChallengeId] = useState<string | null>(null)
  const [otp, setOtp] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [reporterName, setReporterName] = useState('')
  const [reporterPhone, setReporterPhone] = useState('')

  const { data: scanData, isLoading: scanLoading, isError } = useQuery({
    queryKey: ['qr-scan', code],
    queryFn: () => scanQrCode(code!),
    retry: false,
  })

  if (scanLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-slate-50">
        <p className="text-slate-500">Verificando tarjeta QR…</p>
      </div>
    )
  }

  if (isError || !scanData) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-3 bg-slate-50 px-4 text-center">
        <p className="text-lg font-semibold text-slate-800">Tarjeta QR no válida</p>
        <p className="text-sm text-slate-500">
          Este código QR no está registrado o fue revocado.
        </p>
      </div>
    )
  }

  if (scanData.cardState !== 'assigned' && scanData.cardState !== 'lost') {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-3 bg-slate-50 px-4 text-center">
        <p className="text-lg font-semibold text-slate-800">{scanData.workshop.name}</p>
        <p className="text-sm text-slate-500">
          Esta tarjeta QR aún no está vinculada a un vehículo.
        </p>
      </div>
    )
  }

  async function handleChallenge(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    setLoading(true)
    try {
      const result = await startOwnerChallenge(
        code!,
        plate.toUpperCase().replace(/[^A-Z0-9]/g, ''),
        customerCode.trim() || null,
        '1x0000000000000000000000000000000AA', // Turnstile bypass en dev; en prod usar el widget
      )
      setChallengeId(result.challengeId)
      setStep('otp')
    } catch {
      setError('No se pudo iniciar la verificación. Intentá nuevamente.')
    } finally {
      setLoading(false)
    }
  }

  async function handleFoundKeys(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    setLoading(true)
    try {
      await reportFoundKeys(
        code!,
        reporterName.trim() || null,
        reporterPhone.trim() || null,
      )
      setStep('found-keys-done')
    } catch {
      setError('No se pudo enviar el aviso. Intentá de nuevo.')
    } finally {
      setLoading(false)
    }
  }

  async function handleOtp(e: React.FormEvent) {
    e.preventDefault()
    if (!challengeId) return
    setError(null)
    setLoading(true)
    try {
      await validateOwnerOtp(code!, challengeId, otp.trim().toUpperCase())
      navigate(`/q/${code}/mi-vehiculo`, { replace: true })
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Código incorrecto.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="flex min-h-screen flex-col items-center justify-center bg-slate-50 px-4">
      <div className="w-full max-w-sm space-y-6">
        {/* Header */}
        <div className="text-center">
          <p className="text-xs font-medium uppercase tracking-wide text-slate-400">
            {scanData.workshop.name}
          </p>
          <h1 className="mt-1 text-xl font-bold text-slate-900">Portal del dueño</h1>
          <p className="mt-1 text-sm text-slate-500">
            Verificá que sos el dueño del vehículo para ver su historial.
          </p>
        </div>

        {error && (
          <div className="rounded border border-red-200 bg-red-50 px-4 py-2 text-sm text-red-700">
            {error}
          </div>
        )}

        {/* Step: formulario de verificación */}
        {(step === 'scan' || step === 'challenge') && (
          <form onSubmit={handleChallenge} className="space-y-4">
            <div>
              <label className="mb-1 block text-sm font-medium text-slate-700">
                Patente del vehículo
              </label>
              <input
                required
                className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm uppercase focus:border-blue-500 focus:outline-none"
                placeholder="Ej. ABC123 o AB123CD"
                value={plate}
                onChange={(e) => setPlate(e.target.value.toUpperCase())}
              />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-slate-700">
                Código de acceso del taller{' '}
                <span className="font-normal text-slate-400">(opcional)</span>
              </label>
              <input
                className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm uppercase tracking-widest focus:border-blue-500 focus:outline-none"
                placeholder="Código de 8 caracteres"
                maxLength={8}
                value={customerCode}
                onChange={(e) => setCustomerCode(e.target.value.toUpperCase())}
              />
              <p className="mt-1 text-xs text-slate-400">
                Si no tenés el código, podés pedir que el taller te envíe un código por email.
              </p>
            </div>
            <button
              type="submit"
              disabled={loading || !plate}
              className="w-full rounded-md bg-blue-600 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
            >
              {loading ? 'Verificando…' : 'Verificar identidad'}
            </button>
          </form>
        )}

        {/* Step: ingresar OTP */}
        {step === 'otp' && (
          <form onSubmit={handleOtp} className="space-y-4">
            <div>
              <label className="mb-1 block text-sm font-medium text-slate-700">
                Código de verificación
              </label>
              {customerCode ? (
                <p className="text-sm text-slate-500">
                  Ingresá nuevamente tu código de acceso del taller ({customerCode.length} caracteres).
                </p>
              ) : (
                <p className="text-sm text-slate-500">
                  Te enviamos un código de 6 dígitos por email. Ingresalo acá.
                </p>
              )}
              <input
                required
                autoFocus
                className="mt-2 w-full rounded-md border border-slate-300 px-3 py-2 text-center text-lg font-mono tracking-widest focus:border-blue-500 focus:outline-none"
                placeholder={customerCode ? '••••••••' : '000000'}
                maxLength={customerCode ? 8 : 6}
                value={otp}
                onChange={(e) => setOtp(e.target.value.toUpperCase())}
              />
            </div>
            <button
              type="submit"
              disabled={loading || !otp}
              className="w-full rounded-md bg-blue-600 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
            >
              {loading ? 'Verificando…' : 'Acceder'}
            </button>
            <button
              type="button"
              className="w-full text-sm text-slate-500 hover:underline"
              onClick={() => { setStep('challenge'); setOtp(''); setError(null) }}
            >
              ← Volver
            </button>
          </form>
        )}

        {/* Step: reportar llaves encontradas */}
        {step === 'found-keys' && (
          <form onSubmit={handleFoundKeys} className="space-y-4">
            <div className="rounded border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
              Vas a avisar al taller que encontraste estas llaves. Podés dejar tus datos para que
              puedan contactarte.
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-slate-700">
                Nombre <span className="font-normal text-slate-400">(opcional)</span>
              </label>
              <input
                className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none"
                placeholder="Tu nombre"
                value={reporterName}
                onChange={(e) => setReporterName(e.target.value)}
              />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-slate-700">
                Teléfono <span className="font-normal text-slate-400">(opcional)</span>
              </label>
              <input
                type="tel"
                className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none"
                placeholder="Ej. +54 9 11 1234-5678"
                value={reporterPhone}
                onChange={(e) => setReporterPhone(e.target.value)}
              />
            </div>
            <button
              type="submit"
              disabled={loading}
              className="w-full rounded-md bg-amber-500 py-2 text-sm font-medium text-white hover:bg-amber-600 disabled:opacity-50"
            >
              {loading ? 'Enviando…' : 'Enviar aviso al taller'}
            </button>
            <button
              type="button"
              className="w-full text-sm text-slate-500 hover:underline"
              onClick={() => { setStep('scan'); setError(null) }}
            >
              ← Volver
            </button>
          </form>
        )}

        {step === 'found-keys-done' && (
          <div className="rounded border border-green-200 bg-green-50 px-4 py-4 text-center">
            <p className="font-medium text-green-800">Aviso enviado</p>
            <p className="mt-1 text-sm text-green-700">
              El taller fue notificado. Muchas gracias por tu ayuda.
            </p>
          </div>
        )}

        {/* Enlace para reportar llaves (solo en paso de verificación) */}
        {(step === 'scan' || step === 'challenge') && (
          <div className="pt-2 text-center">
            <button
              type="button"
              className="text-xs text-slate-400 hover:text-amber-600 hover:underline"
              onClick={() => { setStep('found-keys'); setError(null) }}
            >
              Encontré estas llaves — no soy el dueño
            </button>
          </div>
        )}
      </div>
      <p className="mt-6 text-center text-xs text-slate-400">
        <Link to="/privacidad" className="hover:underline">
          Aviso de privacidad
        </Link>
      </p>
    </div>
  )
}
