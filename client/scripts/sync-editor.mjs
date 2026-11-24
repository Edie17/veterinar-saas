import { cpSync } from 'node:fs';
// The editor assets are shipped locally; keep them in step with the locked dependency.
cpSync(new URL('../node_modules/tinymce/', import.meta.url), new URL('../public/tinymce/', import.meta.url), { recursive: true, force: true });
