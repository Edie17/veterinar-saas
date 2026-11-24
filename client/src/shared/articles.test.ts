import { expect, it } from 'vitest';
import { getArticleSlug, sanitizeArticleHtml, slugify } from './articles';

it('removes executable article HTML while preserving useful content', () => {
  const clean = sanitizeArticleHtml('<h2>Rada</h2><script>alert(1)</script><a href="javascript:alert(1)" onclick="alert(1)">Odkaz</a>');
  expect(clean).toContain('<h2>Rada</h2>'); expect(clean).not.toMatch(/script|onclick|javascript:/i);
});
it('handles Czech slugs and malformed URL escapes without crashing', () => {
  expect(slugify('Péče o kočku')).toBe('pece-o-kocku');
  expect(getArticleSlug('/clanky/%E0%A4%A')).toBe('%E0%A4%A');
});
