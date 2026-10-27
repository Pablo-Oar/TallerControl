import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useState } from 'react'
import { Button } from '../../../../components/ui/Button'
import { FormField, FormSelect } from '../../../../components/ui/FormField'
import { Modal } from '../../../../components/ui/Modal'
import { invitationsApi, MECHANIC_EXTRA_PERMISSIONS, type TenantRole } from '../../../../lib/api/users'
import { ApiError } from '../../../../lib/api/client'

const schema = z.object({
  email: z.string().email('Email inválido'),
  displayName: z.string().min(1, 'Ingresá el nombre'),
  role: z.enum(['admin_staff', 'mechanic', 'accountant']),
})

type FormData = z.infer<typeof schema>

interface Props {
  onSuccess: () => void
  onClose: () => void
}

export function InviteMemberModal({ onSuccess, onClose }: Props) {
  const [serverError, setServerError] = useState<string | null>(null)
  const [extra, setExtra] = useState<string[]>([])
  const { register, handleSubmit, watch, formState: { errors, isSubmitting } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { role: 'mechanic' },
  })
  const role = watch('role')

  async function onSubmit(data: FormData) {
    setServerError(null)
    try {
      await invitationsApi.create({
        email: data.email,
        displayName: data.displayName,
        role: data.role as TenantRole,
        permissions: data.role === 'mechanic' && extra.length > 0 ? extra : undefined,
      })
      onSuccess()
    } catch (err) {
      if (err instanceof ApiError && (err.status === 409 || err.status === 422)) {
        // El servidor explica el motivo: ya forma parte del taller, ya hay una invitación pendiente, permiso inválido…
        setServerError(err.detail)
      } else {
        setServerError('No se pudo enviar la invitación. Intentá de nuevo.')
      }
    }
  }

  return (
    <Modal title="Invitar usuario" onClose={onClose} className="max-w-sm">
      <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-4">
        <FormField label="Email" type="email" error={errors.email?.message} {...register('email')} />
        <FormField label="Nombre" error={errors.displayName?.message} {...register('displayName')} />
        <FormSelect label="Rol" {...register('role')}>
          <option value="admin_staff">Administrativo</option>
          <option value="mechanic">Mecánico</option>
          <option value="accountant">Contador (solo consulta de facturación)</option>
        </FormSelect>

        {role === 'mechanic' && (
          <fieldset className="space-y-1.5">
            <legend className="mb-1 text-xs font-bold tracking-wider text-tc-text-muted uppercase">Permisos adicionales</legend>
            {MECHANIC_EXTRA_PERMISSIONS.map(p => (
              <label key={p.value} className="flex items-start gap-2 text-sm text-tc-text-secondary">
                <input
                  type="checkbox"
                  className="mt-1 accent-tc-purple"
                  checked={extra.includes(p.value)}
                  onChange={e => setExtra(cur => (e.target.checked ? [...cur, p.value] : cur.filter(x => x !== p.value)))}
                />
                <span>
                  {p.label}
                  <span className="block text-xs text-tc-text-muted">{p.hint}</span>
                </span>
              </label>
            ))}
          </fieldset>
        )}

        {serverError && (
          <p role="alert" className="border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {serverError}
          </p>
        )}

        <div className="sticky bottom-0 -mx-5 -mb-5 mt-2 flex justify-end gap-3 border-t border-tc-border bg-white px-5 pt-3 pb-[max(0.75rem,env(safe-area-inset-bottom))] shadow-[0_-6px_8px_-6px_rgb(0_0_0/0.12)]">
          <Button type="button" variant="secondary" onClick={onClose}>
            Cancelar
          </Button>
          <Button type="submit" loading={isSubmitting}>
            Enviar invitación
          </Button>
        </div>
      </form>
    </Modal>
  )
}
