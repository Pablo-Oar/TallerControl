import { lazy, Suspense } from 'react'
import { Route, Routes } from 'react-router'

const LandingPage = lazy(() => import('./landing/LandingPage').then(m => ({ default: m.LandingPage })))
const LoginPage = lazy(() => import('./login/LoginPage').then(m => ({ default: m.LoginPage })))
const ForgotPasswordPage = lazy(() => import('./forgot-password/ForgotPasswordPage').then(m => ({ default: m.ForgotPasswordPage })))
const ResetPasswordPage = lazy(() => import('./reset-password/ResetPasswordPage').then(m => ({ default: m.ResetPasswordPage })))
const AcceptInvitationPage = lazy(() => import('./invitations/AcceptInvitationPage').then(m => ({ default: m.AcceptInvitationPage })))
const PublicQuotePage = lazy(() => import('./quotes/PublicQuotePage').then(m => ({ default: m.PublicQuotePage })))
const PublicWorkOrderPage = lazy(() => import('./work-orders/PublicWorkOrderPage').then(m => ({ default: m.PublicWorkOrderPage })))
const PublicPaymentPage = lazy(() => import('./payments/PublicPaymentPage').then(m => ({ default: m.PublicPaymentPage })))
const PublicQrPage = lazy(() => import('./qr/PublicQrPage').then(m => ({ default: m.PublicQrPage })))
const OwnerVehiclePage = lazy(() => import('./qr/OwnerVehiclePage').then(m => ({ default: m.OwnerVehiclePage })))
const PrivacidadPage = lazy(() => import('./PrivacidadPage').then(m => ({ default: m.PrivacidadPage })))
const RegisterPage = lazy(() => import('./registro/RegisterPage').then(m => ({ default: m.RegisterPage })))
const VerifyEmailPage = lazy(() => import('./verificar-email/VerifyEmailPage').then(m => ({ default: m.VerifyEmailPage })))

function PageFallback() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-white">
      <div className="h-5 w-5 animate-spin border-2 border-tc-purple border-t-transparent" />
    </div>
  )
}

/** Área pública (`/*`): sin menú del panel. Liviana — no descarga el panel completo (doc 06 §1). */
export function Component() {
  return (
    <Suspense fallback={<PageFallback />}>
      <Routes>
        {/* Landing page (D-23) */}
        <Route index element={<LandingPage />} />
        {/* Autenticación */}
        <Route path="login" element={<LoginPage />} />
        <Route path="registro" element={<RegisterPage />} />
        <Route path="verificar-email" element={<VerifyEmailPage />} />
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
            <div className="flex min-h-screen items-center justify-center bg-white text-tc-text">
              <h1 className="text-xl font-semibold">Página no encontrada</h1>
            </div>
          }
        />
      </Routes>
    </Suspense>
  )
}
