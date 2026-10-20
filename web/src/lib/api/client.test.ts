import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, clearCsrfToken } from './client'

const json = (status: number, body: unknown) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })

describe('api client — CSRF', () => {
  afterEach(() => {
    vi.restoreAllMocks()
    clearCsrfToken()
  })

  it('T-UI-CSRF: ante "antiforgery" pide un token nuevo y reintenta una vez', async () => {
    let tokens = 0
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      const url = String(input)
      if (url.includes('antiforgery-token')) return json(200, { token: `t${++tokens}` })
      const sent = (init?.headers as Record<string, string>)['X-CSRF-TOKEN']
      return sent === 't2' ? json(200, { ok: true }) : json(400, { detail: 'Falta o es inválida la cabecera antiforgery.' })
    })

    await expect(api.delete('/api/v1/x', undefined, true)).resolves.toEqual({ ok: true })
    expect(tokens).toBe(2)
    expect(fetchMock).toHaveBeenCalledTimes(4)
  })

  it('si el reintento también falla, muestra un mensaje claro (no el del servidor)', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input =>
      String(input).includes('antiforgery-token')
        ? json(200, { token: 'x' })
        : json(400, { detail: 'Falta o es inválida la cabecera antiforgery.' }),
    )
    await expect(api.post('/api/v1/x', {}, true)).rejects.toMatchObject({
      status: 400,
      detail: expect.stringContaining('sesión'),
    })
  })
})
