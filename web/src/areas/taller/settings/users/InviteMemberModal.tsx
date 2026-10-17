import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useState } from 'react'
import { Input } from '../../../../components/ui/Input'
import { Button } from '../../../../components/ui/Button'
import { invitationsApi, type TenantRole } from '../../../../lib/api/users'
import { ApiError } from '../../../../lib/api/client'

const schema = z.object({
  email: z.string().email('Email inválido'),
  displayName: z.string().min(1, 'Ingresá el nombre'),
  role: z.enum(['owner', 'admin_staff', 'mechanic']),
})

type FormData = z.infer<typeof schema>

interface Props {
  onSuccess: () => void
  onClose: () => void
}

export function InviteMemberModal({ onSuccess, onClose }: Props) {
  const [serverError, setServerError] = useState<string | null>(null)
  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { role: 'mechanic' },
  })

  async function onSubmit(data: FormData) {
    setServerError(null)
    try {
      await invitationsApi.create({ email: data.email, displayName: data.displayName, role: data.role as TenantRole })
      onSuccess()
    } catch (err) {
      if (err instanceof ApiError && err.status === 409) {
        setServerError('Ya existe un usuario con ese email en el taller.')
      } else {
        setServerError('No se pudo enviar la invitación. Intentá de nuevo.')
      }
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="w-full max-w-sm rounded-xl bg-white p-6 shadow-lg">
        <h2 className="mb-4 text-lg font-semibold">Invitar usuario</h2>
        <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-4">
          <Input label="Email" type="email" error={errors.email?.message} {...register('email')} />
          <Input label="Nombre" error={errors.displayName?.message} {...register('displayName')} />
          <div className="flex flex-col gap-1">
            <label className="text-sm font-medium text-slate-700">Rol</label>
            <select
              {...register('role')}
              className="rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-blue-500"
            >
              <option value="owner">Dueño</option>
              <option value="admin_staff">Administrativo</option>
              <option value="mechanic">Mecánico</option>
            </select>
          </div>

          {serverError && (
            <p role="alert" className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">
              {serverError}
            </p>
          )}

          <div className="flex justify-end gap-3 pt-2">
            <Button type="button" variant="secondary" onClick={onClose}>
              Cancelar
            </Button>
            <Button type="submit" loading={isSubmitting}>
              Enviar invitación
            </Button>
          </div>
        </form>
      </div>
    </div>
  )
}
