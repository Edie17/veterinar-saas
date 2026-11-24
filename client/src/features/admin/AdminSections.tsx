import type { FormEvent } from 'react';
import type { AppointmentRequest, AppointmentType, Article, ArticleSection, AvailableSlot, SiteContent, WorkingHoursEvent, WorkingHoursRule } from '../../api';
import { AppointmentRow, CalendarAppointmentCard } from '../../components/Appointments';
import { RichTextEditor } from '../../components/RichTextEditor';
import { firstParagraph } from '../../shared/articles';
import { dayNames } from '../../shared/constants';
import { addDays, formatCalendarDay, formatDate, formatTime, getAppointmentDateKey, parseDateInput, toDateInput } from '../../shared/date';
import { createDefaultAppointmentTypeDraft, createDefaultArticleDraft, createDefaultEventDraft, createDefaultSectionDraft, normalizeEvent } from '../../shared/drafts';
import type { Me } from '../../types';

type CmsStat = {
  label: string;
  value: string;
  detail: string;
};

export function DashboardSection({
  stats,
  workingHours,
  appointments,
  onSectionChange,
}: {
  stats: CmsStat[];
  workingHours: WorkingHoursRule[];
  appointments: AppointmentRequest[];
  onSectionChange: (section: 'hours' | 'appointments') => void;
}) {
  return (
    <section className="cmsSectionStack">
      <div className="cmsStatsGrid">
        {stats.map(stat => <article className="cmsStatCard" key={stat.label}><span>{stat.label}</span><strong>{stat.value}</strong><small>{stat.detail}</small></article>)}
      </div>
      <div className="cmsGridTwo">
        <section className="cmsCard">
          <div className="cmsCardHeader"><div><p className="eyebrow">Dnešní provoz</p><h2>Ordinační režim</h2></div><button className="button secondary" onClick={() => onSectionChange('hours')}>Spravovat</button></div>
          <div className="cmsTimeline">
            {workingHours.slice(0, 5).map(rule => <div key={rule.dayOfWeek}><strong>{dayNames[rule.dayOfWeek]}</strong><span>{rule.isClosed ? 'Zavřeno' : `${rule.opensAt}–${rule.closesAt} · ${rule.slotMinutes} min`}</span></div>)}
          </div>
        </section>
        <section className="cmsCard">
          <div className="cmsCardHeader"><div><p className="eyebrow">Objednávky</p><h2>Poslední poptávky</h2></div><button className="button secondary" onClick={() => onSectionChange('appointments')}>Zobrazit</button></div>
          {appointments.length === 0 ? <p>Zatím žádné objednávky.</p> : appointments.slice(0, 4).map(appointment => <AppointmentRow appointment={appointment} key={appointment.id} />)}
        </section>
      </div>
    </section>
  );
}

export function ContentSection({
  site,
  siteDirty,
  onSubmit,
  onChange,
}: {
  site: SiteContent;
  siteDirty: boolean;
  onSubmit: (event: FormEvent) => void;
  onChange: (patch: Partial<SiteContent>) => void;
}) {
  return (
    <form className="cmsCard cmsForm" onSubmit={onSubmit}>
      <div className="cmsCardHeader">
        <div><p className="eyebrow">CMS obsah</p><h2>Veřejná úvodní stránka</h2>{siteDirty && <p className="cmsUnsaved">Máte neuložené změny.</p>}</div>
        <button className="button">{siteDirty ? 'Uložit a publikovat' : 'Publikováno'}</button>
      </div>
      <div className="cmsFormGrid">
        <label className="field"><span>Název ordinace</span><input value={site.clinicName} onChange={event => onChange({ clinicName: event.target.value })} /></label>
        <label className="field"><span>Telefon</span><input value={site.phone} onChange={event => onChange({ phone: event.target.value })} /></label>
        <label className="field wide"><span>Hlavní nadpis</span><input value={site.heroTitle} onChange={event => onChange({ heroTitle: event.target.value })} /></label>
        <label className="field wide"><span>Hero text</span><textarea value={site.heroText} onChange={event => onChange({ heroText: event.target.value })} /></label>
        <label className="field"><span>E-mail</span><input value={site.email} onChange={event => onChange({ email: event.target.value })} /></label>
        <label className="field"><span>Adresa</span><input value={site.address} onChange={event => onChange({ address: event.target.value })} /></label>
      </div>
    </form>
  );
}

