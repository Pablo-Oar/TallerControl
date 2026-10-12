import { Route, Routes } from 'react-router'

/** Área pública (`/q`, `/p`, `/o`, `/c`, `/login`…): liviana, sin el menú del panel. */
export function Component() {
  return (
    <div className="min-h-screen bg-white text-slate-900">
      <main className="mx-auto max-w-md p-4">
        <Routes>
          <Route path="login" element={<h1 className="text-xl font-semibold">Ingresar</h1>} />
          <Route path="*" element={<h1 className="text-xl font-semibold">Página no encontrada</h1>} />
        </Routes>
      </main>
    </div>
  )
}