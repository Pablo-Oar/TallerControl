import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useState } from 'react'
import { useParams, useNavigate } from 'react-router'
import { Input } from '../../../components/ui/Input'
import { Button } from '../../../components/ui/Button'
import { invitationsApi } from '../../../lib/api/users'
import { useAuth } from '../../../lib/auth/AuthContext'
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
    <div className="flex min-h-screen items-center justify-center bg-tc-navy px-4">
      <div className="w-full max-w-sm border border-tc-border bg-white">
        <div className="border-b border-tc-border bg-tc-stub-bg px-6 py-3">
          <span className="text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">TallerControl</span>
        </div>
        <div className="px-6 py-8">
          <h1 className="mb-2 text-xl font-bold tracking-tight text-slate-900">Activar cuenta</h1>
          <p className="mb-6 text-sm text-slate-500">Elegí una contraseña para completar el registro.</p>
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
              Activar cuenta
            </Button>
          </form>
        </div>
      </div>
    </div>
  )
}