export function ArticlesSection({
  sections,
  sectionDraft,
  articleDraft,
  onSectionSubmit,
  onArticleSubmit,
  onSectionDraftChange,
  onArticleDraftChange,
  onSectionDelete,
  onArticleDelete,
}: {
  sections: ArticleSection[];
  sectionDraft: ArticleSection;
  articleDraft: Article;
  onSectionSubmit: (event: FormEvent) => void;
  onArticleSubmit: (event: FormEvent) => void;
  onSectionDraftChange: (section: ArticleSection) => void;
  onArticleDraftChange: (article: Article) => void;
  onSectionDelete: (id?: number) => void;
  onArticleDelete: (article: Article) => void;
}) {
  return (
    <section className="cmsSectionStack cmsArticlesStack">
      <form className="cmsCard cmsForm cmsArticleEditorCard" onSubmit={onArticleSubmit}>
        <div className="cmsCardHeader">
          <div><p className="eyebrow">Článek</p><h2>{articleDraft.id ? 'Upravit článek' : 'Nový článek'}</h2><p>Článek se zobrazí jen v publikované sekci.</p></div>
          <div className="cmsTopbarActions"><button className="button secondary" type="button" onClick={() => onArticleDraftChange(createDefaultArticleDraft(sections[0]?.id ?? 0))}>Nový</button><button className="button">Uložit</button></div>
        </div>
        <div className="cmsFormGrid cmsArticleFormGrid">
          <label className="field"><span>Sekce</span><select value={articleDraft.articleSectionId} onChange={event => onArticleDraftChange({ ...articleDraft, articleSectionId: Number(event.target.value) })}><option value={0}>Vyberte sekci</option>{sections.map(section => <option value={section.id} key={section.id}>{section.title}</option>)}</select></label>
          <label className="field"><span>Název článku</span><input required value={articleDraft.title} onChange={event => onArticleDraftChange({ ...articleDraft, title: event.target.value })} /></label>
          <label className="field"><span>URL slug</span><input value={articleDraft.slug} placeholder="automaticky-z-nazvu" onChange={event => onArticleDraftChange({ ...articleDraft, slug: event.target.value })} /></label>
          <label className="field"><span>Pořadí</span><input type="number" value={articleDraft.sortOrder} onChange={event => onArticleDraftChange({ ...articleDraft, sortOrder: Number(event.target.value) })} /></label>
          <label className="cmsSwitch"><input type="checkbox" checked={articleDraft.isPublished} onChange={event => onArticleDraftChange({ ...articleDraft, isPublished: event.target.checked })} /><span>Publikováno</span></label>
          <label className="field wide"><span>Krátký popis</span><textarea value={articleDraft.summary} onChange={event => onArticleDraftChange({ ...articleDraft, summary: event.target.value })} /></label>
          <div className="field wide">
            <span>Text článku</span>
            <RichTextEditor value={articleDraft.body} onChange={body => onArticleDraftChange({ ...articleDraft, body })} />
          </div>
        </div>
      </form>

      <form className="cmsCard cmsForm cmsSectionEditorCard" onSubmit={onSectionSubmit}>
        <div className="cmsCardHeader">
          <div><p className="eyebrow">Sekce</p><h2>{sectionDraft.id ? 'Upravit sekci' : 'Nová sekce'}</h2><p>Sekce seskupují články na veřejném webu.</p></div>
          <div className="cmsTopbarActions"><button className="button secondary" type="button" onClick={() => onSectionDraftChange(createDefaultSectionDraft())}>Nová</button><button className="button">Uložit</button></div>
        </div>
        <div className="cmsFormGrid cmsSectionFormGrid">
          <label className="field"><span>Název sekce</span><input required value={sectionDraft.title} onChange={event => onSectionDraftChange({ ...sectionDraft, title: event.target.value })} /></label>
          <label className="field"><span>URL slug</span><input value={sectionDraft.slug} placeholder="automaticky-z-nazvu" onChange={event => onSectionDraftChange({ ...sectionDraft, slug: event.target.value })} /></label>
          <label className="field"><span>Pořadí</span><input type="number" value={sectionDraft.sortOrder} onChange={event => onSectionDraftChange({ ...sectionDraft, sortOrder: Number(event.target.value) })} /></label>
          <label className="cmsSwitch"><input type="checkbox" checked={sectionDraft.isPublished} onChange={event => onSectionDraftChange({ ...sectionDraft, isPublished: event.target.checked })} /><span>Publikováno</span></label>
          <label className="field wide"><span>Popis sekce</span><textarea value={sectionDraft.description} onChange={event => onSectionDraftChange({ ...sectionDraft, description: event.target.value })} /></label>
        </div>
      </form>

      <section className="cmsCard">
        <div className="cmsCardHeader"><div><p className="eyebrow">Přehled</p><h2>Sekce a články</h2></div><span className="cmsBadge">{sections.reduce((sum, section) => sum + section.articles.length, 0)} článků</span></div>
        <div className="cmsArticleManager">
          {sections.length === 0 ? <p>Zatím není vytvořená žádná sekce.</p> : sections.map(section => (
            <article className="cmsArticleSection" key={section.id}>
              <div className="cmsArticleSectionTitle">
                <div><strong>{section.title}</strong><span>{section.isPublished ? 'Publikováno' : 'Skryto'} · /{section.slug || 'bez-slugu'}</span></div>
                <div className="cmsRowActions"><button className="button secondary" type="button" onClick={() => onSectionDraftChange(section)}>Upravit</button><button className="button danger" type="button" onClick={() => onSectionDelete(section.id)}>Smazat</button></div>
              </div>
              <div className="cmsTable">
                {section.articles.length === 0 ? <p>V této sekci zatím nejsou články.</p> : section.articles.map(article => (
                  <div className="cmsArticleRow" key={article.id}>
                    <div><strong>{article.title}</strong><span>{article.isPublished ? 'Publikováno' : 'Skryto'} · /{article.slug || 'bez-slugu'}</span></div>
                    <p>{article.summary || firstParagraph(article.body)}</p>
                    <div className="cmsRowActions"><button className="button secondary" type="button" onClick={() => onArticleDraftChange(article)}>Upravit</button><button className="button danger" type="button" onClick={() => onArticleDelete(article)}>Smazat</button></div>
                  </div>
                ))}
              </div>
            </article>
          ))}
        </div>
      </section>
    </section>
  );
}

