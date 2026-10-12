import { Route, Routes } from 'react-router'

/** Área del mecánico (`/m/*`): celular primero. */
export function Component() {
  return (
    <div className="min-h-screen bg-slate-50 text-slate-900">
      <main className="p-4">
        <Routes>
          <Route index element={<h1 className="text-xl font-semibold">Mis trabajos</h1>} />
        </Routes>
      </main>
    </div>
  )
}