import { useState } from 'react'
import { registrationApi } from '../../lib/api/registration'
import { Button } from './Button'

/**
 * Reenvía el enlace de verificación. Sin `email` pide el correo (el enlace vencido no lo trae).
 * La respuesta es siempre la misma, exista o no la cuenta.
 */
export function ResendVerification({ email: initial }: { email?: string }) {
  const [email, setEmail] = useState(initial ?? '')
  const [state, setState] = useState<'idle' | 'sending' | 'sent' | 'error'>('idle')

  async function send() {
    setState('sending')
    try {
      await registrationApi.resendVerification(email.trim())
      setState('sent')
    } catch {
      setState('error')
    }
  }

  if (state === 'sent') {
    return (
      <p role="status" className="mt-4 text-center text-sm text-tc-text-secondary">
        Si la cuenta existe y falta verificarla, te enviamos un enlace nuevo. Puede tardar un minuto.
      </p>
    )
  }

  return (
    <div className="mt-4 space-y-3">
      {initial === undefined && (
        <div>
          <label htmlFor="resend-email" className="mb-1 block text-sm font-medium text-tc-text">Tu email</label>
          <input
            id="resend-email"
            type="email"
            autoComplete="email"
            value={email}
            onChange={e => setEmail(e.target.value)}
            className="h-11 w-full rounded-xl border border-tc-border px-3 text-sm"
          />
        </div>
      )}
      {state === 'error' && (
        <p role="alert" className="text-center text-sm text-red-700">No se pudo reenviar. Probá de nuevo en un rato.</p>
      )}
      <Button
        type="button"
        variant="secondary"
        className="h-11 w-full rounded-xl"
        loading={state === 'sending'}
        disabled={!email.trim()}
        onClick={send}
      >
        Reenviar enlace de verificación
      </Button>
    </div>
  )
}
