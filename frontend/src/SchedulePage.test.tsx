import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import SchedulePage from './SchedulePage';
import { dateKey } from './calendar';
const user = { id: 'user', tenantId: 'tenant', name: 'Teste', email: 'test@example.com', role: 'Owner' as const, isActive: true };
const entry = { id: 'entry', date: dateKey(new Date()), type: 'trabalho', sector: 'Clínica preservada' };
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });
function setup() {
  vi.stubGlobal('matchMedia', vi.fn().mockReturnValue({ matches: false }));
  const fetchMock = vi.fn(async (_path: string, options: RequestInit) => options.method === 'DELETE'
    ? new Response(null, { status: 204 }) : new Response(JSON.stringify([entry]), { status: 200 }));
  vi.stubGlobal('fetch', fetchMock); render(<SchedulePage user={user} />); return fetchMock;
}
it('preenche tipo e setor do registro de hoje após carregar os dados do servidor', async () => {
  setup(); await waitFor(() => expect((screen.getByPlaceholderText('Ex.: Clínica Médica 2') as HTMLInputElement).value).toBe('Clínica preservada'));
  expect((document.getElementById('entry-type') as HTMLSelectElement).value).toBe('trabalho');
  expect(screen.getByRole('heading', { name: 'Editar registro ✨' })).toBeTruthy();
});
it('exclui somente após confirmação e mantém o registro ao cancelar', async () => {
  const fetchMock = setup(); const client = userEvent.setup();
  await screen.findByRole('button', { name: 'Excluir registro' });
  await client.click(screen.getByRole('button', { name: 'Excluir registro' }));
  await client.click(screen.getByRole('button', { name: 'Cancelar' }));
  expect(fetchMock.mock.calls.filter(x => x[1].method === 'DELETE')).toHaveLength(0);
  await client.click(screen.getByRole('button', { name: 'Excluir registro' }));
  await client.click(screen.getByRole('button', { name: 'Confirmar exclusão' }));
  await waitFor(() => expect(screen.queryByRole('button', { name: 'Excluir registro' })).toBeNull());
  expect(fetchMock.mock.calls.filter(x => x[1].method === 'DELETE')).toHaveLength(1);
});
it('mantém setor e tipo ao trocar a data do formulário para um dia ainda vazio', async () => {
  setup(); await waitFor(() => expect((screen.getByPlaceholderText('Ex.: Clínica Médica 2') as HTMLInputElement).value).toBe('Clínica preservada'));
  fireEvent.change(screen.getByLabelText('Data'), { target: { value: '2030-01-02' } });
  expect((screen.getByPlaceholderText('Ex.: Clínica Médica 2') as HTMLInputElement).value).toBe('Clínica preservada');
  expect((document.getElementById('entry-type') as HTMLSelectElement).value).toBe('trabalho');
  expect((screen.getByLabelText('Data') as HTMLInputElement).value).toBe('2030-01-02');
});
