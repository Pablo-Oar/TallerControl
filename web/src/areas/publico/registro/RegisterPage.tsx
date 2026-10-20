import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useState } from 'react'
import { Link } from 'react-router'
import { FormField } from '../../../components/ui/FormField'
import { Button } from '../../../components/ui/Button'
import { registrationApi } from '../../../lib/api/registration'
import { ApiError } from '../../../lib/api/client'

const schema = z
  .object({
    workshopName: z.string().min(2, 'El nombre del taller debe tener al menos 2 caracteres'),
    displayName: z.string().min(2, 'El nombre debe tener al menos 2 caracteres'),
    email: z.string().email('Email inválido'),
    password: z.string().min(8, 'La contraseña debe tener al menos 8 caracteres'),
    confirmPassword: z.string(),
  })
  .refine((d) => d.password === d.confirmPassword, {
    message: 'Las contraseñas no coinciden',
    path: ['confirmPassword'],
  })

type FormData = z.infer<typeof schema>

export function RegisterPage() {
  const [submitted, setSubmitted] = useState(false)
  const [submittedEmail, setSubmittedEmail] = useState('')
  const [serverError, setServerError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormData>({ resolver: zodResolver(schema) })

  async function onSubmit(data: FormData) {
    setServerError(null)
    try {
      const res = await registrationApi.register({
        workshopName: data.workshopName,
        displayName: data.displayName,
        email: data.email,
        password: data.password,
      })
      setSubmittedEmail(res.email)
      setSubmitted(true)
    } catch (err) {
      if (err instanceof ApiError && err.status === 422) {
        setServerError(err.detail)
      } else {
        setServerError('Ocurrió un error. Intentá de nuevo.')
      }
    }
  }

  if (submitted) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-tc-navy px-4">
        <div className="w-full max-w-sm border border-tc-border bg-white">
          <div className="border-b border-tc-border bg-tc-stub-bg px-6 py-3">
            <span className="text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">TallerControl</span>
          </div>
          <div className="px-6 py-8 text-center">
            <p className="mb-3 text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Verificación pendiente</p>
            <h1 className="mb-3 text-xl font-bold text-tc-text">Verificá tu email</h1>
            <p className="text-sm text-tc-text-secondary">
              Te enviamos un enlace de verificación a{' '}
              <span className="font-bold text-tc-text">{submittedEmail}</span>.
              Hacé clic en el enlace para activar tu cuenta.
            </p>
            <p className="mt-4 text-xs text-tc-text-muted">¿No llegó? Revisá la carpeta de spam.</p>
            <p className="mt-6 text-sm text-tc-text-secondary">
              <Link to="/login" className="text-tc-purple hover:opacity-70">
                Volver al inicio de sesión
              </Link>
            </p>
          </div>
        </div>
      </div>
    )
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-tc-navy px-4 py-8">
      <div className="w-full max-w-md border border-tc-border bg-white">
        <div className="border-b border-tc-border bg-tc-stub-bg px-6 py-3">
          <span className="text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">TallerControl</span>
        </div>
        <div className="px-6 py-8">
          <h1 className="mb-1 text-xl font-bold tracking-tight text-tc-text">Crear cuenta</h1>
          <p className="mb-6 text-sm text-tc-text-secondary">Empezá gratis — sin tarjeta de crédito.</p>
          <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-4">
            <FormField
              label="Nombre del taller"
              type="text"
              autoComplete="organization"
              placeholder="Ej: Taller Rodríguez"
              error={errors.workshopName?.message}
              {...register('workshopName')}
            />
            <FormField
              label="Tu nombre"
              type="text"
              autoComplete="name"
              placeholder="Ej: Carlos Rodríguez"
              error={errors.displayName?.message}
              {...register('displayName')}
            />
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
              autoComplete="new-password"
              error={errors.password?.message}
              {...register('password')}
            />
            <FormField
              label="Confirmar contraseña"
              type="password"
              autoComplete="new-password"
              error={errors.confirmPassword?.message}
              {...register('confirmPassword')}
            />

            {serverError && (
              <p role="alert" className="border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
                {serverError}
              </p>
            )}

            <Button type="submit" loading={isSubmitting} className="mt-2 w-full">
              Crear cuenta
            </Button>
          </form>
          <p className="mt-4 text-center text-sm text-tc-text-secondary">
            ¿Ya tenés cuenta?{' '}
            <Link to="/login" className="text-tc-purple hover:opacity-70">
              Iniciá sesión
            </Link>
          </p>
        </div>
      </div>
    </div>
  )
}
