import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useState } from 'react'
import { useNavigate, Link } from 'react-router'
import { AuthAlert, AuthCard, AuthInput } from '../../../components/ui/AuthCard'
import { LockIcon, MailIcon } from '../../../components/ui/icons'
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
      invalidate(me)
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
    <AuthCard
      title="Bienvenido"
      footer={
        <>
          ¿No tenés cuenta?{' '}
          <Link to="/registro" className="font-semibold text-tc-purple hover:underline">
            Crear cuenta gratis
          </Link>
        </>
      }
    >
      <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-3">
        <AuthInput
          label="Email"
          type="email"
          icon={<MailIcon />}
          placeholder="Email"
          autoComplete="email"
          error={errors.email?.message}
          {...register('email')}
        />
        <AuthInput
          label="Contraseña"
          type="password"
          icon={<LockIcon />}
          placeholder="Contraseña"
          autoComplete="current-password"
          error={errors.password?.message}
          {...register('password')}
        />
        <div className="flex items-center justify-between gap-3">
          <label className="flex min-h-[44px] items-center gap-2 text-sm text-tc-text-secondary">
            <input type="checkbox" {...register('rememberMe')} className="h-4 w-4 accent-tc-purple" />
            Recordarme
          </label>
          <Link to="/forgot-password" className="text-sm font-medium text-tc-purple hover:underline">
            ¿Olvidaste tu contraseña?
          </Link>
        </div>

        {serverError && (
          <AuthAlert>{serverError}</AuthAlert>
        )}

        <Button type="submit" loading={isSubmitting} className="mt-1 h-11 w-full rounded-xl">
          Ingresar
        </Button>
      </form>
    </AuthCard>
  )
}
