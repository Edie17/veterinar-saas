import { useEffect, useMemo, useState } from 'react';
import { apiFetch, AppointmentRequest, AppointmentType, AvailableSlot, PublicWorkingHoursSchedule } from '../../api';
import { useRef } from 'react';
import heroBackground from '../../assets/hero-pets-family.webp';
import vetExamCatSmall from '../../assets/vet-exam-cat-640.webp';
import vetExamCatMedium from '../../assets/vet-exam-cat-960.webp';
import vetExamCat from '../../assets/vet-exam-cat.webp';
import { ArticleCards, ArticleDetailPage, ArticlesIndexPage } from '../../components/Articles';
import { CookieConsent } from '../../components/CookieConsent';
import { HoursTable } from '../../components/HoursTable';
import { PublicFooter } from '../../components/PublicFooter';
import { PublicNav } from '../../components/PublicNav';
import { MobileBookingActions } from '../../components/MobileBookingActions';
import { articlePath, firstParagraph, getArticleSlug } from '../../shared/articles';
import { defaultAppointmentTypes, defaultPublicSite, defaultWorkingHours, urgentTopics, visitPrepItems } from '../../shared/constants';
import { formatCalendarDay, formatDate, formatDateTime, formatTime, getAppointmentDateKey, groupSlotsByPart, minutesBetween, toDateInput } from '../../shared/date';
import { createDefaultAppointmentRequest, mergeWorkingHours } from '../../shared/drafts';
import { phoneHref } from '../../shared/navigation';
import { SeoMeta, StructuredData } from '../../shared/seo';
import { validateAppointmentRequest, type AppointmentValidationErrors } from '../../shared/appointmentValidation';
import type { ArticleItem, SiteResponse } from '../../types';
export function PublicSite() {
  const [site, setSite] = useState<SiteResponse>(defaultPublicSite);
  const [siteLoading, setSiteLoading] = useState(true);
  const [siteLoadFailed, setSiteLoadFailed] = useState(false);
  const [appointmentTypes, setAppointmentTypes] = useState<AppointmentType[]>([]);
  const [form, setForm] = useState<AppointmentRequest>(() => createDefaultAppointmentRequest(defaultAppointmentTypes[0]));
  const [selectedDate, setSelectedDate] = useState(() => toDateInput(new Date()));
  const [slots, setSlots] = useState<AvailableSlot[]>([]);
  const [schedule, setSchedule] = useState<PublicWorkingHoursSchedule | null>(null);
  const [busy, setBusy] = useState(false);
  const [slotsLoading, setSlotsLoading] = useState(false);
  const [loadError, setLoadError] = useState('');
  const [bookingLoadError, setBookingLoadError] = useState('');
  const [bookingReload, setBookingReload] = useState(0);
  const [status, setStatus] = useState('');
  const [reservationId, setReservationId] = useState<number | null>(null);
  const [formErrors, setFormErrors] = useState<AppointmentValidationErrors>({});
  const [nearestBusy, setNearestBusy] = useState(false);
  const [nearestSlot, setNearestSlot] = useState<AvailableSlot | null>(null);
  const [nearestMessage, setNearestMessage] = useState('');
  const [slotsForDate, setSlotsForDate] = useState('');
  const [slotsReload, setSlotsReload] = useState(0);
  const nearestRequest = useRef<AbortController | null>(null);
  const todayDate = useMemo(() => toDateInput(new Date()), []);
  const lastDate = useMemo(() => toDateInput(new Date(Date.now() + 60 * 86400000)), []);
  useEffect(() => () => nearestRequest.current?.abort(), []);

  useEffect(() => {
    const controller = new AbortController();
    apiFetch<SiteResponse>('/api/site', { signal: controller.signal })
      .then(data => { if (!controller.signal.aborted) setSite(data); })
      .catch(() => { if (!controller.signal.aborted) { setSiteLoadFailed(true); setLoadError('Obsah webu se nepodařilo načíst. Zkuste stránku obnovit.'); } })
      .finally(() => { if (!controller.signal.aborted) setSiteLoading(false); });
    return () => controller.abort();
  }, []);

  useEffect(() => {
    apiFetch<PublicWorkingHoursSchedule>('/api/public/working-hours')
      .then(data => setSchedule({ ...data, standardRules: mergeWorkingHours(data.standardRules) }))
      .catch(() => setLoadError('Ordinační hodiny se nepodařilo načíst. Zkuste stránku obnovit.'));
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    setBookingLoadError('');
    apiFetch<AppointmentType[]>('/api/appointment-types', { signal: controller.signal })
      .then(data => {
        if (controller.signal.aborted) return;
        const normalized = data;
        setAppointmentTypes(normalized);
        if (normalized[0]) setForm(createDefaultAppointmentRequest(normalized[0]));
        else setBookingLoadError('Ordinace nyní nenabízí žádné online termíny. Kontaktujte ji prosím telefonicky.');
      })
      .catch(() => { if (!controller.signal.aborted) setBookingLoadError('Rezervace se nepodařilo načíst. Zkuste načtení zopakovat nebo kontaktujte ordinaci telefonicky.'); });
    return () => controller.abort();
  }, [bookingReload]);

  useEffect(() => {
    if (!appointmentTypes.length) return;
    if (selectedDate < todayDate) {
      setSelectedDate(todayDate);
      return;
    }

    const controller = new AbortController();
    setSlots([]);
    setSlotsLoading(true);
    setForm(current => ({ ...current, requestedStartAt: null, requestedEndAt: null }));
    apiFetch<AvailableSlot[]>(`/api/availability?date=${selectedDate}&durationMinutes=${form.durationMinutes}`, { signal: controller.signal })
      .then(data => { if (!controller.signal.aborted) { setSlots(data); setSlotsForDate(selectedDate); } })
      .catch(error => { if (!controller.signal.aborted) setStatus(error instanceof Error ? error.message : 'Termíny se nepodařilo načíst.'); })
      .finally(() => { if (!controller.signal.aborted) setSlotsLoading(false); });
    return () => controller.abort();
  }, [selectedDate, form.durationMinutes, todayDate, appointmentTypes.length, slotsReload]);

  useEffect(() => {
    if (!nearestSlot || slotsLoading || slotsForDate !== selectedDate || selectedDate !== getAppointmentDateKey(nearestSlot.startAt)) return;
    const match = slots.find(slot => slot.startAt === nearestSlot.startAt && slot.isAvailable);
    if (match) {
      setForm(current => ({ ...current, requestedStartAt: match.startAt, requestedEndAt: match.endAt }));
      setNearestSlot(null);
    } else { setNearestSlot(null); setNearestMessage('Termín mezitím přestal být dostupný. Vyberte jiný čas nebo hledání zopakujte.'); }
  }, [nearestSlot, slots, slotsLoading, selectedDate, slotsForDate]);

  async function findNearest() {
    nearestRequest.current?.abort();
    const controller = new AbortController(); nearestRequest.current = controller;
    setNearestBusy(true); setNearestMessage(''); setNearestSlot(null);
    try {
      const slot = await apiFetch<AvailableSlot | null>(`/api/availability/next?from=${todayDate}&durationMinutes=${form.durationMinutes}`, { signal: controller.signal });
      if (controller.signal.aborted) return;
      if (!slot) setNearestMessage('V následujících 60 dnech není pro tuto službu volný termín. Kontaktujte prosím ordinaci telefonicky.');
      else { setSelectedDate(getAppointmentDateKey(slot.startAt)); setNearestSlot(slot); setNearestMessage(`Nejbližší termín: ${formatDateTime(slot.startAt)}. Doplňte údaje a dokončete rezervaci.`); }
    } catch (error) { if (!controller.signal.aborted) setNearestMessage(error instanceof Error ? error.message : 'Nejbližší termín se nepodařilo najít.'); }
    finally { if (!controller.signal.aborted) setNearestBusy(false); }
  }

  const content = site.content;
  const selectedReasonNeedsCall = form.visitReason === 'Akutní problém';
  const articleItems = useMemo(() => site.articleSections.flatMap(section =>
    section.articles.map(article => ({ article, section }))
  ), [site]);
  const latestArticleItems = useMemo(() => [...articleItems]
    .sort((a, b) => (b.article.publishedAt ?? '').localeCompare(a.article.publishedAt ?? ''))
    .slice(0, 3), [articleItems]);
  const isArticlesIndex = /^\/clanky\/?$/.test(window.location.pathname);
  const articleSlug = getArticleSlug(window.location.pathname);
  const detailArticleItem = articleSlug
    ? articleItems.find(item => articlePath(item.article) === articleSlug)
    : undefined;

  async function submitAppointment(event: React.FormEvent) {
    event.preventDefault();
    if (busy || slotsLoading || !appointmentTypes.length) return;
    setReservationId(null);
    const validation = validateAppointmentRequest(form, appointmentTypes);
    setFormErrors(validation.errors);
    if (!validation.ok) {
      setStatus('Zkontrolujte prosím zvýrazněná pole.');
      return;
    }
    setBusy(true);
    setStatus('Rezervuji termín…');
    try {
      const response = await apiFetch<{ id: number }>('/api/appointments', { method: 'POST', body: JSON.stringify(validation.value) });
      setReservationId(response.id);
      setStatus('Potvrďte rezervaci pomocí odkazu v e-mailu do 15 minut.');
      setFormErrors({});
      setNearestSlot(null); setNearestMessage('');
      setForm(createDefaultAppointmentRequest(appointmentTypes.find(type => type.title === form.visitReason) ?? appointmentTypes[0]));
      setSlotsReload(value => value + 1);
    } catch (error) {
      setStatus(error instanceof Error ? error.message : 'Rezervace se nepodařila. Termín už může být obsazený, zkuste prosím jiný čas nebo zavolejte do ordinace.');
    } finally { setBusy(false); }
  }

  function selectSlot(slot: AvailableSlot) {
    if (!slot.isAvailable) return;
    setFormErrors(current => ({ ...current, requestedStartAt: undefined }));
    setForm({ ...form, requestedStartAt: slot.startAt, requestedEndAt: slot.endAt });
  }

  function selectAppointmentType(title: string) {
    setNearestSlot(null);
    setNearestMessage('');
    const type = appointmentTypes.find(item => item.title === title) ?? appointmentTypes[0];
    setFormErrors(current => ({ ...current, visitReason: undefined, requestedStartAt: undefined }));
    setForm(current => ({ ...current, visitReason: type.title, durationMinutes: type.durationMinutes, requestedStartAt: null, requestedEndAt: null }));
  }

  function updateFormField<K extends keyof AppointmentRequest>(field: K, value: AppointmentRequest[K]) {
    setFormErrors(current => ({ ...current, [field]: undefined }));
    setForm(current => ({ ...current, [field]: value }));
  }

  const callHref = phoneHref(content.phone);

  if (articleSlug || isArticlesIndex) {
    const archiveTitle = `Články | ${content?.clinicName ?? 'Veterinární ordinace'}`;
    const archiveDescription = 'Praktické rady, prevence a novinky z veterinární ordinace na jednom místě.';
    const detailTitle = siteLoading || siteLoadFailed ? `Článek | ${content?.clinicName ?? 'Veterinární ordinace'}` : detailArticleItem ? `${detailArticleItem.article.title} | ${content?.clinicName ?? 'Veterinární ordinace'}` : `Článek nenalezen | ${content?.clinicName ?? 'Veterinární ordinace'}`;
    const detailDescription = detailArticleItem ? detailArticleItem.article.summary || firstParagraph(detailArticleItem.article.body) : 'Požadovaný článek nebyl nalezen.';

    return (
      <>
        <SeoMeta
          title={isArticlesIndex ? archiveTitle : detailTitle}
          description={isArticlesIndex ? archiveDescription : detailDescription}
          pathname={isArticlesIndex ? '/clanky' : window.location.pathname}
          type={detailArticleItem ? 'article' : 'website'}
        />
        <StructuredData content={content} article={detailArticleItem?.article} section={detailArticleItem?.section} />
        <a className="skipLink" href="#main-content">Přeskočit na obsah</a>
        <PublicNav clinicName={content?.clinicName} hasArticles={articleItems.length > 0} />
        <main id="main-content" tabIndex={-1}>
          {loadError && <p className="status" role="alert">{loadError}</p>}
          {siteLoading
            ? <section className="section articleDetail"><h1>{isArticlesIndex ? 'Články' : 'Článek'}</h1><p role="status">Načítám články…</p></section>
            : siteLoadFailed
              ? <section className="section articleDetail"><h1>Články se nepodařilo načíst</h1><p>Zkuste stránku obnovit. K objednání můžete využít <a href="/#kontakt">rezervační formulář</a> nebo zavolat do ordinace.</p></section>
            : isArticlesIndex
            ? <ArticlesIndexPage articleItems={articleItems} />
            : detailArticleItem
              ? <ArticleDetailPage article={detailArticleItem.article} section={detailArticleItem.section} />
              : <section className="section articleDetail"><a className="articleBackLink" href="/clanky">← Zpět na články</a><h1>Článek nebyl nalezen</h1><p>Možná byl článek skrytý, smazaný nebo má změněnou URL adresu.</p></section>}
        </main>
        <PublicFooter content={content} hasArticles={articleItems.length > 0} />
        <CookieConsent />
        <MobileBookingActions phone={content.phone} />
      </>
    );
  }

  return (
    <>
      <SeoMeta
        title={`${content?.clinicName ?? 'Veterinární ordinace'} | Online rezervace a ordinační hodiny`}
        description={content?.heroText || 'Veterinární ordinace pro psy, kočky a drobná zvířata s online rezervací termínu.'}
        pathname="/"
      />
      <StructuredData content={content} />
      <a className="skipLink" href="#main-content">Přeskočit na obsah</a>
      <PublicNav clinicName={content.clinicName} hasArticles={articleItems.length > 0} />
      <main id="main-content" tabIndex={-1}>
          {loadError && <p className="status" role="alert">{loadError}</p>}
        <section className="hero">
          <img
            className="heroBg"
            src={heroBackground}
            alt=""
            aria-hidden="true"
            width="1500"
            height="1000"
            fetchPriority="high"
            decoding="async"
          />
          <div>
            <p className="eyebrow">Ukázková veterinární ordinace</p>
            <h1>{content.heroTitle}</h1>
            <p>{content.heroText}</p>
            <div className="actions"><a className="button" href="#kontakt">Objednat termín</a>{callHref && <a className="button secondary" href={callHref}>{content.phone}</a>}</div>
          </div>
        </section>

        <section id="sluzby" className="section">
          <h2>Služby ordinace</h2>
          <div className="servicesLayout">
            <figure className="servicePhoto">
              <img
                src={vetExamCat}
                srcSet={`${vetExamCatSmall} 640w, ${vetExamCatMedium} 960w, ${vetExamCat} 1355w`}
                sizes="(max-width: 980px) 100vw, 38vw"
                alt="Veterinární vyšetření kočky v ordinaci"
                width="1355"
                height="1200"
                loading="lazy"
                decoding="async"
              />
              <figcaption>Prevence, diagnostika a kontroly na jednom místě.</figcaption>
            </figure>
            <div className="cards">{site.services.map(service => <article className="card" key={service.id}><span>{service.icon}</span><h3>{service.title}</h3><p>{service.description}</p></article>)}</div>
          </div>
        </section>

        {latestArticleItems.length > 0 && (
          <section className="section articleSections articlePreview">
            <div className="blogHeader articlePreviewHeader">
              <div>
                <p className="eyebrow">Poradna</p>
                <h2>Nejnovější články</h2>
                <p>Aktuální rady a praktické informace z ordinace. Celý archiv článků je na samostatné stránce.</p>
              </div>
              <a className="button secondary" href="/clanky">Všechny články</a>
            </div>
            <ArticleCards items={latestArticleItems} />
          </section>
        )}

        {schedule && (
          <section id="provoz" className="section hoursOverview">
            <div>
              <p className="eyebrow">{schedule.activeEvent ? 'Aktuální provoz' : 'Standardní provoz'}</p>
              <h2>{schedule.activeEvent?.title ?? 'Otevírací doba'}</h2>
              {schedule.activeEvent?.note && <p>{schedule.activeEvent.note}</p>}
            </div>
            <HoursTable rules={mergeWorkingHours(schedule.activeEvent?.rules ?? schedule.standardRules)} />
            {!schedule.activeEvent && schedule.upcomingEvents.length > 0 && (
              <div className="upcomingEvents">
                {schedule.upcomingEvents.map(event => (
                  <article className="noticeCard" key={event.id}>
                    <div><strong>{event.title}</strong><span>{formatDate(event.startsOn)} - {formatDate(event.endsOn)}</span></div>
                    {event.note && <p>{event.note}</p>}
                    <HoursTable rules={mergeWorkingHours(event.rules)} compact />
                  </article>
                ))}
              </div>
            )}
          </section>
        )}

        <section id="akutne" className="section urgentSection">
          <div>
            <p className="eyebrow">Akutní stav</p>
            <h2>Kdy raději volat</h2>
            <p>Formulář je vhodný pro plánované návštěvy. U náhlých potíží je lepší krátký telefonát.</p>
            {callHref ? <a className="button" href={callHref}>Volat {content.phone}</a> : <p>V akutní situaci kontaktujte svého veterináře nebo veterinární pohotovost.</p>}
          </div>
          <div className="urgentGrid">
            {urgentTopics.map(topic => <article className="miniCard" key={topic.title}><strong>{topic.title}</strong><span>{topic.detail}</span></article>)}
          </div>
        </section>

        <section id="kontakt" className="section reservationSection">
          <form className="panel reservationPanel" onSubmit={submitAppointment}>
            {bookingLoadError && <div role="alert" className="status">
              <p>{bookingLoadError}</p>
              {import.meta.env.DEV && <p>Lokální API musí běžet spolu s webem. Spusťte <code>npm run dev</code> z kořene projektu.</p>}
              <button type="button" className="button secondary" onClick={() => setBookingReload(value => value + 1)}>Znovu načíst rezervace</button>
            </div>}
            {!appointmentTypes.length && !bookingLoadError && <p role="status">Načítám online rezervace…</p>}
            <fieldset disabled={busy || nearestBusy || !appointmentTypes.length} style={{ border: 0, padding: 0, minWidth: 0 }}>
            <div className="reservationHeader">
              <div><p className="eyebrow">Online termín</p><h2>Rezervace návštěvy</h2></div>
              {form.requestedStartAt && <span>{formatDateTime(form.requestedStartAt)}</span>}
            </div>
            {reservationId && <div className="reservationSuccess"><strong>Rezervace #{reservationId} čeká na potvrzení.</strong><span>Otevřete odkaz v e-mailu a potvrďte rezervaci do 15 minut. Potom se nepotvrzený termín uvolní.</span></div>}
            <fieldset className="reservationStep">
              <legend><span>1</span> Vyberte den</legend>
              <label className="field"><span>Den návštěvy</span><input type="date" min={todayDate} max={lastDate} value={selectedDate} onChange={e => { setNearestSlot(null); setNearestMessage(''); setSelectedDate(e.target.value); }} /></label>
              <button type="button" className="button secondary" onClick={findNearest}>{nearestBusy ? 'Hledám volný termín…' : 'Najít nejbližší termín'}</button>
              {nearestMessage && <p role="status" className="status">{nearestMessage}</p>}
            </fieldset>
            <fieldset className="reservationStep">
              <legend><span>2</span> Důvod návštěvy</legend>
              {formErrors.visitReason && <p className="fieldError" role="alert">{formErrors.visitReason}</p>}
              <div className="reservationReasonGrid">
                {appointmentTypes.map(reason => (
                  <button
                    className={form.visitReason === reason.title ? 'reasonOption selected' : 'reasonOption'}
                    type="button"
                    aria-pressed={form.visitReason === reason.title}
                    onClick={() => selectAppointmentType(reason.title)}
                    key={reason.title}
                  >
                    <strong>{reason.title}</strong>
                    <span>{reason.durationMinutes} min</span>
                  </button>
                ))}
              </div>
              {selectedReasonNeedsCall && <p className="warningBox">U akutního problému raději nejdřív zavolejte. Formulář použijte jen pokud stav nevyžaduje okamžitou reakci.</p>}
            </fieldset>
            <fieldset className="reservationStep">
              <legend><span>3</span> Vyberte čas</legend>
              <p className="reservationStepHint">Časy jsou počítané podle služby „{form.visitReason}“ na {form.durationMinutes} minut.</p>
              {formErrors.requestedStartAt && <p className="fieldError" role="alert">{formErrors.requestedStartAt}</p>}
              <div className="slotGroups" aria-label="Dostupné termíny">
                {groupSlotsByPart(slots).length === 0 ? <p className="emptySlots">{slotsLoading ? "Načítám termíny…" : "Pro vybraný den nejsou volné termíny. Vyberte jiný den návštěvy nebo zavolejte do ordinace."}</p> : groupSlotsByPart(slots).map(group => (
                  <div className="slotGroup" key={group.label}>
                    <strong>{group.label}</strong>
                    <div className="slotGrid">
                      {group.slots.map(slot => (
                        <button
                          type="button"
                          className={form.requestedStartAt === slot.startAt ? 'slot selected' : 'slot'}
                          key={slot.startAt}
                          aria-pressed={form.requestedStartAt === slot.startAt}
                          aria-label={`Rezervovat čas ${formatTime(slot.startAt)} až ${formatTime(slot.endAt)}`}
                          onClick={() => selectSlot(slot)}
                        >
                          {formatTime(slot.startAt)}
                        </button>
                      ))}
                    </div>
                  </div>
                ))}
              </div>
            </fieldset>
            <fieldset className="reservationStep">
              <legend><span>4</span> Údaje k návštěvě</legend>
              <div className="reservationFields">
                <label className="field">
                  <span>Vaše jméno</span>
                  <input required minLength={2} maxLength={80} autoComplete="name" placeholder="Jan Novák" value={form.name} aria-invalid={Boolean(formErrors.name)} onChange={e => updateFormField('name', e.target.value)} />
                  {formErrors.name && <small className="fieldError" role="alert">{formErrors.name}</small>}
                </label>
                <label className="field">
                  <span>Telefon</span>
                  <input required maxLength={24} inputMode="tel" autoComplete="tel" placeholder="+420 777 123 456" value={form.phone} aria-invalid={Boolean(formErrors.phone)} onChange={e => updateFormField('phone', e.target.value)} />
                  {formErrors.phone && <small className="fieldError" role="alert">{formErrors.phone}</small>}
                </label>
                <label className="field">
                  <span>E-mail</span>
                  <input type="email" required maxLength={120} autoComplete="email" placeholder="jan@example.cz" value={form.email} aria-invalid={Boolean(formErrors.email)} onChange={e => updateFormField('email', e.target.value)} />
                  {formErrors.email && <small className="fieldError" role="alert">{formErrors.email}</small>}
                </label>
                <label className="field">
                  <span>Jméno mazlíčka</span>
                  <input required maxLength={60} placeholder="Bady" value={form.petName} aria-invalid={Boolean(formErrors.petName)} onChange={e => updateFormField('petName', e.target.value)} />
                  {formErrors.petName && <small className="fieldError" role="alert">{formErrors.petName}</small>}
                </label>
                <label className="field">
                  <span>Druh zvířete</span>
                  <select value={form.petType} aria-invalid={Boolean(formErrors.petType)} onChange={e => updateFormField('petType', e.target.value)}><option>Pes</option><option>Kočka</option><option>Drobné zvíře</option><option>Jiné</option></select>
                  {formErrors.petType && <small className="fieldError" role="alert">{formErrors.petType}</small>}
                </label>
                <label className="field wide">
                  <span>Zpráva</span>
                  <textarea maxLength={600} placeholder="Popište důvod návštěvy" value={form.message} aria-invalid={Boolean(formErrors.message)} onChange={e => updateFormField('message', e.target.value)} />
                  {formErrors.message && <small className="fieldError" role="alert">{formErrors.message}</small>}
                </label>
              </div>
            </fieldset>
            <div className="reservationSubmit">
              <button className="button" disabled={busy || slotsLoading || !form.requestedStartAt}>Rezervovat termín</button>
              <span>{form.requestedStartAt ? 'Po odeslání potvrďte termín e-mailem do 15 minut.' : 'Nejdřív vyberte volný čas.'}</span>
            </div>
            </fieldset>
            {status && <p className="status" role="status" aria-live="polite">{status}</p>}
          </form>
        </section>

        <section className="section prepSection">
          <div>
            <p className="eyebrow">Před návštěvou</p>
            <h2>Co mít připravené</h2>
          </div>
          <div className="prepGrid">
            {visitPrepItems.map(item => <article className="miniCard" key={item.title}><strong>{item.title}</strong><span>{item.detail}</span></article>)}
          </div>
        </section>
        <section className="section bookingFaq" aria-labelledby="booking-faq-title">
          <p className="eyebrow">Před objednáním</p><h2 id="booking-faq-title">Jak návštěvu naplánovat</h2>
          <details><summary>Jak funguje online rezervace?</summary><p>Vyberte službu, den a čas nebo použijte hledání nejbližšího termínu. Po vyplnění údajů otevřete e-mail a rezervaci do 15 minut potvrďte. Bez potvrzení se čas znovu uvolní.</p></details>
          <details><summary>Co když potřebuji termín změnit?</summary><p>Ve stejném e-mailu najdete soukromý odkaz pro správu rezervace. Před začátkem návštěvy můžete vybrat jiný volný čas nebo rezervaci zrušit. Odkaz nikomu nepřeposílejte.</p></details>
          <details><summary>Co dělat, když nenajdu volný termín?</summary><p>Zkuste jiný den.{callHref && <> Můžete také zavolat na <a href={callHref}>{content.phone}</a>.</>}</p></details>
        </section>
      </main>
      <PublicFooter content={content} hasArticles={articleItems.length > 0} />
      <CookieConsent />
      <MobileBookingActions phone={content.phone} />
    </>
  );
}
