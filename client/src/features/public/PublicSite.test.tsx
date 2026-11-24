import { beforeEach, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { apiFetch } from '../../api';
import { PublicSite } from './PublicSite';
import { defaultPublicSite, defaultWorkingHours } from '../../shared/constants';

vi.mock('../../api', async importOriginal => ({ ...await importOriginal<typeof import('../../api')>(), apiFetch: vi.fn() }));
const type = { title: 'Kontrola', durationMinutes: 15, sortOrder: 1, isPublished: true };
const start = new Date(Date.now() + 86400000).toISOString();
const slot = { startAt: start, endAt: new Date(new Date(start).getTime() + 900000).toISOString(), isAvailable: true };
let unavailable = false;
beforeEach(() => {
  unavailable = false;
  vi.mocked(apiFetch).mockReset().mockImplementation(async url => {
    if (url === '/api/site') return defaultPublicSite as never;
    if (url === '/api/public/working-hours') return { standardRules: defaultWorkingHours, upcomingEvents: [] } as never;
    if (url === '/api/appointment-types') { if (unavailable) throw new Error('offline'); return [type] as never; }
    if (url.startsWith('/api/availability/next')) return slot as never;
    if (url.startsWith('/api/availability')) return [slot] as never;
    if (url === '/api/appointments') return { id: 42 } as never;
    throw new Error(`Unexpected request ${url}`);
  });
});
it('shows an actionable error beside the disabled form and recovers after retry', async () => {
  unavailable = true; render(<PublicSite />);
  expect(await screen.findByRole('alert')).toBeTruthy();
  expect((screen.getByRole('button', { name: 'Rezervovat termín' }) as HTMLButtonElement).disabled).toBe(true);
  unavailable = false;
  await userEvent.click(screen.getByRole('button', { name: 'Znovu načíst rezervace' }));
  await screen.findByRole('button', { name: /Rezervovat čas/ });
  expect(screen.queryByRole('alert')).toBeNull();
});
it('lets a visitor select an available slot and submit a legitimate booking', async () => {
  render(<PublicSite />);
  const user = userEvent.setup();
  await user.click(await screen.findByRole('button', { name: /Rezervovat čas/ }));
  await user.type(screen.getByLabelText('Vaše jméno'), 'Jana Nová');
  await user.type(screen.getByLabelText('Telefon'), '+420 777 123 456');
  await user.type(screen.getByLabelText('E-mail'), 'jana@example.test');
  await user.type(screen.getByLabelText('Jméno mazlíčka'), 'Bady');
  await user.click(screen.getByRole('button', { name: 'Rezervovat termín' }));
  await waitFor(() => expect(screen.getByText('Rezervace #42 čeká na potvrzení.')).toBeTruthy());
  expect(vi.mocked(apiFetch).mock.calls.filter(([url]) => url === '/api/appointments')).toHaveLength(1);
});
it('finds the next appointment and selects it after loading the corresponding day', async () => {
  render(<PublicSite />);
  await screen.findByRole('button', { name: /Rezervovat čas/ });
  await userEvent.click(screen.getByRole('button', { name: 'Najít nejbližší termín' }));
  await waitFor(() => expect(screen.getByRole('button', { name: /Rezervovat čas/ }).className).toContain('selected'));
  expect((screen.getByRole('button', { name: 'Rezervovat termín' }) as HTMLButtonElement).disabled).toBe(false);
});
it('shows loading instead of a false missing-article page while content is pending', async () => {
  window.history.replaceState(null, '', '/clanky/pomaly-clanek');
  let finish!: (value: typeof defaultPublicSite) => void;
  const pending = new Promise<typeof defaultPublicSite>(resolve => { finish = resolve; });
  const original = vi.mocked(apiFetch).getMockImplementation()!;
  vi.mocked(apiFetch).mockImplementation((url, init) => url === '/api/site' ? pending as never : original(url, init));
  render(<PublicSite />);
  expect(screen.getByRole('status').textContent).toContain('Načítám články');
  expect(screen.queryByText('Článek nebyl nalezen')).toBeNull();
  finish({ ...defaultPublicSite, articleSections: [{ id: 1, title: 'Poradna', slug: 'poradna', description: '', sortOrder: 0, isPublished: true,
    articles: [{ id: 1, articleSectionId: 1, title: 'Pomalý článek', slug: 'pomaly-clanek', summary: '', body: '<p>Načtený obsah.</p>', sortOrder: 0, isPublished: true }] }] });
  expect(await screen.findByRole('heading', { name: 'Pomalý článek' })).toBeTruthy();
  expect(screen.queryByText('Článek nebyl nalezen')).toBeNull();
});
it('distinguishes failed article loading from an actually missing article', async () => {
  window.history.replaceState(null, '', '/clanky/pomaly-clanek');
  const original = vi.mocked(apiFetch).getMockImplementation()!;
  vi.mocked(apiFetch).mockImplementation((url, init) => url === '/api/site' ? Promise.reject(new Error('offline')) : original(url, init));
  render(<PublicSite />);
  expect(await screen.findByRole('heading', { name: 'Články se nepodařilo načíst' })).toBeTruthy();
  expect(screen.queryByText('Článek nebyl nalezen')).toBeNull();
});
