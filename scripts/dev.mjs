import { mkdir } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { resolve } from 'node:path';
import { root, start, run, npmArgs, waitReady, demoEnvironment, stopChildren } from './runtime.mjs';

process.once('SIGINT', () => { stopChildren(); process.exit(0); });
process.once('SIGTERM', () => { stopChildren(); process.exit(0); });
try {
  await mkdir(resolve(root, '.local'), { recursive: true });
  if (!existsSync(resolve(root, 'client/node_modules/vite'))) await run(process.execPath, npmArgs(['ci', '--prefix', 'client']));
  let existingApi = false;
  let apiOccupied = false;
  try {
    const response = await fetch('http://127.0.0.1:5067/api/dev/status', { signal: AbortSignal.timeout(1000) });
    apiOccupied = true;
    existingApi = response.ok && (await response.json()).demo === true;
  } catch { /* start API */ }
  if (apiOccupied && !existingApi) throw new Error('Port 5067 používá jiný server. Před spuštěním dema jej zastavte.');
  if (!existingApi) {
    const env = demoEnvironment(resolve(root, '.local/veterina-demo.db'), 'http://127.0.0.1:5067');
    env.PUBLIC_ORIGIN = 'http://localhost:5173';
    const api = start('dotnet', ['run', '--project', 'api/src/Veterinar.Api', '--no-launch-profile'], env);
    await waitReady('http://127.0.0.1:5067/api/dev/status', api);
  }
  let existingClient = false;
  try { existingClient = (await fetch('http://localhost:5173', { signal: AbortSignal.timeout(1000) })).ok; } catch { /* start Vite */ }
  if (!existingClient) {
    const client = start(process.execPath, npmArgs(['run', 'dev:client', '--prefix', 'client', '--', '--strictPort']));
    await waitReady('http://localhost:5173', client);
  }
  console.log('\nDemo: http://localhost:5173 | CMS: /sprava | admin@example.cz / ChangeMe123!');
  console.log('Testovací schránka: http://localhost:5173/api/dev/mailbox/view\nData: .local/veterina-demo.db (oddělené demo).');
} catch (error) { console.error(error.message); stopChildren(); process.exitCode = 1; }
