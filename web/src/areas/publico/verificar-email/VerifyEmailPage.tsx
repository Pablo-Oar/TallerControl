import { useEffect, useState } from 'react'
import { useSearchParams, Link } from 'react-router'
import { registrationApi } from '../../../lib/api/registration'
import { ApiError } from '../../../lib/api/client'
import { Button } from '../../../components/ui/Button'
import { AuthCard } from '../../../components/ui/AuthCard'
import { ResendVerification } from '../../../components/ui/ResendVerification'

type Status = 'loading' | 'success' | 'error'

export function VerifyEmailPage() {
  const [searchParams] = useSearchParams()
  const token = searchParams.get('token') ?? ''
  const [status, setStatus] = useState<Status>('loading')
  const [message, setMessage] = useState('')

  useEffect(() => {
    if (!token) {
      setStatus('error')
      setMessage('El enlace no es válido.')
      return
    }

    registrationApi
      .verifyEmail(token)
      .then((res) => {
        if (res.success) {
          setStatus('success')
          setMessage(res.message)
        } else {
          setStatus('error')
          setMessage(res.message)
        }
      })
      .catch((err) => {
        if (err instanceof ApiError && err.status === 400 && err.extensions?.['message']) {
          setMessage(err.extensions['message'] as string)
        } else {
          setMessage('No se pudo verificar el email. El enlace puede haber expirado.')
        }
        setStatus('error')
      })
  }, [token])

  if (status === 'loading') {
    return (
      <AuthCard title="Verificando tu email">
        <div role="status" className="space-y-2">
          <div className="mx-auto h-3 w-3/4 animate-pulse rounded-full bg-tc-border" />
          <div className="mx-auto h-3 w-1/2 animate-pulse rounded-full bg-tc-border" />
          <p className="pt-2 text-center text-sm text-tc-text-secondary">Un momento…</p>
        </div>
      </AuthCard>
    )
  }

  if (status === 'success') {
    return (
      <AuthCard title="Email verificado" subtitle="Verificación exitosa">
        <p className="text-center text-sm text-tc-text-secondary">{message}</p>
        <Link to="/login" className="mt-6 block">
          <Button className="h-11 w-full rounded-xl">Iniciar sesión</Button>
        </Link>
      </AuthCard>
    )
  }

  return (
    <AuthCard
      title="Error de verificación"
      footer={
        <Link to="/registro" className="font-semibold text-tc-purple hover:underline">
          Volver al registro
        </Link>
      }
    >
      <p className="text-center text-sm text-tc-text-secondary">{message}</p>
      <ResendVerification />
    </AuthCard>
  )
}
