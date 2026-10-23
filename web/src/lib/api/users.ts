import { api } from './client'

export type TenantRole = 'owner' | 'admin_staff' | 'mechanic' | 'accountant'
export type MembershipStatus = 'active' | 'disabled'

export const ROLE_LABELS: Record<TenantRole, string> = {
  owner: 'Dueño',
  admin_staff: 'Administrativo',
  mechanic: 'Mecánico',
  accountant: 'Contador',
}

/** Permisos adicionales que el dueño puede darle a un mecánico (el servidor no acepta otros). */
export const MECHANIC_EXTRA_PERMISSIONS: { value: string; label: string; hint: string }[] = [
  { value: 'prices.view', label: 'Ver precios y montos', hint: 'Si no, no ve importes en las órdenes ni en el PDF.' },
  { value: 'contact.view', label: 'Ver teléfono y email de los clientes', hint: 'Si no, los datos de contacto no se le muestran.' },
]

export interface PendingInvitationDto {
  id: string
  email: string
  displayName: string | null
  role: TenantRole
  expiresAt: string
  isExpired: boolean
}

export interface MemberDto {
  membershipId: string
  userId: string
  displayName: string
  email: string
  role: TenantRole
  permissions: string[]
  status: MembershipStatus
  version: number
}

export interface UpdateMembershipRequest {
  role?: TenantRole
  permissions?: string[]
  status?: MembershipStatus
}

export const usersApi = {
  listMembers: () => api.get<MemberDto[]>('/api/v1/users'),
  updateMember: (membershipId: string, body: UpdateMembershipRequest, version: number) =>
    api.patch<MemberDto>(`/api/v1/users/${membershipId}`, body, `${version}`),
}

export const invitationsApi = {
  list: () => api.get<PendingInvitationDto[]>('/api/v1/invitations'),
  resend: (id: string) => api.post<PendingInvitationDto>(`/api/v1/invitations/${id}/resend`, {}, true),
  cancel: (id: string) => api.delete<void>(`/api/v1/invitations/${id}`, undefined, true),
  create: (body: {
    email: string
    displayName: string
    role: TenantRole
    permissions?: string[]
  }) => api.post('/api/v1/invitations', body, true),
  accept: (token: string, password: string) =>
    api.post<void>(`/api/public/v1/invitations/${token}/accept`, { password }),
}
