using System.ComponentModel.DataAnnotations;
namespace Veterinar.Application.Site;

public sealed record PageContentDto(int Id, string ClinicName, string HeroTitle, string HeroText, string Phone, string Email, string Address, DateTimeOffset UpdatedAt);
public sealed record ServiceItemDto(int Id, string Title, string Description, string Icon, int SortOrder, bool IsPublished);
public sealed record ArticleDto(int Id, int ArticleSectionId, string Title, string Slug, string Summary, string Body, int SortOrder, bool IsPublished, DateTimeOffset PublishedAt, DateTimeOffset UpdatedAt);
public sealed record ArticleSectionDto(int Id, string Title, string Slug, string Description, int SortOrder, bool IsPublished, IReadOnlyList<ArticleDto> Articles);
public sealed record SiteDto(PageContentDto Content, IReadOnlyList<ServiceItemDto> Services, IReadOnlyList<ArticleSectionDto> ArticleSections);
public sealed record UpdatePageContentRequest([property: Required, MaxLength(180)] string ClinicName, [property: Required, MaxLength(180)] string HeroTitle, [property: Required, MaxLength(2000)] string HeroText, [property: Required, MaxLength(40)] string Phone, [property: Required, MaxLength(180)] string Email, [property: Required, MaxLength(300)] string Address);
public sealed record UpsertArticleSectionRequest(int? Id, [property: Required, MaxLength(180)] string Title, [property: MaxLength(120)] string? Slug, [property: MaxLength(2000)] string Description, int SortOrder, bool IsPublished);
public sealed record UpsertArticleRequest(int? Id, int ArticleSectionId, [property: Required, MaxLength(180)] string Title, [property: MaxLength(120)] string? Slug, [property: MaxLength(2000)] string Summary, [property: Required, MaxLength(200000)] string Body, int SortOrder, bool IsPublished);
