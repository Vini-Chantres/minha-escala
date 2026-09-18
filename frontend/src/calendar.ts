import type { Entry } from './types';
export const colors = ['#e98caf', '#9b8de0', '#79b9ad', '#e5ad71', '#80a8d9', '#c989b5', '#8dbb73', '#d77d7d'];
export const dateKey = (date: Date) => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
export const parseDate = (key: string) => new Date(`${key}T12:00:00`);
export const formatDate = (key: string, short = false) => parseDate(key).toLocaleDateString('pt-BR', { weekday: short ? 'short' : 'long', day: '2-digit', month: short ? '2-digit' : 'long' });
export const moveMonth = (date: Date, delta: number) => new Date(date.getFullYear(), date.getMonth() + delta, 1, 12);
export const moveDay = (key: string, delta: number) => { const d = parseDate(key); d.setDate(d.getDate() + delta); return dateKey(d); };
export function summary(entries: Entry[], current: Date, today: string) {
  const month = dateKey(current).slice(0, 7);
  const monthly = entries.filter(x => x.date.startsWith(month));
  const future = entries.filter(x => x.date >= today).sort((a, b) => a.date.localeCompare(b.date));
  return { off: monthly.filter(x => x.type === 'folga').length, work: monthly.filter(x => x.type === 'trabalho').length,
    sectors: new Set(monthly.map(x => x.sector).filter(Boolean)).size,
    nextOff: future.find(x => x.type === 'folga'), upcoming: future.slice(0, 12) };
}
export function importLegacy(text: string): Entry[] {
  const data: unknown = JSON.parse(text);
  if (!data || typeof data !== 'object' || Array.isArray(data)) throw new Error('Selecione o JSON exportado da escala antiga.');
  const entries = Object.entries(data).map(([date, raw]) => {
    if (!raw || typeof raw !== 'object' || !('type' in raw) || !('sector' in raw)) throw new Error('Registro inválido no arquivo.');
    const value = raw as Record<string, unknown>;
    if (!/^\d{4}-\d{2}-\d{2}$/.test(date) || Number(date.slice(0, 4)) < 1900 || Number(date.slice(0, 4)) > 2100 ||
      isNaN(parseDate(date).getTime()) || dateKey(parseDate(date)) !== date ||
      !['folga', 'trabalho'].includes(String(value.type)) || typeof value.sector !== 'string' || value.sector.length > 120) throw new Error('Arquivo contém datas, tipos ou setores inválidos.');
    return { date, type: value.type as Entry['type'], sector: value.sector.trim() };
  });
  if (!entries.length || entries.length > 3660) throw new Error('Importe de 1 a 3660 registros.');
  return entries;
}
