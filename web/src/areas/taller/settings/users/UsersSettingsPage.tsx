import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { usersApi, type MemberDto, type UpdateMembershipRequest, type TenantRole, type MembershipStatus } from '../../../../lib/api/users'
import { Button } from '../../../../components/ui/Button'
import { StatusBadge } from '../../../../components/ui/StatusBadge'
import { DataTable, RowAction, type DataTableColumn } from '../../../../components/ui/DataTable'
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

const COLUMNS: DataTableColumn<MemberDto>[] = [
  { key: 'name', header: 'Nombre', sortable: true, sortValue: m => m.displayName, cell: m => <span className="font-medium text-tc-text">{m.displayName}</span> },
  { key: 'email', header: 'Email', sortable: true, sortValue: m => m.email, cell: m => <span className="text-xs text-tc-text-secondary">{m.email}</span> },
  { key: 'role', header: 'Rol', widthClass: 'w-44', sortable: true, sortValue: m => ROLE_LABEL[m.role], cell: m => <span className="text-xs font-medium text-tc-text-secondary">{ROLE_LABEL[m.role]}</span> },
  {
    key: 'status',
    header: 'Estado',
    align: 'center',
    widthClass: 'w-40',
    sortable: true,
    sortValue: m => m.status,
    cell: m => <StatusBadge status={m.status} label={STATUS_LABEL[m.status]} />,
  },
]

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
        <h1 className="text-xl font-bold tracking-widest text-tc-text uppercase">Usuarios del taller</h1>
        <Button size="sm" onClick={() => setInviting(true)}>Invitar usuario</Button>
      </div>

      <DataTable<MemberDto>
        label="Usuarios del taller"
        columns={COLUMNS}
        rows={members}
        getRowId={m => m.membershipId}
        actions={m => (m.userId !== me?.user.id ? <RowAction tone="purple" onClick={() => setEditing(m)}>Editar</RowAction> : null)}
        renderCard={m => (
          <>
            <p className="truncate text-sm font-medium text-tc-text">{m.displayName}</p>
            <p className="truncate text-xs text-tc-text-secondary">{m.email}</p>
            <div className="mt-1.5 flex flex-wrap items-center gap-x-3 gap-y-1">
              <StatusBadge status={m.status} label={STATUS_LABEL[m.status]} />
              <span className="text-xs font-medium text-tc-text-secondary">{ROLE_LABEL[m.role]}</span>
            </div>
          </>
        )}
        emptyMessage="No hay usuarios."
      />

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
