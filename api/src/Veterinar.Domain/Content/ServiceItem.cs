namespace Veterinar.Domain.Content;

public sealed class ServiceItem
{
    public int Id { get; set; }
    public Guid ClinicId { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public string Icon { get; set; } = "🐾";
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; } = true;
}
