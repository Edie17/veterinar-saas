import { mkdir, cp, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { root, run, start, npmArgs, waitReady, freePort, demoEnvironment, stopChildren } from './runtime.mjs';

const output = resolve(root, '.qa', new Date().toISOString().replaceAll(':', '-'));
const env = { E2E_RUN: 'false', DEMO_MODE: undefined };
process.once('SIGINT', () => { stopChildren(); process.exit(130); });
try {
  await mkdir(output, { recursive: true });
  await run('dotnet', ['restore', 'api/Veterinar.sln', '--locked-mode'], env);
  await run('dotnet', ['test', 'api/Veterinar.sln', '-c', 'Release', '--no-restore', '--logger', 'trx', '--results-directory', resolve(output, 'backend')], env);
  await run(process.execPath, npmArgs(['run', 'test', '--prefix', 'client', '--', '--reporter=default', '--reporter=junit', `--outputFile=${resolve(output, 'frontend.xml')}`]));
  await run(process.execPath, npmArgs(['run', 'typecheck', '--prefix', 'client']));
  await run(process.execPath, npmArgs(['run', 'build', '--prefix', 'client']));
  const publish = resolve(output, 'app');
  await run('dotnet', ['publish', 'api/src/Veterinar.Api', '-c', 'Release', '--no-restore', '-o', publish], env);
  await cp(resolve(root, 'client/dist'), resolve(publish, 'wwwroot'), { recursive: true });
  const origin = `http://127.0.0.1:${await freePort()}`;
  const api = start('dotnet', ['Veterinar.Api.dll'], demoEnvironment(resolve(output, 'qa.db'), origin), publish);
  await waitReady(`${origin}/api/dev/status`, api);
  await run('dotnet', ['test', 'api/tests/Veterinar.E2E.Tests', '-c', 'Release', '--no-build', '--logger', 'trx', '--results-directory', resolve(output, 'browser')],
    { E2E_RUN: 'true', E2E_BASE_URL: origin, E2E_ARTIFACTS: resolve(output, 'browser'), E2E_ADMIN_EMAIL: 'admin@example.cz', E2E_ADMIN_PASSWORD: 'ChangeMe123!' });
  await writeFile(resolve(output, 'result.json'), JSON.stringify({ success: true, origin, finishedAt: new Date().toISOString() }, null, 2));
  await writeFile(resolve(root, '.qa/latest.json'), JSON.stringify({ success: true, directory: output }, null, 2));
  console.log(`\nQA prošlo. Reporty: ${output}`);
} catch (error) {
  await writeFile(resolve(output, 'result.json'), JSON.stringify({ success: false, error: error.message }, null, 2));
  await writeFile(resolve(root, '.qa/latest.json'), JSON.stringify({ success: false, directory: output }, null, 2));
  console.error(error.message); process.exitCode = 1;
} finally { stopChildren(); }
