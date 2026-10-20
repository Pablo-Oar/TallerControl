import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useState } from 'react'
import { Link } from 'react-router'
import { AuthCard, AuthInput } from '../../../components/ui/AuthCard'
import { MailIcon } from '../../../components/ui/icons'
import { Button } from '../../../components/ui/Button'
import { authApi } from '../../../lib/api/auth'

const schema = z.object({
  email: z.string().email('Email inválido'),
})

type FormData = z.infer<typeof schema>

export function ForgotPasswordPage() {
  const [sent, setSent] = useState(false)
  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<FormData>({
    resolver: zodResolver(schema),
  })

  async function onSubmit(data: FormData) {
    // La API siempre devuelve 202 (no revela si el email existe, T-AUTH-07)
    await authApi.forgotPassword(data.email).catch(() => null)
    setSent(true)
  }

  return (
    <AuthCard
      title="Recuperar contraseña"
      subtitle={sent ? undefined : 'Ingresá tu email y te enviaremos un enlace para restablecerla.'}
      footer={
        <Link to="/login" className="font-semibold text-tc-purple hover:underline">
          Volver al inicio de sesión
        </Link>
      }
    >
      {sent ? (
        <p className="text-center text-sm text-tc-text-secondary">
          Si el email está registrado, te enviamos un enlace para restablecer tu contraseña. Revisá también tu
          carpeta de spam.
        </p>
      ) : (
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
          <Button type="submit" loading={isSubmitting} className="mt-1 h-11 w-full rounded-xl">
            Enviar enlace
          </Button>
        </form>
      )}
    </AuthCard>
  )
}
