import { expect, it, vi } from 'vitest';
import { apiFetch, ApiError } from './api';

it('attaches a CSRF token to a mutation', async () => {
  const fetch = vi.fn().mockResolvedValueOnce(new Response(JSON.stringify({ csrfToken: 'signed-test-token' })))
    .mockResolvedValueOnce(new Response(null, { status: 204 }));
  vi.stubGlobal('fetch', fetch);
  await apiFetch('/api/appointments/confirm', { method: 'POST', body: '{}' });
  expect(fetch.mock.calls[0][0]).toBe('/api/auth/csrf');
  expect(new Headers(fetch.mock.calls[1][1].headers).get('X-CSRF-TOKEN')).toBe('signed-test-token');
});
it('does not expose a raw proxy error page', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('<h1>private upstream detail</h1>', { status: 502 })));
  await expect(apiFetch('/api/site')).rejects.toThrow('HTTP 502');
  try { await apiFetch('/api/site'); } catch (error) { expect(error).toBeInstanceOf(ApiError); expect((error as Error).message).not.toContain('upstream'); }
});
it('keeps a safe API error and its request correlation', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({ title: 'Termín je obsazený.', traceId: 'trace-test' }), { status: 409 })));
  await expect(apiFetch('/api/site')).rejects.toThrow('Termín je obsazený. ID požadavku: trace-test');
});
