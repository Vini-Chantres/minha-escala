import { afterEach, expect, it, vi } from 'vitest';
import { api, HttpError, refresh, setSession } from './api';
const auth = { accessToken: 'token-novo', expiresAt: '2026-09-18T04:00:00Z', user: { id: 'user', tenantId: 'tenant', name: 'Teste', email: 'test@example.com', role: 'Owner' as const, isActive: true } };
afterEach(() => { setSession(null); vi.unstubAllGlobals(); vi.unstubAllEnvs(); });
it('usa a API publicada com credenciais, CSRF e token de acesso', async () => {
  vi.stubEnv('VITE_API_URL', 'https://api.example.com/'); setSession(auth);
  const fetchMock = vi.fn().mockResolvedValue(new Response('{}', { status: 200 })); vi.stubGlobal('fetch', fetchMock);
  await api('/entries');
  expect(fetchMock).toHaveBeenCalledWith('https://api.example.com/api/entries', expect.objectContaining({
    credentials: 'include', headers: { 'X-CSRF': '1', Authorization: 'Bearer token-novo' },
  }));
});
it('renova a sessão na API publicada mantendo o cookie HttpOnly', async () => {
  vi.stubEnv('VITE_API_URL', 'https://api.example.com');
  const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify(auth), { status: 200 })); vi.stubGlobal('fetch', fetchMock);
  await refresh();
  expect(fetchMock).toHaveBeenCalledWith('https://api.example.com/api/auth/refresh', expect.objectContaining({ credentials: 'include' }));
});
it('compartilha um refresh quando duas requisições protegidas expiram juntas', async () => {
  const fetchMock = vi.fn(async (path: string, options: RequestInit) => {
    if (path === '/api/auth/refresh') return new Response(JSON.stringify(auth), { status: 200 });
    if ((options.headers as Record<string, string>).Authorization !== 'Bearer token-novo') return new Response('{}', { status: 401 });
    return new Response(JSON.stringify({ saved: true }), { status: 200 });
  });
  vi.stubGlobal('fetch', fetchMock);
  const results = await Promise.all([api('/entries'), api('/organization/users')]);
  expect(results).toEqual([{ saved: true }, { saved: true }]);
  expect(fetchMock.mock.calls.filter(x => x[0] === '/api/auth/refresh')).toHaveLength(1);
});
it('coordena refresh entre abas via Web Locks sem guardar token no armazenamento', async () => {
  const request = vi.fn(async (_name: string, action: () => Promise<unknown>) => action());
  vi.stubGlobal('navigator', { locks: { request } });
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify(auth), { status: 200 })));
  expect((await refresh()).accessToken).toBe('token-novo'); expect(request).toHaveBeenCalledOnce();
  expect(request.mock.calls[0][0]).toBe('minha-escala-refresh'); expect(localStorage.getItem('accessToken')).toBeNull();
});
it('não tenta refresh em um login recusado', async () => {
  const fetchMock = vi.fn().mockResolvedValue(new Response('{"title":"E-mail ou senha incorretos."}', { status: 401 })); vi.stubGlobal('fetch', fetchMock);
  await expect(api('/auth/login', 'POST', { email: 'x@example.com', password: 'bad' })).rejects.toBeInstanceOf(HttpError);
  expect(fetchMock).toHaveBeenCalledOnce();
});
