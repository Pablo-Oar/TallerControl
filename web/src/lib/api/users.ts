import { api } from './client'

export type TenantRole = 'Owner' | 'AdminStaff' | 'Mechanic'
export type MembershipStatus = 'Active' | 'Inactive'

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
  create: (body: {
    email: string
    displayName: string
    role: TenantRole
    permissions?: string[]
  }) => api.post('/api/v1/invitations', body, true),
  accept: (token: string, password: string) =>
    api.post<void>(`/api/public/v1/invitations/${token}/accept`, { password }),
}
