import { FormField } from '../../../components/ui/FormField'
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
      <div className="flex min-h-screen items-center justify-center bg-tc-navy">
        <div className="space-y-2 w-48">
          <div className="h-2 animate-pulse bg-tc-navy-mid" />
          <div className="h-2 w-3/4 animate-pulse bg-tc-navy-mid" />
        </div>
      </div>
    )
  }

  if (isError || !scanData) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-tc-navy px-4 text-center">
        <div className="border border-tc-border bg-white p-8 max-w-sm w-full">
          <p className="text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted mb-2">Error</p>
          <p className="font-bold text-tc-text">Tarjeta QR no válida</p>
          <p className="mt-2 text-sm text-tc-text-secondary">Este código QR no está registrado o fue revocado.</p>
        </div>
      </div>
    )
  }

  if (scanData.cardState !== 'assigned' && scanData.cardState !== 'lost') {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-tc-navy px-4 text-center">
        <div className="border border-tc-border bg-white p-8 max-w-sm w-full">
          <p className="text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted mb-2">{scanData.workshop.name}</p>
          <p className="font-bold text-tc-text">Tarjeta no asignada</p>
          <p className="mt-2 text-sm text-tc-text-secondary">Esta tarjeta QR aún no está vinculada a un vehículo.</p>
        </div>
      </div>
    )
  }

  async function handleChallenge(e: { preventDefault(): void }) {
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

  async function handleFoundKeys(e: { preventDefault(): void }) {
    e.preventDefault()
    setError(null)
    setLoading(true)
    try {
      await reportFoundKeys(code!, reporterName.trim() || null, reporterPhone.trim() || null)
      setStep('found-keys-done')
    } catch {
      setError('No se pudo enviar el aviso. Intentá de nuevo.')
    } finally {
      setLoading(false)
    }
  }

  async function handleOtp(e: { preventDefault(): void }) {
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
    <div className="flex min-h-screen flex-col items-center justify-center bg-tc-navy px-4 py-8">
      <div className="w-full max-w-sm border border-tc-border bg-white overflow-hidden">
        <div className="bg-tc-stub-bg border-b border-tc-border px-6 py-3">
          <p className="text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">
            {scanData.workshop.name}
          </p>
        </div>
        <div className="px-6 py-6 space-y-5">
          <div>
            <h1 className="text-lg font-bold tracking-tight text-tc-text">Portal del dueño</h1>
            <p className="mt-1 text-sm text-tc-text-secondary">
              Verificá que sos el dueño del vehículo para ver su historial.
            </p>
          </div>

          {error && (
            <div className="border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              {error}
            </div>
          )}

          {/* Step: formulario de verificación */}
          {(step === 'scan' || step === 'challenge') && (
            <form onSubmit={handleChallenge} className="space-y-4">
              <FormField
                label="Patente del vehículo"
                required
                inputClassName="uppercase font-mono tracking-widest"
                placeholder="Ej. ABC123 o AB123CD"
                value={plate}
                onChange={(e) => setPlate(e.target.value.toUpperCase())}
              />
              <FormField
                label="Código de acceso (opcional)"
                inputClassName="uppercase tracking-widest font-mono"
                placeholder="Código de 8 caracteres"
                maxLength={8}
                value={customerCode}
                onChange={(e) => setCustomerCode(e.target.value.toUpperCase())}
                hint="Si no tenés el código, podés pedir que el taller te envíe uno por email."
              />
              <button
                type="submit"
                disabled={loading || !plate}
                className="w-full border border-tc-navy bg-tc-navy py-2.5 text-xs font-bold tracking-wider uppercase text-white hover:bg-transparent hover:text-tc-navy transition-colors disabled:opacity-40"
              >
                {loading ? 'Verificando…' : 'Verificar identidad'}
              </button>
            </form>
          )}

          {/* Step: ingresar OTP */}
          {step === 'otp' && (
            <form onSubmit={handleOtp} className="space-y-4">
              {customerCode ? (
                <p className="text-sm text-tc-text-secondary">
                  Ingresá nuevamente tu código de acceso del taller ({customerCode.length} caracteres).
                </p>
              ) : (
                <p className="text-sm text-tc-text-secondary">
                  Te enviamos un código de 6 dígitos por email. Ingresalo acá.
                </p>
              )}
              <FormField
                label="Código de verificación"
                required
                autoFocus
                inputClassName="text-center text-xl font-mono tracking-widest uppercase"
                placeholder={customerCode ? '••••••••' : '000000'}
                maxLength={customerCode ? 8 : 6}
                value={otp}
                onChange={(e) => setOtp(e.target.value.toUpperCase())}
              />
              <button
                type="submit"
                disabled={loading || !otp}
                className="w-full border border-tc-navy bg-tc-navy py-2.5 text-xs font-bold tracking-wider uppercase text-white hover:bg-transparent hover:text-tc-navy transition-colors disabled:opacity-40"
              >
                {loading ? 'Verificando…' : 'Acceder'}
              </button>
              <button
                type="button"
                className="w-full text-xs font-bold tracking-wider uppercase text-tc-text-muted hover:text-tc-text-secondary"
                onClick={() => { setStep('challenge'); setOtp(''); setError(null) }}
              >
                ← Volver
              </button>
            </form>
          )}

          {/* Step: reportar llaves encontradas */}
          {step === 'found-keys' && (
            <form onSubmit={handleFoundKeys} className="space-y-4">
              <div className="border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
                Vas a avisar al taller que encontraste estas llaves. Podés dejar tus datos para que
                puedan contactarte.
              </div>
              <FormField
                label="Nombre (opcional)"
                placeholder="Tu nombre"
                value={reporterName}
                onChange={(e) => setReporterName(e.target.value)}
              />
              <FormField
                label="Teléfono (opcional)"
                type="tel"
                placeholder="Ej. +54 9 11 1234-5678"
                value={reporterPhone}
                onChange={(e) => setReporterPhone(e.target.value)}
              />
              <button
                type="submit"
                disabled={loading}
                className="w-full border border-amber-500 bg-amber-500 py-2.5 text-xs font-bold tracking-wider uppercase text-white hover:bg-transparent hover:text-amber-600 transition-colors disabled:opacity-40"
              >
                {loading ? 'Enviando…' : 'Enviar aviso al taller'}
              </button>
              <button
                type="button"
                className="w-full text-xs font-bold tracking-wider uppercase text-tc-text-muted hover:text-tc-text-secondary"
                onClick={() => { setStep('scan'); setError(null) }}
              >
                ← Volver
              </button>
            </form>
          )}

          {step === 'found-keys-done' && (
            <div className="border border-green-200 bg-green-50 px-4 py-4 text-center">
              <p className="font-bold text-green-800">Aviso enviado</p>
              <p className="mt-1 text-sm text-green-700">
                El taller fue notificado. Muchas gracias por tu ayuda.
              </p>
            </div>
          )}

          {/* Enlace para reportar llaves (solo en paso de verificación) */}
          {(step === 'scan' || step === 'challenge') && (
            <div className="pt-1 text-center">
              <button
                type="button"
                className="text-xs text-tc-text-muted hover:text-amber-600 transition-colors"
                onClick={() => { setStep('found-keys'); setError(null) }}
              >
                Encontré estas llaves — no soy el dueño
              </button>
            </div>
          )}
        </div>
      </div>
      <p className="mt-4 text-center text-xs text-tc-text-secondary">
        <Link to="/privacidad" className="hover:text-slate-300 transition-colors">
          Aviso de privacidad
        </Link>
      </p>
    </div>
  )
}
