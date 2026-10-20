import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useState } from 'react'
import { Button } from '../../../../components/ui/Button'
import { FormField, FormSelect } from '../../../../components/ui/FormField'
import { Modal } from '../../../../components/ui/Modal'
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
    <Modal title="Invitar usuario" onClose={onClose} className="max-w-sm">
      <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-4">
        <FormField label="Email" type="email" error={errors.email?.message} {...register('email')} />
        <FormField label="Nombre" error={errors.displayName?.message} {...register('displayName')} />
        <FormSelect label="Rol" {...register('role')}>
          <option value="owner">Dueño</option>
          <option value="admin_staff">Administrativo</option>
          <option value="mechanic">Mecánico</option>
        </FormSelect>

        {serverError && (
          <p role="alert" className="border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
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
    </Modal>
  )
}
