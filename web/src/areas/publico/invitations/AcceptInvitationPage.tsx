import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useState } from 'react'
import { useParams, useNavigate } from 'react-router'
import { AuthAlert, AuthCard, AuthInput } from '../../../components/ui/AuthCard'
import { LockIcon } from '../../../components/ui/icons'
import { Button } from '../../../components/ui/Button'
import { invitationsApi } from '../../../lib/api/users'
import { useAuth } from '../../../lib/auth/AuthContext'
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

export function AcceptInvitationPage() {
  const { token } = useParams<{ token: string }>()
  const navigate = useNavigate()
  const { invalidate } = useAuth()
  const [serverError, setServerError] = useState<string | null>(null)

  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<FormData>({
    resolver: zodResolver(schema),
  })

  async function onSubmit(data: FormData) {
    if (!token) return
    setServerError(null)
    try {
      await invitationsApi.accept(token, data.password)
      invalidate()
      navigate('/app', { replace: true })
    } catch (err) {
      if (err instanceof ApiError && err.status === 422) {
        setServerError('La invitación es inválida o expiró. Pedí una nueva al dueño del taller.')
      } else {
        setServerError('Ocurrió un error. Intentá de nuevo.')
      }
    }
  }

  return (
    <AuthCard title="Activar cuenta" subtitle="Elegí una contraseña para completar el registro.">
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
          Activar cuenta
        </Button>
      </form>
    </AuthCard>
  )
}
