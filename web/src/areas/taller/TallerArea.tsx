import { Route, Routes } from 'react-router'

/** Área del taller (`/app/*`): dueño y administrativo. Escritorio primero. */
export function Component() {
  return (
    <div className="min-h-screen bg-slate-50 text-slate-900">
      <main className="p-6">
        <Routes>
          <Route index element={<h1 className="text-2xl font-semibold">Inicio</h1>} />
        </Routes>
      </main>
    </div>
  )
}