import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { readFileSync, writeFileSync } from 'node:fs';

function run(script, env) {
  return spawnSync(process.execPath, [`.github/scripts/${script}.mjs`], {
    encoding: 'utf8', env: { ...process.env, APP_URL: '', REQUIRED_DEPLOY_KEYS: '', ...env },
  });
}
test('aceita o domínio padrão Vercel e exige uma origem HTTPS', () => {
  assert.equal(run('check-deploy-config', { APP_URL: 'https://minha-escala.vercel.app' }).status, 0);
  for (const APP_URL of ['', 'http://app.example.com', 'https://app.example.com/path', 'https://user:password@app.example.com', 'https://app.example.com/'])
    assert.notEqual(run('check-deploy-config', { APP_URL }).status, 0);
});
test('identifica secrets ausentes sem divulgar outros valores', () => {
  const result = run('check-deploy-config', { APP_URL: 'https://minha-escala.vercel.app', REQUIRED_DEPLOY_KEYS: 'DEPLOY_KEY,ABSENT_KEY', DEPLOY_KEY: 'sensitive-test-value', ABSENT_KEY: '' });
  assert.notEqual(result.status, 0); assert.match(result.stderr, /ABSENT_KEY/); assert.doesNotMatch(result.stderr, /sensitive-test-value/);
});
test('encaminha páginas à DigitalOcean mantendo API e health no serviço C#', () => {
  const original = readFileSync('vercel.json', 'utf8');
  try {
    assert.equal(run('render-vercel-config', { DO_FRONTEND_URL: 'https://minha-escala-test.ondigitalocean.app/' }).status, 0);
    const config = JSON.parse(readFileSync('vercel.json', 'utf8'));
    assert.deepEqual(config.rewrites.slice(0, 2), [
      { source: '/api/:path*', destination: { service: 'backend' } },
      { source: '/health', destination: { service: 'backend' } },
    ]);
    assert.equal(config.rewrites[2].destination, 'https://minha-escala-test.ondigitalocean.app/:path*');
    assert.equal(config.services.backend.runtime, 'container');
    assert.equal(config.git.deploymentEnabled, false);
  } finally { writeFileSync('vercel.json', original); }
});
test('não altera a configuração com uma origem DigitalOcean inválida', () => {
  const original = readFileSync('vercel.json', 'utf8');
  for (const DO_FRONTEND_URL of ['', 'http://app.example.com', 'https://app.example.com/path', 'https://user:password@app.example.com']) {
    assert.notEqual(run('render-vercel-config', { DO_FRONTEND_URL }).status, 0);
    assert.equal(readFileSync('vercel.json', 'utf8'), original);
  }
});
