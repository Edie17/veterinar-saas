import type { ReactNode } from 'react';
import type { SiteContent } from '../api';
import { defaultPublicSite } from '../shared/constants';
import { phoneHref } from '../shared/navigation';

export function PublicFooter({ content, hasArticles }: { content?: SiteContent; hasArticles: boolean }) {
  const { clinicName, phone, email, address } = content ?? defaultPublicSite.content;

  const callHref = phoneHref(phone);

  return (
    <footer className="siteFooter" aria-labelledby="footer-title">
      <div className="footerBrand">
        <p className="eyebrow">Kontakt</p>
        <h2 id="footer-title">{formatFooterClinicName(clinicName)}</h2>
        <address>
          <span>{address}</span>
          {callHref ? <a href={callHref}>{phone}</a> : <span>{phone}</span>}
          {email.endsWith('.test') ? <span>{email}</span> : <a href={`mailto:${email}`}>{email}</a>}
        </address>
      </div>
      <nav className="footerNav" aria-label="Rychlé odkazy v patičce">
        <a href="/#provoz">Ordinační hodiny</a>
        <a href="/#sluzby">Služby</a>
        <a href="/#kontakt">Rezervace</a>
        {hasArticles && <a href="/clanky">Články</a>}
      </nav>
      <div className="footerMeta">
        <span>© {new Date().getFullYear()} {clinicName}</span>
      </div>
    </footer>
  );
}

function formatFooterClinicName(clinicName: string): ReactNode {
  const match = clinicName.match(/^(Veterinární ordinace)\s+(.*)$/i);
  if (!match) return clinicName;
  return (
    <>
      <span>{match[1]}</span>
      <span>{match[2]}</span>
    </>
  );
}
