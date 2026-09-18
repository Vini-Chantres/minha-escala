import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { api } from './api';
import type { Audit, User } from './types';

const actionNames: Record<string, string> = { 'auth.login': 'Entrada', 'auth.logout': 'Saída', 'auth.forgot': 'Recuperação solicitada', 'auth.reset': 'Senha redefinida', 'user.register': 'Conta criada', 'user.invite': 'Convite criado', 'user.activate': 'Membro ativado', 'user.deactivate': 'Membro desativado', 'entry.create': 'Registro criado', 'entry.update': 'Registro editado', 'entry.delete': 'Registro excluído', 'entry.import': 'Escala importada' };
export default function Organization() {
  const [users, setUsers] = useState<User[]>([]); const [audit, setAudit] = useState<Audit | null>(null);
  const [page, setPage] = useState(1); const [email, setEmail] = useState('');
  const [loading, setLoading] = useState(true); const [busy, setBusy] = useState(false); const [error, setError] = useState(''); const [message, setMessage] = useState('');
  const load = useCallback(async () => {
    setLoading(true); setError('');
    try { const [u, a] = await Promise.all([api<User[]>('/organization/users'), api<Audit>(`/organization/audit?page=${page}`)]); setUsers(u); setAudit(a); }
    catch (e) { setError(e instanceof Error ? e.message : 'Não foi possível carregar.'); }
    finally { setLoading(false); }
  }, [page]);
  useEffect(() => { void load(); }, [load]);
  async function invite(e: FormEvent) {
    e.preventDefault(); setBusy(true); setError(''); setMessage('');
    try { const result = await api<{ message: string }>('/organization/invitations', 'POST', { email }); setMessage(result.message); setEmail(''); await load(); }
    catch (e) { setError(e instanceof Error ? e.message : 'Não foi possível convidar.'); }
    finally { setBusy(false); }
  }
  async function toggle(user: User) {
    setBusy(true); setError('');
    try { await api(`/organization/users/${user.id}/status`, 'PATCH', { isActive: !user.isActive }); await load(); }
    catch (e) { setError(e instanceof Error ? e.message : 'Não foi possível alterar.'); }
    finally { setBusy(false); }
  }
  return <div className="side-cards">
    {error && <p className="alert error" role="alert">{error} <button className="text-button" onClick={load}>Tentar novamente</button></p>}{message && <p className="alert success" role="status">{message}</p>}
    <section className="card"><h2>Membros da organização</h2><p className="muted">Convide alguém para usar a própria escala na sua organização.</p><form onSubmit={invite}><label htmlFor="invite-email">E-mail do novo membro</label><input id="invite-email" type="email" required maxLength={254} value={email} onChange={e => setEmail(e.target.value)} /><button className="primary" disabled={busy}>{busy ? 'Aguarde…' : 'Enviar convite'}</button></form>
      {loading ? <p role="status">Carregando membros…</p> : <ul className="user-list">{users.map(u => <li key={u.id}><div><b>{u.name}</b><span>{u.email} · {u.role === 'Owner' ? 'Proprietária' : u.isActive ? 'Membro ativo' : 'Membro inativo'}</span></div>{u.role === 'Member' && <button className="secondary" disabled={busy} onClick={() => toggle(u)}>{u.isActive ? 'Desativar' : 'Ativar'}</button>}</li>)}</ul>}
    </section>
    <section className="card"><div className="section-title"><h2>Histórico de atividades</h2><button className="text-button" disabled={loading} onClick={load}>Atualizar</button></div><p className="muted">Ações na organização, sem senhas ou tokens.</p>{loading ? <p role="status">Carregando histórico…</p> : <ul className="audit-list">{audit?.items.map(item => <li key={item.id}><b>{actionNames[item.action] ?? item.action}</b><span>{new Date(item.createdAt).toLocaleString('pt-BR')} · {users.find(u => u.id === item.userId)?.name ?? 'Usuário'}</span></li>)}</ul>}<div className="pagination"><button className="secondary" disabled={page === 1 || loading} onClick={() => setPage(p => p - 1)}>Anterior</button><span>Página {page} · {audit?.total ?? 0} ações</span><button className="secondary" disabled={!audit || page * 25 >= audit.total || loading} onClick={() => setPage(p => p + 1)}>Próxima</button></div></section>
  </div>;
}
