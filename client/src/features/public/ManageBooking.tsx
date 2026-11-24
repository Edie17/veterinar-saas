import { useEffect, useRef, useState } from 'react';
import { apiFetch, type AvailableSlot } from '../../api';
import { formatDateTime, formatTime, toDateInput } from '../../shared/date';
import { downloadCalendar, visitCalendar } from '../../shared/calendar';
import type { SiteResponse } from '../../types';

type ManagedVisit = { id: number; petName: string; visitReason: string; durationMinutes: number; startAt: string; endAt: string; status: string };
export function ManageBooking({ token }: { token: string }) {
  const [visit, setVisit] = useState<ManagedVisit | null>(null);
  const [site, setSite] = useState<SiteResponse | null>(null);
  const [message, setMessage] = useState(token ? 'Načítám rezervaci…' : 'Otevřete soukromý odkaz pro správu z potvrzovacího e-mailu.');
  const [busy, setBusy] = useState(false);
  const [cancelled, setCancelled] = useState(false);
  const [cancelPrompt, setCancelPrompt] = useState(false);
  const [date, setDate] = useState(() => toDateInput(new Date()));
  const [slots, setSlots] = useState<AvailableSlot[]>([]);
  const [loadingSlots, setLoadingSlots] = useState(false);
  const [selected, setSelected] = useState('');
  const [slotError, setSlotError] = useState('');
  const submitting = useRef(false);
  const today = toDateInput(new Date());
  const maxDate = toDateInput(new Date(Date.now() + 60 * 86400000));
  useEffect(() => {
    document.title = 'Správa rezervace | Veterinární ordinace';
    if (!token) return;
    const controller = new AbortController();
    apiFetch<ManagedVisit>('/api/appointments/manage', { method: 'POST', body: JSON.stringify({ token }), signal: controller.signal })
      .then(data => { setVisit(data); setDate(toDateInput(new Date(data.startAt))); setMessage(''); })
      .catch(error => { if (!controller.signal.aborted) setMessage(error instanceof Error ? error.message : 'Rezervaci se nepodařilo načíst.'); });
    apiFetch<SiteResponse>('/api/site', { signal: controller.signal }).then(setSite).catch(() => {});
    return () => controller.abort();
  }, [token]);
  useEffect(() => {
    if (!visit || cancelled || !date) return;
    const controller = new AbortController();
    setSlots([]); setSelected(''); setSlotError(''); setLoadingSlots(true);
    apiFetch<AvailableSlot[]>(`/api/availability?date=${date}&durationMinutes=${visit.durationMinutes}`, { signal: controller.signal })
      .then(data => { if (!controller.signal.aborted) setSlots(data.filter(slot => slot.isAvailable)); })
      .catch(error => { if (!controller.signal.aborted) setSlotError(error instanceof Error ? error.message : 'Termíny se nepodařilo načíst.'); })
      .finally(() => { if (!controller.signal.aborted) setLoadingSlots(false); });
    return () => controller.abort();
  }, [date, visit, cancelled]);
  async function change(action: 'cancel' | 'move') {
    if (submitting.current) return;
    submitting.current = true; setBusy(true); setMessage('');
    try {
      const result = await apiFetch<ManagedVisit | undefined>(`/api/appointments/manage/${action}`, { method: 'POST', body: JSON.stringify({ token, ...(action === 'move' ? { startAt: selected } : {}) }) });
      if (action === 'cancel') { setCancelled(true); setMessage('Rezervace byla zrušena. Termín jsme uvolnili pro další pacienty.'); }
      else { setVisit(result!); setMessage('Termín byl přesunut. Aktualizujte také případnou událost ve svém kalendáři.'); }
      setCancelPrompt(false);
    } catch (error) { setMessage(error instanceof Error ? error.message : 'Změnu se nepodařilo uložit.'); }
    finally { submitting.current = false; setBusy(false); }
  }
  return <main className="section bookingManagement">
    <a href="/">← Zpět na web ordinace</a><p className="eyebrow">Vaše návštěva</p><h1>Správa rezervace</h1>
    {message && <p className="status" role="status">{message}</p>}
    {visit && !cancelled && <>
      <section className="panel managementSummary" aria-label="Aktuální rezervace">
        <h2>{visit.petName}</h2><p><strong>{formatDateTime(visit.startAt)}</strong></p>
        <p>{visit.visitReason} · {visit.durationMinutes} minut · Potvrzeno</p>
        <button className="button secondary" onClick={() => downloadCalendar(visitCalendar(visit, site?.content.clinicName ?? 'Veterinární ordinace', site?.content.address ?? ''))}>Přidat do kalendáře</button>
        <p>Kalendář nabídne připomínku hodinu před návštěvou. Její zobrazení závisí na nastavení vaší kalendářové aplikace.</p>
      </section>
      <section className="panel" aria-labelledby="move-booking-title">
        <h2 id="move-booking-title">Potřebujete jiný termín?</h2><p>Původní termín platí, dokud přesun úspěšně nepotvrdíte.</p>
        <fieldset disabled={busy} className="managementFields">
          <label className="field"><span>Nový den návštěvy</span><input type="date" min={today} max={maxDate} value={date} onChange={event => setDate(event.target.value)} /></label>
          {slotError && <p role="alert" className="error">{slotError}</p>}
          <div className="slotGrid" aria-label="Nové dostupné termíny">
            {slots.map(slot => <button type="button" className={selected === slot.startAt ? 'slot selected' : 'slot'} aria-pressed={selected === slot.startAt} key={slot.startAt} onClick={() => setSelected(slot.startAt)}>{formatTime(slot.startAt)}</button>)}
          </div>
          {!slots.length && !slotError && <p>{loadingSlots ? 'Načítám termíny…' : 'Pro tento den nejsou volné termíny. Vyberte jiný den.'}</p>}
          <button type="button" className="button" disabled={!selected || loadingSlots} onClick={() => change('move')}>{busy ? 'Ukládám…' : 'Potvrdit přesun termínu'}</button>
        </fieldset>
      </section>
      <section className="panel"><h2>Zrušení návštěvy</h2>
        {cancelPrompt ? <div><p>Opravdu chcete zrušit návštěvu {formatDateTime(visit.startAt)}? Uvolněný čas může rezervovat jiný klient.</p>
          <div className="managementActions"><button className="button secondary" disabled={busy} onClick={() => setCancelPrompt(false)}>Ponechat rezervaci</button><button className="button" disabled={busy} onClick={() => change('cancel')}>Ano, zrušit rezervaci</button></div></div>
          : <button className="button secondary" disabled={busy} onClick={() => setCancelPrompt(true)}>Zrušit rezervaci</button>}
      </section>
      <p>Soukromý odkaz z e-mailu si uchovejte a nikomu jej nepřeposílejte. Funguje do začátku návštěvy. Po obnovení této stránky jej otevřete z e-mailu znovu.</p>
    </>}
  </main>;
}
