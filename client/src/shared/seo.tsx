import { useEffect, useMemo } from 'react';
import type { Article, ArticleSection, SiteContent } from '../api';
import { articlePath, firstParagraph } from './articles';
import { siteOrigin } from './constants';

function upsertMeta(selector: string, attributes: Record<string, string>) {
  const head = document.head;
  let element = head.querySelector(selector) as HTMLMetaElement | null;
  if (!element) {
    element = document.createElement('meta');
    head.appendChild(element);
  }
  Object.entries(attributes).forEach(([key, value]) => element?.setAttribute(key, value));
}

function upsertCanonical(pathname: string) {
  let link = document.head.querySelector('link[rel="canonical"]') as HTMLLinkElement | null;
  if (!link) {
    link = document.createElement('link');
    link.rel = 'canonical';
    document.head.appendChild(link);
  }
  link.href = `${siteOrigin}${pathname}`;
}

export function SeoMeta({ title, description, pathname = window.location.pathname, type = 'website' }: { title: string; description: string; pathname?: string; type?: 'website' | 'article' }) {
  useEffect(() => {
    document.title = title;
    upsertMeta('meta[name="description"]', { name: 'description', content: description });
    upsertMeta('meta[property="og:title"]', { property: 'og:title', content: title });
    upsertMeta('meta[property="og:description"]', { property: 'og:description', content: description });
    upsertMeta('meta[property="og:type"]', { property: 'og:type', content: type });
    upsertMeta('meta[property="og:url"]', { property: 'og:url', content: `${siteOrigin}${pathname}` });
    upsertMeta('meta[name="twitter:title"]', { name: 'twitter:title', content: title });
    upsertMeta('meta[name="twitter:description"]', { name: 'twitter:description', content: description });
    upsertCanonical(pathname);
  }, [description, pathname, title, type]);

  return null;
}

export function StructuredData({ content, article, section }: { content?: SiteContent; article?: Article; section?: ArticleSection }) {
  const schema = useMemo(() => {
    const clinicName = content?.clinicName ?? 'Veterinární ordinace';
    const base = {
      '@context': 'https://schema.org',
      '@type': 'WebSite',
      name: clinicName,
      url: siteOrigin,
    };

    if (!article) return base;

    return [
      base,
      {
        '@context': 'https://schema.org',
        '@type': 'Article',
        headline: article.title,
        description: article.summary || firstParagraph(article.body),
        articleSection: section?.title,
        datePublished: article.publishedAt,
        dateModified: article.updatedAt ?? article.publishedAt,
        author: { '@type': 'Organization', name: clinicName },
        publisher: { '@type': 'Organization', name: clinicName },
        mainEntityOfPage: `${siteOrigin}/clanky/${articlePath(article)}`,
      },
    ];
  }, [article, content, section]);

  return <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(schema) }} />;
}
