import type React from 'react';

export function handleHorizontalNav(event: React.KeyboardEvent<HTMLElement>) {
  if (event.key !== 'ArrowRight' && event.key !== 'ArrowLeft') return;
  const links = Array.from(event.currentTarget.querySelectorAll<HTMLAnchorElement>('a[href]'));
  const index = links.indexOf(document.activeElement as HTMLAnchorElement);
  if (index < 0) return;
  event.preventDefault();
  const offset = event.key === 'ArrowRight' ? 1 : -1;
  links[(index + offset + links.length) % links.length]?.focus();
}

export function phoneHref(phone?: string) {
  const number = phone?.replace(/\s/g, '');
  return number && number !== '+420000000000' ? `tel:${number}` : undefined;
}
