export type SiteContent = {
  id: number;
  clinicName: string;
  heroTitle: string;
  heroText: string;
  phone: string;
  email: string;
  address: string;
  updatedAt: string;
};

export type ServiceItem = {
  id: number;
  title: string;
  description: string;
  icon: string;
  sortOrder: number;
  isPublished: boolean;
};

export type Article = {
  id?: number;
  articleSectionId: number;
  title: string;
  slug: string;
  summary: string;
  body: string;
  sortOrder: number;
  isPublished: boolean;
  publishedAt?: string;
  updatedAt?: string;
};

export type ArticleSection = {
  id?: number;
  title: string;
  slug: string;
  description: string;
  sortOrder: number;
  isPublished: boolean;
  articles: Article[];
};

export type AppointmentRequest = {
  id?: number;
  name: string;
  phone: string;
  email?: string;
  petName: string;
  petType: string;
  visitReason: string;
  durationMinutes: number;
  message?: string;
  requestedStartAt?: string | null;
  requestedEndAt?: string | null;
  status?: string;
  holdExpiresAt?: string | null;
  createdAt?: string;
};

export type AvailableSlot = {
  startAt: string;
  endAt: string;
  isAvailable: boolean;
  unavailableReason?: string | null;
};

export type AppointmentType = {
  id?: number;
  title: string;
  durationMinutes: number;
  sortOrder: number;
  isPublished: boolean;
};

export type WorkingHoursRule = {
  id?: number;
  dayOfWeek: number;
  opensAt: string;
  closesAt: string;
  slotMinutes: number;
  isClosed: boolean;
};

export type WorkingHoursEvent = {
  id?: number;
  title: string;
  note?: string | null;
  displayFrom: string;
  startsOn: string;
  endsOn: string;
  isPublished: boolean;
  rules: WorkingHoursRule[];
};

export type PublicWorkingHoursSchedule = {
  today: string;
  standardRules: WorkingHoursRule[];
  activeEvent?: WorkingHoursEvent | null;
  upcomingEvents: WorkingHoursEvent[];
};

export class ApiError extends Error {
  constructor(message: string, public readonly status: number) { super(message); }
}
export async function ensureCsrf(signal?: AbortSignal | null): Promise<string> {
  const res = await fetch('/api/auth/csrf', { credentials: 'include', signal });
  if (!res.ok) throw new Error('Nepodařilo se načíst CSRF token.');
  const data = await res.json();
  return data.csrfToken as string;
}

export async function apiFetch<T>(url: string, init: RequestInit = {}): Promise<T> {
  const method = (init.method ?? 'GET').toUpperCase();
  const headers = new Headers(init.headers ?? {});

  if (!headers.has('Content-Type') && init.body) {
    headers.set('Content-Type', 'application/json');
  }

  if (!['GET', 'HEAD', 'OPTIONS'].includes(method)) {
    headers.set('X-CSRF-TOKEN', await ensureCsrf(init.signal));
  }

  const res = await fetch(url, { ...init, headers, credentials: 'include' });
  if (!res.ok) {
    let message = `Požadavek se nepodařilo dokončit (HTTP ${res.status}).`;
    try {
      const data = await res.json() as { message?: string; error?: string; title?: string; traceId?: string };
      message = data.message || data.error || data.title || message;
      if (data.traceId) message += ` ID požadavku: ${data.traceId}`;
    } catch {
      // A proxy may return HTML or plain text. Keep those internals out of the UI.
    }
    throw new ApiError(message, res.status);
  }

  if (res.status === 204) return undefined as T;
  return res.json() as Promise<T>;
}
