# Especificação aplicada

Referência: [Planejar melhorias do SaaS — conversa compartilhada](https://chatgpt.com/share/6aac9cde-0a0c-83e9-8a0e-752e2369686b). A página foi aberta e lida no navegador. O HTML original foi localizado em `Documents/agenda-enfermagem/minha_escala_app.html` e preservado integralmente em `legacy/`.

Decisões confirmadas na conversa: opção B, React/TypeScript/Vite/Tailwind; backend C#/.NET/ASP.NET Core; EF Core e FluentValidation; Neon/PostgreSQL; monólito organizado em Api/Application/Domain/Infrastructure; e-mail/senha; JWT + refresh token; cadastro/login/logout/recuperação/redefinição; controle de acesso e auditoria; tratamento de erros, carregamento e validação; responsividade para desktop/tablet/mobile; segredos via ambiente; **somente tema claro**; testes na etapa final.

Comportamentos concretos do HTML a preservar: calendário mensal com navegação, seleção do dia carregando o registro, uma folga ou plantão por data, setor opcional, atualização do registro existente, limpeza do formulário, próxima folga a partir de hoje, contadores de folgas/plantões/setores do mês, legendas por setor e até 12 registros futuros em ordem de data. Navegação mensal foi corrigida para evitar pular fevereiro ao partir do dia 31. A persistência passa do localStorage para uma escala pessoal vinculada ao usuário autenticado.

Requisitos adicionais descritos na entrega anterior do chat: .NET 10, migration inicial, refresh rotativo em cookie HttpOnly, senhas PBKDF2-SHA256 com salt, isolamento por TenantId/UserId, papéis Owner/Member, auditoria persistida com endpoint restrito ao Owner, SMTP com fallback apenas de desenvolvimento, `/health` verificando conexão PostgreSQL, CRUD incluindo exclusão e preservação do HTML em `legacy/`. Nesta implementação as senhas usam 600.000 iterações, acima das 210.000 citadas na conversa.

Cada conta independente cria uma organização pessoal com Owner; convites permitem cadastrar Members na mesma organização. Cada membro conserva a própria escala. Importação explícita do JSON antigo permite migrar registros sem transferir automaticamente dados de uma origem/navegador para outra e sem sobrescrever datas que já existem no banco.

Exemplos exploratórios do chat (agenda de clínica com pacientes/salas/horários, OAuth, Next.js, microserviços) não foram tratados como decisões desta escala. A stack escolhida e o comportamento real do HTML determinam o produto entregue.
