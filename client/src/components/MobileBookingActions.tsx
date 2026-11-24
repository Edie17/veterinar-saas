import { useEffect, useState } from 'react';
import { phoneHref } from '../shared/navigation';

export function MobileBookingActions({ phone }: { phone: string }) {
  const [formVisible, setFormVisible] = useState(false);
  useEffect(() => {
    const form = document.getElementById('kontakt');
    if (!form || typeof IntersectionObserver === 'undefined') return;
    const observer = new IntersectionObserver(([entry]) => setFormVisible(entry.isIntersecting), { threshold: 0 });
    observer.observe(form); return () => observer.disconnect();
  }, []);
  const callHref = phoneHref(phone);
  return <nav className="mobileBookingActions" aria-label="Rychlé objednání" hidden={formVisible}>
    {callHref && <a className="button secondary" href={callHref}>Zavolat</a>}
    <a className="button" href="/#kontakt">Objednat</a>
  </nav>;
}
