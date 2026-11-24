namespace Veterinar.Domain.Content;

public sealed class ArticleSection
{
    public int Id { get; set; }
    public Guid ClinicId { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Description { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; } = true;
    public List<Article> Articles { get; set; } = [];
}
