import { api } from './client'

export interface MeResponse {
  user: { id: string; email: string; displayName: string }
  tenant: { id: string; name: string }
  role: 'Owner' | 'AdminStaff' | 'Mechanic'
  permissions: string[]
  timeZone: string
  home: string
}

export interface LoginRequest {
  email: string
  password: string
  rememberMe?: boolean
}

export const authApi = {
  me: () => api.get<MeResponse>('/api/v1/auth/me'),
  login: (body: LoginRequest) => api.post<void>('/api/v1/auth/login', body, true),
  logout: () => api.post<void>('/api/v1/auth/logout', undefined, true),
  forgotPassword: (email: string) =>
    api.post<void>('/api/v1/auth/forgot-password', { email }),
  resetPassword: (token: string, newPassword: string) =>
    api.post<void>('/api/v1/auth/reset-password', { token, newPassword }),
}
