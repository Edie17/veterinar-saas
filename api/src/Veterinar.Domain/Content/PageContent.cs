namespace Veterinar.Domain.Content;

public sealed class PageContent
{
    public int Id { get; set; }
    public Guid ClinicId { get; set; }
    public string ClinicName { get; set; } = "Veterinární ordinace Demo";
    public string HeroTitle { get; set; } = "Moderní ordinace pro psy, kočky a malé mazlíčky.";
    public string HeroText { get; set; } = "Objednejte se online na termín, který sedí vám i ordinačním hodinám.";
    public string Phone { get; set; } = "+420 000 000 000";
    public string Email { get; set; } = "ordinace@example.test";
    public string Address { get; set; } = "Ukázková adresa";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
