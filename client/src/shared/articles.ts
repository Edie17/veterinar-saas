import DOMPurify from 'dompurify';
import type { Article } from '../api';

export function articleKey(article: Article) {
  return `${article.id ?? article.slug ?? article.title}`;
}

export function articlePath(article: Article) {
  return article.slug || slugify(article.title) || articleKey(article);
}

export function getArticleSlug(pathname: string) {
  const match = pathname.match(/^\/clanky\/([^/]+)\/?$/);
  if (!match) return null;
  try { return decodeURIComponent(match[1]); } catch { return match[1]; }
}

export function slugify(value: string) {
  return value
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');
}

export function sanitizeArticleHtml(value: string) {
  return DOMPurify.sanitize(value, {
    USE_PROFILES: { html: true },
  });
}

export function renderArticleHtml(value: string) {
  return sanitizeArticleHtml(value);
}

export function firstParagraph(value: string) {
  const cleaned = DOMPurify.sanitize(value, { ALLOWED_TAGS: [] }).replace(/\s+/g, ' ').trim();
  return cleaned.length > 150 ? `${cleaned.slice(0, 150)}…` : cleaned;
}