export function HoursSection({
  workingHours,
  eventDraft,
  events,
  onWorkingHoursSubmit,
  onEventSubmit,
  onRuleChange,
  onEventRuleChange,
  onEventDraftChange,
  onEventDelete,
}: {
  workingHours: WorkingHoursRule[];
  eventDraft: WorkingHoursEvent;
  events: WorkingHoursEvent[];
  onWorkingHoursSubmit: (event: FormEvent) => void;
  onEventSubmit: (event: FormEvent) => void;
  onRuleChange: (dayOfWeek: number, patch: Partial<WorkingHoursRule>) => void;
  onEventRuleChange: (dayOfWeek: number, patch: Partial<WorkingHoursRule>) => void;
  onEventDraftChange: (event: WorkingHoursEvent) => void;
  onEventDelete: (id?: number) => void;
}) {
  return (
    <section className="cmsSectionStack">
      <form className="cmsCard cmsForm" onSubmit={onWorkingHoursSubmit}>
        <div className="cmsCardHeader"><div><p className="eyebrow">Dostupnost</p><h2>Ordinační hodiny a sloty</h2><p>Nastavení určuje dostupné termíny na veřejném objednávkovém formuláři.</p></div><button className="button">Uložit ordinační hodiny</button></div>
        <WorkingHoursEditor rules={workingHours} onRuleChange={onRuleChange} />
      </form>

      <form className="cmsCard cmsForm" onSubmit={onEventSubmit}>
        <div className="cmsCardHeader">
          <div><p className="eyebrow">Výjimky</p><h2>Eventová otevírací doba</h2><p>Od data zobrazení se event ukáže dopředu. Během eventu nahradí standardní provoz.</p></div>
          <div className="cmsTopbarActions"><button className="button secondary" type="button" onClick={() => onEventDraftChange(createDefaultEventDraft())}>Nový event</button><button className="button">Uložit event</button></div>
        </div>
        <div className="cmsFormGrid">
          <label className="field"><span>Název eventu</span><input required value={eventDraft.title} onChange={event => onEventDraftChange({ ...eventDraft, title: event.target.value })} /></label>
          <label className="field"><span>Zobrazovat od</span><input type="date" required value={eventDraft.displayFrom} onChange={event => onEventDraftChange({ ...eventDraft, displayFrom: event.target.value })} /></label>
          <label className="field"><span>Platí od</span><input type="date" required value={eventDraft.startsOn} onChange={event => onEventDraftChange({ ...eventDraft, startsOn: event.target.value })} /></label>
          <label className="field"><span>Platí do</span><input type="date" required value={eventDraft.endsOn} onChange={event => onEventDraftChange({ ...eventDraft, endsOn: event.target.value })} /></label>
          <label className="field wide"><span>Poznámka pro web</span><textarea value={eventDraft.note ?? ''} onChange={event => onEventDraftChange({ ...eventDraft, note: event.target.value })} /></label>
          <label className="cmsSwitch"><input type="checkbox" checked={eventDraft.isPublished} onChange={event => onEventDraftChange({ ...eventDraft, isPublished: event.target.checked })} /><span>Publikováno</span></label>
        </div>
        <WorkingHoursEditor rules={eventDraft.rules} onRuleChange={onEventRuleChange} />
      </form>

      <section className="cmsCard">
        <div className="cmsCardHeader"><div><p className="eyebrow">Naplánováno</p><h2>Budoucí a aktivní eventy</h2></div><span className="cmsBadge">{events.length} celkem</span></div>
        <div className="cmsTable">
          {events.length === 0 ? <p>Zatím není nastavený žádný event.</p> : events.map(event => (
            <article className="cmsEventRow" key={event.id}>
              <div><strong>{event.title}</strong><span>{formatDate(event.startsOn)} - {formatDate(event.endsOn)} · zobrazit od {formatDate(event.displayFrom)}</span></div>
              <div><span>{event.isPublished ? 'Publikováno' : 'Skryto'}</span>{event.note && <small>{event.note}</small>}</div>
              <div className="cmsRowActions"><button className="button secondary" type="button" onClick={() => onEventDraftChange(normalizeEvent(event))}>Upravit</button><button className="button danger" type="button" onClick={() => onEventDelete(event.id)}>Smazat</button></div>
            </article>
          ))}
        </div>
      </section>
    </section>
  );
}

