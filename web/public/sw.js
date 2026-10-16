// Service worker de TallerControl — área del mecánico (PWA F-51).
// Estrategia: cache-first para assets estáticos del bundle; network-first para /api/*.
const CACHE = 'tc-sw-v1'

self.addEventListener('install', () => self.skipWaiting())

self.addEventListener('activate', event => {
  event.waitUntil(
    caches.keys().then(keys =>
      Promise.all(keys.filter(k => k !== CACHE).map(k => caches.delete(k)))
    ).then(() => self.clients.claim())
  )
})

self.addEventListener('fetch', event => {
  const url = new URL(event.request.url)

  // Solo interceptar mismo origen
  if (url.origin !== self.location.origin) return

  // API → network-first, sin cache
  if (url.pathname.startsWith('/api/')) return

  // Assets estáticos → cache-first
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

// Push notifications
self.addEventListener('push', event => {
  if (!event.data) return
  const payload = event.data.json()
  event.waitUntil(
    self.registration.showNotification(payload.title ?? 'TallerControl', {
      body: payload.body ?? '',
      icon: '/favicon.svg',
      badge: '/favicon.svg',
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
