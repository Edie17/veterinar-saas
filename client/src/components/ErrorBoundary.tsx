import { Component, type ReactNode } from 'react';

export class ErrorBoundary extends Component<{ children: ReactNode }, { failed: boolean }> {
  state = { failed: false };
  static getDerivedStateFromError() { return { failed: true }; }
  render() {
    if (this.state.failed) return <main className="section"><h1>Stránku se nepodařilo zobrazit</h1><p>Zkuste ji obnovit. Pokud chyba přetrvá, kontaktujte ordinaci.</p><button className="button" onClick={() => window.location.reload()}>Obnovit stránku</button></main>;
    return this.props.children;
  }
}