function WorkingHoursEditor({ rules, onRuleChange }: { rules: WorkingHoursRule[]; onRuleChange: (dayOfWeek: number, patch: Partial<WorkingHoursRule>) => void }) {
  return (
    <div className="cmsHoursList">
      {rules.map(rule => (
        <fieldset className="cmsHoursRow" key={rule.dayOfWeek}>
          <legend>{dayNames[rule.dayOfWeek]}</legend>
          <label className="cmsSwitch"><input type="checkbox" checked={!rule.isClosed} onChange={event => onRuleChange(rule.dayOfWeek, { isClosed: !event.target.checked })} /><span>Otevřeno</span></label>
          <label><span>Od</span><input type="time" value={rule.opensAt} disabled={rule.isClosed} onChange={event => onRuleChange(rule.dayOfWeek, { opensAt: event.target.value })} /></label>
          <label><span>Do</span><input type="time" value={rule.closesAt} disabled={rule.isClosed} onChange={event => onRuleChange(rule.dayOfWeek, { closesAt: event.target.value })} /></label>
          <label><span>Slot</span><select value={rule.slotMinutes} disabled={rule.isClosed} onChange={event => onRuleChange(rule.dayOfWeek, { slotMinutes: Number(event.target.value) })}><option value={15}>15 min</option><option value={20}>20 min</option><option value={30}>30 min</option><option value={45}>45 min</option><option value={60}>60 min</option></select></label>
        </fieldset>
      ))}
    </div>
  );
}

