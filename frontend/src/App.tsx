import { useEffect, useState } from 'react';
import { HttpError, onSessionChange, refresh } from './api';
import type { Auth } from './types';
import AuthPage from './AuthPage';
import SchedulePage from './SchedulePage';

export default function App() {
  const [auth, setAuth] = useState<Auth | null>(null);
  const [ready, setReady] = useState(false);
  const [startupError, setStartupError] = useState('');
  const [link, setLink] = useState(() => {
    const params = new URLSearchParams(window.location.hash.slice(1));
    return { reset: params.get('reset') ?? '', invite: params.get('invite') ?? '' };
  });
  useEffect(() => {
    onSessionChange(setAuth);
    if (window.location.hash) window.history.replaceState(null, '', window.location.pathname);
    if (link.reset || link.invite) { setReady(true); return; }
    let active = true;
    refresh().catch(e => { if (active && (!(e instanceof HttpError) || e.status !== 401)) setStartupError('Não foi possível conectar ao servidor. Tente novamente.'); })
      .finally(() => { if (active) setReady(true); });
    return () => { active = false; };
  }, [link.reset, link.invite]);
  if (!ready) return <main className="auth-shell"><div className="card" role="status">Carregando sua escala…</div></main>;
  if (link.reset || !auth) return <AuthPage resetToken={link.reset} inviteToken={link.invite} startupError={startupError} onComplete={() => { setLink({ reset: '', invite: '' }); setStartupError(''); }} />;
  return <SchedulePage key={auth.user.id} user={auth.user} />;
}
