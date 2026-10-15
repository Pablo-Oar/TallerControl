import { useEffect, useState } from 'react'
import { useSearchParams, Link } from 'react-router'
import { registrationApi } from '../../../lib/api/registration'
import { ApiError } from '../../../lib/api/client'

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
        // El endpoint devuelve 400 con { success: false, message } para tokens inválidos/expirados.
        if (err instanceof ApiError && err.status === 400 && err.extensions?.['message']) {
          setMessage(err.extensions['message'] as string)
        } else {
          setMessage('No se pudo verificar el email. El enlace puede haber expirado.')
        }
        setStatus('error')
      })
  }, [token])

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50">
      <div className="w-full max-w-sm rounded-xl border border-slate-200 bg-white p-8 shadow-sm text-center">
        {status === 'loading' && (
          <>
            <div className="mb-4 text-4xl animate-spin">⏳</div>
            <p className="text-slate-600">Verificando tu email...</p>
          </>
        )}

        {status === 'success' && (
          <>
            <div className="mb-4 text-4xl">✅</div>
            <h1 className="mb-2 text-xl font-semibold text-slate-900">Email verificado</h1>
            <p className="text-slate-600 text-sm">{message}</p>
            <Link
              to="/login"
              className="mt-6 inline-block rounded-lg bg-blue-600 px-6 py-2.5 text-sm font-medium text-white hover:bg-blue-700"
            >
              Iniciar sesión
            </Link>
          </>
        )}

        {status === 'error' && (
          <>
            <div className="mb-4 text-4xl">❌</div>
            <h1 className="mb-2 text-xl font-semibold text-slate-900">Error de verificación</h1>
            <p className="text-slate-600 text-sm">{message}</p>
            <p className="mt-6 text-sm text-slate-500">
              <Link to="/registro" className="text-blue-600 hover:underline">
                Volver al registro
              </Link>
            </p>
          </>
        )}
      </div>
    </div>
  )
}
