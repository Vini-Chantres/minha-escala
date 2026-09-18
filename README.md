# Minha Escala

Evolução funcional de `legacy/minha_escala_app.html`: React 19 + TypeScript + Vite + Tailwind 4; ASP.NET Core .NET 10 + EF Core + FluentValidation; PostgreSQL/Neon. Somente tema claro. Calendário mensal e visualização diária para celular, folgas/plantões, setores com cores, contadores mensais, próxima folga, 12 próximos registros, cadastro/edição por data, limpeza do formulário e exclusão com confirmação. Nome/setor são renderizados como texto, sem HTML injetado.

Autenticação por e-mail/senha, cadastro, logout, recuperação/redefinição; JWT de 10 minutos somente na memória do frontend, refresh de 30 dias rotativo em cookie HttpOnly/SameSite Strict/Secure em produção. Refresh, recuperação e convite são armazenados somente como SHA-256; senhas usam PBKDF2-SHA256 com salt aleatório e 600.000 iterações. Logout revoga a sessão imediatamente, redefinição revoga todas as sessões. Reutilizar um refresh já consumido revoga a família de tokens. As APIs verificam sessão, versão de segurança e status do usuário em cada requisição.

Cada cadastro independente cria uma organização com um Owner. O Owner envia convites por e-mail para Members, pode ativar/desativar membros e consultar a auditoria paginada da própria organização. A escala é pessoal, também para Owners: todas as consultas/escritas usam `TenantId` e `UserId`. Um índice único garante um registro por usuário/data; a chave estrangeira composta impede registros em outra organização. Papéis, IDs e status não podem ser escolhidos no cadastro. A desativação invalida os acessos anteriores, mesmo após reativação.

## Estrutura

- `frontend/src/`: telas, cliente HTTP, utilitários de calendário e testes.
- `backend/Domain/`: entidades.
- `backend/Application/`: contratos, validação e erros de aplicação.
- `backend/Infrastructure/`: persistência, migrations, senhas, JWT e SMTP.
- `backend/Api/`: composição, autenticação/autorização, controladores, limites de tentativas e tratamento de erros.
- `backend/Tests/`: testes unitários e integração HTTP com PostgreSQL real.
- `legacy/`: HTML original, sem alterações.
- `scripts/`: carregar ambiente, verificar e preparar a aplicação para publicação.

## Requisitos

Node.js 22.12+ e npm; SDK .NET 10; PostgreSQL 17+ ou Neon. Testes de integração usam uma **base PostgreSQL exclusiva e descartável** (não a base real do aplicativo). Docker Compose é uma alternativa opcional para desenvolvimento.

## Configuração segura

Copie `.env.example` para `.env` na raiz. O arquivo não é versionado e **não é carregado pelo .NET automaticamente**: use `scripts/with-env.ps1`, variáveis do processo/servidor ou user-secrets para os comandos que exigem configuração. O script apenas interpreta `CHAVE=valor` literal, sem executar comandos ou expandir variáveis. Não use este arquivo no frontend. Nunca envie `DATABASE_URL`, `JWT_SECRET` ou credenciais SMTP ao navegador.

| Variável | Uso |
|---|---|
| `DATABASE_URL` | URI PostgreSQL `postgresql://usuario:senha@host/neondb?sslmode=require` ou connection string Npgsql. Escape caracteres especiais em usuário/senha na URI. |
| `JWT_SECRET` | Segredo aleatório com pelo menos 32 bytes. Use 32 bytes gerados criptograficamente, codificados em Base64, e nunca reutilize o exemplo. |
| `JWT_ISSUER` / `JWT_AUDIENCE` | Identificadores estáveis da API e frontend. |
| `APP_URL` | Origem do frontend, sem caminho: `http://localhost:5173` em desenvolvimento; `https://sua-origem` em produção. |
| `ASPNETCORE_ENVIRONMENT` | `Development` local; `Production` no servidor. |
| `ASPNETCORE_URLS` | `http://localhost:5080` local. Produção exige HTTPS direto ou proxy com terminação TLS configurado corretamente. |
| `APPLY_MIGRATIONS` | `false` recomendado; `true` permite migration no startup para um único processo. |
| `SMTP_HOST` / `SMTP_PORT` / `SMTP_FROM` | Obrigatórios em produção para recuperação e convites. |
| `SMTP_USER` / `SMTP_PASSWORD` | Credenciais SMTP somente no servidor, quando exigidas. |
| `SMTP_SECURITY` | `StartTls` (padrão, normalmente porta 587) ou `SslOnConnect` (normalmente 465). |

Gere o segredo sem registrá-lo em logs e cole-o apenas no `.env`/gerenciador de segredos. Exemplo PowerShell interativo:

