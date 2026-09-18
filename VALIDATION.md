# Validação da entrega

Validação original executada em 17/09/2026 (America/Sao_Paulo), após implementar as funcionalidades: **21 testes .NET + 20 testes frontend aprovados; nenhum ignorado ou com falha**. Após adicionar a publicação pelo GitHub em 18/09/2026, foram aprovados **24 testes .NET + 22 testes frontend**, sem falhas ou testes ignorados.

## Validação da publicação pelo GitHub — 18/09/2026

- Restore .NET com `--locked-mode`, build Release: zero avisos/erros.
- 24 testes backend aprovados com PostgreSQL 18 local exclusivo, incluindo isolamento, autenticação, persistência e reconhecimento dos headers Vercel somente quando explicitamente habilitado.
- O comando de migration usado pelo workflow foi executado de verdade com `Infrastructure` como projeto e startup project. Migration inicial aplicada com sucesso; o fixture dos testes voltou a executar as migrations de forma idempotente.
- Frontend: lint e build aprovados; 22 testes aprovados, incluindo chamadas de API e renovação de sessão para a origem HTTPS configurada. Build com `VITE_API_URL=https://api.example.com` aprovado e a URL confirmada no arquivo gerado.
- `actionlint` 1.7.12 aprovou os dois workflows; arquivos YAML e JSON analisados sem erros. O verificador de configuração aceitou subdomínios HTTPS do mesmo domínio e rejeitou origens incompatíveis e URLs com caminho.
- Actions oficiais fixadas por SHA consultado nos respectivos repositórios; CLI Vercel fixado em 59.23.1. Especificação DigitalOcean e Container Images Vercel conferidas na documentação oficial.
- O PostgreSQL temporário foi parado e o arquivo temporário de senha foi removido. Nenhum banco Neon ou serviço externo foi alterado.

**Limites da verificação local inicial:** o Docker CLI está instalado, mas o daemon Docker Desktop não está em execução; portanto a imagem não foi construída localmente. O build da imagem é uma etapa obrigatória do CI Linux antes do deploy. Disponibilidade dos recursos beta Vercel e SMTP externo precisam ser confirmados na primeira publicação. Consulte [DEPLOYMENT.md](DEPLOYMENT.md).

## Integração Neon e GitHub — 18/09/2026

Projeto Neon `aged-pond-28213465` vinculado à branch `production`. CLI Neon 5.0.0, skills e configuração MCP OAuth do Codex instalados. Foi usado Node 22.23.2 portátil para as ferramentas que exigem Node 22.20+. O login Neon foi concluído no armazenamento padrão local, pois o CLI não conseguiu usar o chaveiro deste Windows.

`neon.ts` contém `defineConfig({})`, conforme solicitado. `neon config plan` e `neon deploy` concluíram sem alterações na política remota. A migration EF Core `20260918023154_InitialCreate` foi aplicada no Neon autorizado por conexão direta, TLS com validação de certificado/hostname e channel binding obrigatório. O histórico de migrations e nove tabelas foram conferidos por leitura: `__EFMigrationsHistory`, `audit_logs`, `auth_sessions`, `invitations`, `password_resets`, `refresh_tokens`, `schedule_entries`, `tenants`, `users`. Nenhum dado de teste foi inserido nesse banco de produção.

Repositório Git configurado para `Vini-Chantres/minha-escala`, preservando seu commit inicial e LICENSE. Ambiente `production` criado e secret criptografado `NEON_MIGRATION_DATABASE_URL` configurado após autorização explícita. Arquivos locais de credenciais, contexto Neon e configurações de agentes estão fora do Git. Uma verificação dos arquivos preparados para commit não encontrou senhas Neon ou chaves privadas.

O CI executa em `dev`/`hml` e pull requests; deploy só em `main`. A publicação foi adaptada para contas sem domínio próprio: frontend estático DigitalOcean, API em serviço C# Vercel e rewrites de páginas para a origem DigitalOcean, com um único endereço público Vercel. Mais quatro testes nativos Node foram aprovados para configuração HTTPS, ausência de secrets, preservação das rotas API/health e renderização segura das rewrites. Os workflows atualizados passaram no actionlint.

O deploy real ainda depende dos tokens DigitalOcean/Vercel, IDs/APP_URL e variáveis de runtime/SMTP Vercel. O secret de migrations e as demais conexões devem ser atualizados após a troca da senha Neon.

## Ambiente e resultados

