import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useState } from 'react'
import { Link } from 'react-router'
import { Input } from '../../../components/ui/Input'
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
      <div className="flex min-h-screen items-center justify-center bg-slate-50">
        <div className="w-full max-w-sm rounded-xl border border-slate-200 bg-white p-8 shadow-sm text-center">
          <div className="mb-4 text-4xl">📧</div>
          <h1 className="mb-2 text-xl font-semibold text-slate-900">Verificá tu email</h1>
          <p className="text-slate-600 text-sm">
            Te enviamos un enlace de verificación a{' '}
            <span className="font-medium text-slate-800">{submittedEmail}</span>.
            Hacé clic en el enlace para activar tu cuenta.
          </p>
          <p className="mt-4 text-xs text-slate-400">
            ¿No llegó? Revisá la carpeta de spam.
          </p>
          <p className="mt-6 text-sm text-slate-500">
            <Link to="/login" className="text-blue-600 hover:underline">
              Volver al inicio de sesión
            </Link>
          </p>
        </div>
      </div>
    )
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50">
      <div className="w-full max-w-md rounded-xl border border-slate-200 bg-white p-8 shadow-sm">
        <h1 className="mb-1 text-2xl font-semibold text-slate-900">Crear cuenta</h1>
        <p className="mb-6 text-sm text-slate-500">
          Empezá gratis — sin tarjeta de crédito.
        </p>
        <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-4">
          <Input
            label="Nombre del taller"
            type="text"
            autoComplete="organization"
            placeholder="Ej: Taller Rodríguez"
            error={errors.workshopName?.message}
            {...register('workshopName')}
          />
          <Input
            label="Tu nombre"
            type="text"
            autoComplete="name"
            placeholder="Ej: Carlos Rodríguez"
            error={errors.displayName?.message}
            {...register('displayName')}
          />
          <Input
            label="Email"
            type="email"
            autoComplete="email"
            error={errors.email?.message}
            {...register('email')}
          />
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
            error={errors.confirmPassword?.message}
            {...register('confirmPassword')}
          />

          {serverError && (
            <p role="alert" className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">
              {serverError}
            </p>
          )}

          <Button type="submit" loading={isSubmitting} className="mt-2 w-full">
            Crear cuenta
          </Button>
        </form>
        <p className="mt-4 text-center text-sm text-slate-500">
          ¿Ya tenés cuenta?{' '}
          <Link to="/login" className="text-blue-600 hover:underline">
            Iniciá sesión
          </Link>
        </p>
      </div>
    </div>
  )
}
