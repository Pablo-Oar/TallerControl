import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useAuth } from '../../../../lib/auth/AuthContext'
import { authApi } from '../../../../lib/api/auth'
import { ApiError } from '../../../../lib/api/client'
import { Button } from '../../../../components/ui/Button'
import { FormField } from '../../../../components/ui/FormField'
import { ROLE_LABELS } from '../../../../lib/api/users'

const nameSchema = z.object({ displayName: z.string().trim().min(1, 'El nombre es obligatorio').max(200) })

const passwordSchema = z
  .object({
    currentPassword: z.string().min(1, 'Ingresá tu contraseña actual'),
    newPassword: z.string().min(10, 'Mínimo 10 caracteres'),
    confirm: z.string(),
  })
  .refine(v => v.newPassword === v.confirm, { path: ['confirm'], message: 'Las contraseñas no coinciden' })

function serverMessage(err: unknown, fallback: string): string {
  if (err instanceof ApiError) {
    const detail = err.extensions?.['detail'] ?? err.extensions?.['message']
    if (typeof detail === 'string' && detail) return detail
  }
  return fallback
}

/** U-02: datos propios del usuario (nombre y contraseña). El email y el rol los administra el dueño. */
export function ProfilePage() {
  const { user, invalidate } = useAuth()
  const [nameMsg, setNameMsg] = useState<{ ok: boolean; text: string } | null>(null)
  const [pwdMsg, setPwdMsg] = useState<{ ok: boolean; text: string } | null>(null)

  const nameForm = useForm<z.infer<typeof nameSchema>>({
    resolver: zodResolver(nameSchema),
    defaultValues: { displayName: user?.user.displayName ?? '' },
  })
  const pwdForm = useForm<z.infer<typeof passwordSchema>>({ resolver: zodResolver(passwordSchema) })

  async function saveName(v: z.infer<typeof nameSchema>) {
    setNameMsg(null)
    try {
      await authApi.updateProfile(v.displayName)
      invalidate()
      setNameMsg({ ok: true, text: 'Nombre actualizado.' })
    } catch (err) {
      setNameMsg({ ok: false, text: serverMessage(err, 'No se pudo guardar el nombre.') })
    }
  }

  async function savePassword(v: z.infer<typeof passwordSchema>) {
    setPwdMsg(null)
    try {
      await authApi.changePassword(v.currentPassword, v.newPassword)
      pwdForm.reset()
      setPwdMsg({ ok: true, text: 'Contraseña cambiada. Se cerraron tus otras sesiones.' })
    } catch (err) {
      setPwdMsg({ ok: false, text: serverMessage(err, 'No se pudo cambiar la contraseña.') })
    }
  }

  return (
    <div className="grid max-w-5xl content-start gap-x-12 gap-y-8 lg:grid-cols-2">
      <header className="lg:col-span-2">
        <h1 className="text-lg font-bold tracking-wide text-tc-text uppercase">Mi perfil</h1>
        <p className="mt-1 text-sm text-tc-text-secondary">
          {user?.user.email} · {user ? (ROLE_LABELS[user.role] ?? user.role) : ''}
        </p>
      </header>

      <form onSubmit={nameForm.handleSubmit(saveName)} className="space-y-4" noValidate>
        <h2 className="text-sm font-bold uppercase tracking-wider text-tc-text-secondary">Nombre</h2>
        <FormField label="Tu nombre" error={nameForm.formState.errors.displayName?.message} {...nameForm.register('displayName')} />
        {nameMsg && (
          <p role={nameMsg.ok ? 'status' : 'alert'} className={nameMsg.ok ? 'text-sm text-green-700' : 'text-sm text-red-700'}>
            {nameMsg.text}
          </p>
        )}
        <Button type="submit" loading={nameForm.formState.isSubmitting}>Guardar nombre</Button>
      </form>

      <form onSubmit={pwdForm.handleSubmit(savePassword)} className="space-y-4" noValidate>
        <h2 className="text-sm font-bold uppercase tracking-wider text-tc-text-secondary">Cambiar contraseña</h2>
        <FormField label="Contraseña actual" type="password" autoComplete="current-password"
          error={pwdForm.formState.errors.currentPassword?.message} {...pwdForm.register('currentPassword')} />
        <FormField label="Contraseña nueva" type="password" autoComplete="new-password"
          error={pwdForm.formState.errors.newPassword?.message} {...pwdForm.register('newPassword')} />
        <FormField label="Repetí la contraseña nueva" type="password" autoComplete="new-password"
          error={pwdForm.formState.errors.confirm?.message} {...pwdForm.register('confirm')} />
        {pwdMsg && (
          <p role={pwdMsg.ok ? 'status' : 'alert'} className={pwdMsg.ok ? 'text-sm text-green-700' : 'text-sm text-red-700'}>
            {pwdMsg.text}
          </p>
        )}
        <Button type="submit" loading={pwdForm.formState.isSubmitting}>Cambiar contraseña</Button>
      </form>
    </div>
  )
}
