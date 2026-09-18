import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import AuthPage from './AuthPage';
afterEach(() => cleanup());
describe('autenticação na interface', () => {
  it('mostra erro de login sem perder e-mail', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({ title: 'E-mail ou senha incorretos.' }), { status: 401 })));
    render(<AuthPage resetToken="" inviteToken="" startupError="" onComplete={() => {}} />);
    const user = userEvent.setup(); await user.type(screen.getByLabelText('E-mail'), 'teste@example.com'); await user.type(screen.getByLabelText('Senha'), 'senha-errada'); await user.click(screen.getByRole('button', { name: 'Entrar na minha escala' }));
    expect((await screen.findByRole('alert')).textContent).toContain('E-mail ou senha incorretos.');
    expect((screen.getByLabelText('E-mail') as HTMLInputElement).value).toBe('teste@example.com');
  });
  it('bloqueia confirmação de senha diferente antes de enviar dados', async () => {
    const fetchMock = vi.fn(); vi.stubGlobal('fetch', fetchMock);
    render(<AuthPage resetToken="token-test" inviteToken="" startupError="" onComplete={() => {}} />);
    const user = userEvent.setup(); await user.type(screen.getByLabelText('Senha (mínimo de 12 caracteres)'), 'senha-segura-123'); await user.type(screen.getByLabelText('Confirme a senha'), 'outra-senha-123'); await user.click(screen.getByRole('button', { name: 'Salvar nova senha' }));
    expect(screen.getByRole('alert').textContent).toContain('As senhas precisam ser iguais.'); expect(fetchMock).not.toHaveBeenCalled();
  });
  it('envia recuperação com header CSRF e mostra resposta neutra', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ message: 'Se este e-mail estiver cadastrado, você receberá um link.' }), { status: 200 })); vi.stubGlobal('fetch', fetchMock);
    render(<AuthPage resetToken="" inviteToken="" startupError="" onComplete={() => {}} />);
    const user = userEvent.setup(); await user.click(screen.getByRole('button', { name: 'Esqueci minha senha' })); await user.type(screen.getByLabelText('E-mail'), 'teste@example.com'); await user.click(screen.getByRole('button', { name: 'Enviar link de recuperação' }));
    expect((await screen.findByRole('status')).textContent).toContain('Se este e-mail estiver cadastrado');
    expect(fetchMock.mock.calls[0][1].headers['X-CSRF']).toBe('1');
  });
});
