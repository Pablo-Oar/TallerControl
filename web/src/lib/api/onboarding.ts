import { api } from './client'

export interface OnboardingStepDto {
  key: string
  label: string
  completed: boolean
}

export interface OnboardingDto {
  steps: OnboardingStepDto[]
  completedCount: number
  totalCount: number
  isComplete: boolean
}

export const onboardingApi = {
  get: () => api.get<OnboardingDto>('/api/v1/onboarding'),
}
