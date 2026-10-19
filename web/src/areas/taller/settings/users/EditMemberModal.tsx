import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import type { MemberDto, UpdateMembershipRequest, TenantRole, MembershipStatus } from '../../../../lib/api/users'
import { Button } from '../../../../components/ui/Button'

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
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="w-full max-w-sm rounded-xl bg-white p-6 shadow-lg">
        <h2 className="mb-4 text-lg font-semibold">Editar {member.displayName}</h2>
        <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
          <div className="flex flex-col gap-1">
            <label htmlFor="edit-member-role" className="text-sm font-medium text-slate-700">Rol</label>
            <select
              id="edit-member-role"
              {...register('role')}
              className="border border-tc-border bg-white px-3 py-2 text-sm text-slate-800 outline-none focus:border-tc-border-mid transition-colors"
            >
              <option value="owner">Dueño</option>
              <option value="admin_staff">Administrativo</option>
              <option value="mechanic">Mecánico</option>
            </select>
          </div>
          <div className="flex flex-col gap-1">
            <label htmlFor="edit-member-status" className="text-sm font-medium text-slate-700">Estado</label>
            <select
              id="edit-member-status"
              {...register('status')}
              className="border border-tc-border bg-white px-3 py-2 text-sm text-slate-800 outline-none focus:border-tc-border-mid transition-colors"
            >
              <option value="active">Activo</option>
              <option value="inactive">Inactivo</option>
            </select>
          </div>

          {error && (
            <p role="alert" className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">
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
      </div>
    </div>
  )
}
