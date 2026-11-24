import { useEffect, useState } from 'react';

export function DemoBanner() {
  const [demo, setDemo] = useState(false);
  useEffect(() => {
    if (!import.meta.env.DEV) return;
    const controller = new AbortController();
    fetch('/api/dev/status', { signal: controller.signal })
      .then(response => response.ok ? response.json() : null)
      .then(data => setDemo(data?.demo === true)).catch(() => {});
    return () => controller.abort();
  }, []);
  if (!demo) return null;
  return <aside style={{ padding: '12px 24px', background: '#fff3cd', color: '#332701' }} aria-label="Lokální demo">
    <strong>Ukázková aplikace</strong> · Fiktivní ordinace. Používejte pouze testovací údaje.{' '}
    <a href="/api/dev/mailbox/view" target="_blank" rel="noreferrer">Testovací schránka</a> · <a href="/sprava">Administrace</a>
  </aside>;
}
