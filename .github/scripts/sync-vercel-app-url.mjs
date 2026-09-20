import { pathToFileURL } from 'node:url';

export async function syncVercelAppUrl(env = process.env, fetchImpl = fetch) {
  const { APP_URL, VERCEL_ORG_ID, VERCEL_PROJECT_ID, VERCEL_TOKEN } = env;
  for (const [key, value] of Object.entries({ APP_URL, VERCEL_ORG_ID, VERCEL_PROJECT_ID, VERCEL_TOKEN })) {
    if (!value) throw new Error(`${key} não configurado.`);
  }

  const endpoint = new URL(`https://api.vercel.com/v10/projects/${encodeURIComponent(VERCEL_PROJECT_ID)}/env`);
  endpoint.searchParams.set('upsert', 'true');
  endpoint.searchParams.set('teamId', VERCEL_ORG_ID);

  const response = await fetchImpl(endpoint, {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${VERCEL_TOKEN}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      key: 'APP_URL',
      value: APP_URL,
      type: 'plain',
      target: ['production'],
    }),
  });

  if (!response.ok) throw new Error(`A API da Vercel respondeu HTTP ${response.status}.`);
  console.log('APP_URL de produção sincronizada na Vercel.');
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  syncVercelAppUrl().catch(error => {
    console.error(error.message);
    process.exitCode = 1;
  });
}
