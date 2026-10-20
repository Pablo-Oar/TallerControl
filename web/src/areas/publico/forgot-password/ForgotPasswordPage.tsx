import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useState } from 'react'
import { Link } from 'react-router'
import { FormField } from '../../../components/ui/FormField'
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
    <div className="flex min-h-screen items-center justify-center bg-tc-navy px-4">
      <div className="w-full max-w-sm border border-tc-border bg-white">
        <div className="border-b border-tc-border bg-tc-stub-bg px-6 py-3">
          <span className="text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">TallerControl</span>
        </div>
        <div className="px-6 py-8">
          <h1 className="mb-2 text-xl font-bold tracking-tight text-slate-900">Recuperar contraseña</h1>

          {sent ? (
            <div className="mt-4">
              <p className="text-sm text-slate-600">
                Si el email está registrado, te enviamos un enlace para restablecer tu contraseña.
                Revisá también tu carpeta de spam.
              </p>
              <Link to="/login" className="mt-4 block text-center text-sm text-tc-purple hover:opacity-70">
                Volver al inicio
              </Link>
            </div>
          ) : (
            <>
              <p className="mb-6 text-sm text-slate-500">
                Ingresá tu email y te enviaremos un enlace para restablecer la contraseña.
              </p>
              <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-4">
                <FormField
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
                <Link to="/login" className="text-tc-purple hover:opacity-70">
                  Volver al inicio
                </Link>
              </p>
            </>
          )}
        </div>
      </div>
    </div>
  )
}
