import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import type { MemberDto, UpdateMembershipRequest, TenantRole, MembershipStatus } from '../../../../lib/api/users'
import { Button } from '../../../../components/ui/Button'
import { FormSelect } from '../../../../components/ui/FormField'
import { Modal } from '../../../../components/ui/Modal'

const schema = z.object({
  role: z.enum(['owner', 'admin_staff', 'mechanic']),
  status: z.enum(['active', 'inactive']),
})

type FormData = z.infer<typeof schema>

interface Props {
  member: MemberDto
  saving: boolean
  error: string | null
  onSave: (body: UpdateMembershipRequest) => void
  onClose: () => void
}

export function EditMemberModal({ member, saving, error, onSave, onClose }: Props) {
  const { register, handleSubmit } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { role: member.role, status: member.status },
  })

  function onSubmit(data: FormData) {
    const body: UpdateMembershipRequest = {}
    if (data.role !== member.role) body.role = data.role as TenantRole
    if (data.status !== member.status) body.status = data.status as MembershipStatus
    onSave(body)
  }

  return (
    <Modal title={`Editar ${member.displayName}`} onClose={onClose} className="max-w-sm">
      <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
        <FormSelect label="Rol" {...register('role')}>
          <option value="owner">Dueño</option>
          <option value="admin_staff">Administrativo</option>
          <option value="mechanic">Mecánico</option>
        </FormSelect>
        <FormSelect label="Estado" {...register('status')}>
          <option value="active">Activo</option>
          <option value="inactive">Inactivo</option>
        </FormSelect>

        {error && (
          <p role="alert" className="border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {error}
          </p>
        )}

        <div className="flex justify-end gap-3 pt-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            Cancelar
          </Button>
          <Button type="submit" loading={saving}>
            Guardar
          </Button>
        </div>
      </form>
    </Modal>
  )
}
