import { Route, Routes } from 'react-router'
import { LoginPage } from './login/LoginPage'
import { ForgotPasswordPage } from './forgot-password/ForgotPasswordPage'
import { ResetPasswordPage } from './reset-password/ResetPasswordPage'
import { AcceptInvitationPage } from './invitations/AcceptInvitationPage'
import { PublicQuotePage } from './quotes/PublicQuotePage'
import { PublicWorkOrderPage } from './work-orders/PublicWorkOrderPage'
import { PublicPaymentPage } from './payments/PublicPaymentPage'
import { PublicQrPage } from './qr/PublicQrPage'
import { OwnerVehiclePage } from './qr/OwnerVehiclePage'
import { PrivacidadPage } from './PrivacidadPage'

/** Área pública (`/*`): sin menú del panel. Liviana — no descarga el panel completo (doc 06 §1). */
export function Component() {
  return (
    <Routes>
      <Route path="login" element={<LoginPage />} />
      <Route path="forgot-password" element={<ForgotPasswordPage />} />
      <Route path="reset-password" element={<ResetPasswordPage />} />
      <Route path="invitations/:token/accept" element={<AcceptInvitationPage />} />
      {/* Rutas públicas de presupuestos y OT (compartidos via enlace) */}
      <Route path="p/:token" element={<PublicQuotePage />} />
      <Route path="o/:token" element={<PublicWorkOrderPage />} />
      <Route path="c/:token" element={<PublicPaymentPage />} />
      {/* Portal QR del dueño del vehículo */}
      <Route path="q/:code" element={<PublicQrPage />} />
      <Route path="q/:code/mi-vehiculo" element={<OwnerVehiclePage />} />
      {/* Aviso de privacidad (D-11) */}
      <Route path="privacidad" element={<PrivacidadPage />} />
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
