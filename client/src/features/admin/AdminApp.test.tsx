import { expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { apiFetch, ApiError } from '../../api';
import { AdminApp } from './AdminApp';

vi.mock('../../api', async importOriginal => ({ ...await importOriginal<typeof import('../../api')>(), apiFetch: vi.fn() }));

it('explains rejected credentials and clears the password without exposing an HTTP error', async () => {
  vi.mocked(apiFetch).mockReset().mockImplementation(async url => {
    if (url === '/api/auth/me' || url === '/api/auth/login') throw new ApiError('HTTP 401', 401);
    throw new Error(`Unexpected request ${url}`);
  });
  render(<AdminApp />);
  const user = userEvent.setup();
  await user.type(screen.getByLabelText('Heslo'), 'incorrect-test-password');
  await user.click(screen.getByRole('button', { name: 'Přihlásit do správy' }));
  expect(await screen.findByText('Nesprávný e-mail nebo heslo. Zkontrolujte přihlašovací údaje.')).toBeTruthy();
  expect((screen.getByLabelText('Heslo') as HTMLInputElement).value).toBe('');
  expect(screen.queryByText('HTTP 401')).toBeNull();
});
