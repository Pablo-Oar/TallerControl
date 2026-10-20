import { useEffect, useState } from 'react'
import { useSearchParams, Link } from 'react-router'
import { registrationApi } from '../../../lib/api/registration'
import { ApiError } from '../../../lib/api/client'
import { Button } from '../../../components/ui/Button'

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

  return (
    <div className="flex min-h-screen items-center justify-center bg-tc-navy px-4">
      <div className="w-full max-w-sm border border-tc-border bg-white">
        <div className="border-b border-tc-border bg-tc-stub-bg px-6 py-3">
          <span className="text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">TallerControl</span>
        </div>
        <div className="px-6 py-8 text-center">
          {status === 'loading' && (
            <>
              <div className="mb-4 space-y-2">
                <div className="mx-auto h-3 w-3/4 animate-pulse bg-tc-border" />
                <div className="mx-auto h-3 w-1/2 animate-pulse bg-tc-border" />
              </div>
              <p className="text-sm text-tc-text-secondary">Verificando tu email...</p>
            </>
          )}

          {status === 'success' && (
            <>
              <p className="mb-3 text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Verificación exitosa</p>
              <h1 className="mb-3 text-xl font-bold text-tc-text">Email verificado</h1>
              <p className="text-sm text-tc-text-secondary">{message}</p>
              <div className="mt-6">
                <Link to="/login">
                  <Button className="w-full">Iniciar sesión</Button>
                </Link>
              </div>
            </>
          )}

          {status === 'error' && (
            <>
              <p className="mb-3 text-[10px] font-bold tracking-[0.14em] uppercase text-tc-stamp-red">Error</p>
              <h1 className="mb-3 text-xl font-bold text-tc-text">Error de verificación</h1>
              <p className="text-sm text-tc-text-secondary">{message}</p>
              <p className="mt-6 text-sm text-tc-text-secondary">
                <Link to="/registro" className="text-tc-purple hover:opacity-70">
                  Volver al registro
                </Link>
              </p>
            </>
          )}
        </div>
      </div>
    </div>
  )
}
