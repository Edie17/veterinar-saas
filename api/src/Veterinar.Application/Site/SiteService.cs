using Veterinar.Application.Mapping;
using Ganss.Xss;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Veterinar.Application.Abstractions;
using Veterinar.Domain.Content;

namespace Veterinar.Application.Site;

public sealed class SiteService(ISiteRepository site, IClinicContext clinicContext, DtoMapper mapper)
{
    public async Task<SiteDto> GetPublicSiteAsync(CancellationToken ct = default)
    {
        var content = await site.FindContentAsync(clinicContext.ClinicId, ct)
            ?? new PageContent { ClinicId = clinicContext.ClinicId };
        var services = await site.GetPublishedServicesAsync(clinicContext.ClinicId, ct);
        var sections = await site.GetSectionsWithArticlesAsync(clinicContext.ClinicId, true, ct);

        var publishedSections = sections
            .Select(section =>
            {
                section.Articles = section.Articles
                    .Where(article => article.IsPublished).OrderBy(article => article.SortOrder)
                    .ThenByDescending(article => article.PublishedAt)
                    .ToList();
                return mapper.Map<ArticleSectionDto>(section);
            })
            .Where(section => section.Articles.Count > 0)
            .ToList();

        return new SiteDto(mapper.Map<PageContentDto>(content), mapper.Map<IReadOnlyList<ServiceItemDto>>(services), publishedSections);
    }

    public async Task<PageContentDto> UpdateContentAsync(UpdatePageContentRequest request, CancellationToken ct = default)
    {
        var content = await site.FindContentAsync(clinicContext.ClinicId, ct);
        if (content is null)
        {
            content = new PageContent { ClinicId = clinicContext.ClinicId };
            site.Add(content);
        }

        content.ClinicName = request.ClinicName.Trim();
        content.HeroTitle = request.HeroTitle.Trim();
        content.HeroText = request.HeroText.Trim();
        content.Phone = request.Phone.Trim();
        content.Email = request.Email.Trim();
        content.Address = request.Address.Trim();
        content.UpdatedAt = DateTimeOffset.UtcNow;
        await site.SaveChangesAsync(ct);
        return mapper.Map<PageContentDto>(content);
    }

    public async Task<IReadOnlyList<ArticleSectionDto>> GetAdminArticleSectionsAsync(CancellationToken ct = default)
    {
        var sections = await site.GetSectionsWithArticlesAsync(clinicContext.ClinicId, false, ct);

        return mapper.Map<IReadOnlyList<ArticleSectionDto>>(sections);
    }

    public async Task<ArticleSectionDto> UpsertArticleSectionAsync(UpsertArticleSectionRequest request, CancellationToken ct = default)
    {
        var clinicId = clinicContext.ClinicId;
        ArticleSection section;

        if (request.Id is { } id)
        {
            section = await site.FindSectionAsync(clinicId, id, true, ct)
                ?? throw new Veterinar.Application.Common.RequestException("Article section was not found.");
        }
        else
        {
            section = new ArticleSection { ClinicId = clinicId };
            site.Add(section);
        }

        section.Title = RequireTrimmed(request.Title, "Section title is required.");
        section.Slug = await EnsureUniqueSectionSlugAsync(clinicId, NormalizeSlug(request.Slug, section.Title), section.Id, ct);
        section.Description = (request.Description ?? "").Trim();
        section.SortOrder = request.SortOrder;
        section.IsPublished = request.IsPublished;

        await site.SaveChangesAsync(ct);
        return mapper.Map<ArticleSectionDto>(section);
    }

    public async Task DeleteArticleSectionAsync(int id, CancellationToken ct = default)
    {
        var section = await site.FindSectionAsync(clinicContext.ClinicId, id, false, ct);
        if (section is null) return;

        site.Remove(section);
        await site.SaveChangesAsync(ct);
    }

