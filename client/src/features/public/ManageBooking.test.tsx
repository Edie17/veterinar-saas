import { expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { apiFetch, ApiError } from '../../api';
import { App } from '../../App';

vi.mock('../../api', async importOriginal => ({ ...await importOriginal<typeof import('../../api')>(), apiFetch: vi.fn() }));
it('removes the secret fragment and requires an explicit cancellation confirmation', async () => {
  window.history.replaceState(null, '', '/rezervace/sprava#token=private-test-token');
  const visit = { id: 7, petName: 'Bady', visitReason: 'Kontrola', durationMinutes: 20, startAt: new Date(Date.now() + 86400000).toISOString(), endAt: new Date(Date.now() + 87600000).toISOString(), status: 'Confirmed' };
  let cancelled = false;
  vi.mocked(apiFetch).mockReset().mockImplementation(async url => {
    if (url === '/api/appointments/manage') { if (cancelled) throw new ApiError('Odkaz pro správu rezervace je neplatný.', 404); return visit as never; }
    if (url === '/api/site') return { content: { clinicName: 'Ordinace', address: 'Ulice 1' } } as never;
    if (url.startsWith('/api/availability')) return [] as never;
    if (url === '/api/appointments/manage/cancel') { cancelled = true; return undefined as never; }
    throw new Error(`Unexpected request ${url}`);
  });
  render(<App />);
  expect(window.location.hash).toBe('');
  const user = userEvent.setup();
  await user.click(await screen.findByRole('button', { name: 'Zrušit rezervaci' }));
  expect(vi.mocked(apiFetch).mock.calls.some(([url]) => url.endsWith('/cancel'))).toBe(false);
  await user.click(screen.getByRole('button', { name: 'Ponechat rezervaci' }));
  await user.click(screen.getByRole('button', { name: 'Zrušit rezervaci' }));
  await user.click(screen.getByRole('button', { name: 'Ano, zrušit rezervaci' }));
  await waitFor(() => expect(screen.getByRole('status').textContent).toContain('Rezervace byla zrušena'));
  const mutation = vi.mocked(apiFetch).mock.calls.find(([url]) => url.endsWith('/cancel'));
  expect(JSON.parse(mutation![1]!.body as string).token).toBe('private-test-token');
  expect(screen.queryByRole('button', { name: 'Přidat do kalendáře' })).toBeNull();
  window.location.hash = '#token=private-test-token';
  expect(await screen.findByText('Odkaz pro správu rezervace je neplatný.')).toBeTruthy();
  expect(window.location.hash).toBe('');
});
