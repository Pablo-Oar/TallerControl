import { formatDateTime } from '../../../../lib/dates'
import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { usersApi, invitationsApi, ROLE_LABELS, type MemberDto, type PendingInvitationDto, type UpdateMembershipRequest, type MembershipStatus } from '../../../../lib/api/users'
import { Button } from '../../../../components/ui/Button'
import { StatusBadge } from '../../../../components/ui/StatusBadge'
import { DataTable, RowAction, type DataTableColumn } from '../../../../components/ui/DataTable'
import { ApiError } from '../../../../lib/api/client'
import { InviteMemberModal } from './InviteMemberModal'
import { EditMemberModal } from './EditMemberModal'
import { useAuth } from '../../../../lib/auth/AuthContext'
import { confirmDialog, notify } from '../../../../components/ui/dialogs'

const ROLE_LABEL = ROLE_LABELS

const STATUS_LABEL: Record<MembershipStatus, string> = {
  active: 'Activo',
  disabled: 'Desactivado',
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

  const { data: invitations = [] } = useQuery({
    queryKey: ['invitations'],
    queryFn: () => invitationsApi.list(),
  })

  async function resend(inv: PendingInvitationDto) {
    try {
      await invitationsApi.resend(inv.id)
      queryClient.invalidateQueries({ queryKey: ['invitations'] })
      notify(`Le reenviamos la invitación a ${inv.email}.`, 'info')
    } catch (e) {
      notify(e instanceof ApiError ? e.detail : 'No se pudo reenviar la invitación')
    }
  }

  async function cancelInvitation(inv: PendingInvitationDto) {
    if (!(await confirmDialog(`¿Cancelar la invitación a ${inv.email}? El enlace que recibió deja de funcionar.`, { danger: true }))) return
    try {
      await invitationsApi.cancel(inv.id)
      queryClient.invalidateQueries({ queryKey: ['invitations'] })
    } catch (e) {
      notify(e instanceof ApiError ? e.detail : 'No se pudo cancelar la invitación')
    }
  }

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
      <div className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2">
        <h1 className="text-lg font-bold tracking-wide text-tc-text uppercase">Usuarios del taller</h1>
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

      {invitations.length > 0 && (
        <section aria-label="Invitaciones pendientes" className="overflow-hidden border border-tc-border bg-white">
          <h2 className="border-b border-tc-border bg-tc-stub-bg px-4 py-2 text-[11px] font-bold tracking-[0.14em] text-tc-text-muted uppercase">
            Invitaciones pendientes
          </h2>
          <ul className="divide-y divide-tc-border">
            {invitations.map(inv => (
              <li key={inv.id} className="flex flex-wrap items-center gap-x-4 gap-y-1 px-4 py-2.5 text-sm">
                <span className="min-w-0">
                  <span className="block truncate font-medium text-tc-text">{inv.displayName ?? inv.email}</span>
                  {inv.displayName && <span className="block truncate text-xs text-tc-text-secondary">{inv.email}</span>}
                </span>
                <span className="text-xs text-tc-text-secondary">{ROLE_LABEL[inv.role]}</span>
                <span className={`text-xs font-bold tracking-wider uppercase ${inv.isExpired ? 'text-tc-stamp-red' : 'text-tc-text-muted'}`}>
                  {inv.isExpired ? 'Vencida' : `Vence ${formatDateTime(inv.expiresAt)}`}
                </span>
                <span className="ml-auto flex gap-3">
                  <button type="button" onClick={() => resend(inv)} className="text-xs font-bold tracking-wider text-tc-purple uppercase hover:opacity-70">Reenviar</button>
                  <button type="button" onClick={() => cancelInvitation(inv)} className="text-xs font-bold tracking-wider text-tc-stamp-red uppercase hover:opacity-70">Cancelar</button>
                </span>
              </li>
            ))}
          </ul>
        </section>
      )}

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
          onSuccess={() => { setInviting(false); queryClient.invalidateQueries({ queryKey: ['members'] }); queryClient.invalidateQueries({ queryKey: ['invitations'] }) }}
          onClose={() => setInviting(false)}
        />
      )}
    </div>
  )
}
