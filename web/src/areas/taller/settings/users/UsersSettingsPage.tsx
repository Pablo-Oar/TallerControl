import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { usersApi, type MemberDto, type UpdateMembershipRequest, type TenantRole, type MembershipStatus } from '../../../../lib/api/users'
import { Button } from '../../../../components/ui/Button'
import { StatusBadge } from '../../../../components/ui/StatusBadge'
import { ApiError } from '../../../../lib/api/client'
import { InviteMemberModal } from './InviteMemberModal'
import { EditMemberModal } from './EditMemberModal'
import { useAuth } from '../../../../lib/auth/AuthContext'

const ROLE_LABEL: Record<TenantRole, string> = {
  owner: 'Dueño',
  admin_staff: 'Administrativo',
  mechanic: 'Mecánico',
}

const STATUS_LABEL: Record<MembershipStatus, string> = {
  active: 'Activo',
  inactive: 'Inactivo',
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
    return (
      <div className="border border-tc-border bg-white">
        {[...Array(4)].map((_, i) => (
          <div key={i} className="h-12 animate-pulse border-b border-tc-border bg-tc-surface last:border-0" />
        ))}
      </div>
    )
  }

  if (error) {
    return <p className="text-sm text-tc-stamp-red">No se pudo cargar la lista de usuarios.</p>
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-bold tracking-widest text-slate-900 uppercase">Usuarios del taller</h1>
        <Button size="sm" onClick={() => setInviting(true)}>Invitar usuario</Button>
      </div>

      <div className="border border-tc-border bg-white overflow-hidden">
        <table className="w-full text-sm">
          <thead>
            <tr className="bg-tc-stub-bg border-b border-tc-border">
              <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Nombre</th>
              <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400 hidden sm:table-cell">Email</th>
              <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Rol</th>
              <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Estado</th>
              <th className="px-4 py-2" />
            </tr>
          </thead>
          <tbody>
            {members.map((m) => (
              <tr key={m.membershipId} className="border-b border-tc-border last:border-0 hover:bg-tc-surface">
                <td className="px-4 py-2.5 font-medium text-slate-800">{m.displayName}</td>
                <td className="px-4 py-2.5 text-slate-500 text-xs hidden sm:table-cell">{m.email}</td>
                <td className="px-4 py-2.5 text-slate-600 text-xs font-medium">{ROLE_LABEL[m.role]}</td>
                <td className="px-4 py-2.5">
                  <StatusBadge status={m.status} label={STATUS_LABEL[m.status]} />
                </td>
                <td className="px-4 py-2.5 text-right">
                  {m.userId !== me?.user.id && (
                    <button
                      onClick={() => setEditing(m)}
                      className="text-xs text-tc-purple hover:opacity-70"
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
