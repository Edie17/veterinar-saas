import { useState } from 'react';

export function CookieConsent() {
  const [choice, setChoice] = useState(() => { try { return localStorage.getItem('vet_cookie_consent'); } catch { return null; } });

  function saveChoice(value: 'necessary' | 'accepted') {
    try { localStorage.setItem('vet_cookie_consent', value); } catch { /* The current page still remembers the choice. */ }
    setChoice(value);
  }

  if (choice) return null;

  return (
    <section className="cookieBanner" role="dialog" aria-labelledby="cookie-title" aria-describedby="cookie-description">
      <div>
        <h2 id="cookie-title">Cookies a soukromí</h2>
        <p id="cookie-description">Web používá nezbytné cookies a lokální úložiště pro správné fungování rezervací a zapamatování této volby. Analytiku spustíme až po souhlasu.</p>
      </div>
      <div className="cookieActions">
        <button className="button secondary" type="button" onClick={() => saveChoice('necessary')}>Pouze nezbytné</button>
        <button className="button" type="button" onClick={() => saveChoice('accepted')}>Souhlasím</button>
      </div>
    </section>
  );
}
