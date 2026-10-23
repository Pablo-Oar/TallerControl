import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useState } from 'react'
import { MECHANIC_EXTRA_PERMISSIONS, type MemberDto, type UpdateMembershipRequest, type TenantRole, type MembershipStatus } from '../../../../lib/api/users'
import { FormSelect } from '../../../../components/ui/FormField'
import { EditDialog } from '../../../../components/ui/EditDialog'

const schema = z.object({
  role: z.enum(['owner', 'admin_staff', 'mechanic', 'accountant']),
  status: z.enum(['active', 'disabled']),
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
  const [extra, setExtra] = useState<string[]>(member.permissions.filter(p => MECHANIC_EXTRA_PERMISSIONS.some(x => x.value === p)))
  const { register, handleSubmit, watch, formState: { isDirty } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { role: member.role, status: member.status },
  })

  const role = watch('role')
  const originalExtra = member.permissions.filter(p => MECHANIC_EXTRA_PERMISSIONS.some(x => x.value === p))
  const extraChanged = extra.length !== originalExtra.length || extra.some(p => !originalExtra.includes(p))

  function onSubmit(data: FormData) {
    const body: UpdateMembershipRequest = {}
    if (data.role !== member.role) body.role = data.role as TenantRole
    if (data.status !== member.status) body.status = data.status as MembershipStatus
    // Los permisos extra solo existen para mecánicos; al cambiar de rol se vacían.
    if (data.role === 'mechanic' && (extraChanged || data.role !== member.role)) body.permissions = extra
    onSave(body)
  }

  return (
    <EditDialog
      title={`Editar ${member.displayName}`}
      onClose={onClose}
      onSubmit={handleSubmit(onSubmit)}
      dirty={isDirty || extraChanged}
      saving={saving}
      error={error}
      requireChanges
      className="max-w-sm"
    >
      <FormSelect label="Rol" {...register('role')}>
        <option value="owner">Dueño</option>
        <option value="admin_staff">Administrativo</option>
        <option value="mechanic">Mecánico</option>
        <option value="accountant">Contador</option>
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
      <FormSelect label="Estado" {...register('status')}>
        <option value="active">Activo</option>
        <option value="disabled">Desactivado (no puede ingresar)</option>
      </FormSelect>
    </EditDialog>
  )
}