export function AppointmentsSection({
  appointments,
  calendarDate,
  calendarDays,
  manualAppointmentDate,
  manualAppointment,
  manualSlots,
  selectedManualSlot,
  appointmentTypes,
  onCalendarDateChange,
  onManualDateChange,
  onManualAppointmentChange,
  onManualAppointmentTypeSelect,
  onManualSlotSelect,
  onManualAppointmentSubmit,
  onCancel,
}: {
  appointments: AppointmentRequest[];
  calendarDate: string;
  calendarDays: Array<{ dateKey: string; shortName: string; isToday: boolean }>;
  manualAppointmentDate: string;
  manualAppointment: AppointmentRequest;
  manualSlots: AvailableSlot[];
  selectedManualSlot?: AvailableSlot;
  appointmentTypes: AppointmentType[];
  onCalendarDateChange: (value: string) => void;
  onManualDateChange: (value: string) => void;
  onManualAppointmentChange: (appointment: AppointmentRequest) => void;
  onManualAppointmentTypeSelect: (title: string) => void;
  onManualSlotSelect: (slot: AvailableSlot) => void;
  onManualAppointmentSubmit: (event: FormEvent) => void;
  onCancel: (id: number) => void;
}) {
  return (
    <section className="cmsSectionStack">
      <section className="cmsCard">
        <div className="cmsCardHeader">
          <div><p className="eyebrow">Kalendář</p><h2>Objednaní pacienti</h2><p>Týdenní přehled termínů podle vybraného data.</p></div>
          <div className="cmsCalendarToolbar">
            <button className="button secondary" type="button" onClick={() => onCalendarDateChange(toDateInput(addDays(parseDateInput(calendarDate), -7)))}>Předchozí</button>
            <input type="date" value={calendarDate} onChange={event => onCalendarDateChange(event.target.value)} aria-label="Vybrat týden v kalendáři" />
            <button className="button secondary" type="button" onClick={() => onCalendarDateChange(toDateInput(new Date()))}>Dnes</button>
            <button className="button secondary" type="button" onClick={() => onCalendarDateChange(toDateInput(addDays(parseDateInput(calendarDate), 7)))}>Další</button>
          </div>
        </div>
        <div className="cmsCalendarGrid">
          {calendarDays.map(day => {
            const dayAppointments = appointments
              .filter(appointment => appointment.status !== "Cancelled" && appointment.requestedStartAt && getAppointmentDateKey(appointment.requestedStartAt) === day.dateKey)
              .sort((a, b) => (a.requestedStartAt ?? '').localeCompare(b.requestedStartAt ?? ''));

            return (
              <article className={day.isToday ? 'cmsCalendarDay today' : 'cmsCalendarDay'} key={day.dateKey}>
                <div className="cmsCalendarDayHeader">
                  <strong>{day.shortName}</strong>
                  <span>{formatCalendarDay(day.dateKey)}</span>
                </div>
                <div className="cmsCalendarAppointments">
                  {dayAppointments.length === 0 ? <p>Volno</p> : dayAppointments.map(appointment => <div key={appointment.id ?? `${appointment.phone}-${appointment.requestedStartAt}`}><CalendarAppointmentCard appointment={appointment} /><button type="button" className="button secondary" onClick={() => appointment.id && onCancel(appointment.id)}>Zrušit rezervaci</button></div>)}
                </div>
              </article>
            );
          })}
        </div>
      </section>

      <form className="cmsCard cmsForm" onSubmit={onManualAppointmentSubmit}>
        <div className="cmsCardHeader">
          <div><p className="eyebrow">Ruční zápis</p><h2>Doregistrovat pacienta</h2><p>Pro telefonické objednávky nebo klienty, kterým nejde webový formulář.</p></div>
          <button className="button">Uložit do kalendáře</button>
        </div>
        <div className="cmsManualBooking">
          <div className="cmsFormGrid">
            <label className="field"><span>Datum</span><input type="date" required value={manualAppointmentDate} onChange={event => onManualDateChange(event.target.value)} /></label>
            <label className="field"><span>Služba</span><select value={manualAppointment.visitReason} onChange={event => onManualAppointmentTypeSelect(event.target.value)}>{appointmentTypes.map(type => <option value={type.title} key={type.id ?? type.title}>{type.title} · {type.durationMinutes} min</option>)}</select></label>
            <label className="field"><span>Jméno klienta</span><input required value={manualAppointment.name} onChange={event => onManualAppointmentChange({ ...manualAppointment, name: event.target.value })} /></label>
            <label className="field"><span>Telefon</span><input required value={manualAppointment.phone} onChange={event => onManualAppointmentChange({ ...manualAppointment, phone: event.target.value })} /></label>
            <label className="field"><span>E-mail</span><input type="email" required value={manualAppointment.email ?? ''} onChange={event => onManualAppointmentChange({ ...manualAppointment, email: event.target.value })} /></label>
            <label className="field"><span>Jméno pacienta</span><input required value={manualAppointment.petName} onChange={event => onManualAppointmentChange({ ...manualAppointment, petName: event.target.value })} /></label>
            <label className="field"><span>Druh zvířete</span><input required value={manualAppointment.petType} onChange={event => onManualAppointmentChange({ ...manualAppointment, petType: event.target.value })} /></label>
            <label className="field wide"><span>Poznámka</span><textarea value={manualAppointment.message ?? ''} onChange={event => onManualAppointmentChange({ ...manualAppointment, message: event.target.value })} /></label>
          </div>
          <aside className="cmsSlotPicker">
            <div>
              <strong>Čas návštěvy</strong>
              <span>{selectedManualSlot ? `${formatTime(selectedManualSlot.startAt)} - ${formatTime(selectedManualSlot.endAt)}` : 'Vyberte volný čas'}</span>
            </div>
            <div className="slotGrid compact">
              {manualSlots.filter(slot => slot.isAvailable).length === 0 ? <p>Pro tento den nejsou dostupné sloty.</p> : manualSlots.filter(slot => slot.isAvailable).map(slot => (
                <button
                  className={`slot${manualAppointment.requestedStartAt === slot.startAt ? ' selected' : ''}`}
                  type="button"
                  onClick={() => onManualSlotSelect(slot)}
                  key={slot.startAt}
                >
                  {formatTime(slot.startAt)}
                </button>
              ))}
            </div>
          </aside>
        </div>
      </form>

      <section className="cmsCard">
        <div className="cmsCardHeader"><div><p className="eyebrow">Seznam</p><h2>Všechny objednávky</h2></div><span className="cmsBadge">{appointments.length} celkem</span></div>
        <div className="cmsTable">
          {appointments.length === 0 ? <p>Zatím žádné objednávky.</p> : appointments.map(appointment => <AppointmentRow appointment={appointment} key={appointment.id} detailed />)}
        </div>
      </section>
    </section>
  );
}

