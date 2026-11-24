import { AdminApp } from './features/admin/AdminApp';
import { PublicSite } from './features/public/PublicSite';
import { ConfirmBooking } from './features/public/ConfirmBooking';
import { DemoBanner } from './components/DemoBanner';
import { ManageBooking } from './features/public/ManageBooking';
import { useEffect, useState } from 'react';

function PrivateBookingPage({ confirmation }: { confirmation: boolean }) {
  const [link, setLink] = useState(() => ({ token: new URLSearchParams(window.location.hash.slice(1)).get('token') ?? '', revision: 0 }));
  useEffect(() => {
    const clearFragment = () => window.history.replaceState(null, '', window.location.pathname);
    const consumeLink = () => {
      const token = new URLSearchParams(window.location.hash.slice(1)).get('token');
      if (!token) return;
      clearFragment();
      setLink(previous => ({ token, revision: previous.revision + 1 }));
    };
    clearFragment();
    window.addEventListener('hashchange', consumeLink);
    return () => window.removeEventListener('hashchange', consumeLink);
  }, []);
  return <><DemoBanner />{confirmation
    ? <ConfirmBooking key={link.revision} token={link.token} />
    : <ManageBooking key={link.revision} token={link.token} />}</>;
}

export function App() {
  if (window.location.pathname === '/rezervace/sprava') return <PrivateBookingPage confirmation={false} />;
  if (window.location.pathname === '/rezervace/potvrzeni') return <PrivateBookingPage confirmation />;
  const isAdminRoute = window.location.pathname.startsWith('/sprava');
  return <><DemoBanner />{isAdminRoute ? <AdminApp /> : <PublicSite />}</>;
}
