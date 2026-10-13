import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useState } from 'react'
import { Link } from 'react-router'
import { Input } from '../../../components/ui/Input'
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
    <div className="flex min-h-screen items-center justify-center bg-slate-50">
      <div className="w-full max-w-sm rounded-xl border border-slate-200 bg-white p-8 shadow-sm">
        <h1 className="mb-2 text-2xl font-semibold text-slate-900">Recuperar contraseña</h1>

        {sent ? (
          <div className="mt-4">
            <p className="text-sm text-slate-600">
              Si el email está registrado, te enviamos un enlace para restablecer tu contraseña.
              Revisá también tu carpeta de spam.
            </p>
            <Link to="/login" className="mt-4 block text-center text-sm text-blue-600 hover:underline">
              Volver al inicio
            </Link>
          </div>
        ) : (
          <>
            <p className="mb-6 text-sm text-slate-500">
              Ingresá tu email y te enviaremos un enlace para restablecer la contraseña.
            </p>
            <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-4">
              <Input
                label="Email"
                type="email"
                autoComplete="email"
                error={errors.email?.message}
                {...register('email')}
              />
              <Button type="submit" loading={isSubmitting} className="w-full">
                Enviar enlace
              </Button>
            </form>
            <p className="mt-4 text-center text-sm text-slate-500">
              <Link to="/login" className="text-blue-600 hover:underline">
                Volver al inicio
              </Link>
            </p>
          </>
        )}
      </div>
    </div>
  )
}
