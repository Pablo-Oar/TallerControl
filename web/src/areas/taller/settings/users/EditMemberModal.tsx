import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import type { MemberDto, UpdateMembershipRequest, TenantRole, MembershipStatus } from '../../../../lib/api/users'
import { Button } from '../../../../components/ui/Button'

const schema = z.object({
  role: z.enum(['Owner', 'AdminStaff', 'Mechanic']),
  status: z.enum(['Active', 'Inactive']),
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
            <label className="text-sm font-medium text-slate-700">Rol</label>
            <select
              {...register('role')}
              className="rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-blue-500"
            >
              <option value="Owner">Dueño</option>
              <option value="AdminStaff">Administrativo</option>
              <option value="Mechanic">Mecánico</option>
            </select>
          </div>
          <div className="flex flex-col gap-1">
            <label className="text-sm font-medium text-slate-700">Estado</label>
            <select
              {...register('status')}
              className="rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-blue-500"
            >
              <option value="Active">Activo</option>
              <option value="Inactive">Inactivo</option>
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
