import { useCallback, useEffect, useState, type CSSProperties, type FormEvent } from 'react';
import { api, setSession } from './api';
import { colors, dateKey, formatDate, importLegacy, moveDay, moveMonth, parseDate, summary } from './calendar';
import type { Entry, User } from './types';
import Organization from './Organization';

export default function SchedulePage({ user }: { user: User }) {
  const today = dateKey(new Date());
  const [tab, setTab] = useState<'agenda' | 'settings'>('agenda');
  const [entries, setEntries] = useState<Entry[]>([]); const [loading, setLoading] = useState(true);
  const [current, setCurrent] = useState(() => new Date(new Date().getFullYear(), new Date().getMonth(), 1, 12));
  const [form, setForm] = useState<Entry>({ date: today, type: 'folga', sector: '' });
  const [view, setView] = useState<'month' | 'day'>(() => window.matchMedia('(max-width: 640px)').matches ? 'day' : 'month');
  const [selectedDay, setSelectedDay] = useState(today);
  const [typeFilter, setTypeFilter] = useState(''); const [sectorFilter, setSectorFilter] = useState('');
  const [busy, setBusy] = useState(false); const [error, setError] = useState(''); const [message, setMessage] = useState('');
  const [deleteDate, setDeleteDate] = useState<string | null>(null);
  const [pendingImport, setPendingImport] = useState<Entry[] | null>(null);
  const load = useCallback(async () => {
    setLoading(true);
    try {
      const saved = await api<Entry[]>('/entries'); setEntries(saved);
      setForm(old => { const entry = saved.find(x => x.date === old.date); return entry ? { ...entry } : old; });
      setError('');
    }
    catch (e) { setError(e instanceof Error ? e.message : 'Não foi possível carregar.'); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => { void load(); }, [load]);
  const stats = summary(entries, current, today);
  const sectors = [...new Set(entries.map(x => x.sector).filter(Boolean))].sort((a, b) => a.localeCompare(b));
  const color = (sector: string) => colors[Math.max(0, sectors.indexOf(sector)) % colors.length];
  const filtered = entries.filter(x => (!typeFilter || x.type === typeFilter) && (!sectorFilter || x.sector === sectorFilter));
  const byDate = new Map(filtered.map(x => [x.date, x]));
  const existing = entries.find(x => x.date === form.date);
  const monthLabel = current.toLocaleDateString('pt-BR', { month: 'long', year: 'numeric' });
  const first = new Date(current.getFullYear(), current.getMonth(), 1).getDay();
  const days = new Date(current.getFullYear(), current.getMonth() + 1, 0).getDate();
  function select(date: string, keepDraft = false) {
    const entry = entries.find(x => x.date === date);
    setForm(old => entry ? { ...entry } : keepDraft ? { ...old, date } : { date, type: 'folga', sector: '' }); setSelectedDay(date); setMessage(''); setDeleteDate(null);
  }
  function clear() { setForm({ date: today, type: 'folga', sector: '' }); setDeleteDate(null); }
  async function save(e: FormEvent) {
    e.preventDefault(); setBusy(true); setError(''); setMessage('');
    try {
      const saved = await api<Entry>(`/entries/${form.date}`, 'PUT', { date: form.date, type: form.type, sector: form.sector });
      setEntries(old => [...old.filter(x => x.date !== saved.date), saved]); setForm(saved);
      setCurrent(moveMonth(parseDate(saved.date), 0)); setSelectedDay(saved.date); setMessage('Registro salvo na sua escala.');
    } catch (e) { setError(e instanceof Error ? e.message : 'Não foi possível salvar.'); }
    finally { setBusy(false); }
  }
  async function remove() {
    if (!deleteDate) return;
    setBusy(true); setError('');
    try { await api(`/entries/${deleteDate}`, 'DELETE'); setEntries(old => old.filter(x => x.date !== deleteDate)); setForm({ date: deleteDate, type: 'folga', sector: '' }); setDeleteDate(null); setMessage('Registro excluído.'); }
    catch (e) { setError(e instanceof Error ? e.message : 'Não foi possível excluir.'); }
    finally { setBusy(false); }
  }
  async function logout() {
    setBusy(true); setError('');
    try { await api('/auth/logout', 'POST'); setSession(null); }
    catch (e) { setError(e instanceof Error ? e.message : 'Não foi possível sair.'); }
    finally { setBusy(false); }
  }
  async function prepareImport(file?: File) {
    if (!file) return;
    setError(''); setMessage('');
    try { if (file.size > 1_000_000) throw new Error('O arquivo precisa ter menos de 1 MB.'); setPendingImport(importLegacy(await file.text())); }
    catch (e) { setError(e instanceof Error ? e.message : 'Arquivo inválido.'); }
  }
  async function commitImport() {
    if (!pendingImport) return;
    setBusy(true); setError('');
    try { const result = await api<{ imported: number; skipped: number }>('/entries/import', 'POST', pendingImport); setPendingImport(null); await load(); setMessage(`${result.imported} registros importados; ${result.skipped} datas já cadastradas foram preservadas.`); }
    catch (e) { setError(e instanceof Error ? e.message : 'Não foi possível importar.'); }
    finally { setBusy(false); }
  }
  return <div className="app-shell">
    <header className="app-header"><div className="brand"><span className="flower" aria-hidden="true">🌷</span><div><h1>Minha Escala <span aria-hidden="true">💗</span></h1><p className="muted">Sua rotina de trabalho, organizada com carinho</p></div></div><div className="account"><span>Olá, {user.name.split(' ')[0]}</span><button className="secondary" disabled={busy} onClick={logout}>Sair</button></div></header>
    <nav className="tabs" aria-label="Navegação principal"><button aria-current={tab === 'agenda' ? 'page' : undefined} onClick={() => setTab('agenda')}>Minha agenda</button><button aria-current={tab === 'settings' ? 'page' : undefined} onClick={() => setTab('settings')}>Configurações</button></nav>
    <main>
      {error && <div className="alert error" role="alert">{error} <button className="text-button" disabled={loading || busy} onClick={load}>Atualizar</button></div>}
      {message && <div className="alert success" role="status">{message}</div>}
      {tab === 'agenda' ? <>
        <section className="hero card"><div><p className="eyebrow">PRÓXIMA FOLGA</p><h2>{stats.nextOff ? formatDate(stats.nextOff.date) : 'Seu descanso começa aqui'}</h2><p>{stats.nextOff ? stats.nextOff.sector || 'Dia de descanso 🌸' : 'Cadastre suas folgas para acompanhar.'}</p></div><div className="stats"><div><b>{stats.off}</b><span>folgas no mês</span></div><div><b>{stats.work}</b><span>plantões</span></div><div><b>{stats.sectors}</b><span>setores</span></div></div></section>
        <div className="agenda-layout">
          <section className="card calendar-card" aria-label="Calendário da escala">
            <div className="section-title"><h2>Minha agenda</h2><div className="view-switch" aria-label="Visualização"><button aria-pressed={view === 'month'} onClick={() => setView('month')}>Mês</button><button aria-pressed={view === 'day'} onClick={() => setView('day')}>Dia</button></div></div>
            <div className="month-header"><button className="nav-button" aria-label={view === 'month' ? 'Mês anterior' : 'Dia anterior'} onClick={() => view === 'month' ? setCurrent(d => moveMonth(d, -1)) : setSelectedDay(d => moveDay(d, -1))}>‹</button><h3>{view === 'month' ? monthLabel : formatDate(selectedDay)}</h3><button className="nav-button" aria-label={view === 'month' ? 'Próximo mês' : 'Próximo dia'} onClick={() => view === 'month' ? setCurrent(d => moveMonth(d, 1)) : setSelectedDay(d => moveDay(d, 1))}>›</button></div>
            <div className="filters"><label>Tipo<select value={typeFilter} onChange={e => setTypeFilter(e.target.value)}><option value="">Todos</option><option value="folga">Folgas</option><option value="trabalho">Plantões</option></select></label><label>Setor<select value={sectorFilter} onChange={e => setSectorFilter(e.target.value)}><option value="">Todos os setores</option>{sectors.map(s => <option key={s}>{s}</option>)}</select></label><button className="text-button" onClick={() => { setCurrent(moveMonth(new Date(), 0)); setSelectedDay(today); }}>Hoje</button></div>
            {loading ? <p className="empty-state" role="status">Carregando seus registros…</p> : view === 'month' ? <><div className="weekdays" aria-hidden="true">{['DOM', 'SEG', 'TER', 'QUA', 'QUI', 'SEX', 'SÁB'].map(d => <span key={d}>{d}</span>)}</div><div className="calendar-grid">{Array.from({ length: first }, (_, i) => <div key={`empty-${i}`} />)}{Array.from({ length: days }, (_, i) => {
              const date = dateKey(new Date(current.getFullYear(), current.getMonth(), i + 1, 12)); const entry = byDate.get(date);
              return <button key={date} className={`day ${entry ? entry.type === 'folga' ? 'off' : 'work' : ''} ${date === today ? 'today' : ''} ${date === form.date ? 'selected' : ''}`} aria-label={`${formatDate(date)}${entry ? `, ${entry.type === 'folga' ? 'Folga' : 'Plantão'}, ${entry.sector || 'Sem setor'}` : ', sem registro'}`} aria-pressed={date === form.date} style={{ '--sector': color(entry?.sector ?? '') } as CSSProperties} onClick={() => select(date)}><span>{i + 1}</span>{entry && <small>{entry.type === 'folga' ? '🌸 Folga' : entry.sector || 'Plantão'}</small>}</button>;
            })}</div></> : <div className="day-view"><p className="muted">{selectedDay === today ? 'Sua escala de hoje' : 'Sua escala deste dia'}</p>{byDate.get(selectedDay) ? <EntryRow entry={byDate.get(selectedDay)!} color={color(byDate.get(selectedDay)!.sector)} onSelect={select} /> : <p className="empty-state">Nenhum registro para este dia 🌷</p>}<button className="secondary" onClick={() => select(selectedDay)}>{entries.some(x => x.date === selectedDay) ? 'Editar este dia' : 'Adicionar a este dia'}</button></div>}
            <div className="legend"><span><i style={{ background: '#f7c6da' }} />Folga</span>{sectors.map(s => <span key={s}><i style={{ background: color(s) }} />{s}</span>)}</div>
            <p className="muted calendar-hint">Selecione um dia para adicionar ou editar sua escala.</p>
          </section>
          <aside className="side-cards">
            <section className="card" aria-labelledby="form-title"><div className="section-title"><h2 id="form-title">{existing ? 'Editar registro' : 'Adicionar à escala'} ✨</h2><button className="text-button" disabled={busy} onClick={clear}>Limpar</button></div>
              <form onSubmit={save}><fieldset disabled={busy || loading}><div className="form-row"><div><label htmlFor="entry-date">Data</label><input id="entry-date" type="date" required min="1900-01-01" max="2100-12-31" value={form.date} onChange={e => select(e.target.value, true)} /></div><div><label htmlFor="entry-type">Tipo</label><select id="entry-type" value={form.type} onChange={e => setForm({ ...form, type: e.target.value as Entry['type'] })}><option value="folga">🌸 Folga</option><option value="trabalho">💗 Plantão</option></select></div></div><label htmlFor="entry-sector">Setor</label><input id="entry-sector" list="sectors" placeholder="Ex.: Clínica Médica 2" maxLength={120} value={form.sector} onChange={e => setForm({ ...form, sector: e.target.value })} /><datalist id="sectors">{sectors.map(s => <option key={s} value={s} />)}</datalist><button className="primary">{busy ? 'Aguarde…' : 'Salvar na minha escala'}</button></fieldset></form>
              {existing && <button className="danger" disabled={busy} onClick={() => setDeleteDate(form.date)}>Excluir registro</button>}
              {deleteDate && <div className="confirmation" role="alert"><p>Excluir o registro de {formatDate(deleteDate)}?</p><div className="button-row"><button className="danger solid" disabled={busy} onClick={remove}>Confirmar exclusão</button><button className="secondary" disabled={busy} onClick={() => setDeleteDate(null)}>Cancelar</button></div></div>}
            </section>
            <section className="card"><div className="section-title"><h2>Próximos dias</h2><span className="petal" aria-hidden="true">🌸</span></div><div className="entries">{stats.upcoming.length ? stats.upcoming.map(entry => <EntryRow key={entry.date} entry={entry} color={color(entry.sector)} onSelect={select} />) : <p className="empty-state">Nenhum registro futuro ainda 🌷</p>}</div></section>
          </aside>
        </div>
      </> : <div className="settings-layout"><section className="card"><h2>Sua conta</h2><p><b>{user.name}</b><br />{user.email}</p><p className="muted">{user.role === 'Owner' ? 'Proprietária da organização' : 'Membro da organização'}. Sua escala é pessoal.</p><h3>Trazer minha escala antiga</h3><p className="muted">Selecione o arquivo JSON exportado da agenda original. Datas já cadastradas serão preservadas.</p><label htmlFor="import-file">Arquivo da escala (.json)</label><input id="import-file" type="file" accept="application/json,.json" disabled={busy} onChange={e => { void prepareImport(e.target.files?.[0]); e.target.value = ''; }} />{pendingImport && <div className="confirmation"><p>{pendingImport.length} registros prontos para importar.</p><div className="button-row"><button className="primary" disabled={busy} onClick={commitImport}>Importar registros</button><button className="secondary" disabled={busy} onClick={() => setPendingImport(null)}>Cancelar</button></div></div>}</section>{user.role === 'Owner' && <Organization />}</div>}
    </main><footer>Feita para cuidar da sua rotina 🌷</footer>
  </div>;
}
function EntryRow({ entry, color, onSelect }: { entry: Entry; color: string; onSelect: (date: string) => void }) {
  return <button className="entry" style={{ '--sector': color } as CSSProperties} onClick={() => onSelect(entry.date)}><div><b>{formatDate(entry.date, true)}</b><span>{entry.sector || 'Sem setor'}</span></div><span className="badge">{entry.type === 'folga' ? '🌸 Folga' : '💗 Plantão'}</span></button>;
}
