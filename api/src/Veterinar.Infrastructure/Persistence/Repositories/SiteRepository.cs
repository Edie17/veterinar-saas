using Microsoft.EntityFrameworkCore;
using Veterinar.Application.Abstractions;
using Veterinar.Domain.Content;

namespace Veterinar.Infrastructure.Persistence.Repositories;

public sealed class SiteRepository(AppDbContext db) : ISiteRepository
{
    public Task<PageContent?> FindContentAsync(Guid clinicId, CancellationToken ct = default) => db.PageContents.SingleOrDefaultAsync(x => x.ClinicId == clinicId, ct);
    public async Task<IReadOnlyList<ServiceItem>> GetPublishedServicesAsync(Guid clinicId, CancellationToken ct = default) => await db.Services.AsNoTracking().Where(x => x.ClinicId == clinicId && x.IsPublished).OrderBy(x => x.SortOrder).ToListAsync(ct);
    public async Task<IReadOnlyList<ArticleSection>> GetSectionsWithArticlesAsync(Guid clinicId, bool publishedOnly, CancellationToken ct = default) => await db.ArticleSections.AsNoTracking().Include(x => x.Articles).Where(x => x.ClinicId == clinicId && (!publishedOnly || x.IsPublished)).OrderBy(x => x.SortOrder).ToListAsync(ct);
    public Task<ArticleSection?> FindSectionAsync(Guid clinicId, int id, bool includeArticles, CancellationToken ct = default)
    {
        IQueryable<ArticleSection> query = db.ArticleSections;
        if (includeArticles) query = query.Include(x => x.Articles);
        return query.SingleOrDefaultAsync(x => x.Id == id && x.ClinicId == clinicId, ct);
    }
    public Task<bool> SectionExistsAsync(Guid clinicId, int id, CancellationToken ct = default) => db.ArticleSections.AnyAsync(x => x.Id == id && x.ClinicId == clinicId, ct);
    public Task<bool> SectionSlugExistsAsync(Guid clinicId, string slug, int excludingId, CancellationToken ct = default) => db.ArticleSections.AnyAsync(x => x.ClinicId == clinicId && x.Slug == slug && x.Id != excludingId, ct);
    public Task<Article?> FindArticleAsync(Guid clinicId, int id, CancellationToken ct = default) => db.Articles.SingleOrDefaultAsync(x => x.Id == id && x.ClinicId == clinicId, ct);
    public Task<bool> ArticleSlugExistsAsync(Guid clinicId, string slug, int excludingId, CancellationToken ct = default) => db.Articles.AnyAsync(x => x.ClinicId == clinicId && x.Slug == slug && x.Id != excludingId, ct);
    public void Add(PageContent value) => db.PageContents.Add(value); public void Add(ArticleSection value) => db.ArticleSections.Add(value); public void Add(Article value) => db.Articles.Add(value); public void Remove(ArticleSection value) => db.ArticleSections.Remove(value); public void Remove(Article value) => db.Articles.Remove(value); public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
