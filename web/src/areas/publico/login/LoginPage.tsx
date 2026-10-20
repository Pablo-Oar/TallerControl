import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useState } from 'react'
import { useNavigate, Link } from 'react-router'
import { FormField } from '../../../components/ui/FormField'
import { Button } from '../../../components/ui/Button'
import { authApi } from '../../../lib/api/auth'
import { useAuth } from '../../../lib/auth/AuthContext'
import { ApiError } from '../../../lib/api/client'

const schema = z.object({
  email: z.string().email('Email inválido'),
  password: z.string().min(1, 'Ingresá la contraseña'),
  rememberMe: z.boolean().optional(),
})

type FormData = z.infer<typeof schema>

export function LoginPage() {
  const navigate = useNavigate()
  const { invalidate } = useAuth()
  const [serverError, setServerError] = useState<string | null>(null)

  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<FormData>({
    resolver: zodResolver(schema),
  })

  async function onSubmit(data: FormData) {
    setServerError(null)
    try {
      await authApi.login({ email: data.email, password: data.password, rememberMe: data.rememberMe })
      const me = await authApi.me()
      invalidate()
      navigate(me.home, { replace: true })
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.status === 423) {
          setServerError('Tu cuenta está bloqueada por intentos fallidos. Intentá de nuevo en unos minutos.')
        } else if (err.status === 401 && err.extensions?.['code'] === 'email_not_verified') {
          setServerError('Verificá tu email antes de ingresar. Revisá tu casilla de correo.')
        } else {
          setServerError('Email o contraseña incorrectos.')
        }
      } else {
        setServerError('Ocurrió un error. Intentá de nuevo.')
      }
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-tc-navy px-4">
      <div className="w-full max-w-sm border border-tc-border bg-white">
        <div className="border-b border-tc-border bg-tc-stub-bg px-6 py-3">
          <span className="text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">TallerControl</span>
        </div>
        <div className="px-6 py-8">
          <h1 className="mb-6 text-xl font-bold tracking-tight text-slate-900">Ingresar</h1>
          <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-4">
            <FormField
              label="Email"
              type="email"
              autoComplete="email"
              error={errors.email?.message}
              {...register('email')}
            />
            <FormField
              label="Contraseña"
              type="password"
              autoComplete="current-password"
              error={errors.password?.message}
              {...register('password')}
            />
            <label className="flex items-center gap-2 text-sm text-slate-600">
              <input type="checkbox" {...register('rememberMe')} className="accent-tc-purple" />
              Recordarme
            </label>

            {serverError && (
              <p role="alert" className="border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
                {serverError}
              </p>
            )}

            <Button type="submit" loading={isSubmitting} className="mt-2 w-full">
              Ingresar
            </Button>
          </form>
          <p className="mt-4 text-center text-sm text-slate-500">
            <Link to="/forgot-password" className="text-tc-purple hover:opacity-70">
              ¿Olvidaste tu contraseña?
            </Link>
          </p>
          <p className="mt-2 text-center text-sm text-slate-500">
            ¿No tenés cuenta?{' '}
            <Link to="/registro" className="text-tc-purple hover:opacity-70">
              Crear cuenta gratis
            </Link>
          </p>
        </div>
      </div>
    </div>
  )
}
