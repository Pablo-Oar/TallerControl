import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { usersApi, type MemberDto, type UpdateMembershipRequest, type TenantRole, type MembershipStatus } from '../../../../lib/api/users'
import { Button } from '../../../../components/ui/Button'
import { ApiError } from '../../../../lib/api/client'
import { InviteMemberModal } from './InviteMemberModal'
import { EditMemberModal } from './EditMemberModal'
import { useAuth } from '../../../../lib/auth/AuthContext'

const ROLE_LABEL: Record<TenantRole, string> = {
  Owner: 'Dueño',
  AdminStaff: 'Administrativo',
  Mechanic: 'Mecánico',
}

const STATUS_LABEL: Record<MembershipStatus, string> = {
  Active: 'Activo',
  Inactive: 'Inactivo',
}

export function UsersSettingsPage() {
  const queryClient = useQueryClient()
  const { user: me } = useAuth()
  const [editing, setEditing] = useState<MemberDto | null>(null)
  const [inviting, setInviting] = useState(false)

  const { data: members = [], isLoading, error } = useQuery({
    queryKey: ['members'],
    queryFn: () => usersApi.listMembers(),
  })

  const updateMutation = useMutation({
    mutationFn: ({ id, body, version }: { id: string; body: UpdateMembershipRequest; version: number }) =>
      usersApi.updateMember(id, body, version),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['members'] })
      setEditing(null)
    },
  })

  if (isLoading) {
    return <div className="flex justify-center py-12"><div className="h-6 w-6 animate-spin rounded-full border-2 border-blue-600 border-t-transparent" /></div>
  }

  if (error) {
    return <p className="text-sm text-red-600">No se pudo cargar la lista de usuarios.</p>
  }

  return (
    <div>
      <div className="mb-6 flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Usuarios del taller</h1>
        <Button onClick={() => setInviting(true)}>Invitar usuario</Button>
      </div>

      <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
        <table className="w-full text-sm">
          <thead className="bg-slate-50 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">
            <tr>
              <th className="px-4 py-3">Nombre</th>
              <th className="px-4 py-3">Email</th>
              <th className="px-4 py-3">Rol</th>
              <th className="px-4 py-3">Estado</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {members.map((m) => (
              <tr key={m.membershipId} className="hover:bg-slate-50">
                <td className="px-4 py-3 font-medium">{m.displayName}</td>
                <td className="px-4 py-3 text-slate-500">{m.email}</td>
                <td className="px-4 py-3">{ROLE_LABEL[m.role]}</td>
                <td className="px-4 py-3">
                  <span className={`inline-block rounded-full px-2 py-0.5 text-xs font-medium ${m.status === 'Active' ? 'bg-green-100 text-green-700' : 'bg-slate-100 text-slate-500'}`}>
                    {STATUS_LABEL[m.status]}
                  </span>
                </td>
                <td className="px-4 py-3 text-right">
                  {/* No editar la propia membresía desde esta pantalla */}
                  {m.userId !== me?.user.id && (
                    <button
                      onClick={() => setEditing(m)}
                      className="text-xs text-blue-600 hover:underline"
                    >
                      Editar
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {editing && (
        <EditMemberModal
          member={editing}
          error={updateMutation.error instanceof ApiError ? updateMutation.error.detail : null}
          saving={updateMutation.isPending}
          onSave={(body) => updateMutation.mutate({ id: editing.membershipId, body, version: editing.version })}
          onClose={() => { setEditing(null); updateMutation.reset() }}
        />
      )}

      {inviting && (
        <InviteMemberModal
          onSuccess={() => { setInviting(false); queryClient.invalidateQueries({ queryKey: ['members'] }) }}
          onClose={() => setInviting(false)}
        />
      )}
    </div>
  )
}
