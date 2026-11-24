using Veterinar.Application.Mapping;
using Microsoft.EntityFrameworkCore;
using Veterinar.Application.Abstractions;
using Veterinar.Application.Site;
using Veterinar.Domain.Appointments;
using Veterinar.Domain.Content;
using Veterinar.Domain.Scheduling;
using Veterinar.Domain.Clinics;
using Veterinar.Domain.Users;

namespace Veterinar.Application.Tests;

public sealed class SiteServiceTests
{
    [Fact]
    public async Task UpsertArticleAsync_Sanitizes_TinyMce_Html_On_Server()
    {
        var clinicId = Guid.NewGuid();
        await using var db = new FakeAppDbContext();
        var section = new ArticleSection
        {
            ClinicId = clinicId,
            Title = "Poradna",
            Slug = "poradna",
            Description = "",
            IsPublished = true,
            SortOrder = 1
        };
        db.ArticleSections.Add(section);
        await db.SaveChangesAsync();
        var service = CreateService(db, clinicId);

        await service.UpsertArticleAsync(new UpsertArticleRequest(
            null,
            section.Id,
            "Bezpečný článek",
            null,
            "",
            "<h2>Tipy</h2><p>Text</p><img src=x onerror=alert(1)><script>alert(1)</script><a href=\"javascript:alert(1)\">klik</a>",
            1,
            true));

        var article = await db.Articles.SingleAsync();
        Assert.Contains("<h2>Tipy</h2>", article.Body);
        Assert.Contains("<p>Text</p>", article.Body);
        Assert.DoesNotContain("<script", article.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onerror", article.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", article.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", article.Body, StringComparison.OrdinalIgnoreCase);
    }

    private static SiteService CreateService(FakeAppDbContext db, Guid clinicId) =>
        new(db, new FakeClinicContext(clinicId), CreateMapper());

    private static DtoMapper CreateMapper() => new DtoMapper();

    private sealed record FakeClinicContext(Guid ClinicId) : IClinicContext
    {
        public bool HasClinic => true;
    }

    private sealed class FakeAppDbContext : DbContext, ISiteRepository
    {
        public DbSet<PageContent> PageContents => Set<PageContent>();
        public DbSet<ServiceItem> Services => Set<ServiceItem>();
        public DbSet<ArticleSection> ArticleSections => Set<ArticleSection>();
        public DbSet<Article> Articles => Set<Article>();
        public Task<PageContent?> FindContentAsync(Guid id, CancellationToken ct = default) => PageContents.SingleOrDefaultAsync(x => x.ClinicId == id, ct);
        public async Task<IReadOnlyList<ServiceItem>> GetPublishedServicesAsync(Guid id, CancellationToken ct = default) => await Services.Where(x => x.ClinicId == id && x.IsPublished).ToListAsync(ct);
        public async Task<IReadOnlyList<ArticleSection>> GetSectionsWithArticlesAsync(Guid id, bool publishedOnly, CancellationToken ct = default) => await ArticleSections.Include(x => x.Articles).Where(x => x.ClinicId == id && (!publishedOnly || x.IsPublished)).ToListAsync(ct);
        public Task<ArticleSection?> FindSectionAsync(Guid clinicId, int id, bool includeArticles, CancellationToken ct = default) => ArticleSections.Include(x => x.Articles).SingleOrDefaultAsync(x => x.ClinicId == clinicId && x.Id == id, ct);
        public Task<bool> SectionExistsAsync(Guid c, int id, CancellationToken ct = default) => ArticleSections.AnyAsync(x => x.ClinicId == c && x.Id == id, ct);
        public Task<bool> SectionSlugExistsAsync(Guid c, string slug, int except, CancellationToken ct = default) => ArticleSections.AnyAsync(x => x.ClinicId == c && x.Slug == slug && x.Id != except, ct);
        public Task<Article?> FindArticleAsync(Guid c, int id, CancellationToken ct = default) => Articles.SingleOrDefaultAsync(x => x.ClinicId == c && x.Id == id, ct);
        public Task<bool> ArticleSlugExistsAsync(Guid c, string slug, int except, CancellationToken ct = default) => Articles.AnyAsync(x => x.ClinicId == c && x.Slug == slug && x.Id != except, ct);
        public void Add(PageContent x) => PageContents.Add(x); public void Add(ArticleSection x) => ArticleSections.Add(x); public void Add(Article x) => Articles.Add(x); public void Remove(ArticleSection x) => ArticleSections.Remove(x); public void Remove(Article x) => Articles.Remove(x);
        Task ISiteRepository.SaveChangesAsync(CancellationToken ct) => base.SaveChangesAsync(ct);

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        }
    }
}
