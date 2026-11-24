import type { Article, ArticleSection } from '../api';
import vetExamCat from '../assets/vet-exam-cat.webp';
import { useEffect, useRef } from 'react';
import type { ArticleItem } from '../types';
import { articleKey, articlePath, firstParagraph, renderArticleHtml } from '../shared/articles';
import { formatDate } from '../shared/date';

export function ArticlesIndexPage({ articleItems }: { articleItems: ArticleItem[] }) {
  const sortedItems = [...articleItems].sort((a, b) => (b.article.publishedAt ?? '').localeCompare(a.article.publishedAt ?? ''));

  return (
    <section className="section articleSections articleArchive">
      <div className="blogHeader">
        <p className="eyebrow">Poradna</p>
        <h1>Články</h1>
        <p>Praktické rady, prevence a novinky z ordinace na jednom místě.</p>
      </div>
      {sortedItems.length === 0 ? <p>Zatím nejsou publikované žádné články.</p> : <ArticleCards items={sortedItems} />}
    </section>
  );
}

export function ArticleCards({ items }: { items: ArticleItem[] }) {
  return (
    <div className="blogGrid">
      {items.map(({ article, section }) => (
        <article className="blogCard" key={articleKey(article)}>
          <a className="blogCardMedia" href={`/clanky/${articlePath(article)}`} aria-label={`Číst článek ${article.title}`}>
            <img src={vetExamCat} alt={`Ilustrační fotografie k článku ${article.title}`} loading="lazy" />
            <span>{section.title}</span>
          </a>
          <div className="blogCardBody">
            <time>{formatDate(article.publishedAt ?? new Date().toISOString())}</time>
            <h3><a href={`/clanky/${articlePath(article)}`}>{article.title}</a></h3>
            <p>{article.summary || firstParagraph(article.body)}</p>
            <a className="readMoreLink" href={`/clanky/${articlePath(article)}`}>Číst více</a>
          </div>
        </article>
      ))}
    </div>
  );
}

export function ArticleDetailPage({ article, section }: { article: Article; section: ArticleSection }) {
  const body = useRef<HTMLDivElement>(null);
  useEffect(() => {
    body.current?.querySelectorAll('table').forEach(table => {
      table.tabIndex = 0;
      if (!table.getAttribute('aria-label')) table.setAttribute('aria-label', table.caption?.textContent || 'Tabulka v článku');
    });
  }, [article.body]);
  return (
    <article className="section articleDetail">
      <a className="articleBackLink" href="/clanky">← Zpět na články</a>
      <div className="articleDetailHero">
        <div>
          <p className="eyebrow">{section.title}</p>
          <h1>{article.title}</h1>
          {(article.summary || firstParagraph(article.body)) && <p>{article.summary || firstParagraph(article.body)}</p>}
          <time>{formatDate(article.publishedAt ?? new Date().toISOString())}</time>
        </div>
        <figure>
          <img src={vetExamCat} alt={`Ilustrační fotografie k článku ${article.title}`} loading="eager" />
        </figure>
      </div>
      <div className="articleDetailBody">
        <div ref={body} className="articleContent" dangerouslySetInnerHTML={{ __html: renderArticleHtml(article.body) }} />
      </div>
    </article>
  );
}