export function SettingsSection({
  me,
  appointmentTypes,
  appointmentTypeDraft,
  onAppointmentTypeSubmit,
  onAppointmentTypeDraftChange,
  onAppointmentTypeDelete,
}: {
  me: Me;
  appointmentTypes: AppointmentType[];
  appointmentTypeDraft: AppointmentType;
  onAppointmentTypeSubmit: (event: FormEvent) => void;
  onAppointmentTypeDraftChange: (type: AppointmentType) => void;
  onAppointmentTypeDelete: (id?: number) => void;
}) {
  return (
    <section className="cmsSectionStack">
      <form className="cmsCard cmsForm" onSubmit={onAppointmentTypeSubmit}>
        <div className="cmsCardHeader">
          <div><p className="eyebrow">Rezervační formulář</p><h2>{appointmentTypeDraft.id ? 'Upravit důvod návštěvy' : 'Nový důvod návštěvy'}</h2><p>Délka návštěvy určuje, jak dlouho bude termín blokovaný v kalendáři.</p></div>
          <div className="cmsTopbarActions"><button className="button secondary" type="button" onClick={() => onAppointmentTypeDraftChange(createDefaultAppointmentTypeDraft())}>Nový</button><button className="button">Uložit</button></div>
        </div>
        <div className="cmsFormGrid">
          <label className="field"><span>Název důvodu</span><input required value={appointmentTypeDraft.title} onChange={event => onAppointmentTypeDraftChange({ ...appointmentTypeDraft, title: event.target.value })} /></label>
          <label className="field"><span>Délka návštěvy</span><select value={appointmentTypeDraft.durationMinutes} onChange={event => onAppointmentTypeDraftChange({ ...appointmentTypeDraft, durationMinutes: Number(event.target.value) })}><option value={10}>10 min</option><option value={15}>15 min</option><option value={20}>20 min</option><option value={30}>30 min</option><option value={45}>45 min</option><option value={60}>60 min</option><option value={90}>90 min</option></select></label>
          <label className="field"><span>Pořadí</span><input type="number" value={appointmentTypeDraft.sortOrder} onChange={event => onAppointmentTypeDraftChange({ ...appointmentTypeDraft, sortOrder: Number(event.target.value) })} /></label>
          <label className="cmsSwitch"><input type="checkbox" checked={appointmentTypeDraft.isPublished} onChange={event => onAppointmentTypeDraftChange({ ...appointmentTypeDraft, isPublished: event.target.checked })} /><span>Zobrazit ve formuláři</span></label>
        </div>
      </form>

      <section className="cmsCard">
        <div className="cmsCardHeader"><div><p className="eyebrow">Důvody návštěv</p><h2>Délky rezervací</h2></div><span className="cmsBadge">{appointmentTypes.length} celkem</span></div>
        <div className="cmsTable">
          {appointmentTypes.length === 0 ? <p>Zatím není nastavený žádný důvod návštěvy.</p> : appointmentTypes.map(type => (
            <article className="cmsEventRow" key={type.id ?? type.title}>
              <div><strong>{type.title}</strong><span>{type.durationMinutes} min · pořadí {type.sortOrder}</span></div>
              <div><span>{type.isPublished ? 'Zobrazeno ve formuláři' : 'Skryto'}</span></div>
              <div className="cmsRowActions"><button className="button secondary" type="button" onClick={() => onAppointmentTypeDraftChange(type)}>Upravit</button><button className="button danger" type="button" onClick={() => onAppointmentTypeDelete(type.id)}>Smazat</button></div>
            </article>
          ))}
        </div>
      </section>

      <section className="cmsCard">
        <div className="cmsCardHeader"><div><p className="eyebrow">Ordinace</p><h2>Přihlášený účet</h2></div></div>
        <div className="cmsSettingsGrid">
          <article><strong>E-mail</strong><span>{me.email}</span></article>
          <article><strong>Role</strong><span>{me.role}</span></article>
          <article><strong>Bezpečnost</strong><span>Cookie auth + CSRF token</span></article>
          <article><strong>Rezervace</strong><span>Potvrzení e-mailem do 15 minut.</span></article>
        </div>
      </section>
    </section>
  );
}
