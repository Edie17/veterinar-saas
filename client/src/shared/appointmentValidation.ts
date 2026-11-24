import type { AppointmentRequest, AppointmentType } from '../api';

export type AppointmentField = 'requestedStartAt' | 'visitReason' | 'name' | 'phone' | 'email' | 'petName' | 'petType' | 'message';
export type AppointmentValidationErrors = Partial<Record<AppointmentField, string>>;

type AppointmentValidationResult =
  | { ok: true; value: AppointmentRequest; errors: AppointmentValidationErrors }
  | { ok: false; errors: AppointmentValidationErrors };

const allowedPetTypes = new Set(['Pes', 'Kočka', 'Drobné zvíře', 'Jiné']);
const suspiciousInputPattern = /<[^>]*>|<\/|javascript\s*:|data\s*:\s*text\/html|vbscript\s*:|on[a-z]+\s*=|&#x?[0-9a-f]+;/i;
const controlCharactersPattern = /[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F]/g;

export function validateAppointmentRequest(form: AppointmentRequest, appointmentTypes: AppointmentType[], now = new Date()): AppointmentValidationResult {
  const errors: AppointmentValidationErrors = {};
  const selectedType = appointmentTypes.find(type => type.isPublished && type.title === form.visitReason);
  const normalized: AppointmentRequest = {
    ...form,
    name: normalizeText(form.name),
    phone: normalizePhone(form.phone),
    email: normalizeText(form.email ?? ''),
    petName: normalizeText(form.petName),
    petType: normalizeText(form.petType),
    visitReason: normalizeText(form.visitReason),
    durationMinutes: selectedType?.durationMinutes ?? form.durationMinutes,
    message: normalizeText(form.message ?? ''),
  };

  validatePlainText('name', normalized.name, errors, { label: 'Jméno', min: 2, max: 80 });
  validatePlainText('petName', normalized.petName, errors, { label: 'Jméno mazlíčka', min: 1, max: 60 });
  validatePlainText('message', normalized.message ?? '', errors, { label: 'Zpráva', min: 0, max: 600, optional: true });

  if (!isValidPhone(normalized.phone)) {
    errors.phone = 'Zadejte platné telefonní číslo, ideálně ve formátu +420 777 123 456.';
  }

  if (!isValidEmail(normalized.email ?? '')) {
    errors.email = 'Zadejte platnou e-mailovou adresu.';
  }

  if (!allowedPetTypes.has(normalized.petType)) {
    errors.petType = 'Vyberte platný druh zvířete.';
  }

  if (!selectedType) {
    errors.visitReason = 'Vyberte platný důvod návštěvy.';
  }

  if (!normalized.requestedStartAt || !normalized.requestedEndAt) {
    errors.requestedStartAt = 'Vyberte prosím volný termín.';
  } else {
    const start = new Date(normalized.requestedStartAt);
    const end = new Date(normalized.requestedEndAt);
    if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime()) || end <= start) {
      errors.requestedStartAt = 'Vybraný termín není platný.';
    } else if (start < now) {
      errors.requestedStartAt = 'Nelze rezervovat termín v minulosti.';
    }
  }

  if (Object.keys(errors).length > 0) {
    return { ok: false, errors };
  }

  return { ok: true, value: normalized, errors: {} };
}

function validatePlainText(field: AppointmentField, value: string, errors: AppointmentValidationErrors, options: { label: string; min: number; max: number; optional?: boolean }) {
  if (!value && options.optional) return;
  if (value.length < options.min) {
    errors[field] = `${options.label} je příliš krátké.`;
    return;
  }
  if (value.length > options.max) {
    errors[field] = `${options.label} je příliš dlouhé.`;
    return;
  }
  if (containsSuspiciousInput(value)) {
    errors[field] = `${options.label} nesmí obsahovat HTML, skripty ani vložený kód.`;
  }
}

function normalizeText(value: string) {
  return value.replace(controlCharactersPattern, '').replace(/\s+/g, ' ').trim();
}

function normalizePhone(value: string) {
  return normalizeText(value).replace(/[().-]/g, ' ').replace(/\s+/g, ' ');
}

function containsSuspiciousInput(value: string) {
  return suspiciousInputPattern.test(value);
}

function isValidPhone(value: string) {
  if (containsSuspiciousInput(value)) return false;
  const compact = value.replace(/\s/g, '');
  return /^(\+420)?[0-9]{9}$/.test(compact) || /^\+[1-9][0-9]{7,14}$/.test(compact);
}

function isValidEmail(value: string) {
  if (!value || containsSuspiciousInput(value) || value.length > 120) return false;
  return /^[^\s@<>]+@[^\s@<>]+\.[^\s@<>]{2,}$/.test(value);
}
