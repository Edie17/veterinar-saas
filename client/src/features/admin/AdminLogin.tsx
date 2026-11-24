import type { FormEvent } from 'react';

type AdminLoginProps = {
  email: string;
  password: string;
  message: string;
  busy: boolean;
  onEmailChange: (value: string) => void;
  onPasswordChange: (value: string) => void;
  onSubmit: (event: FormEvent) => void;
};

export function AdminLogin({ email, password, message, busy, onEmailChange, onPasswordChange, onSubmit }: AdminLoginProps) {
  return (
    <main className="cmsLoginShell">
      <section className="cmsLoginHero">
        <span className="cmsLogoMark">🐾</span>
        <p className="eyebrow">Veterinární CMS</p>
        <h1>Profesionální správa veterinární ordinace.</h1>
        <p>Obsah webu, ordinační hodiny, objednávky a provozní nastavení v jednom bezpečném administračním prostředí.</p>
      </section>
      <form className="cmsLoginCard" onSubmit={onSubmit}>
        <h2>Přihlášení do CMS</h2>
        <p>Přihlaste se účtem pro správu ordinace.</p>
        <label className="field"><span>E-mail</span><input type="email" autoComplete="username" required disabled={busy} value={email} onChange={event => onEmailChange(event.target.value)} /></label>
        <label className="field"><span>Heslo</span><input type="password" autoComplete="current-password" placeholder="Heslo" required disabled={busy} value={password} onChange={event => onPasswordChange(event.target.value)} /></label>
        <button className="button" disabled={busy}>{busy ? 'Přihlašuji…' : 'Přihlásit do správy'}</button>
        {message && <p role="status" aria-live="polite" className="error">{message}</p>}
      </form>
    </main>
  );
}
