import { type RouteObject } from 'react-router'

// Cada área se carga por separado (code splitting por ruta) para que el celular
// de quien escanea un QR no descargue el panel completo (doc 06 §1).
export const appRoutes: RouteObject[] = [
  {
    path: '/app/*',
    lazy: () => import('../areas/taller/TallerArea.tsx'),
  },
  {
    path: '/m/*',
    lazy: () => import('../areas/mecanico/MecanicoArea.tsx'),
  },
  {
    path: '/*',
    lazy: () => import('../areas/publico/PublicoArea.tsx'),
  },
]