import { handleHorizontalNav } from '../shared/navigation';
import { useRef, useState } from 'react';

export function PublicNav({ clinicName, hasArticles }: { clinicName?: string; hasArticles: boolean }) {
  const isArticlesPage = window.location.pathname.startsWith('/clanky');
  const [open, setOpen] = useState(false);
  const toggle = useRef<HTMLButtonElement>(null);

  return (
    <header className="topbar" role="banner" onKeyDown={event => {
      if (event.key === 'Escape' && open) { setOpen(false); toggle.current?.focus(); }
    }}>
      <a className="brand" href="/" aria-label={`Domů - ${clinicName ?? 'Veterinární ordinace'}`}><span aria-hidden="true">🐾</span><span className="brandName">{clinicName ?? 'Veterinární ordinace'}</span></a>
      <button ref={toggle} type="button" className="publicMenuToggle" aria-expanded={open} aria-controls="public-navigation" onClick={() => setOpen(value => !value)}>Menu<span aria-hidden="true">{open ? '×' : '☰'}</span></button>
      <nav id="public-navigation" className="publicNav" data-open={open} aria-label="Hlavní navigace" onKeyDown={handleHorizontalNav} onClick={event => { if ((event.target as HTMLElement).closest('a')) setOpen(false); }}>
        <a href="/#provoz">Provoz</a>
        <a href="/#sluzby">Služby</a>
        {hasArticles && <a href="/clanky" aria-current={isArticlesPage ? 'page' : undefined}>Články</a>}
        <a href="/#footer-title">Kontakt</a>
        <a className="publicNavBooking" href="/#kontakt">Objednat se</a>
      </nav>
    </header>
  );
}
