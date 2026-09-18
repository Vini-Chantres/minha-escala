import { useState, type FormEvent } from 'react';
import { api, setSession } from './api';
import type { Auth } from './types';

interface Props { resetToken: string; inviteToken: string; startupError: string; onComplete: () => void }
export default function AuthPage({ resetToken, inviteToken, startupError, onComplete }: Props) {
  const [mode, setMode] = useState<'login' | 'register' | 'forgot' | 'reset'>(resetToken ? 'reset' : inviteToken ? 'register' : 'login');
  const [name, setName] = useState(''); const [email, setEmail] = useState('');
  const [password, setPassword] = useState(''); const [confirm, setConfirm] = useState('');
  const [busy, setBusy] = useState(false); const [error, setError] = useState(''); const [message, setMessage] = useState('');
  const labels = { login: 'Bem-vinda à sua escala', register: inviteToken ? 'Aceitar convite' : 'Crie sua conta', forgot: 'Recuperar senha', reset: 'Escolha uma nova senha' };
  function change(next: typeof mode) { setMode(next); setError(''); setMessage(''); setPassword(''); setConfirm(''); }
  async function submit(event: FormEvent) {
    event.preventDefault(); setError(''); setMessage('');
    if ((mode === 'register' || mode === 'reset') && password !== confirm) { setError('As senhas precisam ser iguais.'); return; }
    setBusy(true);
    try {
      if (mode === 'forgot') {
        const result = await api<{ message: string }>('/auth/forgot-password', 'POST', { email }); setMessage(result.message);
      } else if (mode === 'reset') {
        await api('/auth/reset-password', 'POST', { token: resetToken, password }); setSession(null);
        onComplete(); change('login'); setMessage('Senha alterada. Entre com sua nova senha.');
      } else {
        const auth = await api<Auth>(`/auth/${mode}`, 'POST', { email, password, ...(mode === 'register' ? { name, invitationToken: inviteToken || null } : {}) });
        setSession(auth); onComplete();
      }
    } catch (e) { setError(e instanceof Error ? e.message : 'Não foi possível concluir.'); }
    finally { setBusy(false); }
  }
  return <main className="auth-shell">
    <div className="auth-intro"><span className="flower" aria-hidden="true">🌷</span><h1>Minha Escala</h1><p>Sua rotina de trabalho,<br />organizada com carinho.</p><div className="auth-detail">Um lugar para seus plantões.<br />E espaço para seu descanso.</div></div>
    <section className="card auth-card" aria-labelledby="auth-title">
      <p className="eyebrow">SUA ROTINA, MAIS LEVE</p><h2 id="auth-title">{labels[mode]}</h2>
      <p className="muted">{mode === 'login' ? 'Entre para acessar suas folgas e plantões.' : mode === 'register' ? 'Seus registros ficam salvos com segurança na sua conta.' : 'Vamos ajudar você a voltar à sua agenda.'}</p>
      {(error || startupError) && <p className="alert error" role="alert">{error || startupError}</p>}
      {message && <p className="alert success" role="status">{message}</p>}
      <form onSubmit={submit}>
        {mode === 'register' && <><label htmlFor="name">Seu nome</label><input id="name" autoComplete="name" required maxLength={100} value={name} onChange={e => setName(e.target.value)} /></>}
        {mode !== 'reset' && <><label htmlFor="email">E-mail</label><input id="email" type="email" autoComplete="email" required maxLength={254} value={email} onChange={e => setEmail(e.target.value)} /></>}
        {mode !== 'forgot' && <><label htmlFor="password">Senha{mode !== 'login' && ' (mínimo de 12 caracteres)'}</label><input id="password" type="password" autoComplete={mode === 'login' ? 'current-password' : 'new-password'} required minLength={mode === 'login' ? 1 : 12} maxLength={128} value={password} onChange={e => setPassword(e.target.value)} /></>}
        {(mode === 'register' || mode === 'reset') && <><label htmlFor="confirm">Confirme a senha</label><input id="confirm" type="password" autoComplete="new-password" required minLength={12} maxLength={128} value={confirm} onChange={e => setConfirm(e.target.value)} /></>}
        <button className="primary" disabled={busy}>{busy ? 'Aguarde…' : { login: 'Entrar na minha escala', register: 'Criar conta', forgot: 'Enviar link de recuperação', reset: 'Salvar nova senha' }[mode]}</button>
      </form>
      <div className="auth-actions">
        {mode === 'login' ? <><button className="text-button" disabled={busy} onClick={() => change('forgot')}>Esqueci minha senha</button><span>Ainda não tem uma conta? <button className="text-button" disabled={busy} onClick={() => change('register')}>Cadastre-se</button></span></> : <button className="text-button" disabled={busy} onClick={() => { change('login'); onComplete(); }}>Voltar para entrar</button>}
      </div>
    </section>
  </main>;
}
