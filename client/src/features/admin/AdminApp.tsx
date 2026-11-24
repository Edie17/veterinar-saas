import { useEffect, useMemo, useState, useRef, type FormEvent } from 'react';
import { apiFetch, ApiError, AppointmentRequest, AppointmentType, Article, ArticleSection, AvailableSlot, SiteContent, WorkingHoursEvent, WorkingHoursRule } from '../../api';
import { sanitizeArticleHtml } from '../../shared/articles';
import { defaultAppointmentTypes, defaultWorkingHours } from '../../shared/constants';
import { createCalendarWeek, minutesBetween, toDateInput } from '../../shared/date';
import { createDefaultAppointmentRequest, createDefaultAppointmentTypeDraft, createDefaultArticleDraft, createDefaultEventDraft, createDefaultSectionDraft, mergeWorkingHours, normalizeAppointmentTypes, normalizeArticleSections, normalizeEvent } from '../../shared/drafts';
import { validateAppointmentRequest } from '../../shared/appointmentValidation';
import type { CmsSection, Me, SiteResponse } from '../../types';
import { AdminLayout } from './AdminLayout';
import { AdminLogin } from './AdminLogin';
import { AppointmentsSection, ArticlesSection, ContentSection, DashboardSection, HoursSection, SettingsSection } from './AdminSections';
export function AdminApp() {
  const saving = useRef(false);
  const [busy, setBusy] = useState(false);
  const [me, setMe] = useState<Me | null>(null);
  const [email, setEmail] = useState('admin@example.cz');
  const [password, setPassword] = useState('');
  const [site, setSite] = useState<SiteContent | null>(null);
  const [siteDirty, setSiteDirty] = useState(false);
  const [appointments, setAppointments] = useState<AppointmentRequest[]>([]);
  const [articleSections, setArticleSections] = useState<ArticleSection[]>([]);
  const [sectionDraft, setSectionDraft] = useState<ArticleSection>(() => createDefaultSectionDraft());
  const [articleDraft, setArticleDraft] = useState<Article>(() => createDefaultArticleDraft(0));
  const [adminAppointmentTypes, setAdminAppointmentTypes] = useState<AppointmentType[]>([]);
  const [appointmentTypeDraft, setAppointmentTypeDraft] = useState<AppointmentType>(() => createDefaultAppointmentTypeDraft());
  const [calendarDate, setCalendarDate] = useState(() => toDateInput(new Date()));
  const [manualAppointmentDate, setManualAppointmentDate] = useState(() => toDateInput(new Date()));
  const [manualAppointment, setManualAppointment] = useState<AppointmentRequest>(() => createDefaultAppointmentRequest(defaultAppointmentTypes[0]));
  const [manualSlots, setManualSlots] = useState<AvailableSlot[]>([]);
  const [workingHours, setWorkingHours] = useState<WorkingHoursRule[]>(defaultWorkingHours);
  const [workingHourEvents, setWorkingHourEvents] = useState<WorkingHoursEvent[]>([]);
  const [eventDraft, setEventDraft] = useState<WorkingHoursEvent>(() => createDefaultEventDraft());
  const [message, setMessage] = useState('');
  const [activeSection, setActiveSection] = useState<CmsSection>('dashboard');

  function handleError(error: unknown) {
    if (error instanceof ApiError && error.status === 401) { setMe(null); setPassword(''); }
    setMessage(error instanceof Error ? error.message : 'Požadavek se nepodařilo dokončit.');
  }

  useEffect(() => {
    apiFetch<Me>('/api/auth/me').then(setMe).catch(() => setMe(null));
  }, []);

  useEffect(() => {
    if (!me) return;
    apiFetch<SiteResponse>('/api/site').then(data => {
      setSite(data.content);
      setSiteDirty(false);
    }).catch(handleError);
    apiFetch<AppointmentRequest[]>('/api/admin/appointments').then(setAppointments).catch(handleError);
    apiFetch<AppointmentType[]>('/api/admin/appointment-types').then(data => {
      const normalized = normalizeAppointmentTypes(data);
      setAdminAppointmentTypes(normalized);
      if (normalized[0]) setManualAppointment(current => normalized.some(x => x.title === current.visitReason) ? current : createDefaultAppointmentRequest(normalized[0]));
    }).catch(handleError);
    apiFetch<ArticleSection[]>('/api/admin/article-sections').then(data => {
      const normalized = normalizeArticleSections(data);
      setArticleSections(normalized);
      if (normalized[0]?.id) setArticleDraft(createDefaultArticleDraft(normalized[0].id));
    }).catch(handleError);
    apiFetch<WorkingHoursRule[]>('/api/admin/working-hours').then(data => setWorkingHours(mergeWorkingHours(data))).catch(handleError);
    apiFetch<WorkingHoursEvent[]>('/api/admin/working-hours/events').then(data => setWorkingHourEvents(data.map(normalizeEvent))).catch(handleError);
  }, [me]);

  useEffect(() => {
    if (!me) return;
    const controller = new AbortController();
    const timer = window.setInterval(() => {
      apiFetch<AppointmentRequest[]>('/api/admin/appointments', { signal: controller.signal })
        .then(setAppointments).catch(error => { if (!controller.signal.aborted) handleError(error); });
    }, 60000);
    return () => { window.clearInterval(timer); controller.abort(); };
  }, [me]);

  const cmsStats = useMemo(() => {
    const upcoming = appointments.filter(appointment => appointment.requestedStartAt).length;
    const openDays = workingHours.filter(rule => !rule.isClosed).length;
    const averageSlots = workingHours
      .filter(rule => !rule.isClosed)
      .reduce((sum, rule) => sum + Math.max(0, minutesBetween(rule.opensAt, rule.closesAt) / rule.slotMinutes), 0);

    return [
      { label: 'Nové objednávky', value: appointments.length.toString(), detail: `${upcoming} s vybraným termínem` },
      { label: 'Otevřené dny', value: openDays.toString(), detail: 'v aktuálním týdnu' },
      { label: 'Dostupné sloty', value: Math.round(averageSlots).toString(), detail: 'orientačně za týden' },
    ];
  }, [appointments, workingHours]);

  const calendarDays = useMemo(() => createCalendarWeek(calendarDate), [calendarDate]);
  const visibleAppointmentTypes = adminAppointmentTypes;
  const selectedManualSlot = manualSlots.find(slot => slot.startAt === manualAppointment.requestedStartAt);

  useEffect(() => {
    if (!me) return;
    const controller = new AbortController();
    setManualSlots([]);
    setManualAppointment(current => ({ ...current, requestedStartAt: null, requestedEndAt: null }));
    apiFetch<AvailableSlot[]>(`/api/availability?date=${manualAppointmentDate}&durationMinutes=${manualAppointment.durationMinutes}`, { signal: controller.signal })
      .then(setManualSlots)
      .catch(error => { if (!controller.signal.aborted) handleError(error); });
    return () => controller.abort();
  }, [me, manualAppointmentDate, manualAppointment.durationMinutes]);

  async function login(event: FormEvent) {
    event.preventDefault();
    if (saving.current) return;
    saving.current = true; setBusy(true);
    setMessage('');
    try {
      const data = await apiFetch<Me>('/api/auth/login', { method: 'POST', body: JSON.stringify({ email, password }) });
      setMe(data);
      setPassword('');
      setMessage('Přihlášení proběhlo úspěšně.');
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        setPassword('');
        setMessage('Nesprávný e-mail nebo heslo. Zkontrolujte přihlašovací údaje.');
      } else if (error instanceof ApiError && error.status === 429) {
        setMessage('Příliš mnoho pokusů o přihlášení. Počkejte chvíli a zkuste to znovu.');
      } else handleError(error);
    }
    finally { saving.current = false; setBusy(false); }
  }

  async function saveSite(event: FormEvent) {
    event.preventDefault();
    if (saving.current) return;
    saving.current = true; setBusy(true);
    try {

    if (!site) return;
    try {
      const saved = await apiFetch<SiteContent>('/api/admin/site', { method: 'PUT', body: JSON.stringify(site) });
      setSite(saved);
      setSiteDirty(false);
      setMessage('Obsah webu byl uložen a publikován.');
    } catch (error) {
      handleError(error);
      setMessage(error instanceof Error ? error.message : 'Obsah webu se nepodařilo uložit.');
    }
    } catch (error) { handleError(error); }
    finally { saving.current = false; setBusy(false); }
  }

  function updateSite(patch: Partial<SiteContent>) {
    setSite(current => current ? { ...current, ...patch } : current);
    setSiteDirty(true);
  }

  async function saveWorkingHours(event: FormEvent) {
    event.preventDefault();
    if (saving.current) return;
    saving.current = true; setBusy(true);
    try {

    const saved = await apiFetch<WorkingHoursRule[]>('/api/admin/working-hours', { method: 'PUT', body: JSON.stringify({ rules: workingHours }) });
    setWorkingHours(mergeWorkingHours(saved));
    setMessage('Ordinační hodiny byly uloženy.');
    } catch (error) { handleError(error); }
    finally { saving.current = false; setBusy(false); }
  }

  async function saveArticleSection(event: FormEvent) {
    event.preventDefault();
    if (saving.current) return;
    saving.current = true; setBusy(true);
    try {

    const method = sectionDraft.id ? 'PUT' : 'POST';
    const url = sectionDraft.id ? `/api/admin/article-sections/${sectionDraft.id}` : '/api/admin/article-sections';
    const saved = await apiFetch<ArticleSection>(url, { method, body: JSON.stringify(sectionDraft) });
    setArticleSections(sections => normalizeArticleSections([saved, ...sections.filter(section => section.id !== saved.id)]));
    setSectionDraft(createDefaultSectionDraft());
    if (!articleDraft.articleSectionId && saved.id) setArticleDraft(createDefaultArticleDraft(saved.id));
    setMessage('Sekce článků byla uložena.');
    } catch (error) { handleError(error); }
    finally { saving.current = false; setBusy(false); }
  }

  async function deleteArticleSection(id?: number) {
    if (saving.current) return;
    saving.current = true; setBusy(true);
    try {
    if (!id) return;
    await apiFetch(`/api/admin/article-sections/${id}`, { method: 'DELETE' });
    setArticleSections(sections => sections.filter(section => section.id !== id));
    if (sectionDraft.id === id) setSectionDraft(createDefaultSectionDraft());
    if (articleDraft.articleSectionId === id) setArticleDraft(createDefaultArticleDraft(articleSections.find(section => section.id !== id)?.id ?? 0));
    setMessage('Sekce byla smazána.');
    } catch (error) { handleError(error); }
    finally { saving.current = false; setBusy(false); }
  }

  async function saveArticle(event: FormEvent) {
    event.preventDefault();
    if (saving.current) return;
    saving.current = true; setBusy(true);
    try {

    if (!articleDraft.articleSectionId) {
      setMessage('Nejdřív vytvořte sekci pro článek.');
      return;
    }

    const method = articleDraft.id ? 'PUT' : 'POST';
    const url = articleDraft.id ? `/api/admin/articles/${articleDraft.id}` : '/api/admin/articles';
    const saved = await apiFetch<Article>(url, { method, body: JSON.stringify({ ...articleDraft, body: sanitizeArticleHtml(articleDraft.body) }) });
    setArticleSections(sections => normalizeArticleSections(sections.map(section => section.id === saved.articleSectionId
      ? { ...section, articles: [saved, ...section.articles.filter(article => article.id !== saved.id)] }
      : { ...section, articles: section.articles.filter(article => article.id !== saved.id) })));
    setArticleDraft(createDefaultArticleDraft(saved.articleSectionId));
    setMessage('Článek byl uložen.');
    } catch (error) { handleError(error); }
    finally { saving.current = false; setBusy(false); }
  }

  async function deleteArticle(article: Article) {
    if (saving.current) return;
    saving.current = true; setBusy(true);
    try {
    if (!article.id) return;
    await apiFetch(`/api/admin/articles/${article.id}`, { method: 'DELETE' });
    setArticleSections(sections => sections.map(section => ({ ...section, articles: section.articles.filter(item => item.id !== article.id) })));
    if (articleDraft.id === article.id) setArticleDraft(createDefaultArticleDraft(article.articleSectionId));
    setMessage('Článek byl smazán.');
    } catch (error) { handleError(error); }
    finally { saving.current = false; setBusy(false); }
  }

  async function saveAppointmentType(event: FormEvent) {
    event.preventDefault();
    if (saving.current) return;
    saving.current = true; setBusy(true);
    try {

    const method = appointmentTypeDraft.id ? 'PUT' : 'POST';
    const url = appointmentTypeDraft.id ? `/api/admin/appointment-types/${appointmentTypeDraft.id}` : '/api/admin/appointment-types';
    const saved = await apiFetch<AppointmentType>(url, { method, body: JSON.stringify(appointmentTypeDraft) });
    setAdminAppointmentTypes(types => normalizeAppointmentTypes([saved, ...types.filter(type => type.id !== saved.id)]));
    setAppointmentTypeDraft(createDefaultAppointmentTypeDraft());
    setMessage('Důvod návštěvy byl uložen.');
    } catch (error) { handleError(error); }
    finally { saving.current = false; setBusy(false); }
  }

  async function deleteAppointmentType(id?: number) {
    if (saving.current) return;
    saving.current = true; setBusy(true);
    try {
    if (!id) return;
    await apiFetch(`/api/admin/appointment-types/${id}`, { method: 'DELETE' });
    setAdminAppointmentTypes(types => types.filter(type => type.id !== id));
    if (appointmentTypeDraft.id === id) setAppointmentTypeDraft(createDefaultAppointmentTypeDraft());
    setMessage('Důvod návštěvy byl smazán.');
    } catch (error) { handleError(error); }
    finally { saving.current = false; setBusy(false); }
  }

  async function saveManualAppointment(event: FormEvent) {
    event.preventDefault();
    if (saving.current) return;
    saving.current = true; setBusy(true);
    try {

    const validation = validateAppointmentRequest(manualAppointment, visibleAppointmentTypes);
    if (!validation.ok) {
      setMessage(Object.values(validation.errors)[0] ?? 'Zkontrolujte údaje objednávky.');
      return;
    }

    try {
      await apiFetch<{ id: number }>('/api/admin/appointments', { method: 'POST', body: JSON.stringify(validation.value) });
      await apiFetch<AppointmentRequest[]>('/api/admin/appointments').then(setAppointments);
      setManualAppointment(createDefaultAppointmentRequest(visibleAppointmentTypes[0]));
      setMessage('Pacient byl ručně objednaný do kalendáře.');
      apiFetch<AvailableSlot[]>(`/api/availability?date=${manualAppointmentDate}&durationMinutes=${manualAppointment.durationMinutes}`)
        .then(setManualSlots)
        .catch(() => setManualSlots([]));
    } catch (error) {
      handleError(error);
      setMessage(error instanceof Error ? error.message : 'Objednávku se nepodařilo uložit.');
    }
    } catch (error) { handleError(error); }
    finally { saving.current = false; setBusy(false); }
  }

  function selectManualAppointmentType(title: string) {
    const type = visibleAppointmentTypes.find(item => item.title === title) ?? visibleAppointmentTypes[0];
    if (!type) return;
    setManualAppointment(current => ({ ...current, visitReason: type.title, durationMinutes: type.durationMinutes, requestedStartAt: null, requestedEndAt: null }));
  }

  function selectManualSlot(slot: AvailableSlot) {
    if (!slot.isAvailable) return;
    setManualAppointment(current => ({ ...current, requestedStartAt: slot.startAt, requestedEndAt: slot.endAt }));
  }

  async function saveWorkingHoursEvent(event: FormEvent) {
    event.preventDefault();
    if (saving.current) return;
    saving.current = true; setBusy(true);
    try {

    const method = eventDraft.id ? 'PUT' : 'POST';
    const url = eventDraft.id ? `/api/admin/working-hours/events/${eventDraft.id}` : '/api/admin/working-hours/events';
    const saved = await apiFetch<WorkingHoursEvent>(url, { method, body: JSON.stringify(eventDraft) });
    const normalized = normalizeEvent(saved);
    setWorkingHourEvents(events => [normalized, ...events.filter(item => item.id !== normalized.id)].sort((a, b) => a.startsOn.localeCompare(b.startsOn)));
    setEventDraft(createDefaultEventDraft());
    setMessage('Event provozní doby byl uložen.');
    } catch (error) { handleError(error); }
    finally { saving.current = false; setBusy(false); }
  }

  async function deleteWorkingHoursEvent(id?: number) {
    if (saving.current) return;
    saving.current = true; setBusy(true);
    try {
    if (!id) return;
    await apiFetch(`/api/admin/working-hours/events/${id}`, { method: 'DELETE' });
    setWorkingHourEvents(events => events.filter(event => event.id !== id));
    if (eventDraft.id === id) setEventDraft(createDefaultEventDraft());
    setMessage('Event byl smazán.');
    } catch (error) { handleError(error); }
    finally { saving.current = false; setBusy(false); }
  }

  async function cancelAppointment(id: number) {
    try {
      await apiFetch(`/api/admin/appointments/${id}/cancel`, { method: 'POST' });
      setAppointments(items => items.map(x => x.id === id ? { ...x, status: 'Cancelled' } : x));
      setMessage('Rezervace byla zrušena.');
    } catch (error) { handleError(error); }
  }
  async function logout() {
    try { await apiFetch('/api/auth/logout', { method: 'POST' }); setMe(null); setPassword(''); setMessage('Byli jste odhlášeni.'); }
    catch (error) { handleError(error); }
  }

  function updateRule(dayOfWeek: number, patch: Partial<WorkingHoursRule>) {
    setWorkingHours(rules => rules.map(rule => rule.dayOfWeek === dayOfWeek ? { ...rule, ...patch } : rule));
  }

  function updateEventRule(dayOfWeek: number, patch: Partial<WorkingHoursRule>) {
    setEventDraft(draft => ({ ...draft, rules: draft.rules.map(rule => rule.dayOfWeek === dayOfWeek ? { ...rule, ...patch } : rule) }));
  }

  if (!me) {
    return (
      <AdminLogin
        email={email}
        password={password}
        message={message}
        busy={busy}
        onEmailChange={setEmail}
        onPasswordChange={setPassword}
        onSubmit={login}
      />
    );
  }

  return (
    <AdminLayout me={me} site={site} activeSection={activeSection} message={message} onSectionChange={setActiveSection}>
      <button className="button secondary" type="button" onClick={logout}>Odhlásit</button>
      <fieldset disabled={busy} style={{ border: 0, padding: 0, minWidth: 0 }}>
      {activeSection === 'dashboard' && (
        <DashboardSection
          stats={cmsStats}
          workingHours={workingHours}
          appointments={appointments}
          onSectionChange={setActiveSection}
        />
      )}

      {activeSection === 'content' && site && (
        <ContentSection
          site={site}
          siteDirty={siteDirty}
          onSubmit={saveSite}
          onChange={updateSite}
        />
      )}

      {activeSection === 'articles' && (
        <ArticlesSection
          sections={articleSections}
          sectionDraft={sectionDraft}
          articleDraft={articleDraft}
          onSectionSubmit={saveArticleSection}
          onArticleSubmit={saveArticle}
          onSectionDraftChange={setSectionDraft}
          onArticleDraftChange={setArticleDraft}
          onSectionDelete={deleteArticleSection}
          onArticleDelete={deleteArticle}
        />
      )}

      {activeSection === 'hours' && (
        <HoursSection
          workingHours={workingHours}
          eventDraft={eventDraft}
          events={workingHourEvents}
          onWorkingHoursSubmit={saveWorkingHours}
          onEventSubmit={saveWorkingHoursEvent}
          onRuleChange={updateRule}
          onEventRuleChange={updateEventRule}
          onEventDraftChange={setEventDraft}
          onEventDelete={deleteWorkingHoursEvent}
        />
      )}

      {activeSection === 'appointments' && (
        <AppointmentsSection
          appointments={appointments}
          calendarDate={calendarDate}
          calendarDays={calendarDays}
          manualAppointmentDate={manualAppointmentDate}
          manualAppointment={manualAppointment}
          manualSlots={manualSlots}
          selectedManualSlot={selectedManualSlot}
          appointmentTypes={visibleAppointmentTypes}
          onCancel={cancelAppointment}
          onCalendarDateChange={setCalendarDate}
          onManualDateChange={setManualAppointmentDate}
          onManualAppointmentChange={setManualAppointment}
          onManualAppointmentTypeSelect={selectManualAppointmentType}
          onManualSlotSelect={selectManualSlot}
          onManualAppointmentSubmit={saveManualAppointment}
        />
      )}

      {activeSection === 'settings' && (
        <SettingsSection
          me={me}
          appointmentTypes={adminAppointmentTypes}
          appointmentTypeDraft={appointmentTypeDraft}
          onAppointmentTypeSubmit={saveAppointmentType}
          onAppointmentTypeDraftChange={setAppointmentTypeDraft}
          onAppointmentTypeDelete={deleteAppointmentType}
        />
      )}
      </fieldset>
    </AdminLayout>
  );
}
