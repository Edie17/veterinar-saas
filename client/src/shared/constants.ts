import type { AppointmentType, WorkingHoursRule } from '../api';
import type { CmsSection, SiteResponse } from '../types';

export const dayNames = ['Neděle', 'Pondělí', 'Úterý', 'Středa', 'Čtvrtek', 'Pátek', 'Sobota'];

export const defaultWorkingHours: WorkingHoursRule[] = [1, 2, 3, 4, 5, 6, 0].map(dayOfWeek => ({
  dayOfWeek,
  opensAt: dayOfWeek === 0 || dayOfWeek === 6 ? '00:00' : '08:00',
  closesAt: dayOfWeek === 0 || dayOfWeek === 6 ? '00:00' : '16:00',
  slotMinutes: 30,
  isClosed: dayOfWeek === 0 || dayOfWeek === 6,
}));

export const defaultAppointmentTypes: AppointmentType[] = [
  { title: 'Preventivní prohlídka', durationMinutes: 20, sortOrder: 1, isPublished: true },
  { title: 'Očkování', durationMinutes: 10, sortOrder: 2, isPublished: true },
  { title: 'Akutní problém', durationMinutes: 30, sortOrder: 3, isPublished: true },
  { title: 'Kontrola', durationMinutes: 15, sortOrder: 4, isPublished: true },
  { title: 'Stomatologie', durationMinutes: 45, sortOrder: 5, isPublished: true },
];

export const defaultPublicSite: SiteResponse = {
  content: {
    id: 1,
    clinicName: 'Veterinární ordinace Demo',
    heroTitle: 'Moderní ordinace pro psy, kočky a malé mazlíčky.',
    heroText: 'Objednejte se online na termín, který sedí vám i ordinačním hodinám.',
    phone: '+420 000 000 000',
    email: 'ordinace@example.test',
    address: 'Ukázková adresa',
    updatedAt: new Date().toISOString(),
  },
  services: [
    { id: 1, title: 'Preventivní péče', description: 'Prohlídky, očkování a čipování.', icon: '🩺', sortOrder: 1, isPublished: true },
    { id: 2, title: 'Stomatologie', description: 'Kontrola chrupu a čištění zubního kamene.', icon: '🦷', sortOrder: 2, isPublished: true },
    { id: 3, title: 'Diagnostika', description: 'Laboratorní testy a doporučení dalšího postupu.', icon: '🔬', sortOrder: 3, isPublished: true },
  ],
  articleSections: [],
};

export const urgentTopics = [
  { title: 'Úraz nebo krvácení', detail: 'Nečekejte na volný slot.' },
  { title: 'Podezření na otravu', detail: 'Připravte název látky nebo obal.' },
  { title: 'Dušnost', detail: 'Telefonát pomůže určit další krok.' },
  { title: 'Křeče', detail: 'Poznamenejte čas začátku a délku.' },
  { title: 'Náhlé zhoršení stavu', detail: 'Popište změnu chování a příznaky.' },
];

export const visitPrepItems = [
  { title: 'Očkovací průkaz', detail: 'Hodí se při prevenci i cestování.' },
  { title: 'Užívané léky', detail: 'Stačí seznam nebo fotka balení.' },
  { title: 'Starší zprávy', detail: 'Výsledky a doporučení z jiných pracovišť.' },
  { title: 'Vzorek', detail: 'Jen pokud byl předem domluvený.' },
];

export const siteOrigin = window.location.origin;

export const cmsNav: Array<{ id: CmsSection; label: string; icon: string; hint: string }> = [
  { id: 'dashboard', label: 'Přehled', icon: '⌘', hint: 'Stav ordinace' },
  { id: 'content', label: 'Obsah webu', icon: '✎', hint: 'Landing page' },
  { id: 'articles', label: 'Články', icon: '▤', hint: 'Sekce a poradna' },
  { id: 'hours', label: 'Ordinační hodiny', icon: '◷', hint: 'Dostupné sloty' },
  { id: 'appointments', label: 'Objednávky', icon: '☑', hint: 'Poptávky klientů' },
  { id: 'settings', label: 'Nastavení', icon: '⚙', hint: 'Klinika a účet' },
];
