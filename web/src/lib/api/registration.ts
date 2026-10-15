import { api } from './client'

export interface RegisterRequest {
  workshopName: string
  displayName: string
  email: string
  password: string
}

export interface RegisterResponse {
  email: string
}

export interface VerifyEmailResponse {
  success: boolean
  message: string
}

export const registrationApi = {
  register: (body: RegisterRequest) =>
    api.post<RegisterResponse>('/api/public/v1/register', body),
  verifyEmail: (token: string) =>
    api.get<VerifyEmailResponse>(`/api/public/v1/verify-email?token=${encodeURIComponent(token)}`),
}
