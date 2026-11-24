import { expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PublicNav } from './PublicNav';

it('opens and closes the mobile disclosure and restores focus after Escape', async () => {
  render(<PublicNav clinicName="Testovací ordinace" hasArticles />);
  const user = userEvent.setup();
  const toggle = screen.getByRole('button', { name: 'Menu' });
  expect(toggle.getAttribute('aria-expanded')).toBe('false');
  await user.click(toggle);
  expect(toggle.getAttribute('aria-expanded')).toBe('true');
  screen.getByRole('link', { name: 'Články' }).focus();
  await user.keyboard('{Escape}');
  expect(toggle.getAttribute('aria-expanded')).toBe('false');
  expect(document.activeElement).toBe(toggle);
});
it('closes navigation after choosing a section and distinguishes contact from booking', async () => {
  render(<PublicNav hasArticles={false} />);
  await userEvent.click(screen.getByRole('button', { name: 'Menu' }));
  await userEvent.click(screen.getByRole('link', { name: 'Služby' }));
  expect(screen.getByRole('button', { name: 'Menu' }).getAttribute('aria-expanded')).toBe('false');
  expect(screen.getByRole('link', { name: 'Kontakt' }).getAttribute('href')).toBe('/#footer-title');
  expect(screen.getByRole('link', { name: 'Objednat se' }).getAttribute('href')).toBe('/#kontakt');
  expect(screen.queryByRole('link', { name: 'Články' })).toBeNull();
});
