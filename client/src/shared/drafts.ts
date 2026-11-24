import type { AppointmentRequest, AppointmentType, Article, ArticleSection, WorkingHoursEvent, WorkingHoursRule } from '../api';
import { defaultAppointmentTypes, defaultWorkingHours } from './constants';
import { addDays, toDateInput } from './date';

export function mergeWorkingHours(rules: WorkingHoursRule[]) {
  return defaultWorkingHours.map(defaultRule => rules.find(rule => rule.dayOfWeek === defaultRule.dayOfWeek) ?? defaultRule);
}

export function createDefaultSectionDraft(): ArticleSection {
  return {
    title: 'Poradna',
    slug: '',
    description: '',
    sortOrder: 1,
    isPublished: true,
    articles: [],
  };
}

export function createDefaultArticleDraft(articleSectionId: number): Article {
  return {
    articleSectionId,
    title: '',
    slug: '',
    summary: '',
    body: '',
    sortOrder: 1,
    isPublished: true,
  };
}

export function normalizeArticleSections(sections: ArticleSection[]) {
  return [...sections]
    .map(section => ({ ...section, articles: [...section.articles].sort((a, b) => a.sortOrder - b.sortOrder || a.title.localeCompare(b.title, 'cs')) }))
    .sort((a, b) => a.sortOrder - b.sortOrder || a.title.localeCompare(b.title, 'cs'));
}

export function createDefaultAppointmentRequest(type: AppointmentType = defaultAppointmentTypes[0]): AppointmentRequest {
  return {
    name: '',
    phone: '',
    email: '',
    petName: '',
    petType: 'Pes',
    visitReason: type.title,
    durationMinutes: type.durationMinutes,
    message: '',
    requestedStartAt: null,
    requestedEndAt: null,
  };
}

export function createDefaultAppointmentTypeDraft(): AppointmentType {
  return { title: 'Preventivní prohlídka', durationMinutes: 20, sortOrder: 1, isPublished: true };
}

export function normalizeAppointmentTypes(types: AppointmentType[]) {
  return [...types].sort((a, b) => a.sortOrder - b.sortOrder || a.title.localeCompare(b.title, 'cs'));
}

export function createDefaultEventDraft(): WorkingHoursEvent {
  const today = new Date();
  const startsOn = toDateInput(addDays(today, 7));
  const endsOn = toDateInput(addDays(today, 14));

  return {
    title: 'Vánoční provoz',
    note: '',
    displayFrom: toDateInput(today),
    startsOn,
    endsOn,
    isPublished: true,
    rules: defaultWorkingHours.map(rule => ({ ...rule, isClosed: true, opensAt: '00:00', closesAt: '00:00' })),
  };
}

export function normalizeEvent(event: WorkingHoursEvent): WorkingHoursEvent {
  return { ...event, rules: mergeWorkingHours(event.rules) };
}
