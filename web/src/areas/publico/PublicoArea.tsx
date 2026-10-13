import { Route, Routes } from 'react-router'
import { LoginPage } from './login/LoginPage'
import { ForgotPasswordPage } from './forgot-password/ForgotPasswordPage'
import { ResetPasswordPage } from './reset-password/ResetPasswordPage'
import { AcceptInvitationPage } from './invitations/AcceptInvitationPage'

/** Área pública (`/*`): sin menú del panel. Liviana — no descarga el panel completo (doc 06 §1). */
export function Component() {
  return (
    <Routes>
      <Route path="login" element={<LoginPage />} />
      <Route path="forgot-password" element={<ForgotPasswordPage />} />
      <Route path="reset-password" element={<ResetPasswordPage />} />
      <Route path="invitations/:token/accept" element={<AcceptInvitationPage />} />
      <Route
        path="*"
        element={
          <div className="flex min-h-screen items-center justify-center bg-white text-slate-900">
            <h1 className="text-xl font-semibold">Página no encontrada</h1>
          </div>
        }
      />
    </Routes>
  )
}
