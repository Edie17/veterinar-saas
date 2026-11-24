import { expect, it } from 'vitest';
import { visitCalendar } from './calendar';

it('exports UTC times with a stable identifier and a calendar reminder', () => {
  const visit = { id: 42, startAt: '2026-10-25T09:00:00+01:00', endAt: '2026-10-25T09:20:00+01:00', petName: 'Bady', visitReason: 'Kontrola' };
  const calendar = visitCalendar(visit, 'Ordinace', 'Ulice 1', new Date('2026-10-19T12:00:00Z'));
  expect(calendar).toContain('DTSTART:20261025T080000Z\r\n');
  expect(calendar).toContain('DTEND:20261025T082000Z\r\n');
  expect(calendar).toContain('UID:veterinar-42@localhost');
  expect(calendar).toContain('TRIGGER:-PT1H');
});
it('escapes text and folds UTF-8 lines without allowing injected calendar properties', () => {
  const calendar = visitCalendar({ id: 1, startAt: '2026-10-25T08:00:00Z', endAt: '2026-10-25T08:20:00Z', petName: 'Míca\r\nATTENDEE:evil@example.test' }, 'Žluťoučká ordinace '.repeat(12), 'Ulice, 1; město');
  expect(calendar).not.toContain('\r\nATTENDEE:');
  expect(calendar).toContain('LOCATION:Ulice\\, 1\\; město');
  for (const line of calendar.split('\r\n')) expect(new TextEncoder().encode(line).length).toBeLessThanOrEqual(75);
});
