import type { ReactNode } from 'react';
import type { SiteContent } from '../../api';
import { cmsNav } from '../../shared/constants';
import type { CmsSection, Me } from '../../types';

type AdminLayoutProps = {
  me: Me;
  site: SiteContent | null;
  activeSection: CmsSection;
  message: string;
  children: ReactNode;
  onSectionChange: (section: CmsSection) => void;
};

export function AdminLayout({ me, site, activeSection, message, children, onSectionChange }: AdminLayoutProps) {
  return (
    <div className="cmsShell">
      <aside className="cmsSidebar" aria-label="Hlavní CMS navigace">
        <div className="cmsBrandBlock">
          <span className="cmsLogoMark">🐾</span>
          <div><strong>Veterinární CMS</strong><small>{site?.clinicName ?? 'Na Tlapce'}</small></div>
        </div>
        <nav className="cmsNav">
          {cmsNav.map(item => (
            <button
              key={item.id}
              type="button"
              className={activeSection === item.id ? 'cmsNavItem active' : 'cmsNavItem'}
              onClick={() => onSectionChange(item.id)}
              aria-current={activeSection === item.id ? 'page' : undefined}
            >
              <span>{item.icon}</span>
              <div><strong>{item.label}</strong><small>{item.hint}</small></div>
            </button>
          ))}
        </nav>
        <div className="cmsSidebarFooter">
          <small>Přihlášen jako</small>
          <strong>{me.email}</strong>
          <span>{me.role}</span>
        </div>
      </aside>

      <main className="cmsMain">
        <header className="cmsTopbar">
          <div>
            <p className="eyebrow">Administrace kliniky</p>
            <h1>{cmsNav.find(item => item.id === activeSection)?.label}</h1>
          </div>
          <div className="cmsTopbarActions">
            <a className="button secondary" href="/" target="_blank" rel="noreferrer">Náhled webu</a>
            <button className="button" type="button" onClick={() => onSectionChange('content')}>Upravit web</button>
          </div>
        </header>

        {message && <p className="status cmsStatus" role="status" aria-live="polite">{message}</p>}
        {children}
      </main>
    </div>
  );
}
