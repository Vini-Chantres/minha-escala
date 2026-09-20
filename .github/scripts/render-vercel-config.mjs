import { readFileSync, writeFileSync } from 'node:fs';

const input = process.env.DO_FRONTEND_URL;
if (!input) throw new Error('A publicação DigitalOcean deve fornecer DO_FRONTEND_URL.');
const url = new URL(input);
if (url.protocol !== 'https:' || url.username || url.password || url.port || url.pathname !== '/' || url.search || url.hash)
  throw new Error('DO_FRONTEND_URL deve ser uma origem HTTPS válida.');
const config = JSON.parse(readFileSync('vercel.json', 'utf8'));
config.rewrites.find(route => route.source === '/').destination = `${url.origin}/`;
config.rewrites.find(route => route.source === '/:path*').destination = `${url.origin}/:path*`;
writeFileSync('vercel.json', `${JSON.stringify(config, null, 2)}\n`);
console.log('Rotas Vercel configuradas para o site estático DigitalOcean.');