| Verificação | Resultado |
|---|---|
| SDK .NET | 10.0.400 |
| Runtime de desenvolvimento | Node.js 22; PostgreSQL 18 local |
| Restore das ferramentas e pacotes .NET | Aprovado |
| Build .NET Release da solução | Aprovado, 0 erros e 0 avisos |
| Migration inicial EF Core | Gerada, aplicada e consistente com o modelo |
| `migrations has-pending-model-changes` | Nenhuma alteração pendente |
| SQL idempotente | Gerado em `database/migrations.sql` |
| Testes .NET | 21 aprovados, 0 falhas, 0 ignorados |
| Restore npm pelo lockfile (`npm ci`) | Aprovado |
| ESLint com `--max-warnings 0` | Aprovado |
| TypeScript + build Vite de produção | Aprovado |
| Testes Vitest/Testing Library | 20 aprovados em 4 arquivos |
| Auditoria npm | 0 vulnerabilidades reportadas |
| Auditoria NuGet com dependências indiretas | Nenhum pacote vulnerável reportado |
| Script `scripts/check.ps1` | Executado com sucesso; a última correção do formulário também passou novamente em lint/build/testes frontend |
| Script `scripts/publish.ps1` | Executado com sucesso com processos de desenvolvimento parados |
| Pacote publicado | API e React servidos pela mesma origem local, sem depender do Vite |

## Cobertura .NET

Nove casos unitários verificam PBKDF2/salt/checagem de senha, tokens aleatórios e hash, validação de tipo/setor/data/senha/e-mail, decodificação da URI Neon e exigência de TLS com validação de certificado em produção.

Doze testes HTTP usam **PostgreSQL real**: health, proteção anônima, rotas desconhecidas, cadastro/login/validação, CRUD e atualização do mesmo dia, persistência após um novo login, isolamento entre organizações e usuários, refresh rotativo e revogação por reutilização, logout com revogação imediata, recuperação/redefinição com token de uso único e invalidação das sessões, convite e papel Member, bloqueio de acesso administrativo, desativação/reativação, proteção do Owner, importação atômica preservando datas existentes, CSRF/origem, auditoria sem credenciais/conteúdo da escala e limite de tentativas com 429/Retry-After.

As bases `escala_test_integration` e `escala_test_functional` foram criadas numa instância temporária separada, autenticada por SCRAM, em `127.0.0.1:55439`. Não foram utilizados bancos existentes ou credenciais Neon. A instância temporária foi encerrada depois da validação; segredos de teste não integram os arquivos entregues.

## Cobertura frontend

Onze casos verificam calendário local, navegação em janeiro/dia 31, anos bissextos, transição entre anos, contadores, próxima folga, ordenação, limite de 12 registros, importação do formato antigo e rejeição de arquivos inválidos.

Três verificam erro de login sem perder e-mail, confirmação de senha antes de enviar dados e recuperação com mensagem neutra/header CSRF. Outros três verificam um único refresh para requisições que expiram juntas, coordenação entre abas com Web Locks sem armazenamento de tokens e ausência de refresh em login recusado.

Três testes de regressão cobrem preenchimento de tipo/setor de hoje após carregar os dados, exclusão somente depois de confirmação/cancelamento e preservação dos campos ao alterar a data do formulário para um dia vazio, mantendo o comportamento do HTML original.

## Verificação funcional no navegador

Conta e dados exclusivamente de teste. Verificados cadastro, agenda, criação de folga e plantão, edição, setores/cores, contadores, próxima folga, próximos dias, filtro de folgas, seleção pelo calendário, alteração nativa do campo de data, cancelamento e confirmação de exclusão, página de configurações e histórico real das ações. Também verificados logout após recarregar, novo login com recuperação dos registros, persistência após reiniciar o backend e restauração de sessão em duas abas abertas simultaneamente.

Inspeção visual e largura de conteúdo realizadas em desktop (1280 px), tablet (768 px), celular (390 px) e celular pequeno (320 px), sem rolagem horizontal. A visualização diária é selecionada inicialmente em telas de até 640 px. A correção do formulário foi conferida também no navegador, com o setor preenchido após recarregar.

## Reexecutar

Na raiz do projeto, configure `TEST_DATABASE_URL` apontando para uma base **exclusiva** cujo nome comece com `escala_test_`. O fixture aplica as migrations sem apagar bases/tabelas. Não configure essa variável com sua base real ou de produção.

```powershell
$env:TEST_DATABASE_URL = 'postgresql://USUARIO:SENHA_ESCAPADA@localhost:5432/escala_test_integracao?sslmode=disable'
./scripts/check.ps1
```

Para teste numa branch Neon exclusiva, mantenha `sslmode=require`. Sem `TEST_DATABASE_URL`, os testes de integração são explicitamente ignorados; os testes unitários continuam disponíveis. O nome da base é verificado antes de iniciar a integração.

## Limites desta validação

Na validação original, PostgreSQL/Npgsql/EF Core foram executados localmente e a URI Neon/requisitos TLS foram testados. Após o usuário fornecer a conexão, o Neon real foi integrado e migrado conforme a seção acima. Envio externo de SMTP e implantação HTTPS ainda não foram realizados: recuperação/convites foram exercitados pelo fallback local de desenvolvimento e pela API. Configure credenciais SMTP válidas e os segredos no servidor antes de usar em produção.

Durante a verificação foram corrigidas vulnerabilidades de dependências de e-mail/testes, conflitos de versão EF Core, tipos nullable SMTP e preenchimento do formulário após carregamento. Bloqueios de arquivos do Windows ocorreram quando serviços de desenvolvimento estavam em execução; os serviços foram parados e a preparação da publicação foi concluída. O README explica essa sequência.
