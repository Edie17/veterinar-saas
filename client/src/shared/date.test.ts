import { expect, it } from 'vitest';
import { createCalendarWeek, formatTime, getAppointmentDateKey, toDateInput } from './date';

it('uses the Prague date across UTC midnight in summer and winter', () => {
  expect(toDateInput(new Date('2026-07-01T22:30:00Z'))).toBe('2026-07-02');
  expect(getAppointmentDateKey('2026-12-01T23:30:00Z')).toBe('2026-12-02');
});
it('keeps a seven-day Monday week through daylight saving transition', () => {
  const week = createCalendarWeek('2026-10-25');
  expect(week.map(day => day.dateKey)).toEqual(['2026-10-19', '2026-10-20', '2026-10-21', '2026-10-22', '2026-10-23', '2026-10-24', '2026-10-25']);
  expect(formatTime('2026-10-25T02:30:00Z')).toMatch(/^0?3:30$/);
});
