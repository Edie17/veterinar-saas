using Veterinar.Domain.Content;

namespace Veterinar.Application.Abstractions;

/// <summary>Persistence port for public site content and CMS articles.</summary>
public interface ISiteRepository
{
    Task<PageContent?> FindContentAsync(Guid clinicId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ServiceItem>> GetPublishedServicesAsync(Guid clinicId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArticleSection>> GetSectionsWithArticlesAsync(Guid clinicId, bool publishedOnly, CancellationToken cancellationToken = default);
    Task<ArticleSection?> FindSectionAsync(Guid clinicId, int id, bool includeArticles, CancellationToken cancellationToken = default);
    Task<bool> SectionExistsAsync(Guid clinicId, int id, CancellationToken cancellationToken = default);
    Task<bool> SectionSlugExistsAsync(Guid clinicId, string slug, int excludingId, CancellationToken cancellationToken = default);
    Task<Article?> FindArticleAsync(Guid clinicId, int id, CancellationToken cancellationToken = default);
    Task<bool> ArticleSlugExistsAsync(Guid clinicId, string slug, int excludingId, CancellationToken cancellationToken = default);
    void Add(PageContent content);
    void Add(ArticleSection section);
    void Add(Article article);
    void Remove(ArticleSection section);
    void Remove(Article article);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
