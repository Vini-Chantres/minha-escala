import { describe, expect, it } from 'vitest';
import { dateKey, importLegacy, moveDay, moveMonth, summary } from './calendar';
import type { Entry } from './types';
describe('preservação da escala', () => {
  it('navega de janeiro para fevereiro sem pular mês a partir do dia 31', () => {
    expect(dateKey(moveMonth(new Date(2026, 0, 31, 12), 1))).toBe('2026-02-01');
    expect(dateKey(moveMonth(new Date(2026, 0, 31, 12), -1))).toBe('2025-12-01');
  });
  it('navega entre dias, meses e ano bissexto no horário local', () => {
    expect(moveDay('2024-02-28', 1)).toBe('2024-02-29'); expect(moveDay('2026-12-31', 1)).toBe('2027-01-01');
  });
  it('mantém contadores do mês, próxima folga e próximos registros ordenados', () => {
    const entries: Entry[] = [
      { date: '2026-10-01', type: 'folga', sector: '' }, { date: '2026-09-20', type: 'trabalho', sector: 'UTI' },
      { date: '2026-09-17', type: 'folga', sector: 'UTI' }, { date: '2026-09-01', type: 'folga', sector: 'Clínica' },
    ];
    const result = summary(entries, new Date(2026, 8, 1, 12), '2026-09-17');
    expect(result.off).toBe(2); expect(result.work).toBe(1); expect(result.sectors).toBe(2);
    expect(result.nextOff?.date).toBe('2026-09-17'); expect(result.upcoming.map(x => x.date)).toEqual(['2026-09-17', '2026-09-20', '2026-10-01']);
    expect(summary(entries, new Date(2026, 8, 1, 12), '2027-01-01').nextOff).toBeUndefined();
  });
  it('limita a lista aos 12 próximos registros', () => {
    const entries = Array.from({ length: 20 }, (_, i) => ({ date: `2026-09-${String(i + 1).padStart(2, '0')}`, type: 'folga' as const, sector: '' }));
    expect(summary(entries, new Date(2026, 8, 1), '2026-09-01').upcoming).toHaveLength(12);
  });
  it('importa o formato localStorage original como texto seguro', () => {
    expect(importLegacy('{"2026-09-17":{"type":"folga","sector":" <script>texto</script> "}}')).toEqual([{ date: '2026-09-17', type: 'folga', sector: '<script>texto</script>' }]);
  });
  it.each(['{}', '[]', '{"2026-02-30":{"type":"folga","sector":""}}', '{"2026-09-17":{"type":"admin","sector":""}}', '{"2026-09-17":null}', '{"2026-09-17":{"type":"folga","sector":7}}'])('rejeita arquivo inválido %s', text => { expect(() => importLegacy(text)).toThrow(); });
});
