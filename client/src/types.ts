import type { Article, ArticleSection, ServiceItem, SiteContent } from './api';

export type SiteResponse = {
  content: SiteContent;
  services: ServiceItem[];
  articleSections: ArticleSection[];
};

export type Me = {
  email: string;
  role: string;
  clinicId: string;
};

export type CmsSection = 'dashboard' | 'content' | 'articles' | 'hours' | 'appointments' | 'settings';

export type ArticleItem = {
  article: Article;
  section: ArticleSection;
};
