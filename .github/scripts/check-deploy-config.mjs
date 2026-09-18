// Não imprima valores secretos, somente o nome da configuração ausente.
const config = process.env;
for (const key of ['APP_URL', ...(config.REQUIRED_DEPLOY_KEYS ?? '').split(',').filter(Boolean)]) {
  if (!config[key]?.trim()) throw new Error(`Configure ${key} no ambiente production do GitHub.`);
}
const url = new URL(config.APP_URL);
if (url.protocol !== 'https:' || url.username || url.password || url.port || url.pathname !== '/' || url.search || url.hash || config.APP_URL.endsWith('/'))
  throw new Error('APP_URL deve ser uma origem HTTPS sem barra final, caminho ou credenciais. Pode ser o domínio padrão da Vercel.');
console.log('Configuração de publicação válida.');
