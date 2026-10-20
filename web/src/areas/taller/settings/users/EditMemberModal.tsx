import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import type { MemberDto, UpdateMembershipRequest, TenantRole, MembershipStatus } from '../../../../lib/api/users'
import { FormSelect } from '../../../../components/ui/FormField'
import { EditDialog } from '../../../../components/ui/EditDialog'

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
  const { register, handleSubmit, formState: { isDirty } } = useForm<FormData>({
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
    <EditDialog
      title={`Editar ${member.displayName}`}
      onClose={onClose}
      onSubmit={handleSubmit(onSubmit)}
      dirty={isDirty}
      saving={saving}
      error={error}
      requireChanges
      className="max-w-sm"
    >
      <FormSelect label="Rol" {...register('role')}>
        <option value="owner">Dueño</option>
        <option value="admin_staff">Administrativo</option>
        <option value="mechanic">Mecánico</option>
      </FormSelect>
      <FormSelect label="Estado" {...register('status')}>
        <option value="active">Activo</option>
        <option value="inactive">Inactivo</option>
      </FormSelect>
    </EditDialog>
  )
}