    public async Task<ArticleDto> UpsertArticleAsync(UpsertArticleRequest request, CancellationToken ct = default)
    {
        var clinicId = clinicContext.ClinicId;
        var sectionExists = await site.SectionExistsAsync(clinicId, request.ArticleSectionId, ct);
        if (!sectionExists)
        {
            throw new Veterinar.Application.Common.RequestException("Article section was not found.");
        }

        Article article;
        if (request.Id is { } id)
        {
            article = await site.FindArticleAsync(clinicId, id, ct)
                ?? throw new Veterinar.Application.Common.RequestException("Article was not found.");
        }
        else
        {
            article = new Article { ClinicId = clinicId, PublishedAt = DateTimeOffset.UtcNow };
            site.Add(article);
        }

        article.ArticleSectionId = request.ArticleSectionId;
        article.Title = RequireTrimmed(request.Title, "Article title is required.");
        article.Slug = await EnsureUniqueArticleSlugAsync(clinicId, NormalizeSlug(request.Slug, article.Title), article.Id, ct);
        article.Summary = (request.Summary ?? "").Trim();
        article.Body = SanitizeArticleBody(request.Body);
        article.SortOrder = request.SortOrder;
        article.IsPublished = request.IsPublished;
        article.UpdatedAt = DateTimeOffset.UtcNow;
        if (article.PublishedAt == default)
        {
            article.PublishedAt = DateTimeOffset.UtcNow;
        }

        await site.SaveChangesAsync(ct);
        return mapper.Map<ArticleDto>(article);
    }

    public async Task DeleteArticleAsync(int id, CancellationToken ct = default)
    {
        var article = await site.FindArticleAsync(clinicContext.ClinicId, id, ct);
        if (article is null) return;

        site.Remove(article);
        await site.SaveChangesAsync(ct);
    }

    private async Task<string> EnsureUniqueSectionSlugAsync(Guid clinicId, string slug, int currentId, CancellationToken ct)
    {
        var candidate = slug;
        var suffix = 2;
        while (await site.SectionSlugExistsAsync(clinicId, candidate, currentId, ct))
        {
            candidate = $"{slug}-{suffix++}";
        }
        return candidate;
    }

    private async Task<string> EnsureUniqueArticleSlugAsync(Guid clinicId, string slug, int currentId, CancellationToken ct)
    {
        var candidate = slug;
        var suffix = 2;
        while (await site.ArticleSlugExistsAsync(clinicId, candidate, currentId, ct))
        {
            candidate = $"{slug}-{suffix++}";
        }
        return candidate;
    }

    private static string NormalizeSlug(string? slug, string fallback)
    {
        var value = string.IsNullOrWhiteSpace(slug) ? fallback : slug;
        value = value
            .Replace("á", "a").Replace("č", "c").Replace("ď", "d").Replace("é", "e").Replace("ě", "e")
            .Replace("í", "i").Replace("ň", "n").Replace("ó", "o").Replace("ř", "r").Replace("š", "s")
            .Replace("ť", "t").Replace("ú", "u").Replace("ů", "u").Replace("ý", "y").Replace("ž", "z")
            .Replace("Á", "A").Replace("Č", "C").Replace("Ď", "D").Replace("É", "E").Replace("Ě", "E")
            .Replace("Í", "I").Replace("Ň", "N").Replace("Ó", "O").Replace("Ř", "R").Replace("Š", "S")
            .Replace("Ť", "T").Replace("Ú", "U").Replace("Ů", "U").Replace("Ý", "Y").Replace("Ž", "Z");
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        var ascii = builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        ascii = Regex.Replace(ascii, @"[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(ascii) ? "clanek" : ascii;
    }

    private static string RequireTrimmed(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new Veterinar.Application.Common.RequestException(message);
        return value.Trim();
    }

    private static string SanitizeArticleBody(string value)
    {
        var body = RequireTrimmed(value, "Article body is required.");
        var sanitizer = CreateArticleSanitizer();
        var sanitized = sanitizer.Sanitize(body).Trim();
        if (string.IsNullOrWhiteSpace(Regex.Replace(sanitized, "<[^>]+>", string.Empty)))
        {
            throw new Veterinar.Application.Common.RequestException("Article body is required.");
        }

        return sanitized;
    }

    private static HtmlSanitizer CreateArticleSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        foreach (var tag in new[] { "h1", "h2", "h3", "h4", "p", "br", "strong", "b", "em", "i", "u", "ul", "ol", "li", "a", "table", "thead", "tbody", "tr", "th", "td", "blockquote" })
        {
            sanitizer.AllowedTags.Add(tag);
        }

        sanitizer.AllowedAttributes.Clear();
        foreach (var attribute in new[] { "href", "target", "rel", "colspan", "rowspan" })
        {
            sanitizer.AllowedAttributes.Add(attribute);
        }

        sanitizer.AllowedSchemes.Clear();
        foreach (var scheme in new[] { "http", "https", "mailto", "tel" })
        {
            sanitizer.AllowedSchemes.Add(scheme);
        }

        return sanitizer;
    }
}