```powershell
[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

### Neon

1. No seu projeto Neon, escolha uma branch/base para este aplicativo e um usuário com permissão de migrations. Copie a URI de conexão para `DATABASE_URL` **somente no backend**.
2. A aplicação interpreta URIs Neon e fortalece `sslmode=require` para `VerifyFull`, validando certificado e hostname. Connection strings em produção precisam de `SSL Mode=VerifyFull`. Não desative a validação TLS.
3. Para migrations, prefira o endpoint direto da branch. A conexão do aplicativo pode usar o endpoint pooled. Configure as variáveis no processo antes de executar os comandos abaixo.
4. Aplique a migration inicial uma vez. Nenhum banco Neon é criado ou modificado sem você configurar a conexão. `/health` retorna 200 quando o banco está acessível e 503 quando indisponível, sem expor credenciais.

### Restore e migrations (PowerShell na raiz)

```powershell
Set-Location backend
dotnet tool restore
dotnet restore MinhaEscala.slnx
Set-Location ..
./scripts/with-env.ps1 -Command { dotnet ef database update --project backend/Infrastructure --startup-project backend/Api }
```

Para gerar migrations futuras: execute `dotnet ef migrations add Nome --project backend/Infrastructure --startup-project backend/Api` no mesmo wrapper. Não use `EnsureCreated` no aplicativo e não apague tabelas para atualizar o esquema.

### Banco local opcional

Defina `POSTGRES_PASSWORD` no `.env`, inicie `docker compose up -d postgres` e use `DATABASE_URL=postgresql://escala:SENHA_ESCAPADA@localhost:5432/escala?sslmode=disable`, exclusivamente em Development. O banco é exposto só em localhost; seus dados ficam em volume persistente. Neon não requer Docker.

## Executar localmente

Em um terminal na raiz:

```powershell
./scripts/with-env.ps1 -Command { dotnet run --project backend/Api --no-launch-profile }
```

Em outro terminal:

```powershell
Set-Location frontend
npm.cmd ci
npm.cmd run dev
```

Abra `http://localhost:5173`. O Vite encaminha `/api` e `/health` à API na porta 5080; o cookie permanece na mesma origem. Faça um cadastro, salve uma folga/plantão, recarregue e entre novamente para confirmar a persistência. Não há credencial padrão. Em Development sem SMTP, o link de recuperação/convite aparece **somente no terminal da API**. Esse fallback contém token sensível: mantenha o terminal local e não publique os logs. Em produção, SMTP é exigido e links não são registrados.

### Importar os dados antigos

O `localStorage` do HTML antigo pertence à origem daquele HTML e não pode ser lido automaticamente pela aplicação nova. Abra o HTML original no mesmo navegador onde usava a escala; no console do navegador execute este código para baixar os seus registros:

```javascript
const blob = new Blob([localStorage.getItem('minhaEscala') || '{}'], { type: 'application/json' });
const url = URL.createObjectURL(blob);
const a = document.createElement('a'); a.href = url; a.download = 'minha-escala.json'; a.click();
URL.revokeObjectURL(url);
```

Na nova aplicação, abra Configurações → Trazer minha escala antiga, selecione o JSON e confirme a importação. Datas já presentes no banco são preservadas. Arquivos inválidos são rejeitados antes de salvar. A importação pertence exclusivamente ao usuário autenticado.

## Verificação final

No Windows, pare os processos de desenvolvimento (API e Vite) antes de verificar ou preparar a publicação: eles mantêm DLLs e módulos nativos em uso.

```powershell
./scripts/check.ps1
```

Esse comando restaura dependências, compila .NET Release, executa testes .NET, reinstala pelo lockfile npm e executa lint/build/testes frontend. Os testes HTTP com PostgreSQL só executam quando `TEST_DATABASE_URL` estiver configurada; caso contrário, são explicitamente marcados como ignorados. Use uma base exclusiva com nome iniciado por `escala_test_` e defina sua conexão nessa variável. O fixture aplica migrations nessa base, sem apagar bases/tabelas preexistentes. Consulte `VALIDATION.md` para os resultados desta entrega e a forma de executar a integração.

## Preparar publicação

```powershell
./scripts/publish.ps1
```

O script gera uma pasta `publish/` com o backend e os arquivos React em `wwwroot`, servidos pela mesma origem. Isso evita expor cookies entre sites. Configure `Production`, `APP_URL` HTTPS, JWT, SMTP e banco no servidor; aplique migrations antes de iniciar. Execute `dotnet Api.dll` na pasta publicada, configurando o listener HTTPS/certificado. Para um proxy reverso, configure TLS, hosts permitidos e forwarded headers apenas para proxies confiáveis conforme seu ambiente; não confie indiscriminadamente em headers encaminhados. Como alternativa ao comando de migrations, `database/migrations.sql` contém o SQL idempotente gerado pelo EF Core para aplicação controlada no PostgreSQL.

Para publicar o **frontend na DigitalOcean e o backend na Vercel pelo GitHub Actions**, siga [DEPLOYMENT.md](DEPLOYMENT.md). O fluxo é `dev → hml → main`, com deploy somente em `main`. O backend mantém C#/.NET usando Container Images/Services da Vercel (beta). Sem domínio próprio, um endereço público Vercel encaminha páginas/assets à DigitalOcean e API ao serviço C#, preservando a mesma origem e os cookies de sessão. O React usa `VITE_API_URL` vazia nessa publicação e no desenvolvimento local.

O backend retorna ProblemDetails, valida entradas, limita tentativas de autenticação, exige header `X-CSRF: 1` e origem autorizada em POSTs de autenticação. O frontend mostra carregamento, erros recuperáveis e confirma exclusões/importações. Auditoria persiste a ação, ator e ID do recurso, sem senha, token ou conteúdo da escala.

Referências utilizadas: [JWT no ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0), [FluentValidation no ASP.NET](https://docs.fluentvalidation.net/en/latest/aspnet.html), [Tailwind com Vite](https://tailwindcss.com/docs/installation/using-vite).
