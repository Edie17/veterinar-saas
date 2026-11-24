import type { AvailableSlot } from '../api';
import { dayNames } from './constants';

export function addDays(date: Date, days: number) {
  const copy = new Date(date);
  copy.setUTCDate(copy.getUTCDate() + days);
  return copy;
}

export function toDateInput(date: Date) {
  const parts = new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Prague', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(date);
  const part = (name: string) => parts.find(x => x.type === name)?.value;
  return `${part('year')}-${part('month')}-${part('day')}`;
}

export function parseDateInput(value: string) {
  const parsed = new Date(`${value}T12:00:00Z`);
  return Number.isNaN(parsed.getTime()) ? new Date() : parsed;
}

export function createCalendarWeek(value: string) {
  const selected = parseDateInput(value);
  const day = selected.getUTCDay();
  const mondayOffset = day === 0 ? -6 : 1 - day;
  const monday = addDays(selected, mondayOffset);
  const todayKey = toDateInput(new Date());

  return Array.from({ length: 7 }, (_, index) => {
    const date = addDays(monday, index);
    const dateKey = toDateInput(date);
    return {
      dateKey,
      shortName: dayNames[date.getUTCDay()].slice(0, 3),
      isToday: dateKey === todayKey,
    };
  });
}

export function getAppointmentDateKey(value: string) {
  return toDateInput(new Date(value));
}

export function formatCalendarDay(value: string) {
  return new Intl.DateTimeFormat('cs-CZ', { day: 'numeric', month: 'numeric', timeZone: 'Europe/Prague' }).format(parseDateInput(value));
}

export function minutesBetween(start: string, end: string) {
  const [startHour, startMinute] = start.split(':').map(Number);
  const [endHour, endMinute] = end.split(':').map(Number);
  return endHour * 60 + endMinute - (startHour * 60 + startMinute);
}

export function formatTime(value: string) {
  return new Intl.DateTimeFormat('cs-CZ', { hour: '2-digit', minute: '2-digit', timeZone: 'Europe/Prague' }).format(new Date(value));
}

export function formatDateTime(value: string) {
  return new Intl.DateTimeFormat('cs-CZ', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'Europe/Prague' }).format(new Date(value));
}

export function groupSlotsByPart(slots: AvailableSlot[]) {
  const availableSlots = slots.filter(slot => slot.isAvailable);
  const groups = [
    { label: 'Dopoledne', slots: availableSlots.filter(slot => Number(formatTime(slot.startAt).split(':')[0]) < 12) },
    { label: 'Odpoledne', slots: availableSlots.filter(slot => Number(formatTime(slot.startAt).split(':')[0]) >= 12) },
  ];
  return groups.filter(group => group.slots.length > 0);
}

export function formatDate(value: string) {
  const date = value.includes('T') ? new Date(value) : parseDateInput(value);
  return Number.isNaN(date.getTime())
    ? value
    : new Intl.DateTimeFormat('cs-CZ', { dateStyle: 'medium', timeZone: 'Europe/Prague' }).format(date);
}
