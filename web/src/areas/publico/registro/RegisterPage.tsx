import { useForm } from 'react-hook-form'
import { ResendVerification } from '../../../components/ui/ResendVerification'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useState } from 'react'
import { Link } from 'react-router'
import { AuthAlert, AuthCard, AuthInput } from '../../../components/ui/AuthCard'
import { ArrowRightIcon, LockIcon, MailIcon, UserIcon, WrenchIcon } from '../../../components/ui/icons'
import { Button } from '../../../components/ui/Button'
import { registrationApi } from '../../../lib/api/registration'
import { ApiError } from '../../../lib/api/client'

const schema = z
  .object({
    workshopName: z.string().min(2, 'El nombre del taller debe tener al menos 2 caracteres'),
    displayName: z.string().min(2, 'El nombre debe tener al menos 2 caracteres'),
    email: z.string().email('Email inválido'),
    password: z.string().min(10, 'La contraseña debe tener al menos 10 caracteres'),
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
      <AuthCard
        title="Verificá tu email"
        subtitle="Verificación pendiente"
        footer={
          <Link to="/login" className="font-semibold text-tc-purple hover:underline">
            Volver al inicio de sesión
          </Link>
        }
      >
        <p className="text-center text-sm text-tc-text-secondary">
          Te enviamos un enlace de verificación a{' '}
          <span className="font-bold text-tc-text">{submittedEmail}</span>. Hacé clic en el enlace para activar tu
          cuenta.
        </p>
        <p className="mt-4 text-center text-xs text-tc-text-muted">¿No llegó? Revisá la carpeta de spam.</p>
        <ResendVerification email={submittedEmail} />
      </AuthCard>
    )
  }

  return (
    <AuthCard
      className="max-w-md"
      title="Creá tu cuenta gratis"
      subtitle="Empezá en minutos, sin tarjeta de crédito."
      footer={
        <>
          ¿Ya tenés cuenta?{' '}
          <Link to="/login" className="font-semibold text-tc-purple hover:underline">
            Iniciá sesión
          </Link>
        </>
      }
    >
      <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-4">
        <AuthInput
          label="Nombre del taller"
          visibleLabel
          icon={<WrenchIcon />}
          autoComplete="organization"
          placeholder="Ej: Taller Rodríguez"
          error={errors.workshopName?.message}
          {...register('workshopName')}
        />
        <AuthInput
          label="Tu nombre"
          visibleLabel
          icon={<UserIcon />}
          autoComplete="name"
          placeholder="Ej: Carlos Rodríguez"
          error={errors.displayName?.message}
          {...register('displayName')}
        />
        <AuthInput
          label="Email"
          visibleLabel
          type="email"
          icon={<MailIcon />}
          autoComplete="email"
          placeholder="tu@taller.com"
          error={errors.email?.message}
          {...register('email')}
        />
        <AuthInput
          label="Contraseña"
          visibleLabel
          type="password"
          icon={<LockIcon />}
          autoComplete="new-password"
          placeholder="Mínimo 10 caracteres"
          hint="Usá al menos 10 caracteres."
          error={errors.password?.message}
          {...register('password')}
        />
        <AuthInput
          label="Confirmar contraseña"
          visibleLabel
          type="password"
          icon={<LockIcon />}
          autoComplete="new-password"
          placeholder="Repetí la contraseña"
          error={errors.confirmPassword?.message}
          {...register('confirmPassword')}
        />

        {serverError && (
          <AuthAlert>{serverError}</AuthAlert>
        )}

        <Button type="submit" loading={isSubmitting} className="mt-1 h-11 w-full gap-2 rounded-xl">
          <span className="inline-flex items-center gap-2">
            Crear cuenta
            <ArrowRightIcon />
          </span>
        </Button>
      </form>
    </AuthCard>
  )
}
