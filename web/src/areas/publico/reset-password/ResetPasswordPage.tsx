import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useState } from 'react'
import { useSearchParams, Link, useNavigate } from 'react-router'
import { Input } from '../../../components/ui/Input'
import { Button } from '../../../components/ui/Button'
import { authApi } from '../../../lib/api/auth'
import { ApiError } from '../../../lib/api/client'

const schema = z
  .object({
    password: z.string().min(8, 'Mínimo 8 caracteres'),
    confirm: z.string(),
  })
  .refine((d) => d.password === d.confirm, {
    message: 'Las contraseñas no coinciden',
    path: ['confirm'],
  })

type FormData = z.infer<typeof schema>

export function ResetPasswordPage() {
  const [searchParams] = useSearchParams()
  const navigate = useNavigate()
  const token = searchParams.get('token') ?? ''
  const [serverError, setServerError] = useState<string | null>(null)

  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<FormData>({
    resolver: zodResolver(schema),
  })

  async function onSubmit(data: FormData) {
    setServerError(null)
    try {
      await authApi.resetPassword(token, data.password)
      navigate('/login', { replace: true, state: { passwordReset: true } })
    } catch (err) {
      if (err instanceof ApiError && err.status === 422) {
        setServerError('El enlace es inválido o expiró. Pedí uno nuevo.')
      } else {
        setServerError('Ocurrió un error. Intentá de nuevo.')
      }
    }
  }

  if (!token) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-tc-navy px-4">
        <p className="text-sm text-white">
          Enlace inválido.{' '}
          <Link to="/forgot-password" className="text-tc-purple hover:opacity-70">
            Pedí uno nuevo.
          </Link>
        </p>
      </div>
    )
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-tc-navy px-4">
      <div className="w-full max-w-sm border border-tc-border bg-white">
        <div className="border-b border-tc-border bg-tc-stub-bg px-6 py-3">
          <span className="text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">TallerControl</span>
        </div>
        <div className="px-6 py-8">
          <h1 className="mb-6 text-xl font-bold tracking-tight text-slate-900">Nueva contraseña</h1>
          <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-4">
            <Input
              label="Contraseña"
              type="password"
              autoComplete="new-password"
              error={errors.password?.message}
              {...register('password')}
            />
            <Input
              label="Confirmar contraseña"
              type="password"
              autoComplete="new-password"
              error={errors.confirm?.message}
              {...register('confirm')}
            />

            {serverError && (
              <p role="alert" className="border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
                {serverError}
              </p>
            )}

            <Button type="submit" loading={isSubmitting} className="mt-2 w-full">
              Guardar contraseña
            </Button>
          </form>
        </div>
      </div>
    </div>
  )
}
