import { useEffect, useState } from 'react';
import { apiFetch } from '../../api';

export function ConfirmBooking({ token }: { token: string }) {
  const [busy, setBusy] = useState(false);
  const [done, setDone] = useState(false);
  const [message, setMessage] = useState('Odkaz platí 15 minut od vytvoření rezervace.');
  useEffect(() => { document.title = 'Potvrzení rezervace | Veterinární ordinace'; }, []);
  async function confirm() {
    if (busy || done) return;
    setBusy(true);
    try {
      await apiFetch('/api/appointments/confirm', { method: 'POST', body: JSON.stringify({ token }) });
      setDone(true);
      setMessage('Rezervace je potvrzena. Těšíme se na vaši návštěvu.');
    } catch (error) { setMessage(error instanceof Error ? error.message : 'Potvrzení se nepodařilo.'); }
    finally { setBusy(false); }
  }
  return <main className="section"><h1>Potvrzení rezervace</h1><p role="status">{message}</p>
    {!done && <button className="button" disabled={busy || !token} onClick={confirm}>{busy ? 'Potvrzuji…' : 'Potvrdit rezervaci'}</button>}
    {done && <p>Potřebujete termín přesunout, zrušit nebo přidat do kalendáře? Použijte odkaz „Správa rezervace“ ve stejném e-mailu.</p>}
    <p><a href="/">Zpět na web ordinace</a></p></main>;
}
