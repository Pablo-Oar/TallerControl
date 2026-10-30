import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useState } from 'react'
import { useSearchParams, Link, useNavigate } from 'react-router'
import { AuthAlert, AuthCard, AuthInput } from '../../../components/ui/AuthCard'
import { LockIcon } from '../../../components/ui/icons'
import { Button } from '../../../components/ui/Button'
import { authApi } from '../../../lib/api/auth'
import { ApiError } from '../../../lib/api/client'

const schema = z
  .object({
    password: z.string().min(10, 'Mínimo 10 caracteres'),
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
      <AuthCard
        title="Enlace inválido"
        footer={
          <Link to="/forgot-password" className="font-semibold text-tc-purple hover:underline">
            Pedí uno nuevo
          </Link>
        }
      >
        <p className="text-center text-sm text-tc-text-secondary">Este enlace no es válido o ya venció.</p>
      </AuthCard>
    )
  }

  return (
    <AuthCard title="Nueva contraseña" subtitle="Elegí una contraseña de al menos 10 caracteres.">
      <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-4">
        <AuthInput
          label="Contraseña"
          type="password"
          icon={<LockIcon />}
          autoComplete="new-password"
          placeholder="Mínimo 10 caracteres"
          error={errors.password?.message}
          {...register('password')}
        />
        <AuthInput
          label="Confirmar contraseña"
          type="password"
          icon={<LockIcon />}
          autoComplete="new-password"
          placeholder="Repetí la contraseña"
          error={errors.confirm?.message}
          {...register('confirm')}
        />

        {serverError && <AuthAlert>{serverError}</AuthAlert>}

        <Button type="submit" loading={isSubmitting} className="mt-1 h-11 w-full rounded-xl">
          Guardar contraseña
        </Button>
      </form>
    </AuthCard>
  )
}
