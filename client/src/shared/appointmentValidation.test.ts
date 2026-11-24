import { describe, expect, it } from 'vitest';
import { validateAppointmentRequest } from './appointmentValidation';
import { createDefaultAppointmentRequest } from './drafts';

const type = { title: 'Kontrola', durationMinutes: 15, sortOrder: 1, isPublished: true };
const now = new Date('2026-10-19T08:00:00Z');
const valid = () => ({ ...createDefaultAppointmentRequest(type), name: ' Jana Nová ', phone: '777 123 456',
  email: 'jana@example.test', petName: 'Bady', requestedStartAt: '2026-10-20T09:00:00+02:00', requestedEndAt: '2026-10-20T09:15:00+02:00' });

describe('appointment validation', () => {
  it('normalizes a legitimate request and derives service duration', () => {
    const result = validateAppointmentRequest({ ...valid(), durationMinutes: 999 }, [type], now);
    expect(result.ok).toBe(true);
    if (result.ok) { expect(result.value.name).toBe('Jana Nová'); expect(result.value.durationMinutes).toBe(15); }
  });
  it.each(['name', 'petName', 'message'] as const)('rejects script content in %s', field => {
    const result = validateAppointmentRequest({ ...valid(), [field]: '<script>alert(1)</script>' }, [type], now);
    expect(result.errors[field]).toMatch(/HTML/);
  });
  it('requires a future selected interval', () => {
    expect(validateAppointmentRequest({ ...valid(), requestedStartAt: null }, [type], now).errors.requestedStartAt).toBeTruthy();
    expect(validateAppointmentRequest(valid(), [type], new Date('2027-01-01')).errors.requestedStartAt).toBeTruthy();
  });
  it('requires a published service and valid contact fields', () => {
    const result = validateAppointmentRequest({ ...valid(), email: 'bad', phone: '123' }, [{ ...type, isPublished: false }], now);
    expect(result.errors.email).toBeTruthy(); expect(result.errors.phone).toBeTruthy(); expect(result.errors.visitReason).toBeTruthy();
  });
});
