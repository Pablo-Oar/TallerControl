// Service worker de TallerControl — área del mecánico (PWA F-51).
// Estrategia: network-first para navegaciones (HTML, así una versión nueva se ve sin recargar a mano),
// cache-first para assets con hash. Lectura sin conexión del mecánico (P-02): las órdenes que ya vio
// (GET /api/v1/work-orders…) y la sesión (/me) se guardan en DATA y se sirven si no hay red; el resto de /api/*
// no se intercepta. DATA se vacía al cerrar sesión (mensaje 'clear-data'). Subir CACHE en cada release de UI.
const CACHE = 'tc-sw-v6'
const DATA = 'tc-data-v1'

/** Lecturas que se guardan para verlas sin conexión (nunca escrituras ni documentos del taller en general). */
function isOfflineReadable(url) {
  return url.pathname === '/api/v1/auth/me' || url.pathname.startsWith('/api/v1/work-orders')
}

self.addEventListener('install', () => self.skipWaiting())

self.addEventListener('activate', event => {
  event.waitUntil(
    caches.keys().then(keys =>
      Promise.all(keys.filter(k => k !== CACHE && k !== DATA).map(k => caches.delete(k)))
    ).then(() => self.clients.claim())
  )
})

self.addEventListener('fetch', event => {
  const url = new URL(event.request.url)

  // Solo interceptar mismo origen
  if (url.origin !== self.location.origin) return

  // Lecturas del mecánico → network-first; sin red, la última respuesta guardada
  if (url.pathname.startsWith('/api/')) {
    if (event.request.method === 'GET' && isOfflineReadable(url) && !url.pathname.endsWith('/pdf')) {
      event.respondWith(
        fetch(event.request)
          .then(response => {
            if (response.ok) {
              const clone = response.clone()
              caches.open(DATA).then(cache => cache.put(event.request, clone))
            }
            return response
          })
          .catch(() => caches.open(DATA).then(cache => cache.match(event.request)).then(cached => cached || Response.error()))
      )
    }
    return
  }

  // Navegaciones (HTML) → network-first; sin red, la última copia o la raíz de la SPA
  if (event.request.mode === 'navigate') {
    event.respondWith(
      fetch(event.request)
        .then(response => {
          if (response.ok) {
            const clone = response.clone()
            caches.open(CACHE).then(cache => cache.put(event.request, clone))
          }
          return response
        })
        .catch(() => caches.match(event.request).then(cached => cached || caches.match('/')))
    )
    return
  }

  // Assets estáticos (con hash) → cache-first
  event.respondWith(
    caches.match(event.request).then(cached => {
      if (cached) return cached
      return fetch(event.request).then(response => {
        if (response.ok && event.request.method === 'GET') {
          const clone = response.clone()
          caches.open(CACHE).then(cache => cache.put(event.request, clone))
        }
        return response
      })
    })
  )
})

// Al cerrar sesión se descartan los datos guardados (un celular compartido no debe mostrar lo del usuario anterior)
self.addEventListener('message', event => {
  if (event.data === 'clear-data') event.waitUntil(caches.delete(DATA))
})

// Push notifications
self.addEventListener('push', event => {
  if (!event.data) return
  const payload = event.data.json()
  event.waitUntil(
    self.registration.showNotification(payload.title ?? 'TallerControl', {
      body: payload.body ?? '',
      icon: '/favicon.svg',
      badge: '/favicon.svg',
      // Mismo tag = el aviso reemplaza al anterior en vez de duplicarse.
      tag: payload.tag ?? undefined,
      data: payload.url ? { url: payload.url } : undefined,
    })
  )
})

self.addEventListener('notificationclick', event => {
  event.notification.close()
  const url = event.notification.data?.url ?? '/m'
  event.waitUntil(
    self.clients.matchAll({ type: 'window' }).then(clients => {
      for (const client of clients) {
        if (client.url.includes(self.location.origin) && 'focus' in client) {
          client.navigate(url)
          return client.focus()
        }
      }
      return self.clients.openWindow(url)
    })
  )
})
