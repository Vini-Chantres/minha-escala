import type { Auth } from './types';

let token: string | null = null;
let refreshPromise: Promise<Auth> | null = null;
let sessionListener: ((auth: Auth | null) => void) | null = null;
function apiUrl(path: string) { return `${(import.meta.env.VITE_API_URL ?? '').replace(/\/$/, '')}/api${path}`; }
export function onSessionChange(listener: (auth: Auth | null) => void) { sessionListener = listener; }
export function setSession(auth: Auth | null) { token = auth?.accessToken ?? null; sessionListener?.(auth); }
export class HttpError extends Error { constructor(message: string, public status: number) { super(message); } }

async function read<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const problem = await response.json().catch(() => ({}));
    const details = problem.errors ? Object.values(problem.errors).flat().join(' ') : '';
    throw new HttpError(details || problem.title || (response.status === 401 ? 'Sua sessão terminou. Entre novamente.' : 'Não foi possível concluir. Tente novamente.'), response.status);
  }
  return response.status === 204 ? undefined as T : response.json();
}
export function refresh(): Promise<Auth> {
  if (!refreshPromise) {
    const request = () => fetch(apiUrl('/auth/refresh'), { method: 'POST', credentials: 'include', headers: { 'X-CSRF': '1' } }).then(read<Auth>);
    // Serializa a rotação entre abas; o cookie atualizado será usado pela próxima aba.
    const coordinatedRequest = async () => navigator.locks ? await navigator.locks.request('minha-escala-refresh', request) : await request();
    refreshPromise = coordinatedRequest()
      .then(auth => { setSession(auth); return auth; })
      .catch(e => { setSession(null); throw e; }).finally(() => { refreshPromise = null; });
  }
  return refreshPromise;
}
export async function api<T>(path: string, method = 'GET', body?: unknown, retry = true): Promise<T> {
  const headers: Record<string, string> = { 'X-CSRF': '1' };
  if (token) headers.Authorization = `Bearer ${token}`;
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  let response: Response;
  try { response = await fetch(apiUrl(path), { method, credentials: 'include', headers, body: body === undefined ? undefined : JSON.stringify(body) }); }
  catch { throw new HttpError('Sem conexão com o servidor. Verifique sua internet e tente novamente.', 0); }
  if (response.status === 401 && retry && (!path.startsWith('/auth/') || path === '/auth/logout' || path === '/auth/me')) {
    await refresh(); return api<T>(path, method, body, false);
  }
  return read<T>(response);
}
