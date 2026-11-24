namespace Veterinar.Domain.Content;

public sealed class Article
{
    public int Id { get; set; }
    public Guid ClinicId { get; set; }
    public int ArticleSectionId { get; set; }
    public ArticleSection? Section { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Body { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; } = true;
    public DateTimeOffset PublishedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
