/** Wrapper de fetch con manejo de CSRF y errores RFC 7807. */

let csrfToken: string | null = null

async function ensureCsrfToken(): Promise<string> {
  if (csrfToken) return csrfToken
  const res = await fetch('/api/v1/auth/antiforgery-token', { credentials: 'include' })
  if (!res.ok) throw new ApiError(res.status, 'No se pudo obtener el token CSRF')
  const data = await res.json()
  csrfToken = data.token as string
  return csrfToken
}

/** Invalida el token CSRF en memoria (llamar al hacer logout). */
export function clearCsrfToken() {
  csrfToken = null
}

export class ApiError extends Error {
  readonly status: number
  readonly detail: string
  readonly extensions?: Record<string, unknown>

  constructor(status: number, detail: string, extensions?: Record<string, unknown>) {
    super(detail)
    this.status = status
    this.detail = detail
    this.extensions = extensions
  }
}

/** El servidor rechazó la petición por la cabecera antiforgery (token vencido o de otra sesión). */
function isCsrfFailure(status: number, json: { detail?: string } | null) {
  return status === 400 && /antiforgery/i.test(json?.detail ?? '')
}

const CSRF_MESSAGE = 'Tu sesión cambió o venció. Volvé a intentar la acción.'

async function request<T>(
  method: string,
  url: string,
  body?: unknown,
  requiresCsrf = false,
  retried = false,
  extraHeaders?: Record<string, string>,
): Promise<T> {
  const headers: Record<string, string> = { 'Content-Type': 'application/json', ...extraHeaders }

  if (requiresCsrf) {
    headers['X-CSRF-TOKEN'] = await ensureCsrfToken()
  }

  const res = await fetch(url, {
    method,
    credentials: 'include',
    headers,
    body: body !== undefined ? JSON.stringify(body) : undefined,
  })

  if (res.status === 204 || res.headers.get('Content-Length') === '0') {
    return undefined as T
  }

  const json = await res.json().catch(() => null)

  if (!res.ok) {
    if (requiresCsrf && isCsrfFailure(res.status, json)) {
      // El token cacheado ya no vale: se descarta y se reintenta una vez con uno nuevo.
      csrfToken = null
      if (!retried) return request<T>(method, url, body, requiresCsrf, true, extraHeaders)
      throw new ApiError(res.status, CSRF_MESSAGE, json ? { ...json } : undefined)
    }
    const detail = json?.detail ?? res.statusText
    const extensions = json ? { ...json } : undefined
    throw new ApiError(res.status, detail, extensions)
  }

  return json as T
}

/** `version` es el valor crudo (sin comillas); acá se arma el ETag como pide HTTP. */
async function requestWithIfMatch<T>(
  method: string,
  url: string,
  body: unknown,
  version: string,
  retried = false,
): Promise<T> {
  const res = await fetch(url, {
    method,
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      'If-Match': `"${version}"`,
      'X-CSRF-TOKEN': await ensureCsrfToken(),
    },
    body: JSON.stringify(body),
  })

  if (res.status === 204) return undefined as T
  const json = await res.json().catch(() => null)
  if (!res.ok) {
    if (isCsrfFailure(res.status, json)) {
      csrfToken = null
      if (!retried) return requestWithIfMatch<T>(method, url, body, version, true)
      throw new ApiError(res.status, CSRF_MESSAGE, json ?? undefined)
    }
    throw new ApiError(res.status, json?.detail ?? res.statusText, json ?? undefined)
  }
  return json as T
}

export const api = {
  get: <T>(url: string) => request<T>('GET', url),
  post: <T>(url: string, body?: unknown, csrf = false, headers?: Record<string, string>) =>
    request<T>('POST', url, body, csrf, false, headers),
  put: <T>(url: string, body: unknown, ifMatch: string) => requestWithIfMatch<T>('PUT', url, body, ifMatch),
  patch: <T>(url: string, body: unknown, ifMatch: string) => requestWithIfMatch<T>('PATCH', url, body, ifMatch),
  patchJson: <T>(url: string, body?: unknown, csrf = false) => request<T>('PATCH', url, body, csrf),
  delete: <T>(url: string, body?: unknown, csrf = false) => request<T>('DELETE', url, body, csrf),
}
