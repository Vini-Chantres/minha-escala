# Publicação pelo GitHub Actions

O React fica hospedado como site estático no DigitalOcean App Platform; a API C#/.NET 10 executa em um contêiner Vercel. O endereço público da aplicação é o domínio padrão HTTPS do projeto Vercel, por exemplo `https://minha-escala-api.vercel.app`. A Vercel encaminha páginas/assets ao site DigitalOcean e `/api/*` e `/health` ao serviço C#. Assim, frontend e API têm a mesma origem no navegador: o refresh cookie continua HttpOnly, Secure e SameSite=Strict, **sem precisar comprar domínio ou liberar cookies de terceiros**. Acessos de usuários devem usar o endereço Vercel; o endereço DigitalOcean é a origem de assets, não o endereço de login.

Container Images e Services da Vercel estão em **beta**, conforme documentação consultada em 18/09/2026. Confirme a disponibilidade na sua conta antes da primeira publicação.

## Repositório e branches

Repositório: [Vini-Chantres/minha-escala](https://github.com/Vini-Chantres/minha-escala).

O conteúdo desta pasta é a raiz do repositório, incluindo `.github/`, `.do/`, `.config/`, lockfiles e `LICENSE`. Não versionar `.env`, `.neon`, configurações locais de agentes, `.vercel`, `node_modules`, `bin`, `obj` ou `publish`.

Fluxo: **dev → hml → main**. Push em `dev`/`hml` e pull requests executam apenas CI. Push em `main` executa CI e, após aprovação das verificações, os jobs de publicação. A execução manual de deploy também verifica que a branch é `main`. Nenhum job de `dev`/`hml` acessa o banco de produção: os testes usam PostgreSQL temporário exclusivo.

Promova mudanças por merges nessa ordem e confira o CI antes de cada promoção. Não faça force-push para promover branches. O ambiente GitHub `production` agrupa os secrets/variables de publicação. Regras de aprovação que você configure nesse ambiente serão respeitadas.

## DigitalOcean

Autorize a integração GitHub do App Platform a ler o repositório e crie um token com as permissões necessárias para leitura/escrita de App Platform. A documentação orienta fazer a autorização inicialmente pelo painel, criando uma aplicação (uma aplicação de exemplo também serve).

`.do/app.yaml` cria/atualiza um app pelo nome estável `DO_APP_NAME`, com `frontend/` como origem, `npm ci && npm run build`, `dist/` como saída e fallback `index.html`. O deploy automático da integração está desativado para que somente o workflow publique após CI. Não reutilize o nome de outro app que não deva ser gerenciado por este spec.

O spec deixa `VITE_API_URL` **vazia**: o React chama `/api` no endereço público Vercel. Nunca coloque banco/JWT/SMTP em variáveis `VITE_*`. A action retorna o endereço `*.ondigitalocean.app`; o workflow preenche automaticamente as rewrites Vercel com ele. Não é preciso cadastrar domínio customizado nem informar antecipadamente o endereço DigitalOcean.

O App Platform recompila a branch `main`; evite modificar/publicar essa branch durante uma execução. Os deploys pelo GitHub são serializados e não interrompem migrations em andamento. Observe limites e cobrança da conta: este spec usa site estático, sem web service pago adicional para fazer proxy.

## Vercel

Crie/vincule um projeto com Root Directory **na raiz do repositório**, não em `frontend/` ou `backend/`. Confirme suporte a Services/Container Images. O serviço `backend` de `vercel.json` usa `Dockerfile.vercel`; a imagem compila a API, executa como usuário sem privilégios e escuta HTTP em 8080. HTTPS é terminado pela Vercel.

Use o domínio de produção HTTPS padrão do projeto em `APP_URL`. Os IDs da organização/equipe e do projeto estão nas configurações Vercel ou em `.vercel/project.json` após vinculação pelo CLI. Não versionar esse arquivo. Crie um token com acesso à equipe/projeto. `git.deploymentEnabled: false` evita deploy direto da integração fora das Actions. A regra de páginas começa com `example.invalid` no arquivo versionado e é substituída pelo endereço real retornado pela DigitalOcean **durante o workflow**.

Nas variáveis **Production da Vercel**, configure:

| Variável | Valor |
| --- | --- |
| `DATABASE_URL` | Conexão pooled Neon, com TLS e `channel_binding=require`. |
| `JWT_SECRET` | Segredo aleatório com pelo menos 32 bytes. |
| `JWT_ISSUER` | `minha-escala-api` |
| `JWT_AUDIENCE` | `minha-escala-web` |
| `APP_URL` | Exatamente o endereço público Vercel, sem barra final. Deve coincidir com a variable GitHub `APP_URL`. |
| `SMTP_HOST`, `SMTP_FROM` | Obrigatórios em produção para convites/recuperação de senha. |
| `SMTP_PORT`, `SMTP_USER`, `SMTP_PASSWORD`, `SMTP_SECURITY` | Valores reais do servidor SMTP; normalmente porta 587 e `StartTls`. |
| `PORT`, `ASPNETCORE_HTTP_PORTS` | `8080`. Não configure `ASPNETCORE_URLS` com outra porta. |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `APPLY_MIGRATIONS` | `false` |
| `VERCEL_PROXY` | `true` somente no contêiner com ingresso privado Vercel; já habilitado nessa imagem específica. |

`VERCEL_PROXY` reconhece HTTPS/IP pelos headers definidos pela Vercel. Em um servidor público independente, mantenha-o desativado. CORS/CSRF continuam exigindo a origem `APP_URL`. O domínio de produção deve ser público, sem proteção de deploy que exija login Vercel para o usuário final; dados permanecem protegidos pela autenticação da aplicação.

## GitHub: secrets e variables

Settings → Environments → **production**:

| Tipo | Nome | Conteúdo |
| --- | --- | --- |
| Secret | `NEON_MIGRATION_DATABASE_URL` | Conexão **direta** Neon para o mesmo banco da API, com permissões de migrations. Configurado nesta entrega após autorização explícita. |
| Secret | `VERCEL_TOKEN` | Token Vercel da equipe/projeto. |
| Secret | `DIGITALOCEAN_ACCESS_TOKEN` | Token App Platform DigitalOcean. |
| Variable | `VERCEL_ORG_ID` | ID da organização/equipe Vercel. |
| Variable | `VERCEL_PROJECT_ID` | ID do projeto Vercel. |
| Variable | `APP_URL` | Origem HTTPS pública do projeto Vercel, sem barra final. |
| Variable, opcional | `DO_APP_NAME` | Nome estável exclusivo; padrão `minha-escala-frontend`. |
| Variable, opcional | `DO_REGION` | Região App Platform; padrão `nyc`. |

**Após trocar a senha Neon**, atualize o secret `NEON_MIGRATION_DATABASE_URL`, `DATABASE_URL` na Vercel e os arquivos locais de conexão. A senha não está no Git. Não envie tokens ou senhas em issues/commits/logs. Credenciais da aplicação ficam na Vercel; o GitHub recebe somente tokens de deploy e a conexão de migrations.

## Configuração Neon instalada

Projeto `aged-pond-28213465`, branch `production`. CLI Neon, skills e MCP OAuth do Codex foram instalados localmente. `.neon` e configurações locais de agentes são ignorados pelo Git. O MCP usa OAuth e exige autorização no cliente ao primeiro uso; configurar o servidor não equivale a uma sessão MCP autenticada.

`neon.ts` contém exatamente a política mínima solicitada:

```ts
import { defineConfig } from "@neon/config/v1";
export default defineConfig({});
```

O `neon deploy` dessa política reconcilia a configuração do projeto; **não publica React/.NET e não substitui migrations EF Core**. O plano e o deploy foram executados sem alterações na configuração remota. A migration inicial EF Core foi aplicada ao banco Neon e conferida pelo histórico/tabelas. Não execute testes de integração na base de produção.

Para preparar em outro computador, instale o CLI, conclua `neon login`, execute `neon link --project-id aged-pond-28213465 --branch production --no-env-pull -y`, `npm ci`, `neon config plan` e `neon deploy --no-env-pull`. As ferramentas de skills exigiram Node 22.20+; nesta máquina foi usado Node 22.23.2 portátil. Frontend e CI usam Node 22.

## Sequência e validação

1. CI: testes do roteamento/configuração, lint/build/22 testes React; restore/build/24 testes .NET com PostgreSQL temporário; build da imagem Docker Linux.
2. Conferência dos secrets/variables de produção. Configuração ausente impede qualquer publicação.
3. Publicação do site estático DigitalOcean e obtenção automática da URL de assets.
4. Migrations Neon por conexão direta, renderização de `vercel.json`, deploy do backend e das rotas Vercel.
5. Verificação HTTP de `/health` e da página inicial na origem pública.

Futuras migrations e releases frontend devem ser compatíveis com a API que permanece ativa durante a publicação. O workflow não faz rollback destrutivo do banco. Após a primeira publicação, confira cadastro/login, CRUD da escala, recarregamento/renovação de sessão, logout, convites e recuperação por SMTP. HTTP 200 não substitui essa checagem funcional real.

## Referências oficiais

- [DigitalOcean: GitHub Actions](https://docs.digitalocean.com/products/app-platform/how-to/deploy-from-github-actions/)
- [DigitalOcean: action e parâmetros](https://github.com/digitalocean/app_action)
- [Vercel: Container Images](https://vercel.com/docs/functions/container-images)
- [Vercel: Services](https://vercel.com/docs/services)
- [Vercel: rewrites externas](https://vercel.com/docs/routing/rewrites)
