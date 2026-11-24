import { spawn } from 'node:child_process';
import { createServer } from 'node:net';
import { fileURLToPath } from 'node:url';
import { resolve } from 'node:path';

export const root = fileURLToPath(new URL('../', import.meta.url));
export const children = new Set();
export function start(command, args, env = {}, cwd = root) {
  const child = spawn(command, args, { cwd, env: { ...process.env, ...env }, stdio: 'inherit', windowsHide: true });
  children.add(child);
  child.once('error', error => { child.startError = error; children.delete(child); });
  child.once('exit', () => children.delete(child));
  return child;
}
export function run(command, args, env = {}, cwd = root) {
  return new Promise((ok, fail) => {
    const child = start(command, args, env, cwd);
    child.once('error', fail);
    child.once('exit', code => code === 0 ? ok() : fail(new Error(`${command} ${args.join(' ')} skončil s kódem ${code}`)));
  });
}
export function npmArgs(args) {
  if (!process.env.npm_execpath) throw new Error('Spusťte přes npm run dev nebo npm run qa.');
  return [process.env.npm_execpath, ...args];
}
export async function waitReady(url, child) {
  for (let i = 0; i < 240; i++) {
    if (child?.startError) throw child.startError;
    if (child?.exitCode !== null && child?.exitCode !== undefined) throw new Error(`Proces skončil před spuštěním ${url}.`);
    try { if ((await fetch(url, { signal: AbortSignal.timeout(1500) })).ok) return; } catch { /* server starting */ }
    await new Promise(r => setTimeout(r, 250));
  }
  throw new Error(`Server není dostupný: ${url}`);
}
export async function freePort() {
  const server = createServer();
  await new Promise(r => server.listen(0, '127.0.0.1', r));
  const port = server.address().port;
  await new Promise(r => server.close(r));
  return port;
}
export function stopChildren() {
  for (const child of children) {
    if (!child.pid) continue;
    if (process.platform === 'win32') spawn('taskkill', ['/pid', String(child.pid), '/T', '/F'], { windowsHide: true, stdio: 'ignore' });
    else child.kill('SIGTERM');
  }
}
export function demoEnvironment(db, origin) {
  return { ASPNETCORE_ENVIRONMENT: 'Development', ASPNETCORE_URLS: origin, DEMO_MODE: 'true',
    DATABASE_PROVIDER: 'sqlite', CONNECTION_STRING: `Data Source=${resolve(db)};Pooling=False`,
    ADMIN_EMAIL: 'admin@example.cz', ADMIN_PASSWORD: 'ChangeMe123!', COOKIE_SECURE: 'false', AllowedHosts: 'localhost;127.0.0.1',
    CSRF_SECRET: 'dev-only-local-demo-secret-never-production', PUBLIC_ORIGIN: origin, TRUSTED_PROXIES: '',
    SMTP_HOST: '', E2E_ADMIN_EMAIL: 'admin@example.cz', E2E_ADMIN_PASSWORD: 'ChangeMe123!' };
}
